using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class SyncJob
{
    public Guid Id { get; set; }
    public EntityType EntityType { get; set; }
    public SyncDirection Direction { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public int RecordsProcessed { get; set; }
    public int RecordsSucceeded { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Internal entity being synced, when known (may be null for a HubSpot->Internal
    /// import where no internal entity existed yet at job start).</summary>
    public Guid? InternalEntityId { get; set; }

    /// <summary>HubSpot object id involved, when known.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Ties this job to logs/audit entries for the same operation.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>How many attempts (including the first) were made against HubSpot for this job.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Short, safe category for the final failure (e.g. "RateLimited", "ServerError", "Transient", "BadRequest") — never a raw exception message with payload/token content.</summary>
    public string? FailureCategory { get; set; }
}
