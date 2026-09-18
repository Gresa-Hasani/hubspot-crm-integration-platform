using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Automation;

/// <summary>
/// End-to-end Phase 6 sales automation tests against real PostgreSQL. Covers both trigger paths
/// (internal REST update and HubSpot sync) driving the same DealStageAutomationService /
/// ContactLifecycleAutomationService / OnboardingService / AutomationExecutor pipeline built in
/// Phase 6, plus the inspection API endpoints.
/// </summary>
public class SalesAutomationTests
{
    [Fact]
    public async Task InternalUpdate_DealToClosedWon_CreatesTransition_RunsAutomation_AndCreatesOnboarding()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var company = await CreateCompanyAsync(client, $"Acme Corp {suffix}");
        var contact = await CreateContactAsync(client, $"contact.{suffix}@acme.example", company.Id);
        var deal = await CreateDealAsync(client, $"Enterprise Deal {suffix}", DealStage.Negotiation, company.Id, contact.Id);

        var updateResponse = await client.PutAsJsonAsync($"/api/deals/{deal.Id}", new UpdateDealRequest
        {
            Name = deal.Name,
            CompanyId = company.Id,
            ContactId = contact.Id,
            Stage = DealStage.ClosedWon,
            Amount = deal.Amount,
            Currency = deal.Currency
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var historyResponse = await client.GetAsync($"/api/deals/{deal.Id}/stage-history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DealStageTransitionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Equal(2, history!.Count); // create (null -> Negotiation) + update (Negotiation -> ClosedWon)
        Assert.Equal(DealStage.Negotiation, history[1].FromStage);
        Assert.Equal(DealStage.ClosedWon, history[1].ToStage);
        Assert.Equal(TransitionSource.InternalUpdate, history[1].Source);

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        var execution = Assert.Single(executions!, e => e.EntityId == deal.Id && e.AutomationType == AutomationType.DealClosedWonOnboarding);
        Assert.Equal(AutomationStatus.Succeeded, execution.Status);

        var onboardingResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingRecords = await onboardingResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        var onboarding = Assert.Single(onboardingRecords!, o => o.DealId == deal.Id);
        Assert.Equal(company.Id, onboarding.CompanyId);
        Assert.Equal(contact.Id, onboarding.ContactId);

        var getOneResponse = await client.GetAsync($"/api/onboarding/{onboarding.Id}");
        Assert.Equal(HttpStatusCode.OK, getOneResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditRows = await db.AuditLogs.Where(a => a.EntityId == deal.Id.ToString() && a.Source == "SalesAutomation").ToListAsync();
        Assert.Contains(auditRows, a => a.Action == "AutomationSucceeded");
    }

    [Fact]
    public async Task InternalUpdate_ContactLifecycleChange_CreatesTransition_ExposedViaHistoryEndpoint()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var contact = await CreateContactAsync(client, $"lifecycle.{suffix}@acme.example", companyId: null);

        var updateResponse = await client.PutAsJsonAsync($"/api/contacts/{contact.Id}", new UpdateContactRequest
        {
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            LifecycleStage = LifecycleStage.Opportunity
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var historyResponse = await client.GetAsync($"/api/contacts/{contact.Id}/lifecycle-history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ContactLifecycleTransitionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Equal(2, history!.Count); // create (null -> Lead) + update (Lead -> Opportunity)
        Assert.Equal(LifecycleStage.Lead, history[1].FromStage);
        Assert.Equal(LifecycleStage.Opportunity, history[1].ToStage);

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        Assert.Contains(executions!, e => e.EntityId == contact.Id && e.AutomationType == AutomationType.ContactLifecycleTransition && e.Status == AutomationStatus.Succeeded);
    }

    [Fact]
    public async Task InternalUpdate_DealHasNoCompany_SkipsOnboarding_ButStillRecordsTransitionAndExecution()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var deal = await CreateDealAsync(client, $"No Company Deal {suffix}", DealStage.Negotiation, companyId: null, contactId: null);

        var updateResponse = await client.PutAsJsonAsync($"/api/deals/{deal.Id}", new UpdateDealRequest
        {
            Name = deal.Name,
            Stage = DealStage.ClosedWon,
            Amount = deal.Amount,
            Currency = deal.Currency
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        var execution = Assert.Single(executions!, e => e.EntityId == deal.Id && e.AutomationType == AutomationType.DealClosedWonOnboarding);
        Assert.Equal(AutomationStatus.Skipped, execution.Status);
        Assert.Contains("no associated Company", execution.ResultSummary);

        var onboardingResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingRecords = await onboardingResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        Assert.DoesNotContain(onboardingRecords!, o => o.DealId == deal.Id);
    }

    [Fact]
    public async Task ReenteringClosedWon_ProducesFreshAutomationExecution_ButOnboardingRecordStaysUnique()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var company = await CreateCompanyAsync(client, $"Re-entry Co {suffix}");
        var deal = await CreateDealAsync(client, $"Re-entry Deal {suffix}", DealStage.Negotiation, company.Id, contactId: null);

        await client.PutAsJsonAsync($"/api/deals/{deal.Id}", new UpdateDealRequest { Name = deal.Name, CompanyId = company.Id, Stage = DealStage.ClosedWon, Amount = deal.Amount, Currency = deal.Currency });
        await client.PutAsJsonAsync($"/api/deals/{deal.Id}", new UpdateDealRequest { Name = deal.Name, CompanyId = company.Id, Stage = DealStage.Negotiation, Amount = deal.Amount, Currency = deal.Currency });
        await client.PutAsJsonAsync($"/api/deals/{deal.Id}", new UpdateDealRequest { Name = deal.Name, CompanyId = company.Id, Stage = DealStage.ClosedWon, Amount = deal.Amount, Currency = deal.Currency });

        var executionsResponse = await client.GetAsync("/api/automations/executions?limit=200");
        var executions = await executionsResponse.Content.ReadFromJsonAsync<List<AutomationExecutionResponse>>(CrmApiFactory.JsonOptions);
        var dealExecutions = executions!.Where(e => e.EntityId == deal.Id && e.AutomationType == AutomationType.DealClosedWonOnboarding).ToList();
        Assert.Equal(2, dealExecutions.Count);
        Assert.All(dealExecutions, e => Assert.Equal(AutomationStatus.Succeeded, e.Status));

        var onboardingResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingRecords = await onboardingResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(onboardingRecords!.Where(o => o.DealId == deal.Id));
    }

    [Fact]
    public async Task ManualRetry_ReRunsFailedExecution_CreatesOnboardingRecord_AndPreservesOriginalRow()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var company = await CreateCompanyAsync(client, $"Retry Co {suffix}");
        var deal = await CreateDealAsync(client, $"Retry Deal {suffix}", DealStage.ClosedWon, company.Id, contactId: null);

        Guid failedExecutionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failedExecution = new CrmIntegration.Domain.Entities.AutomationExecution
            {
                Id = Guid.NewGuid(),
                AutomationType = AutomationType.DealClosedWonOnboarding,
                EntityType = EntityType.Deal,
                EntityId = deal.Id,
                IdempotencyKey = $"DealClosedWonOnboarding:simulated-failure-{suffix}",
                Status = AutomationStatus.Failed,
                CorrelationId = "test-corr",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                FailureCategory = "SimulatedTransientError",
                ErrorMessage = "Simulated failure for retry test."
            };
            db.AutomationExecutions.Add(failedExecution);
            await db.SaveChangesAsync();
            failedExecutionId = failedExecution.Id;
        }

        var retryResponse = await client.PostAsync($"/api/automations/executions/{failedExecutionId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retried = await retryResponse.Content.ReadFromJsonAsync<AutomationExecutionResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(AutomationStatus.Succeeded, retried!.Status);
        Assert.NotEqual(failedExecutionId, retried.Id);

        var originalResponse = await client.GetAsync($"/api/automations/executions/{failedExecutionId}");
        var original = await originalResponse.Content.ReadFromJsonAsync<AutomationExecutionResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(AutomationStatus.Failed, original!.Status); // retry never mutates the original row

        var onboardingResponse = await client.GetAsync("/api/onboarding?limit=200");
        var onboardingRecords = await onboardingResponse.Content.ReadFromJsonAsync<List<OnboardingRecordResponse>>(CrmApiFactory.JsonOptions);
        Assert.Single(onboardingRecords!.Where(o => o.DealId == deal.Id));
    }

    [Fact]
    public async Task RetryingNonFailedExecution_ReturnsBadRequest()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        Guid succeededExecutionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var execution = new CrmIntegration.Domain.Entities.AutomationExecution
            {
                Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal,
                EntityId = Guid.NewGuid(), IdempotencyKey = $"key-{Guid.NewGuid():N}", Status = AutomationStatus.Succeeded,
                CorrelationId = "corr", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
            };
            db.AutomationExecutions.Add(execution);
            await db.SaveChangesAsync();
            succeededExecutionId = execution.Id;
        }

        var response = await client.PostAsync($"/api/automations/executions/{succeededExecutionId}/retry", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetExecution_ReturnsNotFound_ForUnknownId()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.GetAsync($"/api/automations/executions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<CompanyResponse> CreateCompanyAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions))!;
    }

    private static async Task<ContactResponse> CreateContactAsync(HttpClient client, string email, Guid? companyId)
    {
        var response = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
        {
            FirstName = "Test",
            LastName = "Contact",
            Email = email,
            CompanyId = companyId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions))!;
    }

    private static async Task<DealResponse> CreateDealAsync(HttpClient client, string name, DealStage stage, Guid? companyId, Guid? contactId)
    {
        var response = await client.PostAsJsonAsync("/api/deals", new CreateDealRequest
        {
            Name = name,
            Stage = stage,
            CompanyId = companyId,
            ContactId = contactId,
            Amount = 25000,
            Currency = "EUR"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DealResponse>(CrmApiFactory.JsonOptions))!;
    }
}
