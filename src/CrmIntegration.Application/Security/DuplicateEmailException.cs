namespace CrmIntegration.Application.Security;

/// <summary>Thrown when creating a user whose normalized email already exists. Maps to HTTP 409.</summary>
public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email) : base($"A user with email '{email}' already exists.")
    {
    }
}
