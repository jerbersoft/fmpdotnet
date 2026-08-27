using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Buffett-style owner earnings for one fiscal quarter. From <c>stable/owner-earnings</c>.
///
/// <para><b>Quarterly only.</b> The endpoint accepts <c>period</c> and ignores it, measured 2026-08-27, so there
/// is no annual series to ask for — the rows step by quarter and the newest is the latest reported one.</para>
///
/// <para><b>Owner earnings is a derived figure, not a filed one.</b> It is net income plus depreciation and
/// amortisation less the capital spending needed to hold the business steady, and the last term is an estimate
/// FMP makes: it splits total capex into <see cref="MaintenanceCapex"/> and <see cref="GrowthCapex"/> using
/// <see cref="AveragePpe"/>. No issuer files that split. Two providers computing this from the same statements
/// will disagree, and nothing on the row records the method.</para></summary>
public sealed record OwnerEarnings
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>ISO currency the figures are reported in.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Fiscal year. Arrives as a JSON <b>string</b> on this path — <c>"2026"</c> — and as an integer on
    /// six others in the same section. One <c>int?</c> reads both only because <c>FmpJsonContext</c> sets
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal quarter as FMP labels it: <c>Q1</c>–<c>Q4</c>. Never <c>FY</c> — see the type's
    /// summary.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>Quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The ratio FMP uses to split capex into maintenance and growth — <b>a rate, not an amount</b>,
    /// despite the name reading like a balance. AAPL measured 0.13466 for Q3 2026.</summary>
    [JsonPropertyName("averagePPE")] public decimal? AveragePpe { get; init; }

    /// <summary>Estimated capital spending needed to maintain the business, signed the way the cash flow
    /// statement signs capex outflows. <b>Usually negative but not guaranteed to be</b>: measured 2026-08-27,
    /// AAPL's Q3 2026 row is −383,794,540 and its Q2 2026 row is +159,994,500. Add it to net income exactly as
    /// FMP signs it; do not flip the sign first, on this row or any other.</summary>
    [JsonPropertyName("maintenanceCapex")] public decimal? MaintenanceCapex { get; init; }

    /// <summary>Owner earnings for the quarter. Note the spelling: FMP writes <c>ownersEarnings</c>, plural
    /// possessive, where the endpoint is named <c>owner-earnings</c>.</summary>
    [JsonPropertyName("ownersEarnings")] public decimal? OwnersEarnings { get; init; }

    /// <summary>Estimated capital spending on growth — total capex less <see cref="MaintenanceCapex"/>. Also
    /// negative.</summary>
    [JsonPropertyName("growthCapex")] public decimal? GrowthCapex { get; init; }

    /// <summary>Owner earnings per share for the quarter.</summary>
    [JsonPropertyName("ownersEarningsPerShare")] public decimal? OwnersEarningsPerShare { get; init; }
}
