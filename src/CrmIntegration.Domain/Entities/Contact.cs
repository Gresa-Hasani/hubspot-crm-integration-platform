using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; }
    public string? HubSpotId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public LifecycleStage LifecycleStage { get; set; } = LifecycleStage.Lead;
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
