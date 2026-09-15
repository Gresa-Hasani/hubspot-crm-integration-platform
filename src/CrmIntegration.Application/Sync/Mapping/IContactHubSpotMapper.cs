using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Mapping;

/// <summary>
/// Explicit translation between the internal Contact entity and HubSpot's contact properties.
/// This is the only place Contact<->HubSpot field names should appear — see docs/FIELD_MAPPING.md.
/// </summary>
public interface IContactHubSpotMapper
{
    /// <summary>HubSpot property names this mapper reads/writes, for GET ...?properties= calls.</summary>
    IReadOnlyList<string> HubSpotProperties { get; }

    IReadOnlyDictionary<string, string?> ToHubSpotProperties(Contact contact);

    /// <summary>Applies HubSpot's values onto the internal entity (HubSpot -> Internal direction). Does not touch Id/CreatedAt/CompanyId.</summary>
    void ApplyHubSpotProperties(Contact contact, HubSpotRecord record);
}
