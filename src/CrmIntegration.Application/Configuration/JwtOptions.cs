namespace CrmIntegration.Application.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>How long a refresh token stays valid before it must be re-issued via login. See docs/SECURITY.md "Refresh-token design".</summary>
    public int RefreshTokenExpirationDays { get; set; } = 14;
}
