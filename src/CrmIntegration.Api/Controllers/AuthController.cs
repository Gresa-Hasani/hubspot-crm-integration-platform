using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CrmIntegration.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Login, refresh-token rotation, logout, and "who am I". See docs/SECURITY.md for the full
/// authentication design. Login/refresh are intentionally anonymous (that's the point of them);
/// logout and /me require a valid access token.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;

    public AuthController(
        IAuthenticationService authenticationService,
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository)
    {
        _authenticationService = authenticationService;
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(LoginResponse.FromResult(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);

        // A rotated access token still needs the user's current role/email — RefreshTokenService
        // already confirmed the user is active before rotating, so this is just a claims lookup,
        // minted via the same IJwtTokenService the login path uses (keeping RefreshTokenService
        // itself JWT-agnostic — it only knows tokens, not claims).
        var user = await _userRepository.GetByIdAsync(rotation.UserId, cancellationToken)
            ?? throw new InvalidRefreshTokenException();
        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Ok(new RefreshResponse(accessToken.Token, accessToken.ExpiresAtUtc, rotation.NewToken.RawToken, rotation.NewToken.ExpiresAtUtc));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await _refreshTokenService.RevokeAsync(request.RefreshToken, CurrentUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        var id = CurrentUserId();
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Ok(new CurrentUserResponse(id, email, Enum.Parse<Domain.Enums.UserRole>(role)));
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException("Authenticated request is missing a subject claim."));
}
