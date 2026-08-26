using FinancialModelingPrep.Models;
using FinancialModelingPrep.Serialization;

namespace FinancialModelingPrep.Endpoints;

/// <summary>The period-shaped fundamentals endpoints: the three statements plus the four derived sets.
///
/// <para>All seven take the same three arguments and answer newest period first. They are grouped here because
/// they share one query shape, not because FMP groups them — FMP splits them across its Statements, Ratios and
/// Metrics sections.</para></summary>
public sealed class StatementEndpoints(FmpTransport transport)
{
    /// <summary>Income statements for one symbol, newest first.</summary>
    public Task<IReadOnlyList<IncomeStatement>> GetIncomeStatementAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement", symbol, period, limit),
            FmpJsonContext.Default.ListIncomeStatement, ct);

    /// <summary>Balance sheets for one symbol, newest first.</summary>
    public Task<IReadOnlyList<BalanceSheetStatement>> GetBalanceSheetAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/balance-sheet-statement", symbol, period, limit),
            FmpJsonContext.Default.ListBalanceSheetStatement, ct);

    /// <summary>Cash flow statements for one symbol, newest first.</summary>
    public Task<IReadOnlyList<CashFlowStatement>> GetCashFlowAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/cash-flow-statement", symbol, period, limit),
            FmpJsonContext.Default.ListCashFlowStatement, ct);

    /// <summary>Financial ratios for one symbol, newest first.</summary>
    public Task<IReadOnlyList<FinancialRatios>> GetRatiosAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/ratios", symbol, period, limit),
            FmpJsonContext.Default.ListFinancialRatios, ct);

    /// <summary>Key metrics for one symbol, newest first.</summary>
    public Task<IReadOnlyList<KeyMetrics>> GetKeyMetricsAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/key-metrics", symbol, period, limit),
            FmpJsonContext.Default.ListKeyMetrics, ct);

    /// <summary>Period-on-period growth rates for one symbol, newest first.</summary>
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
    public Task<IReadOnlyList<EnterpriseValues>> GetEnterpriseValuesAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/enterprise-values", symbol, period, limit),
            FmpJsonContext.Default.ListEnterpriseValues, ct);

    /// <summary>The one query shape all seven share. Written once so the seven cannot drift apart.</summary>
    private static FmpRequest Periodic(string path, string symbol, FiscalPeriod period, int? limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("period", period.ToQueryValue())
            .With("limit", limit);
    }
}
