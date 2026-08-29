using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The one record the nine technical-indicator paths share, against responses captured live on
/// 2026-08-29.</summary>
public class TechnicalIndicatorBarTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static IReadOnlyList<TechnicalIndicatorBar> Parse(string fixture) =>
        JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListTechnicalIndicatorBar)!;

    [Theory]
    [InlineData("adx", TechnicalIndicator.Adx)]
    [InlineData("dema", TechnicalIndicator.Dema)]
    [InlineData("ema", TechnicalIndicator.Ema)]
    [InlineData("rsi", TechnicalIndicator.Rsi)]
    [InlineData("sma", TechnicalIndicator.Sma)]
    [InlineData("standarddeviation", TechnicalIndicator.StandardDeviation)]
    [InlineData("tema", TechnicalIndicator.Tema)]
    [InlineData("williams", TechnicalIndicator.WilliamsR)]
    [InlineData("wma", TechnicalIndicator.Wma)]
    public void Every_path_binds_its_column_to_Value_and_names_itself(string segment, TechnicalIndicator expected)
    {
        var rows = Parse($"technical-indicators-{segment}.AAPL.head.json");

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            // The indicator is resolved from the field that ARRIVED, not stamped by the caller's argument.
            Assert.Equal(expected, row.Indicator);
            Assert.NotNull(row.Value);
            Assert.NotNull(row.Open);
            Assert.NotNull(row.High);
            Assert.NotNull(row.Low);
            Assert.NotNull(row.Close);
            Assert.NotNull(row.Volume);
            Assert.NotNull(row.Timestamp);
        });
    }

    [Fact]
    public void The_shared_OHLCV_block_is_identical_across_paths()
    {
        // The nine paths are the same price series with one column swapped — measured 2026-08-29, exactly nine
        // distinct key tuples across 88 non-empty responses. If a future change bound OHLCV differently per
        // path, this fails. It is also what justifies one record instead of nine.
        var sma = Parse("technical-indicators-sma.AAPL.head.json");
        var adx = Parse("technical-indicators-adx.AAPL.head.json");

        Assert.Equal(sma.Count, adx.Count);
        for (var i = 0; i < sma.Count; i++)
        {
            Assert.Equal(sma[i].Timestamp, adx[i].Timestamp);
            Assert.Equal(sma[i].Open, adx[i].Open);
            Assert.Equal(sma[i].High, adx[i].High);
            Assert.Equal(sma[i].Low, adx[i].Low);
            Assert.Equal(sma[i].Close, adx[i].Close);
            Assert.Equal(sma[i].Volume, adx[i].Volume);
            Assert.NotEqual(sma[i].Value, adx[i].Value);
        }
    }

    [Fact]
    public void A_daily_row_carries_midnight_and_an_intraday_row_carries_a_real_time()
    {
        // Pins the LocalDateTime decision against a future tidy-up to LocalDate. Measured 2026-08-29: all 1254
        // daily rows are `00:00:00`, and every intraday timeframe carries a real bar time. One property serves
        // both, so it cannot drop the time half.
        var daily = Parse("technical-indicators-sma.AAPL.head.json")[0];
        var hourly = Parse("technical-indicators-sma.AAPL.1hour.head.json")[0];

        Assert.Equal(new LocalDateTime(2026, 8, 28, 0, 0, 0), daily.Timestamp);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 30, 0), hourly.Timestamp);
    }

    [Fact]
    public void A_fractional_volume_survives_on_a_daily_bar()
    {
        // The measurement that forces decimal? rather than long?. EndOfDayBar.Volume is long? because daily
        // EQUITY bars showed no fractions — but measured 2026-08-29, BTCUSD carried 75 fractional volumes
        // across 1825 daily rows. One record serves daily and intraday here, so long? would truncate real data.
        var rows = Parse("technical-indicators-rsi.BTCUSD.fractional-volume.json");

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.Volume);
            Assert.NotEqual(decimal.Truncate(row.Volume!.Value), row.Volume!.Value);
        });
    }

    [Fact]
    public void A_negative_indicator_value_binds()
    {
        // Williams %R is negative by construction. Measured 2026-08-29 on 1254 AAPL daily rows: −99.5844 to
        // 0.0000, none positive. A model assuming non-negative indicator columns is wrong on one of nine.
        var rows = Parse("technical-indicators-williams.AAPL.range.json");

        Assert.Contains(rows, r => r.Value == 0m);
        Assert.Contains(rows, r => r.Value < -90m);
        Assert.All(rows, r => Assert.Equal(TechnicalIndicator.WilliamsR, r.Indicator));
    }

    [Fact]
    public void A_row_with_no_indicator_column_is_rejected()
    {
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void A_row_with_two_indicator_columns_is_rejected()
    {
        // Never observed in 88 captures. If FMP ever answers two, the row is not what this record models and
        // guessing which column the caller meant would be worse than failing.
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3,
              "sma": 1.5, "rsi": 60.0}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void An_unrecognised_column_is_rejected_rather_than_silently_dropped()
    {
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3,
              "macd": 1.5}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void A_null_value_binds_as_null_rather_than_failing()
    {
        // No null was observed in 386,617 field slots on 2026-08-29, but the properties are nullable by house
        // convention and the converter must honour that rather than throw on a shape it merely never saw.
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": null, "high": null, "low": null, "close": null,
              "volume": null, "sma": null}]
            """;
        var rows = JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar)!;

        Assert.Single(rows);
        Assert.Equal(TechnicalIndicator.Sma, rows[0].Indicator);
        Assert.Null(rows[0].Value);
        Assert.Null(rows[0].Open);
    }
}
