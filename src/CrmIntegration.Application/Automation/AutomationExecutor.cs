using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Automation;

public class AutomationExecutor : IAutomationExecutor
{
    private const int MaxStoredErrorLength = 500;

    private readonly IAutomationExecutionRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutomationExecutor> _logger;

    public AutomationExecutor(
        IAutomationExecutionRepository repository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<AutomationExecutor> logger)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AutomationExecution> ExecuteAsync(
        AutomationType automationType,
        EntityType entityType,
        Guid entityId,
        string idempotencyKey,
        string correlationId,
        Func<CancellationToken, Task<AutomationOutcome>> action,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null && existing.Status is AutomationStatus.Succeeded or AutomationStatus.Skipped or AutomationStatus.Failed)
        {
            _logger.LogInformation(
                "AutomationSkipped (already executed) AutomationType={AutomationType} EntityId={EntityId} IdempotencyKey={IdempotencyKey} PreviousStatus={PreviousStatus}",
                automationType, entityId, idempotencyKey, existing.Status);
            return existing;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var execution = new AutomationExecution
        {
            Id = Guid.NewGuid(),
            AutomationType = automationType,
            EntityType = entityType,
            EntityId = entityId,
            IdempotencyKey = idempotencyKey,
            Status = AutomationStatus.Pending,
            CorrelationId = correlationId,
            StartedAt = now
        };

        await _repository.AddAsync(execution, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (SyncMappingConflictException)
        {
            // A concurrent caller won the race to create this exact idempotency key first.
            // Their execution is authoritative — reuse it rather than running the action twice.
            var winner = await _repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException($"Lost a race creating AutomationExecution '{idempotencyKey}' but the winning row could not be found.");
            _logger.LogInformation(
                "AutomationSkipped (lost concurrent race) AutomationType={AutomationType} EntityId={EntityId} IdempotencyKey={IdempotencyKey}",
                automationType, entityId, idempotencyKey);
            return winner;
        }

        _logger.LogInformation("AutomationStarted AutomationType={AutomationType} EntityId={EntityId} CorrelationId={CorrelationId}", automationType, entityId, correlationId);

        execution.Status = AutomationStatus.Running;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var outcome = await action(cancellationToken);
            execution.Status = outcome.Status;
            execution.ResultSummary = outcome.ResultSummary;
            execution.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _auditLogRepository.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = $"Automation{execution.Status}",
                EntityType = entityType.ToString(),
                EntityId = entityId.ToString(),
                Source = "SalesAutomation",
                CorrelationId = correlationId,
                Metadata = $"{{\"automationType\":\"{automationType}\",\"executionId\":\"{execution.Id}\",\"resultSummary\":\"{outcome.ResultSummary}\"}}",
                Timestamp = execution.CompletedAt.Value
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Automation{Status} AutomationType={AutomationType} EntityId={EntityId} Summary={Summary} CorrelationId={CorrelationId}",
                execution.Status, automationType, entityId, outcome.ResultSummary, correlationId);
        }
        catch (Exception ex)
        {
            execution.Status = AutomationStatus.Failed;
            execution.FailureCategory = ex.GetType().Name;
            execution.ErrorMessage = Truncate(ex.Message);
            execution.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _auditLogRepository.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "AutomationFailed",
                EntityType = entityType.ToString(),
                EntityId = entityId.ToString(),
                Source = "SalesAutomation",
                CorrelationId = correlationId,
                Metadata = $"{{\"automationType\":\"{automationType}\",\"executionId\":\"{execution.Id}\",\"failureCategory\":\"{execution.FailureCategory}\"}}",
                Timestamp = execution.CompletedAt.Value
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(ex, "AutomationFailed AutomationType={AutomationType} EntityId={EntityId} CorrelationId={CorrelationId}", automationType, entityId, correlationId);
        }

        return execution;
    }

    private static string? Truncate(string? message) =>
        string.IsNullOrEmpty(message) || message.Length <= MaxStoredErrorLength ? message : message[..MaxStoredErrorLength] + "...(truncated)";
}
