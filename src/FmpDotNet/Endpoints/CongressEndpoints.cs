using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's congressional disclosure group — what members of Congress traded, who they are, and what
/// Senators are worth.
///
/// <para><b>Twelve paths, five row shapes.</b> Eight of the twelve answer
/// <see cref="CongressionalTrade"/>; the other four each answer their own.</para>
///
/// <para><b>Two of those paths are named for a parameter they do not accept, and this facade exists partly to
/// close that.</b> <c>house-trades-by-id</c> and <c>senate-trades-by-id</c> take <c>senateID</c>. Measured
/// 2026-08-29, passing <c>id</c> is not rejected — it is discarded, and the endpoint answers 200 with the
/// unfiltered latest feed: 100 well-formed rows belonging to 21 members the caller did not ask about. See
/// <see cref="GetHouseTradesByMemberAsync"/>.</para>
///
/// <para><b>Row order is not stable between calls.</b> Measured 2026-08-29, two requests seconds apart
/// returned the same 142 rows with 104 of 142 positions changed. Nothing that consumes these methods may
/// depend on position. Re-measured 2026-09-02 (#52) the same 142 rows came back in the same order twice, as
/// did <c>house-trades</c> and <c>senate-positions</c> — it was unstable once, and nothing here promises it
/// will not be again.</para>
///
/// <para><b>Paging is per path and cannot be reasoned about from a sibling (#52).</b> Measured 2026-09-02: the
/// two latest feeds and the four filtered trade paths page at 100 with <c>limit</c> honoured to
/// <see cref="MaxCongressionalTradePageSize"/>; the two <c>-by-name</c> paths answer everything and ignore
/// both parameters; <c>senate-positions</c> and <c>senate-profile</c> page at 300 and 500; the two net-worth
/// paths ignore both. Where a path pages, the page boundary can cut a run of equal sort keys two ways — see
/// <see cref="GetHouseTradesAsync"/>.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-29 against an Ultimate key. No path in
/// the group answered 402.</para>
///
/// <para><b>Plan tier — mixed, second-hand.</b> As fmpsdk 20260824.0, the independent client this SDK is
/// cross-checked against, records it: <see cref="GetHouseLatestAsync"/> and <see cref="GetSenateLatestAsync"/> work
/// on a free key; the six filtered trade methods need Starter or higher; <see cref="GetProfilesAsync"/>,
/// <see cref="GetPositionsAsync"/>, <see cref="GetNetWorthAsync"/> and <see cref="GetNetWorthSummaryAsync"/> need
/// Premium or higher (402 on free and Starter; working on Premium 2026-08-23). Not verified here: every path answered
/// 200 on the Ultimate key this SDK is measured with (2026-09-02). The members off the class's main rung carry their
/// own notes. A dated observation, not a contract — catch <see cref="FmpPlanRestrictedException"/> rather than gating
/// on it.</para></summary>
public sealed class CongressEndpoints(FmpTransport transport)
{
    /// <summary>The largest page either latest feed will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-29, <c>house-latest?limit=1000</c> and
    /// <c>?limit=5000</c> each answered exactly 250 rows at HTTP 200, with nothing in the body saying the
    /// request had been trimmed. The same cap holds on the filtered trade paths: measured 2026-09-02 (#52),
    /// <c>house-trades</c> and <c>senate-trades</c> answered 250 to <c>limit=251</c> and to
    /// <c>limit=1000</c>.</para></summary>
    public const int MaxCongressionalTradePageSize = 250;

    /// <summary>The largest page <c>stable/senate-positions</c> serves, and its default. Measured 2026-09-02
    /// (#52): <c>limit=5</c> answered 5, <c>limit=5000</c> answered 300 with nothing in the body saying so. The
    /// whole dataset is 8,227 rows over 28 such pages.</summary>
    public const int MaxCongressMemberPositionPageSize = 300;

    /// <summary>The largest page <c>stable/senate-profile</c> serves, and its default. Measured 2026-09-02
    /// (#52): <c>limit=5</c> answered 5, <c>limit=5000</c> answered 500 with nothing in the body saying
    /// so.</summary>
    public const int MaxCongressMemberProfilePageSize = 500;

    // ---- the eight trade paths ---------------------------------------------------------------------------

