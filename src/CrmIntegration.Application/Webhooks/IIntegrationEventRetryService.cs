using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Webhooks;

/// <summary>Manually re-queues a DeadLettered IntegrationEvent for another processing pass.</summary>
public interface IIntegrationEventRetryService
{
    Task<IntegrationEvent> RetryAsync(Guid integrationEventId, CancellationToken cancellationToken = default);
}
