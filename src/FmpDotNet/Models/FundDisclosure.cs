using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One holding line from a fund's SEC Form N-PORT filing, from <c>stable/funds/disclosure</c> —
/// twenty-three keys, the widest shape in the ETF and mutual-fund group.
///
/// <para><b>This is the fund's own filed portfolio, not FMP's cached view of it.</b> Where
/// <see cref="EtfHolding"/> answers "what does FMP think this ETF holds right now", this answers "what did the
/// fund tell the SEC it held on this date". <see cref="Date"/> is a real as-of date;
/// <see cref="EtfHolding.UpdatedAt"/> is not.</para>
///
/// <para><b>The only path in this SDK with a snake_case key.</b> <c>cur_cd</c> sits between <c>units</c> and
/// <c>valUsd</c>, both camelCase, in the same object — see <see cref="CurrencyCode"/>.</para>
///
/// <para><b>No ordering was found</b> in the responses measured 2026-08-30, and there is no pagination:
/// <c>limit</c> and <c>page</c> were ignored. A quarter outside the fund's coverage answers <c>[]</c> at
/// HTTP 200 — 2026 Q3 and Q4 both did on 2026-08-30 — as does a <c>quarter</c> of 0 or 5, which is why
/// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c> guards that argument.</para></summary>
public sealed record FundDisclosure
{
    /// <summary>The filing fund's SEC Central Index Key, zero-padded to ten characters — the padding is the
    /// value, so this is a <see cref="string"/>. Measured 2026-08-30 it was constant across every row of all
    /// 27 responses.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The portfolio as-of date the filing reports — the fund's <b>fiscal</b> period end. Measured
    /// 2026-08-30, SPY reports on calendar quarter ends while FXAIX reports on 2026-05-31 and ARKK on
    /// 2026-01-30. See <see cref="FundDisclosureDate"/>, which is how a caller discovers which dates a given
    /// fund has.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When EDGAR accepted the filing.
    ///
    /// <para><b>Read as US Eastern wall clock, not UTC</b> — see
    /// <see cref="NullableEasternInstantJsonConverter"/>. The zone was established by identity rather than
    /// assumed: twenty NPORT-P filings across two CIKs and ten quarters were looked up a second time through
    /// <c>stable/sec-filings-search/cik</c>, whose <c>acceptedDate</c> was measured against EDGAR on
    /// 2026-08-26. Measured 2026-08-30, <b>12 of 19 matched to the second</b> (10 of 10 for the SPY trust) and
    /// the largest residual across all nineteen was <b>90 seconds</b> — against 3,600 for an hour. The seven
    /// misses are same-day sibling filings, one per fund series, minutes apart.</para>
    ///
    /// <para><b>The identical wire shape on <see cref="EtfHolding.UpdatedAt"/> is UTC.</b> Two paths in this
    /// group send <c>"uuuu-MM-dd HH:mm:ss"</c> and they mean different zones. Swapping the converters costs
    /// four or five hours and nothing throws.</para>
    ///
    /// <para>Constant across a response, because a response is one filing: measured 2026-08-30, each of the
    /// twenty responses sampled carried exactly one distinct value.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The held security's ticker, or <see langword="null"/>.
    ///
    /// <para><b>Nullable because FMP actually sent JSON <see langword="null"/></b> — 176 of 11,522 rows
    /// measured 2026-08-30, not merely because the deserialiser cannot promise a key. Warrants, unlisted debt
    /// and foreign lines have no ticker. Use <see cref="Name"/> or <see cref="Cusip"/> instead.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name. <c>"N/A"</c> on 120 of 11,522 rows measured 2026-08-30, mapped to
    /// <see langword="null"/> by <see cref="SentinelStringJsonConverter"/>.</summary>
    [JsonPropertyName("name")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Name { get; init; }

    /// <summary>The issuer's Legal Entity Identifier, or <see langword="null"/>. <c>"N/A"</c> on 495 of 11,522
    /// rows measured 2026-08-30 — the most common sentinel on this path.</summary>
    [JsonPropertyName("lei")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Lei { get; init; }

    /// <summary>The security's title as filed — often the same text as <see cref="Name"/>, but not always:
    /// a futures line measured 2026-08-30 read <c>"S and P500 EMINI FUT MAR26 ESH6"</c> against a
    /// <see cref="Name"/> of <c>"CHICAGO MERCANTILE EXCH INC"</c>. Never measured carrying a sentinel, so it
    /// takes no converter.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>The security's CUSIP, or <see langword="null"/>. <c>"N/A"</c> on 202 of 11,522 rows measured
    /// 2026-08-30. Note that <c>"000000000"</c> also appears and is <b>not</b> treated as absence — it is a
    /// real filed value, and this SDK does not invent sentinels FMP did not send.</summary>
    [JsonPropertyName("cusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Cusip { get; init; }

    /// <summary>The security's ISIN, or <see langword="null"/>. <c>""</c> on 149 of 11,522 rows measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("isin")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Isin { get; init; }

    /// <summary>The position size, in the unit named by <see cref="Units"/>. <b>Signed and fractional</b> —
    /// measured values include <c>0.668</c>, so an integer type is wrong here for the same reason it is wrong
    /// on <see cref="EtfHolding.SharesNumber"/>.</summary>
    [JsonPropertyName("balance")] public decimal? Balance { get; init; }

    /// <summary>What <see cref="Balance"/> counts, as an SEC N-PORT code — measured 2026-08-30 over a
    /// 3,861-row sample: <c>NS</c> (number of shares) ×3,830, <c>NC</c> (contracts) ×29, <c>PA</c> (principal
    /// amount) ×2.
    ///
    /// <para>A free string rather than an enum: three values in one sample is a sample, not a vocabulary, and
    /// the SEC's list is longer than what was observed.</para></summary>
    [JsonPropertyName("units")] public string? Units { get; init; }

    /// <summary>The currency the position is denominated in.
    ///
    /// <para><b>The wire key is <c>cur_cd</c> — the only snake_case key in this SDK</b> — and the property
    /// takes a readable name while the attribute carries the wire verbatim, the same trade
    /// <see cref="MarketMover.ChangePercentage"/> makes. <b>Do not "fix" the attribute.</b></para>
    ///
    /// <para><b>It is not always three letters.</b> Measured 2026-08-30, 29 of 3,861 rows sent
    /// <c>"USDUSD"</c> — a doubled code, all of them equity-futures lines (<c>units NC</c>,
    /// <c>assetCat DE</c>). This field must therefore never be given a strict three-letter currency
    /// type.</para></summary>
    [JsonPropertyName("cur_cd")] public string? CurrencyCode { get; init; }

    /// <summary>The position's value in US dollars. Wire key <c>valUsd</c>. Measured range 2026-08-30:
    /// −41,402,229.68 to 125,580,304,518.46 — 14 significant digits, and not exactly representable in
    /// binary64 (the nearest <see cref="double"/> is 125,580,304,518.4600067138671875), which is why this is
    /// <see cref="decimal"/>.</summary>
    [JsonPropertyName("valUsd")] public decimal? ValueUsd { get; init; }

    /// <summary>The position's share of the fund, as a percentage. Wire key <c>pctVal</c>.
    ///
    /// <para><b>Not bounded by 0 and 100.</b> Measured range 2026-08-30: −0.0032285713047007715 to
    /// <b>10.880031435864327</b>. Not range-checked, and must not be.</para></summary>
    [JsonPropertyName("pctVal")] public decimal? PercentValue { get; init; }

    /// <summary>The direction of the position — <c>"Long"</c> ×3,831 and <c>"N/A"</c> ×30 over the 3,861-row
    /// sample measured 2026-08-30, the <c>N/A</c> rows all being futures lines. The sentinel becomes
    /// <see langword="null"/>; no short position appeared in the sample, so <c>"Short"</c> is
    /// unmeasured.</summary>
    [JsonPropertyName("payoffProfile")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? PayoffProfile { get; init; }

    /// <summary>The SEC N-PORT asset category code. Wire key <c>assetCat</c>. Measured 2026-08-30 over a
    /// 3,861-row sample: <c>EC</c> ×3,818, <c>DE</c> ×30, <c>STIV</c> ×10, <c>DBT</c> ×2, <c>EP</c> ×1.
    /// Five values in one sample is a sample and not a vocabulary, so this is a free string rather than an
    /// enum, and the values above are recorded as observations.</summary>
    [JsonPropertyName("assetCat")] public string? AssetCategory { get; init; }

    /// <summary>The SEC N-PORT issuer category code. Wire key <c>issuerCat</c>. Measured 2026-08-30 over the
    /// same sample: <c>CORP</c> ×3,736, <c>OTHER</c> ×115, <c>RF</c> ×6, <c>UST</c> ×2, <c>PF</c> ×2. A free
    /// string, for the reason on <see cref="AssetCategory"/>.</summary>
    [JsonPropertyName("issuerCat")] public string? IssuerCategory { get; init; }

    /// <summary>The ISO-2 country the investment is attributed to. Wire key <c>invCountry</c>. Seventeen
    /// distinct codes plus <c>"N/A"</c> in the sample measured 2026-08-30; the sentinel becomes
    /// <see langword="null"/>.</summary>
    [JsonPropertyName("invCountry")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? InvestmentCountry { get; init; }

    /// <summary>Whether the security is restricted. Wire key <c>isRestrictedSec</c>, and the wire value is the
    /// <b>string</b> <c>"N"</c> or <c>"Y"</c> — see <see cref="YesNoBooleanJsonConverter"/>.
    ///
    /// <para><b>Its <c>Y</c> form is unmeasured</b>: <c>N</c> on all 3,861 rows sampled 2026-08-30. The
    /// converter is written so that an unexpected value nulls this one field rather than the row.</para></summary>
    [JsonPropertyName("isRestrictedSec")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsRestrictedSecurity { get; init; }

    /// <summary>The ASC 820 fair-value hierarchy level. Wire key <c>fairValLevel</c>.
    ///
    /// <para><b>A quoted integer that stays a <see cref="string"/>.</b> Measured 2026-08-30: <c>"1"</c>
    /// ×3,829, <c>"2"</c> ×28, <c>"3"</c> ×4, always quoted. It is a <b>code, not a quantity</b> — nothing a
    /// caller does with a fair-value level is arithmetic — so parsing it to <see cref="int"/> would invent a
    /// numeric identity the source does not have and gain nothing.</para>
    ///
    /// <para>No sentinel was ever measured on this field, so unlike its numeric-string cousin
    /// <c>FundShareClass.EntityOrgType</c> it carries no converter.</para></summary>
    [JsonPropertyName("fairValLevel")] public string? FairValueLevel { get; init; }

    /// <summary>Whether the position is cash collateral for a loaned security. <c>N</c> ×3,855, <c>Y</c> ×6
    /// over the sample measured 2026-08-30 — one of the two <c>is*</c> fields whose <c>Y</c> form was actually
    /// observed.</summary>
    [JsonPropertyName("isCashCollateral")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsCashCollateral { get; init; }

    /// <summary>Whether the position is non-cash collateral. <b>Its <c>Y</c> form is unmeasured</b>: <c>N</c>
    /// on all 3,861 rows sampled 2026-08-30.</summary>
    [JsonPropertyName("isNonCashCollateral")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsNonCashCollateral { get; init; }

    /// <summary>Whether the security is on loan from the fund. <c>N</c> ×3,605, <c>Y</c> ×256 over the sample
    /// measured 2026-08-30 — the most balanced of the four.</summary>
    [JsonPropertyName("isLoanByFund")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsLoanByFund { get; init; }
}
