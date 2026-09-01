using System.Collections;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The rows one <c>stable/earnings-calendar</c> call returned, plus the evidence needed to judge whether
/// they are all of them.
///
/// <para>This is the list — it implements <see cref="IReadOnlyList{T}"/> and
/// <see cref="Endpoints.CalendarEndpoints.GetEarningsCalendarAsync(LocalDate, LocalDate, bool, bool, CancellationToken)"/>
/// declares that as its return type, so nothing about the ordinary path changes. It exists because
/// <c>stable/earnings-calendar</c> truncates silently, and a bare list of rows cannot tell a caller whether it is
/// looking at an answer or at the first 4000 rows of one. Two ways to read the signal:</para>
///
/// <code>
/// var rows = await calendar.GetEarningsCalendarAsync(from, to);
/// if (EarningsCalendarResult.IsLikelyTruncated(rows)) { /* narrow the range and retry */ }
/// // or, for the detail of which tell fired:
/// if (rows is EarningsCalendarResult { AtRowCap: true }) { ... }
/// </code>
///
/// <para><b>Everything here is measured on the raw response, before the SDK clamps or drops anything.</b> That
/// ordering is the whole point and it is not a detail: the consumer this SDK replaces clamps first and then tests
/// <c>rows.Count &gt;= 4000</c>, so a genuinely truncated response whose overshoot rows the clamp removed arrives
/// at its detector at 3999 and is judged complete. <see cref="Count"/> is what the caller was handed;
/// <see cref="RowsReturned"/> is what FMP sent, and only the second can answer the question.</para></summary>
public sealed class EarningsCalendarResult : IReadOnlyList<EarningsCalendarEntry>
{
    /// <summary>FMP's undocumented hard cap on one <c>stable/earnings-calendar</c> response.
    ///
    /// <para>Measured 2026-08-26: <c>from=2026-05-13&amp;to=2026-05-13</c> answers 2039 rows, while
    /// <c>from=2026-05-13&amp;to=2026-05-14</c> answers <b>exactly 4000</b> of which only 1969 fall on 05-13 — so
    /// 70 rows of a day that came back complete on its own vanish, and the truncation does not respect day
    /// boundaries. <c>limit=6000</c> was accepted and ignored: still exactly 4000.</para>
    ///
    /// <para><b>The cap is real, but it is escapable, and this type was built believing it was not.</b> Measured
    /// 2026-09-01 (#46): <c>page</c> is honoured on this path even though <c>limit</c> is not, so hitting
    /// <see cref="RowCap"/> means "there is another page", not "rows are gone". <c>from=2026-05-13&amp;to=2026-05-19</c>
    /// answers 4000 on page 0 and 2497 more on page 1, disjoint by <c>(date, symbol)</c>, with page 1 carrying the
    /// 2038 rows of 05-13 that page 0 omits entirely. Until
    /// <see cref="Endpoints.CalendarEndpoints.GetEarningsCalendarAsync"/> sends <c>page</c>, the detectors on this
    /// type stay exactly as useful as they were — a capped response still means data the caller has not been given.
    /// What changes is the remedy: narrowing the range is no longer the only one. See #49.</para></summary>
    public const int RowCap = 4000;

    private readonly IReadOnlyList<EarningsCalendarEntry> _rows;

    internal EarningsCalendarResult(
        IReadOnlyList<EarningsCalendarEntry> rows,
        int rowsReturned,
        LocalDate requestedFrom,
        LocalDate requestedTo,
        LocalDate? earliestReturnedDate)
    {
        _rows = rows;
        RowsReturned = rowsReturned;
        RequestedFrom = requestedFrom;
        RequestedTo = requestedTo;
        EarliestReturnedDate = earliestReturnedDate;
    }

    /// <summary>How many rows the caller is holding, after any clamping and after rows with no usable date were
    /// dropped. Compare against <see cref="RowsReturned"/> to see what the SDK removed.</summary>
    public int Count => _rows.Count;

    /// <inheritdoc/>
    public EarningsCalendarEntry this[int index] => _rows[index];

