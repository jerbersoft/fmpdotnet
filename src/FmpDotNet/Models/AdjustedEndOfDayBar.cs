using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One daily bar from either of FMP's two <c>adj*</c> price endpoints —
/// <c>stable/historical-price-eod/non-split-adjusted</c> and <c>stable/historical-price-eod/dividend-adjusted</c>.
///
/// <para><b>These two endpoints are shape-identical and mean completely different things, and the payload cannot
/// tell them apart.</b> That is the entire reason this type is documented at length rather than being four
/// properties and a date. Measured 2026-08-27 for AAPL on 2020-08-28 — the last session before its four-for-one
/// split took effect on 2020-08-31 — all three daily endpoints answered the same session like this:</para>
/// <list type="table">
///   <listheader><term>endpoint</term><description>open / close / volume</description></listheader>
///   <item><term><c>non-split-adjusted</c></term><description>504.04 / 499.24 /  46,907,500</description></item>
///   <item><term><c>full</c></term><description>126.01 / 124.81 / 187,630,000</description></item>
///   <item><term><c>dividend-adjusted</c></term><description>122.12 / 120.96 / 187,630,000</description></item>
/// </list>
/// <para>499.24 is exactly four times 124.81, and 187,630,000 is exactly four times 46,907,500.</para>
///
/// <para><b>So <c>non-split-adjusted</c> returns raw, as-traded prices, and its <c>adj</c> field names are simply
/// wrong.</b> The path parses as <i>non-(split-adjusted)</i> — "not adjusted for splits" — rather than "adjusted
/// for non-splits", which is how almost everyone reads it first. The SDK therefore exposes it as
/// <see cref="Endpoints.ChartEndpoints.GetUnadjustedAsync"/>, named for what it returns, and this record's
/// property names keep FMP's <c>Adj</c> prefix only because renaming them would hide which wire field each one
/// came from.</para>
///
/// <para><b>Only the method you called tells you which adjustment you have.</b> There is no field on the row that
/// distinguishes them, no flag, and no difference in shape; a bar sitting in a variable is ambiguous. A caller
/// who mixes the two series — or who caches them under one key — gets a price history with a silent four-fold
/// discontinuity in it, on a date that looks like an ordinary session. If these are stored, store which endpoint
/// produced them.</para>
///
/// <para>Rows arrive <b>newest first</b>, and the SDK does not re-sort them.</para></summary>
public sealed record AdjustedEndOfDayBar
{
    /// <summary>The symbol as FMP spells it. Present on every row measured.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The session date, <c>"2020-08-28"</c> on the wire — a trading day, not a moment.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The session's opening price, under whichever adjustment the endpoint you called applies. Raw
    /// as-traded from <see cref="Endpoints.ChartEndpoints.GetUnadjustedAsync"/>; split- and dividend-adjusted from
    /// <see cref="Endpoints.ChartEndpoints.GetDividendAdjustedAsync"/>.</summary>
    [JsonPropertyName("adjOpen")] public decimal? AdjOpen { get; init; }

    /// <summary>The session's high, under the same adjustment as <see cref="AdjOpen"/>.</summary>
    [JsonPropertyName("adjHigh")] public decimal? AdjHigh { get; init; }

    /// <summary>The session's low, under the same adjustment as <see cref="AdjOpen"/>.</summary>
    [JsonPropertyName("adjLow")] public decimal? AdjLow { get; init; }

    /// <summary>The session's close, under the same adjustment as <see cref="AdjOpen"/>.</summary>
    [JsonPropertyName("adjClose")] public decimal? AdjClose { get; init; }

    /// <summary>Shares traded in the session — and <b>this is adjusted too</b>, which is easy to miss because the
    /// name carries no <c>adj</c> prefix. AAPL's 2020-08-28 session reads 46,907,500 from the unadjusted endpoint
    /// and 187,630,000 from the dividend-adjusted one: exactly four times, the same split factor applied to the
    /// prices. Volume from the two endpoints is not comparable.</summary>
    [JsonPropertyName("volume")] public long? Volume { get; init; }
}
