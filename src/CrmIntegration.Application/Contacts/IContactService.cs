namespace CrmIntegration.Application.Contacts;

public interface IContactService
{
    Task<IReadOnlyList<ContactResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<ContactResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContactResponse> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default, string? correlationId = null);
    Task<ContactResponse> UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default, string? correlationId = null);
}
