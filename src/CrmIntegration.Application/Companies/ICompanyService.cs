namespace CrmIntegration.Application.Companies;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<CompanyResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyResponse> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
}
