using System.Security.Cryptography;
using System.Text;
using CrmIntegration.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Webhooks;

/// <summary>
/// Implements HubSpot's v3 webhook signature (https://developers.hubspot.com/docs/api/webhooks/validating-requests,
/// confirmed current as of Phase 4/5 research — see docs/WEBHOOKS.md):
///
///   base64( HMAC-SHA256( requestMethod + requestUri + requestBody + timestamp, key = signing secret ) )
///
/// compared against X-HubSpot-Signature-v3, with X-HubSpot-Request-Timestamp rejected if older
/// than WebhookOptions.MaxTimestampAge (HubSpot recommends 5 minutes). v1/v2 (hex-encoded, no
/// timestamp/replay protection) are deprecated and intentionally not implemented.
/// </summary>
public class HubSpotWebhookSignatureValidator : IHubSpotWebhookSignatureValidator
{
    private readonly HubSpotOptions _hubSpotOptions;
    private readonly WebhookOptions _webhookOptions;
    private readonly TimeProvider _timeProvider;

    public HubSpotWebhookSignatureValidator(IOptions<HubSpotOptions> hubSpotOptions, IOptions<WebhookOptions> webhookOptions, TimeProvider timeProvider)
    {
        _hubSpotOptions = hubSpotOptions.Value;
        _webhookOptions = webhookOptions.Value;
        _timeProvider = timeProvider;
    }

    public SignatureValidationResult Validate(string requestMethod, string requestUri, string requestBody, string? signatureHeader, string? timestampHeader)
    {
        if (string.IsNullOrEmpty(_hubSpotOptions.WebhookSigningSecret))
        {
            return SignatureValidationResult.SigningSecretNotConfigured;
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return SignatureValidationResult.MissingSignatureHeader;
        }

        if (string.IsNullOrEmpty(timestampHeader))
        {
            return SignatureValidationResult.MissingTimestampHeader;
        }

        if (!long.TryParse(timestampHeader, out var timestampMillis))
        {
            return SignatureValidationResult.MalformedTimestamp;
        }

        DateTimeOffset requestTime;
        try
        {
            requestTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMillis);
        }
        catch (ArgumentOutOfRangeException)
        {
            return SignatureValidationResult.MalformedTimestamp;
        }

        var now = _timeProvider.GetUtcNow();
        var age = now - requestTime;
        if (age > _webhookOptions.MaxTimestampAge || age < -_webhookOptions.MaxTimestampAge)
        {
            return SignatureValidationResult.StaleTimestamp;
        }

        var sourceString = requestMethod.ToUpperInvariant() + requestUri + requestBody + timestampHeader;
        var keyBytes = Encoding.UTF8.GetBytes(_hubSpotOptions.WebhookSigningSecret);
        var sourceBytes = Encoding.UTF8.GetBytes(sourceString);
        var expectedHash = HMACSHA256.HashData(keyBytes, sourceBytes);
        var expectedSignature = Convert.ToBase64String(expectedHash);

        var providedBytes = Encoding.UTF8.GetBytes(signatureHeader);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

        var isMatch = providedBytes.Length == expectedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);

        return isMatch ? SignatureValidationResult.Valid : SignatureValidationResult.SignatureMismatch;
    }
}
