using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Services;

public interface IDealSyncService
{
    Task<SyncJob> SyncToHubSpotAsync(Guid dealId, string correlationId, CancellationToken cancellationToken = default);
    Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default);
}
