using System.ComponentModel.DataAnnotations;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Deals;

public record DealResponse(
    Guid Id,
    string? HubSpotId,
    string Name,
    Guid? CompanyId,
    Guid? ContactId,
    DealStage Stage,
    decimal Amount,
    string Currency,
    DateTime? CloseDate,
    string? Owner,
    DealStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSyncedAt);

public class CreateDealRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }

    public Guid? ContactId { get; set; }

    public DealStage Stage { get; set; } = DealStage.QualifiedToBuy;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    public DateTime? CloseDate { get; set; }

    [StringLength(200)]
    public string? Owner { get; set; }
}

public class UpdateDealRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }

    public Guid? ContactId { get; set; }

    public DealStage Stage { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    public DateTime? CloseDate { get; set; }

    [StringLength(200)]
    public string? Owner { get; set; }
}
