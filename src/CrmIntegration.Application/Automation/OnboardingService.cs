using CrmIntegration.Application.Common;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Automation;

public class OnboardingService : IOnboardingService
{
    private readonly IDealRepository _dealRepository;
    private readonly IOnboardingRepository _onboardingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(
        IDealRepository dealRepository,
        IOnboardingRepository onboardingRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<OnboardingService> logger)
    {
        _dealRepository = dealRepository;
        _onboardingRepository = onboardingRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AutomationOutcome> HandleDealClosedWonAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        var deal = await _dealRepository.GetByIdAsync(dealId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Deal), dealId);

        var existing = await _onboardingRepository.GetByDealIdAsync(dealId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("OnboardingReused DealId={DealId} OnboardingRecordId={OnboardingRecordId}", dealId, existing.Id);
            return new AutomationOutcome(AutomationStatus.Succeeded, $"Reused existing OnboardingRecord {existing.Id}");
        }

        // Domain policy (see docs/SALES_AUTOMATION.md "Missing Company/Contact policy"):
        // OnboardingRecord.CompanyId is required — a Closed Won deal with no Company cannot get
        // an onboarding record. This is a deliberate skip, not a failure: nothing went wrong,
        // there is just no safe data to act on, and we never fabricate a Company.
        if (deal.CompanyId is not Guid companyId)
        {
            _logger.LogInformation("AutomationSkipped DealId={DealId} Reason=NoCompany", dealId);
            return new AutomationOutcome(AutomationStatus.Skipped, "Deal has no associated Company; onboarding requires a Company.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var onboarding = new OnboardingRecord
        {
            Id = Guid.NewGuid(),
            DealId = dealId,
            CompanyId = companyId,
            ContactId = deal.ContactId, // optional — onboarding proceeds without a primary Contact
            Status = OnboardingStatus.Pending,
            TriggerSource = "DealClosedWonAutomation",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _onboardingRepository.AddAsync(onboarding, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (SyncMappingConflictException)
        {
            // Lost a race to a concurrent Closed Won automation for the same Deal — the
            // OnboardingRecords.DealId unique index is the final backstop (see
            // docs/SALES_AUTOMATION.md "Concurrency protection"). Reuse whatever the winner created.
            var winner = await _onboardingRepository.GetByDealIdAsync(dealId, cancellationToken)
                ?? throw new InvalidOperationException($"Lost a race creating OnboardingRecord for Deal '{dealId}' but the winning row could not be found.");
            _logger.LogInformation("OnboardingReused (lost concurrent race) DealId={DealId} OnboardingRecordId={OnboardingRecordId}", dealId, winner.Id);
            return new AutomationOutcome(AutomationStatus.Succeeded, $"Reused existing OnboardingRecord {winner.Id} (concurrent creation)");
        }

        _logger.LogInformation("OnboardingCreated DealId={DealId} OnboardingRecordId={OnboardingRecordId} HasContact={HasContact}", dealId, onboarding.Id, deal.ContactId is not null);
        return new AutomationOutcome(AutomationStatus.Succeeded, $"Created OnboardingRecord {onboarding.Id}");
    }
}