    /// <summary>Every House disclosure as it arrives, newest first — <c>stable/house-latest</c>.
    ///
    /// <para>The 100 rows a bare call returns is a default rather than a cap; see
    /// <see cref="MaxCongressionalTradePageSize"/> for where it stops.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's disclosures. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/house-latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every Senate disclosure as it arrives, newest first — <c>stable/senate-latest</c>.
    ///
    /// <para><b>The one path of the eight that omits <c>capitalGainsOver200USD</c>.</b> Measured 2026-08-29,
    /// 0 of its 100 rows carried the key, against 100% on the other seven — so
    /// <see cref="CongressionalTrade.CapitalGainsOver200Usd"/> is always <see langword="null"/> on rows from
    /// here. That is the feed's silence, not a missing value in the disclosure.</para>
    ///
    /// <para><b>Plan tier — Free, second-hand.</b> Recorded working on a free key by fmpsdk 20260824.0, which puts
    /// this member below the rest of its class; answered 200 on the Ultimate key here (2026-09-02). See the class
    /// remarks for what that is worth.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's disclosures. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>House disclosures of one ticker, one page at a time — <c>stable/house-trades</c>.
    ///
    /// <para><b>A bare call is the first 100, and until #52 this method could ask for nothing else.</b> Measured
    /// 2026-09-02, <c>symbol=AAPL</c> holds 513 rows; the endpoint answers 100 of them at HTTP 200 with nothing
    /// in the body saying so. <paramref name="page"/> is a page index over <paramref name="limit"/>-sized pages,
    /// as on <see cref="GetHouseLatestAsync"/>, and the same 250 cap applies.</para>
    ///
    /// <para><b>Page at the cap, and de-duplicate.</b> Rows come ordered by
    /// <see cref="CongressionalTrade.DisclosureDate"/> alone, and the order within one date depends on the
    /// request — so a page boundary inside a run of one date can put a row on both sides, or on neither. Measured
    /// 2026-09-02 on AAPL: six pages of 100 answered 513 rows of which <b>510 were distinct</b> — three twice,
    /// three never — identically on two passes; three pages of <see cref="MaxCongressionalTradePageSize"/>
    /// answered all 513 once. There is no row identifier, so de-duplicate on the whole row.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>. FMP's own default is
    /// 100; the cap pages cleaner — see above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's disclosures. Never <see langword="null"/>; empty for an unknown symbol or a page past
    /// the end, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesAsync(
        string symbol, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades").With("symbol", symbol).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Senate disclosures of one ticker, one page at a time — <c>stable/senate-trades</c>.
    ///
    /// <para>Pages exactly as <see cref="GetHouseTradesAsync"/> does, ties and all: measured 2026-09-02,
    /// <c>symbol=AAPL</c> answered 287 rows over three pages of 100, two of which were also on the first page.
    /// Page at <see cref="MaxCongressionalTradePageSize"/> and de-duplicate on the whole row.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>. FMP's own default is
    /// 100.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's disclosures. Never <see langword="null"/>; empty for an unknown symbol or a page past
    /// the end, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesAsync(
        string symbol, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-trades").With("symbol", symbol).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by one member — <c>stable/house-trades-by-id</c>.
    ///
    /// <para><b>The path is named <c>-by-id</c> and the parameter is <c>senateID</c>.</b> Measured
    /// 2026-08-29, <c>?id=M001217</c> was silently ignored and answered the unfiltered latest feed —
    /// 100 rows spanning 21 members — while <c>?senateID=M001217</c> answered that member alone. This method
    /// sends <c>senateID</c> and requires it, because the endpoint's willingness to answer without one is the
    /// hazard rather than a convenience. For the unfiltered feed, call
    /// <see cref="GetHouseLatestAsync"/>, which says so in its name.</para>
    ///
    /// <para><b>Pages at 100 (#52).</b> Measured 2026-09-02, <c>P000197</c> holds 142 rows and a bare call
    /// answers 100 of them. Two pages of 100 answered all 142 once, and one call at
    /// <see cref="MaxCongressionalTradePageSize"/> answered the same 142 — so for a member, one call at the cap
    /// is usually the whole history. <see cref="GetHouseTradesByNameAsync"/> answers it whole regardless of
    /// size.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier — <c>M001217</c>. Carried on every row as
    /// <see cref="CongressionalTrade.SenateId"/>, and listed by
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>. FMP's own default is
    /// 100.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page of that member's disclosures. Never <see langword="null"/>; empty for a member with
    /// none or a page past the end, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByMemberAsync(
        string senateId, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades-by-id")
                .With("senateID", senateId).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every Senate disclosure by one member — <c>stable/senate-trades-by-id</c>.
    ///
    /// <para><b>Named <c>-by-id</c>, keyed on <c>senateID</c>, exactly as
    /// <see cref="GetHouseTradesByMemberAsync"/> is.</b> Measured 2026-08-29 the Senate path behaves the same
    /// way: <c>id</c> is discarded rather than rejected, and the caller is handed the unfiltered latest feed
    /// at HTTP 200. The identifier is required here for that reason. For the unfiltered feed, call
    /// <see cref="GetSenateLatestAsync"/>.</para>
    ///
    /// <para><b>Pages at 100, and the bare call was answering 100 of 145 (#52).</b> Measured 2026-09-01 and
    /// again 2026-09-02, <c>M001243</c> answers 100, 45 and 0 across pages 0 to 2, and one call at
    /// <see cref="MaxCongressionalTradePageSize"/> answers the same 145 once.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier — <c>M001243</c>. Carried on every row as
    /// <see cref="CongressionalTrade.SenateId"/>, and listed by
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>. FMP's own default is
    /// 100.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page of that member's disclosures. Never <see langword="null"/>; empty for a member with
    /// none or a page past the end, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesByMemberAsync(
        string senateId, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-trades-by-id")
                .With("senateID", senateId).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by surname — <c>stable/house-trades-by-name</c>.
    ///
    /// <para><b>Matches the last name.</b> Measured 2026-08-29, <c>name=Pelosi</c> answered 142 rows all
    /// belonging to <c>P000197</c>, and a given name — <c>name=Zach</c> — answered none.</para>
    ///
    /// <para>An empty result means the member disclosed nothing, not that the lookup failed: Zach Nunn is a
    /// sitting Representative in <see cref="GetProfilesAsync"/> with no trades.</para>
    ///
    /// <para><b>Answers the whole history in one body; <c>limit</c> and <c>page</c> are accepted and ignored
    /// (#52).</b> Measured 2026-09-02 on <c>Pelosi</c>: <c>limit=5</c>, <c>limit=250</c>, <c>page=1</c> and
    /// <c>page=1&amp;limit=5</c> each answered all 142 rows, byte-identical to the bare call. Neither is offered
    /// here for that reason. The <c>-by-id</c> path, which pages at 100, answers the same 142 to
    /// <c>P000197</c>.</para></summary>
    /// <param name="name">The member's surname.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unmatched surname, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades-by-name").With("name", name),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every Senate disclosure by surname — <c>stable/senate-trades-by-name</c>.
    ///
    /// <para><b>Matches the last name</b>, like <see cref="GetHouseTradesByNameAsync"/>. An empty result
    /// means the member disclosed nothing rather than that the lookup failed.</para>
    ///
    /// <para><b>Answers the whole history in one body, however long (#52).</b> Measured 2026-09-02,
    /// <c>Tuberville</c> answered <b>1,406 rows</b> to the bare call and to <c>limit=5</c>, <c>page=1</c> and
    /// <c>page=1&amp;limit=5</c> alike — the parameters are ignored, so none is offered — where
    /// <see cref="GetSenateTradesByMemberAsync"/> would hand the same history out 250 rows at a
    /// time.</para></summary>
    /// <param name="name">The member's surname.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unmatched surname, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesByNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-trades-by-name").With("name", name),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    // ---- who the members are -----------------------------------------------------------------------------

