using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Insider Trades</c> group — what officers, directors and 10% owners file on Forms 3, 4
/// and 5.
///
/// <para><b>Five of the six paths FMP files under this heading.</b> The sixth,
/// <c>acquisition-of-beneficial-ownership</c>, is an SC 13D/G stake disclosure rather than an insider
/// transaction and lives on <see cref="InstitutionalOwnershipEndpoints"/>; see that class for why. This SDK
/// files a path by what it returns.</para>
///
/// <para><b>Two of the five answer the same row shape.</b>
/// <see cref="GetLatestAsync"/> and <see cref="SearchAsync"/> both return
/// <see cref="InsiderTrade"/> — the same sixteen keys in the same order, verified 2026-08-28 — and differ only
/// in what they select. The other three answer shapes of their own.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class InsiderTradesEndpoints(FmpTransport transport)
{
    /// <summary>The largest page either insider feed will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-28, <c>insider-trading/latest?limit=2000</c> and
    /// <c>?limit=5000</c> each answered exactly 1,000 rows with HTTP 200 and byte-identical bodies, and
    /// <c>insider-trading/search?limit=2000</c> answered 1,000 as well — nothing in the response says the
    /// request was trimmed. Both feeds paginate, so a caller who asks for 5,000 and advances <c>page</c> by
    /// 5,000 reads a fifth of the archive and is never told.</para></summary>
    public const int MaxInsiderTradePageSize = 1000;

    /// <summary>The whole-market feed of insider filings as they arrive, newest first —
    /// <c>stable/insider-trading/latest</c>.
    ///
    /// <para>The 100 rows a bare call returns is a default rather than a cap: measured 2026-08-28,
    /// <c>limit=200</c> answered 200 and <c>limit=1000</c> answered 1,000. See
    /// <see cref="MaxInsiderTradePageSize"/> for where that stops.</para>
    ///
    /// <para><b>A distinct path from <see cref="SearchAsync"/>, not a special case of it.</b> An unfiltered
    /// search answers the same rows, but the two are separate endpoints and each is modelled as
    /// itself.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxInsiderTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxInsiderTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit.</exception>
    public Task<IReadOnlyList<InsiderTrade>> GetLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);

        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInsiderTrade, ct);
    }

    /// <summary>Insider filings narrowed by any combination of four criteria —
    /// <c>stable/insider-trading/search</c>.
    ///
    /// <para><b>All four discriminators are optional and they intersect.</b> Measured 2026-08-28:
    /// <c>reportingCik=1780525</c> alone answers 553 rows across five symbols — the reporting person changed
    /// employers — of which exactly 10 are AAPL, and <c>symbol=AAPL&amp;reportingCik=1780525</c> answers
    /// exactly those 10. Adding a criterion narrows; it never widens.</para>
    ///
    /// <para><b>A row count that drops sharply when you add a criterion is usually the default page, not the
    /// filter.</b> A bare call returns 100 rows, so <c>reportingCik</c> alone looked like "100 rows, all AAPL"
    /// until the whole 553-row set was asked for. Raise <paramref name="limit"/> before concluding a filter has
    /// lost rows.</para>
    ///
    /// <para><b>With nothing supplied this answers the same feed as <see cref="GetLatestAsync"/>.</b> That is a
    /// valid call rather than a caller error, and a blank discriminator is treated the same way as an absent
    /// one: <c>FmpRequest.With(string, string?)</c> drops only <see langword="null"/> and <c>""</c>, not a
    /// whitespace-only string, so this method blanks each of the four itself before handing them to it — a
    /// caller passing an untouched form field must not send a literal space to FMP as a filter.</para></summary>
    /// <param name="symbol">The issuer's ticker. Optional.</param>
    /// <param name="reportingCik">The <b>insider's</b> Central Index Key, padded or unpadded — both work.
    /// Optional.</param>
    /// <param name="companyCik">The <b>issuer's</b> Central Index Key, padded or unpadded. Optional, and not
    /// interchangeable with <paramref name="reportingCik"/>.</param>
    /// <param name="transactionType">An SEC transaction code — <c>"S-Sale"</c>, <c>"P-Purchase"</c>. The
    /// eighteen valid values come from <c>GetTransactionTypesAsync</c>. Optional, and not validated
    /// here: an unrecognised code answers an empty list rather than an error, and a code FMP adds must not cost
    /// the caller the call.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxInsiderTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching filings, newest first. Never <see langword="null"/>; empty when nothing matches,
    /// not an error.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxInsiderTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTrade>> SearchAsync(
        string? symbol = null, string? reportingCik = null, string? companyCik = null,
        string? transactionType = null, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);

        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/search")
                .With("symbol", NullIfBlank(symbol)).With("reportingCik", NullIfBlank(reportingCik))
                .With("companyCik", NullIfBlank(companyCik)).With("transactionType", NullIfBlank(transactionType))
                .With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInsiderTrade, ct);
    }

    /// <summary>Blanks a discriminator so it does not reach the query string.
    ///
    /// <para><see cref="FmpRequest.With(string, string?)"/> drops a <see langword="null"/> or <c>""</c> value
    /// but sends a whitespace-only one through verbatim — it checks <c>string.IsNullOrEmpty</c>, not
    /// <c>string.IsNullOrWhiteSpace</c>. That distinction is invisible on every other optional parameter in the
    /// SDK because none of them are tested against a blank string, but <see cref="SearchAsync"/> is: a caller
    /// passing an untouched form field's value straight through must not turn it into a literal-space filter
    /// FMP echoes back as a non-match.</para></summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>The paging guard the two feeds share. Extracted at two call sites for the reason
    /// <see cref="SecFilingsEndpoints"/> records: the three-line body is the thing that must not drift between
    /// them.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxInsiderTradePageSize);
    }
}
