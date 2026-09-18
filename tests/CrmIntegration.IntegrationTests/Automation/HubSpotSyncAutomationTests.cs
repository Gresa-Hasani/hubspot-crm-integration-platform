using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Enums;
using CrmIntegration.IntegrationTests.Sync;
using Xunit;

namespace CrmIntegration.IntegrationTests.Automation;

/// <summary>
/// Verifies the HubSpot -> internal sync path (Phase 4's DealSyncService, against a fake HubSpot
/// client + real PostgreSQL) drives the same Phase 6 automation pipeline as the internal REST
/// path — not a reimplementation. This is business-workflow verification of the sync-triggered
/// path; it is not a webhook-delivery test (Phase 5's webhook ingestion is exercised separately).
/// </summary>
public class HubSpotSyncAutomationTests
{
    [Fact]
    public async Task SyncFromHubSpot_ImportedDealAlreadyClosedWon_CreatesTransitionAutomationAndOnboarding()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = $"HubSpot Sync Co {suffix}" });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);
        var companySyncResponse = await client.PostAsync($"/api/sync/companies/{company!.Id}/to-hubspot", null);
        var companySyncJob = await companySyncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
        var companyHubSpotId = companySyncJob!.ExternalId!;

        var dealRecord = await factory.HubSpotClient.CreateDealAsync(new Dictionary<string, string?>
        {
            ["dealname"] = $"HubSpot Closed Deal {suffix}",
            ["amount"] = "50000",
            ["dealstage"] = "closedwon"
        });
        await factory.HubSpotClient.CreateAssociationAsync(HubSpotObjectType.Deal, dealRecord.Id, HubSpotObjectType.Company, companyHubSpotId);

        var syncResponse = await client.PostAsync($"/api/sync/hubspot/deals/{dealRecord.Id}", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var syncJob = await syncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(SyncStatus.Succeeded, syncJob!.Status);
        var dealId = syncJob.InternalEntityId!.Value;

        var dealResponse = await client.GetAsync($"/api/deals/{dealId}");
        var deal = await dealResponse.Content.ReadFromJsonAsync<DealResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(DealStage.ClosedWon, deal!.Stage);
        Assert.Equal(company.Id, deal.CompanyId);

        var historyResponse = await client.GetAsync($"/api/deals/{dealId}/stage-history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DealStageTransitionResponse>>(CrmApiFactory.JsonOptions);
        var transition = Assert.Single(history!);
        Assert.Null(transition.FromStage); // brand-new import: no prior known stage
        Assert.Equal(DealStage.ClosedWon, transition.ToStage);
        Assert.Equal(TransitionSource.HubSpotSync, transition.Source);

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        var execution = Assert.Single(executions!, e => e.EntityId == dealId && e.AutomationType == AutomationType.DealClosedWonOnboarding);
        Assert.Equal(AutomationStatus.Succeeded, execution.Status);

        var onboardingResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingRecords = await onboardingResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        var onboarding = Assert.Single(onboardingRecords!, o => o.DealId == dealId);
        Assert.Equal(company.Id, onboarding.CompanyId);

        // Repeat the sync (no HubSpot-side change): must not create a second transition, a second
        // AutomationExecution for this idempotency key, or a second OnboardingRecord.
        var secondSyncResponse = await client.PostAsync($"/api/sync/hubspot/deals/{dealRecord.Id}", null);
        Assert.Equal(HttpStatusCode.OK, secondSyncResponse.StatusCode);

        var historyAfterResponse = await client.GetAsync($"/api/deals/{dealId}/stage-history");
        var historyAfter = await historyAfterResponse.Content.ReadFromJsonAsync<List<DealStageTransitionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(historyAfter!);

        var executionsAfterResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executionsAfter = await executionsAfterResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(executionsAfter!.Where(e => e.EntityId == dealId && e.AutomationType == AutomationType.DealClosedWonOnboarding));

        var onboardingAfterResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingAfter = await onboardingAfterResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(onboardingAfter!.Where(o => o.DealId == dealId));
    }

    [Fact]
    public async Task SyncFromHubSpot_ExistingDealTransitionsToClosedWon_RecordsFromStage()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var dealRecord = await factory.HubSpotClient.CreateDealAsync(new Dictionary<string, string?>
        {
            ["dealname"] = $"HubSpot Progressing Deal {suffix}",
            ["amount"] = "10000",
            ["dealstage"] = "contractsent" // Negotiation
        });

        var firstSyncResponse = await client.PostAsync($"/api/sync/hubspot/deals/{dealRecord.Id}", null);
        var firstSyncJob = await firstSyncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
        var dealId = firstSyncJob!.InternalEntityId!.Value;

        await factory.HubSpotClient.UpdateDealAsync(dealRecord.Id, new Dictionary<string, string?> { ["dealstage"] = "closedwon" });
        var secondSyncResponse = await client.PostAsync($"/api/sync/hubspot/deals/{dealRecord.Id}", null);
        Assert.Equal(HttpStatusCode.OK, secondSyncResponse.StatusCode);

        var historyResponse = await client.GetAsync($"/api/deals/{dealId}/stage-history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DealStageTransitionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Equal(2, history!.Count);
        Assert.Equal(DealStage.Negotiation, history[1].FromStage);
        Assert.Equal(DealStage.ClosedWon, history[1].ToStage);

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(executions!.Where(e => e.EntityId == dealId && e.AutomationType == AutomationType.DealClosedWonOnboarding));
    }
}
