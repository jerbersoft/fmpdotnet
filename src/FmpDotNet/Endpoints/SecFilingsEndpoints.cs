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
/// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/> and its neighbours
/// answer filings; the three <c>FindCompany*</c> methods
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

    /// <summary>The largest page any of the five filing paths will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="CompanyEndpoints.MaxMergerAcquisitionPageSize"/>: measured 2026-08-28, <c>limit=2000</c> and
    /// <c>limit=5000</c> each answered exactly 1,000 rows with HTTP 200 and nothing in the body to say the
    /// request had been trimmed. These feeds genuinely paginate — page 0 and page 1 return disjoint rows — so a
    /// caller who asks for 5,000 and advances <c>page</c> by 5,000 reads a fifth of the archive and is never
    /// told. Every method here therefore rejects a larger <c>limit</c> rather than passing it on to be
    /// clamped.</para></summary>
    public const int MaxSecFilingPageSize = 1000;

    /// <summary>The 8-K feed — <c>stable/sec-filings-8k</c>, every current-report filing across the market,
    /// newest first.
    ///
    /// <para><b>Filtered by form.</b> Measured 2026-08-28 over 1,000 rows, <c>formType</c> was <c>8-K</c> on all
    /// 1,000. <see cref="SecFiling.HasFinancials"/> varies here — null on 107, false on 725, true on 168 — and
    /// carries real information, unlike on
    /// <see cref="GetFilingsWithFinancialsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/>.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> filter
    /// <see cref="SecFiling.AcceptedDate"/>, not <see cref="SecFiling.FilingDate"/>.</b> A response therefore
    /// carries rows whose <c>FilingDate</c> falls outside the range you asked for — 21 of them on the measured
    /// five-day window. They are not errors and are not dropped; see <see cref="SecFiling"/> for the hypothesis
    /// test that established it.</para>
    ///
    /// <para>Both ends are optional and the endpoint answers without them.</para></summary>
    /// <param name="from">Start of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="to">End of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative,
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxSecFilingPageSize"/>, or both ends of the range
    /// were supplied with <paramref name="to"/> earlier than <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> Get8KFilingsAsync(
        LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/sec-filings-8k")
                .With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }

    /// <summary>The feed of filings that carry financial data — <c>stable/sec-filings-financials</c>.
    ///
    /// <para><b>Filtered by content, not by form.</b> Measured 2026-08-28 over 1,000 rows, <c>formType</c> was
    /// <c>8-K</c> 861 times, <c>6-K</c> 137 and <c>10-K</c> twice, while
    /// <see cref="SecFiling.HasFinancials"/> was <c>true</c> on all 1,000 — so that property is constant here
    /// and tells a caller nothing. This is the same row shape as
    /// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/> over a different
    /// selection.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> filter
    /// <see cref="SecFiling.AcceptedDate"/>.</b> This is the endpoint the hypothesis test was run against:
    /// 2025-03-01 to 2025-03-05 answered 722 rows — comfortably under the cap, so truncation cannot explain it —
    /// of which 16 carried a <c>FilingDate</c> past the requested <c>to</c>, and all 16 of those carried an
    /// <c>AcceptedDate</c> inside it, with zero rows in the whole response falling outside.</para></summary>
    /// <param name="from">Start of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="to">End of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">As
    /// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> GetFilingsWithFinancialsAsync(
        LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/sec-filings-financials")
                .With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }

    /// <summary>Filings for one symbol over a date range — <c>stable/sec-filings-search/symbol</c>.
    ///
    /// <para>Every form type, not just 8-Ks: measured 2026-08-28, AAPL over 2025 answered 80 rows including
    /// <c>8-K</c>, <c>4</c>, <c>25-NSE</c> and <c>10-K</c>.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> are required, and that is FMP's rule rather
    /// than a choice made here.</b> The endpoint reveals its requirements one at a time: <c>symbol</c> alone
    /// answers 400 "Invalid or missing query parameter - from", and <c>symbol</c> with <c>from</c> answers the
    /// same for <c>to</c>. An optional parameter would ship a signature whose default can only fail, so the
    /// compiler enforces what FMP would otherwise charge a call to tell you.</para>
    ///
    /// <para><b>The range filters <see cref="SecFiling.AcceptedDate"/>.</b> Measured on the sibling form-type
    /// path 2026-08-28: 398 rows over a five-day window, of which 7 carried a <c>FilingDate</c> outside
    /// it.</para>
    ///
    /// <para>No <see cref="SecFiling.HasFinancials"/> on this path — the field is absent from the payload, so it
    /// binds null on every row.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="from">Start of the range, inclusive. Required.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>; empty for an unknown symbol,
    /// not an error.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative,
    /// <paramref name="limit"/> is out of range, or <paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchBySymbolAsync(
        string symbol, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return SearchAsync("stable/sec-filings-search/symbol", "symbol", symbol, from, to, page, limit, ct);
    }

    /// <summary>Filings for one Central Index Key over a date range — <c>stable/sec-filings-search/cik</c>.
    ///
    /// <para>The same rows as <see cref="SearchBySymbolAsync"/> where both identify the same filer: measured
    /// 2026-08-28, <c>symbol=AAPL</c> and <c>cik=0000320193</c> over 2025 returned byte-identical bodies of 80
    /// rows, and the unpadded <c>320193</c> returned the same 80.</para>
    ///
    /// <para>Reach for this one when the filer has no ticker — most SEC registrants do not.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded.</param>
    /// <param name="from">Start of the range, inclusive. Required — see
    /// <see cref="SearchBySymbolAsync"/>.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>; empty for an unknown CIK,
    /// not an error.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="SearchBySymbolAsync"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchByCikAsync(
        string cik, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return SearchAsync("stable/sec-filings-search/cik", "cik", cik, from, to, page, limit, ct);
    }

    /// <summary>Every filing of one form type across the market over a date range —
    /// <c>stable/sec-filings-search/form-type</c>.
    ///
    /// <para><paramref name="formType"/> is EDGAR's own spelling — <c>"10-K"</c>, <c>"8-K"</c>, <c>"4"</c>,
    /// <c>"25-NSE"</c>. Not validated here and not an enum, for the reason on
    /// <see cref="SecFiling.FormType"/>: EDGAR defines hundreds and a value this SDK has never seen must not
    /// cost the caller the call.</para>
    ///
    /// <para>Whole-market and therefore wide: measured 2026-08-28, <c>10-K</c> over one January month answered
    /// 398 rows, and over a recent 90-day window it filled the default page. Page it, or narrow the
    /// range.</para></summary>
    /// <param name="formType">The EDGAR form type, spelled as EDGAR spells it.</param>
    /// <param name="from">Start of the range, inclusive. Required — see
    /// <see cref="SearchBySymbolAsync"/>.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>; empty for an unknown form
    /// type, not an error.</returns>
    /// <exception cref="ArgumentException"><paramref name="formType"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="SearchBySymbolAsync"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchByFormTypeAsync(
        string formType, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formType);
        return SearchAsync("stable/sec-filings-search/form-type", "formType", formType, from, to, page, limit, ct);
    }

    /// <summary>The body the three <c>sec-filings-search/*</c> paths share: one required identifier, one
    /// required range, and the page-size cap.
    ///
    /// <para>Extracted rather than written three times, which is the trigger #29 named when it left the
    /// duplicated <c>Batch</c> helper alone at two call sites. The two feeds above are still written out for the
    /// same reason: they are two.</para></summary>
    private Task<IReadOnlyList<SecFiling>> SearchAsync(
        string path, string parameter, string value, LocalDate from, LocalDate to, int page, int limit,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest(path)
                .With(parameter, value).With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }

    /// <summary>The registrant behind one ticker — <c>stable/sec-filings-company-search/symbol</c>.
    ///
    /// <para><b>Returns <see cref="IndustryClassification"/>, the same seven-field row
    /// <see cref="DirectoryEndpoints.GetIndustryClassificationsAsync"/> and
    /// <see cref="SearchEndpoints.FindIndustryClassificationAsync"/> serve.</b> Measured 2026-08-28 for CIK
    /// <c>0000070858</c>, this path and <c>all-industry-classification</c> returned byte-identical values for
    /// all six non-address fields — the same data, not merely the same field names. The address differs only in
    /// encoding, and <see cref="Serialization.BusinessAddressJsonConverter"/> makes that invisible.</para>
    ///
    /// <para><b>No <c>limit</c> and no <c>page</c>, because the endpoint honours neither.</b> Measured
    /// 2026-08-28, the name variant answered 52 rows with and without <c>limit=5</c>. Take what comes back and
    /// page it yourself.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Never <see langword="null"/>; empty for an unknown symbol, not
    /// an error.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank — FMP answers 400
    /// naming the parameter, so it is raised here instead of bought.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyBySymbolAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return FindCompanyAsync("stable/sec-filings-company-search/symbol", "symbol", symbol, ct);
    }

    /// <summary>The registrant behind one Central Index Key —
    /// <c>stable/sec-filings-company-search/cik</c>.
    ///
    /// <para>The route for the majority of SEC registrants, which have no ticker. Measured 2026-08-28, the
    /// padded and unpadded forms of the CIK each answered the same single row, identical to what
    /// <see cref="FindCompanyBySymbolAsync"/> answers for the same filer.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Never <see langword="null"/>; empty for an unknown CIK, not an
    /// error.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return FindCompanyAsync("stable/sec-filings-company-search/cik", "cik", cik, ct);
    }

    /// <summary>Registrants whose name matches — <c>stable/sec-filings-company-search/name</c>.
    ///
    /// <para><b>Matching is loose and its exact rule was not established.</b> Measured 2026-08-28:
    /// <c>Apple</c>, <c>apple</c> and <c>Appl</c> each answered the same 52 rows, so it is case-insensitive and
    /// not an exact comparison; the results include <c>APPLING PARTNERS, LLC</c>, which contains no "apple" at
    /// all, so it is looser than a substring test. A single character, <c>a</c>, answered <b>0</b> rows, so very
    /// short queries are rejected rather than matching broadly. This SDK records what it saw and asserts no
    /// rule.</para>
    ///
    /// <para><b>Most rows come back unclassified.</b> Four of the first five carry a blank
    /// <see cref="IndustryClassification.SicCode"/> and <see cref="IndustryClassification.IndustryTitle"/>, and
    /// four carry the literal string <c>"None"</c> as their symbol — see
    /// <see cref="IndustryClassification.Symbol"/>.</para>
    ///
    /// <para>No <c>limit</c>: measured 2026-08-28, <c>company=Apple</c> answered 52 rows with and without
    /// one.</para></summary>
    /// <param name="company">The name to match. Matched loosely — see above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Never <see langword="null"/>; empty for an unmatched name, not
    /// an error — including when FMP considers the query too short.</returns>
    /// <exception cref="ArgumentException"><paramref name="company"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyByNameAsync(
        string company, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        return FindCompanyAsync("stable/sec-filings-company-search/name", "company", company, ct);
    }

    /// <summary>The body the three <c>sec-filings-company-search/*</c> paths share: one required parameter and
    /// nothing else. Extracted at three call sites, for the reason on <see cref="SearchAsync"/>.</summary>
    private Task<IReadOnlyList<IndustryClassification>> FindCompanyAsync(
        string path, string parameter, string value, CancellationToken ct) =>
        transport.GetListAsync(
            new FmpRequest(path).With(parameter, value),
            FmpJsonContext.Default.ListIndustryClassification, ct);
}
