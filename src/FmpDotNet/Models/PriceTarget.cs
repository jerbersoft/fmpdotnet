using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Where the analyst price targets on one symbol sit, from <c>stable/price-target-consensus</c>.
///
/// <para>One row, five fields, all populated on the symbol measured 2026-08-28. <b>The mean can fall below the
/// median</b> — AAPL answered a consensus of 340.72 against a median of 360 — which is an ordinary left-skewed
/// distribution and not a fault. Nothing here recomputes or cross-checks the four numbers.</para>
///
/// <para>The values arrive as a mix of JSON integers and floats in the same response, so all four are
/// <see langword="decimal"/>.</para></summary>
public sealed record PriceTargetConsensus
{
    /// <summary>The symbol the targets are for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The highest published target.</summary>
    [JsonPropertyName("targetHigh")] public decimal? TargetHigh { get; init; }

    /// <summary>The lowest published target.</summary>
    [JsonPropertyName("targetLow")] public decimal? TargetLow { get; init; }

    /// <summary>The mean of the published targets. Can sit below <see cref="TargetMedian"/>.</summary>
    [JsonPropertyName("targetConsensus")] public decimal? TargetConsensus { get; init; }

    /// <summary>The median of the published targets.</summary>
    [JsonPropertyName("targetMedian")] public decimal? TargetMedian { get; init; }
}

/// <summary>Analyst price-target activity on one symbol, summarised over four windows, from
/// <c>stable/price-target-summary</c>.
///
/// <para>The same ten fields as the whole-universe <see cref="BulkPriceTargetSummary"/>. <see cref="Publishers"/>
/// converged on the same type too, a nullable read-only list of strings — see below.
/// <see cref="Symbol"/> did not: it is nullable here, against a non-nullable
/// <see cref="BulkPriceTargetSummary.Symbol"/> initialised to <c>""</c> on the bulk type, so the two records are
/// not interchangeable on that one field.</para>
///
/// <para><b>Here, "unknown" is the <see langword="null"/> this endpoint returns, and a zero inside a returned
/// row is a measured zero — the opposite of the bulk CSV path.</b> Measured 2026-08-28: an uncovered symbol
/// answers an empty array and <see cref="Endpoints.AnalystEndpoints.GetPriceTargetSummaryAsync"/> returns
/// <see langword="null"/> — checked on four, <c>MRV.TO</c>, <c>001231.SZ</c>, <c>0018.HK</c> and <c>GOODY.IS</c>,
/// all <c>[]</c>. A covered symbol sends all ten keys, and a window with no activity in it arrives as a real
/// <c>0</c>: <c>BRK-B</c> sent <see cref="LastMonthCount"/> 0 and <see cref="LastYearCount"/> 0 alongside
/// <see cref="AllTimeCount"/> 2 and <see cref="AllTimeAvgPriceTarget"/> 465.5.
/// <see cref="BulkPriceTargetSummary"/> genuinely behaves the other way — no field on that CSV can ever be
/// blank, so a zero and "unknown" collapse into the same value there — and the two paths must not be reasoned
/// about interchangeably on this point.</para>
///
/// <para><b><see cref="Publishers"/> arrives as a string containing a JSON array</b> and is parsed by
/// <see cref="PublisherListJsonConverter"/>. It is the only nested-format field in this endpoint
/// group.</para></summary>
public sealed record PriceTargetSummary
{
    /// <summary>The symbol the summary is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Price targets published in the last month.</summary>
    [JsonPropertyName("lastMonthCount")] public int? LastMonthCount { get; init; }

    /// <summary>Average target across the last month. Meaningless unless <see cref="LastMonthCount"/> is above
    /// zero — gate on the count, never on the average.</summary>
    [JsonPropertyName("lastMonthAvgPriceTarget")] public decimal? LastMonthAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last quarter.</summary>
    [JsonPropertyName("lastQuarterCount")] public int? LastQuarterCount { get; init; }

    /// <summary>Average target across the last quarter. Gate on <see cref="LastQuarterCount"/>.</summary>
    [JsonPropertyName("lastQuarterAvgPriceTarget")] public decimal? LastQuarterAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last year.</summary>
    [JsonPropertyName("lastYearCount")] public int? LastYearCount { get; init; }

    /// <summary>Average target across the last year. Gate on <see cref="LastYearCount"/>.</summary>
    [JsonPropertyName("lastYearAvgPriceTarget")] public decimal? LastYearAvgPriceTarget { get; init; }

    /// <summary>Price targets published over the whole history FMP holds.</summary>
    [JsonPropertyName("allTimeCount")] public int? AllTimeCount { get; init; }

    /// <summary>Average target across the whole history. Gate on <see cref="AllTimeCount"/>.</summary>
    [JsonPropertyName("allTimeAvgPriceTarget")] public decimal? AllTimeAvgPriceTarget { get; init; }

    /// <summary>The publications the targets came from.
    ///
    /// <para><b>On the wire this is a string containing a JSON array</b>, not an array — measured 2026-08-28,
    /// AAPL sent seven names and MSFT six, both in that form. <see cref="PublisherListJsonConverter"/> reads it,
    /// so this property is the list, matching <see cref="BulkPriceTargetSummary.Publishers"/>.</para>
    ///
    /// <para>An empty list means FMP reported no publishers; <see langword="null"/> means the field could not be
    /// read. Those are different states and are kept apart.</para></summary>
    [JsonPropertyName("publishers")]
    [JsonConverter(typeof(PublisherListJsonConverter))]
    public IReadOnlyList<string>? Publishers { get; init; }
}
