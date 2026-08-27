using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One reported headcount, from an SEC filing. Serves both <c>stable/employee-count</c> and
/// <c>stable/historical-employee-count</c>.
///
/// <para><b>Those two documented paths are one dataset.</b> Their responses were byte-identical, compared as
/// sorted JSON on 2026-08-27: <c>AAPL</c> 32 rows, <c>JPM</c> 5, <c>SHOP</c> 11, <c>XOM</c> 0 on both. Both
/// honour <c>limit</c> downward and both answer the same nine fields. One record therefore serves both, and both
/// methods are shipped — see
/// <see cref="Endpoints.CompanyEndpoints.GetEmployeeCountAsync(string, int?, CancellationToken)"/>.</para>
///
/// <para>Every field below was populated on every row measured, across three filers. <b><c>XOM</c> answers zero
/// rows</b>, so an empty result for a large company is normal rather than a symptom.</para></summary>
public sealed record EmployeeCount
{
    /// <summary>The filer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded — <c>"0000320193"</c>. A string and not a number:
    /// parsing it loses the padding that SEC filings use.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>When EDGAR accepted the filing.
    ///
    /// <para>FMP sends this as <c>"2025-10-31 06:01:26"</c> — space-separated, no <c>T</c>, no offset — and it is
    /// EDGAR's <b>Eastern</b> wall clock, matching <see cref="IncomeStatement.AcceptedDate"/>. If read with
    /// <see cref="NullableFmpInstantJsonConverter"/> instead, which parses the identical wire shape as UTC, every
    /// value would be four or five hours early and nothing would throw.</para></summary>
    [JsonPropertyName("acceptanceTime")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptanceTime { get; init; }

    /// <summary>The period the filing reports on — a fiscal period end, not the filing date.</summary>
    [JsonPropertyName("periodOfReport")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? PeriodOfReport { get; init; }

    /// <summary>The filer's name as it appears on the filing.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The SEC form the count was taken from — <c>"10-K"</c> on every row measured.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>When the filing was made. Can differ from <see cref="AcceptanceTime"/>'s date by a day when
    /// EDGAR accepted it after hours — <c>2023-11-02 18:08:27</c> accepted, filed <c>2023-11-03</c>.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The reported headcount.
    ///
    /// <para><b>Named <c>Employees</c> rather than <c>EmployeeCount</c> because C# forbids a member sharing its
    /// enclosing type's name</b> (CS0542). <c>Count</c> was the other candidate and was rejected: on a type that
    /// always arrives in a list, <c>row.Count</c> reads as the number of rows. The wire name is unchanged.</para></summary>
    [JsonPropertyName("employeeCount")] public int? Employees { get; init; }

    /// <summary>URL of the EDGAR filing index the count came from.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
}
