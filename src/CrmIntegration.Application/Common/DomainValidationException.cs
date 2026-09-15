namespace CrmIntegration.Application.Common;

/// <summary>
/// Thrown when a request violates a business rule that DataAnnotations can't express
/// (e.g. a referenced CompanyId that doesn't exist). Mapped to HTTP 400 at the API boundary.
/// </summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }
}
