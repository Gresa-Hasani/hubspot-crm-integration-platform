namespace CrmIntegration.Application.Webhooks;

public enum SignatureValidationResult
{
    Valid,
    MissingSignatureHeader,
    MissingTimestampHeader,
    MalformedTimestamp,
    StaleTimestamp,
    SignatureMismatch,
    SigningSecretNotConfigured
}

/// <summary>
/// Validates HubSpot's v3 webhook request signature (X-HubSpot-Signature-v3 +
/// X-HubSpot-Request-Timestamp). See docs/WEBHOOKS.md for the exact algorithm and why v3 (not the
/// deprecated v1/v2) was chosen. Never logs or returns the signing secret or the computed/expected
/// signature — only a classification of why validation failed.
/// </summary>
public interface IHubSpotWebhookSignatureValidator
{
    SignatureValidationResult Validate(string requestMethod, string requestUri, string requestBody, string? signatureHeader, string? timestampHeader);
}
