using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One article FMP wrote itself, from <c>stable/fmp-articles</c>.
///
/// <para><b>The tenth News path, and the only one that does not share <see cref="NewsArticle"/>.</b> It
/// carries the same eight concepts and renames six of them: <c>date</c> for <c>publishedDate</c>,
/// <c>content</c> for <c>text</c>, <c>link</c> for <c>url</c>, <c>author</c> for <c>publisher</c>,
/// <c>tickers</c> for <c>symbol</c>, with only <c>title</c>, <c>image</c> and <c>site</c> spelled the same.
/// One record with two names per field is not available, which is why there are two records.</para>
///
/// <para><b>Two of the differences are in kind, not in spelling.</b> <see cref="Content"/> is HTML where
/// <see cref="NewsArticle.Text"/> is plain text, and <see cref="Date"/> is <b>UTC</b> where
/// <see cref="NewsArticle.PublishedDate"/> is Eastern. Both were measured; see each property.</para>
///
/// <para><b>This path may produce nothing on a given day, and an empty response is not a broken call.</b>
/// Measured 2026-08-31: weekdays carried 22 to 53 rows, the 2026-08-22 weekend carried 1 and 2, and the
/// 2026-08-29 weekend carried <b>none at all</b> — the path had then been silent for 60.5 hours. An earlier
/// figure of 3.5 articles per weekend day is an average and not a floor.</para>
///
/// <para>Nothing was null on any of the 200 rows measured 2026-08-30. Every property is nullable anyway,
/// because the deserialiser cannot promise a key is present.</para></summary>
public sealed record FmpArticle
{
    /// <summary>The headline. The one name this record shares with <see cref="NewsArticle"/>.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>When the article was published, read as <b>UTC</b> — <i>not</i> Eastern.
    ///
    /// <para><b>The opposite reading to <see cref="NewsArticle.PublishedDate"/>, on the identical
    /// <c>"yyyy-MM-dd HH:mm:ss"</c> wire shape.</b> The two converters differ in nothing but the zone they
    /// read that shape as, and picking the wrong one compiles, deserialises, and is wrong by four to five
    /// hours. <c>NewsTests.The_two_records_read_the_same_wire_string_hours_apart</c> is the test that fails
    /// if they are ever swapped.</para>
    ///
    /// <para><b>Measured by distribution against a known-Eastern control, on 2026-08-30.</b> The DST
    /// discriminator that settled the feeds cannot be run here: this path's reachable history begins
    /// 2026-06-26, entirely inside EDT, and the two shapes share zero urls so no article can be compared
    /// across them. Instead, hour-of-day histograms, weekdays only, 894 <c>news/general-latest</c> rows
    /// against 779 of these: <b>r = +0.656 at lag 4</b> (the UTC hypothesis) against <b>r = −0.225 at lag
    /// 0</b> (the Eastern one). Lag 0 is the only alignment Eastern permits and the worst in the entire
    /// 24-hour sweep. Read as Eastern, FMP would be near-silent through the pre-market hours where the feeds
    /// peak and busiest at 9pm; read as UTC, that becomes a 17:00 ET peak just after the close.</para>
    ///
    /// <para><b>This is the weaker of the SDK's two News timestamp bindings and this doc says so.</b> It
    /// rests on inference from distribution rather than a direct clock comparison. The direct test —
    /// comparing a newly appeared article's wire <c>date</c> against FMP's own <c>Date</c> response header —
    /// is recorded as outstanding in the measurements file, and remains un-run because the path published
    /// nothing between 2026-08-28 21:05:54 and at least 2026-08-31 09:38 UTC.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableFmpInstantJsonConverter))]
    public Instant? Date { get; init; }

