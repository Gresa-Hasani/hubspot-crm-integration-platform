using System.Net;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Infrastructure.HubSpot;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.HubSpot;

public class HubSpotClientTests
{
    private const string BaseUrl = "https://api.hubapi.test";
    private const string Token = "test-token-value";

    private static (HubSpotClient Client, FakeHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        return (new HubSpotClient(httpClient, NullLogger<HubSpotClient>.Instance), handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetContactAsync_BuildsCorrectEndpointAndSendsAuthHeader()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK,
            """{"id":"1","properties":{"email":"alice@acme.example"},"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-02T00:00:00Z"}"""));

        var result = await client.GetContactAsync("1", new[] { "email" });

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal($"{BaseUrl}/crm/v3/objects/contacts/1?properties=email", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(Token, handler.LastRequest.Headers.Authorization.Parameter);

        Assert.NotNull(result);
        Assert.Equal("1", result!.Id);
        Assert.Equal("alice@acme.example", result.Properties["email"]);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), result.CreatedAt);
    }

    [Fact]
    public async Task GetContactAsync_ReturnsNull_On404()
    {
        var (client, _) = CreateClient(_ => JsonResponse(HttpStatusCode.NotFound,
            """{"message":"contact not found","correlationId":"abc-123"}"""));

        var result = await client.GetContactAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateContactAsync_SendsPropertiesAsRequestBody_AndParsesResponse()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.Created,
            """{"id":"42","properties":{"email":"bob@acme.example","firstname":"Bob"}}"""));

        var result = await client.CreateContactAsync(new Dictionary<string, string?>
        {
            ["email"] = "bob@acme.example",
            ["firstname"] = "Bob"
        });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal($"{BaseUrl}/crm/v3/objects/contacts", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("\"email\":\"bob@acme.example\"", handler.LastRequestBody);
        Assert.Equal("42", result.Id);
    }

    [Fact]
    public async Task UpdateCompanyAsync_UsesPatchAndCorrectPath()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK,
            """{"id":"99","properties":{"name":"Acme"}}"""));

        var result = await client.UpdateCompanyAsync("99", new Dictionary<string, string?> { ["name"] = "Acme" });

        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal($"{BaseUrl}/crm/v3/objects/companies/99", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("99", result.Id);
    }

    [Fact]
    public async Task SearchDealsAsync_SendsFilterGroupsAndParsesPaging()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK,
            """{"total":1,"results":[{"id":"7","properties":{"dealname":"Big Deal"}}],"paging":{"next":{"after":"cursor-1"}}}"""));

        var result = await client.SearchDealsAsync(new HubSpotSearchRequest(
            Filters: [new HubSpotSearchFilter("dealname", HubSpotSearchOperator.Equal, "Big Deal")],
            Properties: ["dealname"]));

        Assert.Equal($"{BaseUrl}/crm/v3/objects/deals/search", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("\"propertyName\":\"dealname\"", handler.LastRequestBody);
        Assert.Contains("\"operator\":\"EQ\"", handler.LastRequestBody);
        Assert.Single(result.Results);
        Assert.Equal("cursor-1", result.NextAfter);
    }

    [Fact]
    public async Task CreateAssociationAsync_PutsToV4DefaultAssociationEndpoint()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Created));

        await client.CreateAssociationAsync(HubSpotObjectType.Contact, "1", HubSpotObjectType.Company, "2");

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal($"{BaseUrl}/crm/v4/objects/contacts/1/associations/default/companies/2", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAssociatedIdsAsync_ReturnsToObjectIds()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK,
            """{"results":[{"toObjectId":"100"},{"toObjectId":"200"}]}"""));

        var ids = await client.GetAssociatedIdsAsync(HubSpotObjectType.Deal, "5", HubSpotObjectType.Contact);

        Assert.Equal($"{BaseUrl}/crm/v4/objects/deals/5/associations/contacts", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(["100", "200"], ids);
    }

    [Fact]
    public async Task GetAssociatedIdsAsync_AcceptsNumericToObjectId()
    {
        // Discovered against a real HubSpot portal: v4 associations returns toObjectId as a JSON
        // number, unlike v3 objects which returns id as a string. Both must parse correctly.
        var (client, _) = CreateClient(_ => JsonResponse(HttpStatusCode.OK,
            """{"results":[{"toObjectId":869154931910}]}"""));

        var ids = await client.GetAssociatedIdsAsync(HubSpotObjectType.Company, "1", HubSpotObjectType.Contact);

        Assert.Equal(["869154931910"], ids);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(HubSpotBadRequestException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(HubSpotUnauthorizedException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(HubSpotForbiddenException))]
    [InlineData(HttpStatusCode.Conflict, typeof(HubSpotConflictException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(HubSpotServerException))]
    [InlineData(HttpStatusCode.ServiceUnavailable, typeof(HubSpotServerException))]
    public async Task CreateContactAsync_MapsStatusCodeToSpecificException(HttpStatusCode status, Type expectedExceptionType)
    {
        var (client, _) = CreateClient(_ => JsonResponse(status, """{"message":"failure","correlationId":"corr-1"}"""));

        var exception = await Assert.ThrowsAnyAsync<HubSpotApiException>(() =>
            client.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "x@example.com" }));

        Assert.IsType(expectedExceptionType, exception);
        Assert.Equal("failure", exception.Message);
        Assert.Equal("corr-1", exception.HubSpotCorrelationId);
    }

    [Fact]
    public async Task CreateContactAsync_On429_ExposesRetryAfter()
    {
        var (client, _) = CreateClient(_ =>
        {
            var response = JsonResponse((HttpStatusCode)429, """{"message":"rate limited"}""");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        });

        var exception = await Assert.ThrowsAsync<HubSpotRateLimitedException>(() =>
            client.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "x@example.com" }));

        Assert.Equal(TimeSpan.FromSeconds(10), exception.RetryAfter);
    }

    [Fact]
    public async Task CreateContactAsync_On429_WithoutRetryAfterHeader_LeavesRetryAfterNull()
    {
        var (client, _) = CreateClient(_ => JsonResponse((HttpStatusCode)429, """{"message":"rate limited"}"""));

        var exception = await Assert.ThrowsAsync<HubSpotRateLimitedException>(() =>
            client.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "x@example.com" }));

        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task CreateContactAsync_OnMalformedJsonBody_ThrowsHubSpotServerException()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json", System.Text.Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<HubSpotServerException>(() =>
            client.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "x@example.com" }));
    }

    [Fact]
    public async Task CreateContactAsync_OnNonJsonErrorBody_StillThrowsMappedException()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("<html>Bad Gateway</html>", System.Text.Encoding.UTF8, "text/html")
        });

        var exception = await Assert.ThrowsAsync<HubSpotBadRequestException>(() =>
            client.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "x@example.com" }));

        Assert.Contains("400", exception.Message);
    }

    [Fact]
    public async Task GetContactAsync_OnNetworkFailure_ThrowsHubSpotTransientException()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<HubSpotTransientException>(() => client.GetContactAsync("1"));
    }

    [Fact]
    public async Task GetContactAsync_RespectsCallerCancellation()
    {
        var (client, _) = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """{"id":"1","properties":{}}"""));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetContactAsync("1", cancellationToken: cts.Token));
    }
}
