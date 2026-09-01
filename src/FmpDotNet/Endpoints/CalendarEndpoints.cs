using System.Text.Json.Serialization.Metadata;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Calendar</c> group — events dated on a calendar rather than tied to a fiscal period.
///
/// <para>Nine methods. Three of them pair a per-symbol history against a date-ranged calendar answering the
/// same question from the opposite end: earnings (<see cref="GetEarningsAsync"/> /
/// <see cref="GetEarningsCalendarAsync"/>), dividends (<see cref="GetDividendsAsync"/> /
/// <see cref="GetDividendsCalendarAsync"/>) and splits (<see cref="GetSplitsAsync"/> /
/// <see cref="GetSplitsCalendarAsync"/>). The earnings pair's row shapes overlap exactly — seven fields,
/// identical names — which is why <see cref="EarningsCalendarEntry"/> is <see cref="EarningsReport"/> plus five
/// optional extras; the dividend and split pairs instead share one record across both ends. The remaining three
/// methods are IPO feeds with no per-symbol twin at all: <see cref="GetIpoCalendarAsync"/> is a scheduling
/// calendar, and <see cref="GetIpoDisclosuresAsync"/>/<see cref="GetIpoProspectusesAsync"/> are EDGAR filing
/// feeds that take a date range because FMP offers no per-symbol path for either.</para>
///
/// <para><b>Rows whose <c>date</c> cannot be parsed are dropped on five of the nine methods, and handed over
/// unfiltered on the other four — check which group a method is in before relying on either behaviour.</b> The
/// rule applies to <see cref="GetEarningsAsync"/> and to the four date-ranged methods that report their own
/// truncation: <see cref="GetEarningsCalendarAsync"/>, which answers through
/// <see cref="EarningsCalendarResult"/>, and <see cref="GetDividendsCalendarAsync"/>,
/// <see cref="GetSplitsCalendarAsync"/> and <see cref="GetIpoCalendarAsync"/>, which answer through
/// <see cref="CalendarResult{T}"/>. It does not apply to <see cref="GetDividendsAsync"/> or
/// <see cref="GetSplitsAsync"/> — see the remarks on each — nor to <see cref="GetIpoDisclosuresAsync"/> and
/// <see cref="GetIpoProspectusesAsync"/>, which apply no date-based filtering of any kind and return exactly
/// what FMP sent.</para>
///
/// <para><b>The split is not simply per-symbol against calendar, and no rule about a method's shape will
/// predict it.</b> <see cref="GetEarningsAsync"/> is a per-symbol path and drops; its two newer per-symbol
/// siblings do not. That is why this note lists the methods by name — the grouping is a record of what each one
/// does, not a principle you can apply to the next one added.</para>
///
/// <para>Reasoned once, here, for the five methods it applies to:</para>
///
/// <list type="bullet">
/// <item><description>Wherever it applies, the date is not a field, it is half the row's identity —
/// <c>(symbol, date)</c> is the key a caller stores, deduplicates and joins on. A row with no date cannot be placed
/// on a timeline, cannot be matched to the same event arriving from another request, and in a keyed store becomes
/// either a phantom or a collision. The SDK already applies exactly this rule to the directory endpoints, where a
/// blank label is dropped because "a label is a key".</description></item>
/// <item><description>On the four calendar methods it is also the only answer that stays consistent: a
/// null-dated row cannot be clamped to a range either, so keeping it would force a second arbitrary decision
/// with no honest answer.</description></item>
/// <item><description>It is a defence rather than a routine, and it was measured only on the earnings pair.
/// Measured 2026-08-26, all 165 rows of AAPL's full earnings history and all 48 rows of the captured earnings
/// calendar day carried a parseable date, so the rule should remove nothing there;
/// <see cref="EarningsCalendarResult.RowsReturned"/> against <see cref="EarningsCalendarResult.Count"/>, and
/// <see cref="CalendarResult{T}.RowsReturned"/> against <see cref="CalendarResult{T}.Count"/> on the other
/// three, say how much it removed if it ever does. The dividends, splits and IPO calendars apply the same rule
/// by analogy to that reasoning, not from a per-path measurement of undated rows — no such measurement was taken
/// on those three paths, so treat their expected removal count as unknown rather than zero.</description></item>
/// </list></summary>
public sealed class CalendarEndpoints(FmpTransport transport)
{
    /// <summary>The most pages <see cref="GetEarningsCalendarAsync"/> and
    /// <see cref="GetDividendsCalendarAsync"/> will fetch for one call.
    ///
    /// <para><b>A guard, not a measurement, and it is the one number here with no probe behind it.</b> Neither
    /// path has a page ceiling: measured 2026-09-01, <c>page=101</c> and <c>page=1000</c> both answer <c>[]</c>
    /// under HTTP 200, so a walk that stops on a short page provably terminates and this bound is never
    /// reached in practice. It exists because a sibling path already breaks that reasoning —
    /// <c>ipos-calendar</c> serves <c>page=5</c> byte-identically to <c>page=0</c>, every page full, so a walk
    /// there would never end. 100 pages is 400,000 rows, about fourteen years of dividends at the measured
    /// 28,104 rows a year.</para>
    ///
    /// <para>Reaching it is reported rather than thrown: the rows already fetched are real, and this SDK
    /// reports rather than fails. A walk stopped here ends with a full page as its last, so
    /// <see cref="Models.CalendarResult{T}.AtRowCap"/> fires.</para></summary>
    public const int MaxCalendarPages = 100;

