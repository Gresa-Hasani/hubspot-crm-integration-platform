using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Services;

public interface ICompanySyncService
{
    Task<SyncJob> SyncToHubSpotAsync(Guid companyId, string correlationId, CancellationToken cancellationToken = default);
    Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default);
}
