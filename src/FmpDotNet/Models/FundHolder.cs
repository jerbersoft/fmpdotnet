using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One institution's reported position in a security, from
/// <c>stable/funds/disclosure-holders-latest</c>.
///
/// <para><b>This is the reverse of <see cref="FundDisclosure"/>.</b> That answers "what does this fund hold";
/// this answers "which funds hold this security". The argument is the <b>held</b> symbol, and it need not be a
/// fund — measured 2026-08-30, <c>symbol=AAPL</c> answered 3,209 rows.</para>
///
/// <para><b>"Latest" is per holder, not per response.</b> One response mixes reporting dates by <b>years</b>:
/// measured 2026-08-30, SPY's 220 rows carried <b>19 distinct dates spanning 2019-09-30 to 2026-06-30</b> and
/// AAPL's 3,209 rows carried <b>66</b>. Four recent dates dominate both, but 18 of SPY's rows and 292 of
/// AAPL's report a date before 2026 at all — a holder that stopped filing in 2019 is still here, with its
/// 2019 position. <b>Rows in one response are not comparable as of one date</b>; read
/// <see cref="DateReported"/> per row before summing or ranking anything.</para>
///
/// <para><b><see cref="SecurityCusip"/> is not constant per response either.</b> AAPL's mixes the common stock
/// <c>037833100</c> with the bonds <c>037833EF3</c> and <c>037833DZ0</c>, and SPY's mixes <c>78462F103</c>
/// with <c>000000000</c> and synthetic identifiers. The path answers "funds holding <b>any security of this
/// issuer</b>".</para>
///
/// <para>No ordering was found, and there is no pagination — <c>limit</c> and <c>page</c> were ignored,
/// <c>symbol=AAPL</c> returning 3,209 rows and 701,175 bytes with and without them.</para></summary>
public sealed record FundHolder
{
    /// <summary>The holding institution's SEC Central Index Key, zero-padded to ten characters — the padding
    /// is the value, so this is a <see cref="string"/>. Nullable because the deserialiser cannot promise a key
    /// is present; no measured row omitted it.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The holding institution's name, or <see langword="null"/>. <c>""</c> on 16 of 3,979 rows
    /// measured 2026-08-30, mapped by <see cref="SentinelStringJsonConverter"/>. <see cref="Cik"/> was present
    /// on those rows, so an unnamed holder is still identifiable.</summary>
    [JsonPropertyName("holder")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Holder { get; init; }

    /// <summary>The CUSIP of the specific security held. <c>"N/A"</c> on 3 of 3,979 rows measured 2026-08-30.
    /// <b>Not constant across a response</b> — see the record's own summary.</summary>
    [JsonPropertyName("securityCusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? SecurityCusip { get; init; }

    /// <summary>Shares held. <b>Signed and fractional</b>: measured range 2026-08-30 was −990 to
    /// 1,016,998,069, with values like <c>122518791.23</c> and <c>3049046.052</c>.</summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The date this holder's position was reported as of. <b>Read it per row</b> — see the record's
    /// summary for why a single response is not one as-of date.</summary>
    [JsonPropertyName("dateReported")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateReported { get; init; }

    /// <summary>The change in shares since the holder's previous report. <b>Signed, and frequently zero</b> —
    /// measured 2026-08-30 over AAPL's 3,209 rows: 2,532 zero, 291 positive, 386 negative. A zero here is a
    /// reported no-change, not a missing value.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>The position's share of the <b>holder's</b> portfolio, as a percentage.
    ///
    /// <para><b>Not bounded by 100.</b> Measured range 2026-08-30: 1.2e-07 to <b>264.39824722</b>. Not
    /// range-checked, and must not be.</para></summary>
    [JsonPropertyName("weightPercent")] public decimal? WeightPercent { get; init; }
}
