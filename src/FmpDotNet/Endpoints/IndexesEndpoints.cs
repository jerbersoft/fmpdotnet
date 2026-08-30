using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Index membership — who is in the Dow Jones, the S&amp;P 500 and the Nasdaq 100 now, and every
/// change to those lists that FMP records.
///
/// <para><b>Three things hold across all six paths, measured 2026-08-30, and a caller should read them
/// once.</b></para>
///
/// <list type="number">
///   <item><description><b>None of them takes a parameter, and that is measured.</b> <c>limit</c>,
///     <c>page</c>, <c>symbol</c> and an unknown <c>wibble=42</c> each returned a response
///     <b>byte-identical</b> to the bare request on every path; on the three change feeds so did
///     <c>from=2020-01-01&amp;to=2026-12-31</c>. There is nothing to narrow with and no pagination to walk.
///     The largest response is <c>historical-sp500-constituent</c> at 1,525 rows and 365,284
///     bytes.</description></item>
///   <item><description><b>A row count is not a company count.</b> <c>sp500-constituent</c> returned 503
///     rows over 500 distinct CIKs and <c>nasdaq-constituent</c> 102 over 101. See
///     <see cref="IndexConstituent"/>.</description></item>
///   <item><description><b>The change feeds are not membership history.</b> Of the 628 current constituents
///     carrying a <c>dateFirstAdded</c>, 24 have no addition row at all, so replaying the changes does not
///     reconstruct who was in an index on a past date. That is why the three methods are named for
///     <b>changes</b> rather than for the paths they call, and why this SDK offers no as-of-date membership
///     method.</description></item>
/// </list>
///
/// <para>Market hours and exchange holidays are a separate facade — <c>MarketHoursEndpoints</c> — because
/// the two groups share no path prefix, no parameter, no record and no concept.</para></summary>
public sealed class IndexesEndpoints(FmpTransport transport)
{
    /// <summary>The Dow Jones Industrial Average's current members, from
    /// <c>stable/dowjones-constituent</c>.
    ///
    /// <para>30 rows measured 2026-08-30, and <see cref="IndexConstituent.Founded"/> was ISO on
    /// <b>30 of 30</b> — which is exactly the sample that makes that field look like a date. It is not; read
    /// its documentation before binding it yourself.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points
    /// at the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetDowJonesConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/dowjones-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>The S&amp;P 500's current members, from <c>stable/sp500-constituent</c>.
    ///
    /// <para><b>503 rows over 500 distinct CIKs</b>, measured 2026-08-30 — FOX/FOXA, NWS/NWSA and GOOGL/GOOG
    /// are the three dual-class pairs. Counting rows counts share classes.</para>
    ///
    /// <para>This is the path on which <see cref="IndexConstituent.Founded"/> shows what it really is: a bare
    /// year on <b>477 of 503</b> rows.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetSp500ConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/sp500-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>The Nasdaq 100's current members, from <c>stable/nasdaq-constituent</c>.
    ///
    /// <para>102 rows over 101 distinct CIKs, measured 2026-08-30. The only path on which
    /// <see cref="IndexConstituent.DateFirstAdded"/> is ever <see langword="null"/> — 7 rows: ADBE, AMAT,
    /// CSCO, FAST, MSFT, PAYX and QCOM.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetNasdaqConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/nasdaq-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>Every recorded change to the Dow Jones Industrial Average's membership, from
    /// <c>stable/historical-dowjones-constituent</c>.
    ///
    /// <para><b>Named for changes, not for the path, because a row is a change and not a
    /// constituent.</b> 86 rows measured 2026-08-30, each one an addition <i>or</i> a removal. See
    /// <see cref="IndexConstituentChange"/> for what that means for <c>symbol</c>, and for why this feed
    /// cannot answer "who was in the index on date X".</para>
    ///
    /// <para><b>This path is where absence is spelled only one way.</b> All 86 rows use <c>""</c> and none
    /// uses JSON <see langword="null"/> — unlike its two siblings. An implementer testing here alone never
    /// meets the second spelling.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetDowJonesConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-dowjones-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);

    /// <summary>Every recorded change to the S&amp;P 500's membership, from
    /// <c>stable/historical-sp500-constituent</c>.
    ///
    /// <para><b>The largest response in this facade</b> — 1,525 rows and 365,284 bytes measured 2026-08-30,
    /// reaching back to a 1957 backfill, and it cannot be narrowed: <c>from</c>/<c>to</c> are accepted and
    /// discarded here, verified byte-identical.</para>
    ///
    /// <para>Named for changes rather than for the path; see
    /// <see cref="GetDowJonesConstituentChangesAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetSp500ConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-sp500-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);

    /// <summary>Every recorded change to the Nasdaq 100's membership, from
    /// <c>stable/historical-nasdaq-constituent</c>. 444 rows measured 2026-08-30.
    ///
    /// <para>Named for changes rather than for the path; see
    /// <see cref="GetDowJonesConstituentChangesAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetNasdaqConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-nasdaq-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);
}
