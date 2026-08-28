using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>SEC Filings</c> group — what companies have filed with EDGAR, and who the filers are.
///
/// <para><b>Nine of the twelve paths FMP files under this heading.</b> The other three are reference lists
/// rather than filings and live where their job already is: <c>all-industry-classification</c> and
/// <c>standard-industrial-classification-list</c> on <see cref="DirectoryEndpoints"/>, and
/// <c>industry-classification-search</c> on <see cref="SearchEndpoints"/>. That follows existing practice rather
/// than departing from it — <c>commodities-list</c>, <c>forex-list</c> and <c>index-list</c> are already on
/// <see cref="DirectoryEndpoints"/> although FMP documents them under Commodity, Forex and Indexes. This SDK
/// files a path by what it returns.</para>
///
/// <para><b>Two families here, and they do not share a row shape.</b>
/// <see cref="GetProfileAsync(string, CancellationToken)"/> answers a registrant;
/// <c>Get8KFilingsAsync</c> and its neighbours answer filings; the three <c>FindCompany*</c> methods
/// answer the same classification row <see cref="DirectoryEndpoints"/> and <see cref="SearchEndpoints"/> serve,
/// which is why they return <see cref="IndustryClassification"/> rather than a type of their own.</para>
///
/// <para><b>Dates are the trap in this group.</b> <c>from</c> and <c>to</c> filter
/// <see cref="SecFiling.AcceptedDate"/>, not <see cref="SecFiling.FilingDate"/>, so a response legitimately
/// carries rows dated outside the range you asked for. See <see cref="SecFiling"/> for the measurement.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class SecFilingsEndpoints(FmpTransport transport)
{
    /// <summary>The EDGAR registrant profile for one symbol, or <see langword="null"/> when FMP knows no such
    /// symbol — <c>stable/sec-profile</c>.
    ///
    /// <para><b>Not the same thing as <see cref="CompanyEndpoints.GetProfileAsync"/>.</b> That answers market
    /// data; this answers the registration record. See <see cref="SecProfile"/>.</para>
    ///
    /// <para><b>A bare call to this endpoint answers Apple's profile with HTTP 200</b>, measured 2026-08-28 —
    /// it defaults rather than erroring. A blank symbol would reach FMP as a bare call, because
    /// <c>FmpRequest</c> drops empty values, so it is rejected here instead: a caller must not receive a
    /// well-formed answer to a question they did not ask.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The profile, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<SecProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/sec-profile").With("symbol", symbol),
            FmpJsonContext.Default.ListSecProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>The EDGAR registrant profile for one Central Index Key, or <see langword="null"/> when FMP knows
    /// no such filer — <c>stable/sec-profile</c> with <c>cik</c> instead of <c>symbol</c>.
    ///
    /// <para>The same path and the same 35 fields as
    /// <see cref="GetProfileAsync(string, CancellationToken)"/>; measured 2026-08-28, AAPL and CIK
    /// <c>0000320193</c> answered identically, and the padded and unpadded forms of the CIK both answered one
    /// row.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded — both work.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The profile, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank — see
    /// <see cref="GetProfileAsync(string, CancellationToken)"/> for why a blank one is refused rather than
    /// sent.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<SecProfile?> GetProfileByCikAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/sec-profile").With("cik", cik),
            FmpJsonContext.Default.ListSecProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
}
