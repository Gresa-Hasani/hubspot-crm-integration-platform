using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Webhooks;

public interface IIntegrationEventRepository
{
    /// <returns>false if an event with the same ExternalEventId already exists (duplicate) — nothing is inserted.</returns>
    Task<bool> TryAddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string externalEventId, CancellationToken cancellationToken = default);

    Task<IntegrationEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationEvent>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions one Received event to Processing and returns it, or null if it was
    /// already claimed by another worker/instance (or is no longer in a claimable state). See
    /// docs/WEBHOOKS.md "Event claiming" for why this is a single conditional UPDATE rather than
    /// row-locking.
    /// </summary>
    Task<IntegrationEvent?> TryClaimNextAsync(CancellationToken cancellationToken = default);
}
