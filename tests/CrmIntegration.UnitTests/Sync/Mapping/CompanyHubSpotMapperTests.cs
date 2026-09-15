using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Domain.Entities;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Mapping;

public class CompanyHubSpotMapperTests
{
    [Fact]
    public void ToHubSpotProperties_MapsAllFields()
    {
        var mapper = new CompanyHubSpotMapper();
        var company = new Company { Name = "Acme Technologies", Domain = "acme.example", Industry = "Software", Country = "United States", EmployeeCount = 250 };

        var properties = mapper.ToHubSpotProperties(company);

        Assert.Equal("Acme Technologies", properties["name"]);
        Assert.Equal("acme.example", properties["domain"]);
        Assert.Equal("Software", properties["industry"]);
        Assert.Equal("United States", properties["country"]);
        Assert.Equal("250", properties["numberofemployees"]);
    }

    [Fact]
    public void ApplyHubSpotProperties_NormalizesIncomingValues()
    {
        var mapper = new CompanyHubSpotMapper();
        var company = new Company();
        var record = new HubSpotRecord("1", new Dictionary<string, string?>
        {
            ["name"] = "  Globex   Corp  ",
            ["domain"] = "https://www.globex.example/",
            ["country"] = "USA",
            ["numberofemployees"] = "42"
        }, null, null);

        mapper.ApplyHubSpotProperties(company, record);

        Assert.Equal("Globex Corp", company.Name);
        Assert.Equal("globex.example", company.Domain);
        Assert.Equal("United States", company.Country);
        Assert.Equal(42, company.EmployeeCount);
    }
}
