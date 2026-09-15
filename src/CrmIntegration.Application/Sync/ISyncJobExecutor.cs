using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync;

/// <summary>
/// Owns SyncJob lifecycle (Pending -> Running -> Succeeded/Failed/DeadLettered), retry
/// invocation, attempt counting, and audit logging so entity-specific sync services only need to
/// implement the actual field-mapping/matching/HubSpot-call logic once per entity/direction.
/// </summary>
public interface ISyncJobExecutor
{
    Task<SyncJob> ExecuteAsync(
        EntityType entityType,
        SyncDirection direction,
        Guid? internalEntityId,
        string? externalId,
        string correlationId,
        Func<CancellationToken, Task<SyncActionResult>> action,
        CancellationToken cancellationToken = default);
}
