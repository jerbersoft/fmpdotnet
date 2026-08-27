using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One filing FMP has recently ingested, from <c>stable/latest-financial-statements</c> — the only
/// market-wide path in the Statements group.
///
/// <para><b>A three-week window, not the universe.</b> Measured 2026-08-27: 250 rows a page, <c>page</c> capped
/// at 100, so 25,250 rows are reachable in total — and page 100 was still returning filings dated 2026-08-05.
/// Everything older is simply unreachable through this path. Use it to learn what has landed since you last
/// looked, not to enumerate anything.</para>
///
/// <para><b>Keyed on <see cref="CalendarYear"/>, not fiscal year</b> — the only path in this section that is.
/// Joining these rows to the statement endpoints on "year" silently mismatches every filer whose fiscal year does
/// not end in December.</para></summary>
public sealed record LatestFinancialStatement
{
    /// <summary>Ticker as FMP spells it, including non-US suffixes — <c>300415.SZ</c> was the first row
    /// measured.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Calendar year, <b>not fiscal year</b>. See the type's summary.</summary>
    [JsonPropertyName("calendarYear")] public int? CalendarYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c>, or <c>Q1</c>–<c>Q4</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>Period end — the last day of the fiscal period the filing reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When FMP ingested the filing. The feed is sorted by this, descending.
    ///
    /// <para><b>A wall clock with no timezone, deliberately.</b> FMP sends
    /// <c>"2026-08-27 11:03:21"</c> — space-separated, no offset, not ISO-8601 with a <c>T</c> — and which zone
    /// that is has never been measured for this field. See
    /// <see cref="NullableLocalDateTimeJsonConverter"/>.</para></summary>
    [JsonPropertyName("dateAdded")]
    [JsonConverter(typeof(NullableLocalDateTimeJsonConverter))]
    public LocalDateTime? DateAdded { get; init; }
}
