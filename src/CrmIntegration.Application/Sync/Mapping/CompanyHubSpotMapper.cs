using CrmIntegration.Application.Common;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Mapping;

public class CompanyHubSpotMapper : ICompanyHubSpotMapper
{
    public IReadOnlyList<string> HubSpotProperties { get; } =
        ["name", "domain", "industry", "country", "numberofemployees"];

    public IReadOnlyDictionary<string, string?> ToHubSpotProperties(Company company) => new Dictionary<string, string?>
    {
        ["name"] = company.Name,
        ["domain"] = company.Domain,
        ["industry"] = company.Industry,
        ["country"] = company.Country,
        ["numberofemployees"] = company.EmployeeCount?.ToString()
    };

    public void ApplyHubSpotProperties(Company company, HubSpotRecord record)
    {
        if (record.Properties.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            company.Name = Normalization.NormalizeCompanyName(name);
        }

        if (record.Properties.TryGetValue("domain", out var domain))
        {
            company.Domain = Normalization.NormalizeDomain(domain);
        }

        if (record.Properties.TryGetValue("industry", out var industry))
        {
            company.Industry = industry?.Trim();
        }

        if (record.Properties.TryGetValue("country", out var country))
        {
            company.Country = Normalization.NormalizeCountry(country);
        }

        if (record.Properties.TryGetValue("numberofemployees", out var employeeCount)
            && int.TryParse(employeeCount, out var parsed))
        {
            company.EmployeeCount = parsed;
        }
    }
}
