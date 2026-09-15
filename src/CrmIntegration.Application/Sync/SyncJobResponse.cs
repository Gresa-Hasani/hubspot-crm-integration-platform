using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Sync;

public record SyncJobResponse(
    Guid Id,
    EntityType EntityType,
    SyncDirection Direction,
    SyncStatus Status,
    Guid? InternalEntityId,
    string? ExternalId,
    string CorrelationId,
    int AttemptCount,
    string? FailureCategory,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    public static SyncJobResponse FromEntity(SyncJob job) => new(
        job.Id, job.EntityType, job.Direction, job.Status, job.InternalEntityId, job.ExternalId,
        job.CorrelationId, job.AttemptCount, job.FailureCategory, job.ErrorMessage, job.StartedAt, job.CompletedAt);
}
