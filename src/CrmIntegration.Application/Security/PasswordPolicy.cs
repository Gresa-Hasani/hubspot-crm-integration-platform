namespace CrmIntegration.Application.Security;

/// <summary>
/// Minimum password policy enforced on user creation and password reset. Deliberately simple —
/// this is a portfolio-scale internal tool, not a consumer product with a dedicated password-
/// strength UX; see docs/SECURITY.md "Known limitations" for what a production system would add
/// (breach-list checking, configurable complexity rules, etc.).
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= MinimumLength
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);

    public const string Description = "Password must be at least 8 characters and include at least one letter and one digit.";
}
