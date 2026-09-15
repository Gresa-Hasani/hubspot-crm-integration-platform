using System.Text.Json.Serialization;

namespace CrmIntegration.Infrastructure.HubSpot.Dtos;

/// <summary>Wire shape of a single CRM object as returned by /crm/v3/objects/{type}[/...].</summary>
public class HubSpotObjectDto
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public Dictionary<string, string?> Properties { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}

public class HubSpotWriteRequestDto
{
    [JsonPropertyName("properties")]
    public Dictionary<string, string?> Properties { get; set; } = new();
}

public class HubSpotPagingDto
{
    [JsonPropertyName("next")]
    public HubSpotPagingNextDto? Next { get; set; }
}

public class HubSpotPagingNextDto
{
    [JsonPropertyName("after")]
    public string? After { get; set; }
}

public class HubSpotSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("results")]
    public List<HubSpotObjectDto> Results { get; set; } = new();

    [JsonPropertyName("paging")]
    public HubSpotPagingDto? Paging { get; set; }
}

public class HubSpotSearchRequestDto
{
    [JsonPropertyName("filterGroups")]
    public List<HubSpotFilterGroupDto> FilterGroups { get; set; } = new();

    [JsonPropertyName("properties")]
    public List<string> Properties { get; set; } = new();

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 10;

    [JsonPropertyName("after")]
    public string? After { get; set; }
}

public class HubSpotFilterGroupDto
{
    [JsonPropertyName("filters")]
    public List<HubSpotFilterDto> Filters { get; set; } = new();
}

public class HubSpotFilterDto
{
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "EQ";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Error body HubSpot returns for non-2xx responses.</summary>
public class HubSpotErrorResponseDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

public class HubSpotAssociationsResponseDto
{
    [JsonPropertyName("results")]
    public List<HubSpotAssociationResultDto> Results { get; set; } = new();
}

public class HubSpotAssociationResultDto
{
    [JsonPropertyName("toObjectId")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string ToObjectId { get; set; } = string.Empty;
}