    /// <summary>FMP's undocumented hard cap on one <c>stable/dividends-calendar</c> page. Measured 2026-08-28
    /// and again 2026-09-01: a request for the whole of 2025 answers exactly 4000 rows, and
    /// <c>limit=10000</c> is accepted and ignored. <c>page</c> is what escapes it.</summary>
    private const int DividendsCalendarRowCap = 4000;

    /// <summary>Walks <c>page=0, 1, 2, …</c> until a page comes back short, and hands back the concatenation
    /// with the evidence gathered between pages.
    ///
    /// <para><b>Rows are concatenated in walk order and otherwise untouched</b> — not sorted, not
    /// de-duplicated. A row arriving on both sides of a seam is FMP's row, served twice by FMP; removing one
    /// would be a guess about which of two identical rows is real, and FMP's own data carries genuine
    /// duplicate rows. The count goes to
    /// <see cref="Models.CalendarResult{T}.SeamDuplicateRows"/> instead, where it doubles as the count of
    /// rows the walk never saw.</para>
    ///
    /// <para><b>Three terminators.</b> A short page is the last page — measured on four walks, none of which
    /// produced a short page followed by a full one. A page whose distinct rows are exactly its predecessor's
    /// is that predecessor served again, which is what <c>ipos-calendar</c> does today; it is discarded rather
    /// than appended. And <see cref="MaxCalendarPages"/> bounds the loop whatever FMP does.</para>
    ///
    /// <para>Internal rather than private so the terminators can be tested at their own level. Reaching the
    /// page ceiling through a calendar method would need 100 full pages — 400,000 rows at the real cap — for
    /// one assertion; <c>CalendarWalkTests</c> reaches it with <c>rowCap: 2</c>. Nothing here branches on being
    /// under test.</para></summary>
    internal async Task<(List<T> Rows, Models.CalendarWalk Walk)> WalkAsync<T>(
        Func<int, FmpRequest> buildRequest,
        JsonTypeInfo<List<T>> typeInfo,
        int rowCap,
        CancellationToken ct)
    {
        var all = new List<T>();
        var pages = 0;
        var seamDuplicates = 0;
        var lastPageRowCount = 0;
        HashSet<T>? previous = null;

        for (var page = 0; page < MaxCalendarPages; page++)
        {
            var rows = await transport.GetListAsync(buildRequest(page), typeInfo, ct).ConfigureAwait(false);

            // Distinct rows, because the seam is measured between SETS: a row FMP sends twice within one page
            // is not a paging artefact and must not be counted as one. The models are records, so this is
            // structural equality and costs one hash pass per page.
            var current = new HashSet<T>(rows);

            if (previous is not null)
            {
                var shared = 0;
                foreach (var row in current)
                    if (previous.Contains(row)) shared++;

                // Every row already seen and nothing new: this page IS the previous one. Stop before appending
                // it, or a path that ignores `page` returns the same rows MaxCalendarPages times.
                if (shared == current.Count && shared == previous.Count) break;

                seamDuplicates += shared;
            }

            all.AddRange(rows);
            pages++;
            lastPageRowCount = rows.Count;
            previous = current;

            if (rows.Count < rowCap) break;
        }

        return (all, new Models.CalendarWalk(all.Count, pages, lastPageRowCount, seamDuplicates));
    }

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
    /// <para><b>There is no <c>period</c> parameter.</b> Unlike the period-shaped endpoints on
    /// <see cref="StatementEndpoints"/>, this one accepts <c>symbol</c> and <c>limit</c> only, and every row is a
    /// quarterly announcement. There is nothing to ask for annually.</para>
    ///
    /// <para>Rows whose date cannot be parsed are dropped — see the note on the type.</para></summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
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
    /// <para>Three upstream behaviours make this endpoint harder to use correctly than its signature suggests.
    /// The cap and the re-dating were measured on 2026-08-26; the walk that escapes the cap, and what it costs,
    /// was measured 2026-09-01 (#49) — none of it read from the documentation.</para>
    ///
    /// <para><b>1. The response is silently capped at 4000 rows, and the truncation eats the front of the
    /// range.</b> One day (05-13) answers 2039 rows on its own. Ask for 05-13 to 05-14 together and the answer is
    /// <b>exactly 4000</b>, of which only 1969 are dated 05-13 — 70 rows of a day that was complete a moment ago
    /// are simply gone, and the cut does not respect day boundaries. Ask for 05-13 to 05-19 and the answer is again
    /// exactly 4000, this time with <b>no 05-13 row at all</b>. <c>limit=6000</c> is accepted and ignored. This
    /// method detects the truncation, which is what
    /// <see cref="EarningsCalendarResult"/> is for — the returned list is one, and
    /// <see cref="EarningsCalendarResult.IsLikelyTruncated(IReadOnlyList{EarningsCalendarEntry})"/> reads it. The
    /// signal is computed on the raw response <b>before</b> any clamping, which matters: clamp first and a
    /// truncated 4000-row response can reach a row-count test already reduced below 4000 and pass it.
    /// <b>The cap is escapable and this method now escapes it</b> — see below. What the cap still costs is a
    /// request per 4000 rows: measured 2026-09-01, the first half of 2025 is 45,765 rows over 12 requests.</para>
    ///
    /// <para><b>2. This method walks the cursor, and the walk is not lossless.</b> Measured 2026-09-01 (#49):
    /// <c>page</c> is honoured here even though <c>limit</c> is not, so <c>from=2026-05-13&amp;to=2026-05-19</c>
    /// answers 4000 rows on page 0, 2496 on page 1 and 0 on page 2 — <b>6496 in total, where this method used
    /// to return 4000</b>. It now fetches all of them, at one request per page.</para>
    ///
    /// <para><b>But a page seam loses rows, and the loss is reported rather than repaired.</b> FMP orders
    /// these responses by <c>date</c> and by nothing else, and a seam always falls <i>inside</i> a date — all
    /// 22 measured. So some rows arrive on both sides of a seam and an equal number of different rows arrive
    /// on neither: measured over the first half of 2025, 1,166 rows across 7 of 11 seams, deterministic across
    /// re-fetches. <see cref="EarningsCalendarResult.SeamDuplicateRows"/> counts them, and seven seams
    /// re-checked a day at a time put that count at exactly the number of rows lost — 381/381, 174/174,
    /// 315/315, and three clean seams losing none. <b>A range narrow enough to fit one page has no seam and
    /// cannot lose anything</b>, which is what to fall back on when
    /// <see cref="EarningsCalendarResult.LikelyTruncated"/> fires.</para>
    ///
    /// <para><b>3. <paramref name="includeReportTimes"/> re-dates some rows past the end of the range; it does not
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
    /// <param name="to">Last day of the range, inclusive. May equal <paramref name="from"/>, and a range narrow
    /// enough to fit one page is the only width that cannot lose rows at a seam.</param>
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
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<EarningsCalendarEntry>> GetEarningsCalendarAsync(
        LocalDate from, LocalDate to, bool includeReportTimes = false, bool clampToRange = false,
        CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var (rows, walk) = await WalkAsync(
            page => new FmpRequest("stable/earnings-calendar")
                .With("from", from)
                .With("to", to)
                // Sent only when true. The measured plain request omits the parameter rather than sending
                // false, and there is no evidence about how FMP reads an explicit false.
                .With("includeReportTimes", includeReportTimes ? true : (bool?)null)
                // Omitted on page 0, where it was measured byte-identical to sending nothing: the first
                // request of a walk is the request this method made before it walked.
                .With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListEarningsCalendarEntry,
            EarningsCalendarResult.RowCap,
            ct).ConfigureAwait(false);

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

        return new EarningsCalendarResult(kept, walk, from, to, earliest);
    }

