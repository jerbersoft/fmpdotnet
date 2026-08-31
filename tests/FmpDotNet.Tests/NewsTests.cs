using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The ten News paths, checked against captures taken live 2026-08-29 to 2026-08-31.</summary>
public class NewsTests
{
    [Fact]
    public void A_feed_row_binds_all_eight_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("news-stock-latest.head.json"),
            FmpJsonContext.Default.ListNewsArticle)!;

        Assert.NotEmpty(rows);
        var row = rows[0];

        // Binding.Unbound names every [JsonPropertyName] property that came back null, blank or empty, so an
        // empty result is the WHOLE record binding rather than a spot check. Five of the models in this repo
        // were measured 2026-08-27 with most of their [JsonPropertyName] attributes doing nothing, which a
        // two-field assertion cannot see. Task 1 verified this fixture's first row carries no null.
        Assert.Empty(Binding.Unbound(row));

        // And the three failures a whole-record check still cannot see, because a value in the wrong property
        // is not an absent value: a timestamp that silently defaulted, a url that landed in `site`, and a
        // symbol that landed where the publisher goes.
        Assert.NotNull(row.PublishedDate);
        Assert.StartsWith("http", row.Url);
        Assert.DoesNotContain("http", row.Symbol!, StringComparison.Ordinal);
    }

    [Fact]
    public void General_news_carries_no_symbol_at_all()
    {
        // Measured 2026-08-29: `symbol` was null on 250 of 250 general-latest rows, against 46 of 250 on
        // stock-latest and 0 of 250 on all four search paths. The absence is structural — general news has no
        // ticker — which is why Symbol is nullable and why its doc carries the per-path counts rather than one
        // number. A row without a symbol is not a broken row, and the rest of this asserts that.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("news-general-latest.head.json"),
            FmpJsonContext.Default.ListNewsArticle)!;

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Null(r.Symbol));
        Assert.All(rows, r => Assert.Contains("Symbol", Binding.Unbound(r)));
        Assert.All(rows, r => Assert.NotNull(r.PublishedDate));
        Assert.All(rows, r => Assert.NotNull(r.Title));
        Assert.All(rows, r => Assert.NotNull(r.Url));
    }

    [Fact]
    public void The_feed_timestamp_is_Eastern_wall_clock_and_shifts_with_DST()
    {
        // One of the two tests this slice exists to protect. The SDK carries two converters for the identical
        // "yyyy-MM-dd HH:mm:ss" wire shape, and NullableFmpInstantJsonConverter (UTC) compiles here,
        // deserialises here, and is wrong by four to five hours. Nothing in the data reveals the swap.
        //
        // 16:05 is the measured post-close cluster: hour 16 held 170 rows on 2026-08-27 (EDT) and 119 on
        // 2026-01-14 (EST), and `16:05` was a top-five wire value on BOTH days. A stored instant would have
        // moved by an hour across the DST boundary; a stripped Eastern wall clock does not. That is the
        // measurement, over 1,803 rows, that chose this converter.
        var summer = JsonSerializer.Deserialize(
            """[{"publishedDate":"2026-08-27 16:05:00"}]""",
            FmpJsonContext.Default.ListNewsArticle)![0];
        var winter = JsonSerializer.Deserialize(
            """[{"publishedDate":"2026-01-14 16:05:00"}]""",
            FmpJsonContext.Default.ListNewsArticle)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 27, 20, 5, 0), summer.PublishedDate);   // EDT, UTC-4
        Assert.Equal(Instant.FromUtc(2026, 1, 14, 21, 5, 0), winter.PublishedDate);   // EST, UTC-5

        // The two offsets differ, which rules out every FIXED-offset reading as well as UTC — a converter
        // hard-coding -4 or -5 would pass one of the two assertions above and fail this one.
        Assert.NotEqual(
            summer.PublishedDate!.Value - Instant.FromUtc(2026, 8, 27, 16, 5, 0),
            winter.PublishedDate!.Value - Instant.FromUtc(2026, 1, 14, 16, 5, 0));
    }

    [Fact]
    public void A_null_timestamp_stays_null_rather_than_becoming_the_epoch()
    {
        // publishedDate was never null in 2,250 measured rows, and the property is nullable anyway because the
        // deserialiser cannot promise a key is present. This pins what "absent" reads as: a caller who gets
        // 1970-01-01 instead of null has an article that silently sorts to the bottom of every feed.
        var rows = JsonSerializer.Deserialize(
            """[{"publishedDate":null},{"title":"no timestamp key at all"}]""",
            FmpJsonContext.Default.ListNewsArticle)!;

        Assert.All(rows, r => Assert.Null(r.PublishedDate));
    }
}
