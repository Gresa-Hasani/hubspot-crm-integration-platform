using CrmIntegration.Application.Common;
using Xunit;

namespace CrmIntegration.UnitTests.Normalization;

public class NormalizationTests
{
    [Theory]
    [InlineData("  Alice@ACME.example  ", "alice@acme.example")]
    [InlineData("Bob@Example.COM", "bob@example.com")]
    public void NormalizeEmail_TrimsAndLowercases(string input, string expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizeEmail(input));

    [Theory]
    [InlineData("+1 (555) 123-4567", "+15551234567")]
    [InlineData("055 123 45 67", "0551234567")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void NormalizePhone_StripsFormattingButKeepsLeadingPlus(string? input, string? expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizePhone(input));

    [Theory]
    [InlineData("https://www.Acme.example/pricing", "acme.example")]
    [InlineData("HTTP://ACME.EXAMPLE", "acme.example")]
    [InlineData("acme.example/", "acme.example")]
    [InlineData(null, null)]
    public void NormalizeDomain_StripsProtocolWwwAndPath(string? input, string? expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizeDomain(input));

    [Theory]
    [InlineData("  Acme   Technologies  ", "Acme Technologies")]
    [InlineData("Acme\tTechnologies", "Acme Technologies")]
    public void NormalizeCompanyName_TrimsAndCollapsesWhitespace(string input, string expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizeCompanyName(input));

    [Theory]
    [InlineData("USA", "United States")]
    [InlineData("uk", "United Kingdom")]
    [InlineData("Germany", "Germany")]
    [InlineData(null, null)]
    public void NormalizeCountry_MapsKnownAliasesToCanonicalName(string? input, string? expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizeCountry(input));

    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData(" usd ", "USD")]
    public void NormalizeCurrency_Uppercases(string input, string expected) =>
        Assert.Equal(expected, CrmIntegration.Application.Common.Normalization.NormalizeCurrency(input));
}
