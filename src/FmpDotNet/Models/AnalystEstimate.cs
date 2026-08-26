using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The sell-side consensus for one future fiscal period. From <c>stable/analyst-estimates</c>.
///
/// <para>Every row is a <b>forecast</b>, not a report. Nothing here happened: the figures are what a panel of
/// analysts expects a period that has not ended yet to look like, and <see cref="Date"/> is the day that period
/// will end. Do not join these against reported statements as though they were the same kind of fact — a
/// consensus row for <c>2030-09-27</c> exists today, and the statement for it will not exist for years.</para>
///
/// <para><b>Rows arrive furthest-future first.</b> Measured 2026-08-26 against AAPL:
/// <c>period=annual&amp;limit=3</c> answered <c>2030-09-27, 2029-09-27, 2028-09-27</c> and
/// <c>period=quarter&amp;limit=3</c> answered <c>2028-09-27, 2028-06-27, 2028-03-27</c>. So a small
/// <c>limit</c> returns the estimates furthest out in time, not the nearest ones — see
/// <see cref="Endpoints.AnalystEndpoints.GetEstimatesAsync"/>, where that trap is documented in full.</para>
///
/// <para><b>The wire orders the EPS trio differently from every other group, and that is upstream's doing rather
/// than a fault here.</b> Measured 2026-08-26, the JSON runs <c>…sgaExpenseAvg, epsAvg, epsHigh, epsLow,
/// numAnalystsRevenue…</c> — Avg/High/Low, where the five money groups all run Low/High/Avg. This record
/// deliberately groups EPS <c>Low, High, Avg</c> like the other five, because a reader scanning the type for
/// "the low estimate" should find it in the same position every time and an inconsistency copied from the wire
/// buys nothing. Property order does not affect <c>System.Text.Json</c> deserialisation in either direction, so
/// the regrouping is cosmetic; a future reader diffing this record against a raw capture will see the EPS trio
/// out of step and should conclude nothing from it. What <i>would</i> matter is a wrong
/// <see cref="JsonPropertyNameAttribute"/>, which reads silently as null rather than failing — the wire names are
/// asserted in both directions by the tests against the shipped captures.</para>
///
/// <para><b>22 fields, not 23.</b> Measured 2026-08-26: both captures — three annual rows and three quarterly —
/// carry exactly these 22 keys in exactly this order on every row, none missing and none extra. The project's
/// own evidence note headlines this endpoint as "23 fields" and then enumerates 22; the enumeration is right and
/// the count was a miscount. Everything below is mapped, so nothing is being dropped. The record carries one
/// property beyond those 22 — <see cref="Period"/> — which is <see cref="JsonIgnoreAttribute"/>d because the
/// endpoint stamps it from the request rather than reading it off the wire.</para>
///
/// <para>Every money figure is <see langword="decimal"/>, not double, matching every other money field in this
/// SDK. Measured 2026-08-26 the values reach 7.4e11 with 12 significant digits — <c>revenueHigh</c> of
/// <c>743323914333</c> on the 2030 annual row — and they are <b>computed means and extremes</b> rather than
/// reported figures, so fractional parts are normal rather than exceptional. Decimal is exact in base 10, which
/// is the base FMP writes them in: <c>12.04031</c> has no exact binary representation, so a double round-trip
/// stops reproducing the number that was sent, and differencing a high against a low — which is the whole point
/// of a low/high band only a few percent wide — puts that error precisely where the caller is looking. Keeping
/// the whole surface decimal also removes the mixed-type surface on which an accidental narrowing conversion
/// hides.</para></summary>
public sealed record AnalystEstimate
{
    /// <summary>Ticker as FMP spells it. Class-share tickers want FMP's hyphenated spelling (<c>BRK-B</c>, not
    /// <c>BRK.B</c>) — a dotted one answers an empty array rather than an error.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Which series this row belongs to. <b>Echoed from the request — FMP does not send it.</b>
    ///
    /// <para>Measured 2026-08-26 against both captures: the wire carries no <c>period</c> and no <c>fiscalYear</c>,
    /// so an annual row and a quarterly row are byte-for-byte indistinguishable in shape. That matters because the
    /// two series <b>collide on <see cref="Date"/></b> — a fiscal year end and its Q4 end are the same day, and
    /// <c>2028-09-27</c> is present in AAPL's annual capture (revenue average 558,901,943,758) and in its
    /// quarterly one (128,079,050,952) with entirely different figures. So <c>(Symbol, Date)</c> is not a key, and
    /// anyone who calls <see cref="Endpoints.AnalystEndpoints.GetEstimatesAsync"/> twice and concatenates the two
    /// lists silently merges a year into a quarter with nothing to say which row is which.</para>
    ///
    /// <para><see cref="Endpoints.AnalystEndpoints.GetEstimatesAsync"/> therefore stamps this from the
    /// <c>period</c> argument it was given, on every row, before handing the list back. The distinction survives a
    /// <c>Concat</c> and a shared storage key of <c>(symbol, period, date)</c> works without the caller
    /// reconstructing the period from which variable the list landed in.</para>
    ///
    /// <para>Deliberately one level stronger than <see cref="Endpoints.StatementEndpoints.GetEnterpriseValuesAsync"/>,
    /// which documents the identical collision and resolves it only by telling the caller to remember what it
    /// asked for. A doc comment is not load-bearing at the point where two lists are concatenated; a property is.
    /// The two endpoints differ because this one was built after the trap had been seen to bite in a real
    /// consumer, not because one of them is wrong about the upstream.</para>
    ///
    /// <para><see cref="JsonIgnoreAttribute"/> because it is not a wire field: the source-generated context must
    /// neither look for it in a payload nor emit it in one. Its default is <see cref="FiscalPeriod.Annual"/>,
    /// which matters only if a row is deserialised outside the endpoint — through the transport directly, say —
    /// where nothing has stamped it and it means "unset" rather than "annual".</para></summary>
    [JsonIgnore] public FiscalPeriod Period { get; init; }

