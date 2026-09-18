namespace CrmIntegration.Application.Common;

/// <summary>
/// Commits changes made through repositories in a single database transaction. Needed because
/// later phases (e.g. Closed Won -> Onboarding) touch more than one repository per operation.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a SERIALIZABLE database transaction — used only
    /// where an in-process check-then-write isn't enough to prevent a genuine cross-request race
    /// (e.g. last-Admin protection in user management; see docs/SECURITY.md). The transaction
    /// commits if <paramref name="operation"/> completes normally, or rolls back if it throws.
    /// A true concurrent conflict surfaces as a LastAdminProtectionException from the losing call.
    /// </summary>
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
