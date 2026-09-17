using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

/// <summary>
/// Detects Deal.Stage transitions and triggers stage-based automation (currently: Closed Won ->
/// onboarding). Callable from both the HubSpot sync path and the internal REST update path —
/// see docs/SALES_AUTOMATION.md "HubSpot-sync integration" / "Internal-update integration".
/// </summary>
public interface IDealStageAutomationService
{
    /// <param name="deal">The Deal in its already-persisted, authoritative post-update state.</param>
    /// <param name="previousStage">The Stage value before this update was applied — null if the Deal was just created/imported.</param>
    /// <returns>The transition row if one was recorded, or null if the stage didn't actually change.</returns>
    Task<DealStageTransition?> EvaluateAsync(
        Deal deal,
        DealStage? previousStage,
        TransitionSource source,
        string correlationId,
        Guid? integrationEventId = null,
        CancellationToken cancellationToken = default);
}
