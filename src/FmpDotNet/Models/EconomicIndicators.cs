using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One observation of one macroeconomic series. From <c>stable/economic-indicators</c>.
///
/// <para>The narrowest record in the SDK, and deliberately so: the endpoint answers a name, a date and a
/// number, and nothing about which series a row belongs to is carried anywhere except
/// <see cref="Name"/>.</para>
///
/// <para><see cref="Name"/> is the wire spelling of the
/// <see cref="EconomicIndicator"/> that was asked for — <c>federalFunds</c>, <c>CPI</c>,
/// <c>30YearFixedRateMortgageAverage</c>. It is not mapped back to the enum, because a value FMP invented
/// after this SDK shipped has no member to map to and would have to be discarded or guessed
/// at.</para></summary>
public sealed record EconomicObservation
{
    /// <summary>The series this row belongs to, spelled as FMP spells it — the same string
    /// <see cref="EconomicIndicatorExtensions.ToQueryValue"/> sent.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The observation date. Monthly series are dated to the first of the month, quarterly series to
    /// the first day of the quarter — measured 2026-08-29, <c>GDP</c> answers <c>2025-10-01</c> for Q4.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The observation. Units are the series' own and are not carried on the row — <c>GDP</c> is
    /// billions of dollars, <c>federalFunds</c> is a percentage, <c>CPI</c> is an index.</summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }
}

/// <summary>One country's equity risk premium. From <c>stable/market-risk-premium</c>.
///
/// <para>The whole response is 192 rows, measured 2026-08-29, returned reverse-alphabetically by country.
/// There is no query surface at all — no country parameter, no date parameter — so this is a full download or
/// nothing.</para></summary>
public sealed record MarketRiskPremium
{
    /// <summary>The country, as FMP names it. Non-empty on all 192 rows measured 2026-08-29; nullable
    /// because every string on every model in this SDK is.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>The continent, as FMP groups it. Non-empty on all 192 rows measured 2026-08-29.</summary>
    [JsonPropertyName("continent")] public string? Continent { get; init; }

    /// <summary>The premium attributable to country risk alone, as a percentage.</summary>
    [JsonPropertyName("countryRiskPremium")] public decimal? CountryRiskPremium { get; init; }

    /// <summary>The total equity risk premium — the mature-market premium plus
    /// <see cref="CountryRiskPremium"/>, as a percentage.</summary>
    [JsonPropertyName("totalEquityRiskPremium")] public decimal? TotalEquityRiskPremium { get; init; }
}

/// <summary>One day's US Treasury yield curve, twelve tenors wide. From <c>stable/treasury-rates</c>.
///
/// <para>Every tenor is a percentage and every one is <see langword="decimal"/>. The property names are the
/// tenors: <see cref="Month1"/> through <see cref="Month6"/>, then <see cref="Year1"/> through
/// <see cref="Year30"/>. All twelve carried a value on every row measured 2026-08-29.</para>
///
/// <para><b>This is the one path in issue #40's group whose data is current.</b> Measured 2026-08-29 the bare
/// call answered 2026-05-29 through 2026-08-27; the indicator, ESG-benchmark and COT paths beside it are all
/// months or years stale. See <c>GetTreasuryRatesAsync</c>.</para></summary>
public sealed record TreasuryRate
{
    /// <summary>The trading day this curve was observed on. Weekends and holidays are absent rather than
    /// repeated.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>One-month yield, as a percentage.</summary>
    [JsonPropertyName("month1")] public decimal? Month1 { get; init; }

    /// <summary>Two-month yield, as a percentage.</summary>
    [JsonPropertyName("month2")] public decimal? Month2 { get; init; }

    /// <summary>Three-month yield, as a percentage.</summary>
    [JsonPropertyName("month3")] public decimal? Month3 { get; init; }

    /// <summary>Six-month yield, as a percentage.</summary>
    [JsonPropertyName("month6")] public decimal? Month6 { get; init; }

    /// <summary>One-year yield, as a percentage.</summary>
    [JsonPropertyName("year1")] public decimal? Year1 { get; init; }

    /// <summary>Two-year yield, as a percentage.</summary>
    [JsonPropertyName("year2")] public decimal? Year2 { get; init; }

    /// <summary>Three-year yield, as a percentage.</summary>
    [JsonPropertyName("year3")] public decimal? Year3 { get; init; }

    /// <summary>Five-year yield, as a percentage.</summary>
    [JsonPropertyName("year5")] public decimal? Year5 { get; init; }

    /// <summary>Seven-year yield, as a percentage.</summary>
    [JsonPropertyName("year7")] public decimal? Year7 { get; init; }

    /// <summary>Ten-year yield, as a percentage.</summary>
    [JsonPropertyName("year10")] public decimal? Year10 { get; init; }

    /// <summary>Twenty-year yield, as a percentage.</summary>
    [JsonPropertyName("year20")] public decimal? Year20 { get; init; }

    /// <summary>Thirty-year yield, as a percentage.</summary>
    [JsonPropertyName("year30")] public decimal? Year30 { get; init; }
}
