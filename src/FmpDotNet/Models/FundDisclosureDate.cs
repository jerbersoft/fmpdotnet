using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One reporting period a fund has filed, from <c>stable/funds/disclosure-dates</c> — and the
/// <c>year</c>/<c>quarter</c> pair that selects it.
///
/// <para><b>This is the index for <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</b> That method takes a
/// year and a quarter, and answers <c>[]</c> at HTTP 200 for a period the fund never filed. This path is how a
/// caller finds out which periods exist, and <see cref="Year"/> and <see cref="Quarter"/> are the arguments to
/// pass, ready-made.</para>
///
/// <para><b><see cref="Date"/> is a FISCAL period end; <see cref="Year"/> and <see cref="Quarter"/> are
/// CALENDAR.</b> Measured 2026-08-30, SPY files on calendar quarter ends but FXAIX files on 2026-05-31 and
/// 2025-11-30, and ARKK on 2026-01-30 — so FXAIX's May date is reported as Q2. Verified over 80 rows across
/// three funds: <c>Year == Date.Year</c> and <c>Quarter == (Date.Month - 1) / 3 + 1</c> with <b>zero
/// mismatches</b>.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>newest first</b> across 127 rows. Coverage reaches back to
/// 2019-09-30 for SPY, 2019-11-30 for FXAIX and 2020-04-30 for ARKK — it differs per fund, which is why
/// nothing in this SDK bounds the <c>year</c> argument.</para></summary>
public sealed record FundDisclosureDate
{
    /// <summary>The fund's fiscal period end. Nullable because the deserialiser cannot promise a key is
    /// present, not because any measured row omitted it — no row was missing a key across all 127 measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The calendar year of <see cref="Date"/>. Pass it to
    /// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter of <see cref="Date"/> — 1 to 4. <b>Not the fund's own fiscal quarter
    /// number</b>: FXAIX's 2026-05-31 period end is reported here as <c>2</c>. Pass it to
    /// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }
}
