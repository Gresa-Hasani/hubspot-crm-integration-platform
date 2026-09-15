using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class OnboardingRecord
{
    public Guid Id { get; set; }
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TriggerSource { get; set; } = string.Empty;
}
