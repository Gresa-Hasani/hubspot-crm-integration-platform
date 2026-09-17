namespace CrmIntegration.Application.Webhooks;

public class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>Reject requests whose X-HubSpot-Request-Timestamp is older than this (HubSpot recommends 5 minutes).</summary>
    public TimeSpan MaxTimestampAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Bounded attempts for processing one IntegrationEvent before it's DeadLettered. Kept separate from Phase 4's SyncRetryOptions — see docs/WEBHOOKS.md "Retry boundary".</summary>
    public int MaxProcessingAttempts { get; set; } = 3;

    /// <summary>How often the background processor polls for pending events.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
}
