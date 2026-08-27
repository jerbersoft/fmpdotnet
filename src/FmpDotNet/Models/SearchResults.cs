using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/search-symbol</c> or <c>stable/search-name</c>.
///
/// <para>The two endpoints return an identical five-field shape and share this type — one searches the ticker,
/// the other the company name, and both answer with the same row. Measured 2026-08-27: <c>query=AAPL</c> matched
/// 7 listings and <c>query=Apple</c> matched 37.</para>
///
/// <para><b>A match is a listing, not a company.</b> Apple appears once per exchange it trades on, each with its
/// own symbol and currency. Taking the first row picks a listing arbitrarily.</para></summary>
public sealed record SymbolSearchResult
{
    /// <summary>The ticker as FMP spells it, exchange suffix included — <c>AAPL</c>, <c>APC.F</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>name</c> on this endpoint pair.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The currency this listing trades in — <c>USD</c> for <c>AAPL</c>, <c>EUR</c> for <c>APC.F</c>.
    /// Present here, and notably absent from <see cref="CusipSearchResult"/> and
    /// <see cref="IsinSearchResult"/>.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>The exchange's display name — <c>NASDAQ Global Select</c>.</summary>
    [JsonPropertyName("exchangeFullName")] public string? ExchangeFullName { get; init; }

    /// <summary>The exchange's short code — <c>NASDAQ</c>, <c>FSX</c>. The value
    /// <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/> expects, and the vocabulary
    /// <see cref="Endpoints.DirectoryEndpoints.GetExchangesAsync"/> publishes.
    ///
    /// <para><b>The code, not the display name.</b> <see cref="ExchangeVariant.Exchange"/> is the other way
    /// round — same field name, opposite meaning, on an endpoint in the same group.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
}

/// <summary>One match from <c>stable/search-cik</c> — the SEC Central Index Key resolved to the listings it
/// covers.
///
/// <para>Measured 2026-08-27: <c>0000320193</c> answered a single row. <b>The query accepts either form</b> —
/// padded or bare — and the response always carries the ten-character padded one.</para></summary>
public sealed record CikSearchResult
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c> on this endpoint — <b>not</b> <c>name</c>, which is
    /// what <see cref="SymbolSearchResult.Name"/> binds on its siblings.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The Central Index Key, zero-padded to ten characters regardless of how it was asked for. Matches
    /// <see cref="CikEntry.Cik"/> exactly, so the two round-trip.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The exchange's display name — <c>NASDAQ Global Select</c>.</summary>
    [JsonPropertyName("exchangeFullName")] public string? ExchangeFullName { get; init; }

    /// <summary>The exchange's short code — <c>NASDAQ</c>. The code, as on
    /// <see cref="SymbolSearchResult.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The currency this listing trades in.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

/// <summary>One match from <c>stable/search-cusip</c> — a CUSIP resolved to the listings that carry it.
///
/// <para>Measured 2026-08-27: <c>037833100</c> answered 4 rows, because one CUSIP spans a security's listings.
/// <b>This endpoint ignores <c>limit</c></b> (4 rows asked down to 1 still answered 4), which is why
/// <see cref="Endpoints.SearchEndpoints.FindByCusipAsync"/> offers no such parameter.</para>
///
/// <para>Separate from <see cref="IsinSearchResult"/> rather than shared with it: the shapes are otherwise
/// identical, but a CUSIP and an ISIN are different facts and one shared type would carry a permanently-null
/// field on every row.</para></summary>
public sealed record CusipSearchResult
{
    /// <summary>The ticker as FMP spells it — <c>AAPL.MX</c>, <c>AAPL</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name.
    ///
    /// <para><b>The wire key is <c>companyName</c> here and <c>name</c> on <see cref="IsinSearchResult"/></b>, for
    /// the identical fact on two sibling endpoints. Both models call it <c>CompanyName</c> so a caller never has
    /// to learn which endpoint spells it which way — the same treatment
    /// <see cref="CompanySymbol.Name"/> gives the two symbol directories.</para></summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The CUSIP, echoed back — nine characters.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The listing's market capitalisation.
    ///
    /// <para><b>Denominated in the listing's local currency, and nothing on this row says which.</b> Measured
    /// 2026-08-27, <c>037833100</c> answered <c>AAPL.MX</c> at 78,694,853,448,000 — MXN, confirmed against that
    /// symbol's profile — alongside <c>AAPL</c> at 4,537,071,141,960 in USD. This shape carries no
    /// <c>currency</c> field and no <c>exchange</c> field, unlike <see cref="SymbolSearchResult"/>, so
    /// <b>ordering these rows by market capitalisation ranks currencies rather than companies</b> and the
    /// Mexican listing sorts seventeen times above the American one.</para>
    ///
    /// <para>To compare across listings, resolve each symbol through
    /// <see cref="Endpoints.CompanyEndpoints.GetProfileAsync"/> and read
    /// <see cref="CompanyProfile.Currency"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}

/// <summary>One match from <c>stable/search-isin</c> — an ISIN resolved to the listings that carry it.
///
/// <para>Measured 2026-08-27: <c>US0378331005</c> answered 5 rows, one of them with a market capitalisation of
/// zero. <b>This endpoint ignores <c>limit</c></b>, as <see cref="CusipSearchResult"/> notes of its
/// sibling.</para></summary>
public sealed record IsinSearchResult
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name.
    ///
    /// <para><b>The wire key is <c>name</c> here and <c>companyName</c> on
    /// <see cref="CusipSearchResult"/></b> — see that property. The C# name is deliberately the same on both.</para></summary>
    [JsonPropertyName("name")] public string? CompanyName { get; init; }

    /// <summary>The ISIN, echoed back — twelve characters.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The listing's market capitalisation, in the listing's local currency, unlabelled — see
    /// <see cref="CusipSearchResult.MarketCap"/> for the full account and the measured example. One of the five
    /// measured rows (<c>AAPL.DE</c>) reported zero rather than null.</summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}
