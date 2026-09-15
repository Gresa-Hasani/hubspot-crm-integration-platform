using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using Xunit;

namespace CrmIntegration.IntegrationTests;

public class CompaniesApiTests : IClassFixture<CrmApiFactory>
{
    private readonly HttpClient _client;

    public CompaniesApiTests(CrmApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateThenGet_RoundTripsANormalizedCompany()
    {
        var request = new CreateCompanyRequest
        {
            Name = "  Acme   Technologies  ",
            Domain = "https://www.acme-" + Guid.NewGuid().ToString("N")[..8] + ".example/",
            Country = "USA",
            EmployeeCount = 250
        };

        var createResponse = await _client.PostAsJsonAsync("/api/companies", request, CrmApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Acme Technologies", created!.Name);
        Assert.Equal("United States", created.Country);
        Assert.DoesNotContain("www.", created.Domain);

        var getResponse = await _client.GetAsync($"/api/companies/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.GetAsync($"/api/companies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = string.Empty }, CrmApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
