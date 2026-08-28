using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's analyst coverage group — what the sell side expects, as opposed to what a company reported.
///
/// <para>Everything here is a <b>forecast</b>. The statement endpoints on <see cref="StatementEndpoints"/> answer
/// periods that have ended; this group answers periods that have not, and the two must not be joined as though
/// they were the same kind of fact.</para></summary>
public sealed class AnalystEndpoints(FmpTransport transport)
{
    /// <summary>Sell-side consensus estimates for one symbol's future fiscal periods, from
    /// <c>stable/analyst-estimates</c>.
    ///
    /// <para><b>Rows come back descending — furthest future first — so <paramref name="limit"/> returns the N
    /// furthest-out estimates, not the next N.</b> This is the opposite of what a caller reaching for
    /// <c>limit</c> will assume, and it fails silently: asking for 3 gets 3 real rows, they are simply the wrong
    /// 3. Measured against AAPL on 2026-08-26:</para>
    ///
    /// <list type="bullet">
    /// <item><description><c>period=annual&amp;limit=3</c> answered <c>2030-09-27, 2029-09-27,
    /// 2028-09-27</c> — the three furthest-out annual periods FMP has, ending four years past the call;</description></item>
    /// <item><description><c>period=quarter&amp;limit=3</c> answered <c>2028-09-27, 2028-06-27,
    /// 2028-03-27</c> — likewise the three furthest-out quarters, and not the three about to happen.</description></item>
    /// </list>
    ///
    /// <para>So a caller wanting the <i>nearest</i> estimates must ask for all of them and take from the tail, or
    /// page to the last page — not shrink <paramref name="limit"/>. The SDK deliberately does not reverse or
    /// re-sort the rows: the wire order is what FMP paged and limited by, so reordering here would make
    /// <paramref name="limit"/> and <paramref name="page"/> describe something other than what was returned, and
    /// would hide the quirk rather than fix it. Sort or reverse at the call site once, knowing what was asked.
    /// This is contrary to <see cref="StatementEndpoints"/>, whose seven endpoints answer <i>newest</i> first —
    /// same-looking method, opposite end of the series.</para>
    ///
    /// <para><paramref name="period"/> selects the cadence and is sent as FMP's request vocabulary
    /// (<c>annual</c>/<c>quarter</c>) via <see cref="FiscalPeriodExtensions.ToQueryValue"/>. Unlike the statement
    /// endpoints, the rows carry <b>no</b> <c>period</c> and no <c>fiscalYear</c> echoing the choice back, and the
    /// two series <b>collide on <c>date</c></b>: a fiscal year end and its Q4 end are the same day. Measured
    /// 2026-08-26, <c>2028-09-27</c> is in AAPL's annual series with a revenue average of 558,901,943,758 and in
    /// its quarterly series with 128,079,050,952. Calling this twice and concatenating the results would therefore
    /// merge a year into a quarter with nothing left to tell them apart.</para>
    ///
    /// <para><b>So this method stamps <see cref="AnalystEstimate.Period"/> on every row it returns</b>, from the
    /// <paramref name="period"/> it was given. <c>(Symbol, Period, Date)</c> is then a key that survives a
    /// <c>Concat</c>, and no caller has to reconstruct the cadence from which variable a list landed in. That is
    /// deliberately stronger than <see cref="StatementEndpoints.GetEnterpriseValuesAsync"/>, which documents the
    /// identical collision and resolves it only by asking the caller to remember — a doc comment is not
    /// load-bearing at the line where two lists are joined, and a property is.</para>
    ///
    /// <para><paramref name="page"/> is a zero-based page index and <paramref name="limit"/> the page size; both
    /// are omitted from the query when null rather than guessed at, leaving FMP's own defaults in force. An
    /// unknown symbol answers <c>[]</c> with HTTP 200 rather than a 404, which the transport surfaces as an empty
    /// list — never null — so "no coverage", "not found" and "wrong spelling of a class-share ticker" are one
    /// shape here.</para>
    ///
    /// <para>See <see cref="AnalystEstimate"/> for the row: 22 fields, a <c>date</c> that is a fiscal period
    /// <i>end</i> rather than a publication date, and six low/high/average groups drawn from panels of differing
    /// size.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it — hyphenated for class shares (<c>BRK-B</c>, not
    /// <c>BRK.B</c>). Required; a null, empty or whitespace symbol throws before a request is spent.</param>
    /// <param name="period">Annual or quarterly cadence. Defaults to <see cref="FiscalPeriod.Annual"/>.</param>
    /// <param name="limit">Page size. Null leaves it to FMP. <b>Read the ordering note above before reaching for
    /// this</b> — a small limit takes from the far end of the series, not the near one.</param>
    /// <param name="page">Zero-based page index. Null leaves it to FMP.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> or <paramref name="page"/> is given
    /// and out of range — a limit must be positive, a page index cannot be negative.</exception>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error in the body.</exception>
    public async Task<IReadOnlyList<AnalystEstimate>> GetEstimatesAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, int? page = null,
        CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(Estimates(symbol, period, limit, page),
            FmpJsonContext.Default.ListAnalystEstimate, ct).ConfigureAwait(false);

