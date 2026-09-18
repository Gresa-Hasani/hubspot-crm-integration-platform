using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CrmIntegration.Domain.Enums;
using CrmIntegration.IntegrationTests.Webhooks;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>
/// Explicit regression proof for the Phase 8 spec's HubSpot webhook exception: POST
/// /api/webhooks/hubspot must remain callable by HubSpot without a JWT, and its security remains
/// HubSpot's own v3 signature validation (unchanged). See docs/SECURITY.md "HubSpot webhook
/// authentication exception".
/// </summary>
public class WebhookAuthenticationExceptionTests
{
    private const string Path = "/api/webhooks/hubspot";

    private static HttpRequestMessage SignedRequest(string body, DateTimeOffset? timestamp = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds().ToString();
        var fullUri = $"http://localhost{Path}";
        var source = "POST" + fullUri + body + ts;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookApiFactory.SigningSecret), Encoding.UTF8.GetBytes(source));
        var signature = Convert.ToBase64String(hash);

        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-HubSpot-Signature-v3", signature);
        request.Headers.Add("X-HubSpot-Request-Timestamp", ts);
        return request;
    }

    private static string EventBody() =>
        $$"""[{"eventId":{{Random.Shared.Next(1_000_000, 999_000_000)}},"subscriptionType":"contact.propertyChange","objectId":999,"occurredAt":1700000000000,"attemptNumber":0}]""";

    [Fact]
    public async Task ValidSignature_NoJwt_NoAuthorizationHeaderAtAll_IsAccepted()
    {
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient(); // deliberately anonymous — no Authorization header
        var request = SignedRequest(EventBody());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidSignature_WithAnUnrelatedValidJwtAlsoAttached_IsStillAccepted()
    {
        // Proves the endpoint's [AllowAnonymous] doesn't merely tolerate a missing token — a
        // present-but-irrelevant one doesn't confuse it either. Signature validation is the only
        // thing that matters here.
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAccessTokenAsync(UserRole.ReadOnly));
        var request = SignedRequest(EventBody());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignature_IsRejected_RegardlessOfJwtPresence()
    {
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAccessTokenAsync(UserRole.Admin));
        var body = EventBody();

        var response = await client.PostAsync(Path, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignature_IsRejected_EvenWithAValidAdminJwtAttached()
    {
        // A valid internal JWT must never substitute for a valid HubSpot signature.
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAccessTokenAsync(UserRole.Admin));
        var body = EventBody();
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-HubSpot-Signature-v3", "not-a-valid-signature");
        request.Headers.Add("X-HubSpot-Request-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InspectionEndpoints_UnlikeIngestion_DoRequireAuthentication()
    {
        using var factory = new WebhookApiFactory();
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/webhooks/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
