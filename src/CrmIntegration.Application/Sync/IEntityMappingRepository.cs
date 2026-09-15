using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync;

public interface IEntityMappingRepository
{
    Task<EntityMapping?> GetByInternalIdAsync(EntityType entityType, Guid internalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default);
    Task<EntityMapping?> GetByExternalIdAsync(EntityType entityType, string externalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default);
    Task AddAsync(EntityMapping mapping, CancellationToken cancellationToken = default);
}
