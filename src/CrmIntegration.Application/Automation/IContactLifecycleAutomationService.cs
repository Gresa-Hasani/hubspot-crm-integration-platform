using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

/// <summary>
/// Detects Contact.LifecycleStage transitions, persists history, and creates an audited
/// AutomationExecution for each meaningful change. Deliberately conservative for Phase 6 — see
/// docs/SALES_AUTOMATION.md "Contact lifecycle business rules": no emails/campaigns/tasks are
/// fabricated, only transition tracking + execution/audit.
/// </summary>
public interface IContactLifecycleAutomationService
{
    Task<ContactLifecycleTransition?> EvaluateAsync(
        Contact contact,
        LifecycleStage? previousStage,
        TransitionSource source,
        string correlationId,
        Guid? integrationEventId = null,
        CancellationToken cancellationToken = default);
}
