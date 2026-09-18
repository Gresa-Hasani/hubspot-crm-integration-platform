using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CrmIntegration.IntegrationTests.Sync;

/// <summary>
/// Exercises the real synchronization engine (real PostgreSQL + real HubSpot API) end to end.
/// A no-op unless HubSpot__AccessToken in the environment is a real token — see
/// HubSpotLiveVerificationTests (Phase 3) for the same pattern. All objects are fictional test
/// data and are deleted from both HubSpot and PostgreSQL at the end of the test.
///
///   $env:HubSpot__AccessToken = "&lt;your real private-app token&gt;"
///   dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~LiveSyncVerification
/// </summary>
public class LiveSyncVerificationTests
{
    private readonly ITestOutputHelper _output;

    public LiveSyncVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FullBidirectionalSyncFlow_AgainstRealHubSpotAndRealPostgres()
    {
        var accessToken = Environment.GetEnvironmentVariable("HubSpot__AccessToken");
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Contains("00000000"))
        {
            _output.WriteLine("SKIPPED (no-op): set a real HubSpot__AccessToken environment variable to run this against a live portal.");
            return;
        }

        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(CrmIntegration.Domain.Enums.UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createdHubSpotIds = new List<(HubSpotObjectType Type, string Id)>();
        var createdInternalCompanyIds = new List<Guid>();
        var createdInternalContactIds = new List<Guid>();
        var createdInternalDealIds = new List<Guid>();

        try
        {
            // --- A. Internal -> HubSpot ---
            var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest
            {
                Name = $"Live Sync Test Co {suffix}",
                Domain = $"live-sync-test-{suffix}.com"
            });
            var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);
            createdInternalCompanyIds.Add(company!.Id);

