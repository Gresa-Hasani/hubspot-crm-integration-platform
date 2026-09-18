namespace CrmIntegration.Application.Security;

/// <summary>Thrown when a presented refresh token is unknown, expired, or already revoked/rotated. Maps to HTTP 401 — never reveals which of those was the specific reason.</summary>
public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException() : base("The refresh token is invalid, expired, or has already been used.")
    {
    }
}
