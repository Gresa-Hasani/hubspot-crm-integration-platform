using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Security;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalization.NormalizeEmail(email);
        var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        // Never log the submitted password. Verify against a dummy hash when the user doesn't
        // exist so the request takes a comparable code path either way (a minor timing-safety
        // measure — this is not a claim of full timing-attack resistance, see docs/SECURITY.md).
        var passwordOk = user is not null && _passwordHasher.VerifyPassword(user.PasswordHash, password);

        if (user is null || !user.IsActive || !passwordOk)
        {
            await AuditAsync("LoginFailed", normalizedEmail, cancellationToken);
            _logger.LogWarning("LoginFailed Email={Email}", normalizedEmail);
            throw new InvalidCredentialsException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        await AuditAsync("LoginSucceeded", normalizedEmail, cancellationToken, user.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Issued after the login save so the audit trail/LastLoginAt update and the refresh
        // token's own save (RefreshTokenService.IssueAsync) are two clear, separately-committed
        // steps rather than one large mixed-concern transaction.
        var refreshToken = await _refreshTokenService.IssueAsync(user.Id, cancellationToken);

        _logger.LogInformation("LoginSucceeded UserId={UserId}", user.Id);

        return new LoginResult(
            accessToken.Token, accessToken.ExpiresAtUtc,
            refreshToken.RawToken, refreshToken.ExpiresAtUtc,
            user.Id, user.Email, user.Role);
    }

    private async Task AuditAsync(string action, string email, CancellationToken cancellationToken, Guid? userId = null)
    {
        await _auditLogRepository.AddAsync(new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = "ApplicationUser",
            EntityId = userId?.ToString() ?? email,
            Source = "Authentication",
            CorrelationId = Guid.NewGuid().ToString(),
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);
    }
}
