using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Integrations.HubSpot;

namespace CrmIntegration.Application.Sync.Matching;

public class CompanyMatchService : ICompanyMatchService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IHubSpotClient _hubSpotClient;

    public CompanyMatchService(ICompanyRepository companyRepository, IHubSpotClient hubSpotClient)
    {
        _companyRepository = companyRepository;
        _hubSpotClient = hubSpotClient;
    }

    public async Task<InternalMatchResult> FindInternalMatchAsync(string? normalizedDomain, string normalizedName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(normalizedDomain))
        {
            var byDomain = await _companyRepository.FindByNormalizedDomainAsync(normalizedDomain, cancellationToken);
            switch (byDomain.Count)
            {
                case 1: return new InternalMatchResult(MatchType.ExactMatch, byDomain[0].Id);
                case > 1: return new InternalMatchResult(MatchType.Ambiguous, null);
            }
        }

        var byName = await _companyRepository.FindByNormalizedNameAsync(normalizedName, cancellationToken);
        return byName.Count switch
        {
            0 => new InternalMatchResult(MatchType.NoMatch, null),
            // Name alone is never confident enough to be Exact, even with exactly one hit.
            _ => new InternalMatchResult(MatchType.Ambiguous, null)
        };
    }

    public async Task<HubSpotMatchResult> FindHubSpotMatchAsync(string? normalizedDomain, string normalizedName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(normalizedDomain))
        {
            var byDomain = await _hubSpotClient.SearchCompaniesAsync(
                new HubSpotSearchRequest(
                    Filters: [new HubSpotSearchFilter("domain", HubSpotSearchOperator.Equal, normalizedDomain)],
                    Properties: ["domain"],
                    Limit: 2),
                cancellationToken);

            switch (byDomain.Results.Count)
            {
                case 1: return new HubSpotMatchResult(MatchType.ExactMatch, byDomain.Results[0].Id);
                case > 1: return new HubSpotMatchResult(MatchType.Ambiguous, null);
            }
        }

        var byName = await _hubSpotClient.SearchCompaniesAsync(
            new HubSpotSearchRequest(
                Filters: [new HubSpotSearchFilter("name", HubSpotSearchOperator.Equal, normalizedName)],
                Properties: ["name"],
                Limit: 2),
            cancellationToken);

        return byName.Results.Count switch
        {
            0 => new HubSpotMatchResult(MatchType.NoMatch, null),
            _ => new HubSpotMatchResult(MatchType.Ambiguous, null)
        };
    }
}
