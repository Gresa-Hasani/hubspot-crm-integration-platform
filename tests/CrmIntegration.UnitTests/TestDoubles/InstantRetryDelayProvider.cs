using CrmIntegration.Application.Sync;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>Records requested delays but completes instantly, so retry tests don't actually sleep.</summary>
public class InstantRetryDelayProvider : IRetryDelayProvider
{
    public List<TimeSpan> RequestedDelays { get; } = new();

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(delay);
        return Task.CompletedTask;
    }
}
