using CrmIntegration.Application.Common;
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

public class SyncJobRetryServiceTests
{
    [Fact]
    public async Task RetryAsync_Throws_WhenJobIsNotDeadLettered()
    {
        var syncJobRepository = new FakeSyncJobRepository();
        var job = new SyncJob { Id = Guid.NewGuid(), Status = SyncStatus.Failed, EntityType = EntityType.Contact, Direction = SyncDirection.InternalToHubSpot, CorrelationId = "c1", StartedAt = DateTime.UtcNow };
        syncJobRepository.Jobs.Add(job);

        var service = new SyncJobRetryService(syncJobRepository, NullContactSyncService(), NullCompanySyncService(), NullDealSyncService());

        await Assert.ThrowsAsync<DomainValidationException>(() => service.RetryAsync(job.Id));
    }

    [Fact]
    public async Task RetryAsync_ReRunsContactSync_ForDeadLetteredInternalToHubSpotJob()
    {
        var contactRepository = new FakeContactRepository();
        var mappingRepository = new FakeEntityMappingRepository();
        var hubSpot = new FakeHubSpotClient();
        var syncJobRepository = new FakeSyncJobRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();

        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@acme.example", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        contactRepository.Contacts.Add(contact);

        var deadLetteredJob = new SyncJob
        {
            Id = Guid.NewGuid(), Status = SyncStatus.DeadLettered, EntityType = EntityType.Contact, Direction = SyncDirection.InternalToHubSpot,
            InternalEntityId = contact.Id, CorrelationId = "original-corr", StartedAt = DateTime.UtcNow
        };
        syncJobRepository.Jobs.Add(deadLetteredJob);

        var mapper = new ContactHubSpotMapper(new HubSpotStageMapper(Options.Create(new HubSpotOptions())));
        var matchService = new ContactMatchService(contactRepository, hubSpot);
        var retryExecutor = new HubSpotRetryExecutor(new InstantRetryDelayProvider(), Options.Create(new SyncRetryOptions()), NullLogger<HubSpotRetryExecutor>.Instance);
        var jobExecutor = new SyncJobExecutor(syncJobRepository, auditLogRepository, retryExecutor, unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), Options.Create(new SyncRetryOptions()), NullLogger<SyncJobExecutor>.Instance);
        var contactSyncService = new ContactSyncService(contactRepository, mappingRepository, mapper, matchService, hubSpot, jobExecutor, new NoOpContactLifecycleAutomationService(), unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<ContactSyncService>.Instance);

        var service = new SyncJobRetryService(syncJobRepository, contactSyncService, NullCompanySyncService(), NullDealSyncService());

        var newJob = await service.RetryAsync(deadLetteredJob.Id);

        Assert.Equal(SyncStatus.Succeeded, newJob.Status);
        Assert.NotEqual(deadLetteredJob.Id, newJob.Id);
        Assert.Equal("original-corr", newJob.CorrelationId);
        Assert.Single(mappingRepository.Mappings);
    }

    private static ICompanySyncService NullCompanySyncService() => new NotImplementedCompanySyncService();
    private static IContactSyncService NullContactSyncService() => new NotImplementedContactSyncService();
    private static IDealSyncService NullDealSyncService() => new NotImplementedDealSyncService();

    private class NotImplementedContactSyncService : IContactSyncService
    {
        public Task<SyncJob> SyncToHubSpotAsync(Guid contactId, string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync) => throw new NotImplementedException();
    }

    private class NotImplementedCompanySyncService : ICompanySyncService
    {
        public Task<SyncJob> SyncToHubSpotAsync(Guid companyId, string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class NotImplementedDealSyncService : IDealSyncService
    {
        public Task<SyncJob> SyncToHubSpotAsync(Guid dealId, string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync) => throw new NotImplementedException();
    }
}
