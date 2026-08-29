using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of the three movers lists — <c>stable/biggest-gainers</c>,
/// <c>stable/biggest-losers</c> and <c>stable/most-actives</c>.
///
/// <para>The three share one shape exactly. Measured 2026-08-29, each answered <b>50 rows</b> carrying the
/// same six keys, and the lists overlap: 8 symbols were in both gainers and most-actives, 1 in both losers and
/// most-actives, 0 in both gainers and losers.</para>
///
/// <para><b>No row carries a date.</b> The lists describe a session and never name it. Cross-checked
/// 2026-08-29 (a Saturday), <c>FNGR</c> read <c>price 0.398, change 0.2246, changesPercentage 129.5271</c>
/// here, and <c>stable/quote?symbol=FNGR</c> returned those three values <b>identically</b> with
/// <c>timestamp 1787947201</c> — <c>2026-08-28 20:00:01Z</c>, Friday's close. So the lists are the last
/// completed session, and <see cref="Quote"/> is where a caller learns which one that was.</para>
///
/// <para><b><c>most-actives</c> carries no volume</b>, measured the same day — the quantity that defines the
/// ranking is not in the response. <see cref="Quote.Volume"/> has it.</para></summary>
public sealed record MarketMover
{
    /// <summary>The ticker. Nullable because the deserialiser cannot promise a key is present, not because any
    /// measured row omitted it — no null appeared in any field across 9,855 rows measured 2026-08-29.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>name</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The last price. See <see cref="Symbol"/> for why it is nullable.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The absolute change over the session.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>The percentage change over the session.
    ///
    /// <para><b>The wire spells this <c>changesPercentage</c> — with an S — which is a third spelling of one
    /// concept in this API.</b> <see cref="Quote.ChangePercentage"/> binds <c>changePercentage</c> and
    /// <see cref="EndOfDayBar.ChangePercent"/> binds <c>changePercent</c>. The property carries the house
    /// name so the three read alike in C#; the attribute carries the wire verbatim, under the same rule that
    /// binds <c>senateID</c> to <c>SenateId</c>. <b>Do not "fix" the attribute</b> — the property would then
    /// bind nothing, silently.</para></summary>
    [JsonPropertyName("changesPercentage")] public decimal? ChangePercentage { get; init; }

    /// <summary>The exchange the symbol trades on. Present on every measured row; the movers lists span all
    /// exchanges at once, unlike the sector and industry paths, which answer for one exchange at a
    /// time.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
}
