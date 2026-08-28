using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>What every 13F filer together reported about one symbol in one quarter, from
/// <c>stable/institutional-ownership/symbol-positions-summary</c>.
///
/// <para><b>One row, not a list.</b> The path answers a JSON array and it carried exactly one element for every
/// symbol measured 2026-08-28 — these are whole-market aggregates for the symbol and quarter, not per-filer
/// rows. <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetSymbolPositionsAsync"/> therefore returns
/// <c>SymbolPositions?</c>, unwrapping as <see cref="Endpoints.SecFilingsEndpoints.GetProfileAsync"/> does.</para>
///
/// <para><b>Twelve figures, each as a triple:</b> the quarter's value, the previous quarter's (<c>last*</c>),
/// and the change between them. The changes go negative — <see cref="ClosedPositionsChange"/> is −18 on the
/// captured row.</para>
///
/// <para><b><see cref="OwnershipPercent"/> exceeds 100, legitimately.</b> See its own documentation.</para>
///
/// <para><b>Fifteen fields are genuine counts and are <see cref="int"/>; everything else is
/// <see cref="decimal"/>.</b> The counts are the investors-holding and four position-count triples. The option
/// contract counts are the deliberate exception — see <see cref="TotalCalls"/>.</para></summary>
public sealed record SymbolPositions
{
    /// <summary>The ticker asked for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded — the one place in this facade where the CIK
    /// is an issuer's rather than a filer's, because the row is about the security rather than about a
    /// holder.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>How many institutions reported holding the security. A count of filers — 6,435 for AAPL in
    /// 2026 Q2 — hence <see cref="int"/>.</summary>
    [JsonPropertyName("investorsHolding")] public int? InvestorsHolding { get; init; }

    /// <summary>The same count one quarter earlier.</summary>
    [JsonPropertyName("lastInvestorsHolding")] public int? LastInvestorsHolding { get; init; }

    /// <summary>The change in that count. Goes negative.</summary>
    [JsonPropertyName("investorsHoldingChange")] public int? InvestorsHoldingChange { get; init; }

