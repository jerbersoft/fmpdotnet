namespace FmpDotNet;

/// <summary>Which terms to list from <c>stable/senate-positions</c>. Every property is optional; the ones left
/// unset are not sent, so an empty <see cref="CongressPositionCriteria"/> asks for FMP's default page — the first
/// 300 of 8,227 rows, measured 2026-09-02 over 28 pages.
///
/// <para><b>Each filter is named for the row property it matches</b>, so the value to send is the value that comes
/// back on <see cref="Models.CongressMemberPosition"/>. That matters because the filters are exact and
/// case-sensitive and the endpoint does not say so: measured 2026-09-02, <c>party=Republican</c> answered 300 rows,
/// every one Republican, while <c>republican</c> and <c>Whig</c> each answered an empty list at HTTP 200 —
/// indistinguishable from a real filter that matched nothing.</para>
///
/// <para><b>Filters run over the dataset and then page.</b> The bare first page held 191 Republicans and 109
/// Democrats; <c>party=Republican</c> answered a full 300, not the 191. Filters combine as AND: <c>senateID</c>
/// with the member's own party answered the row, with the other party answered nothing.</para></summary>
public sealed record CongressPositionCriteria
{
    /// <summary>Party for the term — <c>Democrat</c> or <c>Republican</c>, spelled as
    /// <see cref="Models.CongressMemberPosition.Party"/> spells it.</summary>
    public string? Party { get; init; }

    /// <summary>The seat — <c>Representative</c> or <c>Senator</c>, as
    /// <see cref="Models.CongressMemberPosition.Position"/> spells it. Measured 2026-09-02, <c>Senator</c> narrowed
    /// the first page from 37 Senators among 300 to 300 Senators.</summary>
    public string? Position { get; init; }

    /// <summary>One member's Bioguide identifier — <c>M001243</c>. Sent as <c>senateID</c>, FMP's spelling on
    /// every path in the group. Measured 2026-09-02, 300 → 4 rows: one per term served.</summary>
    public string? SenateId { get; init; }

    /// <summary>Zero-based page index over <see cref="Limit"/>-sized pages. Measured 2026-09-02,
    /// <c>page=1&amp;limit=5</c> answered exactly rows 6 to 10 of the bare answer. A page past the end answers an
    /// empty list, not an error.</summary>
    public int? Page { get; init; }

    /// <summary>Rows per page, 1 to <see cref="Endpoints.CongressEndpoints.MaxCongressMemberPositionPageSize"/>.
    /// Left unset, FMP serves 300 — which is also the cap: measured 2026-09-02, <c>limit=5</c> answered 5 and
    /// <c>limit=5000</c> answered 300 with nothing in the body saying so, which is why the facade refuses a value
    /// above the cap rather than sending it.</summary>
    public int? Limit { get; init; }

    /// <summary>Renders the criteria onto a request, dropping everything unset — the reason an empty criteria is
    /// FMP's default page rather than a request for nothing.</summary>
    internal FmpRequest ToRequest() =>
        new FmpRequest("stable/senate-positions")
            .With("party", Party)
            .With("position", Position)
            .With("senateID", SenateId)
            .With("page", Page)
            .With("limit", Limit);
}
