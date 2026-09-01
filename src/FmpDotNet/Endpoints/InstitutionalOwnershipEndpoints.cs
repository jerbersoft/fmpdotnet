using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Form 13F</c> group — who owns what, as institutions report it quarterly to the SEC.
///
/// <para><b>Nine paths: the eight FMP files under Form 13F, plus <c>acquisition-of-beneficial-ownership</c>,
/// which FMP files under Insider Trades.</b> That one is an SC 13D/G filing — the disclosure an investor makes
/// on crossing 5% of a class. Its subject is an institutional stake, its fields are voting and dispositive
/// power, and its reporting person is an entity (<c>"The Vanguard Group"</c>). It shares nothing with a Form 4
/// transaction but the word "ownership", so it is here rather than on
/// <see cref="InsiderTradesEndpoints"/>. <see cref="SecFilingsEndpoints"/> set that precedent, sending three of
/// its twelve documented paths to <see cref="DirectoryEndpoints"/> and <see cref="SearchEndpoints"/>: this SDK
/// files a path by what it returns.</para>
///
/// <para><b>Start at <see cref="GetFilingDatesAsync"/>.</b> Five of the nine take a <c>year</c> and a
/// <c>quarter</c>, all five reject a call that omits <c>quarter</c> with
/// <c>400 … missing query parameter - quarter</c>, and an unfiled pair answers <c>[]</c> with HTTP 200 rather
/// than an error. That path is the only one that enumerates the pairs that exist.</para>
///
/// <para><b>Two kinds of CIK reach this class and they are not interchangeable.</b> The four <c>cik</c>-keyed
/// methods want an institutional <i>filer's</i> CIK — Berkshire's <c>0001067983</c>. An <i>issuer's</i> CIK,
/// which is what <see cref="SecFilingsEndpoints.GetProfileByCikAsync"/> takes, answers <c>[]</c> on all four:
/// measured 2026-08-28, Apple's <c>320193</c> returned zero rows from every one of them.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class InstitutionalOwnershipEndpoints(FmpTransport transport)
{
    /// <summary>Every quarter one 13F filer has reported, newest first —
    /// <c>stable/institutional-ownership/dates</c>.
    ///
    /// <para><b>Call this before the four quarter-keyed methods.</b> They answer an unfiled <c>year</c>/
    /// <c>quarter</c> pair with an empty list and HTTP 200, so a caller who guesses a pair cannot tell "this
    /// filer reported nothing that quarter" from "this filer has not filed yet". This path answers that
    /// question directly.</para>
    ///
    /// <para><b>No <c>limit</c> and no <c>page</c>, because the endpoint honours neither.</b> Measured
    /// 2026-08-28, Berkshire answered all 53 quarters with and without <c>limit=5</c>.</para></summary>
    /// <param name="cik">The institutional filer's SEC Central Index Key, padded or unpadded — both work,
    /// measured 2026-08-28. <b>Not an issuer's CIK</b>; see the note on this class.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The filer's quarters, newest first. Never <see langword="null"/>; empty for a CIK that has
    /// filed no 13F, which includes every issuer CIK.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FilingQuarter>> GetFilingDatesAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/dates").With("cik", cik),
            FmpJsonContext.Default.ListFilingQuarter, ct);
    }

    /// <summary>Every position one filer reported for one quarter — <c>stable/institutional-ownership/extract</c>.
    ///
    /// <para><b>Wide, and unpageable.</b> State Street's 2026 Q2 answered 4,177 rows. The endpoint accepts
    /// <c>limit</c> and ignores it — measured 2026-08-28, <c>limit=5</c> returned all 4,177 with a
    /// byte-identical body — so no <c>limit</c> and no <c>page</c> are offered here rather than shipping a
    /// control that silently does nothing.</para>
    ///
    /// <para><b>An unfiled quarter answers an empty list, not an error</b>, and so does an issuer's CIK. Use
    /// <see cref="GetFilingDatesAsync"/> to find out which quarters exist.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="year">The calendar year of the quarter end. <b>Not range-checked</b> — an out-of-range year
    /// answers an empty list with HTTP 200, which is a legitimate "no data", and inventing a floor would invent
    /// a fact.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP: omitting it answers
    /// <c>400 … missing query parameter - quarter</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every reported position, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InstitutionalHolding>> GetHoldingsAsync(
        string cik, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/extract")
                .With("cik", cik).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListInstitutionalHolding, ct);
    }

    /// <summary>The largest page <see cref="GetHolderAnalyticsAsync"/> will serve — <b>100, not 1,000</b>, and
    /// measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, and the odd one out in this group. Measured 2026-08-28,
    /// <c>limit=5</c> answered 5 rows while <c>limit=200</c>, <c>limit=1000</c>, <c>limit=1001</c> and
    /// <c>limit=2000</c> each answered exactly 100 with HTTP 200 and byte-identical bodies — nothing in the
    /// response says the request was trimmed. The path genuinely paginates, so a caller who asks for 1,000 and
    /// advances <c>page</c> by 1,000 reads a tenth of the holder list and is never told. A larger
    /// <c>limit</c> is therefore refused here rather than passed on to be clamped.</para>
    ///
    /// <para>Every other paged path in this slice caps at 1,000; see <see cref="MaxOwnershipPageSize"/> and
    /// <see cref="InsiderTradesEndpoints.MaxInsiderTradePageSize"/>.</para></summary>
    public const int MaxHolderAnalyticsPageSize = 100;

    /// <summary>Every institution reporting a position in one symbol for one quarter, with FMP's
    /// quarter-over-quarter analytics —
    /// <c>stable/institutional-ownership/extract-analytics/holder</c>.
    ///
    /// <para><b>The mirror of <see cref="GetHoldingsAsync"/>.</b> That asks a filer what it holds; this asks a
    /// symbol who holds it, and adds weights, ownership percentages, holding periods and performance that a
    /// 13F does not itself report.</para>
    ///
    /// <para><b>Paged, and the cap is 100</b> — see <see cref="MaxHolderAnalyticsPageSize"/>. A widely-held
    /// symbol runs to thousands of holders, so this is a path you page rather than one you drain in a
    /// call.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked; see
    /// <see cref="GetHoldingsAsync"/>.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxHolderAnalyticsPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's holders. Never <see langword="null"/>; empty for an unknown symbol or an unfiled
    /// quarter, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4,
    /// <paramref name="page"/> is negative, or <paramref name="limit"/> is outside 1 to
    /// <see cref="MaxHolderAnalyticsPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderAnalytics>> GetHolderAnalyticsAsync(
        string symbol, int year, int quarter, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfQuarterOutOfRange(quarter);
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxHolderAnalyticsPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/extract-analytics/holder")
                .With("symbol", symbol).With("year", year).With("quarter", quarter)
                .With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListHolderAnalytics, ct);
    }

    /// <summary>How one filer's quarter was spread across industries —
    /// <c>stable/institutional-ownership/holder-industry-breakdown</c>.
    ///
    /// <para>One row per industry, sorted by weight; Berkshire's 2026 Q2 answered 24. No <c>limit</c> and no
    /// <c>page</c> — the endpoint honours neither, and the result set is small enough that it does not
    /// matter.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The industry breakdown, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderIndustryBreakdown>> GetHolderIndustryBreakdownAsync(
        string cik, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/holder-industry-breakdown")
                .With("cik", cik).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListHolderIndustryBreakdown, ct);
    }

    /// <summary>One filer's aggregate portfolio performance for every quarter it has reported —
    /// <c>stable/institutional-ownership/holder-performance-summary</c>.
    ///
    /// <para><b>No <c>year</c> and no <c>quarter</c>, and that is the endpoint's shape rather than a choice
    /// made here.</b> It answers the filer's whole history, newest first, in one response — one row per quarter
    /// in <see cref="GetFilingDatesAsync"/>: 53 for Berkshire, measured 2026-08-28, and 110 for FMR
    /// (<c>0000315066</c>), measured 2026-09-01, each matching its <c>dates</c> count exactly. Across 299
    /// filers measured 2026-09-01 (#53) the largest answer was <b>110 rows, about 131 KB</b>, and the earliest
    /// quarter anywhere <b>1998-09-30</b>; Berkshire's 53 is where Berkshire's history begins at FMP (2013 Q2,
    /// shared by eleven other filers in that sample), not how far the endpoint reaches. There is no
    /// per-quarter variant to offer.</para>
    ///
    /// <para>No <c>limit</c> either: the endpoint ignores it, alone (<c>limit=5</c> answered all 110 of FMR's
    /// rows) and beside <c>page</c> (<c>page=100&amp;limit=5</c> answered 10 — the offset applied, the limit
    /// not).</para>
    ///
    /// <para><b>No <c>page</c>, and this one is a deliberate omission rather than an absent parameter — FMP reads
    /// <c>page</c> here as a ROW OFFSET, not a page index.</b> Measured 2026-09-01 (#46) on the same Berkshire
    /// CIK: pages 0, 1, 2 and 5 answered <b>53, 52, 51 and 48</b> rows, each starting one row later than the
    /// last. So <c>page=n</c> skips <i>n</i> rows and returns everything after them, and a caller looping pages
    /// the ordinary way re-reads 52 of the 53 rows page 0 already gave them, accumulating <i>n</i>(<i>n</i>+1)/2
    /// duplicates instead of reaching more data. Offering it under the name FMP uses would be worse than
    /// offering nothing.</para>
    ///
    /// <para><b>Nor under an honest name.</b> Measured 2026-09-01 (#53) at the far end, on FMR's 110 rows:
    /// <c>page=n</c> answers exactly the plain response's rows <i>n</i> onward — equal row for row at 1, 50 and
    /// 109 — and 110, 111 and 1000 answer <c>[]</c> with HTTP 200. The offset reaches nothing the plain call
    /// does not already return, and the whole history arrives in one response every time, so there is nothing
    /// to page: <c>Skip(n)</c> on the returned list is the identical operation without the second request.
    /// See <c>docs/superpowers/specs/2026-09-01-holder-performance-paging-measurements.md</c>.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter the filer has reported, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderPerformance>> GetHolderPerformanceAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/holder-performance-summary").With("cik", cik),
            FmpJsonContext.Default.ListHolderPerformance, ct);
    }

    /// <summary>What every 13F filer together reported about one symbol in one quarter, or
    /// <see langword="null"/> when FMP has nothing —
    /// <c>stable/institutional-ownership/symbol-positions-summary</c>.
    ///
    /// <para><b>One row, unwrapped from the array FMP sends.</b> The path answers a JSON array that carried
    /// exactly one element for every symbol measured 2026-08-28; its 36 fields are whole-market aggregates for
    /// the symbol and quarter rather than per-filer rows, so a list return would make every caller write
    /// <c>[0]</c>. Unwrapped the way <see cref="SecFilingsEndpoints.GetProfileAsync"/> does it.</para>
    ///
    /// <para><b><see cref="SymbolPositions.OwnershipPercent"/> can exceed 100</b>, legitimately — read its
    /// documentation before treating it as a fraction.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The quarter's aggregates, or <see langword="null"/> when FMP has none — which is what an
    /// unknown symbol or an unfiled quarter answers, with HTTP 200 rather than a 404.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<SymbolPositions?> GetSymbolPositionsAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfQuarterOutOfRange(quarter);

        return await transport.GetSingleAsync(
            new FmpRequest("stable/institutional-ownership/symbol-positions-summary")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListSymbolPositions, ct).ConfigureAwait(false);
    }

    /// <summary>The largest page <see cref="GetLatestFilingsAsync"/> and
    /// <see cref="GetBeneficialOwnershipAsync"/> will ask for, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="SecFilingsEndpoints.MaxSecFilingPageSize"/>: measured 2026-08-28,
    /// <c>institutional-ownership/latest?limit=2000</c> answered exactly 1,000 rows with HTTP 200 and nothing
    /// in the body to say the request had been trimmed. The feed paginates, so a caller who asks for 2,000 and
    /// advances <c>page</c> by 2,000 reads half the archive and is never told.</para>
    ///
    /// <para><b>Not the cap for <see cref="GetHolderAnalyticsAsync"/></b>, which clamps at 100 — see
    /// <see cref="MaxHolderAnalyticsPageSize"/>. One constant for the whole group would have let a caller ask
    /// that path for 1,000 rows and receive 100 in silence.</para>
    ///
    /// <para><b>On <see cref="GetBeneficialOwnershipAsync"/> this is a sibling-derived bound rather than a
    /// measured one.</b> No query on that path produced a result set large enough to provoke a clamp — the
    /// widest found was 180 rows, and <c>limit=2000</c> for AAPL answered its whole 99-row set. The guard is
    /// applied there because an unbounded <c>limit</c> is worse than a conservative one, not because 1,000 was
    /// observed to be its ceiling.</para></summary>
    public const int MaxOwnershipPageSize = 1000;

    /// <summary>Total 13F-reported value by industry for one quarter, across the whole market —
    /// <c>stable/institutional-ownership/industry-summary</c>.
    ///
    /// <para>394 rows per quarter, measured 2026-08-28, one per SIC industry. Takes no filer and no symbol: it
    /// is the market's whole 13F universe cut one way.</para>
    ///
    /// <para><b>This is the path whose values are fractional</b> — 53 of those 394 rows — which is why every
    /// money field in this group is <c>decimal?</c>. See
    /// <see cref="IndustryOwnershipSummary.IndustryValue"/>.</para></summary>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryOwnershipSummary>> GetIndustrySummaryAsync(
        int year, int quarter, CancellationToken ct = default)
    {
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/industry-summary")
                .With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListIndustryOwnershipSummary, ct);
    }

    /// <summary>The whole-market feed of 13F filings as they arrive, newest first —
    /// <c>stable/institutional-ownership/latest</c>.
    ///
    /// <para>Every filer, every quarter, new submissions and amendments alike:
    /// <see cref="InstitutionalFiling.FormType"/> carried <c>13F-HR</c>, <c>13F-HR/A</c>, <c>13F-NT</c> and
    /// <c>13F-NT/A</c> in the measured page. Use it to notice that a filer has reported; use
    /// <see cref="GetHoldingsAsync"/> to read what they reported.</para>
    ///
    /// <para><b>The two dates on the row are spelled differently from the rest of this group and mean different
    /// things.</b> See <see cref="InstitutionalFiling.FilingDate"/> and
    /// <see cref="InstitutionalFiling.AcceptedDate"/>.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxOwnershipPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxOwnershipPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InstitutionalFiling>> GetLatestFilingsAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxOwnershipPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInstitutionalFiling, ct);
    }

    /// <summary>SC 13D/G disclosures for one issuer — who has crossed 5% of a class, and with what voting and
    /// dispositive power — <c>stable/acquisition-of-beneficial-ownership</c>.
    ///
    /// <para><b>FMP documents this under Insider Trades. It is here because it is not an insider
    /// transaction</b> — the reporting person is an institution and the subject is a stake. See
    /// <see cref="BeneficialOwnership"/>.</para>
    ///
    /// <para><b><paramref name="limit"/> and no <c>page</c>, and both halves are measured.</b> The endpoint
    /// honours <c>limit</c>; it ignores <c>page</c> — <c>page=0</c> and <c>page=1</c> returned byte-identical
    /// bodies on 2026-08-28. Honouring one does not predict honouring the other, so each was measured
    /// separately and only the one that works is offered.</para>
    ///
    /// <para>Historical as well as current: the captured AAPL response spans 2015 to 2026 in 99 rows.</para></summary>
    /// <param name="symbol">The issuer's ticker, as FMP spells it.</param>
    /// <param name="limit">Rows to return, 1 to <see cref="MaxOwnershipPageSize"/>. <b>The upper bound is
    /// derived from this path's siblings rather than measured on it</b> — see
    /// <see cref="MaxOwnershipPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The disclosures, newest first. Never <see langword="null"/>; empty for an unknown symbol, not
    /// an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxOwnershipPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BeneficialOwnership>> GetBeneficialOwnershipAsync(
        string symbol, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxOwnershipPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/acquisition-of-beneficial-ownership")
                .With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListBeneficialOwnership, ct);
    }

    /// <summary>Rejects a quarter FMP could only answer with an error.
    ///
    /// <para>Five methods on this class take a quarter and all five require it: measured 2026-08-28, omitting it
    /// answers <c>400 … missing query parameter - quarter</c> on every one. The range is the calendar's, not a
    /// measured cap — there is no fifth quarter to measure.</para>
    ///
    /// <para>The parameter is named <c>quarter</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentException.ParamName"/>.</para></summary>
    private static void ThrowIfQuarterOutOfRange(int quarter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quarter, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quarter, 4);
    }
}
