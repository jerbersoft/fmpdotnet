using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One country's share of an ETF's holdings, from <c>stable/etf/country-weightings</c>.
///
/// <para><b>Two keys, and no symbol.</b> Measured 2026-08-30 over 226 rows across 13 ETFs, the shape is exactly
/// <c>country</c> and <c>weightPercentage</c> — the response never names the fund it describes, unlike
/// <see cref="EtfSectorWeighting"/>, which echoes <c>symbol</c> on every row. A caller holding rows from two
/// funds has to keep track of which is which.</para>
///
/// <para><b>The weight arrives as a percent-suffixed STRING here and as a bare number on the sibling
/// path.</b> See <see cref="WeightPercentage"/>.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>ordered by weight, descending</b>. A commodity fund still
/// answers a row rather than an empty list: GLD and SLV each returned one row, <c>"Other"</c> at
/// <c>"100%"</c>, and TLT returned two — <c>"United States"</c> at <c>"98.19%"</c> and <c>"Other"</c> at
/// <c>"1.81%"</c>. Some symbols do answer an empty list at HTTP 200 rather than an error, so the list can
/// still come back empty.</para></summary>
public sealed record EtfCountryWeighting
{
    /// <summary>The country name, as FMP spells it — <c>"United States"</c>, <c>"United Kingdom"</c>. Not an
    /// ISO code, and <c>"Other"</c> is one of the values, so this is not a country vocabulary a caller can
    /// map exhaustively. Nullable because the deserialiser cannot promise a key is present, not because any
    /// measured row omitted it: no row was missing a key across all 226 measured 2026-08-30.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>97.52</c> means 97.52%.
    ///
    /// <para><b>The wire sends this as a string with a trailing <c>%</c></b> — <c>"97.52%"</c>, 227 of 227
    /// rows measured 2026-08-30 — while <see cref="EtfSectorWeighting.WeightPercentage"/>, one letter apart in
    /// the URL, sends a bare JSON number. <see cref="PercentSuffixedDecimalJsonConverter"/> reconciles them, so
    /// both properties mean the same thing to a caller. <b>Do not swap in
    /// <see cref="TolerantDecimalJsonConverter"/></b>: it cannot read a trailing <c>%</c> and would bind
    /// <see langword="null"/> on every row without failing anything.</para></summary>
    [JsonPropertyName("weightPercentage")]
    [JsonConverter(typeof(PercentSuffixedDecimalJsonConverter))]
    public decimal? WeightPercentage { get; init; }
}
