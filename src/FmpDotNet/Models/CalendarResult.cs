using System.Collections;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The rows one date-ranged calendar call returned, plus the evidence needed to judge whether they are
/// all of them.
///
/// <para>This is the list — it implements <see cref="IReadOnlyList{T}"/> and every method returning one declares
/// that as its return type, so nothing about the ordinary path changes. It exists because three of FMP's
/// calendar paths truncate silently, and a bare list of rows cannot tell a caller whether it is looking at an
/// answer or at the tail of one.</para>
///
/// <code>
/// var rows = await fmp.Calendar.GetSplitsCalendarAsync(from, to);
/// if (rows is CalendarResult&lt;StockSplit&gt; { LikelyTruncated: true }) { /* narrow the range and retry */ }
/// </code>
///
/// <para><b>Two different mechanisms, measured 2026-08-28, and they need different tells.</b></para>
///
/// <list type="bullet">
/// <item><description><c>dividends-calendar</c> caps at <b>4000 rows</b>. A request for the whole of 2025
/// answered 4000 rows whose earliest date was 2025-12-29 — the last three days of the year. <c>limit=10000</c>
/// was accepted and ignored. <see cref="RowCap"/> is 4000 and <see cref="LookbackLimitDays"/> is null: the cap
/// always fires first at 340–876 rows a day, so no window limit is observable on this path and asserting one
/// would be inventing evidence.</description></item>
/// <item><description><c>splits-calendar</c> and <c>ipos-calendar</c> clamp to a <b>90-day window measured from
/// <c>to</c></b>. Across four <c>to</c> values spanning twenty months, each with <c>from</c> fixed at
/// 2015-01-01, the earliest row returned was exactly 90 days before <c>to</c> every time. A request for the
/// whole of 2024 answered Q4 of 2024 — <b>737 and 358 rows</b>, nowhere near any cap, which is why
/// <see cref="AtRowCap"/> is blind to it. <see cref="LookbackLimitDays"/> is 90 and <see cref="RowCap"/> is
/// null.</description></item>
/// </list>
///
/// <para><b>Everything here is measured on the raw response, before the SDK clamps or drops anything.</b> That
/// ordering is the whole point and it is not a detail: clamp first and a genuinely truncated response whose
/// overshoot rows the clamp removed arrives at its detector already reduced, and is judged complete.
/// <see cref="Count"/> is what the caller was handed; <see cref="RowsReturned"/> is what FMP sent, and only the
/// second can answer the question.</para>
///
/// <para><c>stable/earnings-calendar</c> has the same defect and its own type,
/// <see cref="EarningsCalendarResult"/>, which shipped first and is deliberately left alone. Folding it into
/// this generic is public API surgery on a shipped path and is a separate decision.</para></summary>
/// <typeparam name="T">The row type the calendar path returns.</typeparam>
public sealed class CalendarResult<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _rows;

    internal CalendarResult(
        IReadOnlyList<T> rows,
        int rowsReturned,
        LocalDate requestedFrom,
        LocalDate requestedTo,
        LocalDate? earliestReturnedDate,
        int? rowCap,
        int? lookbackLimitDays)
    {
        _rows = rows;
        RowsReturned = rowsReturned;
        RequestedFrom = requestedFrom;
        RequestedTo = requestedTo;
        EarliestReturnedDate = earliestReturnedDate;
        RowCap = rowCap;
        LookbackLimitDays = lookbackLimitDays;
    }

    /// <summary>How many rows the caller is holding, after any rows with no usable date were dropped. Compare
    /// against <see cref="RowsReturned"/> to see what the SDK removed.</summary>
    public int Count => _rows.Count;

    /// <inheritdoc/>
    public T this[int index] => _rows[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>How many rows FMP's response actually carried, counted before the SDK dropped anything. This,
    /// and not <see cref="Count"/>, is what the truncation tells are computed from.</summary>
    public int RowsReturned { get; }

    /// <summary>The <c>from</c> that was asked for.</summary>
    public LocalDate RequestedFrom { get; }

    /// <summary>The <c>to</c> that was asked for.</summary>
    public LocalDate RequestedTo { get; }

    /// <summary>The earliest date anywhere in the raw response, or <see langword="null"/> if it carried no
    /// dated row. Raw, so nothing the SDK does can move it.</summary>
    public LocalDate? EarliestReturnedDate { get; }

    /// <summary>FMP's undocumented hard cap on this path's response, or <see langword="null"/> where no cap was
    /// measured. 4000 on <c>dividends-calendar</c>; null on the two window-clamped paths.</summary>
    public int? RowCap { get; }

    /// <summary>How far back this path will reach from <see cref="RequestedTo"/>, or <see langword="null"/>
    /// where no window limit was measured. 90 on <c>splits-calendar</c> and <c>ipos-calendar</c>; null on
    /// <c>dividends-calendar</c>, whose row cap always fires first.</summary>
    public int? LookbackLimitDays { get; }

    /// <summary>The response came back at or above <see cref="RowCap"/>, so it is almost certainly cut short.
    ///
    /// <para>Always <see langword="false"/> where <see cref="RowCap"/> is null. Exact at the cap and blind just
    /// under it, so a false reading here is "complete" and never "truncated";
    /// <see cref="MissesStartOfRange"/> is the tell that covers the near-cap case.</para></summary>
    public bool AtRowCap => RowCap is { } cap && RowsReturned >= cap;

    /// <summary>The requested range is wider than this path will serve, so its front was dropped.
    ///
    /// <para>Always <see langword="false"/> where <see cref="LookbackLimitDays"/> is null. Note that a span of
    /// <i>exactly</i> the limit reads <see langword="false"/> here and still loses a day: measured 2026-08-28,
    /// <c>from = to - 90</c> answered an earliest row of 2026-05-31 against a requested 2026-05-30, while
    /// <c>from = to - 88</c> was honoured exactly. <see cref="MissesStartOfRange"/> catches that
    /// boundary.</para></summary>
    public bool ExceedsLookbackLimit =>
        LookbackLimitDays is { } limit && Period.DaysBetween(RequestedFrom, RequestedTo) > limit;

    /// <summary>The earliest row returned is later than the first day asked for, although something came back.
    ///
    /// <para><b>The only tell that sees both mechanisms.</b> Both of them drop rows from the <i>front</i> of the
    /// range, and this compares what arrived against what was asked for, so it does not care which one did it.
    /// A row cap of 4000 is invisible to it only when the cap is not reached, and a 90-day clamp is invisible to
    /// a row count entirely.</para>
    ///
    /// <para><b>Known false positive:</b> a range whose first days are a weekend, a holiday or simply quiet
    /// legitimately has nothing on them, and this reads <see langword="true"/> anyway. That is the deliberate
    /// direction to be wrong in — a caller re-requesting a narrower range that was fine loses a request,
    /// whereas the opposite loses rows.</para></summary>
    public bool MissesStartOfRange => EarliestReturnedDate is { } earliest && earliest > RequestedFrom;

    /// <summary>Any tell fired, so treat these rows as incomplete and narrow the range.
    ///
    /// <para><b>Safe widths, measured 2026-08-28.</b> <c>dividends-calendar</c> ran 340–876 rows a day, so the
    /// cap falls somewhere between five and eleven days depending on the season — a six-day window returned 2147
    /// and was complete, a thirty-day window was not. <c>splits-calendar</c> and <c>ipos-calendar</c> are flat
    /// 90 days regardless of season. The SDK reports rather than chunks: see the remarks on the methods that
    /// return this type.</para></summary>
    public bool LikelyTruncated => AtRowCap || ExceedsLookbackLimit || MissesStartOfRange;

    /// <summary>Whether a calendar result should be treated as cut short, for callers holding it as a plain
    /// <see cref="IReadOnlyList{T}"/>.
    ///
    /// <para><b>Answers <see langword="false"/> for any list this SDK did not produce, and that means "no
    /// evidence" rather than "complete".</b> The per-response evidence — <see cref="RowsReturned"/> and
    /// <see cref="EarliestReturnedDate"/>, both taken raw — lives on the instance, and a test double, a
    /// concatenation of several chunks or a list a caller has already filtered has thrown it away. There is no
    /// fallback to offer: this type's cap is per-instance and null on two of the three paths that return it, so
    /// a row-count threshold here would be a number nobody measured. (<see cref="EarningsCalendarResult"/> can
    /// fall back on <c>Count &gt;= 4000</c> only because it has one known cap.) Test each chunk as it arrives,
    /// not the concatenation.</para></summary>
    /// <param name="rows">The rows to judge.</param>
    public static bool IsLikelyTruncated(IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows is CalendarResult<T> { LikelyTruncated: true };
    }
}
