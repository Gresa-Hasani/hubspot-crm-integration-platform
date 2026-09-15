namespace CrmIntegration.Application.Common;

/// <summary>
/// Thrown when an operation targets an entity that doesn't exist (e.g. updating a deleted
/// Contact). Mapped to HTTP 404 at the API boundary.
/// </summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityType, Guid id)
        : base($"{entityType} '{id}' was not found.")
    {
    }
}
