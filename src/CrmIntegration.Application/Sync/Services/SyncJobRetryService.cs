using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync.Services;

public class SyncJobRetryService : ISyncJobRetryService
{
    private readonly ISyncJobRepository _syncJobRepository;
    private readonly IContactSyncService _contactSyncService;
    private readonly ICompanySyncService _companySyncService;
    private readonly IDealSyncService _dealSyncService;

    public SyncJobRetryService(
        ISyncJobRepository syncJobRepository,
        IContactSyncService contactSyncService,
        ICompanySyncService companySyncService,
        IDealSyncService dealSyncService)
    {
        _syncJobRepository = syncJobRepository;
        _contactSyncService = contactSyncService;
        _companySyncService = companySyncService;
        _dealSyncService = dealSyncService;
    }

    public async Task<SyncJob> RetryAsync(Guid deadLetteredJobId, CancellationToken cancellationToken = default)
    {
        var original = await _syncJobRepository.GetByIdAsync(deadLetteredJobId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(SyncJob), deadLetteredJobId);

        if (original.Status != SyncStatus.DeadLettered)
        {
            throw new DomainValidationException($"SyncJob '{deadLetteredJobId}' is '{original.Status}', not DeadLettered; only dead-lettered jobs can be retried through this endpoint.");
        }

        return (original.EntityType, original.Direction) switch
        {
            (EntityType.Contact, SyncDirection.InternalToHubSpot) =>
                await _contactSyncService.SyncToHubSpotAsync(RequireInternalId(original), original.CorrelationId, cancellationToken),
            (EntityType.Contact, SyncDirection.HubSpotToInternal) =>
                await _contactSyncService.SyncFromHubSpotAsync(RequireExternalId(original), original.CorrelationId, cancellationToken),
            (EntityType.Company, SyncDirection.InternalToHubSpot) =>
                await _companySyncService.SyncToHubSpotAsync(RequireInternalId(original), original.CorrelationId, cancellationToken),
            (EntityType.Company, SyncDirection.HubSpotToInternal) =>
                await _companySyncService.SyncFromHubSpotAsync(RequireExternalId(original), original.CorrelationId, cancellationToken),
            (EntityType.Deal, SyncDirection.InternalToHubSpot) =>
                await _dealSyncService.SyncToHubSpotAsync(RequireInternalId(original), original.CorrelationId, cancellationToken),
            (EntityType.Deal, SyncDirection.HubSpotToInternal) =>
                await _dealSyncService.SyncFromHubSpotAsync(RequireExternalId(original), original.CorrelationId, cancellationToken),
            _ => throw new DomainValidationException($"Unsupported entity/direction combination for retry: {original.EntityType}/{original.Direction}.")
        };
    }

    private static Guid RequireInternalId(SyncJob job) =>
        job.InternalEntityId ?? throw new DomainValidationException($"SyncJob '{job.Id}' has no InternalEntityId recorded; cannot retry an Internal->HubSpot sync.");

    private static string RequireExternalId(SyncJob job) =>
        job.ExternalId ?? throw new DomainValidationException($"SyncJob '{job.Id}' has no ExternalId recorded; cannot retry a HubSpot->Internal sync.");
}
