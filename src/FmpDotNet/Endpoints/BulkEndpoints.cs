using System.Net;
using System.Runtime.CompilerServices;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Bulk</c> group — whole-universe CSV downloads.
///
/// <para>Every method here streams. Measured 2026-08-26, one bulk response reaches 69 MB and three of them send no
/// <c>Content-Length</c>, so there is no size at which buffering is safe.</para>
///
/// <para>These endpoints are throttled separately from the account's per-minute cap and much more tightly — a
/// second call moments after the first was already refused, and FMP's own error text warns that frequent use may
/// get the key restricted. The data behind them refreshes only once every few hours, so treat a successful
/// download as something to cache, not something to repeat.</para>
///
/// <para><b>Plan gating on this surface is not settled.</b> <c>profile-bulk</c> was recorded as 402-on-Premium by
/// the application this SDK replaces and answered 200 when re-probed on 2026-08-26. A 402 or 403 arrives as
/// <see cref="FmpPlanRestrictedException"/> on the first <c>MoveNextAsync</c> rather than as an empty stream, and
/// that asymmetry with <c>CompanyEndpoints.TryGetAllSharesFloatAsync</c> — which returns null for the same status —
/// is deliberate. An empty stream would be indistinguishable from a genuinely empty universe, and "a paywalled
/// endpoint reading as an empty result" is the exact defect the caller-side history records. Catch the exception to
/// fall back to the per-symbol path; do not infer entitlement from a row count.</para>
///
/// <para><b>Corrected 2026-09-02 (#45): "not settled" rested on a misreading.</b> The predecessor's own record has
/// <c>profile-bulk</c> at 402 on a <i>free</i> key (2026-07-23) and again on a plan its specs do not name, and the
/// 2026-08-26 re-probe that answered 200 was on this repository's Ultimate key. A 402 below a tier and a 200 on it
/// is the ladder as described, not gating that moved. The handling above stands on its own — a refusal must arrive
/// as an exception — but the surface is not unsettled; it is, on every record there is, Ultimate-only. See
/// <c>docs/superpowers/specs/2026-09-02-plan-tier-provenance.md</c>.</para>
///
/// <para><b>Plan tier — Ultimate, second-hand.</b> fmpsdk 20260824.0, the independent client this SDK is
/// cross-checked against, recorded every path in this class as 402 on free, Starter and Premium and working on
/// Ultimate on 2026-08-24. Not verified here: every path answered 200 on the Ultimate key this SDK is measured with
/// (2026-08-27), which says nothing about the plans below it. A dated observation, not a contract — catch
/// <see cref="FmpPlanRestrictedException"/> rather than gating on it.</para></summary>
public sealed class BulkEndpoints(FmpBulkTransport transport)
{
    /// <summary>Streams FMP's letter rating and component scores for every company it covers. From
    /// <c>stable/rating-bulk</c> — 45,008 rows and 1.8 MB measured 2026-08-26.
    ///
    /// <para>The letter scale runs above <c>A+</c>: see <see cref="BulkCompanyRating.Rating"/>.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkCompanyRating> StreamRatingsAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/rating-bulk"), BulkCompanyRating.FromCsv, ct);

    /// <summary>Streams FMP's discounted-cash-flow valuation beside the market price, for every company it
    /// covers. From <c>stable/dcf-bulk</c> — 33,583 rows and 1.6 MB measured 2026-08-26.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkDiscountedCashFlow> StreamDiscountedCashFlowsAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/dcf-bulk"), BulkDiscountedCashFlow.FromCsv, ct);

    /// <summary>Streams Altman Z and Piotroski F scores for every company FMP covers. From
    /// <c>stable/scores-bulk</c> — 62,339 rows and 6.7 MB measured 2026-08-26.
    ///
    /// <para>Rows map to <see cref="FinancialScores"/>, the same type <c>Statements.GetScoresAsync</c> returns:
    /// the CSV carries exactly the same 11 names, verified against the header.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<FinancialScores> StreamScoresAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/scores-bulk"), FinancialScores.FromCsv, ct);

