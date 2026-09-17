using CrmIntegration.Application.Common;

namespace CrmIntegration.Application.Reporting;

/// <summary>
/// Parses and validates the "from"/"to" query-string date filters shared by several reporting
/// endpoints. Dates are plain "yyyy-MM-dd" strings interpreted as UTC calendar-day boundaries —
/// see docs/REPORTING.md "Timezone policy". `From` is inclusive at 00:00:00.000 UTC that day;
/// `To` is inclusive through 23:59:59.999 UTC that day.
/// </summary>
public static class ReportingDateRange
{
    public static (DateOnly? From, DateOnly? To, DateTime? FromUtc, DateTime? ToUtc) Parse(string? from, string? to)
    {
        DateOnly? fromDate = ParseDateOnly(from, "from");
        DateOnly? toDate = ParseDateOnly(to, "to");

        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            throw new DomainValidationException($"'from' ({fromDate}) must not be after 'to' ({toDate}).");
        }

        var fromUtc = fromDate is DateOnly f ? f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : (DateTime?)null;
        var toUtc = toDate is DateOnly t ? t.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) : (DateTime?)null;

        return (fromDate, toDate, fromUtc, toUtc);
    }

    private static DateOnly? ParseDateOnly(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var parsed))
        {
            throw new DomainValidationException($"'{paramName}' must be a date in 'yyyy-MM-dd' format; got '{value}'.");
        }

        return parsed;
    }
}
