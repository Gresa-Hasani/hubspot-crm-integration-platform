using System.Text.Json;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Infrastructure.Webhooks.Dtos;

namespace CrmIntegration.Infrastructure.Webhooks;

public class WebhookPayloadParser : IWebhookPayloadParser
{
    public bool TryParse(string requestBody, out IReadOnlyList<WebhookEvent> events, out string? error)
    {
        events = [];
        error = null;

        List<HubSpotWebhookEventDto>? dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<HubSpotWebhookEventDto>>(requestBody);
        }
        catch (JsonException ex)
        {
            error = $"Request body is not a valid JSON array of HubSpot webhook events: {ex.Message}";
            return false;
        }

        if (dtos is null)
        {
            error = "Request body deserialized to null.";
            return false;
        }

        var mapped = new List<WebhookEvent>(dtos.Count);
        try
        {
            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.SubscriptionType) || dto.EventId == 0)
                {
                    error = "One or more events is missing a required field (eventId/subscriptionType).";
                    return false;
                }

                mapped.Add(new WebhookEvent(
                    dto.EventId.ToString(),
                    dto.SubscriptionType,
                    dto.ObjectId.ToString(),
                    dto.PropertyName,
                    DateTimeOffset.FromUnixTimeMilliseconds(dto.OccurredAt),
                    dto.AttemptNumber));
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = $"One or more events has an invalid occurredAt timestamp: {ex.Message}";
            return false;
        }

        events = mapped;
        return true;
    }
}
