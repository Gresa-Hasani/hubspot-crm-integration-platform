using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Automation;

public class AutomationExecutorTests
{
    private static AutomationExecutor CreateExecutor(
        IAutomationExecutionRepository executionRepository,
        FakeAuditLogRepository auditLogRepository,
        CrmIntegration.Application.Common.IUnitOfWork unitOfWork,
        DateTimeOffset now) =>
        new(executionRepository, auditLogRepository, unitOfWork, new FixedTimeProvider(now), NullLogger<AutomationExecutor>.Instance);

    [Fact]
    public async Task ExecuteAsync_Succeeded_PersistsTerminalExecutionAndAuditLog()
    {
        var executionRepository = new FakeAutomationExecutionRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executor = CreateExecutor(executionRepository, auditLogRepository, unitOfWork, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var entityId = Guid.NewGuid();
        var result = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, entityId, "key-1", "corr-1",
            _ => Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, "Created OnboardingRecord")));

        Assert.Equal(AutomationStatus.Succeeded, result.Status);
        Assert.Equal("Created OnboardingRecord", result.ResultSummary);
        Assert.NotNull(result.CompletedAt);
        Assert.Single(executionRepository.Executions);
        Assert.Single(auditLogRepository.Entries);
        Assert.Equal("AutomationSucceeded", auditLogRepository.Entries[0].Action);
    }

    [Fact]
    public async Task ExecuteAsync_Skipped_PersistsSkippedStatusWithoutError()
    {
        var executionRepository = new FakeAutomationExecutionRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executor = CreateExecutor(executionRepository, auditLogRepository, unitOfWork, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, Guid.NewGuid(), "key-2", "corr-1",
            _ => Task.FromResult(new AutomationOutcome(AutomationStatus.Skipped, "Deal has no associated Company; onboarding requires a Company.")));

        Assert.Equal(AutomationStatus.Skipped, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.FailureCategory);
    }

    [Fact]
    public async Task ExecuteAsync_ActionThrows_MarksFailedAndPersistsAuditLog()
    {
        var executionRepository = new FakeAutomationExecutionRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executor = CreateExecutor(executionRepository, auditLogRepository, unitOfWork, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, Guid.NewGuid(), "key-3", "corr-1",
            _ => throw new InvalidOperationException("boom"));

        Assert.Equal(AutomationStatus.Failed, result.Status);
        Assert.Equal(nameof(InvalidOperationException), result.FailureCategory);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Single(auditLogRepository.Entries);
        Assert.Equal("AutomationFailed", auditLogRepository.Entries[0].Action);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingTerminalExecution_ShortCircuitsWithoutInvokingActionAgain()
    {
        var executionRepository = new FakeAutomationExecutionRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executor = CreateExecutor(executionRepository, auditLogRepository, unitOfWork, DateTimeOffset.UtcNow);

        var entityId = Guid.NewGuid();
        var first = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, entityId, "key-4", "corr-1",
            _ => Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, "Created OnboardingRecord")));

        var actionInvokedAgain = false;
        var second = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, entityId, "key-4", "corr-2",
            _ =>
            {
                actionInvokedAgain = true;
                return Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, "Should not run"));
            });

        Assert.False(actionInvokedAgain);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(executionRepository.Executions);
    }

    [Fact]
    public async Task ExecuteAsync_LosesConcurrentRace_ReturnsWinningExecutionWithoutRunningAction()
    {
        // Simulates: no row exists for this key when the initial lookup runs, but a concurrent
        // caller inserts and commits the winning row before this caller's own insert lands,
        // triggering the DB's unique-constraint violation on IdempotencyKey.
        var winner = new CrmIntegration.Domain.Entities.AutomationExecution
        {
            Id = Guid.NewGuid(),
            AutomationType = AutomationType.DealClosedWonOnboarding,
            EntityType = EntityType.Deal,
            EntityId = Guid.NewGuid(),
            IdempotencyKey = "key-5",
            Status = AutomationStatus.Succeeded,
            CorrelationId = "winner-corr",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ResultSummary = "Created by the winner"
        };
        var executionRepository = new FirstLookupMissesRepository(winner);
        var auditLogRepository = new FakeAuditLogRepository();
        var conflictingUnitOfWork = new ConflictingUnitOfWork(conflictOnCall: 1);
        var executor = CreateExecutor(executionRepository, auditLogRepository, conflictingUnitOfWork, DateTimeOffset.UtcNow);

        var actionInvoked = false;
        var result = await executor.ExecuteAsync(
            AutomationType.DealClosedWonOnboarding, EntityType.Deal, winner.EntityId, "key-5", "loser-corr",
            _ =>
            {
                actionInvoked = true;
                return Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, "Should not run"));
            });

        Assert.False(actionInvoked);
        Assert.Equal(winner.Id, result.Id);
        Assert.Equal("Created by the winner", result.ResultSummary);
    }

    /// <summary>Returns null from the first GetByIdempotencyKeyAsync call (simulating "no row yet"), then behaves like a normal repository seeded with the winning row.</summary>
    private class FirstLookupMissesRepository : IAutomationExecutionRepository
    {
        private readonly FakeAutomationExecutionRepository _inner = new();
        private bool _firstLookupDone;

        public FirstLookupMissesRepository(CrmIntegration.Domain.Entities.AutomationExecution winner)
        {
            _inner.Executions.Add(winner);
        }

        public Task AddAsync(CrmIntegration.Domain.Entities.AutomationExecution execution, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(execution, cancellationToken);

        public Task<CrmIntegration.Domain.Entities.AutomationExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task<CrmIntegration.Domain.Entities.AutomationExecution?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            if (!_firstLookupDone)
            {
                _firstLookupDone = true;
                return Task.FromResult<CrmIntegration.Domain.Entities.AutomationExecution?>(null);
            }

            return _inner.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        }

        public Task<IReadOnlyList<CrmIntegration.Domain.Entities.AutomationExecution>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
            _inner.ListRecentAsync(limit, cancellationToken);
    }
}
