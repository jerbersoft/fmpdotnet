using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One earnings call, transcribed. From <c>stable/earning-call-transcript</c>.
///
/// <para><b>This record and its two siblings deliberately disagree with each other, because the wire
/// does.</b> The same quarter is <see cref="Period"/> — the string <c>"Q3"</c> — here and on
/// <see cref="LatestTranscript"/>, but <see cref="TranscriptDate.Quarter"/> — the integer <c>3</c> — on
/// <see cref="TranscriptDate"/>. The same year is <see cref="Year"/> here and
/// <c>fiscalYear</c> on both siblings. Normalising the three would mean inventing values FMP did not send, so
/// each record transcribes its own endpoint and the divergence is documented on all three.</para>
///
/// <para><b>The request and the response disagree too.</b> The endpoint is queried with
/// <c>quarter=3</c> and answers <c>period: "Q3"</c>. See
/// <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/>.</para></summary>
public sealed record EarningsTranscript
{
    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fiscal quarter as a string — <c>Q3</c>. The <b>request</b> takes the integer
    /// <c>3</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> here and <c>fiscalYear</c> on both sibling
    /// records.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The date the call was held.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The whole transcript as one string, speaker names inline —
    /// <c>Suhasini Chandramouli: Good afternoon, and welcome…</c>.
    ///
    /// <para><b>Measured at 46,487 characters</b> for AAPL 2025 Q3 on 2026-08-29. It is not chunked, not
    /// parsed into speaker turns, and not offered as a stream: FMP sends one JSON string field and this SDK
    /// transcribes it. A caller who wants turns splits on <c>": "</c> at a line start and owns the result;
    /// there is no delimiter FMP guarantees.</para></summary>
    [JsonPropertyName("content")] public string? Content { get; init; }
}

/// <summary>One quarter for which a transcript exists. From
/// <c>stable/earning-call-transcript-dates</c>.
///
/// <para>The index into <see cref="EarningsTranscript"/>: these three fields are exactly what
/// <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/> needs, except that the year is spelled
/// <see cref="FiscalYear"/> here and <c>year</c> there. Measured 2026-08-29, <c>?symbol=AAPL</c> answered 84
/// rows spanning 2026-07-30 back to 2005-10-13 — full history, newest first, no cap observed.</para>
///
/// <para><b><see cref="Quarter"/> is an integer here and a string on both sibling records.</b> See
/// <see cref="EarningsTranscript"/>.</para></summary>
public sealed record TranscriptDate
{
    /// <summary>The fiscal quarter as an integer — <c>3</c>. Spelled <c>period: "Q3"</c> on both sibling
    /// records, and this is the form <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/>
    /// takes.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> on <see cref="EarningsTranscript"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The date the call was held.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}

/// <summary>One entry in the whole-market feed of newly published transcripts. From
/// <c>stable/earning-call-transcript-latest</c>.
///
/// <para><b>Global, not US-only.</b> Measured 2026-08-29 the first page carried <c>7011.T</c>,
/// <c>601939.SS</c> and <c>PRS.OL</c> — Tokyo, Shanghai and Oslo — so
/// <see cref="Symbol"/> carries exchange suffixes and nothing should split on the dot.</para>
///
/// <para><b>Not sorted by date.</b> The same measurement had row 0 dated 2026-11-07 and row 1 dated
/// 2026-08-28. Nothing here promises an ordering.</para>
///
/// <para>This record carries <see cref="Period"/> as a string like <see cref="EarningsTranscript"/> and
/// <see cref="FiscalYear"/> like <see cref="TranscriptDate"/> — one field from each sibling's vocabulary.
/// See <see cref="EarningsTranscript"/> for why none of the three is normalised.</para></summary>
public sealed record LatestTranscript
{
    /// <summary>The ticker, as FMP spells it — including an exchange suffix for non-US listings.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fiscal quarter as a string — <c>Q2</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> on <see cref="EarningsTranscript"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The date the call was held. Measured 2026-08-29 this can be in the future relative to other
    /// rows on the same page.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}
