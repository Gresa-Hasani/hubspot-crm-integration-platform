using System.ComponentModel.DataAnnotations;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Contacts;

public record ContactResponse(
    Guid Id,
    string? HubSpotId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    LifecycleStage LifecycleStage,
    string? Source,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSyncedAt);

public class CreateContactRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? JobTitle { get; set; }

    public Guid? CompanyId { get; set; }

    public LifecycleStage LifecycleStage { get; set; } = LifecycleStage.Lead;

    [StringLength(100)]
    public string? Source { get; set; }
}

public class UpdateContactRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? JobTitle { get; set; }

    public Guid? CompanyId { get; set; }

    public LifecycleStage LifecycleStage { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }
}