            var contactResponse = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
            {
                FirstName = "Live",
                LastName = $"SyncTest {suffix}",
                Email = $"live.sync.{suffix}@example.com",
                CompanyId = company.Id
            });
            var contact = await contactResponse.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions);
            createdInternalContactIds.Add(contact!.Id);

            var dealResponse = await client.PostAsJsonAsync("/api/deals", new CreateDealRequest
            {
                Name = $"Live Sync Test Deal {suffix}",
                Amount = 12345,
                Currency = "EUR",
                CompanyId = company.Id,
                ContactId = contact.Id
            });
            var deal = await dealResponse.Content.ReadFromJsonAsync<DealResponse>(CrmApiFactory.JsonOptions);
            createdInternalDealIds.Add(deal!.Id);

            var companySyncJob = await (await client.PostAsync($"/api/sync/companies/{company.Id}/to-hubspot", null))
                .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
            Assert.Equal(SyncStatus.Succeeded, companySyncJob!.Status);
            createdHubSpotIds.Add((HubSpotObjectType.Company, companySyncJob.ExternalId!));
            _output.WriteLine($"Synced company -> HubSpot {companySyncJob.ExternalId}");

            var contactSyncJob = await (await client.PostAsync($"/api/sync/contacts/{contact.Id}/to-hubspot", null))
                .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
            Assert.Equal(SyncStatus.Succeeded, contactSyncJob!.Status);
            createdHubSpotIds.Add((HubSpotObjectType.Contact, contactSyncJob.ExternalId!));
            _output.WriteLine($"Synced contact -> HubSpot {contactSyncJob.ExternalId}");

            var dealSyncJob = await (await client.PostAsync($"/api/sync/deals/{deal.Id}/to-hubspot", null))
                .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
            Assert.Equal(SyncStatus.Succeeded, dealSyncJob!.Status);
            createdHubSpotIds.Add((HubSpotObjectType.Deal, dealSyncJob.ExternalId!));
            _output.WriteLine($"Synced deal -> HubSpot {dealSyncJob.ExternalId}");

            using (var scope = factory.Services.CreateScope())
            {
                var hubSpotClient = scope.ServiceProvider.GetRequiredService<IHubSpotClient>();

                var remoteContact = await hubSpotClient.GetContactAsync(contactSyncJob.ExternalId!);
                Assert.NotNull(remoteContact);

                var contactCompanies = await hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Contact, contactSyncJob.ExternalId!, HubSpotObjectType.Company);
                Assert.Contains(companySyncJob.ExternalId, contactCompanies);
                _output.WriteLine("Verified Contact -> Company association");

                var dealCompanies = await hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Deal, dealSyncJob.ExternalId!, HubSpotObjectType.Company);
                var dealContacts = await hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Deal, dealSyncJob.ExternalId!, HubSpotObjectType.Contact);
                Assert.Contains(companySyncJob.ExternalId, dealCompanies);
                Assert.Contains(contactSyncJob.ExternalId, dealContacts);
                _output.WriteLine("Verified Deal -> Company and Deal -> Contact associations");
            }

            // Update internal data, sync again, verify HubSpot update (not a duplicate create).
            await client.PutAsJsonAsync($"/api/companies/{company.Id}", new UpdateCompanyRequest { Name = $"Live Sync Test Co {suffix} (Updated)" });
            var secondCompanySync = await (await client.PostAsync($"/api/sync/companies/{company.Id}/to-hubspot", null))
                .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
            Assert.Equal(companySyncJob.ExternalId, secondCompanySync!.ExternalId);
            _output.WriteLine("Re-synced updated company -> same HubSpot id (no duplicate)");

            using (var scope = factory.Services.CreateScope())
            {
                var hubSpotClient = scope.ServiceProvider.GetRequiredService<IHubSpotClient>();
                var updatedRemoteCompany = await hubSpotClient.GetCompanyAsync(companySyncJob.ExternalId!, ["name"]);
                Assert.Equal($"Live Sync Test Co {suffix} (Updated)", updatedRemoteCompany!.Properties["name"]);
                _output.WriteLine("Verified HubSpot company reflects the internal update");
            }

            // --- B. HubSpot -> Internal ---
            using (var scope = factory.Services.CreateScope())
            {
                var hubSpotClient = scope.ServiceProvider.GetRequiredService<IHubSpotClient>();
                var importCompanyRecord = await hubSpotClient.CreateCompanyAsync(new Dictionary<string, string?>
                {
                    ["name"] = $"Live Import Test Co {suffix}",
                    ["domain"] = $"live-import-test-{suffix}.com"
                });
                createdHubSpotIds.Add((HubSpotObjectType.Company, importCompanyRecord.Id));

                var importJob = await (await client.PostAsync($"/api/sync/hubspot/companies/{importCompanyRecord.Id}", null))
                    .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
                Assert.Equal(SyncStatus.Succeeded, importJob!.Status);
                Assert.NotNull(importJob.InternalEntityId);
                createdInternalCompanyIds.Add(importJob.InternalEntityId!.Value);
                _output.WriteLine($"Imported HubSpot company {importCompanyRecord.Id} -> internal {importJob.InternalEntityId}");

                var secondImportJob = await (await client.PostAsync($"/api/sync/hubspot/companies/{importCompanyRecord.Id}", null))
                    .Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
                Assert.Equal(importJob.InternalEntityId, secondImportJob!.InternalEntityId);
                _output.WriteLine("Re-imported same HubSpot company -> same internal id (idempotent)");

                using var innerScope = factory.Services.CreateScope();
                var db = innerScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var mappingCount = await db.EntityMappings.CountAsync(m => m.ExternalId == importCompanyRecord.Id);
                Assert.Equal(1, mappingCount);
            }

            // --- C. Harmless real failure path ---
            var badEmailContactResponse = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
            {
                FirstName = "Bad",
                LastName = $"Email {suffix}",
                Email = $"bad.{suffix}@bad-email-test.invalid" // .invalid is a reserved, non-resolvable TLD (RFC 2606) — HubSpot rejects it
            });
            var badEmailContact = await badEmailContactResponse.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions);
            createdInternalContactIds.Add(badEmailContact!.Id);

            var failedSyncResponse = await client.PostAsync($"/api/sync/contacts/{badEmailContact.Id}/to-hubspot", null);
            Assert.Equal(System.Net.HttpStatusCode.BadGateway, failedSyncResponse.StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var failedJob = await db.SyncJobs
                    .Where(j => j.InternalEntityId == badEmailContact.Id)
                    .OrderByDescending(j => j.StartedAt)
                    .FirstAsync();
                Assert.Equal(SyncStatus.Failed, failedJob.Status);
                Assert.Equal("BadRequest", failedJob.FailureCategory);
                _output.WriteLine($"Verified harmless real HubSpot 400 failure recorded as SyncJob.Failed/BadRequest: {failedJob.ErrorMessage}");
            }
        }
        finally
        {
            // Clean up HubSpot first (best-effort), then PostgreSQL.
            using var scope = factory.Services.CreateScope();
            var hubSpotClient = scope.ServiceProvider.GetRequiredService<IHubSpotClient>();
            foreach (var (type, id) in createdHubSpotIds)
            {
                try
                {
                    await DeleteHubSpotObjectAsync(accessToken!, type, id);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Cleanup warning: failed to delete HubSpot {type} {id}: {ex.Message}");
                }
            }

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Deals.RemoveRange(db.Deals.Where(d => createdInternalDealIds.Contains(d.Id)));
            db.Contacts.RemoveRange(db.Contacts.Where(c => createdInternalContactIds.Contains(c.Id)));
            db.Companies.RemoveRange(db.Companies.Where(c => createdInternalCompanyIds.Contains(c.Id)));
            db.EntityMappings.RemoveRange(db.EntityMappings.Where(m =>
                createdInternalCompanyIds.Contains(m.InternalId) ||
                createdInternalContactIds.Contains(m.InternalId) ||
                createdInternalDealIds.Contains(m.InternalId)));
            await db.SaveChangesAsync();
            _output.WriteLine("Cleaned up all test records from HubSpot and PostgreSQL.");
        }
    }

    private static async Task DeleteHubSpotObjectAsync(string accessToken, HubSpotObjectType type, string id)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.hubapi.com") };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        await httpClient.DeleteAsync($"/crm/v3/objects/{type.ToApiPath()}/{id}");
    }
}
