using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The current bid and ask outside regular hours, from <c>stable/aftermarket-quote</c> or
/// <c>stable/batch-aftermarket-quote</c>.
///
/// <para>Measured against the live API on 2026-08-27: the seven properties below are the whole response. AAPL
/// answered <c>{"symbol":"AAPL","bidSize":2,"bidPrice":310.57,"askSize":4,"askPrice":310.66,"volume":148401,
/// "timestamp":1787819647000}</c> at 04:34 ET.</para>
///
/// <para>The complement to <see cref="AftermarketTrade"/>: that says what last printed, this says where the book
/// currently stands.</para>
///
/// <para><b>The two are stamped independently, and the gap between them varies.</b> Measured 2026-08-27 for AAPL,
/// this endpoint's <see cref="Timestamp"/> ran 25 seconds behind <see cref="AftermarketTrade.Timestamp"/> in one
/// capture and 8 seconds behind in a later one — so the lag is real but not a constant to correct for. Repeated
/// calls seconds apart returned both stamps unchanged, so each is "most recent known" rather than "as of now".</para>
///
/// <para>The consequence for a caller: pairing a trade with a book gives two observations of nearby but different
/// moments, not one snapshot. In a book this thin — 4 shares bid against a regular-session volume in the tens of
/// millions — that gap is worth respecting rather than assuming away. (An earlier single probe caught the two
/// carrying the same millisecond, which is what makes this worth stating: one observation of equality was
/// coincidence, and would have been recorded as a property of the API.)</para>
///
/// <para>As with <see cref="AftermarketTrade"/>, "aftermarket" covers both extended sessions — the measurement
/// above is pre-market.</para></summary>
public sealed record AftermarketQuote
{
    /// <summary>The symbol as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Shares bid at <see cref="BidPrice"/>. Measured at <c>4</c> for AAPL — extended-hours books are
    /// thin, and the size matters as much as the price when reading one.
    ///
    /// <para><see cref="long"/>, unlike <see cref="Volume"/> beside it: across the twelve liquid symbols measured
    /// on 2026-08-27, no <see cref="BidSize"/>, <see cref="AskSize"/> or
    /// <see cref="AftermarketTrade.TradeSize"/> was ever fractional. A quoted size is a share count; an aggregated
    /// volume apparently is not.</para></summary>
    [JsonPropertyName("bidSize")] public long? BidSize { get; init; }

    /// <summary>The best bid.</summary>
    [JsonPropertyName("bidPrice")] public decimal? BidPrice { get; init; }

    /// <summary>Shares offered at <see cref="AskPrice"/>.</summary>
    [JsonPropertyName("askSize")] public long? AskSize { get; init; }

    /// <summary>The best ask.</summary>
    [JsonPropertyName("askPrice")] public decimal? AskPrice { get; init; }

    /// <summary>Shares traded in the extended session so far. Measured at 148,401 for AAPL against a regular-session
    /// volume of 33.5 million on <see cref="Quote.Volume"/> the same morning — about 0.4%, which is the useful
    /// context for how much any of these prices mean.
    ///
    /// <para><see cref="decimal"/> rather than <see cref="long"/>: measured 2026-08-27, 4 of 12 liquid symbols
    /// answered a fractional extended-hours volume (<c>AMZN</c> at <c>203057.73636</c>). The size fields beside it
    /// are not affected — see <see cref="BidSize"/>.</para></summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }

    /// <summary>When the quote was struck, read from a Unix epoch in <b>milliseconds</b> — the same unit as
    /// <see cref="AftermarketTrade.Timestamp"/> and <b>not</b> the unit <see cref="Quote.Timestamp"/> uses. See
    /// <see cref="NullableEpochSecondsInstantJsonConverter"/> for why the two are separate converters.</summary>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(NullableEpochMillisecondsInstantJsonConverter))]
    public Instant? Timestamp { get; init; }
}
