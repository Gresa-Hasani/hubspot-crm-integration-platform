namespace CrmIntegration.Application.Sync.Matching;

/// <summary>
/// Deterministic-only Company duplicate detection. Normalized domain is the strongest identifier
/// and can produce an ExactMatch. Normalized name alone is never confident enough to be an
/// ExactMatch — a name-only hit is always surfaced as Ambiguous (see docs/FIELD_MAPPING.md).
/// </summary>
public interface ICompanyMatchService
{
    Task<InternalMatchResult> FindInternalMatchAsync(string? normalizedDomain, string normalizedName, CancellationToken cancellationToken = default);
    Task<HubSpotMatchResult> FindHubSpotMatchAsync(string? normalizedDomain, string normalizedName, CancellationToken cancellationToken = default);
}