    /// <summary>The article body, as <b>HTML that FMP wrote</b>.
    ///
    /// <para>Measured 2026-08-30, <b>200 of 200</b> rows carried tags — <c>&lt;ul&gt;</c>, <c>&lt;li&gt;</c>,
    /// <c>&lt;strong&gt;</c> — at a median length of 3,013 characters, against <b>0 of 2,250</b> rows
    /// carrying a tag in <see cref="NewsArticle.Text"/>. <b>A caller rendering this into a page is rendering
    /// markup from the wire.</b> This SDK does not strip it: the record carries what the wire sent, and what
    /// is safe to render is the caller's policy rather than an SDK's guess.</para></summary>
    [JsonPropertyName("content")] public string? Content { get; init; }

    /// <summary>The ticker, exactly as FMP spells it — exchange-prefixed, as in <c>"NASDAQ:CSIQ"</c>.
    ///
    /// <para><b>This value cannot be fed back into a <c>symbols=</c> query.</b> Measured 2026-08-30,
    /// <c>news/stock?symbols=NASDAQ:CSIQ</c> returns <b>0 rows</b> while <c>symbols=CSIQ</c> returns 20 —
    /// and a zero-row answer reads as "this company has no news". <see cref="Symbol"/> is the property that
    /// round-trips; this one is kept under its wire name because it is what FMP sent.</para>
    ///
    /// <para><b>The plural name is a warning, not a description of the measured data.</b> All 200 rows
    /// measured 2026-08-30 carried exactly one ticker and <b>not one comma appeared</b>. Every one carried a
    /// prefix: NASDAQ 101, NYSE 86, OTC 10, AMEX 3.</para></summary>
    [JsonPropertyName("tickers")] public string? Tickers { get; init; }

    /// <summary>A URL for the article's lead image.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>The link to the article. <see cref="NewsArticle"/> spells this <c>url</c>.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>Who wrote it. 7 distinct values across the 200 rows measured 2026-08-30.
    /// <see cref="NewsArticle"/> spells this <c>publisher</c> and carries 6 to 39 values per feed.</summary>
    [JsonPropertyName("author")] public string? Author { get; init; }

    /// <summary>The source name — the constant <c>"Financial Modeling Prep"</c> on all 200 rows measured
    /// 2026-08-30. These are FMP's own articles rather than a feed of anyone else's, which is what
    /// <see cref="Author"/>'s seven values and this path's absence of a <c>symbols</c> filter both
    /// reflect.</summary>
    [JsonPropertyName("site")] public string? Site { get; init; }

    /// <summary>The ticker with its exchange prefix removed — the half of <see cref="Tickers"/> that a
    /// <c>symbols=</c> query accepts.
    ///
    /// <para><see langword="null"/> when <see cref="Tickers"/> does not contain exactly one colon with a
    /// non-empty part on each side. Two tickers, no prefix, or a shape this SDK has never measured all read
    /// as "no single symbol here" rather than as a guess. Kept <i>beside</i> the wire value rather than
    /// replacing it, following <see cref="ExchangeMarketHours"/>.</para></summary>
    [JsonIgnore] public string? Symbol => Split(Tickers).Symbol;

    /// <summary>The exchange <see cref="Tickers"/> is prefixed with — <c>"NASDAQ"</c>, <c>"NYSE"</c>,
    /// <c>"OTC"</c>, <c>"AMEX"</c> across the 200 rows measured 2026-08-30. <see langword="null"/> under the
    /// same conditions as <see cref="Symbol"/>.</summary>
    [JsonIgnore] public string? Exchange => Split(Tickers).Exchange;

    /// <summary>Splits <c>EXCHANGE:SYMBOL</c>, returning nulls for anything else.
    ///
    /// <para>Strict on purpose. The alternative — take the text after the last colon — would turn
    /// <c>"NASDAQ:CSIQ,NYSE:GE"</c> into <c>"GE"</c> and silently discard the other ticker, which is a
    /// fabricated answer rather than a missing one. No comma appeared in the 200 rows measured 2026-08-30,
    /// so the multi-valued form is unmeasured and this returns null for it.</para></summary>
    private static (string? Symbol, string? Exchange) Split(string? tickers)
    {
        if (string.IsNullOrWhiteSpace(tickers)) return (null, null);

        var parts = tickers.Split(':');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[1], parts[0])
            : (null, null);
    }
}
