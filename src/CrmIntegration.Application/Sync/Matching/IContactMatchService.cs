namespace CrmIntegration.Application.Sync.Matching;

/// <summary>
/// Deterministic-only Contact duplicate detection, per docs/FIELD_MAPPING.md. Normalized email is
/// the sole identifier — no fuzzy/AI matching in this phase.
/// </summary>
public interface IContactMatchService
{
    Task<InternalMatchResult> FindInternalMatchAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<HubSpotMatchResult> FindHubSpotMatchAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
