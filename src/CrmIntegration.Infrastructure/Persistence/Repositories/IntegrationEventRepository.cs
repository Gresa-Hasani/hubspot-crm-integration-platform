using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class IntegrationEventRepository : IIntegrationEventRepository
{
    private readonly AppDbContext _context;

    public IntegrationEventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        await _context.IntegrationEvents.AddAsync(integrationEvent, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Unique-constraint violation on ExternalEventId — a concurrent duplicate delivery
            // won the race. Detach so this context can be reused for the next event in the batch.
            _context.Entry(integrationEvent).State = EntityState.Detached;
            return false;
        }
    }

    public Task<bool> ExistsAsync(string externalEventId, CancellationToken cancellationToken = default) =>
        _context.IntegrationEvents.AnyAsync(e => e.ExternalEventId == externalEventId, cancellationToken);

    public Task<IntegrationEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.IntegrationEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<IntegrationEvent>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        await _context.IntegrationEvents.AsNoTracking().OrderByDescending(e => e.ReceivedAt).Take(limit).ToListAsync(cancellationToken);

    public async Task<IntegrationEvent?> TryClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var candidateId = await _context.IntegrationEvents
            .Where(e => e.Status == IntegrationEventStatus.Received)
            .OrderBy(e => e.ReceivedAt)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidateId is null)
        {
            return null;
        }

        // Single atomic UPDATE ... WHERE Id = x AND Status = 'Received'. If a concurrent worker
        // claimed it first, this affects 0 rows and we report "nothing claimed" rather than
        // taking a row lock — see docs/WEBHOOKS.md "Event claiming" for why this is enough here.
        var rowsAffected = await _context.IntegrationEvents
            .Where(e => e.Id == candidateId && e.Status == IntegrationEventStatus.Received)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Status, IntegrationEventStatus.Processing), cancellationToken);

        if (rowsAffected == 0)
        {
            return null;
        }

        return await _context.IntegrationEvents.FirstOrDefaultAsync(e => e.Id == candidateId, cancellationToken);
    }
}
