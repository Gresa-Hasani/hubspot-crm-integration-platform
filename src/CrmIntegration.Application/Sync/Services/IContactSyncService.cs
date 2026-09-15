using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Services;

public interface IContactSyncService
{
    Task<SyncJob> SyncToHubSpotAsync(Guid contactId, string correlationId, CancellationToken cancellationToken = default);
    Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default);
}
