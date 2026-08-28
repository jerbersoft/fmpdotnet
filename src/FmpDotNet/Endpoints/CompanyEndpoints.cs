using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Company</c> group — profiles and the identifiers hanging off them.</summary>
public sealed class CompanyEndpoints(FmpTransport transport)
{
    /// <summary>Company profile for one symbol, or null when FMP knows no such symbol.
    ///
    /// <para><c>stable/profile</c> answers a single-element array rather than an object, and an unknown symbol
    /// answers an empty array rather than a 404 — so "not found" is a shape, not a status code.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<CompanyProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/profile").With("symbol", symbol),
            FmpJsonContext.Default.ListCompanyProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Company profile for one SEC Central Index Key — <c>stable/profile-cik</c>. Returns
    /// <see langword="null"/> when FMP knows no such filer.
    ///
    /// <para>The same 36 fields as <see cref="GetProfileAsync"/>, in the same order, in a single-element array —
    /// compared field by field on 2026-08-27, which is why this shares <see cref="CompanyProfile"/> rather than
    /// declaring a type of its own. Use it when what you hold is a CIK from an EDGAR filing rather than a
    /// ticker.</para>
    ///
    /// <para><b>The CIK is sent exactly as given, because FMP accepts either spelling.</b> The zero-padded
    /// <c>0000320193</c> and the bare <c>320193</c> both answered the same AAPL row on 2026-08-27, so there is
    /// nothing to normalise and normalising would silently rewrite the caller's identifier. An unknown but
    /// numeric CIK — <c>9999999999</c> — answers <c>[]</c> with HTTP 200; a non-numeric one answers
    /// <b>400</b>.</para></summary>
    /// <param name="cik">The Central Index Key, padded or not.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The profile, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<CompanyProfile?> GetProfileByCikAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/profile-cik").With("cik", cik),
            FmpJsonContext.Default.ListCompanyProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Public float and shares outstanding for one symbol, or null when FMP knows no such symbol.
    ///
    /// <para>Single object rather than a list, and no <c>limit</c> parameter, because <c>stable/shares-float</c>
    /// holds no history: measured 2026-08-26, it answers exactly one row and silently ignores <c>limit</c>. As with
    /// <see cref="GetProfileAsync"/>, an unknown symbol answers an empty array with HTTP 200 rather than a 404 — and
    /// so does a class-share ticker spelled with a dot, which FMP wants hyphenated (<c>BRK-B</c>, not
    /// <c>BRK.B</c>).</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<SharesFloat?> GetSharesFloatAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/shares-float").With("symbol", symbol),
            FmpJsonContext.Default.ListSharesFloat, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>One page of <c>stable/shares-float-all</c> — the same float and share-count rows as
    /// <see cref="GetSharesFloatAsync"/>, for the whole universe, paged. Returns <see langword="null"/> when the
    /// endpoint is outside this API key's plan.
    ///
    /// <para><b>Page 0 is not a sample of the universe, and reading it as one has already cost real data.</b> The
    /// universe is ordered by symbol and it is global, so measured 2026-08-26 <c>page=0&amp;limit=5</c> answers
    /// <c>000001.SZ, 000002.SZ, 000004.SZ, 000005.SZ, 000006.SZ</c> and <c>page=1&amp;limit=5</c> continues
    /// <c>000007.SZ … 000011.SZ</c> — Shenzhen listings, disjoint and consecutive. The application this SDK replaces
    /// called this endpoint with no <c>page</c> and no <c>limit</c> at all, recorded the result as "HTTP 200 with
    /// only a partial (mostly foreign) page", concluded the endpoint was half-broken on its plan, and wrote zero
    /// shares for its entire US universe. It was not a plan quirk and the endpoint was not broken: it was reading
    /// page zero of a symbol-ordered global list without knowing pages existed. There is no call that returns
    /// everything. A caller that wants the universe walks the pages until one comes back short of
    /// <paramref name="limit"/>.</para>
    ///
    /// <para><b><see cref="SharesFloat.Source"/> is always null here</b>, and that null means "this endpoint does
    /// not carry the field", not "FMP names no filing". Measured on both captured pages, a bulk row has five
    /// fields — <c>symbol, date, freeFloat, floatShares, outstandingShares</c> — where the per-symbol endpoint has
    /// six. The model is shared deliberately, because everything the two do carry is identical in meaning and shape,
    /// but a caller that needs the EDGAR URL must re-fetch the symbol through
    /// <see cref="GetSharesFloatAsync"/>. Null <see cref="SharesFloat.Source"/> is also normal on that path (every
    /// ETF measured answered null), so the two nulls are genuinely indistinguishable on the value alone — only the
    /// endpoint you called tells them apart.</para>
    ///
    /// <para><b>A refusal throws; it does not come back as null.</b> There is no <c>Try</c>-prefixed twin, and
    /// there cannot usefully be one: C# forbids <c>out</c> parameters on async methods, so the BCL's
    /// <c>bool TryX(out T)</c> shape is unavailable here, and the nullable-return imitation this method used to
    /// have put two error channels on one signature — a caller had to read this paragraph to learn that null
    /// meant "refused" rather than "nothing there". Catch <see cref="FmpPlanRestrictedException"/> to degrade to
    /// the per-symbol loop; it says which of 402 or 403 arrived, which the null never could.</para>
    ///
    /// <para>Gating here is not settled and must not be assumed either way: this endpoint was recorded as 402 on
    /// Premium by the predecessor and answered 200 when re-probed on 2026-08-26. It is JSON rather than CSV and is
    /// not in FMP's Bulk category, so it runs on the ordinary per-minute throttle and not the far tighter bulk one —
    /// paging it is a normal cost, not a bulk download.</para></summary>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Page size. Required rather than optional on purpose — see the first paragraph. Sending
    /// neither parameter is what produced the incident above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows, or an empty list when the page is past the end of the universe. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative or
    /// <paramref name="limit"/> is not positive.</exception>
    /// <exception cref="FmpRateLimitedException">FMP answered 429. Likely if the pages are walked flat out.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SharesFloat>> GetAllSharesFloatAsync(
        int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return transport.GetListAsync(
            new FmpRequest("stable/shares-float-all").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSharesFloat, ct);
    }

    /// <summary>The largest page <c>stable/delisted-companies</c> will serve, measured rather than documented.
    ///
    /// <para>This is a <b>cap, not a page size</b>, and that distinction is the whole reason the constant exists.
    /// On 2026-08-26 <c>limit=1000</c> and <c>limit=100</c> returned byte-identical 16,982-byte bodies of 100 rows,
    /// while <c>limit=10</c> was honoured — so the parameter works downward and is silently clamped upward. A
    /// caller who asks for 1,000 and walks pages assuming they got them skips 90% of the archive and never sees an
    /// error. <see cref="GetDelistedAsync(int, int, CancellationToken)"/> therefore rejects a larger
    /// <c>limit</c> rather than passing it on to be clamped.</para></summary>
    public const int MaxDelistedPageSize = 100;

    /// <summary>One page of <c>stable/delisted-companies</c> — the archive of securities FMP no longer carries as
    /// listed, with the date each stopped.
    ///
    /// <para><b>Ordered newest delisting first, which puts scheduled future delistings on page 0.</b> Measured
    /// 2026-08-26, the first row was <c>NB2.F</c> dated <c>2026-12-30</c> — four months ahead of the call. Reading
    /// this endpoint as "securities that have stopped trading" therefore marks live securities as gone. Compare
    /// <see cref="DelistedCompany.DelistedDate"/> against today.</para>
    ///
    /// <para><b>The walk is finite and ends short, not empty.</b> The archive held 9,782 rows on 2026-08-26: pages
    /// 0 to 96 full at 100 rows, page 97 carrying 82, page 98 and everything past it answering <c>[]</c> with HTTP
    /// 200 — including <c>page=100000</c>, which is not an error either. Either terminator works, but stopping at
    /// the first short page saves a request. History runs back to <c>2002-01-31</c>.</para>
    ///
    /// <para>This is the endpoint that carries a delisting <b>date</b>.
    /// <see cref="DirectoryEndpoints.GetActivelyTradingAsync(CancellationToken)"/> carries only presence, so
    /// absence from that list says something changed while this says when.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxDelistedPageSize"/>. Required rather than defaulted,
    /// matching <see cref="GetAllSharesFloatAsync(int, int, CancellationToken)"/>: the page size and the page index
    /// have to agree for a walk to be complete, and a default would let them disagree invisibly.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows, in FMP's order. Empty past the end of the archive, never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxDelistedPageSize"/> — see that constant for why the
    /// upper bound is enforced here instead of being silently clamped upstream.</exception>
    /// <exception cref="FmpRateLimitedException">FMP answered 429. Likely if 98 pages are walked flat out.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<DelistedCompany>> GetDelistedAsync(
        int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxDelistedPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/delisted-companies").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListDelistedCompany, ct);
    }

    /// <summary>Latest market capitalisation for one symbol — <c>stable/market-capitalization</c>. Returns
    /// <see langword="null"/> when FMP knows no such symbol.
    ///
    /// <para>A single-element array rather than an object, and an unknown symbol answers <c>[]</c> with HTTP 200
    /// rather than a 404 — measured on <c>ZZZZNOPE</c>, 2026-08-27. "Not found" is a shape here, exactly as it is
    /// for <see cref="GetProfileAsync"/>.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The row, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<MarketCapitalization?> GetMarketCapAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/market-capitalization").With("symbol", symbol),
            FmpJsonContext.Default.ListMarketCapitalization, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Latest market capitalisation for several symbols in one call —
    /// <c>stable/market-capitalization-batch</c>.
    ///
    /// <para><b>The response is not positionally aligned with the request, and the endpoint gives no indication
    /// of that.</b> Measured 2026-08-27 against the first 100 plain tickers of <c>stable/stock-list</c>: 100
    /// requested, <b>99 returned</b>. The missing row is <c>WDSP</c> — a symbol FMP's own directory lists and
    /// its market-cap endpoint has nothing for. <c>AAPL,ZZZZNOPE</c> behaves the same way, answering one row.
    /// A caller that zips the request list against the response list corrupts every row after the first gap.
    /// <b>Match rows by <see cref="MarketCapitalization.Symbol"/>.</b></para>
    ///
    /// <para><b>No upper bound on the batch size was found up to 500 symbols</b>, and the endpoint neither
    /// errors nor truncates — 500 requested answered 499. That is why this method does not chunk and does not
    /// enforce a cap: a chunk size would be invented rather than measured. An empty <c>symbols</c> answers
    /// <b>400</b>, which is why the empty case is rejected here rather than sent.</para></summary>
    /// <param name="symbols">The tickers. Blank entries are dropped; the rest are joined with commas as FMP
    /// expects.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per symbol FMP had one for — possibly fewer than were asked for. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> contains no non-blank symbol.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketCapitalization>> GetMarketCapBatchAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var joined = string.Join(',', symbols.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (joined.Length == 0)
            throw new ArgumentException("At least one non-blank symbol is required.", nameof(symbols));

        return transport.GetListAsync(
            new FmpRequest("stable/market-capitalization-batch").With("symbols", joined),
            FmpJsonContext.Default.ListMarketCapitalization, ct);
    }

    /// <summary>Market capitalisation over time for one symbol —
    /// <c>stable/historical-market-capitalization</c>, newest first.
    ///
    /// <para><b>Called bare, this answers about three months rather than history.</b> Measured 2026-08-27 on
    /// <c>AAPL</c>: 65 rows spanning <c>2026-05-27 → 2026-08-27</c>. A caller expecting "historical" to mean the
    /// whole series gets a quarter of it and no error.</para>
    ///
    /// <para><b><paramref name="limit"/> cannot widen that window.</b> It clamps downward — <c>limit=5</c>
    /// answers 5 — and is ignored upward: <c>limit=5000</c> and <c>limit=100000</c> both answered the same 65
    /// rows. Only <paramref name="from"/> and <paramref name="to"/> reach further back.</para>
    ///
    /// <para><b>A range is capped at exactly 5,000 rows, and the cap keeps the newest.</b>
    /// <c>from=2000-01-01</c> and <c>from=1990-01-01</c> both answered 5,000 rows starting <c>2006-10-11</c> —
    /// the identical span, six years short of the earlier request. A caller asking for all history gets the most
    /// recent 5,000 sessions with nothing to say anything was dropped. Reaching further means walking backwards
    /// with <paramref name="to"/>; there is deliberately no helper for that here, because the walk is its own
    /// decision rather than a rider on this method.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="from">Start of the range, inclusive. Optional — see the window note above.</param>
    /// <param name="to">End of the range, inclusive. Optional.</param>
    /// <param name="limit">Row cap. Clamps downward only.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows, newest first. Empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Both ends of the range were supplied and
    /// <paramref name="to"/> is earlier than <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketCapitalization>> GetHistoricalMarketCapAsync(
        string symbol, LocalDate? from = null, LocalDate? to = null, int? limit = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-market-capitalization")
                .With("symbol", symbol).With("from", from).With("to", to).With("limit", limit),
            FmpJsonContext.Default.ListMarketCapitalization, ct);
    }

    /// <summary>Comparable companies for one symbol — <c>stable/stock-peers</c>.
    ///
    /// <para>Measured 2026-08-27: <c>AAPL</c> answered 9 rows, <c>JPM</c> and <c>SPY</c> 10 each. ETFs get peers
    /// too, so this is not equity-only. An unknown symbol answers <c>[]</c> with HTTP 200.</para>
    ///
    /// <para>FMP does not document how the peer set is chosen and the SDK does not guess — see
    /// <see cref="StockPeer"/> for the field-level surprise this endpoint carries.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The peers, in FMP's order. Empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<StockPeer>> GetPeersAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/stock-peers").With("symbol", symbol),
            FmpJsonContext.Default.ListStockPeer, ct);
    }

    /// <summary>Reported headcounts for one filer — <c>stable/employee-count</c>, newest first.
    ///
    /// <para><b>This and
    /// <see cref="GetHistoricalEmployeeCountAsync(string, int?, CancellationToken)"/> are the same dataset.</b>
    /// Measured 2026-08-27, the two paths answered byte-identical bodies on every symbol probed — <c>AAPL</c> 32
    /// rows, <c>JPM</c> 5, <c>SHOP</c> 11, <c>XOM</c> 0 on both — compared as sorted JSON. Neither is deprecated
    /// and neither is a subset of the other; FMP documents both names, so the SDK ships both and this note tells
    /// you it does not matter which you call.</para>
    ///
    /// <para><c>XOM</c>, a major filer, answers zero rows. An empty result is normal here rather than a
    /// symptom.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="limit">Row cap. Honoured downward — <c>limit=3</c> answered 3 rows on 2026-08-27. Omitted
    /// from the request when null.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows, newest first. Empty when the filer has no reported history. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EmployeeCount>> GetEmployeeCountAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
        => EmployeeCounts("stable/employee-count", symbol, limit, ct);

    /// <summary>Reported headcounts for one filer — <c>stable/historical-employee-count</c>, newest first.
    ///
    /// <para><b>The same dataset as
    /// <see cref="GetEmployeeCountAsync(string, int?, CancellationToken)"/>, measured byte-identical on four
    /// symbols on 2026-08-27.</b> The word "historical" in the path names nothing this one does that the other
    /// does not — both return the filer's whole reported history. It is shipped because FMP documents the path,
    /// so a caller who found this name in the docs finds it here.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="limit">Row cap. Honoured downward. Omitted from the request when null.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EmployeeCount>> GetHistoricalEmployeeCountAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
        => EmployeeCounts("stable/historical-employee-count", symbol, limit, ct);

    /// <summary>Named officers for one issuer — <c>stable/key-executives</c>.
    ///
    /// <para>Measured 2026-08-27 across 18 symbols: <c>AAPL</c> answered 8 rows, <c>TSM</c> 10, <c>SPY</c>
    /// <c>[]</c> — an ETF has no executives. Two of the eight fields carried nothing on every one of the 203
    /// rows; see <see cref="KeyExecutive.TitleSince"/> and <see cref="KeyExecutive.Active"/> before building
    /// on either, and <see cref="KeyExecutive.CurrencyPay"/> before comparing pay across
    /// issuers.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The officers, in FMP's order. Empty for an issuer with none. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<KeyExecutive>> GetKeyExecutivesAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/key-executives").With("symbol", symbol),
            FmpJsonContext.Default.ListKeyExecutive, ct);
    }

    /// <summary>Reported executive compensation for one filer — <c>stable/governance-executive-compensation</c>.
    ///
    /// <para><b>There is no <c>year</c> parameter, and that is deliberate.</b> FMP documents one and ignores it:
    /// measured 2026-08-27, <c>symbol=AAPL</c> and <c>symbol=AAPL&amp;year=2025</c> answered byte-identical
    /// bodies. Accepting the parameter would let a caller believe they had filtered when they had not, which is
    /// a lie neither the compiler nor the response can catch. Filter
    /// <see cref="ExecutiveCompensation.Year"/> yourself, knowing what you are holding.</para>
    ///
    /// <para><b>What you are holding is the filer's whole history.</b> <c>AAPL</c> answered 339 rows spanning
    /// 1999 → 2025 and <c>JPM</c> 160, in one call. There is no paging and no server-side filter; the SDK does
    /// not emulate one, because a client-side year filter would hide the size of what was actually
    /// fetched.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every row FMP holds for the filer. Empty for an unknown symbol — <c>ZZZZNOPE</c> answered
    /// <c>[]</c> with HTTP 200. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ExecutiveCompensation>> GetExecutiveCompensationAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/governance-executive-compensation").With("symbol", symbol),
            FmpJsonContext.Default.ListExecutiveCompensation, ct);
    }

    /// <summary>Average executive compensation by SEC industry — <c>stable/executive-compensation-benchmark</c>.
    ///
    /// <para><b>Omitting <paramref name="year"/> answers last year, not this one.</b> Measured on 2026-08-27,
    /// the bare call answered 377 rows every one of them stamped <c>2024</c>. The SDK sends no year of its own
    /// when the caller supplies none — substituting one would answer a different question than was asked — so
    /// read <see cref="ExecutiveCompensationBenchmark.Year"/> on the rows rather than assuming.</para>
    ///
    /// <para><b>The first call is slow.</b> 37.18 s cold against 0.53 s warm, measured 2026-08-27. That is
    /// inside this SDK's default timeout and outside many shorter ones.</para>
    ///
    /// <para><b>Recorded 402 on free and on Starter by an independent client on 2026-08-23</b>, and working on
    /// Premium. Not measurable here: every path in this group answered 200 on the Ultimate key this SDK was
    /// measured with. Catch <see cref="FmpPlanRestrictedException"/> if your consumers may be on a lower
    /// tier.</para></summary>
    /// <param name="year">The year to benchmark. Omitted from the request when null, which gets FMP's own
    /// default of last year.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry. A year outside the data answers a single zero row rather than an error.
    /// Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403 — see the plan note above.</exception>
    public Task<IReadOnlyList<ExecutiveCompensationBenchmark>> GetExecutiveCompensationBenchmarkAsync(
        int? year = null, CancellationToken ct = default)
        => transport.GetListAsync(
            new FmpRequest("stable/executive-compensation-benchmark").With("year", year),
            FmpJsonContext.Default.ListExecutiveCompensationBenchmark, ct);

    /// <summary>An issuer's registered notes and preferred shares — <c>stable/company-notes</c>.
    ///
    /// <para><b>Read <see cref="CompanyNote.Symbol"/>'s documentation before using this.</b> The symbols on the
    /// rows name the individual securities, not the issuer, and they contain spaces — 19 of <c>T</c>'s 20 rows
    /// differ from the requested ticker, measured 2026-08-27.</para>
    ///
    /// <para><b>The dataset is sparse and an empty answer is normal.</b> <c>AAPL</c> answered 7 rows, <c>T</c>
    /// 20, <c>F</c> 16; <c>JPM</c>, <c>BAC</c>, <c>VZ</c>, <c>GS</c>, <c>MS</c>, <c>PG</c> and <c>JNJ</c> all
    /// answered <c>[]</c>.</para></summary>
    /// <param name="symbol">The <b>issuer's</b> ticker. What comes back is keyed by the notes' own symbols.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The issuer's registered securities. Empty for most issuers. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CompanyNote>> GetNotesAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/company-notes").With("symbol", symbol),
            FmpJsonContext.Default.ListCompanyNote, ct);
    }

    /// <summary>The largest page <c>stable/mergers-acquisitions-latest</c> will serve, measured rather than
    /// documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as <see cref="MaxDelistedPageSize"/>: measured
    /// 2026-08-27, <c>limit=1000</c> answered 1,000 rows and <c>limit=5000</c> answered 1,000 as well —
    /// silently clamped, with no error and nothing in the response to say so. A caller who asks for 5,000 and
    /// walks pages assuming they got them skips four fifths of the archive.
    /// <see cref="GetLatestMergersAcquisitionsAsync(int, int, CancellationToken)"/> therefore rejects a larger
    /// <c>limit</c> rather than passing it on to be clamped.</para></summary>
    public const int MaxMergerAcquisitionPageSize = 1000;

    /// <summary>One page of <c>stable/mergers-acquisitions-latest</c> — the whole M&amp;A archive, newest
    /// filing first.
    ///
    /// <para><b>"Latest" names the ordering, not the contents.</b> Measured 2026-08-27, page 0 at
    /// <c>limit=1000</c> already reaches back to <c>2021-09-13</c>, and the full archive is <b>4,704 rows
    /// across pages 0–4</b> spanning <c>1994-01-10 → 2026-08-25</c>. Page 4 carries 704; page 5 and beyond
    /// answer <c>[]</c> with HTTP 200. Pages 0 and 1 share no rows, so the walk is disjoint and terminates on
    /// the first short page — one request cheaper than waiting for the empty one, exactly as
    /// <see cref="GetDelistedAsync(int, int, CancellationToken)"/> does.</para>
    ///
    /// <para><b>Recorded 402 on free by an independent client on 2026-08-23</b>, needing Starter or higher.
    /// Not measurable here: every path in this group answered 200 on the Ultimate key this SDK was measured
    /// with.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxMergerAcquisitionPageSize"/>. Required rather
    /// than defaulted, matching <see cref="GetDelistedAsync(int, int, CancellationToken)"/>: the page size and
    /// the page index have to agree for a walk to be complete, and a default would let them disagree
    /// invisibly.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows, newest first. Empty past the end of the archive. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxMergerAcquisitionPageSize"/>.</exception>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403 — see the plan note above.</exception>
    public Task<IReadOnlyList<MergerAcquisition>> GetLatestMergersAcquisitionsAsync(
        int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxMergerAcquisitionPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/mergers-acquisitions-latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListMergerAcquisition, ct);
    }

    /// <summary>Deals whose acquirer or target matches a name — <c>stable/mergers-acquisitions-search</c>.
    ///
    /// <para><b>There is no <c>page</c> and no <c>limit</c>, and that is deliberate.</b> FMP documents both and
    /// ignores both: measured 2026-08-27, <c>name=Bank</c> answered 233 rows bare and <b>233 rows with
    /// <c>page=0&amp;limit=5</c></b>. The endpoint returns its entire result set every time. A signature that
    /// accepted those parameters would let a caller believe they had asked for five rows while holding 233 —
    /// and nothing in the response would tell them otherwise. Take what comes back and page it yourself if you
    /// need to.</para>
    ///
    /// <para>Matching is substring-ish rather than exact: <c>name=Apple</c> answered 3 rows on 2026-08-27,
    /// including <c>Pineapple Energy Inc.</c>. <c>name=zzzznope</c> answered <c>[]</c>; omitting the name
    /// entirely answers <b>400</b>, which is why a blank one is rejected here.</para>
    ///
    /// <para><b>Recorded 402 on free and on Starter by an independent client on 2026-08-23</b>, and working on
    /// Premium.</para></summary>
    /// <param name="name">The company name to match. Matched loosely — see above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every matching deal, unpaged. Empty when nothing matches. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403 — see the plan note above.</exception>
    public Task<IReadOnlyList<MergerAcquisition>> SearchMergersAcquisitionsAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/mergers-acquisitions-search").With("name", name),
            FmpJsonContext.Default.ListMergerAcquisition, ct);
    }

    /// <summary>The request both employee-count paths make. Shared because the two are one dataset behind two
    /// documented names — see <see cref="GetEmployeeCountAsync(string, int?, CancellationToken)"/>. The path is
    /// the only difference, and each caller passes a literal.</summary>
    private Task<IReadOnlyList<EmployeeCount>> EmployeeCounts(
        string path, string symbol, int? limit, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest(path).With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListEmployeeCount, ct);
    }
}
