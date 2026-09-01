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
    private readonly CalendarWalk _walk;

    internal CalendarResult(
        IReadOnlyList<T> rows,
        CalendarWalk walk,
        LocalDate requestedFrom,
        LocalDate requestedTo,
        LocalDate? earliestReturnedDate,
        int? rowCap,
        int? lookbackLimitDays)
    {
        _rows = rows;
        _walk = walk;
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

    /// <summary>How many rows FMP's responses actually carried, counted before the SDK dropped anything and
    /// summed over every page kept. This, and not <see cref="Count"/>, is what the truncation tells are
    /// computed from.</summary>
    public int RowsReturned => _walk.RowsReturned;

    /// <summary>How many pages of rows this result was assembled from. <b>1 on a path that does not page</b>,
    /// never 0. It is pages kept rather than requests spent — a walk that ends by recognising a repeated page
    /// pays for that page without counting it.
    ///
    /// <para>Only <c>dividends-calendar</c> walks among the three paths that return this type — measured
    /// 2026-09-01, a full year of dividends is 8 requests and 28,104 rows against the 4000 a single request
    /// answers. <c>splits-calendar</c> answers <c>page=1</c> with an empty array and <c>ipos-calendar</c>
    /// ignores <c>page</c> altogether, serving page 5 byte-identically to page 0, so neither is walked and
    /// both report 1 here.</para></summary>
    public int PagesFetched => _walk.PagesFetched;

    /// <summary>Rows that arrived on both sides of a page seam — <b>a count of rows lost, not of rows
    /// duplicated</b>.
    ///
    /// <para>FMP orders these responses by <c>date</c> and by nothing else, and a page seam always falls
    /// <i>inside</i> a date rather than between two — true of all 22 seams measured on 2026-09-01. So the
    /// offset that cuts page <i>n+1</i> is applied to an ordering page <i>n</i> was not cut from: some rows
    /// are served on both sides, and an equal number of different rows are served on neither.</para>
    ///
    /// <para><b>The equality is measured, not assumed.</b> Seven seams were re-requested a day at a time — a
    /// single-day request fits one page and so has no seam — and compared against what the walk held for that
    /// date: 381 duplicated / 381 missing, 174/174, 315/315, and three clean seams losing nothing. Not one row
    /// appeared in a walk that the single-day request did not have, so rows are exchanged one for one rather
    /// than invented.</para>
    ///
    /// <para><b>It over-reports rather than under-reports.</b> FMP's own data carries byte-identical duplicate
    /// rows — one page of the measured dividends year held 4000 rows and 3999 distinct ones — and such a pair
    /// straddling a seam would be counted here with no loss behind it. No such case appeared in 22 seams, and
    /// the bias runs the safe direction.</para>
    ///
    /// <para>The remedy is a narrower range: one that fits in a single page has no seam and cannot lose
    /// anything.</para></summary>
    public int SeamDuplicateRows => _walk.SeamDuplicateRows;

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

    /// <summary>The <b>last page fetched</b> came back at or above <see cref="RowCap"/>, so the walk stopped
    /// with a full page in hand and something is still behind it.
    ///
    /// <para><b>This reads the last page, not the total, and before #49 there was only ever one page for it to
    /// read.</b> A 4000-row response used to mean "rows are gone". Now it means "there is another page", and
    /// <see cref="PagesFetched"/> says whether it was fetched — so a walk that ended on a short page is not at
    /// the cap however many rows it gathered. What still fires this is a walk stopped by
    /// <c>MaxCalendarPages</c>, or by a page repeating its predecessor,
    /// with a full page as the last one appended.</para>
    ///
    /// <para>Always <see langword="false"/> where <see cref="RowCap"/> is null. Exact at the cap and blind
    /// just under it, so a false reading here is "complete" and never "truncated";
    /// <see cref="MissesStartOfRange"/> and <see cref="SeamDuplicateRows"/> are the tells that cover what it
    /// cannot see.</para></summary>
    public bool AtRowCap => RowCap is { } cap && _walk.LastPageRowCount >= cap;

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

    /// <summary>Any tell fired, so these rows are not all of them.
    ///
    /// <para><b>Three mechanisms, three tells.</b> <see cref="AtRowCap"/> catches a walk that stopped with a
    /// full page in hand. <see cref="ExceedsLookbackLimit"/> and <see cref="MissesStartOfRange"/> catch the
    /// 90-day window clamp on <c>splits-calendar</c> and <c>ipos-calendar</c>, which no row count can see.
    /// <see cref="SeamDuplicateRows"/> catches the one that only appears once a path is walked: a page seam
    /// that dropped as many rows as it duplicated.</para>
    ///
    /// <para><b>The remedy depends on which fired.</b> A window clamp is answered by moving <c>to</c>; an
    /// unstable seam by narrowing the range until it fits one page, which is the only width measured
    /// lossless. See the remarks on the method that returned this.</para></summary>
    public bool LikelyTruncated =>
        AtRowCap || ExceedsLookbackLimit || MissesStartOfRange || SeamDuplicateRows > 0;

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
