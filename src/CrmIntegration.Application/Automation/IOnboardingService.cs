namespace CrmIntegration.Application.Automation;

/// <summary>
/// Handles the "Deal transitioned to ClosedWon" business action: create (or reuse) exactly one
/// OnboardingRecord per Deal. See docs/SALES_AUTOMATION.md "Missing Company/Contact policy" and
/// "Re-entering Closed Won".
/// </summary>
public interface IOnboardingService
{
    Task<AutomationOutcome> HandleDealClosedWonAsync(Guid dealId, CancellationToken cancellationToken = default);
}
