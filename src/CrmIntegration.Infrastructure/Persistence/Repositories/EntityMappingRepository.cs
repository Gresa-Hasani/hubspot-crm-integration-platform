using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class EntityMappingRepository : IEntityMappingRepository
{
    private readonly AppDbContext _context;

    public EntityMappingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<EntityMapping?> GetByInternalIdAsync(EntityType entityType, Guid internalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default) =>
        _context.EntityMappings.FirstOrDefaultAsync(
            m => m.EntityType == entityType && m.InternalId == internalId && m.ExternalSystem == externalSystem,
            cancellationToken);

    public Task<EntityMapping?> GetByExternalIdAsync(EntityType entityType, string externalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default) =>
        _context.EntityMappings.FirstOrDefaultAsync(
            m => m.EntityType == entityType && m.ExternalId == externalId && m.ExternalSystem == externalSystem,
            cancellationToken);

    public async Task AddAsync(EntityMapping mapping, CancellationToken cancellationToken = default) =>
        await _context.EntityMappings.AddAsync(mapping, cancellationToken);
}
