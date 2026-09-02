using System.Runtime.CompilerServices;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Directory</c> group — what exists: the securities the API knows about, the reference
/// vocabularies it classifies them against, and the adjacent asset-class lists FMP files elsewhere in its own
/// documentation but which answer the same "what exists" question.
///
/// <para><b>The vocabularies.</b> <see cref="GetSectorsAsync(CancellationToken)"/> and
/// <see cref="GetIndustriesAsync(CancellationToken)"/> answer a flat list of labels. They are the authoritative
/// spelling of the <c>sector</c> and <c>industry</c> values that come back on
/// <see cref="CompanyEndpoints.GetProfileAsync(string, CancellationToken)"/> and on the screener, so a caller
/// building a lookup table or validating user input should take them from here rather than hard-coding a list that
/// silently rots when FMP adds a category. <see cref="GetCountriesAsync(CancellationToken)"/> and
/// <see cref="GetExchangesAsync(bool, CancellationToken)"/> are the same idea applied to country and exchange codes —
/// 117 countries and 63 exchanges, the whole set of each, measured 2026-08-27.</para>
///
/// <para><b>The universe.</b> <see cref="GetStockListAsync(CancellationToken)"/> and
/// <see cref="GetActivelyTradingAsync(CancellationToken)"/> answer the symbol directory itself. Measured
/// 2026-08-26, the actively-trading list is a strict subset of the stock list — 68,869 of 91,844 symbols, with
/// <b>zero</b> symbols on the trading list absent from the full list — so the difference between them, 22,975
/// symbols, is exactly the set FMP knows but does not consider actively trading.
/// <see cref="GetEtfListAsync(CancellationToken)"/> and <see cref="GetFinancialStatementSymbolsAsync"/> narrow
/// that same universe further — funds, symbols with statements — and are each a measured, zero-exception subset
/// of the stock list; see the two methods for the counts.
/// <see cref="GetTranscriptSymbolsAsync(CancellationToken)"/> answers the equivalent question for transcripts,
/// 11,178 symbols, but the sweep did not check it against the stock list the way it checked the other two, so no
/// subset claim is made for it.</para>
///
/// <para><b>The other asset classes.</b> <see cref="GetCommodityListAsync(CancellationToken)"/>,
/// <see cref="GetCryptocurrencyListAsync(CancellationToken)"/>, <see cref="GetForexListAsync(CancellationToken)"/>
/// and <see cref="GetIndexListAsync(CancellationToken)"/> answer "what exists" for commodities, crypto, forex and
/// indexes, and FMP files all four elsewhere in its own documentation — Commodity, Crypto, Forex, Indexes — not
/// under Directory. They live here rather than behind their own <c>fmp.Commodity</c>, <c>fmp.Crypto</c>,
/// <c>fmp.Forex</c> and <c>fmp.Index</c> facades because none would have anything else to hold: one
/// <see cref="QuoteEndpoints.GetQuoteAsync"/> already serves quotes for every one of these asset classes alongside
/// stocks, so there is no companion facade for a directory endpoint to join. See <see cref="CommodityInfo"/> for
/// the fuller account, which applies equally to its three siblings.</para>
///
/// <para><b>Two endpoints are not plain full downloads, and disagree on how they are not.</b>
/// <see cref="GetCikListAsync(int, int, CancellationToken)"/> honours <c>page</c> — 10,000 rows per page, 52
/// pages, about 512,665 rows in total, all measured 2026-08-27 — while
/// <see cref="GetSymbolChangesAsync(CancellationToken)"/> honours only <c>limit</c> and <b>silently ignores
/// <c>page</c></b>: <c>page=0</c> and <c>page=1</c> at the same <c>limit</c> answer identical rows. Nothing in
/// either payload says which behaviour a caller is getting; see the two methods for the measurement.</para>
///
/// <para>Every other endpoint in this group takes no argument beyond the API key and exposes no <c>limit</c> on
/// its C# signature, because FMP does not act on one: the two symbol directories were measured ignoring it
/// outright — asking for five symbols still transfers all 68,869 or 91,844 of them, 5.3 MB and 7.7 MB respectively
/// — and nine of the eleven reference and asset-class lists swept on 2026-08-27 show the same, <c>cik-list</c> and
/// <c>symbol-change</c> above being the only two that respond to it. There is no sampling call on these; the
/// alternative when a full download is too much is the screener, which does honour <c>limit</c>, or — for renames
/// and the SEC registrant index specifically — the two paging endpoints above.</para>
///
/// <para><b>Plan tier — mixed, second-hand.</b> As fmpsdk 20260824.0, the independent client this SDK is
/// cross-checked against, records it: <see cref="GetIndexListAsync"/> and the three SIC-code methods work on a free
/// key; the stock, ETF, CIK, actively-trading, financial-statement-symbol and symbol-change lists, the four
/// <c>available-*</c> lists and the two industry-classification methods need Starter or higher (402 on free);
/// <see cref="GetTranscriptSymbolsAsync"/> needs Ultimate (402 on free, Starter and Premium; working on Ultimate
/// 2026-08-24); it records nothing about the commodity, cryptocurrency and forex lists. Not verified here: every path
/// answered 200 on the Ultimate key this SDK is measured with (2026-09-02). The members off the class's main rung
/// carry their own notes. A dated observation, not a contract — catch <see cref="FmpPlanRestrictedException"/> rather
/// than gating on it.</para></summary>
public sealed class DirectoryEndpoints(FmpTransport transport)
{
    /// <summary>Every sector FMP classifies against, in the order the API returns them.
    ///
    /// <para>Measured on 2026-08-26: <c>stable/available-sectors</c> answers exactly 11 rows, each a
    /// single-property object under the key <c>sector</c>, and they happen to arrive alphabetically. The SDK does
    /// not sort them anyway — see <see cref="GetIndustriesAsync(CancellationToken)"/>, whose sibling endpoint
    /// proves the wire order is meaningful.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetSectorsAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-sectors"),
            FmpJsonContext.Default.ListSectorName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Sector);
    }

    /// <summary>Every industry FMP classifies against, in the order the API returns them.
    ///
    /// <para>Measured on 2026-08-26: <c>stable/available-industries</c> answers exactly 159 rows under the key
    /// <c>industry</c>, and they are <b>not</b> alphabetical — they are grouped by sector, running
    /// <c>Steel, Silver, Other Precious Metals, Gold, Copper…</c> through to
    /// <c>…Diversified Utilities, General Utilities</c>. That grouping is the only signal in the response that says
    /// which sector an industry belongs to, since no row carries a sector field, so the order is data and the SDK
    /// preserves it.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetIndustriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-industries"),
            FmpJsonContext.Default.ListIndustryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Industry);
    }

    /// <summary>Every country FMP classifies an exchange against, as ISO 3166-1 alpha-2 codes — 117 of them
    /// measured 2026-08-27.
    ///
    /// <para><b>Codes, not names.</b> The wire key is <c>country</c> and the values are <c>"FK"</c>, <c>"MT"</c>,
    /// <c>"SG"</c> — two characters on every measured row. A caller rendering these to a user needs a lookup;
    /// <see cref="GetExchangesAsync(bool, CancellationToken)"/> carries both spellings of the same fact and is the
    /// cheapest join for it.</para>
    ///
    /// <para>Ignores <c>limit</c>, like every list endpoint in this group except <c>cik-list</c> and
    /// <c>symbol-change</c>. Order is the wire order, unsorted — see <see cref="Labels{T}"/>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetCountriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-countries"),
            FmpJsonContext.Default.ListCountryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Country);
    }

    /// <summary>Every ETF symbol FMP carries — 14,567 measured 2026-08-27.
    ///
    /// <para><b>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>.</b> All 14,567 appeared in
    /// that endpoint's 91,845, none outside it — the same relation already measured for
    /// <see cref="GetActivelyTradingAsync(CancellationToken)"/>. So this is a filter of the universe rather than a
    /// separate one, and a caller holding the stock list already has these rows; what this endpoint adds is
    /// knowing <i>which</i> of them are funds, which no field on the stock list says.</para>
    ///
    /// <para><b>The name arrives under <c>name</c>, not <c>companyName</c></b> — the <c>actively-trading-list</c>
    /// spelling rather than the <c>stock-list</c> one, which is why this reuses that endpoint's wire shape. Both
    /// unwrap to <see cref="CompanySymbol"/>; see that type for why the SDK absorbs the inconsistency instead of
    /// publishing it.</para>
    ///
    /// <para>Ignores <c>limit</c>: asking for 5 rows still transfers all 14,567. Order is the wire order.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetEtfListAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/etf-list"),
            FmpJsonContext.Default.ListActivelyTradingRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.Name);
    }

    /// <summary>Every commodity FMP carries — 40 measured 2026-08-27, the whole set.
    ///
    /// <para>FMP documents this under Commodity rather than Directory. It lives here because it answers
    /// Directory's question, and because no <c>fmp.Commodity</c> facade exists for it to join — see
    /// <see cref="CommodityInfo"/>.</para>
    ///
    /// <para><b><see cref="CommodityInfo.Exchange"/> is null on every row</b>, and
    /// <see cref="CommodityInfo.Currency"/> distinguishes <c>USD</c> from <c>USX</c>, which is US cents. Ignores
    /// <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CommodityInfo>> GetCommodityListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/commodities-list"), FmpJsonContext.Default.ListCommodityInfo, ct);

    /// <summary>Every cryptocurrency pair FMP carries — 4,793 measured 2026-08-27.
    ///
    /// <para>Filed under Crypto in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>.</para>
    ///
    /// <para><b>The supply fields are <see cref="decimal"/> because a whole-number type refuses real rows.</b> 953
    /// circulating values are fractional and one row exceeds <see cref="long.MaxValue"/> on both fields — see
    /// <see cref="CryptocurrencyInfo.CirculatingSupply"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CryptocurrencyInfo>> GetCryptocurrencyListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/cryptocurrency-list"), FmpJsonContext.Default.ListCryptocurrencyInfo, ct);

    /// <summary>Every forex pair FMP carries — 1,551 measured 2026-08-27.
    ///
    /// <para>Filed under Forex in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ForexPair>> GetForexListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/forex-list"), FmpJsonContext.Default.ListForexPair, ct);

    /// <summary>Every market index FMP carries — 425 measured 2026-08-27.
    ///
    /// <para>Filed under Indexes in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>. The <b>constituent</b> lists — S&amp;P 500, Nasdaq, Dow Jones, current and
    /// historical — are a separate six paths and are not modelled. Ignores <c>limit</c>.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndexInfo>> GetIndexListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/index-list"), FmpJsonContext.Default.ListIndexInfo, ct);

    /// <summary>Every symbol FMP carries, listed or not — 91,844 of them measured on 2026-08-26, 7.7 MB of JSON.
    ///
    /// <para><b>The obvious name for this endpoint 404s.</b> <c>stable/company-symbol-list</c> appears in older
    /// FMP material and reads like the natural spelling; re-probed on 2026-08-26 it answers <b>HTTP 404 with the
    /// body <c>[]</c></b>. That pairing is the trap: a caller who checks only that the body parses as a JSON array
    /// sees an empty universe and concludes FMP has no symbols, rather than that the path is wrong. The working
    /// paths are this one and <c>stable/actively-trading-list</c>. The SDK reads the status first, so through
    /// <see cref="FmpTransport"/> the 404 surfaces as an <see cref="FmpApiException"/> naming the status — but a
    /// caller reaching FMP directly, or re-deriving the path from documentation, will meet the empty array.</para>
    ///
    /// <para><b>This is a superset of <see cref="GetActivelyTradingAsync(CancellationToken)"/>, not a different
    /// list.</b> Every one of that endpoint's 68,869 symbols appeared here; the 22,975 extra rows are the
    /// non-trading remainder. A caller who wants "everything FMP knows" wants this; a caller who wants "what is
    /// currently trading" wants the other, and must not filter this one on a guess about what counts.</para>
    ///
    /// <para><b>The list moves under you.</b> Two calls eight minutes apart on 2026-08-26 answered 91,844 and
    /// 91,845 rows. A diff between two downloads is measuring FMP's churn as much as anything else, which matters
    /// for a caller using set difference as a delisting signal: a single-call disappearance is not evidence.</para>
    ///
    /// <para>Order is the wire order, unsorted — see <see cref="Symbols{T}"/>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetStockListAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/stock-list"),
            FmpJsonContext.Default.ListStockListRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.CompanyName);
    }

    /// <summary>The symbols FMP considers actively trading — 68,869 of them measured on 2026-08-26, 5.3 MB of
    /// JSON.
    ///
    /// <para>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>: every symbol here appeared
    /// there, and the company names agreed character for character on all 68,869. The wire spells the name
    /// <c>name</c> here and <c>companyName</c> there; the SDK unwraps both into
    /// <see cref="CompanySymbol"/>.</para>
    ///
    /// <para><b>Absence is a weak signal on its own.</b> Using "symbol dropped off this list" as a delisting alarm
    /// is a reasonable design — it is why the endpoint is here — but the list churns between calls (see
    /// <see cref="GetStockListAsync(CancellationToken)"/>), so a single absence is noise. Confirm across several
    /// days, and note that <see cref="CompanyEndpoints.GetDelistedAsync(int, int, CancellationToken)"/> is the
    /// endpoint that carries an actual delisting <i>date</i> — this one carries only presence.</para>
    ///
    /// <para>Order is the wire order, unsorted, and it is <b>not</b> the same order as the stock list. The two
    /// broadly track each other — 99.9% of adjacent pairs here also move forward there — but they do diverge, so
    /// neither order may be assumed to be a filter of the other. Neither is alphabetical.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetActivelyTradingAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/actively-trading-list"),
            FmpJsonContext.Default.ListActivelyTradingRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.Name);
    }

    /// <summary>Every exchange FMP carries, with its country, symbol suffix and quote delay — 63 measured
    /// 2026-08-27, the whole set.
    ///
    /// <para><b>This is the vocabulary to validate an exchange code against.</b>
    /// <see cref="QuoteEndpoints.GetExchangeQuotesAsync"/> answers an unrecognised exchange with an empty array
    /// and HTTP 200, not an error, so a typo there is indistinguishable from an exchange that went dark.</para>
    ///
    /// <para><see cref="ExchangeInfo.Delay"/> is prose, not a duration, and
    /// <see cref="ExchangeInfo.SymbolSuffix"/> is the literal <c>"N/A"</c> on five rows — see those properties.
    /// Ignores <c>limit</c>.</para>
    ///
    /// <para><b>The 63 is the default set, not the whole one (#51).</b> Measured 2026-09-02,
    /// <c>extended=true</c> answers <b>71</b>: the same 63 rows byte for byte, plus AQS, BUD, BVC, EGX, HOSE,
    /// KUW, RIS and TAL, with the same six fields — it widens the universe, not the row. <c>extended=false</c>
    /// is byte-identical to omitting it, so the default here sends nothing. All 71 codes appear in
    /// <see cref="MarketHoursEndpoints.GetAllExchangesAsync"/>'s 81; the ten that path has to itself are ASE,
    /// BTS, CME, EBS, EURONEXT, FGI, ICEF, NIM, PNK and SSX.</para></summary>
    /// <param name="extended">Ask for the eight exchanges FMP leaves out by default. Sent only when
    /// <see langword="true"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeInfo>> GetExchangesAsync(bool extended = false, CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/available-exchanges").With("extended", extended ? true : (bool?)null),
            FmpJsonContext.Default.ListExchangeInfo, ct);

    /// <summary>Every symbol FMP holds financial statements for — 68,200 measured 2026-08-27, 5.6 MB of JSON.
    ///
    /// <para><b>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>.</b> None of the 68,200 fell
    /// outside that endpoint's 91,845, so the 23,645-symbol difference is exactly the set FMP lists but has no
    /// fundamentals for — the question to ask before calling
    /// <see cref="StatementEndpoints.GetIncomeStatementAsync"/> across a universe and reading empty results as
    /// "no data this period".</para>
    ///
    /// <para>Carries the reporting currency as well as the trading one, and they differ — see
    /// <see cref="FinancialStatementSymbol.ReportingCurrency"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FinancialStatementSymbol>> GetFinancialStatementSymbolsAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/financial-statement-symbol-list"),
            FmpJsonContext.Default.ListFinancialStatementSymbol, ct);

    /// <summary>Every symbol FMP holds an earnings-call transcript for, with the count — 11,178 measured
    /// 2026-08-27.
    ///
    /// <para>A directory rather than content: it says which symbols have transcripts and how many, not what any
    /// of them says.</para>
    ///
    /// <para>The transcripts themselves are on <see cref="TranscriptsEndpoints"/>: this answers which symbols
    /// have any, <see cref="TranscriptsEndpoints.GetDatesAsync"/> answers which quarters one symbol has, and
    /// <see cref="TranscriptsEndpoints.GetTranscriptAsync"/> answers a call in full.</para>
    ///
    /// <para>The count arrives as a quoted string on every row; see
    /// <see cref="TranscriptSymbol.TranscriptCount"/>. Ignores <c>limit</c>.</para>
    ///
    /// <para><b>Plan tier — Ultimate, second-hand.</b> Recorded 402 on free, Starter and Premium and working on
    /// Ultimate (2026-08-24) by fmpsdk 20260824.0; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<TranscriptSymbol>> GetTranscriptSymbolsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/earnings-transcript-list"), FmpJsonContext.Default.ListTranscriptSymbol, ct);

    /// <summary>The <c>limit</c> the SDK sends to <c>stable/symbol-change</c>, and the reason it sends one at all.
    ///
    /// <para><b>Without it the endpoint answers 100 rows and holds 5,456</b>, measured 2026-08-27 — 1.8% of the
    /// history, returned as a well-formed HTTP 200 array indistinguishable from a complete one. FMP documents no
    /// parameters for this path whatsoever; <c>limit</c> works regardless, and <c>page</c> is accepted and
    /// silently ignored, so this is the only lever there is.</para>
    ///
    /// <para>10,000 rather than 5,456 is headroom against growth, not a guess: the ceiling was probed to
    /// <c>limit=100000</c> and the answer stayed 5,456, so there is no server-side cap between the two and asking
    /// for more costs nothing.</para></summary>
    public const int SymbolChangeRequestLimit = 10_000;

    /// <summary>Every ticker rename FMP has recorded — 5,456 measured 2026-08-27.
    ///
    /// <para>This is what explains a symbol disappearing from
    /// <see cref="GetActivelyTradingAsync(CancellationToken)"/> without appearing in
    /// <see cref="CompanyEndpoints.GetDelistedAsync"/>: it was renamed, not delisted. A caller reconciling
    /// historical positions against current tickers wants all of it.</para>
    ///
    /// <para><b>Takes no paging arguments, deliberately.</b> The endpoint's undocumented default returns 100 rows
    /// of 5,456 and its <c>page</c> parameter does nothing — see
    /// <see cref="SymbolChangeRequestLimit"/>. Offering a <c>page</c> the SDK knows is ignored would be worse than
    /// offering nothing, and there is no correct partial answer to "what has been renamed": a reconciliation
    /// against 1.8% of the history is silently wrong rather than incomplete.</para>
    ///
    /// <para><b>The order is FMP's own, and unmeasured.</b> Unlike
    /// <see cref="GetCikListAsync(int, int, CancellationToken)"/>, whose "CIK descending" is read off two measured
    /// page heads, nothing in the 2026-08-27 sweep pins this endpoint's row order — only its count, its four field
    /// names, and that <c>date</c> is ISO on all 5,456 rows. The SDK preserves whatever order the wire
    /// sends.</para>
    ///
    /// <para><b>The whole set includes the row FMP itself flags as invalid (#51).</b> Measured 2026-09-02:
    /// 5,472 rows here, 5,471 with <c>invalid=false</c>, and <b>1</b> with <c>invalid=true</c> — the same
    /// <c>A → AWD</c> row of 2005-11-23, which is also inside these 5,472. FMP documents the parameter and not
    /// its meaning, so this method keeps sending nothing and answering everything; the flagged rows are
    /// <see cref="GetInvalidSymbolChangesAsync"/>, and a caller who wants them out subtracts that
    /// answer.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded rename in FMP's own order, which this SDK does not re-sort. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolChange>> GetSymbolChangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/symbol-change").With("limit", SymbolChangeRequestLimit),
            FmpJsonContext.Default.ListSymbolChange, ct);

    /// <summary>The renames FMP has flagged as invalid — <c>stable/symbol-change?invalid=true</c>.
    ///
    /// <para><b>One row, measured 2026-09-02 (#51):</b> <c>{"date":"2005-11-23","companyName":"Agilent
    /// Technologies Inc.","oldSymbol":"A","newSymbol":"AWD"}</c>. It is not a second dataset: the same row
    /// sits inside <see cref="GetSymbolChangesAsync"/>'s 5,472, and <c>invalid=false</c> answers the 5,471
    /// without it. FMP publishes the parameter with no account of what earns the flag, so this SDK names the
    /// method after the wire and claims nothing more.</para>
    ///
    /// <para>Sends the same <see cref="SymbolChangeRequestLimit"/> as the unflagged call. The path's default
    /// without a limit is 100 rows, and a set that is one row today is not promised to stay one. The value is
    /// case-insensitive upstream — <c>TRUE</c> answered the same row — while <c>1</c> and <c>yes</c> are
    /// ignored and answer the unflagged default; the SDK sends <c>true</c>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The flagged renames, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SymbolChange>> GetInvalidSymbolChangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/symbol-change").With("invalid", true).With("limit", SymbolChangeRequestLimit),
            FmpJsonContext.Default.ListSymbolChange, ct);

    /// <summary>The largest page <c>stable/cik-list</c> will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>: on 2026-08-27 <c>limit=10000</c>, <c>limit=50000</c> and
    /// <c>limit=200000</c> all answered exactly 10,000 rows. A caller who asks for 50,000 and advances the page
    /// index by 50,000 skips four fifths of the registry and never sees an error, so
    /// <see cref="GetCikListAsync(int, int, CancellationToken)"/> rejects a larger <c>limit</c> rather than
    /// passing it on to be clamped — the same treatment
    /// <see cref="CompanyEndpoints.MaxDelistedPageSize"/> gives the delisted archive.</para></summary>
    public const int MaxCikListPageSize = 10_000;

    /// <summary>One page of <c>stable/cik-list</c> — the SEC registrant index, about 512,665 entries measured
    /// 2026-08-27 across 52 pages.
    ///
    /// <para><b>Not a symbol directory.</b> Most registrants have no ticker, and some are people — see
    /// <see cref="CikEntry"/>. Ordered by CIK descending, so page 0 is the most recently assigned.</para>
    ///
    /// <para><b><c>page</c> works here, unlike on <see cref="GetSymbolChangesAsync(CancellationToken)"/>.</b> The
    /// two endpoints sit in the same group and disagree: page 0 and page 1 of this one start at
    /// <c>0002150676</c> and <c>0002150170</c> respectively, while <c>symbol-change</c> answers both with
    /// identical rows. Nothing in either payload says which behaviour you are getting.</para>
    ///
    /// <para>The walk ends short rather than empty: page 51 carried 2,665 rows and page 52 answered <c>[]</c>.
    /// Either terminator works; <see cref="StreamCikListAsync(CancellationToken)"/> stops at the first short page
    /// and saves a request.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCikListPageSize"/>. Required rather than defaulted,
    /// matching <see cref="CompanyEndpoints.GetDelistedAsync"/>: the page size and the page index have to agree
    /// for a walk to be complete, and a default would let them disagree invisibly.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows in FMP's order. Empty past the end. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCikListPageSize"/> — see that constant for why the
    /// upper bound is enforced here rather than silently clamped upstream.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CikEntry>> GetCikListAsync(int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxCikListPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/cik-list").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCikEntry, ct);
    }

    /// <summary>Walks <c>stable/cik-list</c> from page 0 and streams every registrant as one sequence — about
    /// 512,665 rows over 52 requests, measured 2026-08-27.
    ///
    /// <para><b>The termination rule is sound here, unlike the bulk walks.</b> This endpoint answers a page past
    /// the end with an empty HTTP 200 array rather than an error, so the walk needs no heuristic about what a
    /// status code means: a page that comes back shorter than
    /// <see cref="MaxCikListPageSize"/> is the last one, and an empty page ends it too. Compare
    /// <see cref="BulkEndpoints.StreamAllProfilesAsync"/>, which has to read an HTTP 400 as "past the end"
    /// because that family offers nothing better.</para>
    ///
    /// <para><b>52 requests on the ordinary throttle.</b> Not free, and the whole registry is rarely what a caller
    /// wants — <see cref="GetCikListAsync(int, int, CancellationToken)"/> is there for taking one page.</para></summary>
    /// <param name="ct">Cancels the walk between pages as well as mid-page.</param>
    /// <exception cref="FmpRateLimitedException">FMP answered 429. Possible if 52 pages are walked flat out.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async IAsyncEnumerable<CikEntry> StreamCikListAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var page = 0; ; page++)
        {
            var rows = await GetCikListAsync(page, MaxCikListPageSize, ct).ConfigureAwait(false);
            foreach (var row in rows) yield return row;

            // A short page is the last page. An empty one ends it too, and is the same condition — nothing
            // measured returned a short page followed by a full one.
            if (rows.Count < MaxCikListPageSize) yield break;
        }
    }

    /// <summary>The largest page <c>stable/all-industry-classification</c> will serve, measured rather than
    /// documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="CompanyEndpoints.MaxDelistedPageSize"/>: measured 2026-08-28, <c>limit=1000</c>,
    /// <c>limit=5000</c>, <c>limit=26000</c> and <c>limit=30000</c> all answered exactly 1,000 rows with HTTP 200
    /// and nothing in the body to say the request had been trimmed.
    /// <see cref="GetIndustryClassificationsAsync(int, CancellationToken)"/> therefore rejects a larger
    /// <c>limit</c> rather than passing it on to be clamped — a caller who asks for 5,000 and gets 1,000 has no
    /// way to tell which happened.</para></summary>
    public const int MaxIndustryClassificationPageSize = 1000;

    /// <summary>One capped page of <c>stable/all-industry-classification</c> — every SEC registrant FMP knows,
    /// with its SIC code and business address.
    ///
    /// <para><b>There is no <c>page</c> parameter, and that is not an oversight.</b> Measured 2026-08-28: page 0
    /// honours <c>limit</c> and caps at 1,000 rows, while <b>every non-zero page answers the entire 25,952-row
    /// universe</b> — byte-identical across page numbers, ignoring <c>limit</c> entirely. There is no page index
    /// that advances through the data, so exposing one would be exposing a control that does not control
    /// anything. The two behaviours FMP actually has are modelled as two methods: this one, and
    /// <see cref="GetAllIndustryClassificationsAsync(CancellationToken)"/>.</para>
    ///
    /// <para>This method reaches the first 1,000 rows and no further. If you need the rest, you need the other
    /// one — there is no walk that gets there from here.</para></summary>
    /// <param name="limit">Rows to ask for, 1 to <see cref="MaxIndustryClassificationPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Up to <paramref name="limit"/> rows in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxIndustryClassificationPageSize"/> — see that constant for why the upper bound is enforced
    /// here rather than silently clamped upstream.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndustryClassification>> GetIndustryClassificationsAsync(
        int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxIndustryClassificationPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/all-industry-classification").With("page", 0).With("limit", limit),
            FmpJsonContext.Default.ListIndustryClassification, ct);
    }

    /// <summary>Every row of <c>stable/all-industry-classification</c> in one response — 25,952 registrants,
    /// about 7.3 MB, measured 2026-08-28.
    ///
    /// <para><b>This method depends on a bug, deliberately, and this paragraph is the whole justification.</b>
    /// The endpoint's <c>page</c> parameter does not paginate: page 0 caps at 1,000 rows however large a
    /// <c>limit</c> is sent, and any non-zero page returns the complete dataset ignoring <c>limit</c>.
    /// <c>page=1</c>, <c>page=2</c>, <c>page=1&amp;limit=10</c> and <c>page=1</c> with no limit each answered the
    /// same 25,952 rows and the same 7,288,535 bytes. Since the data is 25,952 rows and the only paged route
    /// stops at 1,000, the anomaly is the <b>only</b> way to reach rows 1,001 onward. The choice is between
    /// depending on it and leaving 96% of the dataset unreachable.</para>
    ///
    /// <para><b>If FMP fixes it, this method silently returns 5 rows instead of 25,952</b> — the row shape would
    /// not change, so nothing about the response would look wrong. The smoke suite carries a row-count assertion
    /// for exactly that, because it is the only thing that can catch it.</para>
    ///
    /// <para>One request, but a large one. <see cref="GetIndustryClassificationsAsync(int, CancellationToken)"/>
    /// is there for a caller who only wants a taste of the shape.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every classified registrant FMP knows, in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> GetAllIndustryClassificationsAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/all-industry-classification").With("page", 1),
            FmpJsonContext.Default.ListIndustryClassification, ct);

    /// <summary>The SIC vocabulary — <c>stable/standard-industrial-classification-list</c>, a fixed 444 rows
    /// measured 2026-08-28.
    ///
    /// <para><b>No paging, because the endpoint has none that works.</b> It answered all 444 rows for every
    /// combination of <c>page</c> and <c>limit</c> tried. A signature that accepted either would let a caller
    /// believe they had asked for five rows while holding 444.</para>
    ///
    /// <para><b>An earlier note here said "no parameters" outright, and that was wrong (#51).</b> Measured
    /// 2026-09-01 and again 2026-09-02, <c>sicCode</c> and <c>industryTitle</c> are both honoured — 444 → 1
    /// each — and they are <see cref="GetSicCodeAsync"/> and <see cref="SearchSicCodesAsync"/>. This method
    /// still sends nothing and answers the vocabulary.</para>
    ///
    /// <para>This is the authoritative spelling of the <c>sicCode</c> and <c>industryTitle</c> values that come
    /// back on <see cref="IndustryClassification"/> — with one catch that will silently break a join. See
    /// <see cref="SicCodeEntry.SicCode"/>: this endpoint strips a leading zero and the classification endpoints
    /// do not.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>All 444 SIC codes with their review offices. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SicCodeEntry>> GetSicCodesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/standard-industrial-classification-list"),
            FmpJsonContext.Default.ListSicCodeEntry, ct);

    /// <summary>One SIC code's entry — <c>stable/standard-industrial-classification-list?sicCode=</c>.
    ///
    /// <para><b>Exact match, and it absorbs the leading-zero hazard on the way in.</b> Measured 2026-09-02
    /// (#51): <c>sicCode=100</c> and <c>sicCode=0100</c> both answer the single <c>"100"</c> row, so the
    /// four-wide code <see cref="IndustryClassification.SicCode"/> carries can be passed straight through —
    /// the value goes out exactly as given. It is not a prefix search: <c>1</c> and <c>10</c> answer nothing.
    /// The row that comes back still spells the code without its zero; see
    /// <see cref="SicCodeEntry.SicCode"/>.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <param name="sicCode">The code, padded or not. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The entry, or <see langword="null"/> — <c>sicCode=9999999</c> answers <c>[]</c> at HTTP 200,
    /// measured 2026-09-02.</returns>
    /// <exception cref="ArgumentException"><paramref name="sicCode"/> is null, empty or whitespace: a blank
    /// filter goes out as the unfiltered call and answers all 444 rows.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<SicCodeEntry?> GetSicCodeAsync(string sicCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sicCode);
        return await transport.GetSingleAsync(
            new FmpRequest("stable/standard-industrial-classification-list").With("sicCode", sicCode),
            FmpJsonContext.Default.ListSicCodeEntry, ct).ConfigureAwait(false);
    }

    /// <summary>SIC entries whose title contains a fragment —
    /// <c>stable/standard-industrial-classification-list?industryTitle=</c>.
    ///
    /// <para><b>A case-insensitive substring match, measured 2026-09-02 (#51):</b> <c>CROPS</c> answers 1
    /// row, <c>agricultural production-crops</c> answers the same 1, and <c>AGRICULTURAL</c> answers 4 —
    /// codes 100, 200, 700 and 2870. Sent with <c>sicCode</c> as well the two AND together, which is why
    /// they are two methods rather than one with two optional strings: a method that took both would mostly
    /// answer nothing.</para>
    ///
    /// <para>The fragment goes out as given. The rule is upstream's and measured, not promised.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <param name="industryTitle">The fragment to match. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching entries in FMP's own order, or an empty list. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="industryTitle"/> is null, empty or whitespace: a
    /// blank filter goes out as the unfiltered call and answers all 444 rows.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SicCodeEntry>> SearchSicCodesAsync(string industryTitle, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(industryTitle);
        return transport.GetListAsync(
            new FmpRequest("stable/standard-industrial-classification-list").With("industryTitle", industryTitle),
            FmpJsonContext.Default.ListSicCodeEntry, ct);
    }

    /// <summary>Unwraps the two directory row shapes into <see cref="CompanySymbol"/>. Written once so the pair
    /// cannot drift apart on the judgement calls below, the same way <see cref="Labels{T}"/> serves the
    /// vocabularies.
    ///
    /// <para><b>A blank symbol drops the row; a blank name does not.</b> The symbol is the entry — a row without
    /// one carries no information and would sit in a lookup table matching nothing. A row with a symbol and no
    /// name still tells the caller the symbol exists, and discarding it would quietly shrink a universe that
    /// callers use to decide what is listed. Both are trimmed: a padded ticker is a silent equality miss.</para>
    ///
    /// <para><b>Duplicates are kept and order is preserved</b>, for the reasons set out on
    /// <see cref="Labels{T}"/> — with one addition here. Neither directory is sorted, so preserving the wire order
    /// is not preserving a signal the way it is for industries; it is refusing to spend an O(n log n) sort on
    /// 91,844 rows to impose an order the caller may not want. <c>OrderBy</c> is one call away.</para></summary>
    private static IReadOnlyList<CompanySymbol> Symbols<T>(
        IReadOnlyList<T?> rows, Func<T, string?> symbol, Func<T, string?> name)
        where T : class
    {
        var symbols = new List<CompanySymbol>(rows.Count);
        foreach (var row in rows)
        {
            // As in Labels: a literal null element is legal JSON, and reaching through it here would turn a
            // cosmetic upstream glitch into a NullReferenceException in the caller.
            if (row is null) continue;
            var ticker = symbol(row)?.Trim();
            if (string.IsNullOrEmpty(ticker)) continue;
            var label = name(row)?.Trim();
            symbols.Add(new CompanySymbol { Symbol = ticker, Name = string.IsNullOrEmpty(label) ? null : label });
        }
        return symbols;
    }

    /// <summary>Unwraps the single-property rows into their labels. Written once so the two endpoints cannot drift
    /// apart on the three judgement calls below.
    ///
    /// <para><b>Blanks are dropped.</b> Nothing in either measured payload was null, empty or padded, but a label
    /// is a key: a caller cannot see the payload, and an empty string entering a lookup table becomes a phantom
    /// category that matches nothing and is invisible in a diff. Whitespace is trimmed for the same reason — a
    /// trailing space turns an equality test against <c>"Technology"</c> into a silent miss.</para>
    ///
    /// <para><b>Duplicates are kept.</b> Deliberate, and the opposite of the blank rule: a blank label carries no
    /// information, whereas a repeated one carries the fact that upstream now repeats it. De-duplicating would
    /// change the cardinality of a directory response without saying so, hiding an upstream change behind an SDK
    /// that looks correct. Which duplicates mean — a data fault to report, or two spellings to merge — is the
    /// caller's policy, and <c>Distinct()</c> is one call away for callers who want it.</para>
    ///
    /// <para><b>Order is preserved.</b> See <see cref="GetIndustriesAsync(CancellationToken)"/>.</para></summary>
    private static IReadOnlyList<string> Labels<T>(IReadOnlyList<T?> rows, Func<T, string?> label)
        where T : class
    {
        var names = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            // A literal null element is possible in JSON even though neither capture contained one, and reaching
            // through it here would turn a cosmetic upstream glitch into a NullReferenceException in the caller.
            if (row is null) continue;
            var name = label(row)?.Trim();
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        }
        return names;
    }
}
