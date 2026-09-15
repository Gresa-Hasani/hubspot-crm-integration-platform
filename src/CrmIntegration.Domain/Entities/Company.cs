namespace CrmIntegration.Domain.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string? HubSpotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public int? EmployeeCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
