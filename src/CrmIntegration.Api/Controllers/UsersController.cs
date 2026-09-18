using CrmIntegration.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Admin-only user provisioning/role/status management. There is no public self-registration —
/// see docs/SECURITY.md "User management". Never returns PasswordHash (UserSummary excludes it
/// by construction).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public UsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await _userManagementService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserSummary>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManagementService.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserSummary>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var created = await _userManagementService.CreateAsync(request.Email, request.Password, request.Role, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserSummary>> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await _userManagementService.ChangeRoleAsync(id, request.Role, cancellationToken));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<UserSummary>> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await _userManagementService.SetActiveStatusAsync(id, request.IsActive, cancellationToken));

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userManagementService.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
        return NoContent();
    }
}
