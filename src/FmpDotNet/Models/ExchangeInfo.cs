using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One exchange from <c>stable/available-exchanges</c> — 63 measured 2026-08-27, the whole set.
///
/// <para>This is the authoritative spelling of the exchange codes that appear on
/// <see cref="CompanyProfile.Exchange"/>, on <c>SymbolSearchResult.Exchange</c> and as the
/// <c>exchange</c> argument to <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/> — which answers an
/// unknown exchange with an empty array and HTTP 200 rather than an error, so validating against this list is
/// cheaper than debugging an empty result.</para></summary>
public sealed record ExchangeInfo
{
    /// <summary>The short code — <c>AMEX</c>, <c>ASX</c>, <c>FSX</c>. This is the value the rest of the API
    /// expects.
    ///
    /// <para><b>Note which side of the naming this is.</b> On <see cref="CompanyProfile"/> the code lives under
    /// <c>exchange</c> and the display name under <c>exchangeFullName</c>; on
    /// <c>ExchangeVariant</c> those two are the other way round. This field is the code.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The display name — <c>Australian Securities Exchange</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The country's full name — <c>United States of America</c>.</summary>
    [JsonPropertyName("countryName")] public string? CountryName { get; init; }

    /// <summary>The country's ISO alpha-2 code — <c>US</c>. This is the same vocabulary
    /// <see cref="Endpoints.DirectoryEndpoints.GetCountriesAsync"/> returns, so the two join directly.</summary>
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }

    /// <summary>The suffix FMP appends to symbols on this exchange — <c>.AX</c>, <c>.AT</c>.
    ///
    /// <para><b>Five of the 63 rows carry the literal string <c>"N/A"</c> rather than null</b>, measured
    /// 2026-08-27. The SDK does not normalise it, because doing so would hide which value FMP actually sent — but
    /// a caller appending this blindly produces <c>AAPL.N/A</c>. Test for it explicitly, or use
    /// <c>SearchEndpoints.GetExchangeVariantsAsync</c>, which answers the same question by
    /// returning the symbols themselves.</para></summary>
    [JsonPropertyName("symbolSuffix")] public string? SymbolSuffix { get; init; }

    /// <summary>How delayed this exchange's quotes are, <b>as free-text prose</b> — <c>"Real-time"</c>,
    /// <c>"15 min"</c>, <c>"20 min"</c>, <c>"10 min"</c>.
    ///
    /// <para><b>A <see cref="string"/> rather than a <see cref="NodaTime.Duration"/>, deliberately.</b> Those four
    /// spellings are every value measured across the 63 rows, and FMP publishes no mapping from them to a
    /// quantity — <c>"Real-time"</c> is not a duration at all. Parsing would mean inventing a contract the API
    /// does not offer, and would then silently mis-report the day a fifth spelling appears.</para>
    ///
    /// <para>Null on one row of 63 (<c>FSX</c>), so absence is possible.</para></summary>
    [JsonPropertyName("delay")] public string? Delay { get; init; }
}
