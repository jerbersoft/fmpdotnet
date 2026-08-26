using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One scheduled or reported earnings event from <c>stable/earnings-calendar</c>, across every symbol
/// rather than one.
///
/// <para>The first seven properties are the identical seven <see cref="EarningsReport"/> carries, and they are the
/// <b>whole</b> row unless <c>includeReportTimes=true</c> was sent. The remaining five —
/// <see cref="ReportTime"/>, <see cref="PeriodEnding"/>, <see cref="FiscalPeriod"/>, <see cref="FiscalYear"/> and
/// <see cref="Confirmed"/> — are absent from the payload entirely without that flag, so they read as
/// <see langword="null"/>. <b>Null there means "you did not ask", not "FMP does not know".</b> A caller that stores
/// unflagged rows and later reads <see cref="Confirmed"/> as null is reading its own request parameter back, not a
/// fact about the company. Measured 2026-08-26 against <c>from=2026-05-16&amp;to=2026-05-17</c>: 7 wire fields
/// without the flag, 12 with it, on all 48 rows of the same request.</para>
///
/// <para><b>Rows are not sorted.</b> The week-long capture's first element was dated <c>2026-05-19</c>, its last
/// day; the two-day capture's first element is the one row FMP re-dated past the end of the range. The SDK
/// preserves wire order rather than sorting, because the order carries no meaning to destroy and sorting would hide
/// how arbitrary it is. Sort at the call site if a sort is wanted.</para>
///
/// <para>The same <see langword="decimal"/> rule as <see cref="EarningsReport"/>: revenue reaches 2.3e12 and EPS is
/// signed.</para></summary>
public sealed record EarningsCalendarEntry
{
    /// <summary>Ticker as FMP spells it. The calendar is global, so the measured 48-row sample spans
    /// <c>GFH.AE</c>, <c>DSCT.TA</c>, <c>IOC.BO</c>, <c>RAM.BK</c> and <c>0AAW.L</c> — suffixed exchange codes are
    /// the norm here rather than the exception, and a caller filtering to US listings must do so explicitly.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The earnings date FMP reports for this row.
    ///
    /// <para><b>This is not necessarily the date the row was selected on.</b> With <c>includeReportTimes=true</c>
    /// some rows are re-dated one day forward while selection still happens on the un-shifted date — so a request
    /// for a single day can hand back a row carrying the following day here. Measured, with numbers, on
    /// <see cref="Endpoints.CalendarEndpoints.GetEarningsCalendarAsync(LocalDate, LocalDate, bool, bool, CancellationToken)"/>.</para>
    ///
    /// <para>Nullable for the same reason as <see cref="EarningsReport.Date"/>, and never null on a row the SDK
    /// returns.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Reported EPS, or <see langword="null"/> if the event has not been reported. Signed: <c>-0.17</c>
    /// measured on <c>0AAW.L</c> in the captured 48 rows.</summary>
    [JsonPropertyName("epsActual")] public decimal? EpsActual { get; init; }

    /// <summary>Consensus EPS estimate, or <see langword="null"/> where no analyst covers the symbol — which is
    /// common on the calendar precisely because it is global: 3 of the first 6 measured rows had none.</summary>
    [JsonPropertyName("epsEstimated")] public decimal? EpsEstimated { get; init; }

    /// <summary>Reported revenue in the company's own reporting currency. The calendar sends no currency field and
    /// mixes markets freely, so summing this column across rows adds baht to shekels.</summary>
    [JsonPropertyName("revenueActual")] public decimal? RevenueActual { get; init; }

    /// <summary>Consensus revenue estimate, in the same unstated currency as <see cref="RevenueActual"/>.</summary>
    [JsonPropertyName("revenueEstimated")] public decimal? RevenueEstimated { get; init; }

    /// <summary>When FMP last refreshed this row, as a plain <c>yyyy-MM-dd</c> date.
    ///
    /// <para>Present on calendar rows with and without the report-times flag — worth saying because it is easy to
    /// miss: it is sent <i>last</i> in the flagged payload, after <c>confirmed</c>, rather than staying in the
    /// position it holds in the unflagged one.</para></summary>
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? LastUpdated { get; init; }

    // ---- Only populated when includeReportTimes=true. Null otherwise, meaning "not asked for". ----

    /// <summary>Session marker for the announcement: <c>"bmo"</c> before market open, <c>"amc"</c> after market
    /// close, or <see langword="null"/>.
    ///
    /// <para>Named <c>ReportTime</c> rather than <c>Time</c>, and typed <see cref="string"/> rather than any time
    /// type, because it is <b>not a clock time</b> — there is no hour in it, and the market it is relative to is
    /// whichever one the symbol trades on. The wire name is <c>time</c>, which is exactly the misreading this name
    /// exists to prevent.</para>
    ///
    /// <para><b>Null is the common case even with the flag set:</b> 41 of the 48 measured rows were null, against 5
    /// <c>bmo</c> and 2 <c>amc</c>. So a null here does not distinguish "no report time published" from "flag not
    /// sent" — only knowing what was asked does that. Those three values were the only ones seen across a 4000-row
    /// sweep. The token is kept verbatim; a caller matching on it should fold case itself, since the SDK does not
    /// normalise what it was sent.</para></summary>
    [JsonPropertyName("time")] public string? ReportTime { get; init; }

    /// <summary>Last day of the fiscal period being reported — a plain <c>yyyy-MM-dd</c> date, and the thing most
    /// callers actually mean by "which quarter is this". Non-null on all 48 measured rows when the flag was set,
    /// and distinct from <see cref="Date"/>, which is the announcement date: a <c>2026-03-31</c> period announced
    /// in mid-May is the ordinary case here.</summary>
    [JsonPropertyName("periodEnding")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? PeriodEnding { get; init; }

    /// <summary>Fiscal period label as FMP writes it — <c>Q1</c> through <c>Q4</c>, measured across the 48-row
    /// sample as <c>Q4</c> ×32, <c>Q1</c> ×13, <c>Q2</c> ×3.
    ///
    /// <para>A raw string rather than the <see cref="FmpDotNet.FiscalPeriod"/> enum on purpose: that enum is the
    /// <i>request</i> vocabulary (<c>annual</c>/<c>quarter</c>), this is the <i>response</i> vocabulary, and the
    /// statement endpoints already keep the same two apart for the same reason. A label here is relative to the
    /// company's own fiscal calendar, so <c>Q4</c> in May is not a data fault.</para></summary>
    [JsonPropertyName("fiscalPeriod")] public string? FiscalPeriod { get; init; }

    /// <summary>Fiscal year the period belongs to. Measured as <c>2026</c> ×45 and <c>2025</c> ×3 in a single
    /// two-day sample — a fiscal year lagging the calendar year by one is normal, not stale data. Sent unquoted
    /// here, unlike the statement endpoints which quote it; the SDK reads either.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Whether FMP considers the date confirmed by the company rather than estimated. Measured as
    /// <see langword="false"/> on 44 of 48 rows, so an unconfirmed date is the norm and a scheduling pipeline that
    /// trusts <see cref="Date"/> unconditionally is trusting an estimate most of the time.</summary>
    [JsonPropertyName("confirmed")] public bool? Confirmed { get; init; }
}
