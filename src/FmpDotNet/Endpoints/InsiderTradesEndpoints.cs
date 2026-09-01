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
    /// itself.</para>
    ///
    /// <para><b>There is no <c>date</c> argument, and the reason is that FMP's <c>date</c> parameter here answers
    /// only today and ignores every other value in silence.</b> <c>fmpsdk</c> sends one, so this is recorded
    /// rather than left for the next parameter diff to re-open. Measured 2026-09-01 (#46), against a page holding
    /// 89 rows dated 2026-08-31 and 11 dated 2026-09-01: <c>date=2026-09-01</c> — that day — answered exactly
    /// those <b>11</b>. <c>date=2026-08-31</c>, <c>2026-08-30</c>, <c>2026-08-27</c> and <c>2026-01-15</c> each
    /// answered <b>a body byte-identical to the unfiltered page</b>, same SHA-256 on all four. The 2026-08-31 case
    /// rules out "filtered to nothing", since 89 rows of that very date were sitting in the page it returned
    /// unchanged, and dropping <c>page</c> and <c>limit</c> changes nothing. Modelling it as a date filter would
    /// hand a caller a silently unfiltered page for every historical date they asked for — the failure this whole
    /// endpoint group is documented to avoid.</para></summary>
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
    /// one: <see cref="FmpRequest.With(string, string?)"/> drops only <see langword="null"/> and <c>""</c>, not
    /// a whitespace-only string, so this method blanks each of the four itself before handing them to it — a
    /// caller passing an untouched form field must not send a literal space to FMP as a filter.</para></summary>
    /// <param name="symbol">The issuer's ticker. Optional.</param>
    /// <param name="reportingCik">The <b>insider's</b> Central Index Key, padded or unpadded — both work.
    /// Optional.</param>
    /// <param name="companyCik">The <b>issuer's</b> Central Index Key, padded or unpadded. Optional, and not
    /// interchangeable with <paramref name="reportingCik"/>.</param>
    /// <param name="transactionType">An SEC transaction code — <c>"S-Sale"</c>, <c>"P-Purchase"</c>. The
    /// eighteen valid values come from <see cref="GetTransactionTypesAsync"/>. Optional, and not validated
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

    /// <summary>The paging guard the two feeds share. This facade extracts it at its two call sites where
    /// <see cref="SecFilingsEndpoints"/> inlines the identical three lines at each of its three instead —
    /// the right call here because <see cref="GetLatestAsync"/> and <see cref="SearchAsync"/> need the same
    /// guard set, so the three-line body is the thing that must not drift between them. <see cref="DateRange"/>
    /// lays out the same drift concern for a different guard.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxInsiderTradePageSize);
    }

    /// <summary>Insider activity for one issuer, aggregated by quarter —
    /// <c>stable/insider-trading/statistics</c>.
    ///
    /// <para>One row per quarter with any activity, newest first — 94 for AAPL, measured 2026-08-28, back to
    /// 2003. No <c>limit</c>, no <c>page</c>, and no year or quarter filter: the endpoint honours none of
    /// them.</para>
    ///
    /// <para><b>Read <see cref="InsiderTradeStatistics"/> before comparing its fields.</b> Two pairs of them
    /// read alike and count different things — transactions against shares.</para></summary>
    /// <param name="symbol">The issuer's ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter with activity, newest first. Never <see langword="null"/>; empty for an unknown
    /// symbol, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTradeStatistics>> GetStatisticsAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/statistics").With("symbol", symbol),
            FmpJsonContext.Default.ListInsiderTradeStatistics, ct);
    }

    /// <summary>Insiders whose name starts with what you typed —
    /// <c>stable/insider-trading/reporting-name</c>.
    ///
    /// <para><b>The way to get a <c>reportingCik</c> for <see cref="SearchAsync"/>.</b> That method takes a
    /// CIK, not a name; this turns one into the other.</para>
    ///
    /// <para><b>A prefix match against a surname-first name.</b> Measured 2026-08-28, <c>Cook</c> answered 133
    /// rows all beginning "Cook", and <c>Apple</c> answered 20 including <c>Applebach</c> and
    /// <c>Applebaum</c> — so it is neither a substring match nor a company search, and a given name finds
    /// nothing.</para>
    ///
    /// <para>No <c>limit</c>: the endpoint ignores it.</para></summary>
    /// <param name="name">The start of the insider's name, surname first.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching insiders. Never <see langword="null"/>; empty for an unmatched prefix, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderReportingName>> SearchReportingNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/reporting-name").With("name", name),
            FmpJsonContext.Default.ListInsiderReportingName, ct);
    }

    /// <summary>The eighteen SEC transaction codes <see cref="SearchAsync"/> accepts —
    /// <c>stable/insider-trading-transaction-type</c>.
    ///
    /// <para><b>Note the path: a sibling of <c>insider-trading/*</c>, not a child of it.</b> FMP spells this
    /// one with a hyphen where the rest of the group uses a slash.</para>
    ///
    /// <para>Takes no parameters and answers the whole list. Measured 2026-08-28: 18 rows, <c>A-Award</c>
    /// through <c>Z-Trust</c>, and every <c>transactionType</c> on 1,000 sampled trade rows was drawn from it
    /// or was the empty string.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The eighteen codes. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTransactionType>> GetTransactionTypesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/insider-trading-transaction-type"),
            FmpJsonContext.Default.ListInsiderTransactionType, ct);
}
