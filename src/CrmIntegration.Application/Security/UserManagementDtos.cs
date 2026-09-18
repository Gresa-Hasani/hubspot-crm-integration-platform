using System.ComponentModel.DataAnnotations;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public class CreateUserRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.ReadOnly;
}

public class ChangeUserRoleRequest
{
    public UserRole Role { get; set; }
}

public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}

public class ResetPasswordRequest
{
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
