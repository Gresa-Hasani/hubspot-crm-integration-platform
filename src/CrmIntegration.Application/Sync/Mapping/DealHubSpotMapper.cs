using System.Globalization;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Sync.Mapping;

public class DealHubSpotMapper : IDealHubSpotMapper
{
    private readonly IHubSpotStageMapper _stageMapper;
    private readonly HubSpotOptions _options;

    public DealHubSpotMapper(IHubSpotStageMapper stageMapper, IOptions<HubSpotOptions> options)
    {
        _stageMapper = stageMapper;
        _options = options.Value;
    }

    // "pipeline" is deliberately not included: the Deal entity has no Pipeline field (inventing
    // one to complete the mapping was explicitly out of scope), so created deals land in
    // HubSpot's default pipeline. "amount" carries the value; deal_currency_code is only written
    // when HubSpot:SyncDealCurrencyCode is enabled — see HubSpotOptions for why it's off by
    // default (most portals reject it outright without multi-currency configured).
    public IReadOnlyList<string> HubSpotProperties { get; } =
        ["dealname", "amount", "dealstage", "closedate", "deal_currency_code"];

    public IReadOnlyDictionary<string, string?> ToHubSpotProperties(Deal deal)
    {
        var properties = new Dictionary<string, string?>
        {
            ["dealname"] = deal.Name,
            ["amount"] = deal.Amount.ToString(CultureInfo.InvariantCulture),
            ["dealstage"] = _stageMapper.ToHubSpotDealStage(deal.Stage),
            ["closedate"] = deal.CloseDate?.ToString("O")
        };

        if (_options.SyncDealCurrencyCode)
        {
            properties["deal_currency_code"] = deal.Currency;
        }

        return properties;
    }

    public void ApplyHubSpotProperties(Deal deal, HubSpotRecord record)
    {
        if (record.Properties.TryGetValue("dealname", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            deal.Name = name.Trim();
        }

        if (record.Properties.TryGetValue("amount", out var amount)
            && decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            deal.Amount = parsedAmount;
        }

        if (record.Properties.TryGetValue("dealstage", out var dealStage) && !string.IsNullOrWhiteSpace(dealStage))
        {
            var mappedStage = _stageMapper.FromHubSpotDealStage(dealStage);
            if (mappedStage is not null)
            {
                deal.Stage = mappedStage.Value;
                deal.Status = DealService.DeriveStatus(deal.Stage);
            }
        }

        if (record.Properties.TryGetValue("closedate", out var closeDate)
            && DateTimeOffset.TryParse(closeDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedCloseDate))
        {
            deal.CloseDate = parsedCloseDate.UtcDateTime;
        }

        if (record.Properties.TryGetValue("deal_currency_code", out var currency) && !string.IsNullOrWhiteSpace(currency))
        {
            deal.Currency = Normalization.NormalizeCurrency(currency);
        }
    }
}
