using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

/// <summary>
/// One execution of a business-workflow automation (Closed-Won onboarding, contact lifecycle
/// transition handling). See docs/SALES_AUTOMATION.md for the idempotency-key design — this is
/// deliberately not the only defense against duplicate side effects (e.g. OnboardingRecords'
/// own unique DealId constraint is the final backstop for onboarding specifically), but it does
/// give one auditable row per meaningful automation attempt.
/// </summary>
public class AutomationExecution
{
    public Guid Id { get; set; }
    public AutomationType AutomationType { get; set; }
    public EntityType EntityType { get; set; }

    /// <summary>Internal id of the Deal/Contact the automation ran for.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Unique per meaningful automation attempt — see docs/SALES_AUTOMATION.md for exactly what it's derived from per AutomationType.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public AutomationStatus Status { get; set; } = AutomationStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Short, safe category — never a raw exception with payload/token content.</summary>
    public string? FailureCategory { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Short, safe human-readable outcome, e.g. "Created OnboardingRecord" / "Reused existing OnboardingRecord" / "Skipped: Deal has no Company".</summary>
    public string? ResultSummary { get; set; }
}
