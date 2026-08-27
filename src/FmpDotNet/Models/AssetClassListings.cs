using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/commodities-list</c> — 40 measured 2026-08-27, the whole set.
///
/// <para>FMP documents this under its Commodity section rather than under Directory. The SDK puts it on
/// <see cref="Endpoints.DirectoryEndpoints"/> anyway, because it answers Directory's question — what exists — and
/// because there is no <c>fmp.Commodity</c> facade for it to join: one
/// <see cref="Endpoints.QuoteEndpoints.GetQuoteAsync"/> already serves commodities alongside every other asset
/// class.</para></summary>
public sealed record CommodityInfo
{
    /// <summary>The symbol as FMP spells it — <c>GCUSD</c>, <c>ZMUSD</c>. Feed it to
    /// <see cref="Endpoints.QuoteEndpoints.GetQuoteAsync"/> or
    /// <see cref="Endpoints.ChartEndpoints.GetEndOfDayAsync"/> unchanged.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The contract's name — <c>Soybean Meal Futures</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary><b>Null on all 40 rows measured 2026-08-27.</b> A field FMP documents and never populates.
    ///
    /// <para>Kept rather than dropped so that the day it starts arriving is a visible change. The smoke suite will
    /// record it empty; that is the measured truth, not drift.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The delivery month as a three-letter abbreviation — <c>"Dec"</c>. <b>Not a date</b>: there is no
    /// year on it, and nothing in the response says which year the front month belongs to.</summary>
    [JsonPropertyName("tradeMonth")] public string? TradeMonth { get; init; }

    /// <summary>The quote currency. <b><c>USX</c> is US cents, not a misspelling of <c>USD</c></b> — both appear
    /// across the 40 rows, and a caller converting prices that treats them alike is out by a factor of 100.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

/// <summary>One row of <c>stable/cryptocurrency-list</c> — 4,793 measured 2026-08-27.
///
/// <para>Filed under Crypto in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>.</para></summary>
public sealed record CryptocurrencyInfo
{
    /// <summary>The pair symbol — <c>BTCUSD</c>, <c>MIOTAUSD</c>. Every measured row quotes against USD.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The display name — <c>IOTA USD</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Always <c>CCC</c> on every measured row — FMP's crypto aggregate, not a venue.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The ICO date, or null. Null on 33 of 4,793 rows; the other 4,760 were ISO <c>uuuu-MM-dd</c> and
    /// none was malformed.</summary>
    [JsonPropertyName("icoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IcoDate { get; init; }

    /// <summary>Coins in circulation.
    ///
    /// <para><b><see cref="decimal"/> rather than a whole-number type, and both halves of that were measured.</b>
    /// 953 of the 4,792 populated values carry a fractional part, and <c>SHIBDOGEUSD</c> reports
    /// <c>9223372036854776000</c> — past <see cref="long.MaxValue"/>. Either alone makes an integer type throw, and
    /// a <see cref="System.Text.Json.JsonException"/> here costs the entire 4,793-row response rather than one
    /// field. Nothing measured came within five orders of magnitude of <see cref="decimal"/>'s ceiling.</para></summary>
    [JsonPropertyName("circulatingSupply")] public decimal? CirculatingSupply { get; init; }

    /// <summary>The maximum supply, or null where the coin does not define one — <b>null on 1,474 of 4,793
    /// rows</b>, so absence is ordinary here rather than exceptional.
    ///
    /// <para>Same typing argument as <see cref="CirculatingSupply"/>, and this field is the more extreme of the
    /// two: <c>SHIBDOGEUSD</c> reports <c>1.8398528382123738e+23</c>, five orders of magnitude past
    /// <see cref="long.MaxValue"/> and still comfortably inside <see cref="decimal"/>.</para></summary>
    [JsonPropertyName("totalSupply")] public decimal? TotalSupply { get; init; }
}

/// <summary>One row of <c>stable/forex-list</c> — 1,551 pairs measured 2026-08-27.
///
/// <para>Filed under Forex in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>.</para></summary>
public sealed record ForexPair
{
    /// <summary>The pair symbol, base then quote with no separator — <c>EURUSD</c>, <c>ARSMXN</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The base currency's ISO code — the <c>ARS</c> of <c>ARSMXN</c>.</summary>
    [JsonPropertyName("fromCurrency")] public string? FromCurrency { get; init; }

    /// <summary>The quote currency's ISO code — the <c>MXN</c> of <c>ARSMXN</c>.</summary>
    [JsonPropertyName("toCurrency")] public string? ToCurrency { get; init; }

    /// <summary>The base currency's name — <c>Argentine Peso</c>.</summary>
    [JsonPropertyName("fromName")] public string? FromName { get; init; }

    /// <summary>The quote currency's name — <c>Mexican Peso</c>.</summary>
    [JsonPropertyName("toName")] public string? ToName { get; init; }
}

/// <summary>One row of <c>stable/index-list</c> — 425 measured 2026-08-27.
///
/// <para>Filed under Indexes in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>. Note that the rest of FMP's Indexes section is
/// <c>quote</c> and <c>historical-price-eod</c> re-documented, which <see cref="Endpoints.QuoteEndpoints"/> and
/// <see cref="Endpoints.ChartEndpoints"/> already reach; the constituent lists — S&amp;P 500, Nasdaq, Dow Jones —
/// remain unmodelled.</para></summary>
public sealed record IndexInfo
{
    /// <summary>The index symbol, carat-prefixed — <c>^GSPC</c>, <c>^TTIN</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The index name — <c>S&amp;P/TSX Capped Industrials Index</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The exchange code the index is published under — <c>TSX</c>, <c>SNP</c>. Populated on all 425
    /// measured rows, unlike <see cref="CommodityInfo.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The currency the index is denominated in — <c>CAD</c>, <c>USD</c>. Populated on all 425 rows.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}
