using System.Text.Json;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One symbol's analyst price-target coverage, summarised over four windows. From
/// <c>stable/price-target-summary-bulk</c>, the whole-universe CSV download.
///
/// <para>Measured 2026-08-26: 5,277 rows, 314 kB, ten columns, and <b>not one blank field in the entire
/// response</b> — every column is always populated, which is what makes the zero-versus-absent note below the
/// thing to read before using this type.</para>
///
/// <para><b>A zero here means zero, and it cannot mean "unknown".</b> Because no field is ever blank, a symbol
/// with no analyst activity in a window arrives as <c>0</c> count and <c>0</c> average rather than as null. Those
/// two are indistinguishable in the payload, so the average is only meaningful where the matching count is above
/// zero: an <see cref="LastMonthAvgPriceTarget"/> of 0 alongside a <see cref="LastMonthCount"/> of 0 is "nobody
/// published this month", not "the analysts think it is worthless". Gate on the count, never on the
/// average.</para>
///
/// <para><b><see cref="AllTimeCount"/> was above zero on every row measured</b>, so a symbol appearing at all
/// has some coverage somewhere in its history — the empty windows are recent ones.</para></summary>
public sealed record BulkPriceTargetSummary
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>Price targets published in the last month.</summary>
    public int? LastMonthCount { get; init; }

    /// <summary>Average target across the last month. Meaningless unless <see cref="LastMonthCount"/> is above
    /// zero — see the type's remarks.</summary>
    public decimal? LastMonthAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last quarter.</summary>
    public int? LastQuarterCount { get; init; }

    /// <summary>Average target across the last quarter. Gate on <see cref="LastQuarterCount"/>.</summary>
    public decimal? LastQuarterAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last year.</summary>
    public int? LastYearCount { get; init; }

    /// <summary>Average target across the last year. Gate on <see cref="LastYearCount"/>.</summary>
    public decimal? LastYearAvgPriceTarget { get; init; }

    /// <summary>Price targets published over the whole history FMP holds.</summary>
    public int? AllTimeCount { get; init; }

    /// <summary>Average target across the whole history. Gate on <see cref="AllTimeCount"/>.</summary>
    public decimal? AllTimeAvgPriceTarget { get; init; }

    /// <summary>The publications the targets came from.
    ///
    /// <para><b>This column is a JSON array inside a CSV field</b> — the only one on the endpoint, and the reason
    /// this type does any parsing at all. On the wire it arrives CSV-quoted, so
    /// <c>["StreetInsider","Benzinga"]</c> is delivered as <c>"[""StreetInsider"",""Benzinga""]"</c>. Measured
    /// 2026-08-26 every one of the 5,277 values parsed as JSON, 874 of them as the empty array.</para>
    ///
    /// <para><b>Empty and null mean different things here, deliberately.</b> An empty list is FMP saying there
    /// are no publishers; <see langword="null"/> is this SDK saying the field could not be read. Collapsing the
    /// two would turn a format change upstream into a silent, universal "no publishers" — and throwing instead
    /// would abandon a 5,000-row stream over one malformed row.</para></summary>
    public IReadOnlyList<string>? Publishers { get; init; }

    internal static BulkPriceTargetSummary FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        LastMonthCount = row.GetInt32("lastMonthCount"),
        LastMonthAvgPriceTarget = row.GetDecimal("lastMonthAvgPriceTarget"),
        LastQuarterCount = row.GetInt32("lastQuarterCount"),
        LastQuarterAvgPriceTarget = row.GetDecimal("lastQuarterAvgPriceTarget"),
        LastYearCount = row.GetInt32("lastYearCount"),
        LastYearAvgPriceTarget = row.GetDecimal("lastYearAvgPriceTarget"),
        AllTimeCount = row.GetInt32("allTimeCount"),
        AllTimeAvgPriceTarget = row.GetDecimal("allTimeAvgPriceTarget"),
        Publishers = ParsePublishers(row.GetString("publishers")),
    };

    private static IReadOnlyList<string>? ParsePublishers(string? value)
    {
        if (value is null) return null;
        if (value.Length == 0) return [];

        try
        {
            return JsonSerializer.Deserialize(value, FmpJsonContext.Default.ListString);
        }
        catch (JsonException)
        {
            return null;   // unreadable, which is not the same as empty
        }
    }
}
