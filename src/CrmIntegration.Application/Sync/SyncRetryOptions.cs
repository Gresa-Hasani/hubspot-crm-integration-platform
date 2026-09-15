namespace CrmIntegration.Application.Sync;

/// <summary>Bounded exponential backoff configuration for retrying transient HubSpot failures.</summary>
public class SyncRetryOptions
{
    public const string SectionName = "SyncRetry";

    /// <summary>Total attempts including the first (non-retry) call. Must be >= 1.</summary>
    public int MaxAttempts { get; set; } = 4;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
