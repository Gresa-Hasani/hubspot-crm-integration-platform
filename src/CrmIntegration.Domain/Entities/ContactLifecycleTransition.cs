using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

/// <summary>One recorded change of Contact.LifecycleStage. Only created when the stage actually changed.</summary>
public class ContactLifecycleTransition
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Contact? Contact { get; set; }

    /// <summary>Null when the Contact was created/imported already in ToStage.</summary>
    public LifecycleStage? FromStage { get; set; }

    public LifecycleStage ToStage { get; set; }
    public DateTime OccurredAt { get; set; }
    public TransitionSource Source { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? IntegrationEventId { get; set; }
    public DateTime CreatedAt { get; set; }
}
