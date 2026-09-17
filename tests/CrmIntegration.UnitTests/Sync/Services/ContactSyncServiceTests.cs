using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Services;

public class ContactSyncServiceTests
{
    private readonly FakeContactRepository _contactRepository = new();
    private readonly FakeCompanyRepository _companyRepository = new();
    private readonly FakeEntityMappingRepository _mappingRepository = new();
    private readonly FakeHubSpotClient _hubSpotClient = new();
    private readonly FakeSyncJobRepository _syncJobRepository = new();
    private readonly FakeAuditLogRepository _auditLogRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ContactSyncService CreateService()
    {
        var stageMapper = new HubSpotStageMapper(Options.Create(new HubSpotOptions()));
        var mapper = new ContactHubSpotMapper(stageMapper);
        var matchService = new ContactMatchService(_contactRepository, _hubSpotClient);
        var retryExecutor = new HubSpotRetryExecutor(
            new InstantRetryDelayProvider(),
            Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }),
            NullLogger<HubSpotRetryExecutor>.Instance);
        var jobExecutor = new SyncJobExecutor(
            _syncJobRepository, _auditLogRepository, retryExecutor, _unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }),
            NullLogger<SyncJobExecutor>.Instance);

        return new ContactSyncService(
            _contactRepository, _mappingRepository, mapper, matchService, _hubSpotClient, jobExecutor,
            new NoOpContactLifecycleAutomationService(), _unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<ContactSyncService>.Instance);
    }

    private Contact AddContact(string email = "alice@acme.example", Guid? companyId = null)
    {
        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Smith", Email = email, CompanyId = companyId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _contactRepository.Contacts.Add(contact);
        return contact;
    }

    [Fact]
    public async Task SyncToHubSpotAsync_CreatesHubSpotContact_AndMapping_WhenUnmapped()
    {
        var contact = AddContact();
        var service = CreateService();

        var job = await service.SyncToHubSpotAsync(contact.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Equal(1, _hubSpotClient.CreateCallCount);
        Assert.Equal(contact.HubSpotId, job.ExternalId);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_IsIdempotent_SecondSyncReusesMapping_NoDuplicateCreated()
    {
        var contact = AddContact();
        var service = CreateService();

        await service.SyncToHubSpotAsync(contact.Id, "corr-1");
        var secondJob = await service.SyncToHubSpotAsync(contact.Id, "corr-2");

        Assert.Equal(1, _hubSpotClient.CreateCallCount);
        Assert.Equal(1, _hubSpotClient.UpdateCallCount); // the second sync updates the existing object
        Assert.Single(_mappingRepository.Mappings);
        Assert.Equal(SyncStatus.Succeeded, secondJob.Status);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_ReusesExactHubSpotMatch_InsteadOfCreatingDuplicate()
    {
        var existing = await _hubSpotClient.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "alice@acme.example" });
        var contact = AddContact();
        var service = CreateService();

        var job = await service.SyncToHubSpotAsync(contact.Id, "corr-1");

        Assert.Equal(existing.Id, job.ExternalId);
        Assert.Equal(1, _hubSpotClient.CreateCallCount); // only the pre-existing one, none created by sync
    }

    [Fact]
    public async Task SyncToHubSpotAsync_Throws_OnAmbiguousHubSpotMatch()
    {
        // Simulate two HubSpot contacts sharing an email by inserting two objects with the same property.
        var hubSpot = new FakeHubSpotClient();
        await hubSpot.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "dup@acme.example" });
        await hubSpot.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "dup@acme.example" });

        var contact = AddContact(email: "dup@acme.example");
        var stageMapper = new HubSpotStageMapper(Options.Create(new HubSpotOptions()));
        var mapper = new ContactHubSpotMapper(stageMapper);
        var matchService = new ContactMatchService(_contactRepository, hubSpot);
        var retryExecutor = new HubSpotRetryExecutor(new InstantRetryDelayProvider(), Options.Create(new SyncRetryOptions()), NullLogger<HubSpotRetryExecutor>.Instance);
        var jobExecutor = new SyncJobExecutor(_syncJobRepository, _auditLogRepository, retryExecutor, _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), Options.Create(new SyncRetryOptions()), NullLogger<SyncJobExecutor>.Instance);
        var service = new ContactSyncService(_contactRepository, _mappingRepository, mapper, matchService, hubSpot, jobExecutor, new NoOpContactLifecycleAutomationService(), _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<ContactSyncService>.Instance);

        await Assert.ThrowsAsync<SyncAmbiguousMatchException>(() => service.SyncToHubSpotAsync(contact.Id, "corr-1"));

        var job = Assert.Single(_syncJobRepository.Jobs);
        Assert.Equal(SyncStatus.Failed, job.Status);
        Assert.Equal("AmbiguousMatch", job.FailureCategory);
        Assert.Empty(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_RecoversFromStaleMapping_ByCreatingReplacement()
    {
        var contact = AddContact();
        _mappingRepository.Mappings.Add(new EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Contact, InternalId = contact.Id,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = "does-not-exist", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        var service = CreateService();

        var job = await service.SyncToHubSpotAsync(contact.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.NotEqual("does-not-exist", job.ExternalId);
        Assert.Equal(1, _hubSpotClient.CreateCallCount);
        var mapping = Assert.Single(_mappingRepository.Mappings);
        Assert.Equal(job.ExternalId, mapping.ExternalId);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_CreatesCompanyAssociation_WhenCompanyAlreadyMapped()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _companyRepository.Companies.Add(company);
        var companyHubSpotId = (await _hubSpotClient.CreateCompanyAsync(new Dictionary<string, string?> { ["name"] = "Acme" })).Id;
        _mappingRepository.Mappings.Add(new EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Company, InternalId = company.Id,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = companyHubSpotId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        var contact = AddContact(companyId: company.Id);
        var service = CreateService();

        var job = await service.SyncToHubSpotAsync(contact.Id, "corr-1");

        var associated = await _hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Contact, job.ExternalId!, HubSpotObjectType.Company);
        Assert.Contains(companyHubSpotId, associated);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_SkipsAssociation_WhenCompanyNotYetMapped()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _companyRepository.Companies.Add(company);
        var contact = AddContact(companyId: company.Id);
        var service = CreateService();

        var job = await service.SyncToHubSpotAsync(contact.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        var associated = await _hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Contact, job.ExternalId!, HubSpotObjectType.Company);
        Assert.Empty(associated);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_ImportsNewContact_WhenUnmappedAndNoInternalMatch()
    {
        var record = await _hubSpotClient.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "new@acme.example", ["firstname"] = "New" });
        var service = CreateService();

        var job = await service.SyncFromHubSpotAsync(record.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Single(_contactRepository.Contacts);
        Assert.Equal("new@acme.example", _contactRepository.Contacts[0].Email);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_IsIdempotent_SecondImportUpdatesSameContact()
    {
        var record = await _hubSpotClient.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "new@acme.example", ["firstname"] = "New" });
        var service = CreateService();

        await service.SyncFromHubSpotAsync(record.Id, "corr-1");
        await service.SyncFromHubSpotAsync(record.Id, "corr-2");

        Assert.Single(_contactRepository.Contacts);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_AttachesToExistingInternalContact_WhenEmailMatches()
    {
        var existing = AddContact(email: "existing@acme.example");
        var record = await _hubSpotClient.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "existing@acme.example", ["firstname"] = "Existing" });
        var service = CreateService();

        var job = await service.SyncFromHubSpotAsync(record.Id, "corr-1");

        Assert.Single(_contactRepository.Contacts);
        Assert.Equal(existing.Id, job.InternalEntityId);
    }
}
