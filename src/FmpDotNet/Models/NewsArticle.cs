using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One article-symbol pairing from the nine <c>stable/news/*</c> paths.
///
/// <para><b>Nine paths, one shape, measured exactly.</b> The five <c>-latest</c> feeds and the four search
/// paths returned the same eight keys in the same order across <b>2,250 rows</b> on 2026-08-29. The tenth
/// News path, <c>stable/fmp-articles</c>, is <see cref="FmpArticle"/>: it carries the same eight concepts
/// under six different names and cannot share this record. The facade is
/// <see cref="Endpoints.NewsEndpoints"/>.</para>
///
/// <para><b>A row is an article-symbol pairing, not an article.</b> A multi-symbol query returns the same
/// article once per matching symbol. Measured 2026-08-29 on <c>news/crypto?symbols=BTCUSD,ETHUSD</c>: 19 of
/// 250 urls appeared twice, every one of them under a <i>different</i> <see cref="Symbol"/>, and zero
/// same-symbol repeats — the same pattern on the forex and five-symbol equity queries. <b>Counting rows
/// over-counts articles.</b> This SDK does not deduplicate: the pairing is what the wire sent and the symbol
/// is what made the row match.</para>
///
/// <para><b>Every property is nullable and the measured null counts are in the docs rather than in the
/// type.</b> "Never null in 2,250 rows" and "cannot be null" are different statements, and only the first
/// was measured.</para></summary>
public sealed record NewsArticle
{
    /// <summary>The ticker the row is paired with — <c>"AAPL"</c>, <c>"BTCUSD"</c>, <c>"EURUSD"</c>.
    ///
    /// <para><b>Null 310 times in 2,250 rows measured 2026-08-29, and the nulls are structural rather than
    /// incidental.</b> By path: <b>250 of 250</b> on <c>general-latest</c>, 46 of 250 on
    /// <c>stock-latest</c>, 13 on <c>press-releases-latest</c>, 1 on <c>crypto-latest</c>, and <b>0 of 250
    /// on all four search paths</b>. General news has no ticker at all; the unfiltered feeds carry untagged
    /// rows; a symbol-filtered query cannot lack one.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When the article was published, read as <b>Eastern</b> wall clock.
    ///
    /// <para><b>This is the typing decision of the group, and the intuitive answer is wrong.</b> The wire
    /// sends <c>"yyyy-MM-dd HH:mm:ss"</c> with no offset and no zone marker — verified on all 2,450
    /// timestamps measured 2026-08-29 — and this SDK carries two converters for that exact shape.
    /// <see cref="NullableFmpInstantJsonConverter"/> reads it as UTC and would put every news timestamp
    /// <b>four to five hours early</b>. It compiles, it deserialises, and nothing in the data reveals
    /// it.</para>
    ///
    /// <para><b>The evidence is the DST discriminator, over 1,803 rows on two days six months apart.</b> Two
    /// complete calendar days of <c>press-releases-latest</c>, paged until the day ran short so no hour is
    /// under-represented: 2026-08-27 under EDT (964 rows, peaks at 16:00 with 170 and 08:00 with 138) and
    /// 2026-01-14 under EST (839 rows, peaks at 08:00, 09:00 and 16:00). The wire values <b>do not
    /// shift</b> — <c>16:05</c> and <c>08:00</c> are top clusters on both days. A stored instant would move
    /// by an hour across the boundary; a stripped wall clock does not. And the clusters are the canonical US
    /// wire times read as Eastern: pre-market 07:00–09:00, post-close 16:04–16:30 against a 16:00 ET bell.
    /// Under a UTC reading the post-close cluster would sit at 20:0x, where the summer day holds 14 rows
    /// against hour 16's 170.</para>
    ///
    /// <para><b><see cref="FmpArticle.Date"/> takes the OTHER converter.</b> Same wire shape, different zone,
    /// measured separately. The two are four to five hours apart and swapping them is silent.</para>
    ///
    /// <para>Never null in 2,250 rows measured 2026-08-29.</para></summary>
    [JsonPropertyName("publishedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? PublishedDate { get; init; }

    /// <summary>Who published it — <c>"The Motley Fool"</c>, <c>"FX Street"</c>, <c>"Newsfile Corp"</c>.
    ///
    /// <para>The vocabulary is narrow and differs sharply by feed. Measured 2026-08-29 over 250 rows each:
    /// 28 distinct publishers on <c>stock-latest</c> and on <c>general-latest</c>, 39 on
    /// <c>crypto-latest</c>, <b>9</b> on <c>forex-latest</c> — where FX Street alone supplies 136 of 250 —
    /// and <b>6</b> on <c>press-releases-latest</c>. Never null in 2,250 rows.</para></summary>
    [JsonPropertyName("publisher")] public string? Publisher { get; init; }

    /// <summary>The headline. Never null in 2,250 rows measured 2026-08-29.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>A URL for the article's lead image. Null on 14 of 2,250 rows measured 2026-08-29.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>The publication's own site name. Null on 6 of 2,250 rows measured 2026-08-29 — all six on
    /// <c>crypto-latest</c> and <c>news/crypto</c>.</summary>
    [JsonPropertyName("site")] public string? Site { get; init; }

    /// <summary>A summary or excerpt, as <b>plain text</b>.
    ///
    /// <para><b>The one property that separates this record from <see cref="FmpArticle"/> in kind rather
    /// than in name.</b> Measured 2026-08-29, <b>0 of 2,250</b> rows carried an HTML tag here, at a median
    /// length of 88–462 characters by path. <see cref="FmpArticle.Content"/> is markup on 200 of 200 rows
    /// at a median 3,013 characters, measured 2026-08-29. A caller that renders one the way it renders
    /// the other is either escaping tags into visible text or injecting FMP's markup into a page.</para>
    ///
    /// <para>Never null in 2,250 rows.</para></summary>
    [JsonPropertyName("text")] public string? Text { get; init; }

    /// <summary>The link to the article. Absolute <c>http(s)</c> on every measured row, and unique within a
    /// single-symbol response. Never null in 2,250 rows measured 2026-08-29.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
