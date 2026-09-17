using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>No-op fake for tests that exercise a sync/CRUD service without caring about automation side effects.</summary>
public class NoOpDealStageAutomationService : IDealStageAutomationService
{
    public List<(Deal Deal, DealStage? PreviousStage, TransitionSource Source)> Calls { get; } = new();

    public Task<DealStageTransition?> EvaluateAsync(Deal deal, DealStage? previousStage, TransitionSource source, string correlationId, Guid? integrationEventId = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((deal, previousStage, source));
        return Task.FromResult<DealStageTransition?>(null);
    }
}

/// <summary>Spy for IAutomationExecutor: records every call and, by default, actually invokes the action (so callers can assert on its resulting AutomationOutcome/side effects) without any of the real executor's persistence/idempotency machinery.</summary>
public class FakeAutomationExecutor : IAutomationExecutor
{
    public List<(AutomationType AutomationType, EntityType EntityType, Guid EntityId, string IdempotencyKey, string CorrelationId)> Calls { get; } = new();

    public async Task<AutomationExecution> ExecuteAsync(
        AutomationType automationType,
        EntityType entityType,
        Guid entityId,
        string idempotencyKey,
        string correlationId,
        Func<CancellationToken, Task<AutomationOutcome>> action,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((automationType, entityType, entityId, idempotencyKey, correlationId));
        var outcome = await action(cancellationToken);
        return new AutomationExecution
        {
            Id = Guid.NewGuid(),
            AutomationType = automationType,
            EntityType = entityType,
            EntityId = entityId,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            Status = outcome.Status,
            ResultSummary = outcome.ResultSummary,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }
}

/// <summary>No-op fake for tests that exercise a sync/CRUD service without caring about automation side effects.</summary>
public class NoOpContactLifecycleAutomationService : IContactLifecycleAutomationService
{
    public List<(Contact Contact, LifecycleStage? PreviousStage, TransitionSource Source)> Calls { get; } = new();

    public Task<ContactLifecycleTransition?> EvaluateAsync(Contact contact, LifecycleStage? previousStage, TransitionSource source, string correlationId, Guid? integrationEventId = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((contact, previousStage, source));
        return Task.FromResult<ContactLifecycleTransition?>(null);
    }
}