        // Stamped here rather than left to the caller: see the collision note above. `with` because the record is
        // immutable, and the copy is cheap next to the HTTP call that produced it. Wire order is preserved — the
        // rows are rebuilt in place, not re-sorted.
        var stamped = new List<AnalystEstimate>(rows.Count);
        foreach (var row in rows)
        {
            // A literal null element is legal JSON even though neither capture contained one, and `with` on a
            // null would turn a cosmetic upstream glitch into a NullReferenceException inside the SDK.
            if (row is null) continue;
            stamped.Add(row with { Period = period });
        }
        return stamped;
    }

    /// <summary>Builds and validates the query.
    ///
    /// <para>The symbol and limit rules match <c>StatementEndpoints.Periodic</c> deliberately — a blank symbol and
    /// a non-positive limit are caller mistakes, and both are worth catching here rather than spending a request
    /// to have FMP answer an empty array that reads like "no coverage". They are re-stated rather than shared:
    /// this endpoint takes a <c>page</c> the period-shaped ones do not, and widening a helper that fourteen
    /// call sites depend on, to also serve this endpoint's different query shape, trades a few duplicated lines for a
    /// coupling that would have to be undone the first time the two shapes diverge again.</para></summary>
    private static FmpRequest Estimates(string symbol, FiscalPeriod period, int? limit, int? page)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");
        // Page is zero-based, so zero is the first page and legal where zero is not a legal limit.
        if (page is < 0)
            throw new ArgumentOutOfRangeException(nameof(page), page, "A page index, when given, cannot be negative.");
        return new FmpRequest("stable/analyst-estimates")
            .With("symbol", symbol)
            .With("period", period.ToQueryValue())
            .With("limit", limit)
            .With("page", page);
    }

    /// <summary>Every analyst rating action on one symbol, newest first, from <c>stable/grades</c>.
    ///
    /// <para><b>This returns the whole series and there is no way to ask for less.</b> Measured 2026-08-28,
    /// <c>symbol=AAPL</c> answered <b>1,791 rows</b>; so did <c>limit=5</c>, <c>limit=10000</c> and
    /// <c>page=1</c> — the last with a byte-identical first row. The count varies by symbol (MSFT 967, BRK-B
    /// 93), so it is the whole set each time rather than a cap. Neither <c>limit</c> nor <c>page</c> is offered
    /// here, because offering a parameter FMP discards would let a caller believe they had narrowed something.
    /// Take from the head of the returned list instead.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored too</b>, measured the same day: 1,791 rows with and
    /// without <c>from=2024-01-01&amp;to=2024-12-31</c>. Filter on <see cref="StockGrade.Date"/> at the call
    /// site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockGrade>> GetGradesAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await transport.GetListAsync(
            new FmpRequest("stable/grades").With("symbol", symbol),
            FmpJsonContext.Default.ListStockGrade, ct).ConfigureAwait(false);
    }

    /// <summary>The current spread of analyst opinion on one symbol, from <c>stable/grades-consensus</c>.
    /// Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para><b>This is not the newest row of <see cref="GetGradeHistoryAsync"/>.</b> Measured for AAPL the same
    /// minute on 2026-08-28, this endpoint's counts total 112 analysts and the newest historical row totals 47,
    /// with differently shaped distributions. See <see cref="GradeConsensus"/> for the numbers. They are
    /// different populations, and joining or reconciling them is not something this SDK does for you.</para>
    ///
    /// <para>FMP sends one row in an array; this unwraps it, as
    /// <see cref="CompanyEndpoints.GetProfileAsync"/> does. An unknown-but-well-formed symbol answers an empty
    /// array with HTTP 200 rather than a 404, which surfaces here as <see langword="null"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<GradeConsensus?> GetGradeConsensusAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/grades-consensus").With("symbol", symbol),
            FmpJsonContext.Default.ListGradeConsensus, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Monthly snapshots of analyst ratings for one symbol, newest first, from
    /// <c>stable/grades-historical</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything</b> — 92 rows
    /// for AAPL measured 2026-08-28, unchanged by <c>limit=10000</c>, back to 2018. Rows are dated the first of
    /// each month.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored</b>, measured the same day: 92 rows with and without a
    /// 2024 range. Filter on <see cref="GradeHistory.Date"/> at the call site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N months, or null for the whole history. Must be positive when
    /// given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<GradeHistory>> GetGradeHistoryAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/grades-historical").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListGradeHistory, ct).ConfigureAwait(false);
    }

    /// <summary>Where analyst price targets on one symbol sit, from <c>stable/price-target-consensus</c>.
    /// Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para>One row, unwrapped as <see cref="CompanyEndpoints.GetProfileAsync"/> does. An
    /// unknown-but-well-formed symbol answers an empty array with HTTP 200, not a 404.</para>
    ///
    /// <para><c>from</c>, <c>to</c> and <c>limit</c> are not offered: this endpoint answers a single current
    /// summary and has nothing to page or filter.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<PriceTargetConsensus?> GetPriceTargetConsensusAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/price-target-consensus").With("symbol", symbol),
            FmpJsonContext.Default.ListPriceTargetConsensus, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Analyst price-target activity on one symbol over four windows, from
    /// <c>stable/price-target-summary</c>. Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para><b>A zero count and a zero average are indistinguishable from "unknown" in this payload</b> — read
    /// the remarks on <see cref="PriceTargetSummary"/> and gate every average on its matching count.</para>
    ///
    /// <para><see cref="PriceTargetSummary.Publishers"/> arrives as a string containing a JSON array and is
    /// parsed into a list; this is the same shape and now the same type as the whole-universe
    /// <see cref="BulkEndpoints.StreamPriceTargetSummariesAsync"/> returns.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<PriceTargetSummary?> GetPriceTargetSummaryAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/price-target-summary").With("symbol", symbol),
            FmpJsonContext.Default.ListPriceTargetSummary, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>FMP's current letter rating for one symbol, from <c>stable/ratings-snapshot</c>. Returns
    /// <see langword="null"/> when FMP has no rating.
    ///
    /// <para><b>The returned row carries no date</b> — this endpoint sends none, so
    /// <see cref="CompanyRating.Date"/> is always null here. Use <see cref="GetRatingHistoryAsync"/> if you need
    /// to know when a rating applied.</para>
    ///
    /// <para>One row, unwrapped as <see cref="CompanyEndpoints.GetProfileAsync"/> does. An
    /// unknown-but-well-formed symbol answers an empty array with HTTP 200, not a 404.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<CompanyRating?> GetRatingAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ratings-snapshot").With("symbol", symbol),
            FmpJsonContext.Default.ListCompanyRating, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>FMP's rating for one symbol over time, newest first, from <c>stable/ratings-historical</c>.
    ///
    /// <para><b><paramref name="limit"/> defaults to 100, and that is deliberately <i>not</i> what FMP does.</b>
    /// Measured 2026-08-28: with no <c>limit</c> this endpoint answers <b>exactly one row</b> — from a path
    /// named "historical". Passing FMP's default through faithfully would be useless to a caller, so this method
    /// sends 100 unless told otherwise. The measured ladder, for anyone choosing a value: <c>limit=5</c> → 5,
    /// <c>100</c> → 100, <c>1000</c> → 1000, <c>5000</c> → 5000, <c>10000</c> → <b>6292</b>, <c>50000</c> →
    /// 6292. That last figure is AAPL's whole series, not a cap — it stops growing because the data does. There
    /// is therefore no maximum page size to enforce here.</para>
    ///
    /// <para>This is the only <c>limit</c> in this endpoint group with a non-null default. The dividend, split
    /// and grade-history methods all leave theirs null, because those endpoints answer the whole series when the
    /// parameter is absent and a default would silently truncate it.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored</b>, measured the same day: 1000 rows with and without a
    /// 2024 range. The series is per trading day. Filter on <see cref="CompanyRating.Date"/> at the call
    /// site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N rows. Defaults to 100 rather than to FMP's own default of one. Must be
    /// positive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<CompanyRating>> GetRatingHistoryAsync(
        string symbol, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/ratings-historical").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListCompanyRating, ct).ConfigureAwait(false);
    }
}
