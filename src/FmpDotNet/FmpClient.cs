using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet;

/// <summary>Entry point to the FMP API, grouped the way FMP's own documentation groups it.
///
/// <para>Resolve this from dependency injection after calling <c>AddFmp</c> from the
/// <c>FmpDotNet.Extensions.DependencyInjection</c> package, or build one without a container through that
/// package's <c>FmpClientFactory.Create</c>. Both go through the same wiring.</para>
///
/// <para>A client is a composition of two transports. Every endpoint group takes exactly one — the ordinary
/// transport, or the bulk transport for <see cref="Bulk"/> — and holds no other state, so the pair determines
/// the whole client and a group can never be declared here yet left out of the wiring.</para></summary>
public sealed class FmpClient : IDisposable
{
    private IDisposable? _owned;

    /// <summary>Composes the client from the two transports.</summary>
    public FmpClient(FmpTransport standard, FmpBulkTransport bulk) : this(standard, bulk, null) { }

    /// <summary>Composes the client from the two transports and takes ownership of <paramref name="owned"/>,
    /// which <see cref="Dispose"/> disposes.
    ///
    /// <para><c>FmpClientFactory.Create</c> hands over the private container it built the transports from. A
    /// client resolved from a host's own container owns nothing, and its <see cref="Dispose"/> does
    /// nothing.</para></summary>
    public FmpClient(FmpTransport standard, FmpBulkTransport bulk, IDisposable? owned)
    {
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(bulk);
        _owned = owned;

        Company = new CompanyEndpoints(standard);
        Directory = new DirectoryEndpoints(standard);
        Statements = new StatementEndpoints(standard);
        Calendar = new CalendarEndpoints(standard);
        Analyst = new AnalystEndpoints(standard);
        Economics = new EconomicsEndpoints(standard);
        Search = new SearchEndpoints(standard);
        SecFilings = new SecFilingsEndpoints(standard);
        InstitutionalOwnership = new InstitutionalOwnershipEndpoints(standard);
        InsiderTrades = new InsiderTradesEndpoints(standard);
        Congress = new CongressEndpoints(standard);
        Transcripts = new TranscriptsEndpoints(standard);
        Esg = new EsgEndpoints(standard);
        Cot = new CotEndpoints(standard);
        Quote = new QuoteEndpoints(standard);
        Chart = new ChartEndpoints(standard);
        Bulk = new BulkEndpoints(bulk);
        TechnicalIndicators = new TechnicalIndicatorsEndpoints(standard);
        MarketPerformance = new MarketPerformanceEndpoints(standard);
        EtfAndFunds = new EtfAndFundsEndpoints(standard);
        Indexes = new IndexesEndpoints(standard);
        MarketHours = new MarketHoursEndpoints(standard);
        News = new NewsEndpoints(standard);
        Fundraisers = new FundraisersEndpoints(standard);
        DiscountedCashFlow = new DiscountedCashFlowEndpoints(standard);
    }
    /// <summary>Company profiles and identifiers.</summary>
    public CompanyEndpoints Company { get; }

    /// <summary>What exists — the symbol universe, and the sector and industry labels everything else classifies
    /// against.</summary>
    public DirectoryEndpoints Directory { get; }

    /// <summary>The period-shaped fundamentals: statements, ratios, metrics, growth, enterprise values and
    /// scores.</summary>
    public StatementEndpoints Statements { get; }

    /// <summary>What a company has reported and when it reports next — per-symbol earnings history, and the
    /// whole-market earnings calendar.
    ///
    /// <para>Both answer dates rather than periods, which is why they are not on <see cref="Statements"/>: neither
    /// takes a <see cref="FiscalPeriod"/>, and the calendar takes no symbol at all.</para>
    ///
    /// <para><b>Two of the nine methods make more than one request.</b>
    /// <see cref="CalendarEndpoints.GetEarningsCalendarAsync"/> and
    /// <see cref="CalendarEndpoints.GetDividendsCalendarAsync"/> walk FMP's <c>page</c> cursor past a 4000-row
    /// cap — a full year of dividends is 8 requests — and both report a seam defect that costs about 3% of a wide
    /// range. Read either method before asking for a range wider than a page.</para></summary>
    public CalendarEndpoints Calendar { get; }

    /// <summary>Forward consensus — what analysts expect rather than what was reported.</summary>
    public AnalystEndpoints Analyst { get; }

    /// <summary>Macroeconomic releases. Global and unfiltered by design — see
    /// <see cref="EconomicsEndpoints"/>.</summary>
    public EconomicsEndpoints Economics { get; }

    /// <summary>Finding securities by what they are rather than by symbol — the screener.
    ///
    /// <para>The complement to <see cref="Directory"/>: that answers "everything FMP knows" as a multi-megabyte
    /// download, this answers a question about the universe and returns only the matches.</para></summary>
    public SearchEndpoints Search { get; }

