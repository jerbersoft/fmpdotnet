using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Quote</c> group — current prices, extended-hours prices, and trailing price changes.
///
/// <para>Sixteen documented paths returning only <b>five</b> distinct row shapes, measured 2026-08-27. Nine of the
/// sixteen return the identical four fields of <see cref="ShortQuote"/>; three return
/// <see cref="Quote"/>; the rest are the two aftermarket shapes and <see cref="PriceChange"/>.</para>
///
/// <para><b>Eight of these endpoints answer two different shapes depending on a query parameter.</b>
/// <c>batch-exchange-quote</c> and the six asset-class batches return <see cref="ShortQuote"/> rows by default and
/// <see cref="Quote"/> rows when called with <c>short=false</c>. Since C# cannot return two types from one method,
/// each is exposed as a pair — <c>Get…QuotesAsync</c> for the cheap shape and <c>Get…QuotesFullAsync</c> for the
/// wide one. Measured 2026-08-27, that difference is 1,345,381 bytes against 6,629,855 for the same 14,537 ETF
/// rows, so which one you call is worth being explicit about.</para>
///
/// <para><b>One shape covers every asset class.</b> <c>stable/quote</c> was measured answering the same seventeen
/// fields for <c>AAPL</c>, <c>BTCUSD</c>, <c>EURUSD</c>, <c>^GSPC</c> and <c>GCUSD</c> — which is why FMP's
/// Indexes, Commodity, Forex and Crypto sections re-document this endpoint rather than adding new ones, and why
/// there is one method here rather than five facades over it.</para>
///
/// <para>Everything here is on the ordinary throttle. The whole-universe batches are large but they are not
/// <c>*-bulk</c> paths, so they do not draw on the bulk reservoir.</para></summary>
public sealed class QuoteEndpoints(FmpTransport transport)
{
    // ---- one symbol -----------------------------------------------------------------------------------------

    /// <summary>The full quote for one symbol, or null when FMP knows no such symbol.
    ///
    /// <para>As with <see cref="CompanyEndpoints.GetProfileAsync"/>, <c>stable/quote</c> answers a single-element
    /// array rather than an object, and an unknown symbol answers an empty array with HTTP 200 rather than a 404 —
    /// so "not found" is a shape, not a status code.</para>
    ///
    /// <para><b>Strictly one symbol.</b> Measured 2026-08-27, <c>symbol=AAPL,MSFT</c> answers an empty array
    /// rather than two rows or an error. Use <see cref="GetQuotesAsync"/> for several.</para>
    ///
    /// <para>Works for equities, ETFs, indices, commodities, forex pairs and crypto alike — see the note on the
    /// class.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The quote, or null when FMP returned no rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
        => SingleAsync("stable/quote", symbol, FmpJsonContext.Default.ListQuote, ct);

    /// <summary>The four-field quote for one symbol — <c>stable/quote-short</c> — or null when FMP knows no such
    /// symbol.
    ///
    /// <para>Symbol, price, change and volume. For a single symbol the saving over <see cref="GetQuoteAsync"/> is
    /// a few hundred bytes and rarely worth the loss of <see cref="Quote.Name"/> and the rest; the short shape
    /// earns its place on the whole-universe batches, not here.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The quote, or null when FMP returned no rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<ShortQuote?> GetShortQuoteAsync(string symbol, CancellationToken ct = default)
        => SingleAsync("stable/quote-short", symbol, FmpJsonContext.Default.ListShortQuote, ct);

    /// <summary>The last extended-hours trade for one symbol — <c>stable/aftermarket-trade</c> — or null when FMP
    /// knows no such symbol.
    ///
    /// <para>Covers <b>both</b> extended sessions despite the name: the capture behind
    /// <see cref="AftermarketTrade"/> is a pre-market print. A single last trade, with no history behind
    /// it.</para>
    ///
    /// <para><see cref="AftermarketTrade.Timestamp"/> is epoch <b>milliseconds</b> here, where
    /// <see cref="Quote.Timestamp"/> is epoch seconds.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The trade, or null when FMP returned no rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<AftermarketTrade?> GetAftermarketTradeAsync(string symbol, CancellationToken ct = default)
        => SingleAsync("stable/aftermarket-trade", symbol, FmpJsonContext.Default.ListAftermarketTrade, ct);

