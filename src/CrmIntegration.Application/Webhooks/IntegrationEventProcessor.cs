using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Webhooks;

public class IntegrationEventProcessor : IIntegrationEventProcessor
{
    private readonly IContactSyncService _contactSyncService;
    private readonly ICompanySyncService _companySyncService;
    private readonly IDealSyncService _dealSyncService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly WebhookOptions _options;
    private readonly ILogger<IntegrationEventProcessor> _logger;

    public IntegrationEventProcessor(
        IContactSyncService contactSyncService,
        ICompanySyncService companySyncService,
        IDealSyncService dealSyncService,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<WebhookOptions> options,
        ILogger<IntegrationEventProcessor> logger)
    {
        _contactSyncService = contactSyncService;
        _companySyncService = companySyncService;
        _dealSyncService = dealSyncService;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        integrationEvent.AttemptCount++;
        _logger.LogInformation(
            "WebhookProcessingStarted IntegrationEventId={IntegrationEventId} EntityType={EntityType} ObjectId={ObjectId} Attempt={Attempt} CorrelationId={CorrelationId}",
            integrationEvent.Id, integrationEvent.EntityType, integrationEvent.EntityId, integrationEvent.AttemptCount, integrationEvent.CorrelationId);

        try
        {
            var syncJob = await InvokePhase4SyncAsync(integrationEvent, cancellationToken);

            if (syncJob.Status == SyncStatus.Succeeded)
            {
                await CompleteAsync(integrationEvent, IntegrationEventStatus.Processed, failureCategory: null, errorMessage: null, cancellationToken);
                _logger.LogInformation(
                    "WebhookProcessingSucceeded IntegrationEventId={IntegrationEventId} SyncJobId={SyncJobId} CorrelationId={CorrelationId}",
                    integrationEvent.Id, syncJob.Id, integrationEvent.CorrelationId);
                return;
            }

            // syncJob.Status == DeadLettered: Phase 4 already exhausted its own transient-failure
            // retry budget for this attempt (see docs/WEBHOOKS.md "Retry boundary"). Whether that
            // becomes a terminal DeadLettered IntegrationEvent or gets tried again later depends
            // only on our own (coarser, poll-interval-spaced) attempt budget.
            await HandleFailureAsync(integrationEvent, syncJob.FailureCategory ?? "Unknown", syncJob.ErrorMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            // A deterministic failure (Phase 4 rethrows these rather than retrying them) — never
            // worth retrying at this layer either.
            await CompleteAsync(integrationEvent, IntegrationEventStatus.Failed, CategorizeDeterministicFailure(ex), Truncate(ex.Message), cancellationToken);
            _logger.LogWarning(
                "WebhookProcessingFailed IntegrationEventId={IntegrationEventId} Category={Category} CorrelationId={CorrelationId}",
                integrationEvent.Id, integrationEvent.FailureCategory, integrationEvent.CorrelationId);
        }
    }

    private Task<SyncJob> InvokePhase4SyncAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken) => integrationEvent.EntityType switch
    {
        EntityType.Contact => _contactSyncService.SyncFromHubSpotAsync(integrationEvent.EntityId, integrationEvent.CorrelationId, cancellationToken),
        EntityType.Company => _companySyncService.SyncFromHubSpotAsync(integrationEvent.EntityId, integrationEvent.CorrelationId, cancellationToken),
        EntityType.Deal => _dealSyncService.SyncFromHubSpotAsync(integrationEvent.EntityId, integrationEvent.CorrelationId, cancellationToken),
        _ => throw new InvalidOperationException($"Unsupported EntityType '{integrationEvent.EntityType}' reached the processor — this should have been marked Ignored at ingestion.")
    };

    private async Task HandleFailureAsync(IntegrationEvent integrationEvent, string failureCategory, string? errorMessage, CancellationToken cancellationToken)
    {
        if (integrationEvent.AttemptCount >= _options.MaxProcessingAttempts)
        {
            await CompleteAsync(integrationEvent, IntegrationEventStatus.DeadLettered, failureCategory, errorMessage, cancellationToken);
            _logger.LogError(
                "WebhookDeadLettered IntegrationEventId={IntegrationEventId} Category={Category} Attempts={Attempts} CorrelationId={CorrelationId}",
                integrationEvent.Id, failureCategory, integrationEvent.AttemptCount, integrationEvent.CorrelationId);
        }
        else
        {
            // Leave it claimable again — the background processor's next poll will retry it.
            integrationEvent.Status = IntegrationEventStatus.Received;
            integrationEvent.FailureCategory = failureCategory;
            integrationEvent.LastError = Truncate(errorMessage);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "WebhookProcessingFailed IntegrationEventId={IntegrationEventId} Category={Category} Attempts={Attempts}/{MaxAttempts} — will retry CorrelationId={CorrelationId}",
                integrationEvent.Id, failureCategory, integrationEvent.AttemptCount, _options.MaxProcessingAttempts, integrationEvent.CorrelationId);
        }
    }

    private async Task CompleteAsync(IntegrationEvent integrationEvent, IntegrationEventStatus status, string? failureCategory, string? errorMessage, CancellationToken cancellationToken)
    {
        integrationEvent.Status = status;
        integrationEvent.FailureCategory = failureCategory;
        integrationEvent.LastError = Truncate(errorMessage);
        integrationEvent.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = status == IntegrationEventStatus.Processed ? "WebhookProcessingSucceeded" : $"WebhookProcessing{status}",
            EntityType = integrationEvent.EntityType.ToString(),
            EntityId = integrationEvent.EntityId,
            Source = "WebhookProcessor",
            CorrelationId = integrationEvent.CorrelationId,
            Metadata = $"{{\"integrationEventId\":\"{integrationEvent.Id}\",\"attempts\":{integrationEvent.AttemptCount},\"failureCategory\":\"{failureCategory}\"}}",
            Timestamp = integrationEvent.ProcessedAt.Value
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string CategorizeDeterministicFailure(Exception ex) => ex.GetType().Name;

    private static string? Truncate(string? message) =>
        string.IsNullOrEmpty(message) || message.Length <= 500 ? message : message[..500] + "...(truncated)";
}
