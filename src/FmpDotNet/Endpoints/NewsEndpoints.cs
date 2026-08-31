using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>News — five whole-market feeds, four symbol-filtered searches, and FMP's own articles.
///
/// <para><b>Ten paths, two shapes.</b> The nine <c>news/*</c> paths returned the same eight keys in the same
/// order across 2,250 rows measured 2026-08-29 and all answer <see cref="NewsArticle"/>;
/// <c>stable/fmp-articles</c> renames six of the same eight concepts and answers
/// <see cref="FmpArticle"/>.</para>
///
/// <para><b>Six things hold across this group, every one of them measured, and not one of them catchable by a
/// caller.</b> Every case below arrives at HTTP 200 with well-formed rows.</para>
///
/// <list type="number">
///   <item><description><b>The <c>-latest</c> feeds cannot be filtered, and say nothing when you try.</b>
///     <c>symbols</c> is accepted and silently ignored on all five: measured 2026-08-29,
///     <c>stock-latest?symbols=AAPL</c> returned 20 rows carrying <b>20 distinct symbols</b>. That is why
///     the searches are separate methods that <i>require</i> a symbol rather than one family with an
///     optional one.</description></item>
///   <item><description><b>Omitting <c>symbols</c> on a search path does not mean "everything".</b> Each
///     substitutes one hard-coded default — AAPL on stock and press-releases, BTCUSD on crypto, EURUSD on
///     forex. The singular spelling <c>symbol=MSFT</c> is dropped and the default takes over, byte-identical
///     to the bare call. This SDK requires the parameter so neither can happen.</description></item>
///   <item><description><b>The symbol vocabulary is exact uppercase, and each category has its own.</b>
///     <c>symbols=aapl</c> and <c>symbols=Aapl</c> each returned <b>0 rows</b>;
///     <c>news/crypto?symbols=BTC</c> returned 0 while <c>BTCUSD</c> returned 250. Case is rejected here;
///     the vocabulary is not validated, for the reason <see cref="MarketHoursEndpoints"/> gives about
///     exchange codes — it is upstream's and will go stale.</description></item>
///   <item><description><b>There is an implicit three-month floor, and <c>to</c> alone falls off it.</b>
///     Measured 2026-08-29, no row older than 2026-05-29 05:25:00 — three calendar months, 92 days — was
///     reachable without an explicit <c>from</c>, on the <c>-latest</c> feeds as well as the searches. So
///     <c>to=2026-01-09</c> alone returns 0 rows while <c>from=2026-01-05&amp;to=2026-01-09</c> returns 20.
///     <b>An explicit <c>from</c> escapes the floor entirely</b> — <c>from=2011-01-01</c> reached rows dated
///     2011-02-24.</description></item>
///   <item><description><b>Malformed dates are dropped rather than rejected, and the surviving parameter
///     still applies.</b> <c>from=hello&amp;to=world</c> returned the default response byte-for-byte, and a
///     malformed <c>from</c> beside a valid <c>to</c> lands on the floor and returns nothing.</description></item>
///   <item><description><b>A row is an article-symbol pairing, not an article.</b> A multi-symbol query
///     returns the same article once per matching symbol — 19 of 250 urls twice on
///     <c>crypto?symbols=BTCUSD,ETHUSD</c>, every one under a different symbol, and zero same-symbol
///     repeats. Counting rows over-counts articles. This SDK does not deduplicate.</description></item>
/// </list>
///
/// <para><b>The two path families have different paging ceilings and the difference is not cosmetic.</b> The
/// nine feeds cap <c>limit</c> at <see cref="MaxFeedPageSize"/> and <c>page</c> at
/// <see cref="MaxFeedPage"/>, past which FMP answers HTTP 400. <c>fmp-articles</c> caps <c>limit</c> at
/// <see cref="MaxArticlePageSize"/> and has <b>no page ceiling at all</b> — see
/// <see cref="GetArticlesAsync"/> before writing a loop against it.</para></summary>
public sealed class NewsEndpoints(FmpTransport transport)
{
    /// <summary>The largest <c>limit</c> the nine <c>news/*</c> paths honour. Measured 2026-08-29,
    /// <c>limit=1000</c> and <c>limit=5000</c> both returned 250 rows, byte-identically.</summary>
    public const int MaxFeedPageSize = 250;