    /// <summary>The current extended-hours bid and ask for one symbol — <c>stable/aftermarket-quote</c> — or null
    /// when FMP knows no such symbol.
    ///
    /// <para>The complement to <see cref="GetAftermarketTradeAsync"/>: that says what last printed, this says
    /// where the book stands. <b>They are stamped independently</b>, by a gap that was measured at 25 seconds
    /// and later at 8 on the same symbol — so pairing them gives two nearby observations rather than one
    /// snapshot, and the lag is not a constant to correct for. See <see cref="AftermarketQuote"/>.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The quote, or null when FMP returned no rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<AftermarketQuote?> GetAftermarketQuoteAsync(string symbol, CancellationToken ct = default)
        => SingleAsync("stable/aftermarket-quote", symbol, FmpJsonContext.Default.ListAftermarketQuote, ct);

    /// <summary>One symbol's price change over eleven trailing windows — <c>stable/stock-price-change</c> — or
    /// null when FMP knows no such symbol.
    ///
    /// <para>Percentages on 0–100, from one day to since-inception, with no base price carried. See
    /// <see cref="PriceChange"/>, whose property names differ from the wire's because the wire's are not legal C#
    /// identifiers.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The changes, or null when FMP returned no rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<PriceChange?> GetPriceChangeAsync(string symbol, CancellationToken ct = default)
        => SingleAsync("stable/stock-price-change", symbol, FmpJsonContext.Default.ListPriceChange, ct);

    // ---- several symbols ------------------------------------------------------------------------------------

    /// <summary>Full quotes for several symbols in one call — <c>stable/batch-quote</c>.
    ///
    /// <para><b>Unknown symbols are dropped silently.</b> Measured 2026-08-27, <c>AAPL,NOSUCHTICKER</c> answered
    /// one row. Nothing marks the absence, so a caller who needs to know which symbols failed has to compare the
    /// returned symbols against the ones asked for. That comparison is deliberately left to the caller: what a
    /// missing symbol <i>means</i> — delisted, misspelled, or outside the plan — is not something the SDK can
    /// decide.</para>
    ///
    /// <para><b>Duplicates are echoed back rather than collapsed.</b> A list of 120 symbols containing repeats
    /// answered 120 rows. No cap was reached at 120; FMP documents none, and none was searched for, since a caller
    /// asking for thousands should be using the whole-universe batches instead.</para></summary>
    /// <param name="symbols">The symbols. Joined with commas as FMP expects.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per symbol FMP recognised, in FMP's order. Empty when it recognised none. Never
    /// null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is null, empty, or contains no non-blank
    /// symbol.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetQuotesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
        => transport.GetListAsync(
            Batch("stable/batch-quote", symbols), FmpJsonContext.Default.ListQuote, ct);

    /// <summary>Four-field quotes for several symbols in one call — <c>stable/batch-quote-short</c>.
    ///
    /// <para>The same symbol handling as <see cref="GetQuotesAsync"/> — unknown symbols dropped silently,
    /// duplicates echoed.</para></summary>
    /// <param name="symbols">The symbols. Joined with commas as FMP expects.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per symbol FMP recognised. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is null, empty, or contains no non-blank
    /// symbol.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetShortQuotesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
        => transport.GetListAsync(
            Batch("stable/batch-quote-short", symbols), FmpJsonContext.Default.ListShortQuote, ct);

    /// <summary>Last extended-hours trades for several symbols — <c>stable/batch-aftermarket-trade</c>.</summary>
    /// <param name="symbols">The symbols. Joined with commas as FMP expects.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per symbol FMP recognised. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is null, empty, or contains no non-blank
    /// symbol.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AftermarketTrade>> GetAftermarketTradesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
        => transport.GetListAsync(
            Batch("stable/batch-aftermarket-trade", symbols),
            FmpJsonContext.Default.ListAftermarketTrade, ct);