    /// <summary>Streams every company's peer group. From <c>stable/peers-bulk</c> — 82,930 rows and 6.5 MB
    /// measured 2026-08-26, the widest symbol coverage of any endpoint the SDK models.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkPeers> StreamPeersAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/peers-bulk"), BulkPeers.FromCsv, ct);

    /// <summary>Streams one <paramref name="part"/> of every ETF's holdings. From
    /// <c>stable/etf-holder-bulk</c>.
    ///
    /// <para><b>This is the largest response the SDK models, by a wide margin.</b> Measured 2026-08-26,
    /// <c>part=0</c> alone was <b>298,693,192 bytes over 2,571,137 rows</b> covering 4,610 ETFs. Buffering it is
    /// not an option and neither is materialising it — <c>ToListAsync</c> on this would be millions of records
    /// live at once.</para></summary>
    /// <param name="part">Zero-based part index. An out-of-range part answers HTTP 400.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the part is out of range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkEtfHolding> StreamEtfHoldingsAsync(int part, CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/etf-holder-bulk").With("part", part.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            BulkEtfHolding.FromCsv, ct);

    /// <summary>Streams every part of <c>stable/etf-holder-bulk</c>, walking until FMP refuses the next part.
    ///
    /// <para><b>Consider carefully before calling this.</b> Part 0 alone is 298 MB and 2.57 million rows; the
    /// whole walk is an unknown multiple of that, downloaded through a throttle deliberately set to a trickle.
    /// If you want one fund's holdings, this is the wrong call.</para>
    ///
    /// <para>See <see cref="WalkPartsAsync"/> for how the walk decides it has finished, which is a heuristic
    /// rather than a contract.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or part 0 was rejected.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkEtfHolding> StreamAllEtfHoldingsAsync(CancellationToken ct = default) =>
        WalkPartsAsync(StreamEtfHoldingsAsync, ct);

    /// <summary>Streams trailing-twelve-month key metrics for every company FMP covers. From
    /// <c>stable/key-metrics-ttm-bulk</c> — 71,500 rows and 44.0 MB measured 2026-08-26.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<KeyMetricsTtm> StreamKeyMetricsTtmAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/key-metrics-ttm-bulk"), KeyMetricsTtm.FromCsv, ct);

    /// <summary>Streams trailing-twelve-month ratios for every company FMP covers. From
    /// <c>stable/ratios-ttm-bulk</c> — 71,504 rows and 69.5 MB measured 2026-08-26.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<RatiosTtm> StreamRatiosTtmAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(new FmpRequest("stable/ratios-ttm-bulk"), RatiosTtm.FromCsv, ct);

    /// <summary>Streams one fiscal period of income statements for every company FMP covers. From
    /// <c>stable/income-statement-bulk</c> — 43,124 rows and 14.0 MB measured 2026-08-26 for 2025 Q1.
    ///
    /// <para>Rows map to <see cref="IncomeStatement"/>, the same type the per-symbol endpoint returns. That is
    /// not a shortcut: the CSV header was compared field by field against the model on 2026-08-26 and the two
    /// carry exactly the same 39 names.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised —
    /// an unknown <c>period</c> answers HTTP 400.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<IncomeStatement> StreamIncomeStatementsAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/income-statement-bulk", year, period), IncomeStatement.FromCsv, ct);

    /// <summary>Streams one fiscal period of income-statement growth for every company FMP covers. From
    /// <c>stable/income-statement-growth-bulk</c> — 43,135 rows and 21.3 MB measured 2026-08-26 for 2025 Q1.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<IncomeStatementGrowth> StreamIncomeStatementGrowthAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/income-statement-growth-bulk", year, period), IncomeStatementGrowth.FromCsv, ct);

