using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Companies;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CompanyService(ICompanyRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<CompanyResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _repository.ListAsync(cancellationToken);
        return companies.Select(ToResponse).ToList();
    }

    public async Task<CompanyResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(id, cancellationToken);
        return company is null ? null : ToResponse(company);
    }

    public async Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = Normalization.NormalizeCompanyName(request.Name),
            Domain = Normalization.NormalizeDomain(request.Domain),
            Industry = request.Industry?.Trim(),
            Country = Normalization.NormalizeCountry(request.Country),
            EmployeeCount = request.EmployeeCount,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(company);
    }

    public async Task<CompanyResponse> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Company), id);

        company.Name = Normalization.NormalizeCompanyName(request.Name);
        company.Domain = Normalization.NormalizeDomain(request.Domain);
        company.Industry = request.Industry?.Trim();
        company.Country = Normalization.NormalizeCountry(request.Country);
        company.EmployeeCount = request.EmployeeCount;
        company.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(company);
    }

    private static CompanyResponse ToResponse(Company c) => new(
        c.Id, c.HubSpotId, c.Name, c.Domain, c.Industry, c.Country, c.EmployeeCount,
        c.CreatedAt, c.UpdatedAt, c.LastSyncedAt);
}
