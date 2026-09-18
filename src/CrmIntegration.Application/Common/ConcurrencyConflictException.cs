namespace CrmIntegration.Application.Common;

/// <summary>
/// Thrown when a business invariant that requires database-transaction-level protection (e.g.
/// "at least one active Admin must always exist") is violated, either by an in-transaction check
/// or by a genuine concurrent-transaction conflict detected by PostgreSQL itself. Maps to HTTP 409.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
