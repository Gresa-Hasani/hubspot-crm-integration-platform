namespace CrmIntegration.Application.Security;

/// <summary>Thrown for any login failure — unknown email, wrong password, or inactive account. Deliberately generic (maps to HTTP 401) so a caller can never distinguish "wrong password" from "no such account" from "account disabled".</summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}