    /// <inheritdoc/>
    public IEnumerator<EarningsCalendarEntry> GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>How many rows FMP's response actually carried, counted before the SDK clamped or dropped anything.
    /// This, and not <see cref="Count"/>, is what the truncation tells are computed from.</summary>
    public int RowsReturned { get; }

    /// <summary>The <c>from</c> that was asked for.</summary>
    public LocalDate RequestedFrom { get; }

    /// <summary>The <c>to</c> that was asked for.</summary>
    public LocalDate RequestedTo { get; }

    /// <summary>The earliest <see cref="EarningsCalendarEntry.Date"/> anywhere in the raw response, or
    /// <see langword="null"/> if it carried no dated row. Raw, so clamping cannot move it.</summary>
    public LocalDate? EarliestReturnedDate { get; }

    /// <summary>The response came back at or above <see cref="RowCap"/>, so it is almost certainly cut short.
    ///
    /// <para>Exact at the cap and blind just under it: a 7-day peak-season window (2026-08-01 to 08-07) was
    /// measured at 3676 rows — 92% of the cap without crossing it — so a false reading here is "complete" and never
    /// "truncated". <see cref="MissesStartOfRange"/> is the tell that covers the near-cap case.</para></summary>
    public bool AtRowCap => RowsReturned >= RowCap;

    /// <summary>Nothing came back for the first day of the requested range, although later days did.
    ///
    /// <para>The second truncation tell, and it catches what <see cref="AtRowCap"/> cannot. FMP drops rows
    /// <b>from the front</b> of the range when it truncates: measured 2026-08-26,
    /// <c>from=2026-05-13&amp;to=2026-05-19</c> returned 4000 rows containing <b>no 2026-05-13 row at all</b>, even
    /// though that day answers 2039 rows when asked for on its own. An entire requested day silently absent is not
    /// something a row count can see.</para>
    ///
    /// <para><b>Known false positive:</b> a range whose first day is a weekend or a market holiday legitimately has
    /// nothing on it, and this reads <see langword="true"/> anyway. That is the deliberate direction to be wrong
    /// in — a caller re-requesting a narrower range that was fine loses a request, whereas the opposite loses
    /// rows.</para></summary>
    public bool MissesStartOfRange => EarliestReturnedDate is { } earliest && earliest > RequestedFrom;

    /// <summary>Either tell fired, so treat these rows as incomplete and narrow the range.
    ///
    /// <para><b>Day-at-a-time is the only chunk width measured to be safe.</b> Nothing narrower than a day can be
    /// asked for, and every wider window measured either truncated or came close: a 31-day window in a heavy month
    /// returned exactly 4000; a 7-day peak-season window returned 3676; an unchunked 15-month request returned
    /// <b>7 rows</b>, a reduction the row cap alone does not explain. Density ranges from about 60 rows a day in a
    /// quiet month to about 525 in a peak week, so a chunk width cannot be chosen from the calendar alone — this
    /// signal is the only thing that actually proves a given response complete.</para></summary>
    public bool LikelyTruncated => AtRowCap || MissesStartOfRange;

    /// <summary>Whether a calendar result should be treated as cut short, for callers holding it as a plain
    /// <see cref="IReadOnlyList{T}"/>.
    ///
    /// <para>Exact when handed a list this SDK produced, because it reads <see cref="RowsReturned"/> and
    /// <see cref="EarliestReturnedDate"/> from the raw response. Handed any other list — a test double, a
    /// concatenation of several days, a list a caller has already filtered — it can only fall back to
    /// <c>Count &gt;= <see cref="RowCap"/></c>, which is why the fallback is documented rather than hidden:
    /// concatenating chunks discards the per-response evidence that made the check exact.</para></summary>
    /// <param name="rows">The rows to judge.</param>
    public static bool IsLikelyTruncated(IReadOnlyList<EarningsCalendarEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows is EarningsCalendarResult result ? result.LikelyTruncated : rows.Count >= RowCap;
    }
}
