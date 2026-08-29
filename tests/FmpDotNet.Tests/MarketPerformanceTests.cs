using System.Text.Json;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The eleven Market Performance paths, checked against captures taken live 2026-08-29.</summary>
public class MarketPerformanceTests
{
    [Fact]
    public void A_mover_binds_all_six_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-biggest-gainers.head.json"),
            FmpJsonContext.Default.ListMarketMover)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("FNGR", rows[0].Symbol);
        Assert.Equal("FingerMotion, Inc.", rows[0].Name);
        Assert.Equal(0.398m, rows[0].Price);
        Assert.Equal(0.2246m, rows[0].Change);
        Assert.Equal(129.5271m, rows[0].ChangePercentage);
        Assert.Equal("NASDAQ", rows[0].Exchange);
    }

    [Fact]
    public void The_movers_third_spelling_of_change_percentage_binds_to_the_house_name()
    {
        // FMP spells this fact three ways: `changePercentage` on quote, `changePercent` on end-of-day, and
        // `changesPercentage` — with the S — here. EndOfDayBar already documents its divergence and normalises
        // the C# name; this follows the same rule. Do NOT "fix" the attribute: the property would then bind
        // nothing, silently, and Binding.Unbound above is the only other thing that would notice.
        var row = JsonSerializer.Deserialize(
            """[{"changesPercentage":129.5271}]""", FmpJsonContext.Default.ListMarketMover)![0];

        Assert.Equal(129.5271m, row.ChangePercentage);
    }

    [Fact]
    public void A_mover_carries_no_date_of_its_own()
    {
        // Measured 2026-08-29: the movers shape is exactly six keys and none of them is a date or a timestamp.
        // The lists describe a session and never name it — cross-checked against `stable/quote?symbol=FNGR`,
        // which returned the identical price, change and percentage with `timestamp 1787947201`
        // (2026-08-28 20:00:01Z). This test fails if a future capture grows a date field, which would mean the
        // model can now answer a question its own doc says it cannot.
        using var wire = JsonDocument.Parse(
            Binding.Fixture("market-performance-biggest-gainers.head.json"));

        var keys = wire.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            ["symbol", "price", "name", "change", "changesPercentage", "exchange"], keys);
    }
}
