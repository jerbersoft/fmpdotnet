using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Fundraisers — Regulation Crowdfunding (Form C) and Regulation D (Form D) offerings, six paths.
///
/// <para><b>Two corpora, three shapes each, and they do not overlap.</b> Measured 2026-08-31 in both
/// directions: crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> on the fundraising paths, and
/// fundraising CIK <c>0001617426</c> answers <b>0 rows</b> on the crowdfunding ones. That is why the methods
/// are spelled out rather than parameterised by corpus — a CIK sent to the wrong one produces HTTP 200 with
/// an empty array, which reads exactly like a company that has never filed.</para>
///
/// <para><b>Five things hold across this group, every one of them measured, and not one of them catchable by
/// a caller.</b> Every case below arrives at HTTP 200 with well-formed rows.</para>
///
/// <list type="number">
///   <item><description><b>The four non-<c>-latest</c> paths ignore paging, so this facade does not offer
///     it.</b> Measured 2026-08-31: <c>fundraising?cik=…</c> returned the same 14 rows at <c>page=0</c> and
///     <c>page=1</c>, and both search paths ignore <c>limit</c> outright —
///     <c>crowdfunding-offerings-search?name=Well&amp;limit=2</c> returned all <b>44</b> rows and
///     <c>fundraising-search?name=Apple&amp;limit=2</c> all <b>59</b>.</description></item>
///   <item><description><b>The two <c>-latest</c> paths have different ceilings and different defaults.</b>
///     <see cref="MaxCrowdfundingPageSize"/> is ten times <see cref="MaxFundraisingPageSize"/>, and their
///     defaults differ by the same factor — 100 rows against 10. The two guards are deliberately not
///     shared.</description></item>
///   <item><description><b><c>cik</c> is accepted on <c>fundraising-latest</c> and silently ignored on its
///     crowdfunding sibling, and this facade exposes it on neither.</b> Measured 2026-08-31:
///     <c>fundraising-latest?cik=0001617426&amp;limit=100</c> returned <b>14 rows, all one CIK</b>, while
///     <c>crowdfunding-offerings-latest?cik=0002010670&amp;limit=100</c> returned <b>100 rows across 85
///     distinct CIKs</b>. <see cref="GetFundraisingByCikAsync"/> already provides what the working one adds,
///     and offering the parameter on one method but not the other would invite a caller to try the one that
///     fails silently.</description></item>
///   <item><description><b>A search row is one filing, not one company.</b>
///     <c>fundraising-search?name=Schutt</c> returned 34 rows across <b>5</b> distinct CIKs;
///     <c>crowdfunding-offerings-search?name=Well</c> returned 44 across <b>31</b>. A caller populating a
///     company picker must dedupe by CIK. This SDK does not: the row is what the wire
///     sent.</description></item>
///   <item><description><b>A field called <c>date</c> means four different things across these six
///     paths.</b> <see cref="CrowdfundingOffering.Date"/> is <c>MM-DD-YYYY</c> and is the issuer's formation
///     date rather than the filing's; <see cref="FundraisingNotice.Date"/> is ISO;
///     <see cref="FundraisingSearchHit.Date"/> is an acceptance timestamp. Each record's own doc carries the
///     measurement.</description></item>
/// </list>
///
/// <para><b>Neither search path's matching rule is claimed by this SDK.</b> The fundraising one behaves like
/// a case-insensitive prefix match and the crowdfunding one refutes substring, prefix and whole-word alike —
/// see <see cref="CrowdfundingSearchHit"/>. Both take the caller's string unchanged, because the rule is
/// upstream's and it will go stale.</para></summary>
public sealed class FundraisersEndpoints(FmpTransport transport)
{
    /// <summary>The largest <c>limit</c> <c>stable/crowdfunding-offerings-latest</c> honours. Measured
    /// 2026-08-31, <c>limit=1000</c> and <c>limit=5000</c> both returned 1000 rows. FMP's own default when
    /// the parameter is omitted is <b>100</b>.</summary>
    public const int MaxCrowdfundingPageSize = 1000;

    /// <summary>The largest <c>limit</c> <c>stable/fundraising-latest</c> honours — <b>a tenth of
    /// <see cref="MaxCrowdfundingPageSize"/></b>. Measured 2026-08-31, <c>limit=1000</c> and
    /// <c>limit=101</c> both returned 100 rows. FMP's own default when the parameter is omitted is
    /// <b>10</b>.</summary>
    public const int MaxFundraisingPageSize = 100;

