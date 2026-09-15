using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

public class EntityMapping
{
    public Guid Id { get; set; }
    public EntityType EntityType { get; set; }
    public Guid InternalId { get; set; }
    public ExternalSystem ExternalSystem { get; set; } = Enums.ExternalSystem.HubSpot;
    public string ExternalId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
