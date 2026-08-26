using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Search</c> group — finding securities by what they are rather than by symbol.
///
/// <para>Where <see cref="DirectoryEndpoints"/> answers "everything FMP knows" as a flat 5–8 MB download, this
/// answers a question about the universe and returns only the matches, with the values matched on. It is the
/// endpoint to reach for when a full directory download is more than the question needs.</para></summary>
public sealed class SearchEndpoints(FmpTransport transport)
{
    /// <summary>Screens the universe against <paramref name="criteria"/>, returning matches ordered by market
    /// capitalisation, largest first.
    ///
    /// <para><b>An empty <see cref="ScreenerCriteria"/> is a valid, unfiltered request</b> — not a request for
    /// nothing. Unset properties are never sent, so it asks FMP for the default page: the top 1,000 securities by
    /// market cap, measured 2026-08-26 at 441,559 bytes.</para>
    ///
    /// <para><b>An empty result is not necessarily an empty answer.</b> This endpoint reports an unrecognised
    /// filter value as a match of zero rows with HTTP 200 — <c>sector=Nonsense</c> and an exchange sent by its long
    /// name both do it. Nothing downstream can tell that apart from a real screen that matched nothing, so a
    /// surprising empty result is a reason to check the filter values against
    /// <see cref="DirectoryEndpoints.GetSectorsAsync(CancellationToken)"/>,
    /// <see cref="DirectoryEndpoints.GetIndustriesAsync(CancellationToken)"/>, or
    /// <see cref="ScreenerResult.ExchangeShortName"/> before concluding the universe is empty. See
    /// <see cref="ScreenerCriteria"/> for the full account of what this endpoint accepts without
    /// complaint.</para></summary>
    /// <param name="criteria">The filters to apply. Required rather than optional so the call site always says
    /// what it is asking for, even when the answer is "everything" — pass <c>new ScreenerCriteria()</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching rows in FMP's order. Empty when nothing matched, and — see above — also empty when a
    /// filter value was not recognised. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ScreenerCriteria.Page"/> is negative or
    /// <see cref="ScreenerCriteria.Limit"/> is not positive.</exception>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ScreenerResult>> ScreenAsync(
        ScreenerCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        // Checked here rather than left to FMP because this endpoint does not report bad input: a negative page is
        // one more value it would answer rather than reject, and the answer would look like data.
        if (criteria.Page is { } page) ArgumentOutOfRangeException.ThrowIfNegative(page, nameof(criteria));
        if (criteria.Limit is { } limit) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, nameof(criteria));
        return transport.GetListAsync(criteria.ToRequest(), FmpJsonContext.Default.ListScreenerResult, ct);
    }
}
