using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>The period-shaped fundamentals endpoints: the three statements plus the four derived sets.
///
/// <para>All seven take the same three arguments and answer newest period first. They are grouped here because
/// they share one query shape, not because FMP groups them — FMP splits them across its Statements, Ratios and
/// Metrics sections.</para>
///
/// <para><see cref="GetScoresAsync"/> is the exception and does not go through that shared shape: it takes no
/// period and no limit, and answers one row or none. It lives here because its figures come off the same
/// statements, not because it is period-shaped.</para></summary>
public sealed class StatementEndpoints(FmpTransport transport)
{
    /// <summary>Income statements for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IncomeStatement>> GetIncomeStatementAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement", symbol, period, limit),
            FmpJsonContext.Default.ListIncomeStatement, ct);

    /// <summary>Balance sheets for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<BalanceSheetStatement>> GetBalanceSheetAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/balance-sheet-statement", symbol, period, limit),
            FmpJsonContext.Default.ListBalanceSheetStatement, ct);

    /// <summary>Cash flow statements for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CashFlowStatement>> GetCashFlowAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/cash-flow-statement", symbol, period, limit),
            FmpJsonContext.Default.ListCashFlowStatement, ct);

    /// <summary>Financial ratios for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FinancialRatios>> GetRatiosAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/ratios", symbol, period, limit),
            FmpJsonContext.Default.ListFinancialRatios, ct);

    /// <summary>Key metrics for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<KeyMetrics>> GetKeyMetricsAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/key-metrics", symbol, period, limit),
            FmpJsonContext.Default.ListKeyMetrics, ct);

    /// <summary>Period-on-period growth rates for one symbol, newest first.</summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FinancialGrowth>> GetFinancialGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/financial-growth", symbol, period, limit),
            FmpJsonContext.Default.ListFinancialGrowth, ct);

    /// <summary>Enterprise value bridges for one symbol, newest first.
    ///
    /// <para><paramref name="period"/> is honoured — asking for <see cref="FiscalPeriod.Quarter"/> returns quarter
    /// ends — but unlike the other six, the rows come back with no <c>period</c> field to say so. Two consequences,
    /// both measured against AAPL on 2026-08-26: the caller must remember which series it asked for, and
    /// <c>(symbol, date)</c> is <b>not</b> a unique key across both, because a Q4 end and a fiscal year end are the
    /// same day — <c>2025-09-27</c> appears in the annual series and the quarterly one.</para></summary>
    /// <param name="symbol">Ticker, in FMP's spelling. Class-share tickers are hyphenated (<c>BRK-B</c>).</param>
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<EnterpriseValues>> GetEnterpriseValuesAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/enterprise-values", symbol, period, limit),
            FmpJsonContext.Default.ListEnterpriseValues, ct);

    /// <summary>Altman Z and Piotroski F for one symbol, or null when FMP has no scores for it.
    ///
    /// <para>Single record rather than a list, and <paramref name="symbol"/> is the only parameter: measured 2026-08-26,
    /// <c>stable/financial-scores</c> answers a single-element array and takes neither <c>period</c> nor
    /// <c>limit</c> — which is why this one does not go through the shared periodic query shape the other seven
    /// use. Sending parameters the endpoint does not accept is not free.</para>
    ///
    /// <para>Null covers two different situations the response cannot tell apart, because both arrive as
    /// <c>[]</c> with HTTP 200 rather than a 404: FMP knows no such symbol, and the scores do not apply to this
    /// security. <c>SPY</c> measured <c>[]</c> — both scores are built from issuer accounts an ETF does not file,
    /// so every ETF is expected to answer null here rather than to fail.</para>
    ///
    /// <para>The row carries no date, no period and no fiscal year; see <see cref="FinancialScores"/> for what
    /// that costs a caller who wants to store it.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<FinancialScores?> GetScoresAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/financial-scores").With("symbol", symbol),
            FmpJsonContext.Default.ListFinancialScores, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>The <c>limit</c> the SDK sends when the caller asks for no limit, and the reason it sends one.
    ///
    /// <para><b>Without it FMP returns five rows.</b> Measured 2026-08-27, every per-symbol paged path in this
    /// group has an undocumented default of 5: <c>stable/income-statement</c> for AAPL answered 5 rows of a
    /// 41-row annual history, <c>cash-flow-statement</c> 5 of 37, and so on across all seven of the paths this
    /// SDK shipped before that measurement. A well-formed HTTP 200 array of five rows is indistinguishable from a
    /// complete one, so a caller asking for a company's history got 12% of it and nothing said so.</para>
    ///
    /// <para>100,000 rather than the deepest history found is headroom rather than a guess. The deepest series
    /// measured was <c>income-statement-ttm</c> at 164 rows back to 1985-09-30, and the ceiling was probed:
    /// <c>limit=1000</c>, <c>limit=10000</c> and <c>limit=100000</c> all returned the same true total, so there is
    /// no server-side cap between them and asking for more costs nothing. The precedent is
    /// <see cref="DirectoryEndpoints.SymbolChangeRequestLimit"/>, which exists for exactly this failure.</para>
    ///
    /// <para><b>One endpoint in this group caps below any limit you send.</b> <c>owner-earnings</c> stops at 50
    /// rows regardless — see <c>MaxOwnerEarningsRows</c>.</para></summary>
    public const int FullHistoryLimit = 100_000;

    /// <summary>The one query shape all seven share. Written once so the seven cannot drift apart.</summary>
    private static FmpRequest Periodic(string path, string symbol, FiscalPeriod period, int? limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("period", period.ToQueryValue())
            // `limit ?? FullHistoryLimit`, not `limit` — a null limit means "all of it", and FMP reads a missing
            // limit as 5. See FullHistoryLimit.
            .With("limit", limit ?? FullHistoryLimit);
    }
}
