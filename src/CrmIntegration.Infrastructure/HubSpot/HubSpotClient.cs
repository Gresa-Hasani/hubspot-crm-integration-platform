using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Infrastructure.HubSpot.Dtos;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Infrastructure.HubSpot;

/// <summary>
/// Thin, explicit wrapper over HubSpot's CRM v3 objects API and v4 associations API.
/// Every non-success response is translated into a specific <see cref="HubSpotApiException"/>
/// subclass so callers never have to inspect raw status codes. The HttpClient is a typed client
/// (see Infrastructure.DependencyInjection) with BaseAddress, timeout and the Bearer token
/// already configured — this class never touches the access token directly.
/// </summary>
public class HubSpotClient : IHubSpotClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HubSpotClient> _logger;

    public HubSpotClient(HttpClient httpClient, ILogger<HubSpotClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<HubSpotRecord?> GetContactAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Contact, hubSpotId, properties, cancellationToken);

    public Task<HubSpotRecord> CreateContactAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Contact, properties, cancellationToken);

    public Task<HubSpotRecord> UpdateContactAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Contact, hubSpotId, properties, cancellationToken);

    public Task<HubSpotSearchResult> SearchContactsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Contact, request, cancellationToken);

    public Task<HubSpotRecord?> GetCompanyAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Company, hubSpotId, properties, cancellationToken);

    public Task<HubSpotRecord> CreateCompanyAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Company, properties, cancellationToken);

    public Task<HubSpotRecord> UpdateCompanyAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Company, hubSpotId, properties, cancellationToken);

    public Task<HubSpotSearchResult> SearchCompaniesAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Company, request, cancellationToken);

    public Task<HubSpotRecord?> GetDealAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Deal, hubSpotId, properties, cancellationToken);

    public Task<HubSpotRecord> CreateDealAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Deal, properties, cancellationToken);

    public Task<HubSpotRecord> UpdateDealAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Deal, hubSpotId, properties, cancellationToken);

    public Task<HubSpotSearchResult> SearchDealsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Deal, request, cancellationToken);

    public async Task CreateAssociationAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, string toId, CancellationToken cancellationToken = default)
    {
        var path = $"/crm/v4/objects/{fromType.ToApiPath()}/{fromId}/associations/default/{toType.ToApiPath()}/{toId}";
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        using var response = await SendAsync(request, cancellationToken);
        _logger.LogInformation(
            "Created HubSpot association {FromType}:{FromId} -> {ToType}:{ToId}",
            fromType, fromId, toType, toId);
    }

    public async Task<IReadOnlyList<string>> GetAssociatedIdsAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, CancellationToken cancellationToken = default)
    {
        var path = $"/crm/v4/objects/{fromType.ToApiPath()}/{fromId}/associations/{toType.ToApiPath()}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken);

        var body = await ReadJsonOrThrowAsync<HubSpotAssociationsResponseDto>(response, cancellationToken)
            ?? new HubSpotAssociationsResponseDto();

        return body.Results.Select(r => r.ToObjectId).ToList();
    }

    private async Task<HubSpotRecord?> GetAsync(HubSpotObjectType type, string id, IReadOnlyList<string>? properties, CancellationToken cancellationToken)
    {
        var path = $"/crm/v3/objects/{type.ToApiPath()}/{id}";
        if (properties is { Count: > 0 })
        {
            path += $"?properties={string.Join(',', properties)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        try
        {
            using var response = await SendAsync(request, cancellationToken);
            var dto = await ReadJsonOrThrowAsync<HubSpotObjectDto>(response, cancellationToken);
            return dto is null ? null : ToRecord(dto);
        }
        catch (HubSpotNotFoundException)
        {
            return null;
        }
    }

    private async Task<HubSpotRecord> CreateAsync(HubSpotObjectType type, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken)
    {
        var path = $"/crm/v3/objects/{type.ToApiPath()}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new HubSpotWriteRequestDto { Properties = new Dictionary<string, string?>(properties) })
        };

        using var response = await SendAsync(request, cancellationToken);
        var dto = await ReadJsonOrThrowAsync<HubSpotObjectDto>(response, cancellationToken)
            ?? throw new HubSpotServerException("HubSpot returned an empty body for a create request.", HttpStatusCode.InternalServerError, null);

        _logger.LogInformation("Created HubSpot {ObjectType} {HubSpotId}", type, dto.Id);
        return ToRecord(dto);
    }

    private async Task<HubSpotRecord> UpdateAsync(HubSpotObjectType type, string id, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken)
    {
        var path = $"/crm/v3/objects/{type.ToApiPath()}/{id}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(new HubSpotWriteRequestDto { Properties = new Dictionary<string, string?>(properties) })
        };

        using var response = await SendAsync(request, cancellationToken);
        var dto = await ReadJsonOrThrowAsync<HubSpotObjectDto>(response, cancellationToken)
            ?? throw new HubSpotServerException("HubSpot returned an empty body for an update request.", HttpStatusCode.InternalServerError, null);

        _logger.LogInformation("Updated HubSpot {ObjectType} {HubSpotId}", type, dto.Id);
        return ToRecord(dto);
    }

    private async Task<HubSpotSearchResult> SearchAsync(HubSpotObjectType type, HubSpotSearchRequest request, CancellationToken cancellationToken)
    {
        var path = $"/crm/v3/objects/{type.ToApiPath()}/search";
        var body = new HubSpotSearchRequestDto
        {
            Properties = request.Properties.ToList(),
            Limit = request.Limit,
            After = request.After,
            FilterGroups =
            [
                new HubSpotFilterGroupDto
                {
                    Filters = request.Filters.Select(f => new HubSpotFilterDto
                    {
                        PropertyName = f.PropertyName,
                        Operator = ToHubSpotOperator(f.Operator),
                        Value = f.Value
                    }).ToList()
                }
            ]
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        using var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonOrThrowAsync<HubSpotSearchResponseDto>(response, cancellationToken)
            ?? new HubSpotSearchResponseDto();

        return new HubSpotSearchResult(
            dto.Results.Select(ToRecord).ToList(),
            dto.Paging?.Next?.After);
    }

    private static string ToHubSpotOperator(HubSpotSearchOperator op) => op switch
    {
        HubSpotSearchOperator.Equal => "EQ",
        HubSpotSearchOperator.NotEqual => "NEQ",
        HubSpotSearchOperator.ContainsToken => "CONTAINS_TOKEN",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    private static HubSpotRecord ToRecord(HubSpotObjectDto dto) =>
        new(dto.Id, dto.Properties, dto.CreatedAt, dto.UpdatedAt);

    /// <summary>
    /// Sends the request and maps any non-success response (or transport failure) to the
    /// appropriate <see cref="HubSpotApiException"/>. Never logs the Authorization header.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed, not caller cancellation — this is a transient failure.
            throw new HubSpotTransientException($"HubSpot request to {request.RequestUri} timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HubSpotTransientException($"HubSpot request to {request.RequestUri} failed: {ex.Message}", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var (message, correlationId) = await TryReadErrorAsync(response, cancellationToken);
        response.Dispose();

        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new HubSpotBadRequestException(message, correlationId),
            HttpStatusCode.Unauthorized => new HubSpotUnauthorizedException(message, correlationId),
            HttpStatusCode.Forbidden => new HubSpotForbiddenException(message, correlationId),
            HttpStatusCode.NotFound => new HubSpotNotFoundException(message, correlationId),
            HttpStatusCode.Conflict => new HubSpotConflictException(message, correlationId),
            (HttpStatusCode)429 => new HubSpotRateLimitedException(message, correlationId, response.Headers.RetryAfter?.Delta),
            _ when (int)response.StatusCode >= 500 => new HubSpotServerException(message, response.StatusCode, correlationId),
            _ => new HubSpotServerException(message, response.StatusCode, correlationId)
        };
    }

    /// <summary>Wraps a malformed/unexpected success-response body as a HubSpotServerException instead of leaking a raw JsonException.</summary>
    private static async Task<T?> ReadJsonOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new HubSpotServerException($"HubSpot returned a response body that could not be parsed as {typeof(T).Name}: {ex.Message}", response.StatusCode, null);
        }
    }

    private static async Task<(string Message, string? CorrelationId)> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<HubSpotErrorResponseDto>(cancellationToken: cancellationToken);
            var message = error?.Message ?? $"HubSpot returned {(int)response.StatusCode} {response.ReasonPhrase}.";
            return (message, error?.CorrelationId);
        }
        catch
        {
            return ($"HubSpot returned {(int)response.StatusCode} {response.ReasonPhrase} with a non-JSON body.", null);
        }
    }
}
