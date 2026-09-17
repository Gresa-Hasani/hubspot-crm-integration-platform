using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Automation;

public class ContactLifecycleAutomationService : IContactLifecycleAutomationService
{
    private readonly IContactLifecycleTransitionRepository _transitionRepository;
    private readonly IAutomationExecutor _automationExecutor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContactLifecycleAutomationService> _logger;

    public ContactLifecycleAutomationService(
        IContactLifecycleTransitionRepository transitionRepository,
        IAutomationExecutor automationExecutor,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ContactLifecycleAutomationService> logger)
    {
        _transitionRepository = transitionRepository;
        _automationExecutor = automationExecutor;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ContactLifecycleTransition?> EvaluateAsync(
        Contact contact,
        LifecycleStage? previousStage,
        TransitionSource source,
        string correlationId,
        Guid? integrationEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (previousStage == contact.LifecycleStage)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var transition = new ContactLifecycleTransition
        {
            Id = Guid.NewGuid(),
            ContactId = contact.Id,
            FromStage = previousStage,
            ToStage = contact.LifecycleStage,
            OccurredAt = now,
            Source = source,
            CorrelationId = correlationId,
            IntegrationEventId = integrationEventId,
            CreatedAt = now
        };

        await _transitionRepository.AddAsync(transition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ContactLifecycleChanged ContactId={ContactId} FromStage={FromStage} ToStage={ToStage} Source={Source} CorrelationId={CorrelationId}",
            contact.Id, previousStage, contact.LifecycleStage, source, correlationId);

        // Phase 6 scope: track + audit every meaningful transition. No side-effecting business
        // rule is fabricated for any specific stage (see docs/SALES_AUTOMATION.md) — this simply
        // gives future phases a durable, idempotent hook to build on.
        await _automationExecutor.ExecuteAsync(
            AutomationType.ContactLifecycleTransition,
            EntityType.Contact,
            contact.Id,
            idempotencyKey: $"ContactLifecycleTransition:{transition.Id}",
            correlationId,
            _ => Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, $"Lifecycle changed {previousStage?.ToString() ?? "(new)"} -> {contact.LifecycleStage}")),
            cancellationToken);

        return transition;
    }
}
