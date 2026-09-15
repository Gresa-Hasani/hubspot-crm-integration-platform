namespace CrmIntegration.Application.Integrations.HubSpot;

public enum HubSpotSearchOperator
{
    Equal,
    NotEqual,
    ContainsToken
}

public record HubSpotSearchFilter(string PropertyName, HubSpotSearchOperator Operator, string Value);

/// <summary>
/// A single filter group (HubSpot ANDs filters within a group and ORs across groups). Kept to
/// one group for now since the only Phase 4 use case is "find by normalized email/domain".
/// </summary>
public record HubSpotSearchRequest(
    IReadOnlyList<HubSpotSearchFilter> Filters,
    IReadOnlyList<string> Properties,
    int Limit = 10,
    string? After = null);

public record HubSpotSearchResult(
    IReadOnlyList<HubSpotRecord> Results,
    string? NextAfter);
