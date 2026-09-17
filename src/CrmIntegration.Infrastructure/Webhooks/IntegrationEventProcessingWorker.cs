using CrmIntegration.Application.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Infrastructure.Webhooks;

/// <summary>
/// Polls PostgreSQL for Received IntegrationEvents and processes them one at a time. PostgreSQL
/// (via IIntegrationEventRepository.TryClaimNextAsync) remains the durable, authoritative queue —
/// this class holds no state that would be lost on restart; it just wakes up periodically and
/// asks the database what's next. See docs/WEBHOOKS.md "Durable processing" for why this (and
/// not an in-memory Channel, or an external broker) is the right amount of infrastructure here.
/// </summary>
public class IntegrationEventProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WebhookOptions _options;
    private readonly ILogger<IntegrationEventProcessingWorker> _logger;

    public IntegrationEventProcessingWorker(IServiceScopeFactory scopeFactory, IOptions<WebhookOptions> options, ILogger<IntegrationEventProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Drain everything currently claimable before sleeping, so a burst of webhook
                // deliveries doesn't wait a full polling interval per event.
                while (await ProcessOneAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
                {
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "IntegrationEventProcessingWorker encountered an unexpected error and will retry after the next polling interval.");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();

        var claimed = await repository.TryClaimNextAsync(cancellationToken);
        if (claimed is null)
        {
            return false;
        }

        _logger.LogInformation("WebhookEventClaimed IntegrationEventId={IntegrationEventId} CorrelationId={CorrelationId}", claimed.Id, claimed.CorrelationId);

        var processor = scope.ServiceProvider.GetRequiredService<IIntegrationEventProcessor>();
        await processor.ProcessAsync(claimed, cancellationToken);
        return true;
    }
}
