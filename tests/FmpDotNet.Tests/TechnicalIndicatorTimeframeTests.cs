namespace FmpDotNet.Tests;

/// <summary>The timeframe enum, against the seven values measured valid on 2026-08-29.</summary>
public class TechnicalIndicatorTimeframeTests
{
    [Theory]
    [InlineData(TechnicalIndicatorTimeframe.OneMinute, "1min")]
    [InlineData(TechnicalIndicatorTimeframe.FiveMinutes, "5min")]
    [InlineData(TechnicalIndicatorTimeframe.FifteenMinutes, "15min")]
    [InlineData(TechnicalIndicatorTimeframe.ThirtyMinutes, "30min")]
    [InlineData(TechnicalIndicatorTimeframe.OneHour, "1hour")]
    [InlineData(TechnicalIndicatorTimeframe.FourHours, "4hour")]
    [InlineData(TechnicalIndicatorTimeframe.OneDay, "1day")]
    public void Each_member_maps_to_the_value_FMP_accepts(TechnicalIndicatorTimeframe timeframe, string expected) =>
        Assert.Equal(expected, timeframe.ToQueryValue());

    [Fact]
    public void Every_declared_member_has_a_mapping()
    {
        // Guards the reverse direction of the theory above: a member added without a switch arm would otherwise
        // only be caught when a caller happened to pass it.
        foreach (var member in Enum.GetValues<TechnicalIndicatorTimeframe>())
            Assert.False(string.IsNullOrEmpty(member.ToQueryValue()));
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_FMP()
    {
        // Measured 2026-08-29: `1week`, `1month` and `2hour` all answer HTTP 400 with the body
        // `Invalid timeframe provided.` Throwing here spends no call from the key's quota to learn that.
        var undeclared = (TechnicalIndicatorTimeframe)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToQueryValue());
    }
}
