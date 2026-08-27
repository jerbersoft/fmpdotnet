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
        ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-market-capitalization")
                .With("symbol", symbol).With("from", from).With("to", to).With("limit", limit),
            FmpJsonContext.Default.ListMarketCapitalization, ct);
    }

    /// <summary>Rejects a transposed range before it costs a call, matching <c>ChartEndpoints.ThrowIfBackwards</c>.
    ///
    /// <para>Nullable on both ends because the range is optional here, unlike on the chart endpoints: one end
    /// alone cannot be backwards, so the guard fires only when both are supplied.</para></summary>
    private static void ThrowIfBackwards(LocalDate? from, LocalDate? to)
    {
        if (from is { } start && to is { } end && end < start)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"'to' must not be earlier than 'from' ({start:uuuu-MM-dd}).");
    }
}
