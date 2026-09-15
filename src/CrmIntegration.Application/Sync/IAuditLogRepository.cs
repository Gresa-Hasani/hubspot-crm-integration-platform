using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default);
}
