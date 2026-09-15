namespace CrmIntegration.Application.Sync;

/// <summary>
/// Abstracts the actual delay so retry logic is unit-testable without real sleeps. Production
/// uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; tests substitute a fake that
/// completes instantly and records what delays were requested.
/// </summary>
public interface IRetryDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public class TaskDelayProvider : IRetryDelayProvider
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
