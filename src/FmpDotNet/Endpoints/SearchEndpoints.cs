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

    /// <summary>Finds listings whose <b>ticker</b> matches <paramref name="query"/> — 7 rows for <c>AAPL</c>,
    /// measured 2026-08-27.
    ///
    /// <para><b>A prefix match across every exchange, not an exact lookup.</b> <c>query=AA</c> answered 50 rows.
    /// Fifty is also the undocumented default cap — pass <paramref name="limit"/> to change it.</para>
    ///
    /// <para><b>Returns listings, not companies.</b> Apple appears once per exchange, each with its own symbol
    /// and currency, so taking the first row picks one arbitrarily. Narrow with
    /// <paramref name="exchange"/> instead.</para></summary>
    /// <param name="query">The ticker or ticker prefix. Required and non-blank.</param>
    /// <param name="limit">Rows to return. Omitted by default, which asks FMP for its own default of 50.</param>
    /// <param name="exchange">Restricts to one exchange by short code — <c>NASDAQ</c>. Undocumented by FMP and
    /// measured working: <c>AAPL</c> narrowed from 7 rows to 1. Validate against
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/>; an unknown code answers an empty list, not an
    /// error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matches in FMP's order. Empty when nothing matched — and also empty when the query was not
    /// understood, which this endpoint does not distinguish. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolSearchResult>> FindBySymbolAsync(
        string query, int? limit = null, string? exchange = null, CancellationToken ct = default) =>
        QueryAsync("stable/search-symbol", query, limit, exchange, ct);

    /// <summary>Finds listings whose <b>company name</b> matches <paramref name="query"/> — 37 rows for
    /// <c>Apple</c>, measured 2026-08-27.
    ///
    /// <para>The same row shape and the same behaviour as
    /// <see cref="FindBySymbolAsync(string, int?, string?, CancellationToken)"/>, searching the other
    /// field.</para></summary>
    /// <param name="query">The company name or a fragment of it. Required and non-blank.</param>
    /// <param name="limit">Rows to return. Omitted by default.</param>
    /// <param name="exchange">Restricts to one exchange by short code. See the sibling method.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matches in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolSearchResult>> FindByNameAsync(
        string query, int? limit = null, string? exchange = null, CancellationToken ct = default) =>
        QueryAsync("stable/search-name", query, limit, exchange, ct);

    /// <summary>Resolves an SEC Central Index Key to the listings it covers.
    ///
    /// <para><b>Accepts the padded and the bare form alike</b> — <c>0000320193</c> and <c>320193</c> both answered
    /// the same single row on 2026-08-27 — and always answers with the ten-character padded form, matching
    /// <see cref="CikEntry.Cik"/>.</para>
    ///
    /// <para>This is the useful direction for CIK: <c>search-exchange-variants</c> returns one only for a
    /// symbol's primary listing, and <see cref="DirectoryEndpoints.StreamCikListAsync"/> is a 52-request walk of
    /// the whole registry.</para></summary>
    /// <param name="cik">The Central Index Key, padded or bare. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown CIK. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CikSearchResult>> FindByCikAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/search-cik").With("cik", cik),
            FmpJsonContext.Default.ListCikSearchResult, ct);
    }

    /// <summary>Resolves a CUSIP to the listings that carry it — 4 rows for <c>037833100</c>, measured
    /// 2026-08-27.
    ///
    /// <para><b>The rows carry a market capitalisation in an unstated currency</b>, and the first is not the US
    /// listing. See <see cref="CusipSearchResult.MarketCap"/> before ordering or comparing them.</para>
    ///
    /// <para>Takes no <c>limit</c>: the endpoint ignores it — 4 rows asked down to 1 still answered 4.</para></summary>
    /// <param name="cusip">The nine-character CUSIP. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown CUSIP. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cusip"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CusipSearchResult>> FindByCusipAsync(string cusip, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cusip);
        return transport.GetListAsync(
            new FmpRequest("stable/search-cusip").With("cusip", cusip),
            FmpJsonContext.Default.ListCusipSearchResult, ct);
    }

    /// <summary>Resolves an ISIN to the listings that carry it — 5 rows for <c>US0378331005</c>, measured
    /// 2026-08-27.
    ///
    /// <para>Same caveats as <see cref="FindByCusipAsync(string, CancellationToken)"/>: an unstated market-cap
    /// currency, and no <c>limit</c> because the endpoint ignores it. One of the five measured rows reported a
    /// market capitalisation of zero.</para></summary>
    /// <param name="isin">The twelve-character ISIN. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown ISIN. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="isin"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IsinSearchResult>> FindByIsinAsync(string isin, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isin);
        return transport.GetListAsync(
            new FmpRequest("stable/search-isin").With("isin", isin),
            FmpJsonContext.Default.ListIsinSearchResult, ct);
    }

    /// <summary>Every exchange <paramref name="symbol"/> trades on, each with a full company profile attached —
    /// 6 rows for <c>AAPL</c> measured 2026-08-27, spanning USD, EUR, MXN and CAD.
    ///
    /// <para>The question this answers is "where else does this trade, and under what ticker" — the reliable way
    /// to find a symbol's foreign listings, and better than appending
    /// <see cref="ExchangeInfo.SymbolSuffix"/> by hand, which is the literal string <c>"N/A"</c> on five
    /// exchanges.</para>
    ///
    /// <para><b>The rows are <see cref="ExchangeVariant"/>, not <see cref="CompanyProfile"/>, and the difference
    /// is not cosmetic.</b> FMP serves a v3-era shape here: <c>mktCap</c> for <c>marketCap</c>, <c>lastDiv</c> for
    /// <c>lastDividend</c>, and — the one that produces silently wrong code —
    /// <see cref="ExchangeVariant.Exchange"/> holding the display name where
    /// <see cref="CompanyProfile.Exchange"/> holds the short code. See <see cref="ExchangeVariant"/> for the
    /// measured comparison.</para>
    ///
    /// <para><b>Prices and market caps are in each listing's own currency.</b> Comparing them across rows without
    /// reading <see cref="ExchangeVariant.Currency"/> compares magnitudes, not values.</para></summary>
    /// <param name="symbol">The ticker to find listings for. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per exchange, in FMP's order, the primary listing first. Empty for an unknown symbol —
    /// HTTP 200, not an error. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeVariant>> GetExchangeVariantsAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/search-exchange-variants").With("symbol", symbol),
            FmpJsonContext.Default.ListExchangeVariant, ct);
    }

    /// <summary>The shared body of the two query-shaped searches, which differ only in path.</summary>
    private Task<IReadOnlyList<SymbolSearchResult>> QueryAsync(
        string path, string query, int? limit, string? exchange, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is not null) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit.Value);
        return transport.GetListAsync(
            new FmpRequest(path).With("query", query).With("limit", limit).With("exchange", exchange),
            FmpJsonContext.Default.ListSymbolSearchResult, ct);
    }
}