    /// <summary>Total shares reported across all 13F filers. <b>16,201,347,267 on the captured row — seven
    /// times <see cref="int"/>'s ceiling</b>, on a field whose name says "shares", which is the combination
    /// most likely to be retyped by somebody being helpful.</summary>
    [JsonPropertyName("numberOf13Fshares")] public decimal? NumberOf13FShares { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastNumberOf13Fshares")] public decimal? LastNumberOf13FShares { get; init; }

    /// <summary>The change in reported shares.</summary>
    [JsonPropertyName("numberOf13FsharesChange")] public decimal? NumberOf13FSharesChange { get; init; }

    /// <summary>Total dollars invested across all filers — 2,840,158,192,185 on the captured row.</summary>
    [JsonPropertyName("totalInvested")] public decimal? TotalInvested { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalInvested")] public decimal? LastTotalInvested { get; init; }

    /// <summary>The change in dollars invested.</summary>
    [JsonPropertyName("totalInvestedChange")] public decimal? TotalInvestedChange { get; init; }

    /// <summary>Reported 13F shares as a percentage of shares outstanding — <b>and it exceeds 100.</b>
    ///
    /// <para>Measured 2026-08-28 across six symbols, two were over: AAPL at <c>110.1329</c> and MSFT at
    /// <c>128.2744</c>. This is not a data fault. A 13F is filed by each reporting manager with investment
    /// discretion, so shares held through a chain of managers are reported more than once, and a sum over
    /// filers legitimately passes the shares that exist.</para>
    ///
    /// <para><b>Deliberately unvalidated.</b> No clamp, no range check and no percentage wrapper type: every
    /// one of those would turn a measured value into a wrong one. Treat it as a crowding indicator, not as a
    /// float.</para></summary>
    [JsonPropertyName("ownershipPercent")] public decimal? OwnershipPercent { get; init; }

    /// <summary>The same percentage one quarter earlier.</summary>
    [JsonPropertyName("lastOwnershipPercent")] public decimal? LastOwnershipPercent { get; init; }

    /// <summary>The change, in percentage points.</summary>
    [JsonPropertyName("ownershipPercentChange")] public decimal? OwnershipPercentChange { get; init; }

    /// <summary>How many filers opened a position this quarter. A count.</summary>
    [JsonPropertyName("newPositions")] public int? NewPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastNewPositions")] public int? LastNewPositions { get; init; }

    /// <summary>The change in that count.</summary>
    [JsonPropertyName("newPositionsChange")] public int? NewPositionsChange { get; init; }

    /// <summary>How many filers added to an existing position. A count.</summary>
    [JsonPropertyName("increasedPositions")] public int? IncreasedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastIncreasedPositions")] public int? LastIncreasedPositions { get; init; }

    /// <summary>The change in that count.</summary>
    [JsonPropertyName("increasedPositionsChange")] public int? IncreasedPositionsChange { get; init; }

    /// <summary>How many filers exited entirely. A count.</summary>
    [JsonPropertyName("closedPositions")] public int? ClosedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastClosedPositions")] public int? LastClosedPositions { get; init; }

    /// <summary>The change in that count — <c>−18</c> on the captured row.</summary>
    [JsonPropertyName("closedPositionsChange")] public int? ClosedPositionsChange { get; init; }

    /// <summary>How many filers trimmed a position. A count.</summary>
    [JsonPropertyName("reducedPositions")] public int? ReducedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastReducedPositions")] public int? LastReducedPositions { get; init; }

    /// <summary>The change in that count — <c>−165</c> on the captured row.</summary>
    [JsonPropertyName("reducedPositionsChange")] public int? ReducedPositionsChange { get; init; }

    /// <summary>Call contracts reported across all filers — 188,086,543 on the captured row.
    ///
    /// <para><b>A count that is deliberately <see cref="decimal"/> anyway</b>, and the exception to this
    /// record's own rule. <see cref="int"/> holds the largest value measured with room to spare, but this is a
    /// share-adjacent quantity sitting in a block of six where every sibling is <c>decimal?</c>, and splitting
    /// the block would read as an accident rather than a decision. The genuine counts on this record are the
    /// investor and position tallies, which count filers rather than instruments.</para></summary>
    [JsonPropertyName("totalCalls")] public decimal? TotalCalls { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalCalls")] public decimal? LastTotalCalls { get; init; }

    /// <summary>The change in call contracts.</summary>
    [JsonPropertyName("totalCallsChange")] public decimal? TotalCallsChange { get; init; }

    /// <summary>Put contracts reported across all filers. <see cref="decimal"/> for the reason on
    /// <see cref="TotalCalls"/>.</summary>
    [JsonPropertyName("totalPuts")] public decimal? TotalPuts { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalPuts")] public decimal? LastTotalPuts { get; init; }

    /// <summary>The change in put contracts.</summary>
    [JsonPropertyName("totalPutsChange")] public decimal? TotalPutsChange { get; init; }

    /// <summary><see cref="TotalPuts"/> over <see cref="TotalCalls"/>.</summary>
    [JsonPropertyName("putCallRatio")] public decimal? PutCallRatio { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastPutCallRatio")] public decimal? LastPutCallRatio { get; init; }

    /// <summary>The change in the ratio. <b>Expressed as a percentage change, not in ratio points</b> —
    /// <c>3.0605</c> on the captured row against a ratio that moved from <c>0.8082</c> to <c>0.8388</c>, a
    /// difference of <c>0.0306</c>. The two other <c>*Change</c> conventions in this record are plain
    /// differences; this one is not, and subtracting the two ratios will not reproduce it.</summary>
    [JsonPropertyName("putCallRatioChange")] public decimal? PutCallRatioChange { get; init; }
}
