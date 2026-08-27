using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The last trade printed outside regular hours, from <c>stable/aftermarket-trade</c> or
/// <c>stable/batch-aftermarket-trade</c>.
///
/// <para>Measured against the live API on 2026-08-27: the four properties below are the whole response. AAPL
/// answered <c>{"symbol":"AAPL","price":310.58,"tradeSize":16,"timestamp":1787819647000}</c> at 04:34 ET, during
/// the pre-market session.</para>
///
/// <para>Despite the name, the endpoint covers <b>both</b> extended sessions: the measurement above is a
/// pre-market print, not an after-hours one. Read it as "outside regular hours" rather than "after the
/// close".</para>
///
/// <para>This is a single last trade, not a feed. Consecutive calls return whatever the most recent print was; the
/// endpoint carries no history and there is no way to ask it for one.</para></summary>
public sealed record AftermarketTrade
{
    /// <summary>The symbol as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The price the trade printed at.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>How many shares the trade was for. Measured at <c>16</c> for AAPL — extended-hours prints are
    /// typically small, and a single one is weak evidence of where the security is actually bid.</summary>
    [JsonPropertyName("tradeSize")] public long? TradeSize { get; init; }

    /// <summary>When the trade printed, read from a Unix epoch in <b>milliseconds</b>.
    ///
    /// <para><b>Milliseconds here, seconds on <see cref="Quote.Timestamp"/></b> — the same field name, in the same
    /// endpoint group, in different units. Measured 2026-08-27: <c>1787819647000</c>, which as milliseconds is
    /// 2026-08-27 08:34:07 UTC, or 04:34 ET. Read as seconds it is the year 58623, which at least throws;
    /// the reverse mistake does not, which is why the two converters are separate types. See
    /// <see cref="NullableEpochSecondsInstantJsonConverter"/> for the full comparison.</para></summary>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(NullableEpochMillisecondsInstantJsonConverter))]
    public Instant? Timestamp { get; init; }
}