    /// <summary>Every Form C offering one issuer has filed, from <c>stable/crowdfunding-offerings</c>.
    ///
    /// <para><b>The filer's whole history in one response — there is no paging here and none is offered.</b>
    /// Measured 2026-08-31, <c>page=1</c> returned the same rows as <c>page=0</c>. Finlete Funding
    /// (<c>0002010670</c>) answered 48 rows.</para>
    ///
    /// <para><b>A Form D filer's CIK answers zero rows here</b>, at HTTP 200, which is indistinguishable
    /// from an issuer that has never crowdfunded. Use <see cref="GetFundraisingByCikAsync"/> for Form
    /// D.</para></summary>
    /// <param name="cik">The issuer's SEC CIK, as EDGAR writes it — zero-padded to ten digits on every
    /// measured row, though the endpoint also accepts the unpadded form. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The issuer's offerings. Empty when the CIK has no Form C filings — and equally empty when it
    /// belongs to the other corpus. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingOffering>> GetCrowdfundingOfferingsByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings").With("cik", cik),
            FmpJsonContext.Default.ListCrowdfundingOffering, ct);
    }

    /// <summary>The newest Form C offerings across every issuer, from
    /// <c>stable/crowdfunding-offerings-latest</c>.
    ///
    /// <para><b>There is no page ceiling, and that is measured rather than an oversight.</b> Measured
    /// 2026-08-31, <c>page=1000</c> answered HTTP 200 with rows, where the News feeds answer HTTP 400 past
    /// page 100. A bound invented here would reject requests FMP serves. <b>So a page-until-empty loop is
    /// the caller's to terminate</b> — paging does genuinely advance (<c>page=0</c> and <c>page=1</c> at
    /// <c>limit=5</c> shared <b>zero</b> rows and <c>acceptedDate</c> descended continuously across the
    /// boundary), but nothing here promises it ever runs out.</para>
    ///
    /// <para><b><c>cik</c> is accepted by this path and silently ignored</b> — measured 2026-08-31,
    /// <c>cik=0002010670&amp;limit=100</c> returned 100 rows across 85 distinct CIKs. It is not offered
    /// here; use <see cref="GetCrowdfundingOfferingsByCikAsync"/>.</para></summary>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCrowdfundingPageSize"/>. Omit to take FMP's own
    /// default of 100.</param>
    /// <param name="page">Zero-based page index. No upper bound — see the summary.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's offerings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxCrowdfundingPageSize"/>, or <paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingOffering>> GetCrowdfundingOfferingsLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        ThrowIfCrowdfundingPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListCrowdfundingOffering, ct);
    }

    /// <summary>Finds Form C issuers by name, from <c>stable/crowdfunding-offerings-search</c>.
    ///
    /// <para><b>The matching rule is not known, and this SDK does not claim one.</b> Measured 2026-08-31:
    /// <c>Well</c> and <c>Wellness</c> return byte-identical 44-row bodies while <c>Welln</c> and
    /// <c>Wellnes</c> return <b>zero</b>; <c>Or</c>, <c>Ora</c> and <c>Orav</c> return zero while
    /// <c>Oravanti</c> returns one. Substring, prefix and whole-word are each refuted by one of those rows.
    /// <b>An intermediate-length query returning nothing is not evidence the issuer is absent.</b></para>
    ///
    /// <para><b>FMP's documented "or platform" clause is refuted by measurement.</b> The documentation says
    /// this searches "by company name, campaign name, or platform"; <c>name=NetCapital</c> returns
    /// <b>0 rows</b>, though "NetCapital Funding Portal Inc." is the intermediary in FMP's own documented
    /// sample response, and <c>name=Wefunder</c> returns 4 rows that are all the company <i>Wefunder, Inc.</i>
    /// itself.</para>
    ///
    /// <para><b><c>limit</c> is ignored by this path and is not offered.</b> Measured 2026-08-31,
    /// <c>name=Well&amp;limit=2</c> returned all 44 rows.</para></summary>
    /// <param name="name">The name to match. Passed through unchanged — see the summary. Required and
    /// non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per matching <i>filing</i>, not per company — dedupe by
    /// <see cref="CrowdfundingSearchHit.Cik"/>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingSearchHit>> SearchCrowdfundingOfferingsAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings-search").With("name", name),
            FmpJsonContext.Default.ListCrowdfundingSearchHit, ct);
    }

    /// <summary>Every Form D notice one issuer has filed, from <c>stable/fundraising</c>.
    ///
    /// <para><b>The filer's whole history in one response.</b> Measured 2026-08-31, Schutt Private Investment
    /// Fund (<c>0001617426</c>) answered 14 rows, and <c>page=1</c> returned the same 14 — which is why no
    /// paging is offered.</para>
    ///
    /// <para><b>A Form C filer's CIK answers zero rows here</b>, at HTTP 200. Use
    /// <see cref="GetCrowdfundingOfferingsByCikAsync"/> for Form C.</para></summary>
    /// <param name="cik">The issuer's SEC CIK. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The issuer's notices. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingNotice>> GetFundraisingByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising").With("cik", cik),
            FmpJsonContext.Default.ListFundraisingNotice, ct);
    }

    /// <summary>The newest Form D notices across every issuer, from <c>stable/fundraising-latest</c>.
    ///
    /// <para><b>A tenth of its crowdfunding sibling's capacity, in both directions.</b>
    /// <see cref="MaxFundraisingPageSize"/> is 100 against 1000, and FMP's default when <c>limit</c> is
    /// omitted is 10 against 100. Measured 2026-08-31.</para>
    ///
    /// <para><b>No page ceiling</b>, same as the crowdfunding path — <c>page=1000</c> answered HTTP 200 with
    /// rows. A page-until-empty loop is the caller's to terminate.</para>
    ///
    /// <para><b><c>cik</c> is honoured by this path and is still not offered.</b> Measured 2026-08-31,
    /// <c>cik=0001617426&amp;limit=100</c> returned 14 rows all under that CIK — the same answer
    /// <see cref="GetFundraisingByCikAsync"/> gives. It adds no capability, and its crowdfunding sibling
    /// accepts the same parameter and <i>ignores</i> it, so offering it here would teach a caller to reach
    /// for the one that fails silently.</para></summary>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFundraisingPageSize"/>. Omit to take FMP's own
    /// default of 10.</param>
    /// <param name="page">Zero-based page index. No upper bound — see the summary.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's notices, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxFundraisingPageSize"/>, or <paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingNotice>> GetFundraisingLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        ThrowIfFundraisingPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListFundraisingNotice, ct);
    }

    /// <summary>Finds Form D issuers by name, from <c>stable/fundraising-search</c>.
    ///
    /// <para><b>This one does behave like a case-insensitive prefix match</b>, measured 2026-08-31:
    /// <c>a</c> 0, <c>ab</c> 979, <c>abc</c> 56, <c>Ap</c> 421, <c>App</c> 256,
    /// <c>Apple</c>/<c>apple</c>/<c>APPLE</c> 59 each, <c>pple</c> 0. The SDK still validates nothing,
    /// because that is upstream's rule and it will go stale — and its crowdfunding sibling, which looks like
    /// the same endpoint, does <b>not</b> behave this way.</para>
    ///
    /// <para><b><c>limit</c> is ignored by this path and is not offered.</b> Measured 2026-08-31,
    /// <c>name=Apple&amp;limit=2</c> returned all 59 rows.</para></summary>
    /// <param name="name">The name to match. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per matching <i>filing</i> — dedupe by <see cref="FundraisingSearchHit.Cik"/>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingSearchHit>> SearchFundraisingAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising-search").With("name", name),
            FmpJsonContext.Default.ListFundraisingSearchHit, ct);
    }

    /// <summary>Rejects paging <c>stable/crowdfunding-offerings-latest</c> cannot serve.
    ///
    /// <para><b>Deliberately NOT shared with <see cref="ThrowIfFundraisingPagingOutOfRange"/>, and merging
    /// the two would be a defect rather than a tidy-up.</b> The two <c>-latest</c> paths measured different
    /// ceilings on 2026-08-31: this one returned 1000 rows at both <c>limit=1000</c> and <c>limit=5000</c>,
    /// while its sibling returned 100 at <c>limit=1000</c> and 100 at <c>limit=101</c>. Their defaults differ
    /// by the same factor of ten. A merged guard would either reject a legal request here or accept an
    /// illegal one there. <c>FundraisersTests</c> has a test for each direction.</para>
    ///
    /// <para><b>There is no upper bound on <c>page</c>, on purpose.</b> Measured 2026-08-31, <c>page=1000</c>
    /// answered HTTP 200 with rows. A ceiling invented here would reject requests FMP serves, and the real
    /// hazard — a page-until-empty loop that never terminates — is not something a bound can fix. It is
    /// documented on <see cref="GetCrowdfundingOfferingsLatestAsync"/> instead.</para>
    ///
    /// <para><c>limit</c> is rejected at zero and below rather than passed on, because <c>limit=0</c>
    /// returns <b>one row</b> — not an error, and not nothing.</para></summary>
    private static void ThrowIfCrowdfundingPagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxCrowdfundingPageSize, nameof(limit));
        }

        if (page is { } index) ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
    }

    /// <summary>Rejects paging <c>stable/fundraising-latest</c> cannot serve — a tenth of what its
    /// crowdfunding sibling accepts.
    ///
    /// <para>See <see cref="ThrowIfCrowdfundingPagingOutOfRange"/> for why these are two methods and not
    /// one. The distinct names are what make the divergence legible at every call site.</para></summary>
    private static void ThrowIfFundraisingPagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxFundraisingPageSize, nameof(limit));
        }

        if (page is { } index) ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
    }
}
