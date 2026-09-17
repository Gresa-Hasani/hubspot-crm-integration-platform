using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

public record AutomationExecutionResponse(
    Guid Id,
    AutomationType AutomationType,
    EntityType EntityType,
    Guid EntityId,
    AutomationStatus Status,
    string CorrelationId,
    string? FailureCategory,
    string? ErrorMessage,
    string? ResultSummary,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    public static AutomationExecutionResponse FromEntity(AutomationExecution e) => new(
        e.Id, e.AutomationType, e.EntityType, e.EntityId, e.Status, e.CorrelationId,
        e.FailureCategory, e.ErrorMessage, e.ResultSummary, e.StartedAt, e.CompletedAt);
}

public record DealStageTransitionResponse(
    Guid Id,
    Guid DealId,
    DealStage? FromStage,
    DealStage ToStage,
    DateTime OccurredAt,
    TransitionSource Source,
    string CorrelationId)
{
    public static DealStageTransitionResponse FromEntity(DealStageTransition t) => new(
        t.Id, t.DealId, t.FromStage, t.ToStage, t.OccurredAt, t.Source, t.CorrelationId);
}

public record ContactLifecycleTransitionResponse(
    Guid Id,
    Guid ContactId,
    LifecycleStage? FromStage,
    LifecycleStage ToStage,
    DateTime OccurredAt,
    TransitionSource Source,
    string CorrelationId)
{
    public static ContactLifecycleTransitionResponse FromEntity(ContactLifecycleTransition t) => new(
        t.Id, t.ContactId, t.FromStage, t.ToStage, t.OccurredAt, t.Source, t.CorrelationId);
}

public record OnboardingRecordResponse(
    Guid Id,
    Guid DealId,
    Guid CompanyId,
    Guid? ContactId,
    OnboardingStatus Status,
    string TriggerSource,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static OnboardingRecordResponse FromEntity(OnboardingRecord o) => new(
        o.Id, o.DealId, o.CompanyId, o.ContactId, o.Status, o.TriggerSource, o.CreatedAt, o.UpdatedAt);
}
