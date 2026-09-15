using System.Text.RegularExpressions;

namespace CrmIntegration.Application.Common;

/// <summary>
/// Pure, reusable normalization rules applied before persisting or matching CRM data.
/// Keeping these as static functions (rather than scattering ad-hoc string tweaks through
/// services) makes the same rule apply consistently on internal writes and HubSpot sync.
/// </summary>
public static class Normalization
{
    private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["usa"] = "United States",
        ["us"] = "United States",
        ["u.s.a."] = "United States",
        ["united states of america"] = "United States",
        ["uk"] = "United Kingdom",
        ["u.k."] = "United Kingdom",
        ["great britain"] = "United Kingdom",
        ["deutschland"] = "Germany",
        ["holland"] = "Netherlands",
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var hasLeadingPlus = phone.TrimStart().StartsWith('+');
        var digits = Regex.Replace(phone, @"[^\d]", string.Empty);

        return digits.Length == 0
            ? null
            : (hasLeadingPlus ? "+" : string.Empty) + digits;
    }

    public static string? NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var value = domain.Trim().ToLowerInvariant();
        value = Regex.Replace(value, @"^https?://", string.Empty);
        value = Regex.Replace(value, @"^www\.", string.Empty);
        value = value.TrimEnd('/');
        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
        {
            value = value[..slashIndex];
        }

        return value.Length == 0 ? null : value;
    }

    public static string NormalizeCompanyName(string name) =>
        Regex.Replace(name.Trim(), @"\s+", " ");

    public static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        var trimmed = country.Trim();
        return CountryAliases.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed;
    }

    public static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant();
}
