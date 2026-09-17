using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

/// <summary>
/// A single HubSpot webhook event, persisted durably before processing so ingestion and
/// processing can be safely separated (see docs/WEBHOOKS.md).
/// </summary>
public class IntegrationEvent
{
    public Guid Id { get; set; }

    /// <summary>HubSpot's own eventId — the idempotency key. Stable across HubSpot's own redelivery attempts (see AttemptNumber).</summary>
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>HubSpot's subscriptionType, e.g. "contact.propertyChange", "company.creation".</summary>
    public string EventType { get; set; } = string.Empty;

    public EntityType EntityType { get; set; }

    /// <summary>HubSpot objectId of the affected record.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Normalized event fields (not the raw HTTP request) — see docs/WEBHOOKS.md for what is and isn't stored. Defaults to "{}" (valid JSON) since the column is jsonb.</summary>
    public string Payload { get; set; } = "{}";

    public IntegrationEventStatus Status { get; set; } = IntegrationEventStatus.Received;

    public int AttemptCount { get; set; }

    /// <summary>HubSpot's own delivery attempt counter (0 = first delivery), for diagnostics only — not used for idempotency or our own retry logic.</summary>
    public int HubSpotAttemptNumber { get; set; }

    public DateTime ReceivedAt { get; set; }

    /// <summary>When HubSpot says the underlying change occurred (occurredAt), for diagnostics — never used to reorder or discard processing.</summary>
    public DateTime? OccurredAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    /// <summary>Short, safe category (e.g. "Transient", "AmbiguousMatch") — never a raw exception with payload/token content.</summary>
    public string? FailureCategory { get; set; }

    public string? LastError { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
