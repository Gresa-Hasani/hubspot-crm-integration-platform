using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Services;

/// <summary>
/// Manually re-runs a dead-lettered SyncJob through the same entity-specific sync path that
/// produced it. This does not resume "in place" — it creates a new SyncJob (reusing the original
/// job's CorrelationId so the two are easy to find together), since retrying from scratch is what
/// makes the operation safe to repeat (see ISyncJobExecutor / docs/SYNC_POLICY.md).
/// </summary>
public interface ISyncJobRetryService
{
    Task<SyncJob> RetryAsync(Guid deadLetteredJobId, CancellationToken cancellationToken = default);
}
