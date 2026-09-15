using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Domain.Entities;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;
using MatchType = CrmIntegration.Application.Sync.MatchType;

namespace CrmIntegration.UnitTests.Sync.Matching;

public class CompanyMatchServiceTests
{
    [Fact]
    public async Task FindInternalMatchAsync_ReturnsExactMatch_ForSingleDomainHit()
    {
        var repo = new FakeCompanyRepository();
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", Domain = "acme.example" };
        repo.Companies.Add(company);
        var service = new CompanyMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("acme.example", "acme");

        Assert.Equal(MatchType.ExactMatch, result.MatchType);
        Assert.Equal(company.Id, result.InternalId);
    }

    [Fact]
    public async Task FindInternalMatchAsync_ReturnsAmbiguous_ForMultipleDomainHits()
    {
        var repo = new FakeCompanyRepository();
        repo.Companies.Add(new Company { Id = Guid.NewGuid(), Name = "A", Domain = "shared.example" });
        repo.Companies.Add(new Company { Id = Guid.NewGuid(), Name = "B", Domain = "shared.example" });
        var service = new CompanyMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("shared.example", "whatever");

        Assert.Equal(MatchType.Ambiguous, result.MatchType);
    }

    [Fact]
    public async Task FindInternalMatchAsync_NameOnlyMatch_IsAlwaysAmbiguous_NeverExact()
    {
        var repo = new FakeCompanyRepository();
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme Technologies", Domain = null };
        repo.Companies.Add(company);
        var service = new CompanyMatchService(repo, new FakeHubSpotClient());

        // No domain provided (null) -> falls back to name-only matching, which must never be Exact
        // even though there is exactly one hit.
        var result = await service.FindInternalMatchAsync(null, "Acme Technologies");

        Assert.Equal(MatchType.Ambiguous, result.MatchType);
    }

    [Fact]
    public async Task FindInternalMatchAsync_ReturnsNoMatch_WhenNeitherDomainNorNameHit()
    {
        var repo = new FakeCompanyRepository();
        var service = new CompanyMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("nobody.example", "Nobody Inc");

        Assert.Equal(MatchType.NoMatch, result.MatchType);
    }

    [Fact]
    public async Task FindHubSpotMatchAsync_ReturnsExactMatch_ForSingleDomainHit()
    {
        var hubSpot = new FakeHubSpotClient();
        var created = await hubSpot.CreateCompanyAsync(new Dictionary<string, string?> { ["domain"] = "acme.example", ["name"] = "Acme" });
        var service = new CompanyMatchService(new FakeCompanyRepository(), hubSpot);

        var result = await service.FindHubSpotMatchAsync("acme.example", "acme");

        Assert.Equal(MatchType.ExactMatch, result.MatchType);
        Assert.Equal(created.Id, result.HubSpotId);
    }
}
