using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Automation;

public interface IOnboardingRepository
{
    Task<OnboardingRecord?> GetByDealIdAsync(Guid dealId, CancellationToken cancellationToken = default);
    Task<OnboardingRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(OnboardingRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OnboardingRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
