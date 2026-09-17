using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Webhooks;

public class IntegrationEventRetryService : IIntegrationEventRetryService
{
    private readonly IIntegrationEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IntegrationEventRetryService> _logger;

    public IntegrationEventRetryService(IIntegrationEventRepository repository, IUnitOfWork unitOfWork, ILogger<IntegrationEventRetryService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IntegrationEvent> RetryAsync(Guid integrationEventId, CancellationToken cancellationToken = default)
    {
        var integrationEvent = await _repository.GetByIdAsync(integrationEventId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(IntegrationEvent), integrationEventId);

        if (integrationEvent.Status != IntegrationEventStatus.DeadLettered)
        {
            throw new DomainValidationException($"IntegrationEvent '{integrationEventId}' is '{integrationEvent.Status}', not DeadLettered; only dead-lettered events can be manually retried.");
        }

        integrationEvent.Status = IntegrationEventStatus.Received;
        integrationEvent.AttemptCount = 0;
        integrationEvent.FailureCategory = null;
        integrationEvent.LastError = null;
        integrationEvent.ProcessedAt = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("WebhookRetryRequested IntegrationEventId={IntegrationEventId} CorrelationId={CorrelationId}", integrationEvent.Id, integrationEvent.CorrelationId);

        return integrationEvent;
    }
}
