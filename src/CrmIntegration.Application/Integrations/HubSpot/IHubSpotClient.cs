namespace CrmIntegration.Application.Integrations.HubSpot;

/// <summary>
/// Port to the HubSpot CRM REST API. Implementations must translate transport/HTTP failures
/// into the <see cref="HubSpotApiException"/> hierarchy — callers should never see a raw
/// HttpRequestException or a bare non-success HttpResponseMessage.
/// </summary>
public interface IHubSpotClient
{
    Task<HubSpotRecord?> GetContactAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> CreateContactAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> UpdateContactAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotSearchResult> SearchContactsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default);

    Task<HubSpotRecord?> GetCompanyAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> CreateCompanyAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> UpdateCompanyAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotSearchResult> SearchCompaniesAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default);

    Task<HubSpotRecord?> GetDealAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> CreateDealAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotRecord> UpdateDealAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default);
    Task<HubSpotSearchResult> SearchDealsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates the default association label between two objects (e.g. contact_to_company).</summary>
    Task CreateAssociationAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, string toId, CancellationToken cancellationToken = default);

    /// <summary>Returns the ids of objects of <paramref name="toType"/> associated with the given object.</summary>
    Task<IReadOnlyList<string>> GetAssociatedIdsAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, CancellationToken cancellationToken = default);
}