    /// <summary>The largest <c>page</c> the nine <c>news/*</c> paths accept. Measured 2026-08-29,
    /// <c>page=101</c> is HTTP 400 with a plain-text body.</summary>
    public const int MaxFeedPage = 100;

    /// <summary>The largest <c>limit</c> <c>stable/fmp-articles</c> honours. Measured 2026-08-29,
    /// <c>limit=201</c> is byte-identical to <c>limit=200</c>. <b>This path has no page ceiling</b>, so
    /// there is no constant for one.</summary>
    public const int MaxArticlePageSize = 200;

    /// <summary>Every story FMP carries, from <c>stable/news/general-latest</c>.
    ///
    /// <para><b>The one feed with no ticker at all.</b> <see cref="NewsArticle.Symbol"/> was null on
    /// <b>250 of 250</b> rows measured 2026-08-29. That is why there is no <c>SearchGeneralAsync</c>: this
    /// path has nothing to filter on.</para>
    ///
    /// <para>28 distinct publishers in 250 rows, led by Seeking Alpha with 51. Shares <b>zero</b> urls with
    /// any other feed.</para></summary>
    /// <param name="from">Earliest publication date. <b>Supply it to reach anything older than three
    /// months</b> — see the type's summary.</param>
    /// <param name="to">Latest publication date. Passing this <i>without</i> <paramref name="from"/> is
    /// accepted, and returns nothing for any date older than the floor.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>. Omit to take FMP's own
    /// default of 20.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's articles, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="limit"/> or <paramref name="page"/> is outside its
    /// measured range.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> GetGeneralLatestAsync(
        LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null,
        CancellationToken ct = default) =>
        Feed("stable/news/general-latest", from, to, limit, page, ct);

    /// <summary>Equity news across the whole market, from <c>stable/news/stock-latest</c>.
    ///
    /// <para><b>Unfiltered, and <c>symbols</c> would be ignored if this method offered it.</b> Use
    /// <see cref="SearchStockAsync"/> for one company. Measured 2026-08-29: 146 distinct symbols and 28
    /// publishers in 250 rows, led by The Motley Fool with 54, and <see cref="NewsArticle.Symbol"/> null on
    /// 46 of 250 — the untagged rows a whole-market feed carries.</para></summary>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's articles, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> GetStockLatestAsync(
        LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null,
        CancellationToken ct = default) =>
        Feed("stable/news/stock-latest", from, to, limit, page, ct);

    /// <summary>Cryptocurrency news across the whole market, from <c>stable/news/crypto-latest</c>.
    ///
    /// <para>39 distinct publishers in 250 rows measured 2026-08-29 — the widest of the five feeds — led by
    /// Blockchain News with 30, over 69 distinct symbols. <b>The six rows in the whole 2,250-row sample with
    /// a null <see cref="NewsArticle.Site"/> are all on this path and its search sibling.</b></para></summary>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's articles, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> GetCryptoLatestAsync(
        LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null,
        CancellationToken ct = default) =>
        Feed("stable/news/crypto-latest", from, to, limit, page, ct);

    /// <summary>Currency news across the whole market, from <c>stable/news/forex-latest</c>.
    ///
    /// <para><b>The narrowest feed of the five.</b> 9 distinct publishers in 250 rows measured 2026-08-29,
    /// of which FX Street alone supplied <b>136</b>, over 24 distinct symbols. A caller treating this as a
    /// representative sample of currency coverage is mostly reading one publisher.</para></summary>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's articles, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> GetForexLatestAsync(
        LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null,
        CancellationToken ct = default) =>
        Feed("stable/news/forex-latest", from, to, limit, page, ct);

    /// <summary>Company press releases across the whole market, from
    /// <c>stable/news/press-releases-latest</c>.
    ///
    /// <para><b>Not a separate corpus — a subset of <see cref="GetStockLatestAsync"/>.</b> Measured
    /// 2026-08-29, the two 250-row samples shared <b>53 urls</b>, while every other pair of feeds in this
    /// group shared zero. Reading both and concatenating double-counts those stories.</para>
    ///
    /// <para>6 distinct publishers, the narrowest vocabulary of the five, led by Newsfile Corp with 83.
    /// Daily volume is high: 964 rows on 2026-08-27 and 839 on 2026-01-14.</para></summary>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's releases, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> GetPressReleasesLatestAsync(
        LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null,
        CancellationToken ct = default) =>
        Feed("stable/news/press-releases-latest", from, to, limit, page, ct);

    /// <summary>Equity news for named companies, from <c>stable/news/stock</c>.
    ///
    /// <para><b><paramref name="symbols"/> is required here though the wire makes it optional</b>, because
    /// omitting it does not mean "everything": measured 2026-08-29 the bare call answers 20 <b>AAPL</b>
    /// rows, byte-identical to <c>symbol=MSFT</c> — the singular spelling is dropped and the default takes
    /// over. The unfiltered call already exists under its own name, <see cref="GetStockLatestAsync"/>, and
    /// is honest about taking no filter.</para>
    ///
    /// <para><b>This path is a filtered view of that feed, not a separate corpus.</b> Inside the window
    /// where both 250-row samples were complete, <b>all 21</b> rows here appeared in <c>stock-latest</c> by
    /// url.</para>
    ///
    /// <para>History reaches 2011 with an explicit <paramref name="from"/> — the oldest row measured was
    /// 2011-02-24 08:30:00 — and 2010 and 2008 answered empty.</para></summary>
    /// <param name="symbols">Uppercase tickers. Blank entries are dropped; an entirely blank list is
    /// rejected. A list of 30 was accepted in one call. <b>Each matching article returns once per matching
    /// symbol.</b></param>
    /// <param name="from">Earliest publication date. Supply it to reach past the three-month floor.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching article-symbol pairings, newest first. Never <see langword="null"/>; empty for a
    /// symbol FMP has no news for, which is not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is entirely blank, or an entry is not
    /// uppercase.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> SearchStockAsync(
        IEnumerable<string> symbols, LocalDate? from = null, LocalDate? to = null, int? limit = null,
        int? page = null, CancellationToken ct = default) =>
        Search("stable/news/stock", symbols, from, to, limit, page, ct);

    /// <summary>Cryptocurrency news for named pairs, from <c>stable/news/crypto</c>.
    ///
    /// <para><b>The vocabulary is the PAIR, not the coin, and the wrong one is not an error.</b> Measured
    /// 2026-08-29, <c>symbols=BTC</c> returned <b>0 rows</b> while <c>symbols=BTCUSD</c> returned 250 — and
    /// a zero-row answer reads as "no news about Bitcoin". Omitting <paramref name="symbols"/> substitutes
    /// <c>BTCUSD</c>, which is why this SDK requires it.</para></summary>
    /// <param name="symbols">Uppercase pairs — <c>"BTCUSD"</c>, <c>"ETHUSD"</c>. Not bare coin
    /// symbols.</param>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching article-symbol pairings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is entirely blank, or an entry is not
    /// uppercase.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> SearchCryptoAsync(
        IEnumerable<string> symbols, LocalDate? from = null, LocalDate? to = null, int? limit = null,
        int? page = null, CancellationToken ct = default) =>
        Search("stable/news/crypto", symbols, from, to, limit, page, ct);

    /// <summary>Currency news for named pairs, from <c>stable/news/forex</c>.
    ///
    /// <para>Omitting <paramref name="symbols"/> substitutes <c>EURUSD</c> — measured 2026-08-29 — which is
    /// why this SDK requires it. The pair vocabulary is the same one
    /// <see cref="QuoteEndpoints.GetQuoteAsync"/> takes.</para></summary>
    /// <param name="symbols">Uppercase pairs — <c>"EURUSD"</c>, <c>"USDJPY"</c>.</param>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching article-symbol pairings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is entirely blank, or an entry is not
    /// uppercase.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> SearchForexAsync(
        IEnumerable<string> symbols, LocalDate? from = null, LocalDate? to = null, int? limit = null,
        int? page = null, CancellationToken ct = default) =>
        Search("stable/news/forex", symbols, from, to, limit, page, ct);

    /// <summary>Press releases for named companies, from <c>stable/news/press-releases</c>.
    ///
    /// <para>Omitting <paramref name="symbols"/> substitutes <c>AAPL</c> — measured 2026-08-29 — which is
    /// why this SDK requires it. History reaches 2015 with an explicit <paramref name="from"/>.</para></summary>
    /// <param name="symbols">Uppercase tickers.</param>
    /// <param name="from">Earliest publication date.</param>
    /// <param name="to">Latest publication date.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFeedPageSize"/>.</param>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxFeedPage"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching release-symbol pairings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is entirely blank, or an entry is not
    /// uppercase.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards, or paging is out of
    /// range.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<NewsArticle>> SearchPressReleasesAsync(
        IEnumerable<string> symbols, LocalDate? from = null, LocalDate? to = null, int? limit = null,
        int? page = null, CancellationToken ct = default) =>
        Search("stable/news/press-releases", symbols, from, to, limit, page, ct);

    /// <summary>Articles FMP wrote itself, from <c>stable/fmp-articles</c>.
    ///
    /// <para><b>The tenth path, and the one that behaves least like the other nine.</b> It answers
    /// <see cref="FmpArticle"/> rather than <see cref="NewsArticle"/>, its body is HTML, its timestamp is
    /// UTC rather than Eastern, and three of its four differences below cannot be guarded against.</para>
    ///
    /// <list type="number">
    ///   <item><description><b>It takes neither symbols nor dates, and offering either would be offering a
    ///     control that does nothing.</b> Measured 2026-08-29, <c>?symbols=AAPL</c> and
    ///     <c>?from=2026-01-05&amp;to=2026-01-09</c> each returned a response <b>byte-identical</b> to the
    ///     bare call.</description></item>
    ///   <item><description><b>It has no page ceiling and never errors — it repeats its last page for
    ///     ever.</b> Measured 2026-08-29, pages 1000, 1400, 1600, 2000 and 10000 all returned the identical
    ///     two rows. <b>A caller paging until the response is empty never terminates here.</b> Page against
    ///     <see cref="FmpArticle.Link"/> or <see cref="FmpArticle.Date"/>, not against emptiness. This
    ///     cannot be guarded, because the corpus end moves.</description></item>
    ///   <item><description><b>It may produce nothing on a given day.</b> Measured 2026-08-31, weekdays
    ///     carried 22 to 53 rows and the 2026-08-29 weekend carried <b>none</b>, after which the path was
    ///     silent for 60.5 hours. An empty or stale response is not evidence of a broken call.</description></item>
    ///   <item><description><b><see cref="FmpArticle.Content"/> is markup FMP wrote.</b> 200 of 200 rows
    ///     carried HTML tags, against 0 of 2,250 in <see cref="NewsArticle.Text"/>.</description></item>
    /// </list></summary>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxArticlePageSize"/>. Omit to take FMP's own
    /// default of 20.</param>
    /// <param name="page">Zero-based page index. <b>There is no upper bound</b> — see above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's articles, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxArticlePageSize"/>, or <paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FmpArticle>> GetArticlesAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        ThrowIfArticlePagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest("stable/fmp-articles").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListFmpArticle, ct);
    }

    /// <summary>Reads one of the five unfiltered feeds. No <c>symbols</c> parameter is built, because all
    /// five were measured accepting one and ignoring it.</summary>
    private Task<IReadOnlyList<NewsArticle>> Feed(
        string path, LocalDate? from, LocalDate? to, int? limit, int? page, CancellationToken ct)
    {
        DateRange.ThrowIfBackwards(from, to);
        ThrowIfFeedPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest(path).With("from", from).With("to", to).With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListNewsArticle, ct);
    }

    /// <summary>Reads one of the four symbol-filtered searches. Guards run in parameter order — symbols,
    /// then the range, then paging — so the message a caller gets names the first thing wrong with the call
    /// as they wrote it.</summary>
    private Task<IReadOnlyList<NewsArticle>> Search(
        string path, IEnumerable<string> symbols, LocalDate? from, LocalDate? to, int? limit, int? page,
        CancellationToken ct)
    {
        var request = Symbols(path, symbols);
        DateRange.ThrowIfBackwards(from, to);
        ThrowIfFeedPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            request.With("from", from).With("to", to).With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListNewsArticle, ct);
    }

    /// <summary>Builds a <c>symbols=</c> request, rejecting a list that would reach FMP empty or in the
    /// wrong case.
    ///
    /// <para>Modelled on <c>QuoteEndpoints.Batch</c>, and blank entries are dropped for the same measured
    /// reason: a trailing comma produces <c>symbols=AAPL,</c>, which FMP reads as a request for a symbol
    /// named "" — measured 2026-08-29 it matched <c>symbols=AAPL</c> exactly, so dropping blanks changes no
    /// measured behaviour and removes the shape that could. A list that is <i>entirely</i> blank throws,
    /// because omitting <c>symbols</c> upstream substitutes a hard-coded default rather than answering
    /// broadly.</para>
    ///
    /// <para><b>The case check is this group's own and has no counterpart on the batch quote paths.</b>
    /// Measured 2026-08-29, <c>symbols=aapl</c> and <c>symbols=Aapl</c> each return <b>0 rows</b> at HTTP
    /// 200 — a silent wrong answer that reads as "this symbol has no news", paid for out of the key's
    /// quota. Uppercasing on the caller's behalf is deliberately not done, for the reason
    /// <see cref="MarketHoursEndpoints"/> gives about compensating a range upstream: a request that does not
    /// match the arguments passed turns every debugging session into a puzzle. The message names the
    /// fix instead.</para></summary>
    private static FmpRequest Symbols(string path, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        var kept = symbols.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (kept.Count == 0)
        {
            throw new ArgumentException(
                "At least one non-blank symbol is required. Measured 2026-08-29, omitting 'symbols' upstream "
                + "does not mean 'everything' — it substitutes one hard-coded ticker per path (AAPL, BTCUSD "
                + "or EURUSD) and answers 20 rows about a company nobody asked for. For the unfiltered feed, "
                + "call the matching Get*LatestAsync method instead.",
                nameof(symbols));
        }

        foreach (var symbol in kept)
        {
            var upper = symbol.ToUpperInvariant();
            if (!string.Equals(symbol, upper, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{symbol}' is not uppercase. Measured 2026-08-29, symbols=aapl and symbols=Aapl each "
                    + $"return 0 rows at HTTP 200, which reads as 'this symbol has no news' rather than as "
                    + $"an error — a wasted call against the key's quota. Pass '{upper}'.",
                    nameof(symbols));
            }
        }

        return new FmpRequest(path).With("symbols", string.Join(',', kept));
    }

    /// <summary>Rejects paging the nine <c>news/*</c> paths cannot serve.
    ///
    /// <para><b>Deliberately NOT shared with <see cref="ThrowIfArticlePagingOutOfRange"/>, and merging the
    /// two would be a defect rather than a tidy-up.</b> The two families measured different ceilings on
    /// 2026-08-29: these paths cap <c>limit</c> at 250 and <c>page</c> at 100, while <c>fmp-articles</c>
    /// caps <c>limit</c> at 200 and has no page ceiling at all. A merged guard would either reject a legal
    /// page on that path or accept an illegal one here. <c>NewsTests</c> has a test for each direction.</para>
    ///
    /// <para><c>limit</c> is rejected at zero and below rather than passed on, because <c>limit=0</c> and
    /// <c>limit=-1</c> each return <b>one row</b> — not an error, and not nothing.</para></summary>
    private static void ThrowIfFeedPagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxFeedPageSize, nameof(limit));
        }

        if (page is { } index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, MaxFeedPage, nameof(page));
        }
    }

    /// <summary>Rejects paging <c>stable/fmp-articles</c> cannot serve — which is a shorter list than the
    /// feeds', and the difference is measured rather than an oversight.
    ///
    /// <para><b>There is no upper bound on <c>page</c> here, on purpose.</b> Measured 2026-08-29, pages
    /// 1000, 1400, 1600, 2000 and 10000 all answered HTTP 200 with the identical two rows rather than the
    /// 400 the feeds give past page 100. A ceiling invented here would reject requests FMP answers, and the
    /// real hazard — a page-until-empty loop that never terminates — is not something a bound can fix. It is
    /// documented on <see cref="GetArticlesAsync"/> instead.</para>
    ///
    /// <para>See <see cref="ThrowIfFeedPagingOutOfRange"/> for why these are two methods.</para></summary>
    private static void ThrowIfArticlePagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxArticlePageSize, nameof(limit));
        }

        if (page is { } index) ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
    }
}
