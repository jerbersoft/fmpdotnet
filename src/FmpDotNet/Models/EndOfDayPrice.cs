using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One daily close from <c>stable/historical-price-eod/light</c> — the cheapest bar FMP serves.
///
/// <para>Measured against the live API on 2026-08-27: the four properties below are the whole response, with none
/// missing and none extra across every capture taken. A seven-session window for AAPL answered 7 rows, and a
/// five-year window answered 1255.</para>
///
/// <para><b>Prices here are split-adjusted but not dividend-adjusted</b>, matching
/// <see cref="EndOfDayBar.Close"/> on the <c>full</c> endpoint. If that distinction matters — and for any total-return
/// calculation it does — see <see cref="AdjustedEndOfDayBar"/>, whose documentation carries the measured
/// comparison across AAPL's 2020 four-for-one split.</para>
///
/// <para>Rows arrive <b>newest first</b>. The SDK does not re-sort; nothing in the payload promises the order, so
/// a caller who needs it chronological should say so with an <c>OrderBy</c>.</para></summary>
public sealed record EndOfDayPrice
{
    /// <summary>The symbol as FMP spells it. Present on every row measured — unlike <see cref="IntradayBar"/>,
    /// which carries no symbol at all.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The session date. A calendar date rather than an <see cref="Instant"/>, because the wire value is
    /// <c>"2026-08-26"</c> with no time of day — a daily bar belongs to a trading day, not to a moment.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The session's closing price. Named <c>price</c> on the wire, and it is the close rather than a
    /// mid or a last-trade: measured 2026-08-26 for AAPL it read 313.45, matching <c>close</c> on the <c>full</c>
    /// endpoint for the same session exactly.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Shares traded in the session. Split-adjusted, consistently with <see cref="Price"/> — see
    /// <see cref="AdjustedEndOfDayBar.Volume"/>, where the same session's volume differs four-fold.</summary>
    [JsonPropertyName("volume")] public long? Volume { get; init; }
}