    /// <summary>What companies have filed with the SEC, and who the filers are — EDGAR registrant profiles,
    /// the 8-K and financial-statement filing feeds, and filing search by symbol, CIK or form type.
    ///
    /// <para>Three of the twelve paths FMP documents under this heading are reference lists rather than
    /// filings, and are on <see cref="Directory"/> and <see cref="Search"/> instead. See
    /// <see cref="SecFilingsEndpoints"/>.</para></summary>
    public SecFilingsEndpoints SecFilings { get; }

    /// <summary>Who owns what, as institutions report it quarterly on Form 13F — holdings, holder analytics,
    /// performance and industry breakdowns, plus SC 13D/G beneficial-ownership disclosures.
    ///
    /// <para>The 5% stake disclosures FMP files under Insider Trades are here rather than on
    /// <see cref="InsiderTrades"/>, because an SC 13D/G is an institutional stake filing and not a Form 4
    /// transaction. See <see cref="InstitutionalOwnershipEndpoints"/>.</para></summary>
    public InstitutionalOwnershipEndpoints InstitutionalOwnership { get; }

    /// <summary>What company insiders file on Forms 3, 4 and 5 — the whole-market feed, a four-way search,
    /// per-symbol statistics, and the two reference lists behind them.
    ///
    /// <para>SC 13D/G beneficial-ownership disclosures are <b>not</b> here: FMP documents them under this
    /// heading, but they are institutional stake filings rather than insider transactions and live on
    /// <see cref="InstitutionalOwnership"/>.</para></summary>
    public InsiderTradesEndpoints InsiderTrades { get; }

    /// <summary>Congressional disclosure — what members of Congress traded, who they are, and what Senators
    /// are worth.
    ///
    /// <para>Twelve paths over five row shapes, eight of them answering the same trade row. Sits beside
    /// <see cref="InsiderTrades"/> because both are people-disclose-their-trades feeds, and shares nothing
    /// with it on the wire.</para></summary>
    public CongressEndpoints Congress { get; }

    /// <summary>Earnings call transcripts — one call in full, a symbol's index of calls, and the
    /// whole-market feed of what was just published.
    ///
    /// <para>Sits beside <see cref="Calendar"/> rather than on it because a transcript is the record of a
    /// call rather than a scheduled event, and because the three paths take a symbol-and-period key that
    /// nothing on <see cref="Calendar"/> takes. Which symbols have transcripts at all is on
    /// <see cref="Directory"/>. See <see cref="TranscriptsEndpoints"/>.</para></summary>
    public TranscriptsEndpoints Transcripts { get; }

    /// <summary>Environmental, social and governance data — per-filing scores, rating history, and the
    /// sector averages to read either against.
    ///
    /// <para>Its own facade rather than a corner of <see cref="Company"/> because two of its three paths take
    /// no symbol at all, and the benchmark is a whole-market reference table. See
    /// <see cref="EsgEndpoints"/>.</para></summary>
    public EsgEndpoints Esg { get; }

    /// <summary>The CFTC's weekly Commitment of Traders report — who is positioned how in the futures
    /// markets.
    ///
    /// <para>The only group in this SDK keyed on a futures contract code rather than an equity symbol, which
    /// is why it is its own facade and not a corner of <see cref="Quote"/>. Its data is years stale on the
    /// current key — see <see cref="CotEndpoints"/> before reading an empty result as "no
    /// positions".</para></summary>
    public CotEndpoints Cot { get; }

    /// <summary>What something is trading at now — current prices, extended-hours prices, and trailing price
    /// changes.
    ///
    /// <para>Sixteen of FMP's endpoints returning five row shapes. One <see cref="QuoteEndpoints.GetQuoteAsync"/>
    /// covers equities, ETFs, indices, commodities, forex and crypto alike, which is why there are no per-asset-class
    /// facades here.</para></summary>
    public QuoteEndpoints Quote { get; }

    /// <summary>Price history for one symbol — daily bars in four adjustments, and intraday bars at six sizes.
    ///
    /// <para>The counterpart to <see cref="Quote"/>: that answers "now", this answers "before". Everything in it
    /// truncates silently, in two different ways — see <see cref="ChartEndpoints"/>.</para></summary>
    public ChartEndpoints Chart { get; }

    /// <summary>Whole-universe CSV downloads. Streamed, and throttled separately — see
    /// <see cref="BulkEndpoints"/>.</summary>
    public BulkEndpoints Bulk { get; }