    /// <summary>Streams one fiscal period of balance sheets for every company FMP covers. From
    /// <c>stable/balance-sheet-statement-bulk</c> — 42,353 rows and 19.7 MB measured 2026-08-26 for 2025 Q1.
    ///
    /// <para>Rows map to <see cref="BalanceSheetStatement"/>; the CSV carries exactly the same 61 names as the
    /// per-symbol model, verified against the header.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BalanceSheetStatement> StreamBalanceSheetsAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/balance-sheet-statement-bulk", year, period), BalanceSheetStatement.FromCsv, ct);

    /// <summary>Streams one fiscal period of balance-sheet growth for every company FMP covers. From
    /// <c>stable/balance-sheet-statement-growth-bulk</c> — 42,361 rows and 29.1 MB measured 2026-08-26 for 2025 Q1.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BalanceSheetGrowth> StreamBalanceSheetGrowthAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/balance-sheet-statement-growth-bulk", year, period), BalanceSheetGrowth.FromCsv, ct);

    /// <summary>Streams one fiscal period of cash flow statements for every company FMP covers. From
    /// <c>stable/cash-flow-statement-bulk</c> — 41,697 rows and 12.5 MB measured 2026-08-26 for 2025 Q1.
    ///
    /// <para>Rows map to <see cref="CashFlowStatement"/>; the CSV carries exactly the same 47 names as the
    /// per-symbol model, verified against the header.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<CashFlowStatement> StreamCashFlowsAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/cash-flow-statement-bulk", year, period), CashFlowStatement.FromCsv, ct);

