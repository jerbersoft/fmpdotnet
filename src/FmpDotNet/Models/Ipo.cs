using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One scheduled or priced offering from <c>stable/ipos-calendar</c>.
///
/// <para><b>This is mostly a scheduling feed, not a pricing one.</b> Measured across 450 rows on 2026-08-28,
/// <see cref="Shares"/> was null on 349, <see cref="PriceRange"/> on 441 and <see cref="MarketCap"/> on 354 —
/// and the three are absent independently, so a row can carry a share count and a market cap with no price
/// range beside them. <see cref="Actions"/> was <c>Expected</c> on 359 rows and <c>Priced</c> on 91, and even
/// among the 102 rows with any numeric populated, 11 were still <c>Expected</c>. Gate on the field you are
/// about to read, not on the label.</para></summary>
public sealed record IpoCalendarEntry
{
    /// <summary>The ticker the offering will trade under. Warrants and units appear as their own rows with
    /// their own tickers — <c>XLABW</c> beside <c>XLAB</c>, <c>IPHXU</c> for a unit — so one company can occupy
    /// several rows on one date.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The offering date, and the date this path selects on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary><b>The same value as <see cref="Date"/>, in a different format, and it carries nothing.</b>
    ///
    /// <para>Measured across all 450 rows on 2026-08-28: the date part of <c>daa</c> equalled <c>date</c> in
    /// <b>450 of 450</b>, and the time part took exactly <b>one</b> distinct value across the whole response —
    /// <c>T04:00:00.000Z</c>, which is midnight Eastern. So this is <see cref="Date"/> at midnight in EDT,
    /// expressed as UTC, under a name that explains neither.</para>
    ///
    /// <para>Kept as the raw string rather than parsed to a date or an instant, deliberately. Parsing it would
    /// manufacture a second temporal property that cannot disagree with <see cref="Date"/> and would invite a
    /// caller to think it might mean something else. <b>Use <see cref="Date"/>.</b></para></summary>
    [JsonPropertyName("daa")] public string? Daa { get; init; }

    /// <summary>The issuer's name as FMP writes it, including the instrument — <c>"… Warrant"</c>,
    /// <c>"… Class A Common Stock"</c>, <c>"… Unit"</c>.</summary>
    [JsonPropertyName("company")] public string? Company { get; init; }

    /// <summary>Where it lists. Two values across 450 rows measured 2026-08-28, <c>NASDAQ</c> and <c>NYSE</c> —
    /// a string rather than an enum, because two values from one response is a sample, not a domain.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>FMP's status label: <c>Expected</c> on 359 of 450 rows and <c>Priced</c> on 91, measured
    /// 2026-08-28. Note it does not partition the numeric fields — 11 of the 102 rows carrying a populated
    /// number were still labelled <c>Expected</c>.</summary>
    [JsonPropertyName("actions")] public string? Actions { get; init; }

    /// <summary>Shares offered, or <see langword="null"/> — which is the common case, 349 of 450.
    ///
    /// <para><see langword="decimal"/> rather than an integer type, matching
    /// <see cref="SharesFloat.OutstandingShares"/>. The measured maximum was 555,555,555, which does fit an
    /// <see cref="int"/>; the type follows the SDK's existing convention for share counts rather than the
    /// narrowest thing today's sample allows.</para></summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The offering price or price band, <b>as a formatted string</b>, or <see langword="null"/> —
    /// which is overwhelmingly the common case, 441 of 450.
    ///
    /// <para><b>Not a number, and this was measured rather than assumed.</b> The nine populated values on
    /// 2026-08-28 were all strings, in two shapes: six ranges (<c>"5.00 - 7.00"</c>, <c>"15 - 17"</c>,
    /// <c>"11.25 - 13.25"</c>) and three single prices (<c>"10.00"</c>). Typed <see langword="decimal"/> this
    /// property would read <b>null on all 450 rows</b> — null where FMP sent null, and null where FMP sent a
    /// price — with nothing in the data to tell the two apart. It is the same kind of field as
    /// <see cref="SecProfile.FiftyTwoWeekRange"/>.</para>
    ///
    /// <para>The SDK does not split or parse it: both shapes are real, the separator is not guaranteed, and a
    /// caller who wants numbers can see which shape they have.</para></summary>
    [JsonPropertyName("priceRange")] public string? PriceRange { get; init; }

