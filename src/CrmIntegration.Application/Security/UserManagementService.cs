using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public UserManagementService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        (await _userRepository.ListAsync(cancellationToken)).Select(UserSummary.FromEntity).ToList();

    public async Task<UserSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : UserSummary.FromEntity(user);
    }

    public async Task<UserSummary> CreateAsync(string email, string password, UserRole role, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(password))
        {
            throw new DomainValidationException(PasswordPolicy.Description);
        }

        var normalizedEmail = Normalization.NormalizeEmail(email);
        if (await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new DuplicateEmailException(normalizedEmail);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(password),
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await AuditAsync("UserCreated", user.Id, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UserSummary.FromEntity(user);
    }

    public Task<UserSummary> ChangeRoleAsync(Guid id, UserRole newRole, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteSerializableAsync(async ct =>
        {
            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new EntityNotFoundException(nameof(ApplicationUser), id);

            if (user.Role == UserRole.Admin && user.IsActive && newRole != UserRole.Admin
                && await _userRepository.CountActiveAdminsAsync(ct) <= 1)
            {
                throw new ConcurrencyConflictException("Cannot change the role of the last active Admin — at least one active Admin must always exist.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            user.Role = newRole;
            user.UpdatedAt = now;

            await AuditAsync("UserRoleChanged", user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return UserSummary.FromEntity(user);
        }, cancellationToken);

    public Task<UserSummary> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteSerializableAsync(async ct =>
        {
            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new EntityNotFoundException(nameof(ApplicationUser), id);

            if (!isActive && user.Role == UserRole.Admin && user.IsActive
                && await _userRepository.CountActiveAdminsAsync(ct) <= 1)
            {
                throw new ConcurrencyConflictException("Cannot deactivate the last active Admin — at least one active Admin must always exist.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            user.IsActive = isActive;
            user.UpdatedAt = now;

            await AuditAsync(isActive ? "UserActivated" : "UserDeactivated", user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return UserSummary.FromEntity(user);
        }, cancellationToken);

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(newPassword))
        {
            throw new DomainValidationException(PasswordPolicy.Description);
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ApplicationUser), id);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        user.UpdatedAt = now;

        await AuditAsync("PasswordReset", user.Id, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task AuditAsync(string action, Guid userId, DateTime at, CancellationToken cancellationToken) =>
        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = "ApplicationUser",
            EntityId = userId.ToString(),
            Source = "UserManagement",
            CorrelationId = Guid.NewGuid().ToString(),
            Timestamp = at
        }, cancellationToken);
}
