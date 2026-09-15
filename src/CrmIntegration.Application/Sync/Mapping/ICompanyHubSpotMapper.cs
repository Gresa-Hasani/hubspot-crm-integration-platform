using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Mapping;

public interface ICompanyHubSpotMapper
{
    IReadOnlyList<string> HubSpotProperties { get; }
    IReadOnlyDictionary<string, string?> ToHubSpotProperties(Company company);
    void ApplyHubSpotProperties(Company company, HubSpotRecord record);
}
