using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Webhooks;

public class WebhooksApiTests
{
    private const string Path = "/api/webhooks/hubspot";

    private static (HttpRequestMessage Request, string EventId) BuildSignedRequest(string body, DateTimeOffset? timestamp = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds().ToString();
        var fullUri = $"http://localhost{Path}";
        var source = "POST" + fullUri + body + ts;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookApiFactory.SigningSecret), Encoding.UTF8.GetBytes(source));
        var signature = Convert.ToBase64String(hash);

        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-HubSpot-Signature-v3", signature);
        request.Headers.Add("X-HubSpot-Request-Timestamp", ts);
        return (request, "");
    }

    private static string SingleEventBody(string eventId, string subscriptionType = "contact.propertyChange", string objectId = "999") =>
        $$"""[{"eventId":{{eventId}},"subscriptionType":"{{subscriptionType}}","objectId":{{objectId}},"occurredAt":1700000000000,"attemptNumber":0}]""";

    [Fact]
    public async Task ValidSignedRequest_IsAccepted_AndPersisted()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var eventId = Random.Shared.Next(1_000_000, 999_000_000).ToString();
        var (request, _) = BuildSignedRequest(SingleEventBody(eventId));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var eventsResponse = await client.GetAsync("/api/webhooks/events?limit=200");
        var body = await eventsResponse.Content.ReadAsStringAsync();
        Assert.Contains($"\"externalEventId\":\"{eventId}\"", body);
    }

    [Fact]
    public async Task InvalidSignature_IsRejected_WithUnauthorized()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var body = SingleEventBody(Random.Shared.Next(1_000_000, 999_000_000).ToString());

        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-HubSpot-Signature-v3", "definitely-not-valid");
        request.Headers.Add("X-HubSpot-Request-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignatureHeaders_AreRejected()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var body = SingleEventBody(Random.Shared.Next(1_000_000, 999_000_000).ToString());

        var response = await client.PostAsync(Path, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaleTimestamp_IsRejected()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var body = SingleEventBody(Random.Shared.Next(1_000_000, 999_000_000).ToString());
        var (request, _) = BuildSignedRequest(body, DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var (request, _) = BuildSignedRequest("not valid json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchOfEvents_AllPersisted()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var id1 = Random.Shared.Next(1_000_000, 999_000_000);
        var id2 = id1 + 1;
        var body = $$"""
        [
          {"eventId":{{id1}},"subscriptionType":"contact.creation","objectId":100,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":{{id2}},"subscriptionType":"company.propertyChange","objectId":200,"occurredAt":1700000000001,"attemptNumber":0}
        ]
        """;
        var (request, _) = BuildSignedRequest(body);

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json.GetProperty("accepted").GetInt32());
    }

    [Fact]
    public async Task DuplicateRequest_IsAcknowledgedWithoutReprocessing()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var eventId = Random.Shared.Next(1_000_000, 999_000_000).ToString();
        var body = SingleEventBody(eventId);

        var (first, _) = BuildSignedRequest(body);
        var firstResponse = await client.SendAsync(first);
        var (second, _) = BuildSignedRequest(body);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondJson = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondJson.GetProperty("accepted").GetInt32());
        Assert.Equal(1, secondJson.GetProperty("duplicates").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.IntegrationEvents.CountAsync(e => e.ExternalEventId == eventId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MixedSupportedAndUnsupportedEvents_UnsupportedDoesNotBlockSupported()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var id1 = Random.Shared.Next(1_000_000, 999_000_000);
        var id2 = id1 + 1;
        var body = $$"""
        [
          {"eventId":{{id1}},"subscriptionType":"contact.deletion","objectId":100,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":{{id2}},"subscriptionType":"deal.propertyChange","objectId":200,"occurredAt":1700000000000,"attemptNumber":0}
        ]
        """;
        var (request, _) = BuildSignedRequest(body);

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, json.GetProperty("accepted").GetInt32());
        Assert.Equal(1, json.GetProperty("ignored").GetInt32());
    }

    [Fact]
    public async Task GetEvent_ReturnsNotFound_ForUnknownId()
    {
        using var factory = new WebhookApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.GetAsync($"/api/webhooks/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
