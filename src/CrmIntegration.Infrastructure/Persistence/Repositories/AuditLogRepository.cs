using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default) =>
        await _context.AuditLogs.AddAsync(entry, cancellationToken);
}
