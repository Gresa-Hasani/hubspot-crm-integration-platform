namespace CrmIntegration.Application.Sync;

public record RetryOutcome<T>(bool Succeeded, T? Value, int AttemptsMade, Exception? FinalException);

/// <summary>
/// Runs an operation with bounded exponential backoff for transient HubSpot failures only
/// (HubSpotRateLimitedException, HubSpotServerException, HubSpotTransientException). Any other
/// exception (400/401/403/404/409, validation, ambiguous match, etc.) is deterministic and is
/// rethrown immediately on the first attempt — retrying it would never succeed.
/// </summary>
public interface IHubSpotRetryExecutor
{
    Task<RetryOutcome<T>> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}
