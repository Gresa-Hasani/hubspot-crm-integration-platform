namespace CrmIntegration.Application.Common;

/// <summary>
/// Commits changes made through repositories in a single database transaction. Needed because
/// later phases (e.g. Closed Won -> Onboarding) touch more than one repository per operation.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
