using CrmIntegration.Application.Integrations.HubSpot;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>
/// In-memory stand-in for HubSpot itself: stores objects per type, generates sequential ids,
/// supports simple equality search, and tracks associations. Lets tests exercise the full
/// sync-service logic (including "does it avoid creating a duplicate on retry") without HTTP.
/// Can be configured to throw a specific exception on the next call via <see cref="ThrowOnNextCall"/>.
/// </summary>
public class FakeHubSpotClient : IHubSpotClient
{
    private readonly Dictionary<HubSpotObjectType, Dictionary<string, Dictionary<string, string?>>> _objects = new()
    {
        [HubSpotObjectType.Contact] = new(),
        [HubSpotObjectType.Company] = new(),
        [HubSpotObjectType.Deal] = new()
    };

    private readonly Dictionary<(HubSpotObjectType From, string FromId, HubSpotObjectType To), List<string>> _associations = new();

    // Shared across every FakeHubSpotClient instance in the process (not just this one), and
    // randomly seeded per process run, so integration tests — which create a fresh client per
    // test but share one real Postgres database across all tests AND across separate test runs —
    // never generate an id that collides with a company/contact/deal a previous run already left
    // behind (Companies.HubSpotId etc. are unique-constrained).
    private static int s_nextId = Random.Shared.Next(1_000_000, 999_000_000);

    public int CreateCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public Exception? ThrowOnNextCall { get; set; }

    public IReadOnlyDictionary<string, string?> GetProperties(HubSpotObjectType type, string id) => _objects[type][id];

    public Task<HubSpotRecord?> GetContactAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Contact, hubSpotId);

    public Task<HubSpotRecord> CreateContactAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Contact, properties);

    public Task<HubSpotRecord> UpdateContactAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Contact, hubSpotId, properties);

    public Task<HubSpotSearchResult> SearchContactsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Contact, request);

    public Task<HubSpotRecord?> GetCompanyAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Company, hubSpotId);

    public Task<HubSpotRecord> CreateCompanyAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Company, properties);

    public Task<HubSpotRecord> UpdateCompanyAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Company, hubSpotId, properties);

    public Task<HubSpotSearchResult> SearchCompaniesAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Company, request);

    public Task<HubSpotRecord?> GetDealAsync(string hubSpotId, IReadOnlyList<string>? properties = null, CancellationToken cancellationToken = default) =>
        GetAsync(HubSpotObjectType.Deal, hubSpotId);

    public Task<HubSpotRecord> CreateDealAsync(IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        CreateAsync(HubSpotObjectType.Deal, properties);

    public Task<HubSpotRecord> UpdateDealAsync(string hubSpotId, IReadOnlyDictionary<string, string?> properties, CancellationToken cancellationToken = default) =>
        UpdateAsync(HubSpotObjectType.Deal, hubSpotId, properties);

    public Task<HubSpotSearchResult> SearchDealsAsync(HubSpotSearchRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync(HubSpotObjectType.Deal, request);

    public Task CreateAssociationAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, string toId, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var key = (fromType, fromId, toType);
        if (!_associations.TryGetValue(key, out var list))
        {
            list = new List<string>();
            _associations[key] = list;
        }

        if (!list.Contains(toId))
        {
            list.Add(toId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAssociatedIdsAsync(HubSpotObjectType fromType, string fromId, HubSpotObjectType toType, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var key = (fromType, fromId, toType);
        return Task.FromResult<IReadOnlyList<string>>(_associations.TryGetValue(key, out var list) ? list.ToList() : []);
    }

    private Task<HubSpotRecord?> GetAsync(HubSpotObjectType type, string id)
    {
        ThrowIfConfigured();
        return Task.FromResult(_objects[type].TryGetValue(id, out var props) ? new HubSpotRecord(id, props, null, null) : null);
    }

    private Task<HubSpotRecord> CreateAsync(HubSpotObjectType type, IReadOnlyDictionary<string, string?> properties)
    {
        ThrowIfConfigured();
        CreateCallCount++;
        var id = System.Threading.Interlocked.Increment(ref s_nextId).ToString();
        _objects[type][id] = new Dictionary<string, string?>(properties);
        return Task.FromResult(new HubSpotRecord(id, _objects[type][id], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    private Task<HubSpotRecord> UpdateAsync(HubSpotObjectType type, string id, IReadOnlyDictionary<string, string?> properties)
    {
        ThrowIfConfigured();
        UpdateCallCount++;
        if (!_objects[type].TryGetValue(id, out var existing))
        {
            throw new HubSpotNotFoundException($"{type} '{id}' not found.", null);
        }

        foreach (var (key, value) in properties)
        {
            existing[key] = value;
        }

        return Task.FromResult(new HubSpotRecord(id, existing, null, DateTimeOffset.UtcNow));
    }

    private Task<HubSpotSearchResult> SearchAsync(HubSpotObjectType type, HubSpotSearchRequest request)
    {
        ThrowIfConfigured();
        var matches = _objects[type].Where(kvp => request.Filters.All(f =>
            kvp.Value.TryGetValue(f.PropertyName, out var value) &&
            string.Equals(value, f.Value, StringComparison.OrdinalIgnoreCase)));

        var results = matches.Select(kvp => new HubSpotRecord(kvp.Key, kvp.Value, null, null)).ToList();
        return Task.FromResult(new HubSpotSearchResult(results, null));
    }

    private void ThrowIfConfigured()
    {
        if (ThrowOnNextCall is null)
        {
            return;
        }

        var ex = ThrowOnNextCall;
        ThrowOnNextCall = null;
        throw ex;
    }
}
