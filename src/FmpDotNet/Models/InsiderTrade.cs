using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One insider transaction from a Form 3, 4 or 5 — served by both
/// <c>stable/insider-trading/latest</c> and <c>stable/insider-trading/search</c>.
///
/// <para><b>One record for two paths, verified rather than assumed.</b> Measured 2026-08-28, the two paths
/// return the same sixteen keys in the same order. They differ in what they select, not in what they
/// send.</para>
///
/// <para><b>Share counts are fractional.</b> Over 1,000 rows of the <c>latest</c> feed,
/// <see cref="SecuritiesOwned"/> was fractional on 59 (5.9%) and <see cref="SecuritiesTransacted"/> on 40
/// (4.0%) — phantom stock, deferred units and dividend reinvestment all produce them. Both are
/// <see cref="decimal"/>; an integer type would make <c>System.Text.Json</c> throw on those rows and cost the
/// caller the whole response.</para>
///
/// <para><b>Blank and null are both wire values and mean different things.</b> See
/// <see cref="TransactionType"/> and <see cref="DirectOrIndirect"/>.</para></summary>
public sealed record InsiderTrade
{
    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the form was filed. <b>Not the transaction date</b> — see
    /// <see cref="TransactionDate"/>, which was 59 days earlier on one of the three captured rows.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date the transaction took place. Neither date is derivable from the other; a Form 4 is due
    /// within two business days but a Form 3 can report a holding from months earlier.</summary>
    [JsonPropertyName("transactionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? TransactionDate { get; init; }

    /// <summary>The <b>insider's</b> Central Index Key, zero-padded — a person or an entity that files about
    /// the issuer. Distinct from <see cref="CompanyCik"/>, and the two are not interchangeable in
    /// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/>.</summary>
    [JsonPropertyName("reportingCik")] public string? ReportingCik { get; init; }

    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded.</summary>
    [JsonPropertyName("companyCik")] public string? CompanyCik { get; init; }

    /// <summary>The SEC transaction code — <c>"S-Sale"</c>, <c>"P-Purchase"</c>, <c>"A-Award"</c>. Eighteen
    /// exist and <see cref="Endpoints.InsiderTradesEndpoints.GetTransactionTypesAsync"/> serves the list.
    ///
    /// <para><b><c>""</c> on 40 of 1,000 rows measured 2026-08-28</b>, which is FMP's value and not an absence:
    /// a Form 3 initial statement reports a holding rather than a transaction, so there is no code to send.
    /// That blank is also why this is a <see cref="string"/> rather than an enum — a closed C# enum over a
    /// server-served list would have no member for it, and no member for a code FMP adds next
    /// Tuesday.</para></summary>
    [JsonPropertyName("transactionType")] public string? TransactionType { get; init; }

    /// <summary>Shares the insider holds after the transaction. <b>Fractional on 5.9% of rows measured</b>,
    /// with a maximum of 61,721,535 — see the record's documentation.</summary>
    [JsonPropertyName("securitiesOwned")] public decimal? SecuritiesOwned { get; init; }

    /// <summary>The insider's name as EDGAR spells it — <c>"KRISHNA ARVIND"</c>, <c>"Newstead Jennifer"</c>.
    /// Surname first, case unnormalised.</summary>
    [JsonPropertyName("reportingName")] public string? ReportingName { get; init; }

    /// <summary>The insider's relationship to the issuer — <c>"director"</c>,
    /// <c>"officer: SVP, GC and Secretary"</c>, <c>"director, officer: Chairman, President &amp; CEO"</c>.
    /// Free text carrying several roles comma-joined; not a code, and not parsed here.</summary>
    [JsonPropertyName("typeOfOwner")] public string? TypeOfOwner { get; init; }

    /// <summary><c>"A"</c> for an acquisition, <c>"D"</c> for a disposition. <b><c>""</c> on the same rows
    /// where <see cref="TransactionType"/> is blank</b> — 8 of the 100-row capture.</summary>
    [JsonPropertyName("acquisitionOrDisposition")] public string? AcquisitionOrDisposition { get; init; }

    /// <summary><c>"D"</c> for directly held, <c>"I"</c> for indirectly.
    ///
    /// <para><b>Explicitly <see langword="null"/> on 3 of the 100-row capture</b>, where
    /// <see cref="TransactionType"/> is blank rather than null on its own 8. Two different absences on one
    /// record, neither normalised into the other: <c>""</c> is a value FMP sent, <c>null</c> is a value it did
    /// not.</para></summary>
    [JsonPropertyName("directOrIndirect")] public string? DirectOrIndirect { get; init; }

    /// <summary>The SEC form — <c>"3"</c>, <c>"4"</c>, <c>"4/A"</c>, <c>"5"</c>.
    ///
    /// <para><b>Not the same vocabulary as <see cref="InstitutionalFiling.FormType"/></b>, which carries
    /// <c>"13F-HR"</c> and its variants. One field name, two disjoint value sets — which is why the two records
    /// are separate.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>Shares moved by the transaction. <b>Fractional on 4.0% of rows measured</b>, maximum
    /// 33,586,045.</summary>
    [JsonPropertyName("securitiesTransacted")] public decimal? SecuritiesTransacted { get; init; }

    /// <summary>The price per share.
    ///
    /// <para><b><c>0</c> on 41.4% of rows measured</b> — 414 of 1,000 — and that is a real value rather than a
    /// missing one: an award, a gift and a phantom-stock accrual all move shares at no price. Do not read a
    /// zero here as "unknown".</para></summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>What was transacted — <c>"Common Stock"</c>, <c>"Phantom Stock"</c>,
    /// <c>"Convertible Note"</c>, <c>"Restricted Stock Unit"</c>. Blank on 3 of the 100-row capture.</summary>
    [JsonPropertyName("securityName")] public string? SecurityName { get; init; }

    /// <summary>The filing on EDGAR.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
