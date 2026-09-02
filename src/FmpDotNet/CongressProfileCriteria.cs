namespace FmpDotNet;

/// <summary>Which members to list from <c>stable/senate-profile</c>. Every property is optional and the ones left
/// unset are not sent — but on this path <b>the default is a filter, not the universe</b>.
///
/// <para><b>An empty <see cref="CongressProfileCriteria"/> answers the active members only.</b> Measured
/// 2026-09-02, the bare answer is byte-identical to <c>active=true</c>: 535 members over two pages of 500. The
/// former members are reached only by sending <see cref="Active"/> = <see langword="false"/> — <b>720</b> of them,
/// over two pages — so the universe is 1,255 and "everyone" is two requests, not one.</para>
///
/// <para><b>Each filter is named for the row property it matches</b>, so the value to send is the value that comes
/// back on <see cref="Models.CongressMemberProfile"/>. The filters are exact and case-sensitive and the endpoint
/// does not say so: measured 2026-09-02, <c>latestParty=Independent</c> answered 3 rows, all Independent;
/// <c>independent</c> and <c>Whig</c> each answered an empty list at HTTP 200. Filters combine as AND, and
/// <see cref="Active"/> combines with the rest: <c>latestPosition=Senator</c> answered 99 sitting Senators, and
/// with <c>active=false</c> 110 former ones.</para></summary>
public sealed record CongressProfileCriteria
{
    /// <summary>Whether the member currently serves. <b>Unset and <see langword="true"/> answer the same rows</b>
    /// — measured 2026-09-02, byte for byte. <see langword="false"/> is the only value that changes the answer,
    /// and it is sent as <c>active=false</c> rather than dropped with the unset properties for exactly that
    /// reason.</summary>
    public bool? Active { get; init; }

    /// <summary>Most recent party — <c>Democrat</c>, <c>Republican</c> or <c>Independent</c>, as
    /// <see cref="Models.CongressMemberProfile.LatestParty"/> spells it.</summary>
    public string? LatestParty { get; init; }

    /// <summary>Most recent seat — <c>Representative</c>, <c>Senator</c> or <c>Vice President</c>, as
    /// <see cref="Models.CongressMemberProfile.LatestPosition"/> spells it, space included. Measured 2026-09-02,
    /// <c>Vice President</c> answered one row.</summary>
    public string? LatestPosition { get; init; }

    /// <summary>One member's Bioguide identifier — <c>M001243</c>. Sent as <c>senateID</c>. Measured 2026-09-02,
    /// 500 → 1 row, which makes this the lookup for a member whose id came off a trade.</summary>
    public string? SenateId { get; init; }

    /// <summary>Zero-based page index over <see cref="Limit"/>-sized pages. Measured 2026-09-02,
    /// <c>page=1&amp;limit=100</c> answered exactly rows 101 to 200 of the bare answer, and <c>page=1</c> alone
    /// answered the 35 members past the first 500. A page past the end answers an empty list, not an
    /// error.</summary>
    public int? Page { get; init; }

    /// <summary>Rows per page, 1 to <see cref="Endpoints.CongressEndpoints.MaxCongressMemberProfilePageSize"/>.
    /// Left unset, FMP serves 500, which is also the cap: measured 2026-09-02, <c>limit=5000</c> answered 500 with
    /// nothing in the body saying so.
    ///
    /// <para><b>Small pages cut ties.</b> Paged at 50 the 535 active members came back as 535 rows of which 533
    /// were distinct; at 100 and 250 every page matched the bare order exactly. Page at 100 or more to enumerate,
    /// and de-duplicate on <see cref="Models.CongressMemberProfile.SenateId"/> regardless.</para></summary>
    public int? Limit { get; init; }

    /// <summary>Renders the criteria onto a request, dropping everything unset. <see cref="Active"/> =
    /// <see langword="false"/> is not "unset" and travels.</summary>
    internal FmpRequest ToRequest() =>
        new FmpRequest("stable/senate-profile")
            .With("active", Active)
            .With("latestParty", LatestParty)
            .With("latestPosition", LatestPosition)
            .With("senateID", SenateId)
            .With("page", Page)
            .With("limit", Limit);
}
