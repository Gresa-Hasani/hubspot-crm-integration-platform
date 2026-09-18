using System.Data;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmIntegration.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const string PostgresSerializationFailureSqlState = "40001";

    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState })
        {
            // Translated here (Infrastructure) rather than in Application code, which must not
            // depend on Npgsql. Most commonly hit by a race on EntityMapping's uniqueness
            // constraints during concurrent synchronization of the same entity.
            throw new SyncMappingConflictException("A conflicting record already exists (unique constraint violation).", ex);
        }
    }

    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex) when (IsSerializationFailure(ex))
        {
            // A serialization failure aborts the transaction server-side (and Npgsql marks the
            // NpgsqlTransaction object itself as completed) before this catch even runs, so
            // calling RollbackAsync again here would itself throw "transaction has completed" —
            // safe to skip it and just dispose (the `await using` above still does that).
            throw new ConcurrencyConflictException("A conflicting change was made concurrently; please retry.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// True for a raw serialization-failure PostgresException (thrown directly by e.g. a raw
    /// ExecuteUpdateAsync call) OR one wrapped in a DbUpdateException (thrown when the failure
    /// happens inside SaveChangesAsync) — EF Core wraps provider exceptions during SaveChanges,
    /// so both shapes are real and both need to be caught here.
    /// </summary>
    private static bool IsSerializationFailure(Exception ex) => ex switch
    {
        PostgresException { SqlState: PostgresSerializationFailureSqlState } => true,
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresSerializationFailureSqlState } } => true,
        _ => false
    };
}
