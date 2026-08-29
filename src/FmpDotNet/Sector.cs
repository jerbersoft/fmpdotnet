namespace FmpDotNet;

/// <summary>The sector asked of the Market Performance sector paths.
///
/// <para><b>An enum because a wrong name is not reported.</b> Measured 2026-08-29,
/// <c>stable/historical-sector-performance?sector=Technlogy</c> answers <b>HTTP 200</b> with <c>[]</c> — a typo
/// and a genuinely quiet day are the same response. The same is true of the snapshot paths.</para>
///
/// <para><b>Eleven members, and the set is complete as measured rather than as documented.</b>
/// <c>stable/available-sectors</c> returned 11 rows on 2026-08-29, and every unfiltered sector snapshot taken
/// that day — eight of them, across five dates and three exchanges — carried exactly those 11 names, no more
/// and no fewer.</para>
///
/// <para><b>This buys typo-safety, not casing-safety, and the difference matters.</b>
/// <see cref="EconomicIndicator"/> is case-<i>sensitive</i> upstream: <c>GDP</c> works and <c>gdp</c> does not.
/// Sector is not — measured 2026-08-29, <c>sector=technology</c> answered a response <b>byte-identical</b> to
/// <c>sector=Technology</c>. Do not read the two enums as carrying the same guarantee.</para>
///
/// <para><b>There is deliberately no equivalent for industry.</b> <c>stable/available-industries</c> lists 159
/// names, of which only <b>139</b> appear in any snapshot on either NASDAQ or NYSE. Twenty documented
/// industries — <c>Banks</c>, <c>Asset Management</c>, <c>Environmental Services</c>, <c>Silver</c> and
/// <c>Media &amp; Entertainment</c> among them — answer <c>[]</c> on every exchange. An enum whose members are
/// one-in-eight measured to fail silently would promise a validity it cannot deliver, so industry is a
/// <see langword="string"/> on <c>MarketPerformanceEndpoints</c> and the caller reads the live vocabulary from
/// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/>.</para></summary>
public enum Sector
{
    /// <summary>Wire <c>Basic Materials</c>.</summary>
    BasicMaterials,

    /// <summary>Wire <c>Communication Services</c>.</summary>
    CommunicationServices,

    /// <summary>Wire <c>Consumer Cyclical</c>.</summary>
    ConsumerCyclical,

    /// <summary>Wire <c>Consumer Defensive</c>.</summary>
    ConsumerDefensive,

    /// <summary>Wire <c>Energy</c>.</summary>
    Energy,

    /// <summary>Wire <c>Financial Services</c>.</summary>
    FinancialServices,

    /// <summary>Wire <c>Healthcare</c>.</summary>
    Healthcare,

    /// <summary>Wire <c>Industrials</c>.</summary>
    Industrials,

    /// <summary>Wire <c>Real Estate</c>.</summary>
    RealEstate,

    /// <summary>Wire <c>Technology</c>.</summary>
    Technology,

    /// <summary>Wire <c>Utilities</c>.</summary>
    Utilities,
}

/// <summary>Conversions for <see cref="Sector"/>.</summary>
public static class SectorExtensions
{
    /// <summary>The value FMP expects in the <c>sector=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible: an unrecognised sector is
    /// answered with <b>HTTP 200 and <c>[]</c></b>, measured 2026-08-29, so a value that escaped this method
    /// would reach the caller as an empty result rather than as an error.</para></summary>
    /// <param name="sector">The sector to convert.</param>
    /// <returns>FMP's own label for the sector.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this Sector sector) => sector switch
    {
        Sector.BasicMaterials => "Basic Materials",
        Sector.CommunicationServices => "Communication Services",
        Sector.ConsumerCyclical => "Consumer Cyclical",
        Sector.ConsumerDefensive => "Consumer Defensive",
        Sector.Energy => "Energy",
        Sector.FinancialServices => "Financial Services",
        Sector.Healthcare => "Healthcare",
        Sector.Industrials => "Industrials",
        Sector.RealEstate => "Real Estate",
        Sector.Technology => "Technology",
        Sector.Utilities => "Utilities",
        _ => throw new ArgumentOutOfRangeException(nameof(sector), sector, "Not a known sector."),
    };
}
