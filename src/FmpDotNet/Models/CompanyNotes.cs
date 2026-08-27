using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One registered security of an issuer — usually a note or a preferred share — from
/// <c>stable/company-notes</c>.
///
/// <para>Four fields and three traps, all measured 2026-08-27. <b>The dataset is sparse:</b> <c>AAPL</c>
/// answered 7 rows, <c>T</c> 20 and <c>F</c> 16, while <c>JPM</c>, <c>BAC</c>, <c>VZ</c>, <c>GS</c>, <c>MS</c>,
/// <c>PG</c> and <c>JNJ</c> all answered <c>[]</c>. An empty result is the common case here rather than a
/// symptom.</para></summary>
public sealed record CompanyNote
{
    /// <summary>The issuer's SEC Central Index Key, zero-padded. The one field on this record that reliably
    /// identifies the issuer — see <see cref="Symbol"/> for why that one does not.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary><b>The note's own listing symbol, not the issuer's ticker.</b>
    ///
    /// <para>Measured 2026-08-27: <c>symbol=T</c> answers 20 rows whose symbols are <c>T</c>, <c>T 25</c>,
    /// <c>T 25B</c>, <c>T 26A</c> … <c>T 33A</c>, <c>T PRA</c>, <c>T PRC</c> — <b>19 of the 20 differ from the
    /// requested ticker, and they contain spaces</b>. Anything that treats this value as a tradeable ticker —
    /// feeding it to a quote endpoint, joining it against a symbol list, using it as a dictionary key of
    /// tickers — is wrong. The SDK does not trim, split or normalise it: what is here is what FMP
    /// sent.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The security's title as filed — <c>"AT&amp;amp;T Inc. 5.200% Global Notes due November 18,
    /// 2033"</c>.
    ///
    /// <para><b>HTML-escaped, and the SDK does not decode it.</b> FMP sends <c>&amp;amp;</c> literally and the
    /// only entity observed on 2026-08-27 was that one. Decoding here would be a silent transformation of an
    /// upstream value, and it would be irreversible — a caller could no longer tell what FMP actually sent.
    /// Call <c>System.Net.WebUtility.HtmlDecode</c> at the point of display.</para></summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>Where the security is listed, or <see langword="null"/>.
    ///
    /// <para><b>Null on 19 of <c>T</c>'s 20 rows</b>, measured 2026-08-27 — so null is the norm, not the
    /// exception. A single-row sample from <c>AAPL</c> shows <c>"NASDAQ"</c> and hides this completely, which
    /// is why this field is nullable and why the fixture behind it is AT&amp;T's.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
}
