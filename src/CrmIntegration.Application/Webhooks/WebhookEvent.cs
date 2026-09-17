namespace CrmIntegration.Application.Webhooks;

/// <summary>
/// One HubSpot webhook event, already parsed out of HubSpot's wire format (which lives in
/// Infrastructure, mirroring the HubSpotRecord/HubSpotObjectDto split from Phase 3/4). Only the
/// fields this platform actually uses are kept — see docs/WEBHOOKS.md.
/// </summary>
public record WebhookEvent(
    string EventId,
    string SubscriptionType,
    string ObjectId,
    string? PropertyName,
    DateTimeOffset OccurredAt,
    int AttemptNumber);
