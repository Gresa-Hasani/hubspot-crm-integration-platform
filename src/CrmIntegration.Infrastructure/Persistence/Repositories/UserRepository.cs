using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<ApplicationUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _context.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task<IReadOnlyList<ApplicationUser>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.ApplicationUsers.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);

    public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) =>
        _context.ApplicationUsers.AsNoTracking().AnyAsync(cancellationToken);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        _context.ApplicationUsers.Where(u => u.Role == UserRole.Admin && u.IsActive).CountAsync(cancellationToken);

    public async Task AddAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        await _context.ApplicationUsers.AddAsync(user, cancellationToken);
}
