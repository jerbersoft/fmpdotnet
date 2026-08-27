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
}
