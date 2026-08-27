using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One named officer, from <c>stable/key-executives</c>.
///
/// <para>Measured 2026-08-27 across 203 rows and 18 symbols (<c>AAPL, SHOP, JPM, F, KO, TSM, GE, IBM, WFC, C,
/// BA, MMM, PFE, MRK, DIS, NKE, CSCO, ORCL</c>). Three of the eight fields are frequently null and two carry
/// nothing at all on this plan on this date — each is documented below with the count that says so, because a
/// field that is constant today is a measurement rather than a schema fact.</para>
///
/// <para><c>SPY</c> answers <c>[]</c>: an ETF has no executives.</para></summary>
public sealed record KeyExecutive
{
    /// <summary>The officer's title, as the filing spells it. Long and unnormalised — <c>"VP of Research,
    /// Development, Pathfinding &amp; Corporate Research and CTO"</c>.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>The officer's name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Reported pay, in <see cref="CurrencyPay"/>.
    ///
    /// <para><b>Not comparable across rows without <see cref="CurrencyPay"/>.</b> Null on 32 of the first 64
    /// rows measured, so an absent value is the common case rather than the exception.</para></summary>
    [JsonPropertyName("pay")] public decimal? Pay { get; init; }

    /// <summary>The currency <see cref="Pay"/> is denominated in. Present on every row measured, and
    /// <b>not always <c>"USD"</c></b> — <c>TSM</c> reports <c>"TWD"</c>. Summing or ranking
    /// <see cref="Pay"/> across issuers without reading this compares different units.</summary>
    [JsonPropertyName("currencyPay")] public string? CurrencyPay { get; init; }

    /// <summary><c>"male"</c>, <c>"female"</c>, or null — null on 9 of the first 64 rows measured. An
    /// independent client types this non-optional from FMP's documented example; the measurement says
    /// otherwise.</summary>
    [JsonPropertyName("gender")] public string? Gender { get; init; }

    /// <summary>Year of birth. Null on 24 of the first 64 rows measured.</summary>
    [JsonPropertyName("yearBorn")] public int? YearBorn { get; init; }

    /// <summary>When the officer took the title. <b>Null on all 203 rows measured on 2026-08-27</b>, so nothing
    /// can currently be built on it.
    ///
    /// <para><b>Deliberately <see langword="string"/> rather than a date type, and the type is provisional.</b>
    /// Not one populated value has ever been observed — null across all 203 rows here, and null in the single
    /// documented example an independent client typed from, which guessed <c>int</c> and left a comment saying
    /// the guess was unverified. With no populated value from either source there is no measured shape to infer
    /// a format from, and a wrong guess is not a wrong label: typed <c>Instant?</c>, <c>LocalDate?</c> or
    /// <c>int?</c>, the day FMP starts sending something else is the day this endpoint starts throwing for every
    /// caller. A <see langword="string"/> cannot throw. <b>Re-measure before narrowing this.</b></para></summary>
    [JsonPropertyName("titleSince")] public string? TitleSince { get; init; }

    /// <summary>Whether the officer is current. <b><see langword="true"/> on all 203 rows measured on
    /// 2026-08-27</b> — never <see langword="false"/>, never null.
    ///
    /// <para>Kept rather than dropped, and documented rather than trusted: FMP documents the field, and "always
    /// true so far" is a measurement that can change, not a guarantee. Filtering on it today removes nothing;
    /// assuming it will always be true is the mistake this note exists to prevent.</para></summary>
    [JsonPropertyName("active")] public bool? Active { get; init; }
}
