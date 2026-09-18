using CrmIntegration.Application.Security;
using CrmIntegration.Application.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Receives HubSpot webhook deliveries. Intentionally thin: capture raw body -> validate
/// signature -> hand off to IWebhookIngestionService -> respond. No Phase 4 sync service is
/// called from here — that happens later, out of the HTTP request, via the background processor.
///
/// SECURITY: ReceiveHubSpotWebhook is intentionally [AllowAnonymous] — HubSpot itself must be able
/// to call it, and it cannot present a JWT. Its security is HubSpot's own v3 signature validation
/// (unchanged from Phase 5), not authentication — see docs/SECURITY.md "HubSpot webhook
/// authentication exception". The inspection/retry endpoints below (GET/POST /events*) are real
/// internal administration and require CanManageIntegrations (Admin/Operations) like SyncController.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[Authorize(Policy = AuthorizationPolicies.CanManageIntegrations)]
public class WebhooksController : ControllerBase
{
    private readonly IHubSpotWebhookSignatureValidator _signatureValidator;
    private readonly IWebhookIngestionService _ingestionService;
    private readonly IIntegrationEventRepository _eventRepository;
    private readonly IIntegrationEventRetryService _retryService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IHubSpotWebhookSignatureValidator signatureValidator,
        IWebhookIngestionService ingestionService,
        IIntegrationEventRepository eventRepository,
        IIntegrationEventRetryService retryService,
        ILogger<WebhooksController> logger)
    {
        _signatureValidator = signatureValidator;
        _ingestionService = ingestionService;
        _eventRepository = eventRepository;
        _retryService = retryService;
        _logger = logger;
    }

    [HttpPost("hubspot")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveHubSpotWebhook(CancellationToken cancellationToken)
    {
        var correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        var requestUri = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        var signatureHeader = Request.Headers["X-HubSpot-Signature-v3"].FirstOrDefault();
        var timestampHeader = Request.Headers["X-HubSpot-Request-Timestamp"].FirstOrDefault();

        var validationResult = _signatureValidator.Validate(Request.Method, requestUri, rawBody, signatureHeader, timestampHeader);

        if (validationResult != SignatureValidationResult.Valid)
        {
            _logger.LogWarning("WebhookSignatureRejected Reason={Reason} CorrelationId={CorrelationId}", validationResult, correlationId);
            return StatusCode(StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Title = "Webhook signature validation failed.",
                Detail = validationResult.ToString(),
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["correlationId"] = correlationId }
            });
        }

        _logger.LogInformation("WebhookReceived CorrelationId={CorrelationId} BodyLength={BodyLength}", correlationId, rawBody.Length);

        var result = await _ingestionService.IngestAsync(rawBody, correlationId, cancellationToken);

        if (result.IsMalformed)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Title = "Webhook payload could not be parsed.",
                Detail = result.MalformedReason,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["correlationId"] = correlationId }
            });
        }

        return Ok(new
        {
            correlationId,
            accepted = result.Accepted,
            duplicates = result.Duplicates,
            ignored = result.Ignored
        });
    }

    [HttpGet("events")]
    public async Task<IActionResult> ListEvents(CancellationToken cancellationToken, [FromQuery] int limit = 50)
    {
        var events = await _eventRepository.ListRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(events);
    }

    [HttpGet("events/{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken cancellationToken)
    {
        var integrationEvent = await _eventRepository.GetByIdAsync(id, cancellationToken);
        return integrationEvent is null ? NotFound() : Ok(integrationEvent);
    }

    [HttpPost("events/{id:guid}/retry")]
    public async Task<IActionResult> RetryEvent(Guid id, CancellationToken cancellationToken)
    {
        var integrationEvent = await _retryService.RetryAsync(id, cancellationToken);
        return Ok(integrationEvent);
    }
}
