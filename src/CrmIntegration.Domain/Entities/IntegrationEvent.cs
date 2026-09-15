using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class IntegrationEvent
{
    public Guid Id { get; set; }
    public string ExternalEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public EntityType EntityType { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public IntegrationEventStatus Status { get; set; } = IntegrationEventStatus.Pending;
    public int RetryCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
}
