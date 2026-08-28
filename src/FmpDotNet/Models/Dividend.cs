using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One dividend event. Serves both <c>stable/dividends</c>, which answers one symbol's whole history,
/// and <c>stable/dividends-calendar</c>, which answers every symbol in a date range.
///
/// <para><b>Those two paths return the same nine fields in the same order</b>, compared on 2026-08-28, which is
/// why one record serves both — the same reasoning as <see cref="EmployeeCount"/> and
/// <see cref="SecFiling"/>.</para>
///
/// <para><b>Four dates, in no guaranteed order relative to each other.</b> <see cref="Date"/> is the ex-dividend
/// date; the other three are the record, payment and declaration dates, and the captured calendar rows include
/// a record date the day <i>before</i> its ex-date and a payment date three weeks after. Nothing here sorts or
/// cross-validates them.</para>
///
/// <para><b><see cref="DeclarationDate"/> is very often absent, and FMP spells absent as a blank string rather
/// than as null.</b> Measured 2026-08-28: blank on 15 of AAPL's 92 rows, on 325 of 622 calendar rows for a
/// two-day window, and on 2,232 of 4,000 for a wider one. <see cref="NullableLocalDateJsonConverter"/> reads
/// <c>""</c> as null rather than throwing, which is what keeps those rows from costing the whole
/// response.</para></summary>
public sealed record Dividend
{
    /// <summary>Ticker as FMP spells it. The calendar is global, so the captured rows span <c>001231.SZ</c>,
    /// <c>0018.HK</c> and <c>0237.HK</c> — suffixed exchange codes are the norm there rather than the
    /// exception, and a caller filtering to US listings must do so explicitly.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The ex-dividend date — the day the share trades without the entitlement, and the date both
    /// paths select on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The record date. Blank on 1 of 622 calendar rows measured 2026-08-28, which reads as
    /// null.</summary>
    [JsonPropertyName("recordDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? RecordDate { get; init; }

    /// <summary>The payment date. Blank on 4 of 622 calendar rows measured 2026-08-28.</summary>
    [JsonPropertyName("paymentDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? PaymentDate { get; init; }

    /// <summary>When the dividend was declared, or <see langword="null"/> where FMP has no declaration on file.
    ///
    /// <para><b>Null is the common case on the calendar, not the exception</b> — 325 of 622 rows measured
    /// 2026-08-28, and 2,232 of 4,000 over a wider window. A pipeline that treats a null here as a data fault
    /// will treat over half the calendar as faulty.</para></summary>
    [JsonPropertyName("declarationDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DeclarationDate { get; init; }

    /// <summary>The dividend adjusted for subsequent splits, in the issuer's own currency.</summary>
    [JsonPropertyName("adjDividend")] public decimal? AdjDividend { get; init; }

    /// <summary>The dividend as declared, in the issuer's own currency.
    ///
    /// <para><b>Named <c>DividendAmount</c> rather than <c>Dividend</c> because C# forbids a member sharing its
    /// enclosing type's name</b> (CS0542) — the same rename, for the same reason, as
    /// <see cref="EmployeeCount.Employees"/>. The wire name is unchanged.</para>
    ///
    /// <para><see langword="decimal"/> and not a narrower type: the field arrives as a JSON integer on some rows
    /// and a float on others (32 and 590 of 622 measured), and ranged from 0.001 to 3,383.85 in a single two-day
    /// window across global listings.</para></summary>
    [JsonPropertyName("dividend")] public decimal? DividendAmount { get; init; }

    /// <summary>FMP's computed yield for this event, as a percentage. Measured from 0.018 to 245.16 in one
    /// two-day calendar window — the high end being small-denomination listings rather than an error, and a
    /// reason not to treat this as a sanity-checked figure.</summary>
    [JsonPropertyName("yield")] public decimal? Yield { get; init; }

    /// <summary>How often the issuer pays, as FMP's own label.
    ///
    /// <para><b>A string and not an enum, because the observed set depends on which path answered.</b> Measured
    /// 2026-08-28: <c>stable/dividends</c> for AAPL shows two values (<c>Quarterly</c> ×91,
    /// <c>Irregular</c> ×1); the calendar shows eight (<c>Quarterly</c>, <c>Semi-Annual</c>, <c>Monthly</c>,
    /// <c>Annual</c>, <c>Weekly</c>, <c>Irregular</c>, <c>Special</c>, <c>Bi-Weekly</c>). Either sample would
    /// make an enum that the other contradicts, and an unlisted value would then become a deserialisation
    /// failure instead of a string a caller can read.</para></summary>
    [JsonPropertyName("frequency")] public string? Frequency { get; init; }
}