    /// <summary>Nine technical indicators over one price series —
    /// <see cref="TechnicalIndicatorsEndpoints"/>.
    ///
    /// <para>One method reaches all nine paths. Read
    /// <see cref="TechnicalIndicatorsEndpoints.GetAsync"/> before trusting a value computed over a narrow
    /// range: four of the nine change with the window, and one of them by more than 200%.</para></summary>
    public TechnicalIndicatorsEndpoints TechnicalIndicators { get; }

    /// <summary>How the market moved — the gainers, losers and most-actives lists, and sector and industry
    /// performance and valuation, by day or over a range.
    ///
    /// <para>Every sector and industry method answers for <b>one exchange</b> and requires it. See
    /// <see cref="MarketPerformanceEndpoints"/>.</para></summary>
    public MarketPerformanceEndpoints MarketPerformance { get; }

    /// <summary>ETFs and mutual funds — holdings, exposures, fund fact sheets, and the SEC N-PORT filings
    /// behind them.
    ///
    /// <para><b>No path in this group paginates and none can be narrowed</b>, so two of the nine methods can
    /// return tens of thousands of rows. See <see cref="EtfAndFundsEndpoints"/> before calling
    /// <see cref="EtfAndFundsEndpoints.GetEtfHoldingsAsync"/> or
    /// <see cref="EtfAndFundsEndpoints.SearchFundsByNameAsync"/> in a loop.</para></summary>
    public EtfAndFundsEndpoints EtfAndFunds { get; }

    /// <summary>Index membership — the Dow Jones, S&amp;P 500 and Nasdaq 100 member lists, and every change
    /// FMP records to them.
    ///
    /// <para><b>No method here takes a parameter</b>, which is measured rather than incidental, and the
    /// change feeds cannot be replayed into a membership list. See <see cref="IndexesEndpoints"/> before
    /// reaching for either.</para></summary>
    public IndexesEndpoints Indexes { get; }

    /// <summary>When exchanges trade — opening and closing bells for 81 exchanges, and the holiday calendar
    /// behind them.
    ///
    /// <para>Its own facade rather than a corner of <see cref="Indexes"/>: the two groups share no path
    /// prefix, no parameter, no record and no concept. Read
    /// <see cref="MarketHoursEndpoints.GetHolidaysAsync"/> before passing a date range — the window is
    /// half-open, so <c>from</c> is exclusive and a range whose bounds are equal is rejected rather than
    /// sent.</para></summary>
    public MarketHoursEndpoints MarketHours { get; }

    /// <summary>News — five whole-market feeds, four symbol-filtered searches, and FMP's own articles.
    ///
    /// <para><b>The <c>-latest</c> feeds cannot be filtered and the searches cannot be unfiltered</b>, which
    /// is why they are separate method families rather than one with an optional symbol: measured
    /// 2026-08-29, <c>symbols</c> is accepted and silently ignored on all five feeds, and omitting it on a
    /// search substitutes a hard-coded ticker rather than answering broadly. Read
    /// <see cref="NewsEndpoints.GetArticlesAsync"/> before paging that path — it has no page ceiling and
    /// repeats its last page for ever.</para></summary>
    public NewsEndpoints News { get; }

    /// <summary>Fundraisers — Regulation Crowdfunding (Form C) and Regulation D (Form D) offerings.
    ///
    /// <para><b>Two corpora that do not overlap</b>, which is why the six methods name their corpus rather
    /// than taking it as an argument: measured 2026-08-31, a Form C issuer's CIK answers <b>0 rows</b> on the
    /// Form D paths and vice versa, both at HTTP 200 with an empty array. Read
    /// <see cref="FundraisersEndpoints.GetCrowdfundingOfferingsLatestAsync"/> before paging — neither
    /// <c>-latest</c> path has a page ceiling, and the two have ceilings and defaults that differ by a factor
    /// of ten from each other.</para></summary>
    public FundraisersEndpoints Fundraisers { get; }

    /// <summary>Discounted cash flow — FMP's own valuations, and two models you can drive with your own
    /// assumptions.
    ///
    /// <para><b>Levered and unlevered are different questions with different answers</b> — measured
    /// 2026-08-27/31, KO reads 83.71 unlevered against 49.77 levered — so the SDK gives them separate return
    /// types. And <b>the plain and custom paths do not reconcile with each other or with their own price
    /// columns</b>, in both directions: do not reconstruct a price from any of them. Read
    /// <see cref="CustomDcfAssumptions"/> before passing overrides — the two custom paths honour two
    /// different vocabularies and each silently discards the other's.</para></summary>
    public DiscountedCashFlowEndpoints DiscountedCashFlow { get; }

    /// <summary>Disposes whatever this client owns — the private container behind a factory-built client — and
    /// nothing else. A no-op on a client resolved from dependency injection, and safe to call twice.</summary>
    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();
}
