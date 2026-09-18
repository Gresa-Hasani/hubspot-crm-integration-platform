using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public record UserSummary(Guid Id, string Email, UserRole Role, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, DateTime? LastLoginAt)
{
    public static UserSummary FromEntity(Domain.Entities.ApplicationUser u) =>
        new(u.Id, u.Email, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt, u.LastLoginAt);
}

/// <summary>
/// Admin-only user provisioning/role/status management — never public self-registration. See
/// docs/SECURITY.md "User management" and "Last-Admin safety" for the protection ChangeRoleAsync/
/// SetActiveStatusAsync apply against ending up with zero active Admins.
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws DomainValidationException for a weak password, DuplicateEmailException for an existing normalized email.</summary>
    Task<UserSummary> CreateAsync(string email, string password, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Throws EntityNotFoundException if the user doesn't exist, ConcurrencyConflictException if this would leave zero active Admins.</summary>
    Task<UserSummary> ChangeRoleAsync(Guid id, UserRole newRole, CancellationToken cancellationToken = default);

    /// <summary>Throws EntityNotFoundException if the user doesn't exist, ConcurrencyConflictException if deactivating this user would leave zero active Admins.</summary>
    Task<UserSummary> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Throws EntityNotFoundException if the user doesn't exist, DomainValidationException for a weak password.</summary>
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
}
