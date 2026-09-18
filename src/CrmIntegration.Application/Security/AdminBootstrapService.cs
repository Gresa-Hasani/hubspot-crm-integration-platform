using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Security;

public class AdminBootstrapService : IAdminBootstrapService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly BootstrapAdminOptions _options;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BootstrapAdminOptions> options,
        ILogger<AdminBootstrapService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return;
        }

        if (await _userRepository.AnyUsersExistAsync(cancellationToken))
        {
            return;
        }

        if (!PasswordPolicy.IsValid(_options.Password))
        {
            _logger.LogWarning("BootstrapAdminSkipped Reason=PasswordDoesNotMeetPolicy");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = Normalization.NormalizeEmail(_options.Email),
            PasswordHash = _passwordHasher.HashPassword(_options.Password),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _userRepository.AddAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("BootstrapAdminCreated UserId={UserId}", admin.Id); // never logs the email/password
    }
}
