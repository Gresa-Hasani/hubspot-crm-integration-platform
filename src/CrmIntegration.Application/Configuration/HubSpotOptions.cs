namespace CrmIntegration.Application.Configuration;

public class HubSpotOptions
{
    public const string SectionName = "HubSpot";

    public string BaseUrl { get; set; } = "https://api.hubapi.com";
    public string AccessToken { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? WebhookSigningSecret { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;
}
