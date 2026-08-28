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
/// <c>InsiderTradesEndpoints</c>. <see cref="SecFilingsEndpoints"/> set that precedent, sending three of
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
