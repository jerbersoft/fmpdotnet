namespace FmpDotNet.Tests;

/// <summary>The indicator enum, against the nine paths measured on 2026-08-29.</summary>
public class TechnicalIndicatorTests
{
    [Theory]
    [InlineData(TechnicalIndicator.Adx, "adx")]
    [InlineData(TechnicalIndicator.Dema, "dema")]
    [InlineData(TechnicalIndicator.Ema, "ema")]
    [InlineData(TechnicalIndicator.Rsi, "rsi")]
    [InlineData(TechnicalIndicator.Sma, "sma")]
    [InlineData(TechnicalIndicator.StandardDeviation, "standarddeviation")]
    [InlineData(TechnicalIndicator.Tema, "tema")]
    [InlineData(TechnicalIndicator.WilliamsR, "williams")]
    [InlineData(TechnicalIndicator.Wma, "wma")]
    public void Each_member_maps_to_its_path_segment(TechnicalIndicator indicator, string expected) =>
        Assert.Equal(expected, indicator.ToPathSegment());

    [Fact]
    public void The_standard_deviation_segment_and_field_differ_in_case()
    {
        // The one case in nine where the path segment is not the JSON field name. Measured 2026-08-29: the path
        // is all-lowercase `standarddeviation` and the field is camelCase `standardDeviation`. A binder that
        // derives one from the other gets eight right and this one wrong, silently.
        Assert.Equal("standarddeviation", TechnicalIndicator.StandardDeviation.ToPathSegment());
        Assert.Equal("standardDeviation", TechnicalIndicator.StandardDeviation.ToJsonField());
    }

    [Fact]
    public void Every_json_field_round_trips_back_to_its_member()
    {
        foreach (var member in Enum.GetValues<TechnicalIndicator>())
        {
            Assert.True(TechnicalIndicatorExtensions.TryFromJsonField(member.ToJsonField(), out var found));
            Assert.Equal(member, found);
        }
    }

    [Theory]
    [InlineData("date")]
    [InlineData("open")]
    [InlineData("volume")]
    [InlineData("macd")]
    public void A_field_that_is_not_an_indicator_column_is_rejected(string field)
    {
        // `macd` is a real FMP indicator this SDK does not model — it must still be rejected rather than
        // silently accepted because it looks like a plausible column name.
        Assert.False(TechnicalIndicatorExtensions.TryFromJsonField(field, out _));
    }

    [Fact]
    public void A_field_that_differs_only_in_case_still_resolves()
    {
        // Not hypothetical: measured 2026-08-29, the PATH segment `SMA` returned a response byte-identical to
        // `sma`. The map now matches case-insensitively too, to match FmpJsonContext's SDK-wide
        // PropertyNameCaseInsensitive — a custom converter has to opt into that itself.
        Assert.True(TechnicalIndicatorExtensions.TryFromJsonField("SMA", out var found));
        Assert.Equal(TechnicalIndicator.Sma, found);
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, true)]
    [InlineData(TechnicalIndicator.Dema, true)]
    [InlineData(TechnicalIndicator.Ema, true)]
    [InlineData(TechnicalIndicator.Tema, true)]
    [InlineData(TechnicalIndicator.Rsi, false)]
    [InlineData(TechnicalIndicator.Sma, false)]
    [InlineData(TechnicalIndicator.StandardDeviation, false)]
    [InlineData(TechnicalIndicator.WilliamsR, false)]
    [InlineData(TechnicalIndicator.Wma, false)]
    public void Warm_up_is_classified_by_measurement_not_by_theory(TechnicalIndicator indicator, bool expected)
    {
        // Rsi is the row that matters. It is recursive by construction — Wilder smoothing — and measured
        // 2026-08-29 it returned values identical to the full series on every row of a 10-row window. Anything
        // that "corrects" this to true is reasoning from a textbook against a measurement.
        Assert.Equal(expected, indicator.NeedsWarmUp());
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, 10, 270)]
    [InlineData(TechnicalIndicator.Adx, 20, 540)]
    [InlineData(TechnicalIndicator.Ema, 10, 40)]
    [InlineData(TechnicalIndicator.Dema, 10, 40)]
    [InlineData(TechnicalIndicator.Tema, 10, 40)]
    [InlineData(TechnicalIndicator.Rsi, 10, 0)]
    [InlineData(TechnicalIndicator.Sma, 10, 0)]
    [InlineData(TechnicalIndicator.StandardDeviation, 10, 0)]
    [InlineData(TechnicalIndicator.WilliamsR, 10, 0)]
    [InlineData(TechnicalIndicator.Wma, 10, 0)]
    public void Suggested_warm_up_follows_the_measured_convergence(
        TechnicalIndicator indicator, int periodLength, int expected) =>
        Assert.Equal(expected, indicator.SuggestedWarmUpBars(periodLength));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Suggested_warm_up_rejects_a_period_below_one(int periodLength) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TechnicalIndicator.Adx.SuggestedWarmUpBars(periodLength));

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_FMP()
    {
        // Measured 2026-08-29: an unknown segment such as `macd` answers HTTP 404 with the body `[]` — the
        // success shape on a failure status, which surfaces as an exception naming neither the mistake nor the
        // fix.
        var undeclared = (TechnicalIndicator)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToPathSegment());
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToJsonField());
    }

    [Fact]
    public void The_enum_has_exactly_nine_members()
    {
        // Warm_up_is_classified_by_measurement_not_by_theory and Suggested_warm_up_follows_the_measured_
        // convergence above are fixed InlineData tables keyed by hand to the nine members measured 2026-08-29.
        // Both NeedsWarmUp and SuggestedWarmUpBars end in a catch-all (`_ => false` / `_ => 0`), which is the
        // UNSAFE direction for a member neither table has a row for — "no warm-up needed". A tenth member added
        // later would silently inherit that catch-all in both switches AND pass both InlineData tables, since
        // neither table has a row asserting anything about it. Pinning the count here means a tenth member
        // fails this test first, sending someone to look at all four switches in TechnicalIndicator.cs and both
        // tables above rather than the unmeasured claim shipping by omission.
        Assert.Equal(9, Enum.GetValues<TechnicalIndicator>().Length);
    }
}
