using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Services;

public class DealSyncServiceTests
{
    private readonly FakeDealRepository _dealRepository = new();
    private readonly FakeEntityMappingRepository _mappingRepository = new();
    private readonly FakeSyncJobRepository _syncJobRepository = new();
    private readonly FakeAuditLogRepository _auditLogRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private DealSyncService CreateService(FakeHubSpotClient hubSpotClient)
    {
        var mapper = new DealHubSpotMapper(new HubSpotStageMapper(Options.Create(new HubSpotOptions())), Options.Create(new HubSpotOptions()));
        var retryExecutor = new HubSpotRetryExecutor(new InstantRetryDelayProvider(), Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }), NullLogger<HubSpotRetryExecutor>.Instance);
        var jobExecutor = new SyncJobExecutor(_syncJobRepository, _auditLogRepository, retryExecutor, _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }), NullLogger<SyncJobExecutor>.Instance);

        return new DealSyncService(_dealRepository, _mappingRepository, mapper, hubSpotClient, jobExecutor, new NoOpDealStageAutomationService(), _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<DealSyncService>.Instance);
    }

    private Deal AddDeal(Guid? companyId = null, Guid? contactId = null)
    {
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Enterprise Deal", Amount = 25000m, Currency = "EUR", CompanyId = companyId, ContactId = contactId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _dealRepository.Deals.Add(deal);
        return deal;
    }

    [Fact]
    public async Task SyncToHubSpotAsync_CreatesHubSpotDeal_WhenUnmapped()
    {
        var hubSpot = new FakeHubSpotClient();
        var deal = AddDeal();
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(deal.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Equal(1, hubSpot.CreateCallCount);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_IsIdempotent_SecondSyncUpdatesNotCreates()
    {
        var hubSpot = new FakeHubSpotClient();
        var deal = AddDeal();
        var service = CreateService(hubSpot);

        await service.SyncToHubSpotAsync(deal.Id, "corr-1");
        await service.SyncToHubSpotAsync(deal.Id, "corr-2");

        Assert.Equal(1, hubSpot.CreateCallCount);
        Assert.Equal(1, hubSpot.UpdateCallCount);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_NeverSearchesForDuplicates_EvenWithIdenticalName()
    {
        var hubSpot = new FakeHubSpotClient();
        // Pre-existing HubSpot deal with the exact same name — a Contact/Company sync would
        // normally reuse a matching record, but Deals must never dedupe by name.
        await hubSpot.CreateDealAsync(new Dictionary<string, string?> { ["dealname"] = "Enterprise Deal" });
        var deal = AddDeal();
        var service = CreateService(hubSpot);

        await service.SyncToHubSpotAsync(deal.Id, "corr-1");

        Assert.Equal(2, hubSpot.CreateCallCount); // the pre-existing one, plus a new one — no reuse
    }

    [Fact]
    public async Task SyncToHubSpotAsync_RecoversFromStaleMapping_ByCreatingReplacement()
    {
        var hubSpot = new FakeHubSpotClient();
        var deal = AddDeal();
        _mappingRepository.Mappings.Add(new EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Deal, InternalId = deal.Id,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = "gone", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(deal.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.NotEqual("gone", job.ExternalId);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_CreatesCompanyAndContactAssociations_WhenBothMapped()
    {
        var hubSpot = new FakeHubSpotClient();
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var companyHubSpotId = (await hubSpot.CreateCompanyAsync(new Dictionary<string, string?> { ["name"] = "Acme" })).Id;
        var contactHubSpotId = (await hubSpot.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "a@acme.example" })).Id;
        _mappingRepository.Mappings.Add(new EntityMapping { Id = Guid.NewGuid(), EntityType = EntityType.Company, InternalId = companyId, ExternalSystem = ExternalSystem.HubSpot, ExternalId = companyHubSpotId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _mappingRepository.Mappings.Add(new EntityMapping { Id = Guid.NewGuid(), EntityType = EntityType.Contact, InternalId = contactId, ExternalSystem = ExternalSystem.HubSpot, ExternalId = contactHubSpotId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var deal = AddDeal(companyId, contactId);
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(deal.Id, "corr-1");

        var associatedCompanies = await hubSpot.GetAssociatedIdsAsync(HubSpotObjectType.Deal, job.ExternalId!, HubSpotObjectType.Company);
        var associatedContacts = await hubSpot.GetAssociatedIdsAsync(HubSpotObjectType.Deal, job.ExternalId!, HubSpotObjectType.Contact);
        Assert.Contains(companyHubSpotId, associatedCompanies);
        Assert.Contains(contactHubSpotId, associatedContacts);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_ImportsNewDeal_WhenUnmapped()
    {
        var hubSpot = new FakeHubSpotClient();
        var record = await hubSpot.CreateDealAsync(new Dictionary<string, string?> { ["dealname"] = "Imported Deal", ["amount"] = "1000", ["dealstage"] = "qualifiedtobuy" });
        var service = CreateService(hubSpot);

        var job = await service.SyncFromHubSpotAsync(record.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Single(_dealRepository.Deals);
        Assert.Equal("Imported Deal", _dealRepository.Deals[0].Name);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_IsIdempotent_SecondImportUpdatesSameDeal()
    {
        var hubSpot = new FakeHubSpotClient();
        var record = await hubSpot.CreateDealAsync(new Dictionary<string, string?> { ["dealname"] = "Imported Deal", ["amount"] = "1000" });
        var service = CreateService(hubSpot);

        await service.SyncFromHubSpotAsync(record.Id, "corr-1");
        await service.SyncFromHubSpotAsync(record.Id, "corr-2");

        Assert.Single(_dealRepository.Deals);
        Assert.Single(_mappingRepository.Mappings);
    }
}
