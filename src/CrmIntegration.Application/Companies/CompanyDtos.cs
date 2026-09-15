using System.ComponentModel.DataAnnotations;

namespace CrmIntegration.Application.Companies;

public record CompanyResponse(
    Guid Id,
    string? HubSpotId,
    string Name,
    string? Domain,
    string? Industry,
    string? Country,
    int? EmployeeCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSyncedAt);

public class CreateCompanyRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Domain { get; set; }

    [StringLength(150)]
    public string? Industry { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Range(0, int.MaxValue)]
    public int? EmployeeCount { get; set; }
}

public class UpdateCompanyRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Domain { get; set; }

    [StringLength(150)]
    public string? Industry { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Range(0, int.MaxValue)]
    public int? EmployeeCount { get; set; }
}
