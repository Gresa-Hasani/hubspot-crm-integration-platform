using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

/// <summary>
/// Re-runs a Failed execution's underlying action under a fresh idempotency key (so
/// IAutomationExecutor doesn't short-circuit it as "already done") — this creates a new
/// AutomationExecution row rather than mutating the failed one, the same pattern Phase 4's
/// ISyncJobRetryService uses for dead-lettered SyncJobs, and for the same reason: correlation
/// history stays intact, and re-running the underlying idempotent action from scratch is safe.
/// </summary>
public class AutomationRetryService : IAutomationRetryService
{
    private readonly IAutomationExecutionRepository _executionRepository;
    private readonly IAutomationExecutor _automationExecutor;
    private readonly IOnboardingService _onboardingService;

    public AutomationRetryService(
        IAutomationExecutionRepository executionRepository,
        IAutomationExecutor automationExecutor,
        IOnboardingService onboardingService)
    {
        _executionRepository = executionRepository;
        _automationExecutor = automationExecutor;
        _onboardingService = onboardingService;
    }

    public async Task<AutomationExecution> RetryAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var original = await _executionRepository.GetByIdAsync(executionId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(AutomationExecution), executionId);

        if (original.Status != AutomationStatus.Failed)
        {
            throw new DomainValidationException($"AutomationExecution '{executionId}' is '{original.Status}', not Failed; only failed executions can be retried.");
        }

        var retryKey = $"{original.IdempotencyKey}:retry:{Guid.NewGuid():N}";

        return original.AutomationType switch
        {
            AutomationType.DealClosedWonOnboarding => await _automationExecutor.ExecuteAsync(
                original.AutomationType, original.EntityType, original.EntityId, retryKey, original.CorrelationId,
                ct => _onboardingService.HandleDealClosedWonAsync(original.EntityId, ct), cancellationToken),
            _ => throw new DomainValidationException($"AutomationType '{original.AutomationType}' does not support manual retry.")
        };
    }
}
