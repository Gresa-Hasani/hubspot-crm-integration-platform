using System.Security.Cryptography;
using System.Text;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class HubSpotWebhookSignatureValidatorTests
{
    private const string Secret = "test-signing-secret";
    private const string Method = "POST";
    private const string Uri = "https://example.com/api/webhooks/hubspot";
    private const string Body = "[{\"eventId\":1}]";

    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private static HubSpotWebhookSignatureValidator CreateValidator(string? secret = Secret, TimeSpan? maxAge = null) =>
        new(
            Options.Create(new HubSpotOptions { WebhookSigningSecret = secret }),
            Options.Create(new WebhookOptions { MaxTimestampAge = maxAge ?? TimeSpan.FromMinutes(5) }),
            Clock);

    private static string ComputeValidSignature(string method, string uri, string body, string timestamp)
    {
        var source = method.ToUpperInvariant() + uri + body + timestamp;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(source));
        return Convert.ToBase64String(hash);
    }

    private static string CurrentTimestamp() => Clock.GetUtcNow().ToUnixTimeMilliseconds().ToString();

    [Fact]
    public void Validate_ReturnsValid_ForCorrectSignatureAndFreshTimestamp()
    {
        var validator = CreateValidator();
        var timestamp = CurrentTimestamp();
        var signature = ComputeValidSignature(Method, Uri, Body, timestamp);

        var result = validator.Validate(Method, Uri, Body, signature, timestamp);

        Assert.Equal(SignatureValidationResult.Valid, result);
    }

    [Fact]
    public void Validate_ReturnsSignatureMismatch_ForIncorrectSignature()
    {
        var validator = CreateValidator();
        var timestamp = CurrentTimestamp();

        var result = validator.Validate(Method, Uri, Body, "not-the-right-signature", timestamp);

        Assert.Equal(SignatureValidationResult.SignatureMismatch, result);
    }

    [Fact]
    public void Validate_ReturnsMissingSignatureHeader_WhenSignatureIsNull()
    {
        var validator = CreateValidator();

        var result = validator.Validate(Method, Uri, Body, null, CurrentTimestamp());

        Assert.Equal(SignatureValidationResult.MissingSignatureHeader, result);
    }

    [Fact]
    public void Validate_ReturnsMissingTimestampHeader_WhenTimestampIsNull()
    {
        var validator = CreateValidator();
        var signature = ComputeValidSignature(Method, Uri, Body, "1234");

        var result = validator.Validate(Method, Uri, Body, signature, null);

        Assert.Equal(SignatureValidationResult.MissingTimestampHeader, result);
    }

    [Fact]
    public void Validate_ReturnsMalformedTimestamp_WhenTimestampIsNotNumeric()
    {
        var validator = CreateValidator();
        var signature = ComputeValidSignature(Method, Uri, Body, "not-a-number");

        var result = validator.Validate(Method, Uri, Body, signature, "not-a-number");

        Assert.Equal(SignatureValidationResult.MalformedTimestamp, result);
    }

    [Fact]
    public void Validate_ReturnsStaleTimestamp_WhenOlderThanMaxAge()
    {
        var validator = CreateValidator(maxAge: TimeSpan.FromMinutes(5));
        var staleTimestamp = Clock.GetUtcNow().AddMinutes(-10).ToUnixTimeMilliseconds().ToString();
        var signature = ComputeValidSignature(Method, Uri, Body, staleTimestamp);

        var result = validator.Validate(Method, Uri, Body, signature, staleTimestamp);

        Assert.Equal(SignatureValidationResult.StaleTimestamp, result);
    }

    [Fact]
    public void Validate_ReturnsStaleTimestamp_WhenTimestampIsTooFarInTheFuture()
    {
        var validator = CreateValidator(maxAge: TimeSpan.FromMinutes(5));
        var futureTimestamp = Clock.GetUtcNow().AddMinutes(10).ToUnixTimeMilliseconds().ToString();
        var signature = ComputeValidSignature(Method, Uri, Body, futureTimestamp);

        var result = validator.Validate(Method, Uri, Body, signature, futureTimestamp);

        Assert.Equal(SignatureValidationResult.StaleTimestamp, result);
    }

    [Fact]
    public void Validate_ReturnsSignatureMismatch_WhenBodyIsMutatedAfterSigning()
    {
        var validator = CreateValidator();
        var timestamp = CurrentTimestamp();
        var signature = ComputeValidSignature(Method, Uri, Body, timestamp);

        var result = validator.Validate(Method, Uri, "[{\"eventId\":2}]", signature, timestamp);

        Assert.Equal(SignatureValidationResult.SignatureMismatch, result);
    }

    [Fact]
    public void Validate_ReturnsSignatureMismatch_WhenUriDiffersFromWhatWasSigned()
    {
        var validator = CreateValidator();
        var timestamp = CurrentTimestamp();
        var signature = ComputeValidSignature(Method, Uri, Body, timestamp);

        var result = validator.Validate(Method, "https://example.com/api/webhooks/hubspot?tampered=1", Body, signature, timestamp);

        Assert.Equal(SignatureValidationResult.SignatureMismatch, result);
    }

    [Fact]
    public void Validate_ReturnsSigningSecretNotConfigured_WhenSecretIsMissing()
    {
        var validator = CreateValidator(secret: null);

        var result = validator.Validate(Method, Uri, Body, "anything", CurrentTimestamp());

        Assert.Equal(SignatureValidationResult.SigningSecretNotConfigured, result);
    }
}
