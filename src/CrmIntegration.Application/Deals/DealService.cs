using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Deals;

public class DealService : IDealService
{
    private readonly IDealRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DealService(
        IDealRepository repository,
        ICompanyRepository companyRepository,
        IContactRepository contactRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _contactRepository = contactRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<DealResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var deals = await _repository.ListAsync(cancellationToken);
        return deals.Select(ToResponse).ToList();
    }

    public async Task<DealResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deal = await _repository.GetByIdAsync(id, cancellationToken);
        return deal is null ? null : ToResponse(deal);
    }

    public async Task<DealResponse> CreateAsync(CreateDealRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureReferencesExistAsync(request.CompanyId, request.ContactId, cancellationToken);
        var currency = ValidateAndNormalizeCurrency(request.Currency);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            Stage = request.Stage,
            Status = DeriveStatus(request.Stage),
            Amount = request.Amount,
            Currency = currency,
            CloseDate = request.CloseDate,
            Owner = request.Owner?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(deal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(deal);
    }

    public async Task<DealResponse> UpdateAsync(Guid id, UpdateDealRequest request, CancellationToken cancellationToken = default)
    {
        var deal = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Deal), id);

        await EnsureReferencesExistAsync(request.CompanyId, request.ContactId, cancellationToken);
        var currency = ValidateAndNormalizeCurrency(request.Currency);

        deal.Name = request.Name.Trim();
        deal.CompanyId = request.CompanyId;
        deal.ContactId = request.ContactId;
        deal.Stage = request.Stage;
        deal.Status = DeriveStatus(request.Stage);
        deal.Amount = request.Amount;
        deal.Currency = currency;
        deal.CloseDate = request.CloseDate;
        deal.Owner = request.Owner?.Trim();
        deal.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(deal);
    }

    /// <summary>
    /// Status always follows Stage rather than being freely settable, so the two fields can't
    /// drift apart (e.g. Stage=ClosedWon but Status=Open).
    /// </summary>
    public static DealStatus DeriveStatus(DealStage stage) => stage switch
    {
        DealStage.ClosedWon => DealStatus.Won,
        DealStage.ClosedLost => DealStatus.Lost,
        _ => DealStatus.Open
    };

    private static string ValidateAndNormalizeCurrency(string currency)
    {
        var normalized = Normalization.NormalizeCurrency(currency);
        if (normalized.Length != 3 || !normalized.All(char.IsLetter))
        {
            throw new DomainValidationException($"Currency '{currency}' must be a 3-letter ISO 4217 code.");
        }

        return normalized;
    }

    private async Task EnsureReferencesExistAsync(Guid? companyId, Guid? contactId, CancellationToken cancellationToken)
    {
        if (companyId is Guid cId && !await _companyRepository.ExistsAsync(cId, cancellationToken))
        {
            throw new DomainValidationException($"CompanyId '{cId}' does not reference an existing company.");
        }

        if (contactId is Guid ctId && !await _contactRepository.ExistsAsync(ctId, cancellationToken))
        {
            throw new DomainValidationException($"ContactId '{ctId}' does not reference an existing contact.");
        }
    }

    private static DealResponse ToResponse(Deal d) => new(
        d.Id, d.HubSpotId, d.Name, d.CompanyId, d.ContactId, d.Stage, d.Amount, d.Currency,
        d.CloseDate, d.Owner, d.Status, d.CreatedAt, d.UpdatedAt, d.LastSyncedAt);
}
