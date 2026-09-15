using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync;

public interface ISyncJobRepository
{
    Task AddAsync(SyncJob job, CancellationToken cancellationToken = default);
    Task<SyncJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncJob>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
