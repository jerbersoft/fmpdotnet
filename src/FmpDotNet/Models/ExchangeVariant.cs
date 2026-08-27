using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One listing of a security from <c>stable/search-exchange-variants</c> — every exchange the symbol
/// trades on, with a full company profile attached to each. AAPL answered 6 rows measured 2026-08-27.
///
/// <para><b>This is a v3-era profile shape served under a <c>stable</c> path, and it is not
/// <see cref="CompanyProfile"/>.</b> Both carry 36 fields and 29 of them agree, which is exactly what makes the
/// difference dangerous. Three are pure renames, confirmed by value equality on AAPL —
/// <c>change</c>/<c>changes</c>, <c>lastDividend</c>/<c>lastDiv</c>, <c>marketCap</c>/<c>mktCap</c>. Two more have
/// no counterpart at all: this shape carries <see cref="Dcf"/> and <see cref="DcfDiff"/>, which
/// <see cref="CompanyProfile"/> does not, and omits <c>volume</c> and <c>changePercentage</c>, which it does.
/// <c>averageVolume</c> and <see cref="VolAvg"/> are <b>not</b> a rename: 53,379,406 against 55,604,384 on the
/// same symbol, so they are computed differently or refreshed on different schedules.</para>
///
/// <para><b>The trap is <see cref="Exchange"/>.</b> On <see cref="CompanyProfile"/>, <c>exchange</c> holds the
/// short code and <c>exchangeFullName</c> the display name. Here they are inverted: <c>exchange</c> is
/// <c>"NASDAQ Global Select"</c> and the code <c>"NASDAQ"</c> lives in <see cref="ExchangeShortName"/>. Same field
/// name, opposite meaning, on two endpoints a caller will reasonably use together — and the failure is a filter
/// that silently matches nothing.</para>
///
/// <para><b><see cref="Cik"/> is populated only on the primary listing</b> — null on 5 of the 6 measured rows —
/// so this is not the symbol-to-CIK bridge it appears to be.</para></summary>
public sealed record ExchangeVariant
{
    /// <summary>The ticker on this exchange — <c>AAPL</c>, <c>AAPL.MX</c>, <c>APC.F</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The last price, <b>in <see cref="Currency"/> rather than a common one</b>. Null on one of the six
    /// measured rows, which still reported <see cref="IsActivelyTrading"/> true.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Beta against the market. Identical across all six listings of AAPL, so it describes the company
    /// rather than the listing.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Average volume, under FMP's v3 spelling <c>volAvg</c>.
    ///
    /// <para><b>Not the same number as <see cref="CompanyProfile.AverageVolume"/></b> — 55,604,384 here against
    /// 53,379,406 there for AAPL on the same day. Whatever the difference is, FMP does not document it, so the two
    /// are not interchangeable.</para></summary>
    [JsonPropertyName("volAvg")] public decimal? VolAvg { get; init; }

    /// <summary>Market capitalisation, under the v3 spelling <c>mktCap</c>, and <b>in <see cref="Currency"/></b>:
    /// the Mexican listing reads 78,283,607,480,000 MXN against the US listing's 4,603,751,738,200 USD for the
    /// same company. Confirmed equal to <see cref="CompanyProfile.MarketCap"/> for the primary listing.</summary>
    [JsonPropertyName("mktCap")] public decimal? MktCap { get; init; }

    /// <summary>The last dividend, under the v3 spelling <c>lastDiv</c>. Confirmed equal to
    /// <see cref="CompanyProfile.LastDividend"/>.</summary>
    [JsonPropertyName("lastDiv")] public decimal? LastDiv { get; init; }

    /// <summary>The 52-week range as free text — <c>"169.21-320.85"</c>. A string, not a pair: FMP sends one
    /// hyphenated field, and splitting it is guesswork for any symbol whose prices are negative or formatted with
    /// a different separator. Null on one measured row.</summary>
    [JsonPropertyName("range")] public string? Range { get; init; }

    /// <summary>The absolute price change, under the v3 spelling <c>changes</c>. Confirmed equal to
    /// <see cref="CompanyProfile.Change"/>. There is <b>no</b> percentage counterpart on this shape.</summary>
    [JsonPropertyName("changes")] public decimal? Changes { get; init; }

