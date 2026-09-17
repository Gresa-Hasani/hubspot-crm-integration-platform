namespace CrmIntegration.Application.Webhooks;

public record WebhookIngestionResult(bool IsMalformed, string? MalformedReason, int Accepted, int Duplicates, int Ignored)
{
    public static WebhookIngestionResult Malformed(string reason) => new(true, reason, 0, 0, 0);
    public static WebhookIngestionResult Success(int accepted, int duplicates, int ignored) => new(false, null, accepted, duplicates, ignored);
}

/// <summary>
/// Owns everything after signature validation: parsing the envelope, classifying each event,
/// and persisting it durably (with per-event duplicate detection) — but never calls a Phase 4
/// sync service directly. That happens later, out of the HTTP request, via
/// <see cref="IIntegrationEventProcessor"/>.
/// </summary>
public interface IWebhookIngestionService
{
    Task<WebhookIngestionResult> IngestAsync(string requestBody, string correlationId, CancellationToken cancellationToken = default);
}
