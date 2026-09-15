using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmIntegration.Infrastructure.HubSpot.Dtos;

/// <summary>
/// HubSpot is inconsistent about whether object ids come back as JSON strings or numbers
/// depending on the endpoint (v3 objects: string "id"; v4 associations: numeric "toObjectId").
/// This reads either and normalizes to string, since every id in this codebase is a string.
/// </summary>
public class StringOrNumberJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var longValue) ? longValue.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Expected string or number for id, got {reader.TokenType}.")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
