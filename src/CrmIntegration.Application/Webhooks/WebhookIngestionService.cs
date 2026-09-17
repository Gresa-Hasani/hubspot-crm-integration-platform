using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Webhooks;

public class WebhookIngestionService : IWebhookIngestionService
{
    private readonly IWebhookPayloadParser _parser;
    private readonly IIntegrationEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookIngestionService> _logger;

    public WebhookIngestionService(
        IWebhookPayloadParser parser,
        IIntegrationEventRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<WebhookIngestionService> logger)
    {
        _parser = parser;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<WebhookIngestionResult> IngestAsync(string requestBody, string correlationId, CancellationToken cancellationToken = default)
    {
        if (!_parser.TryParse(requestBody, out var events, out var error))
        {
            _logger.LogWarning("WebhookMalformedPayload CorrelationId={CorrelationId} Reason={Reason}", correlationId, error);
            return WebhookIngestionResult.Malformed(error ?? "Malformed webhook payload.");
        }

        var accepted = 0;
        var duplicates = 0;
        var ignored = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var webhookEvent in events)
        {
            if (await _repository.ExistsAsync(webhookEvent.EventId, cancellationToken))
            {
                duplicates++;
                _logger.LogInformation("WebhookDuplicateDetected EventId={EventId} CorrelationId={CorrelationId}", webhookEvent.EventId, correlationId);
                continue;
            }

            var entityType = WebhookSubscriptionTypeClassifier.TryGetEntityType(webhookEvent.SubscriptionType);
            var isSupported = WebhookSubscriptionTypeClassifier.IsSupported(webhookEvent.SubscriptionType);

            var integrationEvent = new IntegrationEvent
            {
                Id = Guid.NewGuid(),
                ExternalEventId = webhookEvent.EventId,
                EventType = webhookEvent.SubscriptionType,
                EntityType = entityType ?? EntityType.Contact, // placeholder; Status=Ignored below means it's never acted on for unsupported object types
                EntityId = webhookEvent.ObjectId,
                Payload = SerializeSafely(webhookEvent),
                Status = isSupported ? IntegrationEventStatus.Received : IntegrationEventStatus.Ignored,
                HubSpotAttemptNumber = webhookEvent.AttemptNumber,
                ReceivedAt = now,
                OccurredAt = webhookEvent.OccurredAt.UtcDateTime,
                CorrelationId = correlationId
            };

            var wasAdded = await _repository.TryAddAsync(integrationEvent, cancellationToken);
            if (!wasAdded)
            {
                // Lost a race with a concurrent duplicate delivery between the ExistsAsync check
                // and the insert — the unique index is the real backstop (see docs/WEBHOOKS.md).
                duplicates++;
                _logger.LogInformation("WebhookDuplicateDetected EventId={EventId} CorrelationId={CorrelationId} (race with concurrent delivery)", webhookEvent.EventId, correlationId);
                continue;
            }

            if (isSupported)
            {
                accepted++;
                _logger.LogInformation(
                    "WebhookEventPersisted EventId={EventId} SubscriptionType={SubscriptionType} ObjectId={ObjectId} CorrelationId={CorrelationId}",
                    webhookEvent.EventId, webhookEvent.SubscriptionType, webhookEvent.ObjectId, correlationId);
            }
            else
            {
                ignored++;
                _logger.LogInformation(
                    "WebhookUnsupportedEvent EventId={EventId} SubscriptionType={SubscriptionType} CorrelationId={CorrelationId}",
                    webhookEvent.EventId, webhookEvent.SubscriptionType, correlationId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return WebhookIngestionResult.Success(accepted, duplicates, ignored);
    }

    private static string SerializeSafely(WebhookEvent webhookEvent) =>
        System.Text.Json.JsonSerializer.Serialize(webhookEvent);
}
