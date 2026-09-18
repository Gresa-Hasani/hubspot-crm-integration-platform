using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.UnitTests.TestDoubles;

public class FakeDealStageTransitionRepository : IDealStageTransitionRepository
{
    public List<DealStageTransition> Transitions { get; } = new();

    public Task AddAsync(DealStageTransition transition, CancellationToken cancellationToken = default)
    {
        Transitions.Add(transition);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DealStageTransition>> ListByDealAsync(Guid dealId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DealStageTransition>>(
            Transitions.Where(t => t.DealId == dealId).OrderBy(t => t.OccurredAt).ToList());
}

public class FakeContactLifecycleTransitionRepository : IContactLifecycleTransitionRepository
{
    public List<ContactLifecycleTransition> Transitions { get; } = new();

    public Task AddAsync(ContactLifecycleTransition transition, CancellationToken cancellationToken = default)
    {
        Transitions.Add(transition);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContactLifecycleTransition>> ListByContactAsync(Guid contactId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContactLifecycleTransition>>(
            Transitions.Where(t => t.ContactId == contactId).OrderBy(t => t.OccurredAt).ToList());
}

public class FakeAutomationExecutionRepository : IAutomationExecutionRepository
{
    public List<AutomationExecution> Executions { get; } = new();

    public Task AddAsync(AutomationExecution execution, CancellationToken cancellationToken = default)
    {
        Executions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<AutomationExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Executions.FirstOrDefault(e => e.Id == id));

    public Task<AutomationExecution?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Executions.FirstOrDefault(e => e.IdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<AutomationExecution>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AutomationExecution>>(Executions.OrderByDescending(e => e.StartedAt).Take(limit).ToList());
}

public class FakeOnboardingRepository : IOnboardingRepository
{
    public List<OnboardingRecord> Records { get; } = new();

    public Task<OnboardingRecord?> GetByDealIdAsync(Guid dealId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.FirstOrDefault(r => r.DealId == dealId));

    public Task<OnboardingRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task AddAsync(OnboardingRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OnboardingRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OnboardingRecord>>(Records.OrderByDescending(r => r.CreatedAt).Take(limit).ToList());
}

/// <summary>Throws SyncMappingConflictException on the Nth SaveChangesAsync call (1-based), simulating a DB unique-constraint race lost to a concurrent writer; every other call succeeds normally.</summary>
public class ConflictingUnitOfWork : CrmIntegration.Application.Common.IUnitOfWork
{
    private readonly int _conflictOnCall;
    private int _callCount;

    public ConflictingUnitOfWork(int conflictOnCall = 1)
    {
        _conflictOnCall = conflictOnCall;
    }

    public int SaveChangesCallCount => _callCount;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _callCount++;
        if (_callCount == _conflictOnCall)
        {
            throw new CrmIntegration.Application.Sync.SyncMappingConflictException("Simulated unique-constraint race for test purposes.");
        }

        return Task.CompletedTask;
    }

    public Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}
