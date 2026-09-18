using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Automation;

public class OnboardingServiceTests
{
    private static OnboardingService CreateService(
        FakeDealRepository dealRepository,
        IOnboardingRepository onboardingRepository,
        CrmIntegration.Application.Common.IUnitOfWork? unitOfWork = null) =>
        new(dealRepository, onboardingRepository, unitOfWork ?? new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<OnboardingService>.Instance);

    [Fact]
    public async Task HandleDealClosedWonAsync_Throws_WhenDealDoesNotExist()
    {
        var service = CreateService(new FakeDealRepository(), new FakeOnboardingRepository());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.HandleDealClosedWonAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HandleDealClosedWonAsync_Skipped_WhenDealHasNoCompany()
    {
        var dealRepository = new FakeDealRepository();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "No Company Deal", CompanyId = null, Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);
        var service = CreateService(dealRepository, new FakeOnboardingRepository());

        var outcome = await service.HandleDealClosedWonAsync(deal.Id);

        Assert.Equal(AutomationStatus.Skipped, outcome.Status);
        Assert.Contains("no associated Company", outcome.ResultSummary);
    }

    [Fact]
    public async Task HandleDealClosedWonAsync_CreatesOnboardingRecord_WithContact_WhenDealHasCompanyAndContact()
    {
        var dealRepository = new FakeDealRepository();
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Enterprise Deal", CompanyId = companyId, ContactId = contactId, Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);
        var onboardingRepository = new FakeOnboardingRepository();
        var service = CreateService(dealRepository, onboardingRepository);

        var outcome = await service.HandleDealClosedWonAsync(deal.Id);

        Assert.Equal(AutomationStatus.Succeeded, outcome.Status);
        var record = Assert.Single(onboardingRepository.Records);
        Assert.Equal(deal.Id, record.DealId);
        Assert.Equal(companyId, record.CompanyId);
        Assert.Equal(contactId, record.ContactId);
        Assert.Equal("DealClosedWonAutomation", record.TriggerSource);
    }

    [Fact]
    public async Task HandleDealClosedWonAsync_CreatesOnboardingRecord_WithoutContact_WhenDealHasNoContact()
    {
        var dealRepository = new FakeDealRepository();
        var companyId = Guid.NewGuid();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Enterprise Deal", CompanyId = companyId, ContactId = null, Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);
        var onboardingRepository = new FakeOnboardingRepository();
        var service = CreateService(dealRepository, onboardingRepository);

        var outcome = await service.HandleDealClosedWonAsync(deal.Id);

        Assert.Equal(AutomationStatus.Succeeded, outcome.Status);
        var record = Assert.Single(onboardingRepository.Records);
        Assert.Null(record.ContactId);
    }

    [Fact]
    public async Task HandleDealClosedWonAsync_ReusesExistingOnboardingRecord_WhenOneAlreadyExistsForDeal()
    {
        var dealRepository = new FakeDealRepository();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Repeat Deal", CompanyId = Guid.NewGuid(), Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);
        var onboardingRepository = new FakeOnboardingRepository();
        var existing = new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal.Id, CompanyId = deal.CompanyId!.Value, Status = OnboardingStatus.Pending, TriggerSource = "DealClosedWonAutomation", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        onboardingRepository.Records.Add(existing);
        var service = CreateService(dealRepository, onboardingRepository);

        var outcome = await service.HandleDealClosedWonAsync(deal.Id);

        Assert.Equal(AutomationStatus.Succeeded, outcome.Status);
        Assert.Contains(existing.Id.ToString(), outcome.ResultSummary);
        Assert.Single(onboardingRepository.Records);
    }

    [Fact]
    public async Task HandleDealClosedWonAsync_ReusesWinningRecord_WhenConcurrentInsertLosesRace()
    {
        var dealRepository = new FakeDealRepository();
        var companyId = Guid.NewGuid();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Race Deal", CompanyId = companyId, Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);

        // Simulate: this caller's initial GetByDealIdAsync sees nothing yet (no row for this
        // Deal), but by the time its own SaveChangesAsync runs, a concurrent automation has
        // already committed the winning row — so the post-conflict re-fetch must find it.
        var winner = new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal.Id, CompanyId = companyId, Status = OnboardingStatus.Pending, TriggerSource = "DealClosedWonAutomation", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var onboardingRepository = new FirstLookupMissesOnboardingRepository(winner);
        var conflictingUnitOfWork = new RaceOnFirstSaveThenReuseUnitOfWork();
        var service = CreateService(dealRepository, onboardingRepository, conflictingUnitOfWork);

        var outcome = await service.HandleDealClosedWonAsync(deal.Id);

        Assert.Equal(AutomationStatus.Succeeded, outcome.Status);
        Assert.Contains("concurrent creation", outcome.ResultSummary);
        Assert.Contains(winner.Id.ToString(), outcome.ResultSummary);
    }

    /// <summary>Throws SyncMappingConflictException on the very first SaveChangesAsync call, simulating a lost race on OnboardingRecords.DealId's unique index.</summary>
    private class RaceOnFirstSaveThenReuseUnitOfWork : CrmIntegration.Application.Common.IUnitOfWork
    {
        private int _callCount;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _callCount++;
            if (_callCount == 1)
            {
                throw new CrmIntegration.Application.Sync.SyncMappingConflictException("Simulated OnboardingRecords.DealId unique-index race.");
            }

            return Task.CompletedTask;
        }

        public Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    /// <summary>Returns null from the first GetByDealIdAsync call (simulating "no row yet"), then behaves like a normal repository seeded with the winning row.</summary>
    private class FirstLookupMissesOnboardingRepository : IOnboardingRepository
    {
        private readonly FakeOnboardingRepository _inner = new();
        private bool _firstLookupDone;

        public FirstLookupMissesOnboardingRepository(OnboardingRecord winner)
        {
            _inner.Records.Add(winner);
        }

        public Task<OnboardingRecord?> GetByDealIdAsync(Guid dealId, CancellationToken cancellationToken = default)
        {
            if (!_firstLookupDone)
            {
                _firstLookupDone = true;
                return Task.FromResult<OnboardingRecord?>(null);
            }

            return _inner.GetByDealIdAsync(dealId, cancellationToken);
        }

        public Task<OnboardingRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task AddAsync(OnboardingRecord record, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(record, cancellationToken);

        public Task<IReadOnlyList<OnboardingRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
            _inner.ListRecentAsync(limit, cancellationToken);
    }
}
