using System.Text.Json;
using FmpDotNet.Models;
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

    [Fact]
    public void An_article_row_binds_all_eight_renamed_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fmp-articles.head.json"),
            FmpJsonContext.Default.ListFmpArticle)!;

        Assert.NotEmpty(rows);

        // Nothing was null on any of the 200 rows measured 2026-08-30, so EVERY row is asserted whole rather
        // than just the first. Six of these eight keys are renames — date, content, link, author, tickers and
        // the shared title — so a copy-paste of NewsArticle's attributes would bind two of eight and leave
        // six silently null. That is precisely what Binding.Unbound catches and a spot check does not.
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
        Assert.All(rows, r => Assert.NotNull(r.Date));
        Assert.All(rows, r => Assert.StartsWith("http", r.Link));

        // `site` is the constant "Financial Modeling Prep" on all 200 rows measured 2026-08-30. These are
        // FMP's own articles, which is the reason the path exists and the reason `author` has 7 values.
        Assert.All(rows, r => Assert.Equal("Financial Modeling Prep", r.Site));
    }

    [Fact]
    public void The_article_timestamp_is_UTC_rather_than_the_feeds_Eastern()
    {
        // The mirror of the feed converter test. Same wire shape, different zone, measured separately: the
        // daily profile of fmp-articles correlates with a known-Eastern control feed at r = +0.656 at lag 4
        // and r = -0.225 at lag 0 (measured 2026-08-30, 894 control rows against 779). Lag 0 is the only
        // alignment Eastern permits and the worst in the whole 24-hour sweep.
        //
        // A UTC reading does NOT shift with DST, which is the observable difference from the feed converter.
        var summer = JsonSerializer.Deserialize(
            """[{"date":"2026-08-27 16:05:00"}]""", FmpJsonContext.Default.ListFmpArticle)![0];
        var winter = JsonSerializer.Deserialize(
            """[{"date":"2026-01-14 16:05:00"}]""", FmpJsonContext.Default.ListFmpArticle)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 27, 16, 5, 0), summer.Date);
        Assert.Equal(Instant.FromUtc(2026, 1, 14, 16, 5, 0), winter.Date);
    }

    [Fact]
    public void The_two_records_read_the_same_wire_string_hours_apart()
    {
        // THE test the design exists to protect, and the only one that survives a symmetric mistake. Both
        // shapes send "yyyy-MM-dd HH:mm:ss" with no zone marker; the nine feeds are Eastern and fmp-articles
        // is UTC, and the two converters differ in nothing else. Swapping them compiles, deserialises, and
        // moves every timestamp by four or five hours with nothing in the data to reveal it.
        //
        // Asserting the DIFFERENCE rather than each value separately is deliberate: someone who swapped BOTH
        // converters and then "fixed" the two tests above would leave this one failing.
        var summerFeed = JsonSerializer.Deserialize(
            """[{"publishedDate":"2026-08-27 16:05:00"}]""",
            FmpJsonContext.Default.ListNewsArticle)![0];
        var summerArticle = JsonSerializer.Deserialize(
            """[{"date":"2026-08-27 16:05:00"}]""",
            FmpJsonContext.Default.ListFmpArticle)![0];
        var winterFeed = JsonSerializer.Deserialize(
            """[{"publishedDate":"2026-01-14 16:05:00"}]""",
            FmpJsonContext.Default.ListNewsArticle)![0];
        var winterArticle = JsonSerializer.Deserialize(
            """[{"date":"2026-01-14 16:05:00"}]""",
            FmpJsonContext.Default.ListFmpArticle)![0];

        // EDT: the feed's 16:05 is 20:05 UTC, the article's is 16:05 UTC.
        Assert.Equal(Duration.FromHours(4), summerFeed.PublishedDate!.Value - summerArticle.Date!.Value);
        // EST: five, and the gap CHANGING is what rules out a fixed offset on either side.
        Assert.Equal(Duration.FromHours(5), winterFeed.PublishedDate!.Value - winterArticle.Date!.Value);
    }

    [Theory]
    [InlineData("NASDAQ:CSIQ", "CSIQ", "NASDAQ")]
    [InlineData("NYSE:GE", "GE", "NYSE")]
    [InlineData("OTC:ABCD", "ABCD", "OTC")]
    [InlineData("AMEX:XYZ", "XYZ", "AMEX")]
    // No colon: the value is already a bare ticker, or something this SDK has never measured. Either way it
    // is not a prefixed pair, and inventing an exchange for it would be a fabricated fact.
    [InlineData("CSIQ", null, null)]
    // The plural name is a standing warning. Not one comma appeared in the 200 rows measured 2026-08-30, so a
    // multi-valued form has never been seen — and this SDK will not guess which of two tickers a caller meant.
    [InlineData("NASDAQ:CSIQ,NYSE:GE", null, null)]
    [InlineData("A:B:C", null, null)]
    [InlineData(":CSIQ", null, null)]
    [InlineData("NASDAQ:", null, null)]
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void Tickers_splits_into_a_symbol_that_round_trips_and_the_exchange_it_came_from(
        string? tickers, string? symbol, string? exchange)
    {
        // The measured reason this parse exists: every one of 200 rows carried an exchange prefix on
        // 2026-08-30 — NASDAQ 101, NYSE 86, OTC 10, AMEX 3 — and `symbols=NASDAQ:CSIQ` returns 0 rows on
        // news/stock while `symbols=CSIQ` returns 20. Symbol is the half that feeds back into a search call.
        var row = new FmpArticle { Tickers = tickers };

        Assert.Equal(symbol, row.Symbol);
        Assert.Equal(exchange, row.Exchange);
        Assert.Equal(tickers, row.Tickers);   // the wire value is kept under its wire name, never rewritten
    }

    [Fact]
    public void Every_captured_ticker_yields_a_symbol_with_no_exchange_left_in_it()
    {
        // The theory above pins the parse against invented inputs; this pins it against what FMP actually
        // sent. A caller who passes Tickers straight into a symbols= query gets 0 rows and reads it as "no
        // news for this company"; Symbol is the property that does not do that.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fmp-articles.head.json"),
            FmpJsonContext.Default.ListFmpArticle)!;

        Assert.All(rows, r => Assert.NotNull(r.Symbol));
        Assert.All(rows, r => Assert.NotNull(r.Exchange));
        Assert.All(rows, r => Assert.DoesNotContain(":", r.Symbol!, StringComparison.Ordinal));

        // And the computed pair stays invisible to the binding check, which is what the [JsonIgnore]
        // attributes buy. Without them the source generator emits metadata for members the wire never sends.
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
    }
}
