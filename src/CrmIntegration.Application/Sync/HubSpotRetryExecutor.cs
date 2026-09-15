using CrmIntegration.Application.Integrations.HubSpot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Sync;

public class HubSpotRetryExecutor : IHubSpotRetryExecutor
{
    private readonly IRetryDelayProvider _delayProvider;
    private readonly SyncRetryOptions _options;
    private readonly ILogger<HubSpotRetryExecutor> _logger;

    public HubSpotRetryExecutor(IRetryDelayProvider delayProvider, IOptions<SyncRetryOptions> options, ILogger<HubSpotRetryExecutor> logger)
    {
        _delayProvider = delayProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetryOutcome<T>> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                return new RetryOutcome<T>(true, result, attempt, null);
            }
            catch (HubSpotRateLimitedException ex)
            {
                lastException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delay = ex.RetryAfter ?? ComputeBackoff(attempt);
                _logger.LogWarning("HubSpot rate limited (attempt {Attempt}/{MaxAttempts}); waiting {Delay} before retrying.", attempt, maxAttempts, delay);
                await _delayProvider.DelayAsync(delay, cancellationToken);
            }
            catch (HubSpotServerException ex)
            {
                lastException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delay = ComputeBackoff(attempt);
                _logger.LogWarning("HubSpot server error {StatusCode} (attempt {Attempt}/{MaxAttempts}); waiting {Delay} before retrying.", ex.StatusCode, attempt, maxAttempts, delay);
                await _delayProvider.DelayAsync(delay, cancellationToken);
            }
            catch (HubSpotTransientException ex)
            {
                lastException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delay = ComputeBackoff(attempt);
                _logger.LogWarning("HubSpot request failed transiently (attempt {Attempt}/{MaxAttempts}); waiting {Delay} before retrying.", attempt, maxAttempts, delay);
                await _delayProvider.DelayAsync(delay, cancellationToken);
            }
        }

        return new RetryOutcome<T>(false, default, maxAttempts, lastException);
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var multiplier = Math.Pow(2, attempt - 1);
        var delay = TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * multiplier);
        return delay > _options.MaxDelay ? _options.MaxDelay : delay;
    }
}
