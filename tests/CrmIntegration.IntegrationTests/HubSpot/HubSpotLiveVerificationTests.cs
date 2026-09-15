using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Infrastructure.HubSpot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace CrmIntegration.IntegrationTests.HubSpot;

/// <summary>
/// Exercises the real HubSpot API end-to-end (Company -> Contact -> association -> Deal ->
/// associations -> retrieve -> update) using ONLY fictional test data. This is NOT part of the
/// deterministic CI suite: it is a no-op unless HubSpot__AccessToken in the environment is a real
/// token (not the placeholder committed to .env.example), since there is no way to call the real
/// HubSpot API without one. Run manually against a HubSpot developer/test account:
///
///   $env:HubSpot__AccessToken = "&lt;your real private-app token&gt;"
///   dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~HubSpotLiveVerification
///
/// All objects created here are named "... (CRM Platform Test)" so they're easy to find and
/// delete from the portal afterwards; this test does not delete them itself.
/// </summary>
public class HubSpotLiveVerificationTests
{
    private readonly ITestOutputHelper _output;

    public HubSpotLiveVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FullCrudAndAssociationFlow_AgainstRealHubSpotPortal()
    {
        var accessToken = Environment.GetEnvironmentVariable("HubSpot__AccessToken");
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Contains("00000000"))
        {
            _output.WriteLine("SKIPPED (no-op): set a real HubSpot__AccessToken environment variable to run this against a live portal.");
            return;
        }

        var options = Options.Create(new HubSpotOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("HubSpot__BaseUrl") ?? "https://api.hubapi.com",
            AccessToken = accessToken,
            RequestTimeoutSeconds = 30
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
        IHubSpotClient client = new HubSpotClient(httpClient, NullLogger<HubSpotClient>.Instance);

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var company = await client.CreateCompanyAsync(new Dictionary<string, string?>
        {
            ["name"] = $"Acme Technologies (CRM Platform Test {suffix})",
            ["domain"] = $"acme-test-{suffix}.example"
        });
        _output.WriteLine($"Created company {company.Id}");

        var contact = await client.CreateContactAsync(new Dictionary<string, string?>
        {
            ["email"] = $"alice.test.{suffix}@acme-test.example",
            ["firstname"] = "Alice",
            ["lastname"] = $"Smith (Test {suffix})"
        });
        _output.WriteLine($"Created contact {contact.Id}");

        await client.CreateAssociationAsync(HubSpotObjectType.Contact, contact.Id, HubSpotObjectType.Company, company.Id);
        _output.WriteLine("Associated contact -> company");

        var deal = await client.CreateDealAsync(new Dictionary<string, string?>
        {
            ["dealname"] = $"Enterprise Platform Subscription (Test {suffix})",
            ["amount"] = "25000",
            ["dealstage"] = "qualifiedtobuy",
            ["pipeline"] = "default"
        });
        _output.WriteLine($"Created deal {deal.Id}");

        await client.CreateAssociationAsync(HubSpotObjectType.Deal, deal.Id, HubSpotObjectType.Company, company.Id);
        await client.CreateAssociationAsync(HubSpotObjectType.Deal, deal.Id, HubSpotObjectType.Contact, contact.Id);
        _output.WriteLine("Associated deal -> company and deal -> contact");

        var fetchedCompany = await client.GetCompanyAsync(company.Id, new[] { "name", "domain" });
        Assert.NotNull(fetchedCompany);
        Assert.Equal(company.Properties["name"], fetchedCompany!.Properties["name"]);

        var associatedContacts = await client.GetAssociatedIdsAsync(HubSpotObjectType.Company, company.Id, HubSpotObjectType.Contact);
        Assert.Contains(contact.Id, associatedContacts);

        var associatedCompaniesForDeal = await client.GetAssociatedIdsAsync(HubSpotObjectType.Deal, deal.Id, HubSpotObjectType.Company);
        Assert.Contains(company.Id, associatedCompaniesForDeal);

        var updatedDeal = await client.UpdateDealAsync(deal.Id, new Dictionary<string, string?> { ["dealstage"] = "closedwon" });
        Assert.Equal("closedwon", updatedDeal.Properties["dealstage"]);
        _output.WriteLine($"Updated deal {deal.Id} to closedwon");

        _output.WriteLine($"Verification complete. Created records (not deleted): company={company.Id}, contact={contact.Id}, deal={deal.Id}");
    }
}
