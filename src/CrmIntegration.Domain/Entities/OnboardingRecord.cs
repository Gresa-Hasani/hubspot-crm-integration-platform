using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class OnboardingRecord
{
    public Guid Id { get; set; }
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Primary Contact for the handoff, when the Deal had one at Closed Won time — see docs/SALES_AUTOMATION.md "Missing Company/Contact policy".</summary>
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public OnboardingStatus Status { get; set; } = OnboardingStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TriggerSource { get; set; } = string.Empty;
}
