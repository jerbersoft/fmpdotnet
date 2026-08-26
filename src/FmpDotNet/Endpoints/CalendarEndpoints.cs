using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Calendar</c> group — events dated on a calendar rather than tied to a fiscal period.
///
/// <para>The two endpoints here answer the same question from opposite ends: <c>stable/earnings</c> takes one
/// symbol and returns its whole earnings history, <c>stable/earnings-calendar</c> takes a date range and returns
/// every symbol in it. Their row shapes overlap exactly — seven fields, identical names — which is why
/// <see cref="EarningsCalendarEntry"/> is <see cref="EarningsReport"/> plus five optional extras.</para>
///
/// <para>Both drop rows whose <c>date</c> cannot be parsed, rather than returning them with a null date or letting
/// one bad value abort the response. Reasoned once, here, since it applies to both:</para>
///
/// <list type="bullet">
/// <item><description>On these two endpoints the date is not a field, it is half the row's identity —
/// <c>(symbol, date)</c> is the key a caller stores, deduplicates and joins on. A row with no date cannot be placed
/// on a timeline, cannot be matched to the same event arriving from another request, and in a keyed store becomes
/// either a phantom or a collision. The SDK already applies exactly this rule to the directory endpoints, where a
/// blank label is dropped because "a label is a key".</description></item>
/// <item><description>On the calendar it is also the only answer that stays consistent: a null-dated row cannot be
/// clamped to a range either, so keeping it would force a second arbitrary decision with no honest answer.</description></item>
/// <item><description>It is a defence rather than a routine. Measured 2026-08-26, all 165 rows of AAPL's full
/// history and all 48 rows of the captured calendar day carried a parseable date, so this should remove nothing;
/// <see cref="EarningsCalendarResult.RowsReturned"/> against <see cref="EarningsCalendarResult.Count"/> says how
/// much it removed if it ever does.</description></item>
/// </list></summary>
public sealed class CalendarEndpoints(FmpTransport transport)
{
    /// <summary>Earnings dates for one symbol, <b>newest first</b>, from <c>stable/earnings</c>.
    ///
    /// <para><b>The row at index 0 is normally an event that has not happened yet.</b> This is the trap on this
    /// endpoint. Measured against the live API on 2026-08-26, AAPL's head row was <c>2026-10-29</c> —
    /// two months in the future — with <c>epsActual</c> and <c>revenueActual</c> null and the estimates populated.
    /// So <c>GetEarningsAsync("AAPL", limit: 4)</c> does not return the last four reported quarters, it returns
    /// three of them and one forecast, and a caller averaging <see cref="EarningsReport.EpsActual"/> across the
    /// result silently averages N-1 values or throws on a null. Filter on
    /// <c>EpsActual is not null</c> rather than on the date: a past date with null actuals means FMP has not
    /// ingested the report yet, which is a third state, not the same as a future one.</para>
    ///
    /// <para><b><paramref name="limit"/> is a window on the newest end, and without it you get everything.</b>
    /// Measured the same day: no limit answers <b>165 rows spanning 1985-09-30 to 2026-10-29</b> — forty years of
    /// history, not a recent window. A caller that wants a handful of recent quarters should say so, and one
    /// paging a whole universe unbounded should expect the full history per symbol.</para>
    ///
    /// <para><b>There is no <c>period</c> parameter.</b> Unlike the seven period-shaped endpoints on
    /// <see cref="StatementEndpoints"/>, this one accepts <c>symbol</c> and <c>limit</c> only, and every row is a
    /// quarterly announcement. There is nothing to ask for annually.</para>
    ///
    /// <para>Rows whose date cannot be parsed are dropped — see the note on the type.</para></summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    public async Task<IReadOnlyList<EarningsReport>> GetEarningsAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/earnings").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListEarningsReport, ct).ConfigureAwait(false);