    /// <summary>The <b>end of the fiscal period being forecast</b> — not a publication date, and not a date on
    /// which anything was measured.
    ///
    /// <para>This is the single most misread field on the endpoint. It is a horizon: measured 2026-08-26, AAPL's
    /// annual series ran out to <c>2030-09-27</c>, four years past the capture. Reading it as "when this estimate
    /// was made" inverts the meaning completely, and sorting rows by it sorts by how far into the future they
    /// look. FMP publishes no revision or as-of stamp on this endpoint at all, so there is nothing here that says
    /// when the consensus was struck; a caller who needs that has to stamp the response itself on arrival.</para>
    ///
    /// <para>Plain <c>yyyy-MM-dd</c> with no time component, hence <see cref="LocalDate"/> rather than an
    /// <see cref="Instant"/>. The days are fiscal, not calendar: AAPL's September quarter ends <c>2030-09-27</c>,
    /// and the quarterly series steps <c>2028-03-27, 2028-06-27, 2028-09-27</c>, so these are not month ends and
    /// arithmetic that assumes they are will drift.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    // ---- Revenue ----

    /// <summary>Lowest revenue estimate on the panel for this period.</summary>
    [JsonPropertyName("revenueLow")] public decimal? RevenueLow { get; init; }

    /// <summary>Highest revenue estimate on the panel for this period.</summary>
    [JsonPropertyName("revenueHigh")] public decimal? RevenueHigh { get; init; }

    /// <summary>Consensus (mean) revenue estimate, over the panel counted by <see cref="NumAnalystsRevenue"/> —
    /// a different panel from the one behind <see cref="EpsAvg"/>.
    ///
    /// <para><b>Not monotonic in the horizon.</b> Measured 2026-08-26, AAPL's annual consensus runs
    /// <c>2028: 558,901,943,758</c>, <c>2029: 483,092,000,000</c>, <c>2030: 693,145,000,000</c> — the middle year
    /// is 14% <i>below</i> the year before it and 30% below the year after. That is not a data fault to work
    /// around; it is what averaging a different, shrinking set of analysts at each horizon produces. Code that
    /// assumes a forecast series rises with the horizon, or that derives a growth rate from two adjacent rows,
    /// will read a fabricated contraction here.</para></summary>
    [JsonPropertyName("revenueAvg")] public decimal? RevenueAvg { get; init; }

    // ---- EBITDA ----

    /// <summary>Lowest EBITDA estimate on the panel for this period.</summary>
    [JsonPropertyName("ebitdaLow")] public decimal? EbitdaLow { get; init; }

    /// <summary>Highest EBITDA estimate on the panel for this period.</summary>
    [JsonPropertyName("ebitdaHigh")] public decimal? EbitdaHigh { get; init; }

    /// <summary>Consensus (mean) EBITDA estimate.</summary>
    [JsonPropertyName("ebitdaAvg")] public decimal? EbitdaAvg { get; init; }

    // ---- EBIT ----

    /// <summary>Lowest EBIT (operating income) estimate on the panel for this period.</summary>
    [JsonPropertyName("ebitLow")] public decimal? EbitLow { get; init; }

