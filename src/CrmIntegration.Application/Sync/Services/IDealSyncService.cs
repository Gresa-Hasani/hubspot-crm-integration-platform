using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync.Services;

public interface IDealSyncService
{
    Task<SyncJob> SyncToHubSpotAsync(Guid dealId, string correlationId, CancellationToken cancellationToken = default);

    /// <param name="source">Attributed to any DealStageTransition this sync produces — see docs/SALES_AUTOMATION.md.</param>
    Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync);
}
