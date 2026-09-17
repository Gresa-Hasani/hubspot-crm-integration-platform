using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync.Services;

public interface IContactSyncService
{
    Task<SyncJob> SyncToHubSpotAsync(Guid contactId, string correlationId, CancellationToken cancellationToken = default);

    /// <param name="source">Attributed to any ContactLifecycleTransition this sync produces — see docs/SALES_AUTOMATION.md.</param>
    Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync);
}
