using FmpDotNet.Endpoints;

namespace FmpDotNet;

/// <summary>Entry point to the FMP API, grouped the way FMP's own documentation groups it.
///
/// <para>Resolve this from dependency injection after calling
/// <see cref="DependencyInjection.FmpServiceCollectionExtensions.AddFmp(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{FmpOptions})"/>.</para></summary>
public sealed class FmpClient(
    CompanyEndpoints company, DirectoryEndpoints directory, StatementEndpoints statements,
    CalendarEndpoints calendar, AnalystEndpoints analyst, EconomicsEndpoints economics,
    SearchEndpoints search, SecFilingsEndpoints secFilings, QuoteEndpoints quote, ChartEndpoints chart,
    BulkEndpoints bulk)
{
    /// <summary>Company profiles and identifiers.</summary>
    public CompanyEndpoints Company { get; } = company;

    /// <summary>What exists — the symbol universe, and the sector and industry labels everything else classifies
    /// against.</summary>
    public DirectoryEndpoints Directory { get; } = directory;

    /// <summary>The period-shaped fundamentals: statements, ratios, metrics, growth, enterprise values and
    /// scores.</summary>
    public StatementEndpoints Statements { get; } = statements;

    /// <summary>What a company has reported and when it reports next — per-symbol earnings history, and the
    /// whole-market earnings calendar.
    ///
    /// <para>Both answer dates rather than periods, which is why they are not on <see cref="Statements"/>: neither
    /// takes a <see cref="FiscalPeriod"/>, and the calendar takes no symbol at all.</para></summary>
    public CalendarEndpoints Calendar { get; } = calendar;

    /// <summary>Forward consensus — what analysts expect rather than what was reported.</summary>
    public AnalystEndpoints Analyst { get; } = analyst;

    /// <summary>Macroeconomic releases. Global and unfiltered by design — see
    /// <see cref="EconomicsEndpoints"/>.</summary>
    public EconomicsEndpoints Economics { get; } = economics;

    /// <summary>Finding securities by what they are rather than by symbol — the screener.
    ///
    /// <para>The complement to <see cref="Directory"/>: that answers "everything FMP knows" as a multi-megabyte
    /// download, this answers a question about the universe and returns only the matches.</para></summary>
    public SearchEndpoints Search { get; } = search;

    /// <summary>What companies have filed with the SEC, and who the filers are — EDGAR registrant profiles,
    /// the 8-K and financial-statement filing feeds, and filing search by symbol, CIK or form type.
    ///
    /// <para>Three of the twelve paths FMP documents under this heading are reference lists rather than
    /// filings, and are on <see cref="Directory"/> and <see cref="Search"/> instead. See
    /// <see cref="SecFilingsEndpoints"/>.</para></summary>
    public SecFilingsEndpoints SecFilings { get; } = secFilings;

    /// <summary>What something is trading at now — current prices, extended-hours prices, and trailing price
    /// changes.
    ///
    /// <para>Sixteen of FMP's endpoints returning five row shapes. One <see cref="QuoteEndpoints.GetQuoteAsync"/>
    /// covers equities, ETFs, indices, commodities, forex and crypto alike, which is why there are no per-asset-class
    /// facades here.</para></summary>
    public QuoteEndpoints Quote { get; } = quote;

    /// <summary>Price history for one symbol — daily bars in four adjustments, and intraday bars at six sizes.
    ///
    /// <para>The counterpart to <see cref="Quote"/>: that answers "now", this answers "before". Everything in it
    /// truncates silently, in two different ways — see <see cref="ChartEndpoints"/>.</para></summary>
    public ChartEndpoints Chart { get; } = chart;

    /// <summary>Whole-universe CSV downloads. Streamed, and throttled separately — see
    /// <see cref="BulkEndpoints"/>.</summary>
    public BulkEndpoints Bulk { get; } = bulk;
}