    /// <summary>Highest EBIT (operating income) estimate on the panel for this period.</summary>
    [JsonPropertyName("ebitHigh")] public decimal? EbitHigh { get; init; }

    /// <summary>Consensus (mean) EBIT (operating income) estimate.</summary>
    [JsonPropertyName("ebitAvg")] public decimal? EbitAvg { get; init; }

    // ---- Net income ----

    /// <summary>Lowest net income estimate on the panel for this period.</summary>
    [JsonPropertyName("netIncomeLow")] public decimal? NetIncomeLow { get; init; }

    /// <summary>Highest net income estimate on the panel for this period.</summary>
    [JsonPropertyName("netIncomeHigh")] public decimal? NetIncomeHigh { get; init; }

    /// <summary>Consensus (mean) net income estimate.
    ///
    /// <para><b>The groups on a row are not mutually consistent, because each is a mean over a different panel.</b>
    /// Measured 2026-08-26 on AAPL's annual series, the implied net margin runs 27.2% for 2028 and 27.7% for 2030
    /// but <b>38.3%</b> for 2029 — because that row averages revenue over 18 analysts and EPS over 9, and the two
    /// sets need not overlap. A ratio built by dividing one group by another on the same row is therefore not a
    /// forecast anybody made; treat each group as its own measurement, and read
    /// <see cref="NumAnalystsRevenue"/> and <see cref="NumAnalystsEps"/> before deriving anything.</para></summary>
    [JsonPropertyName("netIncomeAvg")] public decimal? NetIncomeAvg { get; init; }

    // ---- SG&A expense ----

    /// <summary>Lowest selling, general and administrative expense estimate on the panel for this period.</summary>
    [JsonPropertyName("sgaExpenseLow")] public decimal? SgaExpenseLow { get; init; }

    /// <summary>Highest selling, general and administrative expense estimate on the panel for this period.</summary>
    [JsonPropertyName("sgaExpenseHigh")] public decimal? SgaExpenseHigh { get; init; }

    /// <summary>Consensus (mean) selling, general and administrative expense estimate.</summary>
    [JsonPropertyName("sgaExpenseAvg")] public decimal? SgaExpenseAvg { get; init; }

    // ---- EPS ----
    //
    // Grouped Low/High/Avg to match the five groups above. The wire sends this trio as epsAvg, epsHigh, epsLow —
    // see the note on the type. Property order is not part of deserialisation; the [JsonPropertyName] values are.

    /// <summary>Lowest earnings-per-share estimate on the panel for this period. Measured in currency units per
    /// share, so it is small where the money fields are enormous — <c>9.49342</c> against a revenue of 5.6e11 on
    /// the 2028 annual row.</summary>
    [JsonPropertyName("epsLow")] public decimal? EpsLow { get; init; }

    /// <summary>Highest earnings-per-share estimate on the panel for this period.</summary>
    [JsonPropertyName("epsHigh")] public decimal? EpsHigh { get; init; }

    /// <summary>Consensus (mean) earnings-per-share estimate. Counted by <see cref="NumAnalystsEps"/>, a
    /// different panel from the revenue one.</summary>
    [JsonPropertyName("epsAvg")] public decimal? EpsAvg { get; init; }

    // ---- Panel sizes ----

    /// <summary>How many analysts contributed the revenue estimates on this row.
    ///
    /// <para>A count of people, so <see langword="int"/> rather than decimal — measured 2026-08-26 the values ran
    /// 6 to 22 across the six captured rows. It shrinks as the horizon lengthens (22 analysts on AAPL's 2028
    /// annual row, 11 on its 2030 one), which is the honest reading of how much a distant consensus is worth: a
    /// wide low/high band on a panel of six is not the same claim as a narrow one on a panel of twenty-two.</para>
    ///
    /// <para>It is <b>not</b> the same as <see cref="NumAnalystsEps"/>, and neither is reliably the larger.
    /// Measured 2026-08-26: the annual rows ran 18 revenue against 9 EPS and 22 against 20, while every quarterly
    /// row ran the other way — 6, 7 and 9 revenue against a flat 11 EPS. So neither count can stand in for the
    /// other, and "how many analysts cover this name" is not a single number this endpoint answers.</para></summary>
    [JsonPropertyName("numAnalystsRevenue")] public int? NumAnalystsRevenue { get; init; }

    /// <summary>How many analysts contributed the EPS estimates on this row. A different panel from
    /// <see cref="NumAnalystsRevenue"/> — see that property.</summary>
    [JsonPropertyName("numAnalystsEps")] public int? NumAnalystsEps { get; init; }
}
