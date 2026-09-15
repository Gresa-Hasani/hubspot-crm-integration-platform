using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class SyncJob
{
    public Guid Id { get; set; }
    public EntityType EntityType { get; set; }
    public SyncDirection Direction { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Running;
    public int RecordsProcessed { get; set; }
    public int RecordsSucceeded { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorMessage { get; set; }
}