    /// <summary>Extended-hours bids and asks for several symbols — <c>stable/batch-aftermarket-quote</c>.</summary>
    /// <param name="symbols">The symbols. Joined with commas as FMP expects.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per symbol FMP recognised. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is null, empty, or contains no non-blank
    /// symbol.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AftermarketQuote>> GetAftermarketQuotesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
        => transport.GetListAsync(
            Batch("stable/batch-aftermarket-quote", symbols),
            FmpJsonContext.Default.ListAftermarketQuote, ct);

    // ---- a whole exchange -----------------------------------------------------------------------------------

    /// <summary>Four-field quotes for every symbol on one exchange — <c>stable/batch-exchange-quote</c>.
    ///
    /// <para>Measured 2026-08-27, <c>NASDAQ</c> answered 14,352 rows. There is no paging parameter: the exchange
    /// arrives whole or not at all.</para>
    ///
    /// <para><b>An unknown exchange answers an empty array, not an error</b> — so a misspelling reads as "this
    /// exchange has no listings". Omitting the exchange entirely does fail, with HTTP 400 and
    /// <c>Query Error: Invalid or missing query parameter - exchange</c>, which is why the argument is
    /// checked here rather than left to produce that.</para>
    ///
    /// <para>See <see cref="GetExchangeQuotesFullAsync"/> for the seventeen-field form and what it costs.</para></summary>
    /// <param name="exchange">The exchange code, as FMP spells it — <c>NASDAQ</c>, <c>NYSE</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quote FMP carries for the exchange. Empty for an unknown exchange. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetExchangeQuotesAsync(
        string exchange, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        return transport.GetListAsync(
            new FmpRequest("stable/batch-exchange-quote").With("exchange", exchange),
            FmpJsonContext.Default.ListShortQuote, ct);
    }

    /// <summary>Full quotes for every symbol on one exchange — <c>stable/batch-exchange-quote</c> with
    /// <c>short=false</c>.
    ///
    /// <para>The same rows as <see cref="GetExchangeQuotesAsync"/> with thirteen more fields each. Measured
    /// 2026-08-27 the two answered the same 14,352 NASDAQ symbols, so this is a wider row rather than a longer
    /// list — and roughly five times the bytes.</para></summary>
    /// <param name="exchange">The exchange code, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quote FMP carries for the exchange. Empty for an unknown exchange. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetExchangeQuotesFullAsync(
        string exchange, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        return transport.GetListAsync(
            new FmpRequest("stable/batch-exchange-quote").With("exchange", exchange).With("short", false),
            FmpJsonContext.Default.ListQuote, ct);
    }

    // ---- whole asset classes --------------------------------------------------------------------------------
    //
    // Six pairs over six paths. Each pair is the same endpoint called two ways: the cheap four-field shape by
    // default, and the seventeen-field shape with short=false. Row counts measured 2026-08-27.

    /// <summary>Four-field quotes for every ETF FMP carries — <c>stable/batch-etf-quotes</c>. Measured
    /// 2026-08-27: 14,537 rows, 1,345,381 bytes.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every ETF quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetEtfQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-etf-quotes", ct);

    /// <summary>Full quotes for every ETF FMP carries — <c>stable/batch-etf-quotes</c> with <c>short=false</c>.
    /// Measured 2026-08-27: the same 14,537 rows, <b>6,629,855 bytes</b> — 4.9 times
    /// <see cref="GetEtfQuotesAsync"/>.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every ETF quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetEtfQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-etf-quotes", ct);

    /// <summary>Four-field quotes for every mutual fund FMP carries — <c>stable/batch-mutualfund-quotes</c>.
    /// Measured 2026-08-27: 7,141 rows.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every mutual fund quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetMutualFundQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-mutualfund-quotes", ct);

    /// <summary>Full quotes for every mutual fund FMP carries — <c>stable/batch-mutualfund-quotes</c> with
    /// <c>short=false</c>.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every mutual fund quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetMutualFundQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-mutualfund-quotes", ct);

    /// <summary>Four-field quotes for every commodity FMP carries — <c>stable/batch-commodity-quotes</c>.
    /// Measured 2026-08-27: 40 rows, much the smallest of these.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every commodity quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetCommodityQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-commodity-quotes", ct);

    /// <summary>Full quotes for every commodity FMP carries — <c>stable/batch-commodity-quotes</c> with
    /// <c>short=false</c>. At 40 rows the payload difference hardly matters here.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every commodity quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetCommodityQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-commodity-quotes", ct);

    /// <summary>Four-field quotes for every cryptocurrency FMP carries — <c>stable/batch-crypto-quotes</c>.
    /// Measured 2026-08-27: 4,778 rows, 486,693 bytes.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every crypto quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetCryptoQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-crypto-quotes", ct);

    /// <summary>Full quotes for every cryptocurrency FMP carries — <c>stable/batch-crypto-quotes</c> with
    /// <c>short=false</c>. Measured 2026-08-27: the same 4,778 rows, 2,200,708 bytes — 4.5 times
    /// <see cref="GetCryptoQuotesAsync"/>.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every crypto quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetCryptoQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-crypto-quotes", ct);

    /// <summary>Four-field quotes for every forex pair FMP carries — <c>stable/batch-forex-quotes</c>. Measured
    /// 2026-08-27: 1,550 rows.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every forex quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetForexQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-forex-quotes", ct);

    /// <summary>Full quotes for every forex pair FMP carries — <c>stable/batch-forex-quotes</c> with
    /// <c>short=false</c>. Note that fields like <see cref="Quote.MarketCap"/> carry whatever FMP puts there and
    /// are not meaningful for a currency pair.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every forex quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetForexQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-forex-quotes", ct);

    /// <summary>Four-field quotes for every index FMP carries — <c>stable/batch-index-quotes</c>. Measured
    /// 2026-08-27: 425 rows.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every index quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ShortQuote>> GetIndexQuotesAsync(CancellationToken ct = default)
        => ShortUniverse("stable/batch-index-quotes", ct);

    /// <summary>Full quotes for every index FMP carries — <c>stable/batch-index-quotes</c> with
    /// <c>short=false</c>.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every index quote. Never null.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<Quote>> GetIndexQuotesFullAsync(CancellationToken ct = default)
        => FullUniverse("stable/batch-index-quotes", ct);

    // ---- shared plumbing ------------------------------------------------------------------------------------

    /// <summary>Reads a one-symbol endpoint that answers a single-element array, returning null for an empty
    /// one.</summary>
    private async Task<T?> SingleAsync<T>(
        string path, string symbol,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> typeInfo, CancellationToken ct)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest(path).With("symbol", symbol), typeInfo, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Builds a <c>symbols=</c> request, rejecting a list that would reach FMP empty.
    ///
    /// <para>Blank entries are dropped rather than sent: a trailing comma produces
    /// <c>symbols=AAPL,</c>, which FMP treats as a request for a symbol named "" and answers by silently
    /// returning one fewer row — indistinguishable from the symbol not existing. A list that is <i>entirely</i>
    /// blank throws, because that request cannot mean anything and the empty array it would answer would read as
    /// "none of these symbols are known".</para></summary>
    private static FmpRequest Batch(string path, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var joined = string.Join(',', symbols.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (joined.Length == 0)
            throw new ArgumentException("At least one non-blank symbol is required.", nameof(symbols));

        return new FmpRequest(path).With("symbols", joined);
    }

    private Task<IReadOnlyList<ShortQuote>> ShortUniverse(string path, CancellationToken ct)
        => transport.GetListAsync(new FmpRequest(path), FmpJsonContext.Default.ListShortQuote, ct);

    private Task<IReadOnlyList<Quote>> FullUniverse(string path, CancellationToken ct)
        => transport.GetListAsync(
            new FmpRequest(path).With("short", false), FmpJsonContext.Default.ListQuote, ct);
}
