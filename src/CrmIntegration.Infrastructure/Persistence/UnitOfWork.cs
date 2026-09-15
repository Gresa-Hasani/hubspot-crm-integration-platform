using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmIntegration.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private const string PostgresUniqueViolationSqlState = "23505";

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
}
