namespace FmpDotNet.Tests;

/// <summary>The sector vocabulary, checked against the capture taken live 2026-08-29.</summary>
public class SectorTests
{
    /// <summary>The eleven wire labels, exactly as `stable/available-sectors` returned them on 2026-08-29.</summary>
    public static TheoryData<Sector, string> WireLabels => new()
    {
        { Sector.BasicMaterials, "Basic Materials" },
        { Sector.CommunicationServices, "Communication Services" },
        { Sector.ConsumerCyclical, "Consumer Cyclical" },
        { Sector.ConsumerDefensive, "Consumer Defensive" },
        { Sector.Energy, "Energy" },
        { Sector.FinancialServices, "Financial Services" },
        { Sector.Healthcare, "Healthcare" },
        { Sector.Industrials, "Industrials" },
        { Sector.RealEstate, "Real Estate" },
        { Sector.Technology, "Technology" },
        { Sector.Utilities, "Utilities" },
    };

    [Theory]
    [MemberData(nameof(WireLabels))]
    public void Every_member_maps_to_its_wire_label(Sector sector, string expected)
        => Assert.Equal(expected, sector.ToQueryValue());

    [Fact]
    public void The_enum_covers_the_measured_vocabulary_and_nothing_else()
    {
        // `stable/available-sectors` returned exactly 11 rows on 2026-08-29, and every unfiltered sector
        // snapshot taken — eight of them, across five dates and three exchanges — carried exactly those 11
        // names. A twelfth member here would be a name FMP was never measured to accept.
        Assert.Equal(11, Enum.GetValues<Sector>().Length);
        Assert.Equal(11, WireLabels.Count);
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_the_wire()
    {
        // An unrecognised sector answers HTTP 200 with `[]`, measured 2026-08-29 with `sector=Technlogy`. A
        // value that escaped this method would surface as "a quiet day" rather than as an argument error.
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Sector)999).ToQueryValue());
    }
}