    /// <summary>Streams one fiscal period of cash-flow growth for every company FMP covers. From
    /// <c>stable/cash-flow-statement-growth-bulk</c> — 41,706 rows and 17.0 MB measured 2026-08-26 for 2025 Q1.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call, or the period was not recognised.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<CashFlowGrowth> StreamCashFlowGrowthAsync(
        int year, BulkFiscalPeriod period, CancellationToken ct = default) =>
        transport.StreamCsvAsync(Periodic("stable/cash-flow-statement-growth-bulk", year, period), CashFlowGrowth.FromCsv, ct);

    /// <summary>The <c>year</c> + <c>period</c> query the six statement-family bulk endpoints share.</summary>
    private static FmpRequest Periodic(string path, int year, BulkFiscalPeriod period) =>
        new FmpRequest(path)
            .With("year", year.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .With("period", period.ToQueryValue());

    /// <summary>Streams every symbol's analyst price-target summary. From <c>stable/price-target-summary-bulk</c>,
    /// which takes no parameters and answers the whole covered universe in one response — 5,277 rows and 314 kB
    /// measured 2026-08-26.
    ///
    /// <para>Read <see cref="BulkPriceTargetSummary"/> before using the averages: no field is ever blank, so a
    /// window with no coverage arrives as a zero count and a zero average rather than as null.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkPriceTargetSummary> StreamPriceTargetSummariesAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/price-target-summary-bulk"),
            BulkPriceTargetSummary.FromCsv, ct);

    /// <summary>Streams every symbol's analyst rating distribution and consensus label. From
    /// <c>stable/upgrades-downgrades-consensus-bulk</c>, which takes no parameters — 13,363 rows and 326 kB
    /// measured 2026-08-26.
    ///
    /// <para>This universe is global and symbol-ordered, so the first rows are Shenzhen and Hong Kong listings
    /// rather than US ones. It is two and a half times the row count of
    /// <see cref="StreamPriceTargetSummariesAsync"/>.</para></summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkAnalystConsensus> StreamAnalystConsensusAsync(CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/upgrades-downgrades-consensus-bulk"),
            BulkAnalystConsensus.FromCsv, ct);

    /// <summary>Streams every earnings result reported in <paramref name="year"/>, against its estimate. From
    /// <c>stable/earnings-surprises-bulk</c> — 65,945 rows and 3.1 MB measured 2026-08-26 for 2025.
    ///
    /// <para><b>Symbol and date together do not identify a row</b>: 210 pairs repeated within the measured year.
    /// See <see cref="BulkEarningsSurprise"/> before storing these under a unique index.</para></summary>
    /// <param name="year">The calendar year to fetch. FMP selects on the reported date, so a fiscal quarter
    /// ending in January lands in that January's year.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkEarningsSurprise> StreamEarningsSurprisesAsync(int year, CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/earnings-surprises-bulk").With("year", year.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            BulkEarningsSurprise.FromCsv, ct);

    /// <summary>Streams end-of-day bars for every symbol FMP covers on <paramref name="date"/>.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkEndOfDayPrice> StreamEndOfDayAsync(LocalDate date, CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/eod-bulk").With("date", date),
            BulkEndOfDayPrice.FromCsv, ct);

    /// <summary>Streams one <paramref name="part"/> of <c>stable/profile-bulk</c> — the whole-universe company
    /// profile download, in CSV.
    ///
    /// <para><b>Why this streams and cannot be buffered.</b> Measured 2026-08-26, <c>part=0</c> answered
    /// <b>30,467,596 bytes</b> across 22,857 lines with <b>no <c>Content-Length</c></b> — the response is chunked,
    /// so nothing can pre-size a buffer and no threshold exists at which "small enough to buffer" could be decided.
    /// The 36 columns are mostly one field: <see cref="BulkCompanyProfile.Description"/> runs past 1,500 characters
    /// per row. A caller that wants only the classification fields still pays to stream the descriptions past,
    /// which is why the per-symbol <c>stable/profile</c> remains the right call for a handful of
    /// symbols.</para>
    ///
    /// <para><b>The caller supplies the part.</b> There is no total to ask for: FMP publishes no part count, no
    /// <c>Link</c> header and no terminator row, and a part that does not exist answers a 400 rather than an empty
    /// body — see <see cref="StreamAllProfilesAsync"/>, which walks the parts on a documented heuristic. This method
    /// makes no guess: it fetches exactly the part named and lets every failure through unchanged, so it is the
    /// place to go when you need to see why a part was refused.</para>
    ///
    /// <para><b>Rows are not in symbol order.</b> Measured <c>part=0</c> opens <c>PRTA, PRDO, MRV.TO</c> — a
    /// Nasdaq biotech, a Nasdaq education company and a Toronto listing — so a part is a shard of the universe with
    /// no ordering a caller can exploit, and part 0 is not "the first symbols alphabetically". Compare
    /// <c>shares-float-all</c>, whose pages <i>are</i> symbol-ordered and whose page 0 is therefore all Shenzhen.
    /// Neither is a sample of the universe; both need every page read.</para></summary>
    /// <param name="part">Zero-based part index. Parts 0 and 1 were measured to exist on 2026-08-26; 99 did
    /// not.</param>
    /// <param name="ct">Cancels the download mid-stream.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="part"/> is negative — checked before a request
    /// is spent, since the bulk throttle makes a wasted call expensive.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403 — see the note on the type about why
    /// this is an exception here and a null elsewhere.</exception>
    /// <exception cref="FmpApiException">FMP refused the request. Two distinguishable cases, told apart by
    /// <see cref="FmpApiException.StatusCode"/>: a <see cref="HttpStatusCode.BadRequest"/> carrying
    /// <c>Query Error: Invalid or missing query parameter - part</c> means the part index is out of range, while a
    /// null status means the bulk throttle refused the call — which arrives as HTTP 200 with a JSON error body, not
    /// as a 429.</exception>
    public IAsyncEnumerable<BulkCompanyProfile> StreamProfilesAsync(int part, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(part);
        return transport.StreamCsvAsync(
            new FmpRequest("stable/profile-bulk").With("part", part),
            BulkCompanyProfile.FromCsv, ct);
    }

    /// <summary>Walks <c>stable/profile-bulk</c> from part 0 upwards and streams every row of every part as one
    /// sequence.
    ///
    /// <para><b>The termination rule is a heuristic, and this is the paragraph that says so.</b> FMP gives no way
    /// to ask how many parts there are and no empty-response terminator: measured 2026-08-26, <c>part=0</c> and
    /// <c>part=1</c> both answered HTTP 200 with data, and <c>part=99</c> answered <b>HTTP 400</b> with the
    /// plain-text body <c>Query Error: Invalid or missing query parameter - part</c>. So the only signal that the
    /// parts have run out is an error status — and a 400 saying "invalid or missing query parameter" could equally
    /// mean the parameter was malformed. This walk is entitled to read it as "past the last part" for one reason
    /// only: <b>the SDK controls the value it sent.</b> <paramref name="ct"/> aside, the sole query parameter is a
    /// non-negative integer this method generated, so "malformed" is not a live possibility for the request it
    /// actually made. If FMP ever changes <c>part</c>'s spelling or adds a required companion parameter, that
    /// reasoning fails and this method will report an empty universe instead of an error. That is the risk being
    /// accepted, and it is why <see cref="StreamProfilesAsync"/> exists alongside it.</para>
    ///
    /// <para>Two guards narrow it:</para>
    /// <list type="bullet">
    /// <item><description>A 400 on <b>part 0</b> is <i>not</i> swallowed — it is rethrown. Part 0 was measured to
    /// exist, so a 400 there cannot mean "past the last part" and almost certainly means the request shape changed.
    /// Only a 400 on a part after at least one has been read ends the walk.</description></item>
    /// <item><description>A part that yields <b>zero data rows</b> also ends the walk. Nothing measured behaves that
    /// way; it is there so that an upstream that starts answering 200-with-header-only cannot spin this into an
    /// unbounded loop against an endpoint whose throttle is measured in calls per hour.</description></item>
    /// </list>
    ///
    /// <para><b>Every other failure propagates.</b> Plan gating (402/403), rate limiting (429) and the bulk
    /// throttle's HTTP-200-with-a-JSON-body all surface as their own exceptions mid-walk, so a partial result is
    /// never silently returned as a complete one. Because the bulk throttle refuses calls made moments apart, a
    /// caller pacing this walk itself — rather than letting it run flat out — is the difference between finishing
    /// and being refused on part 1.</para></summary>
    /// <param name="ct">Cancels the walk between parts as well as mid-part.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Bulk is the most plan-gated part
    /// of the API, so expect this — and read <see cref="FmpPlanRestrictedException.StatusCode"/> before
    /// reporting it as a plan limit, because 403 points at the key at least as often.</exception>
    public IAsyncEnumerable<BulkCompanyProfile> StreamAllProfilesAsync(CancellationToken ct = default) =>
        WalkPartsAsync(StreamProfilesAsync, ct);

    /// <summary>Walks a <c>part</c>-paged bulk endpoint from part 0 until it runs out.
    ///
    /// <para><b>Termination is a heuristic, because the endpoint family gives nothing better.</b> An out-of-range
    /// part answers <b>HTTP 400</b> with a plain-text body under a <c>content-type</c> of <c>application/json</c>
    /// that is a lie; there is no empty-response terminator and no count of parts anywhere. So a 400 ends the
    /// walk — except on part 0, where it is rethrown, since a 400 on the very first request means the request
    /// itself was wrong rather than the universe being exhausted. A part that yields no rows also ends it.</para>
    ///
    /// <para>Shared by <see cref="StreamAllProfilesAsync"/> and <see cref="StreamAllEtfHoldingsAsync"/>. The
    /// logic is small but easy to get subtly wrong — swallowing the part-0 case, or disposing the enumerator on
    /// the wrong path — and two copies would be two places to get it wrong.</para></summary>
    private static async IAsyncEnumerable<T> WalkPartsAsync<T>(
        Func<int, CancellationToken, IAsyncEnumerable<T>> part,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var index = 0; ; index++)
        {
            var rows = 0;
            var exhausted = false;
            var enumerator = part(index, ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (FmpApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && index > 0)
                    {
                        exhausted = true;
                        break;
                    }
                    if (!moved) break;
                    rows++;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (exhausted || rows == 0) yield break;
        }
    }
}
