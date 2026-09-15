using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Contacts;

public class ContactService : IContactService
{
    private readonly IContactRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ContactService(
        IContactRepository repository,
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ContactResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var contacts = await _repository.ListAsync(cancellationToken);
        return contacts.Select(ToResponse).ToList();
    }

    public async Task<ContactResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await _repository.GetByIdAsync(id, cancellationToken);
        return contact is null ? null : ToResponse(contact);
    }

    public async Task<ContactResponse> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyExistsAsync(request.CompanyId, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = Normalization.NormalizeEmail(request.Email),
            Phone = Normalization.NormalizePhone(request.Phone),
            JobTitle = request.JobTitle?.Trim(),
            CompanyId = request.CompanyId,
            LifecycleStage = request.LifecycleStage,
            Source = request.Source?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(contact, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(contact);
    }

    public async Task<ContactResponse> UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Contact), id);

        await EnsureCompanyExistsAsync(request.CompanyId, cancellationToken);

        contact.FirstName = request.FirstName.Trim();
        contact.LastName = request.LastName.Trim();
        contact.Email = Normalization.NormalizeEmail(request.Email);
        contact.Phone = Normalization.NormalizePhone(request.Phone);
        contact.JobTitle = request.JobTitle?.Trim();
        contact.CompanyId = request.CompanyId;
        contact.LifecycleStage = request.LifecycleStage;
        contact.Source = request.Source?.Trim();
        contact.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(contact);
    }

    private async Task EnsureCompanyExistsAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is Guid id && !await _companyRepository.ExistsAsync(id, cancellationToken))
        {
            throw new DomainValidationException($"CompanyId '{id}' does not reference an existing company.");
        }
    }

    private static ContactResponse ToResponse(Contact c) => new(
        c.Id, c.HubSpotId, c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
        c.CompanyId, c.LifecycleStage, c.Source, c.CreatedAt, c.UpdatedAt, c.LastSyncedAt);
}
