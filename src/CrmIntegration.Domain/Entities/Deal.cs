using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class Deal
{
    public Guid Id { get; set; }
    public string? HubSpotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public DealStage Stage { get; set; } = DealStage.QualifiedToBuy;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime? CloseDate { get; set; }
    public string? Owner { get; set; }
    public DealStatus Status { get; set; } = DealStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
