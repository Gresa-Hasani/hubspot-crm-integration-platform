using CrmIntegration.Application.Common;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Sync;

public class SyncJobExecutor : ISyncJobExecutor
{
    private const int MaxStoredErrorLength = 500;

    private readonly ISyncJobRepository _syncJobRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHubSpotRetryExecutor _retryExecutor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly SyncRetryOptions _retryOptions;
    private readonly ILogger<SyncJobExecutor> _logger;

    public SyncJobExecutor(
        ISyncJobRepository syncJobRepository,
        IAuditLogRepository auditLogRepository,
        IHubSpotRetryExecutor retryExecutor,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<SyncRetryOptions> retryOptions,
        ILogger<SyncJobExecutor> logger)
    {
        _syncJobRepository = syncJobRepository;
        _auditLogRepository = auditLogRepository;
        _retryExecutor = retryExecutor;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    public async Task<SyncJob> ExecuteAsync(
        EntityType entityType,
        SyncDirection direction,
        Guid? internalEntityId,
        string? externalId,
        string correlationId,
        Func<CancellationToken, Task<SyncActionResult>> action,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var job = new SyncJob
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            Direction = direction,
            InternalEntityId = internalEntityId,
            ExternalId = externalId,
            CorrelationId = correlationId,
            Status = SyncStatus.Pending,
            StartedAt = now
        };

        await _syncJobRepository.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        job.Status = SyncStatus.Running;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SyncStarted {EntityType} {Direction} InternalId={InternalId} ExternalId={ExternalId} CorrelationId={CorrelationId}",
            entityType, direction, internalEntityId, externalId, correlationId);

        try
        {
            var outcome = await _retryExecutor.ExecuteAsync(() => action(cancellationToken), cancellationToken);
            job.AttemptCount = outcome.AttemptsMade;

            if (outcome.Succeeded)
            {
                var result = outcome.Value!;
                job.InternalEntityId = result.InternalId;
                job.ExternalId = result.ExternalId;
                job.Status = SyncStatus.Succeeded;
                job.RecordsProcessed = 1;
                job.RecordsSucceeded = 1;
                job.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;

                await _auditLogRepository.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = $"SyncSucceeded:{result.Kind}",
                    EntityType = entityType.ToString(),
                    EntityId = result.InternalId.ToString(),
                    Source = "SyncEngine",
                    CorrelationId = correlationId,
                    Metadata = $"{{\"externalId\":\"{result.ExternalId}\",\"direction\":\"{direction}\",\"attempts\":{job.AttemptCount}}}",
                    Timestamp = job.CompletedAt.Value
                }, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "SyncSucceeded {EntityType} {Direction} Kind={Kind} InternalId={InternalId} ExternalId={ExternalId} Attempts={Attempts} CorrelationId={CorrelationId}",
                    entityType, direction, result.Kind, result.InternalId, result.ExternalId, job.AttemptCount, correlationId);
            }
            else
            {
                // Retryable exception type, but attempts exhausted.
                await FailAsync(job, outcome.FinalException, isDeadLetter: true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Deterministic failure — the retry executor never attempted a retry for this one.
            job.AttemptCount = Math.Max(job.AttemptCount, 1);
            await FailAsync(job, ex, isDeadLetter: false, cancellationToken);
            throw;
        }

        return job;
    }

    private async Task FailAsync(SyncJob job, Exception? exception, bool isDeadLetter, CancellationToken cancellationToken)
    {
        job.Status = isDeadLetter ? SyncStatus.DeadLettered : SyncStatus.Failed;
        job.FailureCategory = CategorizeFailure(exception);
        job.ErrorMessage = Truncate(exception?.Message);
        job.RecordsProcessed = 1;
        job.RecordsFailed = 1;
        job.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = isDeadLetter ? "SyncDeadLettered" : "SyncFailed",
            EntityType = job.EntityType.ToString(),
            EntityId = job.InternalEntityId?.ToString() ?? job.ExternalId ?? "unknown",
            Source = "SyncEngine",
            CorrelationId = job.CorrelationId,
            Metadata = $"{{\"failureCategory\":\"{job.FailureCategory}\",\"attempts\":{job.AttemptCount}}}",
            Timestamp = job.CompletedAt.Value
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (isDeadLetter)
        {
            _logger.LogError(
                "SyncDeadLettered {EntityType} {Direction} Category={FailureCategory} Attempts={Attempts} CorrelationId={CorrelationId}",
                job.EntityType, job.Direction, job.FailureCategory, job.AttemptCount, job.CorrelationId);
        }
        else
        {
            _logger.LogWarning(
                "SyncFailed {EntityType} {Direction} Category={FailureCategory} Attempts={Attempts} CorrelationId={CorrelationId}",
                job.EntityType, job.Direction, job.FailureCategory, job.AttemptCount, job.CorrelationId);
        }
    }

    private static string CategorizeFailure(Exception? exception) => exception switch
    {
        HubSpotBadRequestException => "BadRequest",
        HubSpotUnauthorizedException => "Unauthorized",
        HubSpotForbiddenException => "Forbidden",
        HubSpotNotFoundException => "NotFound",
        HubSpotConflictException => "Conflict",
        HubSpotRateLimitedException => "RateLimited",
        HubSpotServerException => "ServerError",
        HubSpotTransientException => "Transient",
        SyncAmbiguousMatchException => "AmbiguousMatch",
        SyncMappingConflictException => "MappingConflict",
        DomainValidationException => "ValidationError",
        EntityNotFoundException => "InternalNotFound",
        _ => "Unknown"
    };

    private static string? Truncate(string? message) =>
        string.IsNullOrEmpty(message) || message.Length <= MaxStoredErrorLength
            ? message
            : message[..MaxStoredErrorLength] + "...(truncated)";
}