    /// <summary>Every dividend FMP holds for one symbol, newest first, from <c>stable/dividends</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything.</b> Measured
    /// 2026-08-28, AAPL answers <b>92 rows</b> with no limit and the same 92 with <c>limit=10000</c> — the whole
    /// history, back to 1987. A default of 100 would have quietly cut a longer one.</para>
    ///
    /// <para><b>There is no date range on this method, because the endpoint ignores one.</b> Measured the same
    /// day: <c>symbol=AAPL</c> answers 92 rows, and <c>symbol=AAPL&amp;from=2024-01-01&amp;to=2024-12-31</c>
    /// answers the same 92. Offering the parameters would let a caller believe a filter happened. Use
    /// <see cref="GetDividendsCalendarAsync"/> for a date range, or filter
    /// <see cref="Dividend.Date"/> at the call site.</para>
    ///
    /// <para>An unknown symbol answers <c>[]</c> with HTTP 200 rather than a 404, which the transport surfaces
    /// as an empty list — never null.</para>
    ///
    /// <para><b>Every row FMP sends is returned, undated ones included.</b> Unlike
    /// <see cref="GetDividendsCalendarAsync"/>, this method does not drop a row whose <c>date</c> will not parse.
    /// Here the symbol is the row's identity rather than the date: a dividend with an unparseable date is still
    /// that symbol's dividend, so the SDK hands it over rather than deciding for the caller that it should be
    /// dropped. On a calendar the date is half the identity instead, because the caller is asking what happened
    /// in a range and an undated row cannot be placed in one. <b>Note this is a choice made per method, not a
    /// property of per-symbol paths:</b> <see cref="GetEarningsAsync"/> is per-symbol and does drop. The note on
    /// <see cref="CalendarEndpoints"/> lists which methods do which.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it — hyphenated for class shares (<c>BRK-B</c>, not
    /// <c>BRK.B</c>).</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<Dividend>> GetDividendsAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/dividends").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListDividend, ct).ConfigureAwait(false);
    }

    /// <summary>Every dividend event FMP has in a date range, across all symbols, from
    /// <c>stable/dividends-calendar</c>.
    ///
    /// <para><b>The response is silently capped at 4000 rows, and the truncation eats the front of the
    /// range.</b> Measured 2026-08-28: <c>from=2025-01-01&amp;to=2025-12-31</c> answered <b>exactly 4000</b>
    /// rows whose earliest date was <b>2025-12-29</b> — a request for a year, answered with its last three days,
    /// and a caller reading <c>rows[0]</c> is handed December believing they hold January. One month behaves the
    /// same way: June 2025 answered 4000 rows starting 2025-06-26. <c>limit=10000</c> was accepted and ignored.
    /// This method reports the truncation — which is what <see cref="CalendarResult{T}"/> is for, and the returned
    /// list is one.</para>
    ///
    /// <para><b>This method walks the cursor, and the walk is not lossless.</b> Measured 2026-09-01 (#49):
    /// <c>page</c> is honoured here even though <c>limit</c> is not. May 2026 answers 4000 rows on page 0,
    /// 4000 on page 1 and 1325 on page 2 — <b>9325 where this method used to return 4000</b> — and the whole
    /// of 2025 is <b>28,104 rows over 8 requests</b> against the same 4000. All of them are now fetched.</para>
    ///
    /// <para><b>A page seam loses rows.</b> Over that 2025 walk, 913 rows arrived on both sides of a seam and
    /// 913 different rows arrived on neither, deterministically — re-fetching the year's first two pages
    /// minutes later returned byte-identical responses carrying the identical 381-row overlap.
    /// <see cref="CalendarResult{T}.SeamDuplicateRows"/> counts them and, measured, that count is the number
    /// lost. A range that fits one page has no seam; that is the remedy when
    /// <see cref="CalendarResult{T}.LikelyTruncated"/> fires.</para>
    ///
    /// <para><b>A safe width cannot be read off the calendar.</b> Density measured 340 rows on 2025-11-20, 673
    /// on 2025-03-14 and 876 on 2025-06-02, so the cap falls somewhere between five and eleven days depending on
    /// the season. A six-day window returned 2147 rows and was complete; a thirty-day window was not. That
    /// season-dependence is why this method walks rather than guesses a chunk size, and reports the seam rather
    /// than hiding it.</para>
    ///
    /// <code>
    /// var rows = await fmp.Calendar.GetDividendsCalendarAsync(from, to);
    /// if (rows is CalendarResult&lt;Dividend&gt; { LikelyTruncated: true }) { /* narrow the range and retry */ }
    /// </code>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this class: on a
    /// calendar the date is half the row's identity. <see cref="CalendarResult{T}.RowsReturned"/> against
    /// <see cref="CalendarResult{T}.Count"/> says how many, and the truncation tells are computed on the raw
    /// response before any of that happens.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive. May equal <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="Dividend"/> — the rows in wire order, which is
    /// <b>not</b> sorted, carrying the row count FMP actually returned so the caller can tell a complete answer
    /// from a truncated one.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<Dividend>> GetDividendsCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var (rows, walk) = await WalkAsync(
            page => new FmpRequest("stable/dividends-calendar")
                .With("from", from)
                .With("to", to)
                // Omitted on page 0, where it was measured byte-identical to sending nothing.
                .With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListDividend,
            DividendsCalendarRowCap,
            ct).ConfigureAwait(false);

        // Taken from the raw response, before the filter below can move it.
        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<Dividend>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        // rowCap 4000, lookbackLimitDays null: the cap always fires first at 340-876 rows a day, so no window
        // limit is observable on this path and asserting one would be inventing evidence.
        return new CalendarResult<Dividend>(
            kept, walk, from, to, earliest, rowCap: DividendsCalendarRowCap, lookbackLimitDays: null);
    }

    /// <summary>Every split FMP holds for one symbol, newest first, from <c>stable/splits</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything.</b> AAPL's
    /// whole history is five rows, back to 1987, measured 2026-08-28.</para>
    ///
    /// <para><b>There is no date range on this method, because the endpoint ignores one.</b> Measured the same
    /// day: <c>symbol=AAPL</c> answers 5 rows with and without <c>from=2024-01-01&amp;to=2024-12-31</c> — and
    /// AAPL had no split in 2024, so a filter that worked would have answered none. Use
    /// <see cref="GetSplitsCalendarAsync"/> for a date range.</para>
    ///
    /// <para><b>Every row FMP sends is returned, undated ones included.</b> Unlike
    /// <see cref="GetSplitsCalendarAsync"/>, this method does not drop a row whose <c>date</c> will not parse.
    /// Here the symbol is the row's identity rather than the date: a split with an unparseable date is still
    /// that symbol's split, so the SDK hands it over rather than deciding for the caller that it should be
    /// dropped. <b>A choice made per method, not a property of per-symbol paths:</b>
    /// <see cref="GetEarningsAsync"/> is per-symbol and does drop. The note on
    /// <see cref="CalendarEndpoints"/> lists which methods do which.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockSplit>> GetSplitsAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/splits").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListStockSplit, ct).ConfigureAwait(false);
    }

    /// <summary>Every split FMP has in a date range, across all symbols, from <c>stable/splits-calendar</c>.
    ///
    /// <para><b>This path will not reach more than 90 days back from <paramref name="to"/>, and it drops the
    /// front of the range without saying so.</b> Measured 2026-08-28 against four different <c>to</c> values
    /// spanning twenty months, each with <c>from</c> fixed at 2015-01-01, the earliest row returned was exactly
    /// 90 days before <c>to</c> every time. <b>A request for the whole of 2024 answers Q4 of 2024</b> — 737 rows,
    /// nine months missing. Walking <c>from</c> backwards against a fixed <c>to</c> shows the edge: −88 days is
    /// honoured exactly, and −100, −120 and −180 all return the identical 947 rows with the identical earliest
    /// date.</para>
    ///
    /// <para><b>No row count can see this.</b> 737 is nowhere near a cap, and no cap was measured on this path
    /// at all — the widest range tried answered 947 rows. So
    /// <see cref="CalendarResult{T}.AtRowCap"/> is structurally blind here and
    /// <see cref="CalendarResult{T}.MissesStartOfRange"/> is what catches it, by comparing the earliest row
    /// against the <c>from</c> that was asked for. That is a different mechanism from
    /// <see cref="GetDividendsCalendarAsync"/>, which is row-capped instead, and the returned type reports which
    /// one applies.</para>
    ///
    /// <para><b><c>page</c> does not rescue this one, and that is worth knowing because it rescues its two
    /// siblings.</b> Measured 2026-09-01 (#46): <c>page</c> is a working cursor on
    /// <see cref="GetEarningsCalendarAsync"/> and <see cref="GetDividendsCalendarAsync"/>, where it pages past
    /// their 4000-row cap, which those two methods now walk (#49). Here it answers nothing to page past.
    /// <c>from=2026-01-01&amp;to=2026-08-28</c>
    /// returned 944 rows whose earliest was 2026-05-31 — the 90-day edge, 89 days before <c>to</c>, not a row
    /// cap — and <c>page=1</c> answered <b>0 rows</b> rather than the missing January-to-May. The limit on this
    /// path is a lookback window, and no cursor reaches outside it.</para>
    ///
    /// <para>Note that a span of <i>exactly</i> 90 days reads
    /// <see cref="CalendarResult{T}.ExceedsLookbackLimit"/> as <see langword="false"/> and still loses a day —
    /// −90 answered an earliest row of 2026-05-31 against a requested 2026-05-30. Read
    /// <see cref="CalendarResult{T}.LikelyTruncated"/>, which is the union of the tells, rather than any one of
    /// them.</para>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this
    /// class.</para></summary>
    /// <param name="from">First day of the range, inclusive. Anything more than 90 days before
    /// <paramref name="to"/> is silently ignored — see above.</param>
    /// <param name="to">Last day of the range, inclusive, and the anchor the 90-day window is measured
    /// from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="StockSplit"/>, carrying the evidence needed to
    /// tell a complete answer from a clamped one.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockSplit>> GetSplitsCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/splits-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListStockSplit, ct).ConfigureAwait(false);

        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<StockSplit>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        // The opposite of the dividend calendar: no cap was measured here, and the clamp is a flat 90-day
        // window from `to`.
        return new CalendarResult<StockSplit>(
            kept, CalendarWalk.Single(rows.Count), from, to, earliest, rowCap: null, lookbackLimitDays: 90);
    }

    /// <summary>Every offering FMP has scheduled or priced in a date range, from <c>stable/ipos-calendar</c>.
    ///
    /// <para><b>This path will not reach more than 90 days back from <paramref name="to"/>, exactly as
    /// <see cref="GetSplitsCalendarAsync"/> does, and it drops the front of the range without saying so.</b>
    /// Measured 2026-08-28 against four <c>to</c> values spanning twenty months, each with <c>from</c> fixed at
    /// 2015-01-01, the earliest row returned was 90 days before <c>to</c> every time. A request for the whole of
    /// 2024 answered Q4 of 2024, at <b>358 rows</b> — no cap was reached and none was measured on this path, so
    /// <see cref="CalendarResult{T}.MissesStartOfRange"/> is what catches it.</para>
    ///
    /// <para><b><c>page</c> is accepted here and does nothing at all</b>, which is worth stating precisely
    /// because the other three date-ranged calendars each do something different with it. Measured 2026-09-01
    /// (#49): <c>from=2026-01-01&amp;to=2026-08-31</c> answers 439 rows, and <c>page=1</c> and <c>page=5</c>
    /// answer the <b>same 439 rows, byte-identically</b>. Compare <c>splits-calendar</c>, where <c>page=1</c>
    /// is an empty array, and <see cref="GetEarningsCalendarAsync"/> and
    /// <see cref="GetDividendsCalendarAsync"/>, where it is a working cursor those two methods walk. A walk
    /// here would never terminate — every page is full and every page is the first — so this method makes one
    /// request and the limit stays the 90-day window.</para>
    ///
    /// <para><b>Most rows are unpriced.</b> <see cref="IpoCalendarEntry.PriceRange"/> was null on 441 of 450
    /// rows, <see cref="IpoCalendarEntry.Shares"/> on 349 and <see cref="IpoCalendarEntry.MarketCap"/> on 354.
    /// A row per warrant and per unit is normal, so one company can occupy several rows on one date.</para>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this
    /// class.</para></summary>
    /// <param name="from">First day of the range, inclusive. Anything more than 90 days before
    /// <paramref name="to"/> is silently ignored.</param>
    /// <param name="to">Last day of the range, inclusive, and the anchor the 90-day window is measured
    /// from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="IpoCalendarEntry"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoCalendarEntry>> GetIpoCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ipos-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoCalendarEntry, ct).ConfigureAwait(false);

        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<IpoCalendarEntry>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        return new CalendarResult<IpoCalendarEntry>(
            kept, CalendarWalk.Single(rows.Count), from, to, earliest, rowCap: null, lookbackLimitDays: 90);
    }

    /// <summary>Effectiveness filings for registrations in a date range, from <c>stable/ipos-disclosure</c>.
    ///
    /// <para><b>This path answers the whole range asked for, and that is the thing to plan for.</b> Measured
    /// 2026-08-28: 2024-01-01 to 2024-12-31 returned <b>25,689 rows</b> spanning 2024-01-02 to 2024-12-31, and
    /// 2020-01-01 to 2026-08-28 returned <b>123,678</b>. It is neither capped nor paginated, so a wide range is
    /// a single large response rather than a truncated one — the opposite failure mode from
    /// <see cref="GetIpoCalendarAsync"/>, and the reason this method returns a plain list with no truncation
    /// signal on it. There is nothing to report; there is a payload to budget for.</para>
    ///
    /// <para><b>One filing appears once per share class it covers</b>, sharing a CIK, form and URL across
    /// several tickers — so the row count is not a filing count.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoDisclosure>> GetIpoDisclosuresAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return await transport.GetListAsync(
            new FmpRequest("stable/ipos-disclosure").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoDisclosure, ct).ConfigureAwait(false);
    }

    /// <summary>Prospectus filings and their offering economics in a date range, from
    /// <c>stable/ipos-prospectus</c>.
    ///
    /// <para>Like <see cref="GetIpoDisclosuresAsync"/>, this answers the whole range asked for and is neither
    /// capped nor paginated — 1,048 rows for a full 2024, 15,726 for 2020 to 2026 — so it returns a plain list
    /// with no truncation signal. Smaller than its sibling by roughly twenty-five to one.</para>
    ///
    /// <para><b>It is a follow-on feed as much as a new-issue one:</b>
    /// <see cref="IpoProspectus.IpoDate"/> ran back to 1989 against 2026 filings in the measured sample. And the
    /// money fields are reported exactly as sent — read the remarks on <see cref="IpoProspectus"/> before
    /// treating them as arithmetically consistent.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoProspectus>> GetIpoProspectusesAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return await transport.GetListAsync(
            new FmpRequest("stable/ipos-prospectus").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoProspectus, ct).ConfigureAwait(false);
    }
}
