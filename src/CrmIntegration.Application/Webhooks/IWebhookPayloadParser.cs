namespace CrmIntegration.Application.Webhooks;

/// <summary>Parses HubSpot's webhook wire format (a JSON array of event objects) into <see cref="WebhookEvent"/>.</summary>
public interface IWebhookPayloadParser
{
    bool TryParse(string requestBody, out IReadOnlyList<WebhookEvent> events, out string? error);
}