    /// <summary>The company name — the same on every listing.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The currency this listing trades in. <b>Read this before comparing <see cref="Price"/> or
    /// <see cref="MktCap"/> across rows</b>: the six measured rows spanned USD, EUR, MXN and CAD.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>The SEC Central Index Key, <b>populated only on the primary listing</b> — null on 5 of 6 measured
    /// rows. Use <see cref="Endpoints.SearchEndpoints.FindByCikAsync"/> for the reverse direction.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The ISIN, identical across every listing — it identifies the security, not the venue.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The CUSIP, identical across every listing, for the same reason as <see cref="Isin"/>.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The exchange's <b>display name</b> — <c>"NASDAQ Global Select"</c>, <c>"Deutsche Börse"</c>.
    ///
    /// <para><b>This is the inverse of <see cref="CompanyProfile.Exchange"/>, which holds the short code under the
    /// identical field name.</b> The code is in <see cref="ExchangeShortName"/> on this type. A caller who filters
    /// on <c>Exchange == "NASDAQ"</c> here matches nothing and is told nothing.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The exchange's <b>short code</b> — <c>NASDAQ</c>, <c>XETRA</c>, <c>MEX</c>. This is the value that
    /// matches <see cref="ExchangeInfo.Exchange"/> and <see cref="CompanyProfile.Exchange"/>, and the one to pass
    /// to <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/>.</summary>
    [JsonPropertyName("exchangeShortName")] public string? ExchangeShortName { get; init; }

    /// <summary>The industry label, matching <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The company's website.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The company description.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The chief executive's name as FMP records it.</summary>
    [JsonPropertyName("ceo")] public string? Ceo { get; init; }

    /// <summary>The sector label, matching <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync"/>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The company's country as an ISO alpha-2 code — the company's, not the listing's: every AAPL row
    /// reads <c>US</c> including the Frankfurt and Mexico listings.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>Headcount. <b>Arrives as a quoted string</b> — <c>"164000"</c> — and binds only because
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = AllowReadingFromString</c>.</summary>
    [JsonPropertyName("fullTimeEmployees")] public int? FullTimeEmployees { get; init; }

    /// <summary>The company's telephone number as free text.</summary>
    [JsonPropertyName("phone")] public string? Phone { get; init; }

    /// <summary>Street address of the company's headquarters.</summary>
    [JsonPropertyName("address")] public string? Address { get; init; }

    /// <summary>City of the company's headquarters.</summary>
    [JsonPropertyName("city")] public string? City { get; init; }

    /// <summary>State or region of the company's headquarters.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>Postal code of the company's headquarters.</summary>
    [JsonPropertyName("zip")] public string? Zip { get; init; }

    /// <summary>The gap between <see cref="Dcf"/> and a price.
    ///
    /// <para><b>Not a gap against <see cref="Price"/> on this row.</b> Measured 2026-08-27, <c>dcf + dcfDiff</c>
    /// implies 312.96 for AAPL while <see cref="Price"/> reads 313.45 — 0.49 below; for the Mexican listing it
    /// implies 5300.01 against 5330, also below. For the Frankfurt listing, <c>APC.DE</c>, it implies 267.95
    /// against a price of 266.25 — 1.70 <b>above</b>. Every row disagreed, and not in a consistent direction, so
    /// the two fields are computed against different snapshots and the row does not say which. Do not
    /// reconstruct a price from this pair.</para>
    ///
    /// <para>Null on one of the six measured rows.</para></summary>
    [JsonPropertyName("dcfDiff")] public decimal? DcfDiff { get; init; }

    /// <summary>A discounted-cash-flow valuation, in <see cref="Currency"/>.
    ///
    /// <para>The only DCF value the SDK currently surfaces: FMP's Discounted Cash Flow group is four further paths
    /// in the long tail of issue #25, and none of them is modelled. See <see cref="DcfDiff"/> for why the pair
    /// does not reconcile with <see cref="Price"/>.</para></summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>URL of the company's logo.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>The company's IPO date — the company's, not this listing's: every AAPL row reads
    /// <c>1980-12-12</c>.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>Whether <see cref="Image"/> is FMP's placeholder rather than a real logo.</summary>
    [JsonPropertyName("defaultImage")] public bool? DefaultImage { get; init; }

    /// <summary>Whether this security is an exchange-traded fund.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>Whether this listing is actively trading. <b>True on the row whose <see cref="Price"/> is
    /// null</b>, so it is not a proxy for "has a price".</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }

    /// <summary>Whether this listing is an American Depositary Receipt.</summary>
    [JsonPropertyName("isAdr")] public bool? IsAdr { get; init; }

    /// <summary>Whether this security is a fund.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }
}