        var dated = new List<EarningsReport>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) dated.Add(row);
        return dated;
    }

    /// <summary>Every earnings event FMP has in a date range, from <c>stable/earnings-calendar</c>.
    ///
    /// <para>Two upstream behaviours make this endpoint harder to use correctly than its signature suggests, and
    /// both were measured on 2026-08-26 rather than read from the documentation.</para>
    ///
    /// <para><b>1. The response is silently capped at 4000 rows, and the truncation eats the front of the
    /// range.</b> One day (05-13) answers 2039 rows on its own. Ask for 05-13 to 05-14 together and the answer is
    /// <b>exactly 4000</b>, of which only 1969 are dated 05-13 — 70 rows of a day that was complete a moment ago
    /// are simply gone, and the cut does not respect day boundaries. Ask for 05-13 to 05-19 and the answer is again
    /// exactly 4000, this time with <b>no 05-13 row at all</b>. <c>limit=6000</c> is accepted and ignored. There is
    /// no cursor, so the SDK cannot page around this; it can only detect it, which is what
    /// <see cref="EarningsCalendarResult"/> is for — the returned list is one, and
    /// <see cref="EarningsCalendarResult.IsLikelyTruncated(IReadOnlyList{EarningsCalendarEntry})"/> reads it. The
    /// signal is computed on the raw response <b>before</b> any clamping, which matters: clamp first and a
    /// truncated 4000-row response can reach a row-count test already reduced below 4000 and pass it.
    /// <b>Day-at-a-time is the only chunk width measured to be safe</b> — a 31-day window in a heavy month returned
    /// exactly 4000, a 7-day peak-season window returned 3676, and an unchunked 15-month request returned 7 rows.</para>
    ///
    /// <para><b>2. <paramref name="includeReportTimes"/> re-dates some rows past the end of the range; it does not
    /// add them.</b> The plain and flagged requests for 05-13 return the <b>identical 2039-symbol set</b>, but 51 of
    /// those rows report <c>2026-05-14</c> when the flag is on. None of those 51 symbols appear in the
    /// <c>from=2026-05-14&amp;to=2026-05-14</c> request at all. So selection happens on the un-shifted date and only
    /// the reported date moves.</para>
    ///
    /// <para>That last measurement is why <paramref name="clampToRange"/> defaults to
    /// <see langword="false"/>. GitHub issue #8 asks for a clamp on the premise that overshoot rows are duplicates
    /// that also appear in the next chunk; they are not, and they do not. A clamp removes <b>no</b> duplicates and
    /// permanently deletes real rows — 51 of 2039, 2.5% of the day — that no other request will ever return.</para>
    ///
    /// <para>Worth separating two halves of an older observation that reached the issue together. That
    /// <c>from=2026-05-13&amp;to=2026-05-19</c> returns a <c>2026-05-20</c> row while the same request without the
    /// flag stops exactly at <c>to</c> was measured on 2026-08-06 and reproduces exactly. That the overshoot row
    /// also appears in the following chunk was <i>inferred</i> from it and never tested; tested on 2026-08-26, it
    /// is false.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive. May equal <paramref name="from"/>, and day-at-a-time is
    /// the recommended and only measured-safe usage.</param>
    /// <param name="includeReportTimes">Sends <c>includeReportTimes=true</c>, which populates
    /// <see cref="EarningsCalendarEntry.ReportTime"/> and the four other extras — and re-dates a small fraction of
    /// rows one day forward, as above. Omitted from the query entirely when false, matching the request that was
    /// measured.</param>
    /// <param name="clampToRange">Discards rows dated outside <c>[from, to]</c>. Off by default because it is lossy
    /// rather than corrective. It is here for one caller: one writing into a store that cannot reject a duplicate —
    /// no unique index, or a delete-then-insert writer with no dedupe — and which would rather lose a row than
    /// double one. If the store can reject or upsert duplicates, leave this off; there are no duplicates to remove
    /// and the rows it deletes are real. Note that clamping never hides the truncation signal, which is taken from
    /// the raw response.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="EarningsCalendarResult"/> — the rows in wire order, which is <b>not</b> sorted,
    /// carrying the row count FMP actually returned so the caller can tell a complete answer from a truncated
    /// one.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    public async Task<IReadOnlyList<EarningsCalendarEntry>> GetEarningsCalendarAsync(
        LocalDate from, LocalDate to, bool includeReportTimes = false, bool clampToRange = false,
        CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"The range end must not precede its start; 'from' was {from:uuuu-MM-dd}.");

        var request = new FmpRequest("stable/earnings-calendar")
            .With("from", from)
            .With("to", to)
            // Sent only when true. The measured plain request omits the parameter rather than sending false, and
            // there is no evidence about how FMP reads an explicit false.
            .With("includeReportTimes", includeReportTimes ? true : (bool?)null);

        var rows = await transport.GetListAsync(request, FmpJsonContext.Default.ListEarningsCalendarEntry, ct)
            .ConfigureAwait(false);

        // Both truncation tells are taken from the raw response, before the filter below can move either of them.
        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<EarningsCalendarEntry>(rows.Count);
        foreach (var row in rows)
        {
            if (row?.Date is not { } date) continue;
            if (clampToRange && (date < from || date > to)) continue;
            kept.Add(row);
        }

        return new EarningsCalendarResult(kept, rows.Count, from, to, earliest);
    }
}
