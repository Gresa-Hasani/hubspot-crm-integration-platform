namespace CrmIntegration.Application.Configuration;

/// <summary>
/// Explicitly configured allowed browser origins (e.g. the Phase 10 dashboard's dev/prod URLs).
/// Empty by default — CORS is only enabled when at least one origin is configured, never
/// AllowAnyOrigin combined with credentials. See docs/SECURITY.md "CORS policy".
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
