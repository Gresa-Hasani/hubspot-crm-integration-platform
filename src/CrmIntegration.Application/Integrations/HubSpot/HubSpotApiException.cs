using System.Net;

namespace CrmIntegration.Application.Integrations.HubSpot;

/// <summary>
/// Base type for every failure surfaced by <see cref="IHubSpotClient"/>. Callers (the future
/// sync/retry engine) should catch this rather than raw HttpRequestException/HttpClient
/// exceptions, and use the specific subclass to decide whether a failure is retryable.
/// The message is safe to log: it never contains the access token or full request/response body.
/// </summary>
public abstract class HubSpotApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? HubSpotCorrelationId { get; }

    protected HubSpotApiException(string message, HttpStatusCode? statusCode, string? hubSpotCorrelationId, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        HubSpotCorrelationId = hubSpotCorrelationId;
    }
}

/// <summary>400 — the request payload was rejected. Not retryable without changing the request.</summary>
public class HubSpotBadRequestException : HubSpotApiException
{
    public HubSpotBadRequestException(string message, string? correlationId)
        : base(message, HttpStatusCode.BadRequest, correlationId)
    {
    }
}

/// <summary>401 — the access token is missing/invalid/expired. Not retryable without re-authenticating.</summary>
public class HubSpotUnauthorizedException : HubSpotApiException
{
    public HubSpotUnauthorizedException(string message, string? correlationId)
        : base(message, HttpStatusCode.Unauthorized, correlationId)
    {
    }
}

/// <summary>403 — authenticated but missing the required scope/permission. Not retryable.</summary>
public class HubSpotForbiddenException : HubSpotApiException
{
    public HubSpotForbiddenException(string message, string? correlationId)
        : base(message, HttpStatusCode.Forbidden, correlationId)
    {
    }
}

/// <summary>404 — the object id doesn't exist in the HubSpot portal.</summary>
public class HubSpotNotFoundException : HubSpotApiException
{
    public HubSpotNotFoundException(string message, string? correlationId)
        : base(message, HttpStatusCode.NotFound, correlationId)
    {
    }
}

/// <summary>409 — e.g. a unique-property conflict (duplicate email) on create.</summary>
public class HubSpotConflictException : HubSpotApiException
{
    public HubSpotConflictException(string message, string? correlationId)
        : base(message, HttpStatusCode.Conflict, correlationId)
    {
    }
}

/// <summary>
/// 429 — rate limited. Carries what a retry policy needs to decide when to try again.
/// Retry-After is only present when HubSpot sends it; callers must treat a missing value as
/// "use your own backoff", not "safe to retry immediately".
/// </summary>
public class HubSpotRateLimitedException : HubSpotApiException
{
    public TimeSpan? RetryAfter { get; }

    public HubSpotRateLimitedException(string message, string? correlationId, TimeSpan? retryAfter)
        : base(message, (HttpStatusCode)429, correlationId)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>5xx — HubSpot-side failure. Transient; safe for a retry policy to retry with backoff.</summary>
public class HubSpotServerException : HubSpotApiException
{
    public HubSpotServerException(string message, HttpStatusCode statusCode, string? correlationId)
        : base(message, statusCode, correlationId)
    {
    }
}

/// <summary>
/// Network failure or request timeout before any HTTP response was received. Transient; safe
/// for a retry policy to retry with backoff.
/// </summary>
public class HubSpotTransientException : HubSpotApiException
{
    public HubSpotTransientException(string message, Exception inner)
        : base(message, statusCode: null, hubSpotCorrelationId: null, inner)
    {
    }
}
