using CrmIntegration.Application.Automation;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Phase 6 sales workflow automation inspection API: automation execution history/audit trail
/// and manual retry. Read-only entity history (stage/lifecycle transitions) lives here too since
/// it is a byproduct of the same automation pipeline.
///
/// SECURITY NOTE (temporary, development-only): not protected by authentication/authorization
/// yet, matching the existing SyncController/WebhooksController posture. Phase 8 scope.
/// </summary>
[ApiController]
[Route("api")]
public class AutomationsController : ControllerBase
{
    private readonly IAutomationExecutionRepository _automationExecutionRepository;
    private readonly IAutomationRetryService _automationRetryService;
    private readonly IDealStageTransitionRepository _dealStageTransitionRepository;
    private readonly IContactLifecycleTransitionRepository _contactLifecycleTransitionRepository;
    private readonly IOnboardingRepository _onboardingRepository;

    public AutomationsController(
        IAutomationExecutionRepository automationExecutionRepository,
        IAutomationRetryService automationRetryService,
        IDealStageTransitionRepository dealStageTransitionRepository,
        IContactLifecycleTransitionRepository contactLifecycleTransitionRepository,
        IOnboardingRepository onboardingRepository)
    {
        _automationExecutionRepository = automationExecutionRepository;
        _automationRetryService = automationRetryService;
        _dealStageTransitionRepository = dealStageTransitionRepository;
        _contactLifecycleTransitionRepository = contactLifecycleTransitionRepository;
        _onboardingRepository = onboardingRepository;
    }

    [HttpGet("automations/executions")]
    public async Task<ActionResult<IReadOnlyList<AutomationExecutionResponse>>> ListExecutions(
        CancellationToken cancellationToken, [FromQuery] int limit = 50)
    {
        var executions = await _automationExecutionRepository.ListRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(executions.Select(AutomationExecutionResponse.FromEntity).ToList());
    }

    [HttpGet("automations/executions/{id:guid}")]
    public async Task<ActionResult<AutomationExecutionResponse>> GetExecution(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _automationExecutionRepository.GetByIdAsync(id, cancellationToken);
        return execution is null ? NotFound() : Ok(AutomationExecutionResponse.FromEntity(execution));
    }

    /// <summary>Manually re-runs a Failed AutomationExecution. See IAutomationRetryService for why this creates a new execution row rather than resuming in place.</summary>
    [HttpPost("automations/executions/{id:guid}/retry")]
    public async Task<ActionResult<AutomationExecutionResponse>> RetryExecution(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _automationRetryService.RetryAsync(id, cancellationToken);
        return Ok(AutomationExecutionResponse.FromEntity(execution));
    }

    [HttpGet("deals/{id:guid}/stage-history")]
    public async Task<ActionResult<IReadOnlyList<DealStageTransitionResponse>>> GetDealStageHistory(Guid id, CancellationToken cancellationToken)
    {
        var transitions = await _dealStageTransitionRepository.ListByDealAsync(id, cancellationToken);
        return Ok(transitions.Select(DealStageTransitionResponse.FromEntity).ToList());
    }

    [HttpGet("contacts/{id:guid}/lifecycle-history")]
    public async Task<ActionResult<IReadOnlyList<ContactLifecycleTransitionResponse>>> GetContactLifecycleHistory(Guid id, CancellationToken cancellationToken)
    {
        var transitions = await _contactLifecycleTransitionRepository.ListByContactAsync(id, cancellationToken);
        return Ok(transitions.Select(ContactLifecycleTransitionResponse.FromEntity).ToList());
    }

    [HttpGet("onboarding")]
    public async Task<ActionResult<IReadOnlyList<OnboardingRecordResponse>>> ListOnboardingRecords(
        CancellationToken cancellationToken, [FromQuery] int limit = 50)
    {
        var records = await _onboardingRepository.ListRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(records.Select(OnboardingRecordResponse.FromEntity).ToList());
    }

    [HttpGet("onboarding/{id:guid}")]
    public async Task<ActionResult<OnboardingRecordResponse>> GetOnboardingRecord(Guid id, CancellationToken cancellationToken)
    {
        var record = await _onboardingRepository.GetByIdAsync(id, cancellationToken);
        return record is null ? NotFound() : Ok(OnboardingRecordResponse.FromEntity(record));
    }
}
