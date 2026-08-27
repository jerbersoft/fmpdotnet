using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One announced merger or acquisition, from <c>stable/mergers-acquisitions-latest</c> and
/// <c>stable/mergers-acquisitions-search</c> — the acquirer, the target, and the filing that announced it.
///
/// <para><b>Three of the nine fields are nullable, and a small sample shows none of them.</b> Measured across
/// the 1,000 rows of page 0 on 2026-08-27: <see cref="TargetedCik"/> null on <b>390</b>,
/// <see cref="TargetedSymbol"/> on <b>181</b>, <see cref="TargetedCompanyName"/> on <b>1</b>. FMP's documented
/// example shows all three populated, and an independent client types all three non-optional as a result. The
/// acquirer's own three fields were populated on every row.</para></summary>
public sealed record MergerAcquisition
{
    /// <summary>The acquirer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The acquirer's name, as the filing spells it — upper-cased on some rows
    /// (<c>"NORTHRIM BANCORP INC"</c>) and not on others (<c>"Ready Capital Corp"</c>).</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The acquirer's SEC Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The target's name, or <see langword="null"/> — null on 1 of the 1,000 rows measured. Rare, but
    /// the field that is most likely to be assumed present.</summary>
    [JsonPropertyName("targetedCompanyName")] public string? TargetedCompanyName { get; init; }

    /// <summary>The target's SEC Central Index Key, or <see langword="null"/>.
    ///
    /// <para><b>This field says "nothing" in two different ways.</b> It is <see langword="null"/> on 390 of the
    /// 1,000 rows measured on 2026-08-27, and it also carries the sentinel <c>"0000000000"</c> — a
    /// well-formed, zero-padded CIK belonging to no filer. Code that checks only for null passes the sentinel
    /// to an EDGAR lookup and gets nothing back, or worse, treats it as a real identity and groups unrelated
    /// deals under it. <b>Check for both.</b></para></summary>
    [JsonPropertyName("targetedCik")] public string? TargetedCik { get; init; }

    /// <summary>The target's ticker, or <see langword="null"/> — null on 181 of the 1,000 rows measured.
    /// Absent for private targets, which is most of them.</summary>
    [JsonPropertyName("targetedSymbol")] public string? TargetedSymbol { get; init; }

    /// <summary>The date on the transaction. A calendar date with no time of day.</summary>
    [JsonPropertyName("transactionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? TransactionDate { get; init; }

    /// <summary>When EDGAR accepted the filing. EDGAR's <b>Eastern</b> wall clock, matching
    /// <see cref="IncomeStatement.AcceptedDate"/> — not UTC.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>URL of the EDGAR filing that announced the deal — an S-4 on most rows.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}
