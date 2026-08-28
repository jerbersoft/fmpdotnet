using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One analyst rating action on one symbol, from <c>stable/grades</c>.
///
/// <para><b>An action is not necessarily a change.</b> <see cref="Action"/> was <c>maintain</c>,
/// <c>upgrade</c> or <c>downgrade</c> across 1,791 rows measured 2026-08-28, and on a <c>maintain</c> the
/// previous and new grades are identical — two of five rows in the captured page. A caller looking for rating
/// changes filters on <see cref="Action"/>, not by comparing the two grade fields.</para>
///
/// <para><b>The vocabulary is not one scale.</b> <see cref="NewGrade"/> took <b>20 distinct values</b> across
/// those 1,791 rows — <c>Buy</c>, <c>Outperform</c>, <c>Overweight</c>, <c>Neutral</c>, <c>Hold</c>,
/// <c>Market Perform</c>, <c>Equal Weight</c>, <c>Underweight</c> and more — because each house uses its own
/// words. Mapping them onto a common ladder is a judgement the SDK does not make for you.</para></summary>
public sealed record StockGrade
{
    /// <summary>The symbol the action was taken on.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When the action was published. Rows arrive newest first.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The house that published it — <c>"Rothschild &amp; Co"</c>, <c>"Jefferies"</c>,
    /// <c>"Goldman Sachs"</c>. Free text, and not a stable identifier.</summary>
    [JsonPropertyName("gradingCompany")] public string? GradingCompany { get; init; }

    /// <summary>The grade before this action, in that house's own vocabulary. Equal to
    /// <see cref="NewGrade"/> whenever <see cref="Action"/> is <c>maintain</c>.</summary>
    [JsonPropertyName("previousGrade")] public string? PreviousGrade { get; init; }

    /// <summary>The grade after this action. See the type's remarks: 20 distinct values across one symbol's
    /// history, drawn from each house's own scale, which is why this is a string and not an enum.</summary>
    [JsonPropertyName("newGrade")] public string? NewGrade { get; init; }

    /// <summary>What the house did: <c>maintain</c>, <c>downgrade</c> or <c>upgrade</c> across 1,791 rows
    /// measured 2026-08-28.
    ///
    /// <para><b>Lower case, while the grades beside it are title case.</b> The token is kept exactly as sent —
    /// a caller matching on it should fold case itself, since the SDK does not normalise what it was
    /// given.</para></summary>
    [JsonPropertyName("action")] public string? Action { get; init; }
}

/// <summary>The current spread of analyst opinion on one symbol, from <c>stable/grades-consensus</c>.
///
/// <para><b>This is not the newest row of <see cref="GradeHistory"/>, although it looks like it could be.</b>
/// Both carry five analyst counts, and a caller could reasonably read one as a live view of the other. Measured
/// for AAPL the same minute on 2026-08-28:</para>
///
/// <code>
/// grades-historical row 0  2026-08-01  strongBuy 6  buy 22  hold 14  sell 3  strongSell 2   total  47
/// grades-consensus         (no date)   strongBuy 1  buy 70  hold 32  sell 9  strongSell 0   total 112
/// </code>
///
/// <para>The totals differ by more than a factor of two and the distributions are differently shaped, so these
/// are different populations rather than one being stale. They stay separate records for that reason, and
/// nothing in this SDK merges or reconciles them.</para>
///
/// <para><b>There is no date on this record</b>, because the endpoint sends none — so there is no way to tell
/// how current a consensus is.</para></summary>
public sealed record GradeConsensus
{
    /// <summary>The symbol the consensus is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Analysts at the strongest buy rating.</summary>
    [JsonPropertyName("strongBuy")] public int? StrongBuy { get; init; }

    /// <summary>Analysts at a buy rating. The largest bucket for AAPL at 70 of 112.</summary>
    [JsonPropertyName("buy")] public int? Buy { get; init; }

    /// <summary>Analysts at a hold rating.</summary>
    [JsonPropertyName("hold")] public int? Hold { get; init; }

    /// <summary>Analysts at a sell rating.</summary>
    [JsonPropertyName("sell")] public int? Sell { get; init; }

    /// <summary>Analysts at the strongest sell rating. <b>Zero is a measured value here, not an absence</b> —
    /// AAPL answered 0 on 2026-08-28.</summary>
    [JsonPropertyName("strongSell")] public int? StrongSell { get; init; }

    /// <summary>FMP's own one-word summary of the five counts — <c>"Buy"</c> for AAPL. A string, because the
    /// observed set is one value from one symbol and an enum built on it would be a guess.</summary>
    [JsonPropertyName("consensus")] public string? Consensus { get; init; }
}

/// <summary>One month's snapshot of how analysts were rating a symbol, from <c>stable/grades-historical</c>.
///
/// <para>Rows are monthly and newest first, dated the first of the month — 92 of them for AAPL measured
/// 2026-08-28, back to 2018. The five counts are named as FMP names them, <c>analystRatings*</c>, and they are
/// <b>not</b> the same five as <see cref="GradeConsensus"/>: read the remarks on that type before treating
/// either as a view of the other.</para></summary>
public sealed record GradeHistory
{
    /// <summary>The symbol the snapshot is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The month the snapshot covers, dated its first day.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Analysts at the strongest buy rating that month.</summary>
    [JsonPropertyName("analystRatingsStrongBuy")] public int? AnalystRatingsStrongBuy { get; init; }

    /// <summary>Analysts at a buy rating that month.</summary>
    [JsonPropertyName("analystRatingsBuy")] public int? AnalystRatingsBuy { get; init; }

    /// <summary>Analysts at a hold rating that month.</summary>
    [JsonPropertyName("analystRatingsHold")] public int? AnalystRatingsHold { get; init; }

    /// <summary>Analysts at a sell rating that month.</summary>
    [JsonPropertyName("analystRatingsSell")] public int? AnalystRatingsSell { get; init; }

    /// <summary>Analysts at the strongest sell rating that month.</summary>
    [JsonPropertyName("analystRatingsStrongSell")] public int? AnalystRatingsStrongSell { get; init; }
}
