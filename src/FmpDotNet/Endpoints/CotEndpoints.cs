using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>The CFTC's weekly Commitment of Traders report — the filing in full, FMP's reading of it, and the
/// contracts both cover.
///
/// <para><b>The data on this key stops at 2024-02-27.</b> Measured 2026-08-29, every response from both dated
/// paths — bare, by symbol, and by range — covered 2024-01-02 to 2024-02-27 and nothing later, about two and
/// a half years before the measurement date. A caller asking for a recent range gets a well-formed empty
/// array with HTTP 200 and nothing saying why. This is the first thing to check when these methods return
/// nothing.</para>
///
/// <para><b>The two dated paths do not answer the same amount of history for the same question</b>, and both
/// look equally healthy. See <see cref="GetAnalysisAsync"/>.</para>
///
/// <para>Contract codes are FMP's own — <c>NG</c>, <c>ZC</c>, <c>EURGBP</c> — not exchange tickers, and not
/// the equity symbols the rest of this SDK takes. <see cref="GetSymbolsAsync"/> lists all 65.</para>
///
/// <para><b>Plan tier — Premium, second-hand.</b> fmpsdk 20260824.0, the independent client this SDK is cross-checked
/// against, recorded every path in this class as 402 on free and Starter and working on Premium on 2026-08-23. Not
/// verified here: every path answered 200 on the Ultimate key this SDK is measured with (2026-09-02), which says
/// nothing about the plans below it. A dated observation, not a contract — catch
/// <see cref="FmpPlanRestrictedException"/> rather than gating on it.</para></summary>
public sealed class CotEndpoints(FmpTransport transport)
{
    /// <summary>The CFTC's weekly report, field for field — <c>stable/commitment-of-traders-report</c>.
    ///
    /// <para>128 fields per row; see <see cref="CotReport"/> for what they are and for the 27 whose C#
    /// spelling differs from the wire. Measured 2026-08-29, a bare call answered <b>545 rows</b> — nine
    /// weekly dates across the 65 contracts — and one symbol over a two-year range answered <b>105</b>, the
    /// full weekly history in that range with no truncation observed.</para>
    ///
    /// <para><b>Every parameter is optional, and omitting <paramref name="symbol"/> means every
    /// contract.</b> That is a legitimate query and it is also 2.4 MB, measured.</para></summary>
    /// <param name="symbol">The contract code from <see cref="GetSymbolsAsync"/> — <c>NG</c>. Omit for every
    /// contract.</param>
    /// <param name="from">First report date in the range, inclusive. Omit for FMP's default window.</param>
    /// <param name="to">Last report date in the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per contract per weekly report date. Never <see langword="null"/>; an empty list
    /// usually means the range is outside the data rather than that the contract has no filings.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotReport>> GetReportAsync(
        string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-report")
                .With("symbol", symbol).With("from", from).With("to", to),
            FmpJsonContext.Default.ListCotReport, ct);
    }

    /// <summary>FMP's reading of the weekly report — <c>stable/commitment-of-traders-analysis</c>.
    ///
    /// <para><b>Asked for one contract over a range, this path truncates to 13 rows and its sibling does
    /// not.</b> Measured 2026-08-29, same symbol, same range, issued together:</para>
    /// <list type="table">
    ///   <listheader><term>range (<c>symbol=NG</c>)</term><description>analysis / report</description></listheader>
    ///   <item><term>2024-01-01 … 2024-03-31</term><description>13 rows / 13 rows — identical</description></item>
    ///   <item><term>2024-01-01 … 2024-06-30</term><description><b>13</b> rows, 2024-04-02 onward /
    ///     26 rows, 2024-01-02 onward</description></item>
    ///   <item><term>2023-01-01 … 2024-12-31</term><description><b>13</b> rows, 2024-10-08 onward /
    ///     105 rows, 2023-01-03 onward</description></item>
    /// </list>
    /// <para>Thirteen is the cap measured on a contract-and-range query — <c>symbol=NG</c> plus a bounded
    /// <paramref name="from"/>/<paramref name="to"/>, as in the table above. The newest rows survive, and the
    /// status is 200 with a well-formed array every time. A caller who asks both for two years of history and
    /// joins them on date gets thirteen rows and no indication that the other 92 were dropped on one side.
    /// <b>Ask for a quarter at a time</b>, or read <see cref="CotReport"/> and derive what you need. Omitting
    /// <paramref name="symbol"/> is a different query, not a narrower one of the same kind — measured
    /// 2026-08-29, a bare call answered <b>545 rows</b>, the same figure as <see cref="GetReportAsync"/>'s
    /// bare call, and the thirteen-row cap does not apply to it.</para>
    ///
    /// <para>No row-count guard is added, for the reason
    /// <see cref="EconomicsEndpoints.GetEconomicCalendarAsync"/> sets out: a threshold that caught this would
    /// reject a legitimately short range. Compare <see cref="CotAnalysis.Date"/> against the range you asked
    /// for.</para>
    ///
    /// <para><see cref="CotAnalysis.ChangeInNetPosition"/> is a <b>percentage</b>, not a difference of
    /// contracts, despite sitting between two contract counts.</para></summary>
    /// <param name="symbol">The contract code from <see cref="GetSymbolsAsync"/>. Omit for every
    /// contract.</param>
    /// <param name="from">First report date in the range, inclusive.</param>
    /// <param name="to">Last report date in the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>At most 13 rows, newest first, for a request naming <paramref name="symbol"/> with a bounded
    /// range; 545 rows for the bare call, matching <see cref="GetReportAsync"/>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotAnalysis>> GetAnalysisAsync(
        string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-analysis")
                .With("symbol", symbol).With("from", from).With("to", to),
            FmpJsonContext.Default.ListCotAnalysis, ct);
    }

    /// <summary>Every contract FMP publishes COT data for — <c>stable/commitment-of-traders-list</c>.
    ///
    /// <para>The whole universe in one call: <b>65 rows</b> measured 2026-08-29, no paging, no parameters.
    /// This is where a contract code for <see cref="GetReportAsync"/> and <see cref="GetAnalysisAsync"/>
    /// comes from.</para>
    ///
    /// <para><b>Named <c>GetSymbolsAsync</c> rather than after the path.</b> <c>GetListAsync</c> is what
    /// <see cref="FmpTransport"/> calls its own primitive, and a facade method of that name would read as the
    /// transport rather than as a directory of contracts.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every contract code and name. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotSymbol>> GetSymbolsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-list"),
            FmpJsonContext.Default.ListCotSymbol, ct);
}
