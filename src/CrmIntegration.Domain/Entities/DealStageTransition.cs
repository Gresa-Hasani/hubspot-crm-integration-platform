using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

/// <summary>
/// One recorded change of Deal.Stage. Only created when the stage actually changed — a sync or
/// webhook re-delivery that leaves Stage unchanged must never create a row here (see
/// docs/SALES_AUTOMATION.md "Detecting stage transitions").
/// </summary>
public class DealStageTransition
{
    public Guid Id { get; set; }
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }

    /// <summary>Null when the Deal was created/imported already in ToStage (no prior known stage).</summary>
    public DealStage? FromStage { get; set; }

    public DealStage ToStage { get; set; }
    public DateTime OccurredAt { get; set; }
    public TransitionSource Source { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>The Phase 5 IntegrationEvent that triggered this, when known — see docs/SALES_AUTOMATION.md for why this isn't always populated.</summary>
    public Guid? IntegrationEventId { get; set; }

    public DateTime CreatedAt { get; set; }
}