    /// <summary>Every term served, one row per member per Congress — <c>stable/senate-positions</c>.
    ///
    /// <para>Serves the House as well as the Senate despite the path's name; measured 2026-08-29,
    /// <see cref="CongressMemberPosition.Position"/> carries both <c>Representative</c> and
    /// <c>Senator</c>.</para>
    ///
    /// <para><b>No <c>limit</c> parameter yet — but FMP does not ignore it, it CLAMPS it, and an earlier
    /// version of this note drew the wrong conclusion from a correct measurement.</b> That <c>?limit=500</c>
    /// answers 300 and <c>?limit=1000</c> answers 500 on the sibling path is true, and page 1 does answer a
    /// further 300 with no overlap. But every value tried on 2026-08-29 was <i>above</i> the page size, and no
    /// value above a cap can tell "discarded" from "clamped". Re-measured 2026-09-01 (#46), <c>limit=5</c>
    /// answers <b>5</b> and <c>limit=5000</c> answers <b>300</b>: the parameter is honoured downward and
    /// clamped upward. So the omission is now a gap rather than a deliberate refusal, and it is tracked with
    /// the four filters this path also accepts — <c>party</c>, <c>position</c> and <c>senateID</c> all measured
    /// honoured — as #52.</para>
    ///
    /// <para><b>Settled 2026-09-02 (#52): the filters and the paging live on
    /// <see cref="CongressPositionCriteria"/>.</b> An empty criteria is the bare call — the first 300 of
    /// <b>8,227 rows over 28 pages</b>, measured to exhaustion the same day. The filters are exact,
    /// case-sensitive and silent about a value they do not know; the criteria type carries the
    /// evidence.</para>
    ///
    /// <para><b>Plan tier — Premium, second-hand.</b> Recorded 402 on free and Starter and working on Premium
    /// (2026-08-23) by fmpsdk 20260824.0; answered 200 on the Ultimate key here (2026-09-02). See the class remarks
    /// for what that is worth.</para></summary>
    /// <param name="criteria">The filters and the page. Required rather than optional so the call site says what
    /// it is asking for — pass <c>new CongressPositionCriteria()</c> for FMP's first page.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's terms. Never <see langword="null"/>; empty for a filter value FMP does not recognise
    /// as well as for a page past the end.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="CongressPositionCriteria.Page"/> is negative, or
    /// <see cref="CongressPositionCriteria.Limit"/> is outside 1 to
    /// <see cref="MaxCongressMemberPositionPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressMemberPosition>> GetPositionsAsync(
        CongressPositionCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ThrowIfCriteriaPagingOutOfRange(criteria.Page, criteria.Limit, MaxCongressMemberPositionPageSize);
        return transport.GetListAsync(criteria.ToRequest(), FmpJsonContext.Default.ListCongressMemberPosition, ct);
    }

    /// <summary>Every member FMP knows, one row each — <c>stable/senate-profile</c>.
    ///
    /// <para><b>The one path in this group whose universe was enumerated to exhaustion.</b> Measured
    /// 2026-08-29, page 0 answered 500, page 1 answered 35 and page 2 answered none — <b>535
    /// members</b>.</para>
    ///
    /// <para><b>No <c>limit</c> parameter yet, and not because FMP ignores it.</b> <c>?limit=1000</c> answering
    /// 500 is a clamp to the page size, not a discard: re-measured 2026-09-01 (#46), <c>limit=5</c> answers
    /// <b>5</b>. Same correction as <see cref="GetPositionsAsync"/>, and the same follow-up — this path also
    /// honours <c>active</c>, <c>latestParty</c>, <c>latestPosition</c> and <c>senateID</c>, none of which is
    /// offered here. See #52.</para>
    ///
    /// <para>This is where a <c>senateID</c> for <see cref="GetHouseTradesByMemberAsync"/> or
    /// <see cref="GetNetWorthAsync"/> comes from.</para>
    ///
    /// <para><b>535 was the active half (#52).</b> Measured 2026-09-02, the bare answer is byte-identical to
    /// <c>active=true</c>; <c>active=false</c> answers a further <b>720</b> former members over two pages, so
    /// the universe is <b>1,255</b> and it takes <see cref="CongressProfileCriteria.Active"/> =
    /// <see langword="false"/> to see the second half. The filters and the paging live on
    /// <see cref="CongressProfileCriteria"/>; an empty criteria is the bare call.</para>
    ///
    /// <para><b>Plan tier — Premium, second-hand.</b> Recorded 402 on free and Starter and working on Premium
    /// (2026-08-23) by fmpsdk 20260824.0; answered 200 on the Ultimate key here (2026-09-02). See the class remarks
    /// for what that is worth.</para></summary>
    /// <param name="criteria">The filters and the page. Required rather than optional so the call site says what
    /// it is asking for, which on this path is never "everyone" — pass <c>new CongressProfileCriteria()</c> for
    /// the first 500 active members.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's members. Never <see langword="null"/>; empty for a filter value FMP does not
    /// recognise as well as for a page past the end.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="CongressProfileCriteria.Page"/> is negative, or
    /// <see cref="CongressProfileCriteria.Limit"/> is outside 1 to
    /// <see cref="MaxCongressMemberProfilePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressMemberProfile>> GetProfilesAsync(
        CongressProfileCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ThrowIfCriteriaPagingOutOfRange(criteria.Page, criteria.Limit, MaxCongressMemberProfilePageSize);
        return transport.GetListAsync(criteria.ToRequest(), FmpJsonContext.Default.ListCongressMemberProfile, ct);
    }

    // ---- what they are worth -----------------------------------------------------------------------------

    /// <summary>Every line of one Senator's financial disclosures — <c>stable/senate-net-worth</c>.
    ///
    /// <para>One row per disclosed asset, income source or liability, across every report filed. Measured
    /// 2026-08-29, <c>H000601</c> answered 250 rows and <c>limit</c> was ignored, so none is offered.</para>
    ///
    /// <para><b><c>page</c> is inert here too, which is worth stating because it is honoured on this group's
    /// four trade paths.</b> Re-measured 2026-09-01 (#46) on <c>M001243</c>, which also answers 250:
    /// <c>limit=5</c>, <c>limit=1</c>, <c>page=1</c> and <c>page=2</c> each returned the full 250 rows in a body
    /// byte-identical to the request without them. Meanwhile <c>senate-trades-by-id</c> pages properly —
    /// 100, 45, 0 across pages 0 to 2. So paging on this group is per-path and cannot be reasoned about from a
    /// sibling. 250 looks like a round number but is not a cap: nothing here answered more than the filer
    /// held.</para>
    ///
    /// <para><b>Plan tier — Premium, second-hand.</b> Recorded 402 on free and Starter and working on Premium
    /// (2026-08-23) by fmpsdk 20260824.0; answered 200 on the Ultimate key here (2026-09-02). See the class remarks
    /// for what that is worth.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier, from
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That member's disclosure lines. Never <see langword="null"/>; empty for a member who has
    /// filed none, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SenateNetWorthLine>> GetNetWorthAsync(
        string senateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-net-worth").With("senateID", senateId),
            FmpJsonContext.Default.ListSenateNetWorthLine, ct);
    }

    /// <summary>One Senator's net worth by year, totalled by category —
    /// <c>stable/senate-net-worth-aggregated</c>.
    ///
    /// <para>One row per reporting year — the aggregate of what <see cref="GetNetWorthAsync"/> returns line by
    /// line. Measured 2026-09-01 across every member <see cref="GetProfilesAsync"/> enumerates, 455 of 535
    /// answer between one and twelve rows each, 3,425 in all, and 80 answer an empty list.</para>
    ///
    /// <para><b>The row shape varies by member, and the record was once modelled from one.</b> FMP sends 27
    /// keys across the population and each member carries the subset they have ever disclosed; <c>H000601</c>
    /// carries 16, which is how this type shipped with 16 properties and dropped eleven categories on 91% of
    /// rows (#57). All 27 bind now, and a key the type does not name lands in
    /// <see cref="SenateNetWorthSummary.UnmappedFields"/> rather than vanishing.</para>
    ///
    /// <para><b>No <c>totalsCol</c>: it is accepted and ignored.</b> <c>fmpsdk</c> sends one, so this is recorded
    /// rather than left to be re-opened by the next parameter diff. Measured 2026-09-01 (#46) on <c>M001243</c>,
    /// which answers three rows: <c>totalsCol=total</c>, <c>=stock</c>, <c>=1</c> and <c>=true</c> each returned
    /// a body byte-identical to the request without it. Four values rather than one because a single wrong value
    /// cannot tell "ignored" from "unrecognised" — if there is a working vocabulary here, none of a column name,
    /// a category name, an index and a boolean is in it.</para>
    ///
    /// <para><b>Plan tier — Premium, second-hand.</b> Recorded 402 on free and Starter and working on Premium
    /// (2026-08-23) by fmpsdk 20260824.0; answered 200 on the Ultimate key here (2026-09-02). See the class remarks
    /// for what that is worth.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier, from
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That member's yearly totals. Never <see langword="null"/>; empty for a member who has filed
    /// none, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SenateNetWorthSummary>> GetNetWorthSummaryAsync(
        string senateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-net-worth-aggregated").With("senateID", senateId),
            FmpJsonContext.Default.ListSenateNetWorthSummary, ct);
    }

    /// <summary>The paging guard the two latest feeds and the four filtered trade paths share, extracted for the
    /// reason <see cref="InsiderTradesEndpoints"/> extracts its own: the callers need an identical guard set, so
    /// the body is the thing that must not drift between them.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxCongressionalTradePageSize);
    }

    /// <summary>The same guard for a criteria object, where either value may be unset and each path has its own
    /// cap. Checked here rather than left to FMP for the reason <see cref="SearchEndpoints.ScreenAsync"/> checks
    /// its own: a negative page is answered rather than rejected, and a limit above the cap is clamped with
    /// nothing in the body saying so.</summary>
    private static void ThrowIfCriteriaPagingOutOfRange(int? page, int? limit, int maxPageSize)
    {
        if (page is { } p) ArgumentOutOfRangeException.ThrowIfNegative(p, "criteria");
        if (limit is { } l)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(l, "criteria");
            ArgumentOutOfRangeException.ThrowIfGreaterThan(l, maxPageSize, "criteria");
        }
    }
}