    /// <summary>Expected market capitalisation at the offering, or <see langword="null"/> — 354 of 450.
    ///
    /// <para><b><see langword="decimal"/> and never a narrower type.</b> Measured 2026-08-28, values ran to
    /// <b>74,999,999,925</b> — about thirty-five times <see cref="int"/>'s ceiling of 2,147,483,647. An
    /// <see cref="int"/> property does not read an out-of-range value as null: <c>System.Text.Json</c> throws,
    /// and <c>FmpTransport</c> does not wrap <c>DeserializeAsync</c>, so a single such row would cost the caller
    /// the whole response. Same rule and same reason as
    /// <see cref="MarketCapitalization.MarketCap"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}

/// <summary>One EDGAR filing marking a registration as effective, from <c>stable/ipos-disclosure</c>.
///
/// <para><b>Every field was populated on every row measured</b> — 8,838 rows on 2026-08-28 — so this record has
/// no measured absent value, which is unusual in this SDK and worth stating rather than leaving to be
/// discovered.</para>
///
/// <para><b>One filing appears once per share class it covers.</b> All five rows of the captured page share a
/// CIK, a form and a URL under five different tickers: a single <c>CERT</c> covering five classes of one fund.
/// A caller deduplicating on <see cref="Url"/> collapses five real rows into one.</para>
///
/// <para><b>The three dates are plain dates, not timestamps.</b> All three were 10 characters on all 8,838 rows
/// — read the note on <see cref="AcceptedDate"/> before reaching for a converter.</para></summary>
public sealed record IpoDisclosure
{
    /// <summary>The ticker the filing covers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When the filing was made.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, <b>as a plain date</b>.
    ///
    /// <para><b>This is not the same kind of value as <see cref="SecFiling.AcceptedDate"/>, despite the
    /// identical field name.</b> That one is a 19-character <c>uuuu-MM-dd HH:mm:ss</c> EDGAR wall clock in US
    /// Eastern, read through <see cref="NullableEasternInstantJsonConverter"/>. This one was <b>10 characters on
    /// all 8,838 rows</b> measured 2026-08-28 — there is no time of day in it at all. Pointing the Eastern
    /// converter at this field would answer <see langword="null"/> for every row and never throw, which is the
    /// silent kind of wrong.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>When the registration became effective.</summary>
    [JsonPropertyName("effectivenessDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? EffectivenessDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters — <c>"0001040674"</c>. A
    /// string and never a number: parsing it loses the padding EDGAR uses.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The EDGAR form type — <c>CERT</c> on the captured page.</summary>
    [JsonPropertyName("form")] public string? Form { get; init; }

    /// <summary>Direct link to the filing on <c>sec.gov</c>. Shared across every row of the same filing.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>One prospectus filing and the offering economics on it, from <c>stable/ipos-prospectus</c>.
///
/// <para><b>Every field was populated on every one of the 165 rows measured</b> on 2026-08-28.</para>
///
/// <para><b>The money fields are reported exactly as FMP sent them, including where that is absurd.</b> One
/// captured row carries a price of 300 per share against a total of 273; another repeats 10,709,298 across
/// three unrelated fields. The SDK does not correct, flag or drop them — a plausibility rule here would be the
/// SDK inventing a fact, and the values are what a caller needs to see in order to judge them.</para>
///
/// <para>Every date here is a plain 10-character date, as on <see cref="IpoDisclosure"/>, and
/// <see cref="AcceptedDate"/> can fall a day <i>before</i> <see cref="FilingDate"/>.</para></summary>
public sealed record IpoProspectus
{
    /// <summary>The ticker the prospectus covers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When EDGAR accepted the filing, as a plain date. See the note on
    /// <see cref="IpoDisclosure.AcceptedDate"/>: this is not the Eastern timestamp the SEC filing paths carry,
    /// and it can precede <see cref="FilingDate"/> by a day.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>When the prospectus was filed.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The issuer's original IPO date, which can be decades before the filing — <c>1989-03-02</c> and
    /// <c>2000-06-22</c> both appear against 2026 filings. This is a follow-on prospectus feed as much as a
    /// new-issue one.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>Offering price per share to the public.
    ///
    /// <para><b>The most fractional of the four prospectus money fields measured:</b> 51 of 165 rows on
    /// 2026-08-28 — nearly a third — against 18, 13 and 11 of 165 for the three totals beside it. Measured 0.12
    /// to 12,183,292 across those same 165 rows. The range fits comfortably within <see cref="int"/>'s ceiling,
    /// so width is not what earns this <see langword="decimal"/>?; the fractional rate does.</para></summary>
    [JsonPropertyName("pricePublicPerShare")] public decimal? PricePublicPerShare { get; init; }

    /// <summary>Total offering value to the public.
    ///
    /// <para><b><see langword="decimal"/> and never a narrower type:</b> measured to <b>74,999,999,925</b>
    /// across 165 rows on 2026-08-28, about thirty-five times <see cref="int"/>'s ceiling, and fractional on 13
    /// of those rows. An <see cref="int"/> here throws rather than reading null, costing the whole
    /// response.</para></summary>
    [JsonPropertyName("pricePublicTotal")] public decimal? PricePublicTotal { get; init; }

    /// <summary>Underwriting discounts and commissions per share.
    ///
    /// <para><b>Not included in the 2026-08-28 magnitude sweep</b> — no range or fractional rate was measured
    /// for this field specifically. It takes <see langword="decimal"/>? alongside its five siblings on this
    /// record because it is per-share money on the same rows and shares their shape, not because its own
    /// magnitude was checked.</para></summary>
    [JsonPropertyName("discountsAndCommissionsPerShare")]
    public decimal? DiscountsAndCommissionsPerShare { get; init; }

    /// <summary>Total underwriting discounts and commissions.
    ///
    /// <para><b>Measured 0 to 500,000,000</b> across 165 rows on 2026-08-28, with 11 of 165 fractional. Unlike
    /// <see cref="PricePublicTotal"/> and <see cref="ProceedsBeforeExpensesTotal"/> beside it, 500,000,000 fits
    /// comfortably within <see cref="int"/>'s ceiling of 2,147,483,647 — this still takes
    /// <see langword="decimal"/>? because it is the same kind of quantity as its siblings and because 11 of
    /// those 165 rows are fractional. The same shape as <see cref="IpoCalendarEntry.Shares"/>, which also fits
    /// and also takes <see langword="decimal"/>?; the opposite of <see cref="StockSplit.Numerator"/>, which
    /// fits and stays <see langword="int"/>?.</para></summary>
    [JsonPropertyName("discountsAndCommissionsTotal")]
    public decimal? DiscountsAndCommissionsTotal { get; init; }

    /// <summary>Proceeds to the issuer per share, before expenses.
    ///
    /// <para><b>Not included in the 2026-08-28 magnitude sweep</b> — no range or fractional rate was measured
    /// for this field specifically. It takes <see langword="decimal"/>? alongside its five siblings on this
    /// record because it is per-share money on the same rows and shares their shape, not because its own
    /// magnitude was checked.</para></summary>
    [JsonPropertyName("proceedsBeforeExpensesPerShare")]
    public decimal? ProceedsBeforeExpensesPerShare { get; init; }

    /// <summary>Total proceeds to the issuer before expenses. Measured 0 to <b>74,499,999,925</b> across 165
    /// rows on 2026-08-28, with 18 of 165 fractional — see <see cref="PricePublicTotal"/> for why this is
    /// <see langword="decimal"/>.</summary>
    [JsonPropertyName("proceedsBeforeExpensesTotal")]
    public decimal? ProceedsBeforeExpensesTotal { get; init; }

    /// <summary>The EDGAR form type — <c>424B4</c> and <c>S-1/A</c> on the captured page.</summary>
    [JsonPropertyName("form")] public string? Form { get; init; }

    /// <summary>Direct link to the filing on <c>sec.gov</c>.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
