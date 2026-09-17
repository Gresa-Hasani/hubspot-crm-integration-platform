using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Automation;

public class DealStageAutomationService : IDealStageAutomationService
{
    private readonly IDealStageTransitionRepository _transitionRepository;
    private readonly IAutomationExecutor _automationExecutor;
    private readonly IOnboardingService _onboardingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DealStageAutomationService> _logger;

    public DealStageAutomationService(
        IDealStageTransitionRepository transitionRepository,
        IAutomationExecutor automationExecutor,
        IOnboardingService onboardingService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<DealStageAutomationService> logger)
    {
        _transitionRepository = transitionRepository;
        _automationExecutor = automationExecutor;
        _onboardingService = onboardingService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DealStageTransition?> EvaluateAsync(
        Deal deal,
        DealStage? previousStage,
        TransitionSource source,
        string correlationId,
        Guid? integrationEventId = null,
        CancellationToken cancellationToken = default)
    {
        // Do not infer a transition when there was no real stage change — repeated
        // synchronization/updates that leave Stage unchanged must never create a row here.
        if (previousStage == deal.Stage)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var transition = new DealStageTransition
        {
            Id = Guid.NewGuid(),
            DealId = deal.Id,
            FromStage = previousStage,
            ToStage = deal.Stage,
            OccurredAt = now,
            Source = source,
            CorrelationId = correlationId,
            IntegrationEventId = integrationEventId,
            CreatedAt = now
        };

        await _transitionRepository.AddAsync(transition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "DealStageChanged DealId={DealId} FromStage={FromStage} ToStage={ToStage} Source={Source} CorrelationId={CorrelationId}",
            deal.Id, previousStage, deal.Stage, source, correlationId);

        if (deal.Stage == DealStage.ClosedWon)
        {
            _logger.LogInformation("ClosedWonDetected DealId={DealId} TransitionId={TransitionId} CorrelationId={CorrelationId}", deal.Id, transition.Id, correlationId);

            // Keyed per-transition (not per-deal): re-entering Closed Won later must still get
            // its own audited execution (reusing the existing onboarding), per
            // docs/SALES_AUTOMATION.md "Re-entering Closed Won" — the actual duplicate-onboarding
            // protection is OnboardingService's own DealId-based check + the DB unique index.
            await _automationExecutor.ExecuteAsync(
                AutomationType.DealClosedWonOnboarding,
                EntityType.Deal,
                deal.Id,
                idempotencyKey: $"DealClosedWonOnboarding:{transition.Id}",
                correlationId,
                ct => _onboardingService.HandleDealClosedWonAsync(deal.Id, ct),
                cancellationToken);
        }

        return transition;
    }
}
