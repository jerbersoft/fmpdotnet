using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/company-screener</c>: a security that matched, with the values it was matched on.
///
/// <para>Measured on 2026-08-26: these fifteen properties are the entire row, present on every one of a captured
/// 1,000, with none missing and none extra. Every filter on <see cref="ScreenerCriteria"/> screens on one of the
/// fields below, so a result can always be checked against the criteria that produced it.</para>
///
/// <para>Rows arrive ordered by <see cref="MarketCap"/> descending, largest first, on every combination of filters
/// measured. Nothing in the API lets a caller change that, which is what makes
/// <see cref="ScreenerCriteria.Limit"/> a "top N by market cap" control rather than an arbitrary
/// sample.</para></summary>
public sealed record ScreenerResult
{
    /// <summary>Ticker as FMP spells it, exchange suffix included on non-US listings.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>The company name, spelled <c>companyName</c> as on <c>stock-list</c>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>Market capitalisation, and the sort key for the whole response — see the note on the type.
    ///
    /// <para><see langword="decimal"/> to match <see cref="CompanyProfile.MarketCap"/>, which had to widen from
    /// <see langword="long"/> when <c>stable/profile</c> was measured serving <c>GOOG</c> as
    /// <c>4098415617064.9995</c> on 2026-08-27. <b>The screener itself was measured the same minute and rounds:
    /// it answered <c>4098415617065</c> for that company, and no fractional value has ever been observed
    /// here.</b> So this widening is a precaution rather than a fix — the two fields are the same quantity from
    /// the same upstream, the rounding is undocumented and can stop, and a <c>long?</c> would abort the whole
    /// screener response on the first row that stops being rounded.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }

    /// <summary>Sector label, spelled as
    /// <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync(CancellationToken)"/> returns it. That endpoint is
    /// the place to get a valid value for <see cref="ScreenerCriteria.Sector"/> from, because an invalid one is
    /// not rejected — it comes back as an empty result.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>Industry label, spelled as
    /// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync(CancellationToken)"/> returns it. Same warning
    /// as <see cref="Sector"/>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>Beta against the broad market. Can be negative — <c>CITI.TO</c> measured <c>-0.420648</c>.
    ///
    /// <para><b>Zero appears to mean "not computed" rather than "moves independently of the market".</b> Measured,
    /// <c>betaLowerThan=0</c> returns rows sitting at exactly <c>0</c> alongside genuinely negative ones, and the
    /// zeros are ETNs and preferred shares — instruments a beta is not usually fitted for. Treating one as a
    /// measured zero puts it in a portfolio calculation as market-neutral. The same shape as
    /// <see cref="SharesFloat.FreeFloat"/>'s zero for ETFs.</para></summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Last price.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Dividend paid over the last year, per share, in the listing currency — an amount, not a
    /// yield.</summary>
    [JsonPropertyName("lastAnnualDividend")] public decimal? LastAnnualDividend { get; init; }

    /// <summary>Share volume, as <see langword="decimal"/> rather than an integer type. Measured 2026-08-27: a
    /// live <c>company-screener</c> sweep returned a <c>volume</c> at <c>$[0].volume</c> that would not convert to
    /// an integer, which failed the whole response. The literal value was not captured, and a follow-up request
    /// the same day returned only plain integers — so the non-integer shape is real but not reliably
    /// reproducible. Every other volume field in this SDK that has met the same problem is already
    /// <see langword="decimal"/>? — <see cref="Quote.Volume"/>, <see cref="IntradayBar.Volume"/>,
    /// <see cref="AftermarketQuote.Volume"/>, <see cref="BulkCompanyProfile.Volume"/> and
    /// <see cref="ShortQuote.Volume"/>.</summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }

    /// <summary>The exchange's <b>long</b> name — <c>NASDAQ Global Select</c>, <c>New York Stock Exchange</c>.
    ///
    /// <para><b>This value cannot be fed back into <see cref="ScreenerCriteria.Exchange"/>.</b> That filter takes
    /// the short code and nothing else: measured 2026-08-26, <c>exchange=NASDAQ</c> matched while
    /// <c>exchange=NASDAQ Global Select</c> answered an empty list with HTTP 200. Round-tripping this field into a
    /// follow-up query is therefore a query that silently matches nothing.
    /// <see cref="ExchangeShortName"/> is the one to send.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The exchange's short code — <c>NASDAQ</c>, <c>NYSE</c>, <c>AMEX</c>, <c>TSX</c>. This is the value
    /// <see cref="ScreenerCriteria.Exchange"/> expects, and the same spelling
    /// <see cref="DelistedCompany.Exchange"/> carries.</summary>
    [JsonPropertyName("exchangeShortName")] public string? ExchangeShortName { get; init; }

    /// <summary>Two-letter country code for the <b>company</b>, not for where it is listed. Measured,
    /// <c>country=CA</c> returns Canadian companies listed in Buenos Aires, Hong Kong and London as well as
    /// Toronto — so this does not narrow a query to one market. Use <see cref="ExchangeShortName"/> for
    /// that.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>Whether FMP classifies this as an ETF. Disjoint from <see cref="IsFund"/>: every measured
    /// <c>isFund=true</c> row carried <c>isEtf=false</c>.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>Whether FMP classifies this as a fund — mutual funds and money-market funds, as distinct from the
    /// exchange-traded ones under <see cref="IsEtf"/>.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }

    /// <summary>Whether FMP considers the security actively trading. The same judgement that decides membership of
    /// <see cref="Endpoints.DirectoryEndpoints.GetActivelyTradingAsync(CancellationToken)"/>, available here
    /// per-row and filterable.</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }
}
