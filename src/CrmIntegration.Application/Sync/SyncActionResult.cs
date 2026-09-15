namespace CrmIntegration.Application.Sync;

public enum SyncResultKind
{
    MappedUpdate,
    ReusedExistingMatch,
    Created,
    Imported,
    AssociationSkippedCounterpartNotMapped
}

/// <summary>What an entity-specific sync action actually did, for the job/audit record.</summary>
public record SyncActionResult(SyncResultKind Kind, Guid InternalId, string ExternalId);
