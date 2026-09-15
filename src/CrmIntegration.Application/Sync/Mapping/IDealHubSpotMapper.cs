using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Mapping;

public interface IDealHubSpotMapper
{
    IReadOnlyList<string> HubSpotProperties { get; }
    IReadOnlyDictionary<string, string?> ToHubSpotProperties(Deal deal);
    void ApplyHubSpotProperties(Deal deal, HubSpotRecord record);
}
