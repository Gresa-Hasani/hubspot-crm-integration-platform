using CrmIntegration.Application.Security;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Manual synchronization triggers for development/testing (Phase 4). Full webhook-driven
/// synchronization is Phase 5. Integration administration — Admin/Operations only (RBAC matrix,
/// docs/SECURITY.md).
/// </summary>
[ApiController]
[Route("api/sync")]
[Authorize(Policy = AuthorizationPolicies.CanManageIntegrations)]
public class SyncController : ControllerBase
{
    private readonly IContactSyncService _contactSyncService;
    private readonly ICompanySyncService _companySyncService;
    private readonly IDealSyncService _dealSyncService;
    private readonly ISyncJobRetryService _syncJobRetryService;
    private readonly ISyncJobRepository _syncJobRepository;

    public SyncController(
        IContactSyncService contactSyncService,
        ICompanySyncService companySyncService,
        IDealSyncService dealSyncService,
        ISyncJobRetryService syncJobRetryService,
        ISyncJobRepository syncJobRepository)
    {
        _contactSyncService = contactSyncService;
        _companySyncService = companySyncService;
        _dealSyncService = dealSyncService;
        _syncJobRetryService = syncJobRetryService;
        _syncJobRepository = syncJobRepository;
    }

    [HttpPost("contacts/{id:guid}/to-hubspot")]
    public async Task<ActionResult<SyncJobResponse>> SyncContactToHubSpot(Guid id, CancellationToken cancellationToken)
    {
        var job = await _contactSyncService.SyncToHubSpotAsync(id, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpPost("companies/{id:guid}/to-hubspot")]
    public async Task<ActionResult<SyncJobResponse>> SyncCompanyToHubSpot(Guid id, CancellationToken cancellationToken)
    {
        var job = await _companySyncService.SyncToHubSpotAsync(id, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpPost("deals/{id:guid}/to-hubspot")]
    public async Task<ActionResult<SyncJobResponse>> SyncDealToHubSpot(Guid id, CancellationToken cancellationToken)
    {
        var job = await _dealSyncService.SyncToHubSpotAsync(id, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpPost("hubspot/contacts/{hubSpotId}")]
    public async Task<ActionResult<SyncJobResponse>> SyncContactFromHubSpot(string hubSpotId, CancellationToken cancellationToken)
    {
        var job = await _contactSyncService.SyncFromHubSpotAsync(hubSpotId, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpPost("hubspot/companies/{hubSpotId}")]
    public async Task<ActionResult<SyncJobResponse>> SyncCompanyFromHubSpot(string hubSpotId, CancellationToken cancellationToken)
    {
        var job = await _companySyncService.SyncFromHubSpotAsync(hubSpotId, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpPost("hubspot/deals/{hubSpotId}")]
    public async Task<ActionResult<SyncJobResponse>> SyncDealFromHubSpot(string hubSpotId, CancellationToken cancellationToken)
    {
        var job = await _dealSyncService.SyncFromHubSpotAsync(hubSpotId, CorrelationId(), cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<SyncJobResponse>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _syncJobRepository.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(SyncJobResponse.FromEntity(job));
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<SyncJobResponse>>> ListJobs(CancellationToken cancellationToken, [FromQuery] int limit = 50)
    {
        var jobs = await _syncJobRepository.ListRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(jobs.Select(SyncJobResponse.FromEntity).ToList());
    }

    /// <summary>Manually re-runs a dead-lettered job. See ISyncJobRetryService for why this creates a new job rather than resuming in place.</summary>
    [HttpPost("jobs/{id:guid}/retry")]
    public async Task<ActionResult<SyncJobResponse>> RetryJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _syncJobRetryService.RetryAsync(id, cancellationToken);
        return Ok(SyncJobResponse.FromEntity(job));
    }

    private string CorrelationId() =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
}
