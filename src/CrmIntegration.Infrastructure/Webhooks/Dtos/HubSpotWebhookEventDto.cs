using System.Text.Json.Serialization;

namespace CrmIntegration.Infrastructure.Webhooks.Dtos;

/// <summary>
/// Wire shape of one element in the JSON array HubSpot POSTs to a webhook's targetUrl. Only
/// fields this platform uses are declared; unknown additional fields are safely ignored by
/// System.Text.Json by default (no [JsonExtensionData] needed since we never round-trip this).
/// </summary>
public class HubSpotWebhookEventDto
{
    [JsonPropertyName("eventId")]
    public long EventId { get; set; }

    [JsonPropertyName("subscriptionType")]
    public string SubscriptionType { get; set; } = string.Empty;

    [JsonPropertyName("objectId")]
    public long ObjectId { get; set; }

    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; set; }

    [JsonPropertyName("occurredAt")]
    public long OccurredAt { get; set; }

    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; }
}
