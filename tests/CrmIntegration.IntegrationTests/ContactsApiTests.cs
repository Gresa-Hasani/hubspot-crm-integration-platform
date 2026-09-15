using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using Xunit;

namespace CrmIntegration.IntegrationTests;

public class ContactsApiTests : IClassFixture<CrmApiFactory>
{
    private readonly HttpClient _client;

    public ContactsApiTests(CrmApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateThenGet_RoundTripsANormalizedContact()
    {
        var uniqueEmail = $"alice.{Guid.NewGuid():N}@acme.example";

        var createResponse = await _client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = $"  {uniqueEmail.ToUpperInvariant()}  ",
            Phone = "+1 (555) 123-4567"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(uniqueEmail, created!.Email);
        Assert.Equal("+15551234567", created.Phone);

        var getResponse = await _client.GetAsync($"/api/contacts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCompanyIdDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
        {
            FirstName = "Bob",
            LastName = "Jones",
            Email = $"bob.{Guid.NewGuid():N}@acme.example",
            CompanyId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var response = await _client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
        {
            FirstName = "Carl",
            LastName = "Doe",
            Email = "not-an-email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContact_AssociatedWithCompany_PersistsTheAssociation()
    {
        var companyResponse = await _client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest
        {
            Name = "Globex " + Guid.NewGuid().ToString("N")[..6]
        });
        var company = await companyResponse.Content.ReadFromJsonAsync<CompanyResponse>(CrmApiFactory.JsonOptions);

        var contactResponse = await _client.PostAsJsonAsync("/api/contacts", new CreateContactRequest
        {
            FirstName = "Dana",
            LastName = "Lee",
            Email = $"dana.{Guid.NewGuid():N}@globex.example",
            CompanyId = company!.Id
        });

        Assert.Equal(HttpStatusCode.Created, contactResponse.StatusCode);
        var contact = await contactResponse.Content.ReadFromJsonAsync<ContactResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(company.Id, contact!.CompanyId);
    }
}
