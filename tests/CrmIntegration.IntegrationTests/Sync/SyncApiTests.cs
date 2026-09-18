using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Sync;

public class SyncApiTests
{
    [Fact]
    public async Task SyncCompanyToHubSpot_CreatesRemoteObject_AndPersistsMapping()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest
        {
            Name = "Sync Test Co " + Guid.NewGuid().ToString("N")[..8]
        });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);

        var syncResponse = await client.PostAsync($"/api/sync/companies/{company!.Id}/to-hubspot", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var job = await syncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);

        Assert.Equal(SyncStatus.Succeeded, job!.Status);
        Assert.NotNull(job.ExternalId);
        Assert.Equal(1, factory.HubSpotClient.CreateCallCount);
    }

    [Fact]
    public async Task SyncCompanyToHubSpot_IsIdempotent_AcrossTwoHttpCalls()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest
        {
            Name = "Idempotent Co " + Guid.NewGuid().ToString("N")[..8]
        });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);

        await client.PostAsync($"/api/sync/companies/{company!.Id}/to-hubspot", null);
        var secondResponse = await client.PostAsync($"/api/sync/companies/{company.Id}/to-hubspot", null);
        var secondJob = await secondResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);

        Assert.Equal(SyncStatus.Succeeded, secondJob!.Status);
        Assert.Equal(1, factory.HubSpotClient.CreateCallCount);
        Assert.Equal(1, factory.HubSpotClient.UpdateCallCount);
    }

    [Fact]
    public async Task SyncContactFromHubSpot_ImportsNewInternalContact()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var uniqueEmail = $"import.{Guid.NewGuid():N}@acme.example";
        var record = await factory.HubSpotClient.CreateContactAsync(new Dictionary<string, string?>
        {
            ["email"] = uniqueEmail,
            ["firstname"] = "Imported"
        });

        var syncResponse = await client.PostAsync($"/api/sync/hubspot/contacts/{record.Id}", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var job = await syncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);

        Assert.Equal(SyncStatus.Succeeded, job!.Status);
        Assert.NotNull(job.InternalEntityId);

        var getResponse = await client.GetAsync($"/api/contacts/{job.InternalEntityId}");
        var contact = await getResponse.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(uniqueEmail, contact!.Email);
    }

    [Fact]
    public async Task SyncContactFromHubSpot_IsIdempotent_AcrossTwoHttpCalls()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var record = await factory.HubSpotClient.CreateContactAsync(new Dictionary<string, string?>
        {
            ["email"] = $"import.{Guid.NewGuid():N}@acme.example"
        });

        var firstResponse = await client.PostAsync($"/api/sync/hubspot/contacts/{record.Id}", null);
        var firstJob = await firstResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);
        var secondResponse = await client.PostAsync($"/api/sync/hubspot/contacts/{record.Id}", null);
        var secondJob = await secondResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);

        Assert.Equal(firstJob!.InternalEntityId, secondJob!.InternalEntityId);
    }

    [Fact]
    public async Task GetJob_ReturnsThePersistedJob_AndListJobsIncludesIt()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = "Lookup Co " + Guid.NewGuid().ToString("N")[..8] });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);
        var syncResponse = await client.PostAsync($"/api/sync/companies/{company!.Id}/to-hubspot", null);
        var job = await syncResponse.Content.ReadFromJsonAsync<SyncJobResponse>(CrmApiFactory.JsonOptions);

        var getJobResponse = await client.GetAsync($"/api/sync/jobs/{job!.Id}");
        Assert.Equal(HttpStatusCode.OK, getJobResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/sync/jobs?limit=200");
        var jobs = await listResponse.Content.ReadFromJsonAsync<List<SyncJobResponse>>(CrmApiFactory.JsonOptions);
        Assert.Contains(jobs!, j => j.Id == job.Id);
    }

    [Fact]
    public async Task GetJob_ReturnsNotFound_ForUnknownId()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.GetAsync($"/api/sync/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SyncSucceeds_WritesAuditLogEntry_InRealDatabase()
    {
        using var factory = new SyncApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var companyResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = "Audited Co " + Guid.NewGuid().ToString("N")[..8] });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);

        await client.PostAsync($"/api/sync/companies/{company!.Id}/to-hubspot", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasAudit = await db.AuditLogs.AnyAsync(a => a.EntityId == company.Id.ToString() && a.Action.StartsWith("SyncSucceeded"));
        Assert.True(hasAudit);
    }

    [Fact]
    public async Task EntityMapping_UniqueConstraint_PreventsDuplicateMappingForSameInternalEntity()
    {
        using var factory = new SyncApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var internalId = Guid.NewGuid();
        var firstExternalId = "test-" + Guid.NewGuid().ToString("N");
        var secondExternalId = "test-" + Guid.NewGuid().ToString("N");
        db.EntityMappings.Add(new CrmIntegration.Domain.Entities.EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Contact, InternalId = internalId,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = firstExternalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.EntityMappings.Add(new CrmIntegration.Domain.Entities.EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Contact, InternalId = internalId,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = secondExternalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EntityMapping_UniqueConstraint_PreventsTwoInternalEntitiesMappingToSameExternalId()
    {
        using var factory = new SyncApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sharedExternalId = "shared-" + Guid.NewGuid().ToString("N")[..8];
        db.EntityMappings.Add(new CrmIntegration.Domain.Entities.EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Company, InternalId = Guid.NewGuid(),
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = sharedExternalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.EntityMappings.Add(new CrmIntegration.Domain.Entities.EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Company, InternalId = Guid.NewGuid(),
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = sharedExternalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
