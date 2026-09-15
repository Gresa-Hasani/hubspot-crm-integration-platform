using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync;

/// <summary>
/// Thrown when a duplicate lookup returns more than one plausible match. Ambiguous matches must
/// never be auto-resolved — this always terminates the sync job as Failed (not DeadLettered:
/// retrying won't change the ambiguity, a human/policy decision is needed).
/// </summary>
public class SyncAmbiguousMatchException : Exception
{
    public EntityType EntityType { get; }

    public SyncAmbiguousMatchException(EntityType entityType, string message) : base(message)
    {
        EntityType = entityType;
    }
}

/// <summary>Thrown when persisting an EntityMapping would violate its uniqueness constraints (a race with another sync).</summary>
public class SyncMappingConflictException : Exception
{
    public SyncMappingConflictException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
