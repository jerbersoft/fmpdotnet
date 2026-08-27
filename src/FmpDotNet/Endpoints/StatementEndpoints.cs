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

    /// <summary>Rolling-twelve-month income statements for one symbol, newest first. From
    /// <c>stable/income-statement-ttm</c>.
    ///
    /// <para><b>The same 39 fields as <see cref="GetIncomeStatementAsync"/>, on a different clock.</b> The wire
    /// field set was compared key by key on 2026-08-27 and is identical, so this reuses
    /// <see cref="IncomeStatement"/> rather than declaring a near-duplicate record. Each row covers the twelve
    /// months ending at the <c>date</c> on it, so consecutive rows OVERLAP by nine months — summing them
    /// quadruples the revenue. The plain statement is the one to sum.</para>
    ///
    /// <para><b>No <c>period</c> parameter, deliberately.</b> The endpoint accepts one and ignores it, measured
    /// 2026-08-27: the answer is always quarterly-stepped and newest-first from the latest quarter. This is the
    /// deepest series in the group — AAPL returned 164 rows back to 1985-09-30.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it. Class shares need the hyphenated form (<c>BRK-B</c>).</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/>, because FMP reads an omitted limit as 5.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol — which answers <c>[]</c> at HTTP 200 rather
    /// than a 404. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IncomeStatement>> GetIncomeStatementTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/income-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListIncomeStatement, ct);

    /// <summary>Rolling-twelve-month balance sheets for one symbol, newest first. From
    /// <c>stable/balance-sheet-statement-ttm</c>.
    ///
    /// <para><b>Sixty of <see cref="BalanceSheetStatement"/>'s 61 fields.</b>
    /// <see cref="BalanceSheetStatement.CapitalLeaseObligationsNonCurrent"/> is <b>never</b> sent on this path and
    /// therefore always binds null — measured 2026-08-27 across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B, KO, GE and
    /// MSFT, where the TTM row carried exactly 60 keys every time and the plain balance sheet carried the 61st
    /// for all ten. It is structural, not a sparse filer, and null here is an absence rather than a zero.</para>
    ///
    /// <para>A rolling balance sheet is a stranger object than a rolling income statement — a balance sheet is
    /// already a point in time — so read these as "the balance sheet as at the end of each trailing twelve-month
    /// window", which is the quarter end, not an average over the year.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BalanceSheetStatement>> GetBalanceSheetTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/balance-sheet-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListBalanceSheetStatement, ct);

    /// <summary>Rolling-twelve-month cash flow statements for one symbol, newest first. From
    /// <c>stable/cash-flow-statement-ttm</c>.
    ///
    /// <para>The same 47 fields as <see cref="GetCashFlowAsync"/>, compared key by key on 2026-08-27 and
    /// identical, so this reuses <see cref="CashFlowStatement"/>. Consecutive rows overlap by nine months; do not
    /// sum them.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CashFlowStatement>> GetCashFlowTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/cash-flow-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListCashFlowStatement, ct);

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

    /// <summary>Period-over-period growth of one income statement, newest first. From
    /// <c>stable/income-statement-growth</c>.
    ///
    /// <para><b>Not the same fields as <see cref="GetFinancialGrowthAsync"/>.</b> That path answers FMP's own
    /// summary growth set; this one answers a growth rate for every line of the income statement, 34 fields
    /// whose names are the upstream's own — typos included. See <see cref="IncomeStatementGrowth"/>.</para>
    ///
    /// <para>Every figure is a <b>fraction, not a percentage</b>: 0.12 is twelve percent. FMP sends 0 where the
    /// prior period was zero or absent, so a zero cannot be told apart from "no prior period to grow
    /// from".</para>
    ///
    /// <para>The model is shared with <c>stable/income-statement-growth-bulk</c>: the JSON and CSV field sets
    /// were compared name by name on 2026-08-27 and are identical.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IncomeStatementGrowth>> GetIncomeStatementGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListIncomeStatementGrowth, ct);

    /// <summary>Period-over-period growth of one balance sheet, newest first — 56 fields. From
    /// <c>stable/balance-sheet-statement-growth</c>.
    ///
    /// <para>Fractions, not percentages, with the same zero-means-two-things caveat as
    /// <see cref="GetIncomeStatementGrowthAsync"/>. Model shared with the bulk CSV form; field sets compared name
    /// by name on 2026-08-27 and identical.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BalanceSheetGrowth>> GetBalanceSheetGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/balance-sheet-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListBalanceSheetGrowth, ct);

    /// <summary>Period-over-period growth of one cash flow statement, newest first — 42 fields. From
    /// <c>stable/cash-flow-statement-growth</c>.
    ///
    /// <para>Fractions, not percentages. Model shared with the bulk CSV form; field sets compared name by name on
    /// 2026-08-27 and identical, <b>including FMP's spelling of
    /// <c>growthNetCashProvidedByOperatingActivites</c></b> — one letter short of <c>Activities</c>. The C#
    /// property corrects it; the wire name does not.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CashFlowGrowth>> GetCashFlowGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/cash-flow-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListCashFlowGrowth, ct);

    /// <summary>Trailing-twelve-month key metrics for one symbol, or null when FMP has none. From
    /// <c>stable/key-metrics-ttm</c>.
    ///
    /// <para><b>One row, and <paramref name="symbol"/> is the only parameter.</b> Measured 2026-08-27, this path
    /// answers a single-element array and ignores both <c>period</c> and <c>limit</c>, so neither is sent — the
    /// same reasoning <see cref="GetScoresAsync"/> follows.</para>
    ///
    /// <para><b>There is no date on this response of any kind.</b> It describes the twelve months ending whenever
    /// FMP last recomputed it, which the payload does not say. Two calls days apart are not comparable as a time
    /// series and nothing in the data will tell you they differ — whoever stores this has to stamp it at fetch
    /// time. See <see cref="KeyMetricsTtm"/>.</para>
    ///
    /// <para>Null means FMP answered <c>[]</c> at HTTP 200, which covers both "no such symbol" and "not
    /// applicable to this security" and cannot distinguish them.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The snapshot, or <see langword="null"/> when FMP answered an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<KeyMetricsTtm?> GetKeyMetricsTtmAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/key-metrics-ttm").With("symbol", symbol),
            FmpJsonContext.Default.ListKeyMetricsTtm, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Trailing-twelve-month financial ratios for one symbol, or null when FMP has none. From
    /// <c>stable/ratios-ttm</c>.
    ///
    /// <para>The twin of <see cref="GetKeyMetricsTtmAsync"/> and carries the same three caveats: one row, no
    /// <c>period</c> or <c>limit</c> (both accepted and ignored, measured 2026-08-27), and <b>no date field</b>,
    /// so two calls days apart are not a series. See <see cref="RatiosTtm"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The snapshot, or <see langword="null"/> when FMP answered an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<RatiosTtm?> GetRatiosTtmAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ratios-ttm").With("symbol", symbol),
            FmpJsonContext.Default.ListRatiosTtm, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>One symbol's income statements exactly as filed, newest first. From
    /// <c>stable/income-statement-as-reported</c>.
    ///
    /// <para><b>The issuer's XBRL tags, not FMP's normalised fields.</b> Use this to see what a company actually
    /// reported; use <see cref="GetIncomeStatementAsync"/> to compare companies. The two do not have the same
    /// field names and are not meant to. See <see cref="AsReportedStatement"/> for why the payload is an open
    /// dictionary whose values are not all numbers.</para>
    ///
    /// <para>Measured 2026-08-27: 24 tagged facts for AAPL and 39 for JPM on the same path and the same
    /// cadence.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AsReportedStatement>> GetIncomeStatementAsReportedAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement-as-reported", symbol, period, limit),
            FmpJsonContext.Default.ListAsReportedStatement, ct);

    /// <summary>One symbol's balance sheets exactly as filed, newest first. From
    /// <c>stable/balance-sheet-statement-as-reported</c>.
    ///
    /// <para><b>The as-filed counterpart of <see cref="GetBalanceSheetAsync"/>.</b> The issuer's own XBRL tags,
    /// not FMP's normalised fields — the two do not share field names and are not meant to. See
    /// <see cref="AsReportedStatement"/> for why the payload is an open dictionary whose values are not all
    /// numbers.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AsReportedStatement>> GetBalanceSheetAsReportedAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/balance-sheet-statement-as-reported", symbol, period, limit),
            FmpJsonContext.Default.ListAsReportedStatement, ct);

    /// <summary>One symbol's cash flow statements exactly as filed, newest first. From
    /// <c>stable/cash-flow-statement-as-reported</c>.
    ///
    /// <para><b>The as-filed counterpart of <see cref="GetCashFlowAsync"/>.</b> The issuer's own XBRL tags, not
    /// FMP's normalised fields — the two do not share field names and are not meant to. See
    /// <see cref="AsReportedStatement"/> for why the payload is an open dictionary whose values are not all
    /// numbers.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AsReportedStatement>> GetCashFlowAsReportedAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/cash-flow-statement-as-reported", symbol, period, limit),
            FmpJsonContext.Default.ListAsReportedStatement, ct);

    /// <summary>One symbol's full financial statement exactly as filed, newest first. From
    /// <c>stable/financial-statement-full-as-reported</c>.
    ///
    /// <para><b>All three statements plus the cover page in one object</b> — 300 keys for AAPL and 923 for JPM,
    /// measured 2026-08-27, and the payload where the 47 strings and the postal code live. See
    /// <see cref="AsReportedStatement"/> for why the payload is an open dictionary whose values are not all
    /// numbers.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AsReportedStatement>> GetFullStatementAsReportedAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/financial-statement-full-as-reported", symbol, period, limit),
            FmpJsonContext.Default.ListAsReportedStatement, ct);

    /// <summary>One symbol's revenue split by product line, newest period first. From
    /// <c>stable/revenue-product-segmentation</c>.
    ///
    /// <para><b>Takes no <c>limit</c>.</b> Measured 2026-08-27, the endpoint transfers the full set regardless of
    /// what is sent, so offering the parameter would be offering a lever that does nothing.</para>
    ///
    /// <para><b>The <c>structure</c> parameter FMP documents is not sent either.</b> Measured on AAPL and on JPM —
    /// a filer with genuinely nested segments — <c>structure=flat</c> and <c>structure=hierarchical</c> returned
    /// payloads identical to sending nothing at all. It is inert.</para>
    ///
    /// <para>Segment names are the company's own and change when it reorganises; see
    /// <see cref="RevenueSegmentation"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<RevenueSegmentation>> GetRevenueByProductAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, CancellationToken ct = default) =>
        transport.GetListAsync(Envelope("stable/revenue-product-segmentation", symbol, period),
            FmpJsonContext.Default.ListRevenueSegmentation, ct);

    /// <summary>One symbol's revenue split by geography, newest period first. From
    /// <c>stable/revenue-geographic-segmentation</c>.
    ///
    /// <para><b>Keys are country and region names, not product lines</b> — the same shape as
    /// <see cref="GetRevenueByProductAsync"/> but split along a different axis.</para>
    ///
    /// <para><b>Takes no <c>limit</c>.</b> Measured 2026-08-27, the endpoint transfers the full set regardless of
    /// what is sent, so offering the parameter would be offering a lever that does nothing.</para>
    ///
    /// <para><b>The <c>structure</c> parameter FMP documents is not sent either.</b> Measured on AAPL and on JPM —
    /// a filer with genuinely nested segments — <c>structure=flat</c> and <c>structure=hierarchical</c> returned
    /// payloads identical to sending nothing at all. It is inert.</para>
    ///
    /// <para>Segment names are the company's own and change when it reorganises; see
    /// <see cref="RevenueSegmentation"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<RevenueSegmentation>> GetRevenueByGeographyAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, CancellationToken ct = default) =>
        transport.GetListAsync(Envelope("stable/revenue-geographic-segmentation", symbol, period),
            FmpJsonContext.Default.ListRevenueSegmentation, ct);

    /// <summary>The most rows <c>stable/owner-earnings</c> will return, whatever limit is sent — and the reason a
    /// caller has to care.
    ///
    /// <para>Measured 2026-08-27 at <c>limit=100000</c>: AAPL, MSFT, GE, KO, JPM, IBM and PG each returned
    /// <b>exactly 50</b>, oldest row 2013-12-31 to 2014-05-09. <c>income-statement-ttm</c> reaches 1985 for the
    /// same filers, so 50 is this endpoint's ceiling rather than the extent of FMP's data.</para>
    ///
    /// <para><b>The payload cannot tell you which case you are in.</b> SHOP returned 46, and that is Shopify's
    /// real history. So fewer than 50 rows is data, exactly 50 rows is a truncation, and the two are
    /// indistinguishable from the response — there is no <c>hasMore</c>, no total, and no error. Comparing
    /// <c>rows.Count</c> against this constant is the only signal there is.</para></summary>
    public const int MaxOwnerEarningsRows = 50;

    /// <summary>Buffett-style owner earnings for one symbol, newest quarter first. From
    /// <c>stable/owner-earnings</c>.
    ///
    /// <para><b>Quarterly only, and capped at <see cref="MaxOwnerEarningsRows"/> rows.</b> A result of exactly 50
    /// rows is probably truncated and cannot be distinguished from a company with exactly 50 quarters of history
    /// — read that constant before treating the oldest row as the start of the series. Roughly twelve years,
    /// measured 2026-08-27.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para>
    ///
    /// <para>The figures are FMP's estimates rather than filed values — see <see cref="OwnerEarnings"/>, which
    /// also covers how the two capex fields are signed, including the one whose sign is not
    /// guaranteed.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/> — which this endpoint still caps at
    /// <see cref="MaxOwnerEarningsRows"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, at most <see cref="MaxOwnerEarningsRows"/> of them, or empty for an unknown
    /// symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<OwnerEarnings>> GetOwnerEarningsAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/owner-earnings", symbol, limit),
            FmpJsonContext.Default.ListOwnerEarnings, ct);

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
    /// rows regardless — see <see cref="MaxOwnerEarningsRows"/>.</para></summary>
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

    /// <summary>The query shape for the per-symbol paths that take no <c>period</c>.
    ///
    /// <para>Separate from <see cref="Periodic"/> rather than passing a nullable period through it, because the
    /// difference is a fact about the endpoints and not a formatting choice: measured 2026-08-27, these paths
    /// accept <c>period</c> and discard it. A helper that could emit it would leave the decision to a call
    /// site.</para></summary>
    private static FmpRequest Rolling(string path, string symbol, int? limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("limit", limit ?? FullHistoryLimit);
    }

    /// <summary>The query shape for the paths that take a <c>period</c> and ignore <c>limit</c>.
    ///
    /// <para>Measured 2026-08-27, both segmentation paths transfer the full set whatever limit is sent, so no
    /// limit is offered rather than one that does nothing.</para></summary>
    private static FmpRequest Envelope(string path, string symbol, FiscalPeriod period)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("period", period.ToQueryValue());
    }
}
