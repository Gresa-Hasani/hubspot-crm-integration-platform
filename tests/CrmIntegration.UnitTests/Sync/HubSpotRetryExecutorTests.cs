using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync;

public class HubSpotRetryExecutorTests
{
    private static HubSpotRetryExecutor CreateExecutor(InstantRetryDelayProvider delayProvider, SyncRetryOptions? options = null) =>
        new(delayProvider, Options.Create(options ?? new SyncRetryOptions { MaxAttempts = 4, BaseDelay = TimeSpan.FromSeconds(2), MaxDelay = TimeSpan.FromSeconds(30) }),
            NullLogger<HubSpotRetryExecutor>.Instance);

    [Fact]
    public async Task ExecuteAsync_ReturnsImmediately_OnFirstSuccess()
    {
        var executor = CreateExecutor(new InstantRetryDelayProvider());

        var outcome = await executor.ExecuteAsync(() => Task.FromResult(42));

        Assert.True(outcome.Succeeded);
        Assert.Equal(42, outcome.Value);
        Assert.Equal(1, outcome.AttemptsMade);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_DeterministicFailures()
    {
        var executor = CreateExecutor(new InstantRetryDelayProvider());
        var callCount = 0;

        await Assert.ThrowsAsync<HubSpotBadRequestException>(() => executor.ExecuteAsync<int>(() =>
        {
            callCount++;
            throw new HubSpotBadRequestException("bad request", null);
        }));

        Assert.Equal(1, callCount);
    }

    [Theory]
    [InlineData(typeof(HubSpotServerException))]
    [InlineData(typeof(HubSpotTransientException))]
    public async Task ExecuteAsync_RetriesTransientFailures_UntilSuccess(Type exceptionType)
    {
        var delayProvider = new InstantRetryDelayProvider();
        var executor = CreateExecutor(delayProvider);
        var callCount = 0;

        var outcome = await executor.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw CreateException(exceptionType);
            }

            return Task.FromResult("ok");
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(3, outcome.AttemptsMade);
        Assert.Equal(2, delayProvider.RequestedDelays.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsAttempts_AndReturnsFailedOutcome()
    {
        var delayProvider = new InstantRetryDelayProvider();
        var executor = CreateExecutor(delayProvider, new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) });
        var callCount = 0;

        var outcome = await executor.ExecuteAsync<int>(() =>
        {
            callCount++;
            throw new HubSpotServerException("down", System.Net.HttpStatusCode.ServiceUnavailable, null);
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal(3, callCount);
        Assert.Equal(3, outcome.AttemptsMade);
        Assert.IsType<HubSpotServerException>(outcome.FinalException);
    }

    [Fact]
    public async Task ExecuteAsync_UsesRetryAfter_WhenHubSpotSuppliesIt()
    {
        var delayProvider = new InstantRetryDelayProvider();
        var executor = CreateExecutor(delayProvider);
        var callCount = 0;

        await executor.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HubSpotRateLimitedException("rate limited", null, TimeSpan.FromSeconds(7));
            }

            return Task.FromResult("ok");
        });

        Assert.Equal(TimeSpan.FromSeconds(7), delayProvider.RequestedDelays[0]);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBackoff_WhenRateLimitedWithoutRetryAfter()
    {
        var delayProvider = new InstantRetryDelayProvider();
        var executor = CreateExecutor(delayProvider, new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromSeconds(2), MaxDelay = TimeSpan.FromSeconds(30) });
        var callCount = 0;

        await executor.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HubSpotRateLimitedException("rate limited", null, null);
            }

            return Task.FromResult("ok");
        });

        Assert.Equal(TimeSpan.FromSeconds(2), delayProvider.RequestedDelays[0]);
    }

    private static Exception CreateException(Type type) => type == typeof(HubSpotServerException)
        ? new HubSpotServerException("server error", System.Net.HttpStatusCode.BadGateway, null)
        : new HubSpotTransientException("network blip", new Exception("inner"));
}
