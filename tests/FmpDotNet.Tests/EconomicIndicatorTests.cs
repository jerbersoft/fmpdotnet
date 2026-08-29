namespace FmpDotNet.Tests;

/// <summary>The 23 wire strings <c>stable/economic-indicators</c> accepts, pinned verbatim.
///
/// <para>The whole value of this type is that the caller cannot mistype the name, so a test that restated the
/// names loosely would guard nothing. Each of the 23 below was probed individually on 2026-08-29 and each
/// answered HTTP 200 with a well-formed array. An unrecognised name answers 200 with twelve bytes of
/// <c>Invalid name</c>, so a wrong string here does not fail loudly — it produces
/// <see cref="FmpApiException"/> at runtime for a value the compiler accepted.</para></summary>
public class EconomicIndicatorTests
{
    [Theory]
    [InlineData(EconomicIndicator.Gdp, "GDP")]
    [InlineData(EconomicIndicator.RealGdp, "realGDP")]
    [InlineData(EconomicIndicator.NominalPotentialGdp, "nominalPotentialGDP")]
    [InlineData(EconomicIndicator.RealGdpPerCapita, "realGDPPerCapita")]
    [InlineData(EconomicIndicator.FederalFunds, "federalFunds")]
    [InlineData(EconomicIndicator.ConsumerPriceIndex, "CPI")]
    [InlineData(EconomicIndicator.InflationRate, "inflationRate")]
    [InlineData(EconomicIndicator.Inflation, "inflation")]
    [InlineData(EconomicIndicator.RetailSales, "retailSales")]
    [InlineData(EconomicIndicator.ConsumerSentiment, "consumerSentiment")]
    [InlineData(EconomicIndicator.DurableGoods, "durableGoods")]
    [InlineData(EconomicIndicator.UnemploymentRate, "unemploymentRate")]
    [InlineData(EconomicIndicator.TotalNonfarmPayroll, "totalNonfarmPayroll")]
    [InlineData(EconomicIndicator.InitialClaims, "initialClaims")]
    [InlineData(EconomicIndicator.IndustrialProductionTotalIndex, "industrialProductionTotalIndex")]
    [InlineData(EconomicIndicator.NewPrivatelyOwnedHousingUnitsStartedTotalUnits,
        "newPrivatelyOwnedHousingUnitsStartedTotalUnits")]
    [InlineData(EconomicIndicator.TotalVehicleSales, "totalVehicleSales")]
    [InlineData(EconomicIndicator.RetailMoneyFunds, "retailMoneyFunds")]
    [InlineData(EconomicIndicator.SmoothedUsRecessionProbabilities, "smoothedUSRecessionProbabilities")]
    [InlineData(EconomicIndicator.ThreeMonthCertificateOfDepositRate,
        "3MonthOr90DayRatesAndYieldsCertificatesOfDeposit")]
    [InlineData(EconomicIndicator.CreditCardInterestRate,
        "commercialBankInterestRateOnCreditCardPlansAllAccounts")]
    [InlineData(EconomicIndicator.Mortgage30Year, "30YearFixedRateMortgageAverage")]
    [InlineData(EconomicIndicator.Mortgage15Year, "15YearFixedRateMortgageAverage")]
    public void Every_member_sends_the_wire_string_FMP_accepts(EconomicIndicator indicator, string wire) =>
        Assert.Equal(wire, indicator.ToQueryValue());

    [Fact]
    public void All_twenty_three_documented_names_are_covered_and_none_was_added_without_a_test()
    {
        // The Theory above is the guard; this is the guard on the Theory. A member added to the enum without
        // an InlineData row would otherwise ship untested, and the failure mode of an untested member is a
        // 200 carrying `Invalid name`.
        Assert.Equal(23, Enum.GetValues<EconomicIndicator>().Length);
    }

    [Fact]
    public void The_default_value_is_a_valid_indicator()
    {
        // default(EconomicIndicator) is ordinal 0. On an enum that is Gdp, a name measured valid; this is one
        // of the reasons the type is an enum rather than a struct wrapping the wire string, whose default
        // would be a null name.
        Assert.Equal(EconomicIndicator.Gdp, default);
        Assert.Equal("GDP", default(EconomicIndicator).ToQueryValue());
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_sending_something_plausible()
    {
        // The same guard FiscalPeriod.ToQueryValue documents, and it matters more here: an unrecognised name
        // is not rejected by FMP with a 400, it is answered with HTTP 200 and a body that is not JSON.
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => ((EconomicIndicator)999).ToQueryValue());

        Assert.Equal("indicator", thrown.ParamName);
    }
}
