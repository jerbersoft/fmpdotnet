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
/// depend on position.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-29 against an Ultimate key. No path in
/// the group answered 402.</para></summary>
public sealed class CongressEndpoints(FmpTransport transport)
{
    /// <summary>The largest page either latest feed will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-29, <c>house-latest?limit=1000</c> and
    /// <c>?limit=5000</c> each answered exactly 250 rows at HTTP 200, with nothing in the body saying the
    /// request had been trimmed.</para></summary>
    public const int MaxCongressionalTradePageSize = 250;

    // ---- the eight trade paths ---------------------------------------------------------------------------

    /// <summary>Every House disclosure as it arrives, newest first — <c>stable/house-latest</c>.
    ///
    /// <para>The 100 rows a bare call returns is a default rather than a cap; see
    /// <see cref="MaxCongressionalTradePageSize"/> for where it stops.</para></summary>
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
    /// here. That is the feed's silence, not a missing value in the disclosure.</para></summary>
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

    /// <summary>Every House disclosure of one ticker — <c>stable/house-trades</c>.</summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unknown symbol, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades").With("symbol", symbol),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every Senate disclosure of one ticker — <c>stable/senate-trades</c>.</summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unknown symbol, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-trades").With("symbol", symbol),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by one member — <c>stable/house-trades-by-id</c>.
    ///
    /// <para><b>The path is named <c>-by-id</c> and the parameter is <c>senateID</c>.</b> Measured
    /// 2026-08-29, <c>?id=M001217</c> was silently ignored and answered the unfiltered latest feed —
    /// 100 rows spanning 21 members — while <c>?senateID=M001217</c> answered that member alone. This method
    /// sends <c>senateID</c> and requires it, because the endpoint's willingness to answer without one is the
    /// hazard rather than a convenience. For the unfiltered feed, call
    /// <see cref="GetHouseLatestAsync"/>, which says so in its name.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier — <c>M001217</c>. Carried on every row as
    /// <see cref="CongressionalTrade.SenateId"/>, and listed by
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That member's disclosures. Never <see langword="null"/>; empty for a member with none, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByMemberAsync(
        string senateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades-by-id").With("senateID", senateId),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every Senate disclosure by one member — <c>stable/senate-trades-by-id</c>.
    ///
    /// <para><b>Named <c>-by-id</c>, keyed on <c>senateID</c>, exactly as
    /// <see cref="GetHouseTradesByMemberAsync"/> is.</b> Measured 2026-08-29 the Senate path behaves the same
    /// way: <c>id</c> is discarded rather than rejected, and the caller is handed the unfiltered latest feed
    /// at HTTP 200. The identifier is required here for that reason. For the unfiltered feed, call
    /// <see cref="GetSenateLatestAsync"/>.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier — <c>M001243</c>. Carried on every row as
    /// <see cref="CongressionalTrade.SenateId"/>, and listed by
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That member's disclosures. Never <see langword="null"/>; empty for a member with none, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesByMemberAsync(
        string senateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-trades-by-id").With("senateID", senateId),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by surname — <c>stable/house-trades-by-name</c>.
    ///
    /// <para><b>Matches the last name.</b> Measured 2026-08-29, <c>name=Pelosi</c> answered 142 rows all
    /// belonging to <c>P000197</c>, and a given name — <c>name=Zach</c> — answered none.</para>
    ///
    /// <para>An empty result means the member disclosed nothing, not that the lookup failed: Zach Nunn is a
    /// sitting Representative in <see cref="GetProfilesAsync"/> with no trades.</para></summary>
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
    /// means the member disclosed nothing rather than that the lookup failed.</para></summary>
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
    /// <para><b>No <c>limit</c> parameter, because FMP ignores it.</b> Measured 2026-08-29,
    /// <c>?limit=500</c> answered 300 — the page size — and page 1 answered a further 300 with no overlap.
    /// Offering a <c>limit</c> the server discards would let a caller believe they had asked for
    /// something.</para></summary>
    /// <param name="page">Zero-based page index; 300 rows per page. A page past the end answers an empty
    /// list, not an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's terms. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressMemberPosition>> GetPositionsAsync(
        int page = 0, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-positions").With("page", page),
            FmpJsonContext.Default.ListCongressMemberPosition, ct);
    }

    /// <summary>Every member FMP knows, one row each — <c>stable/senate-profile</c>.
    ///
    /// <para><b>The one path in this group whose universe was enumerated to exhaustion.</b> Measured
    /// 2026-08-29, page 0 answered 500, page 1 answered 35 and page 2 answered none — <b>535
    /// members</b>.</para>
    ///
    /// <para><b>No <c>limit</c> parameter, because FMP ignores it</b> — <c>?limit=1000</c> answered 500. See
    /// <see cref="GetPositionsAsync"/>.</para>
    ///
    /// <para>This is where a <c>senateID</c> for <see cref="GetHouseTradesByMemberAsync"/> or
    /// <see cref="GetNetWorthAsync"/> comes from.</para></summary>
    /// <param name="page">Zero-based page index; 500 rows per page. A page past the end answers an empty
    /// list, not an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's members. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressMemberProfile>> GetProfilesAsync(
        int page = 0, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        return transport.GetListAsync(
            new FmpRequest("stable/senate-profile").With("page", page),
            FmpJsonContext.Default.ListCongressMemberProfile, ct);
    }

    // ---- what they are worth -----------------------------------------------------------------------------

    /// <summary>Every line of one Senator's financial disclosures — <c>stable/senate-net-worth</c>.
    ///
    /// <para>One row per disclosed asset, income source or liability, across every report filed. Measured
    /// 2026-08-29, <c>H000601</c> answered 250 rows and <c>limit</c> was ignored, so none is
    /// offered.</para></summary>
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
    /// <para>One row per reporting year. Measured 2026-08-29, <c>H000601</c> answered six, 2019 through
    /// 2024 — the aggregate of what <see cref="GetNetWorthAsync"/> returns line by line.</para></summary>
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

    /// <summary>The paging guard the two latest feeds share, extracted for the reason
    /// <see cref="InsiderTradesEndpoints"/> extracts its own: the two callers need an identical guard set, so
    /// the body is the thing that must not drift between them.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxCongressionalTradePageSize);
    }
}
