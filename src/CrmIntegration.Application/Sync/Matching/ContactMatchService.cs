using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Integrations.HubSpot;

namespace CrmIntegration.Application.Sync.Matching;

public class ContactMatchService : IContactMatchService
{
    private readonly IContactRepository _contactRepository;
    private readonly IHubSpotClient _hubSpotClient;

    public ContactMatchService(IContactRepository contactRepository, IHubSpotClient hubSpotClient)
    {
        _contactRepository = contactRepository;
        _hubSpotClient = hubSpotClient;
    }

    public async Task<InternalMatchResult> FindInternalMatchAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var matches = await _contactRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        return matches.Count switch
        {
            0 => new InternalMatchResult(MatchType.NoMatch, null),
            1 => new InternalMatchResult(MatchType.ExactMatch, matches[0].Id),
            _ => new InternalMatchResult(MatchType.Ambiguous, null)
        };
    }

    public async Task<HubSpotMatchResult> FindHubSpotMatchAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var result = await _hubSpotClient.SearchContactsAsync(
            new HubSpotSearchRequest(
                Filters: [new HubSpotSearchFilter("email", HubSpotSearchOperator.Equal, normalizedEmail)],
                Properties: ["email"],
                Limit: 2),
            cancellationToken);

        return result.Results.Count switch
        {
            0 => new HubSpotMatchResult(MatchType.NoMatch, null),
            1 => new HubSpotMatchResult(MatchType.ExactMatch, result.Results[0].Id),
            _ => new HubSpotMatchResult(MatchType.Ambiguous, null)
        };
    }
}
