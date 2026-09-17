namespace CrmIntegration.Application.Deals;

public interface IDealService
{
    Task<IReadOnlyList<DealResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<DealResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DealResponse> CreateAsync(CreateDealRequest request, CancellationToken cancellationToken = default, string? correlationId = null);
    Task<DealResponse> UpdateAsync(Guid id, UpdateDealRequest request, CancellationToken cancellationToken = default, string? correlationId = null);
}
