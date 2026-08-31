using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's own <b>unlevered</b> discounted-cash-flow valuation for one symbol, from
/// <c>stable/discounted-cash-flow</c>.
///
/// <para><b>A stored daily value, not a live calculation.</b> Measured 2026-08-31, AAPL read
/// <c>dcf = 145.66380328033068</c> against <c>Stock Price = 319.7</c>, identical to all 14 decimal places
/// across captures taken minutes apart — while <c>stable/custom-discounted-cash-flow</c> recomputed off a
/// price that moved 314.74 → 314.85 → 314.87 over the same window.</para>
///
/// <para><b>Do not reconcile this against any other price the SDK carries.</b> The two DCF families' price
/// columns disagree in <i>both</i> directions: AAPL -4.83, MSFT -2.50, XOM <b>+2.50</b>, measured 2026-08-31.
/// Five symbols captured back to back agreed on their valuations to within ±0.18 and matched exactly on
/// <b>none</b>, with the sign inconsistent (XOM +0.03 against AAPL -0.06). This replicates the finding already
/// documented on <see cref="ExchangeVariant.DcfDiff"/>, measured 2026-08-27 on a different pair of
/// paths.</para></summary>
public sealed record DcfValuation
{
    /// <summary>The ticker, uppercased by FMP. Measured 2026-08-31, <c>symbol=aapl</c> answers
    /// <c>"AAPL"</c> with values byte-identical to the uppercase call — which is why this facade has no
    /// uppercase guard, unlike the News searches.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The valuation date, ISO <c>yyyy-MM-dd</c>. The day FMP computed the stored value.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The unlevered per-share valuation.
    ///
    /// <para><b>Not comparable with <see cref="LeveredDcfValuation.Dcf"/>, and the gap is not small.</b>
    /// Measured 2026-08-27/31: KO <b>83.71</b> here against <b>49.77</b> levered — 41% — and JPM 728.00
    /// against 907.85, in the opposite direction. The two answer different valuation questions and neither
    /// is "the" DCF.</para></summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>The market price FMP compared the valuation against.
    ///
    /// <para><b>The wire name is <c>Stock Price</c> — capitalised, with a space.</b> Reproduced exactly; a
    /// <c>[JsonPropertyName("stockPrice")]</c> binds nothing and leaves this null on every row. Already
    /// documented for <c>dcf-bulk</c>'s CSV on <c>BulkDiscountedCashFlow</c>; it appears in JSON
    /// here.</para>
    ///
    /// <para><b>Do not reconstruct a price from this field.</b> See the type's summary.</para></summary>
    [JsonPropertyName("Stock Price")] public decimal? StockPrice { get; init; }
}

/// <summary>FMP's own <b>levered</b> discounted-cash-flow valuation for one symbol, from
/// <c>stable/levered-discounted-cash-flow</c>.
///
/// <para><b>Deliberately not shared with <see cref="DcfValuation"/> despite the identical field set.</b>
/// Unlevered and levered DCF answer different valuation questions, and the numbers diverge enormously —
/// measured 2026-08-27/31, KO reads 83.71 unlevered against <b>49.77</b> here, a 41% gap, and JPM 728.00
/// against 907.85 in the opposite direction. With one record a variable that has drifted from the call that
/// produced it is indistinguishable from the other model's answer; with two, passing one where the other is
/// expected does not compile. The independent Python <c>fmpsdk</c> made the same split, with the same
/// reasoning recorded on its type.</para>
///
/// <para>Everything else — the stored-daily-value behaviour, the <c>Stock Price</c> spelling, the refusal to
/// reconcile prices across paths — is as <see cref="DcfValuation"/> records it.</para></summary>
public sealed record LeveredDcfValuation
{
    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The valuation date, ISO <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The levered per-share valuation. <b>Not comparable with <see cref="DcfValuation.Dcf"/></b> —
    /// see the type's summary for the measured gap.</summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>The market price FMP compared the valuation against. Wire name <c>Stock Price</c>,
    /// capitalised and with a space — see <see cref="DcfValuation.StockPrice"/>.</summary>
    [JsonPropertyName("Stock Price")] public decimal? StockPrice { get; init; }
}
