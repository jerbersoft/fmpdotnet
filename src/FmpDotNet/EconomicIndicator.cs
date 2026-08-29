namespace FmpDotNet;

/// <summary>The series asked of <c>GetIndicatorAsync</c>.
///
/// <para><b>Deliberately not a string, and the reason is that the endpoint does not reject a wrong one.</b>
/// Measured 2026-08-29, <c>stable/economic-indicators?name=gdp</c> answers <b>HTTP 200</b>,
/// <c>content-type: application/json; charset=utf-8</c>, and twelve bytes of <c>Invalid name</c> that are not
/// JSON at all. The name is <b>case-sensitive</b> — <c>GDP</c> works and <c>gdp</c> does not — so the
/// difference between a working call and a failing one is one keystroke that no status code reports.</para>
///
/// <para>All 23 names FMP documents were probed individually on 2026-08-29 and all 23 answered a well-formed
/// array, so the set below is complete as measured rather than merely as documented. The members are renamed
/// from the wire — two wire names begin with a digit, and the rest carry FMP's own inconsistent casing — and
/// <see cref="EconomicIndicatorExtensions.ToQueryValue"/> holds the mapping.</para>
///
/// <para><b>Two members return an empty array rather than rows</b>, measured the same day:
/// <see cref="Inflation"/> and <see cref="ThreeMonthCertificateOfDepositRate"/>. They are valid names carrying
/// no data, not invalid names, and they are kept for that reason — dropping them would leave a caller unable
/// to ask, and unable to tell "FMP has no data" from "this SDK omitted it".</para>
///
/// <para><b>The whole endpoint is stale.</b> Measured 2026-08-29, the newest row on every one of the 21
/// non-empty series is dated between 2025-10-01 and 2025-11-26 — nine months earlier. A caller asking for a
/// window computed from today gets an empty array with HTTP 200. See <c>GetIndicatorAsync</c>.</para></summary>
public enum EconomicIndicator
{
    /// <summary>Nominal gross domestic product, quarterly — wire <c>GDP</c>. Newest row measured 2026-08-29:
    /// 2025-10-01.</summary>
    Gdp,

    /// <summary>Inflation-adjusted gross domestic product, quarterly — wire <c>realGDP</c>.</summary>
    RealGdp,

    /// <summary>Nominal potential gross domestic product, quarterly — wire
    /// <c>nominalPotentialGDP</c>.</summary>
    NominalPotentialGdp,

    /// <summary>Real gross domestic product per head, quarterly — wire <c>realGDPPerCapita</c>.</summary>
    RealGdpPerCapita,

    /// <summary>The effective federal funds rate, monthly — wire <c>federalFunds</c>.</summary>
    FederalFunds,

    /// <summary>The consumer price index, monthly — wire <c>CPI</c>, which is the one uppercase name FMP does
    /// not decorate further.</summary>
    ConsumerPriceIndex,

    /// <summary>The rate of change in consumer prices — wire <c>inflationRate</c>. Not the same series as
    /// <see cref="Inflation"/>, which carries no rows at all.</summary>
    InflationRate,

    /// <summary>Wire <c>inflation</c>. <b>Answers a well-formed empty array</b>, measured 2026-08-29 — a
    /// valid name with no data behind it. <see cref="InflationRate"/> is the series a caller almost certainly
    /// wants.</summary>
    Inflation,

    /// <summary>Retail and food-services sales, monthly — wire <c>retailSales</c>.</summary>
    RetailSales,

    /// <summary>The University of Michigan consumer sentiment index, monthly — wire
    /// <c>consumerSentiment</c>.</summary>
    ConsumerSentiment,

    /// <summary>New orders for durable goods, monthly — wire <c>durableGoods</c>.</summary>
    DurableGoods,

    /// <summary>The headline unemployment rate, monthly — wire <c>unemploymentRate</c>.</summary>
    UnemploymentRate,

    /// <summary>Total non-farm payroll employment, monthly — wire <c>totalNonfarmPayroll</c>.</summary>
    TotalNonfarmPayroll,

    /// <summary>Initial jobless claims, weekly — wire <c>initialClaims</c>.</summary>
    InitialClaims,

    /// <summary>Industrial production, total index, monthly — wire
    /// <c>industrialProductionTotalIndex</c>.</summary>
    IndustrialProductionTotalIndex,

    /// <summary>Housing starts, total units, monthly — wire
    /// <c>newPrivatelyOwnedHousingUnitsStartedTotalUnits</c>.</summary>
    NewPrivatelyOwnedHousingUnitsStartedTotalUnits,

    /// <summary>Total vehicle sales, monthly — wire <c>totalVehicleSales</c>.</summary>
    TotalVehicleSales,

    /// <summary>Retail money funds, monthly — wire <c>retailMoneyFunds</c>.</summary>
    RetailMoneyFunds,

    /// <summary>Smoothed US recession probabilities, monthly — wire
    /// <c>smoothedUSRecessionProbabilities</c>.</summary>
    SmoothedUsRecessionProbabilities,

    /// <summary>Three-month certificate-of-deposit rates — wire
    /// <c>3MonthOr90DayRatesAndYieldsCertificatesOfDeposit</c>, which begins with a digit and so cannot be a
    /// C# identifier.
    ///
    /// <para><b>Answers a well-formed empty array</b>, measured 2026-08-29 — like
    /// <see cref="Inflation"/>, a valid name with no data behind it.</para></summary>
    ThreeMonthCertificateOfDepositRate,

    /// <summary>The average interest rate on credit card plans at commercial banks — wire
    /// <c>commercialBankInterestRateOnCreditCardPlansAllAccounts</c>.</summary>
    CreditCardInterestRate,

    /// <summary>The 30-year fixed-rate mortgage average, weekly — wire
    /// <c>30YearFixedRateMortgageAverage</c>, which begins with a digit.</summary>
    Mortgage30Year,

    /// <summary>The 15-year fixed-rate mortgage average, weekly — wire
    /// <c>15YearFixedRateMortgageAverage</c>, which begins with a digit.</summary>
    Mortgage15Year,
}

/// <summary>Conversions for <see cref="EconomicIndicator"/>.</summary>
public static class EconomicIndicatorExtensions
{
    /// <summary>The value FMP expects in the <c>name=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible, and the reason is
    /// sharper here than on <see cref="FiscalPeriod"/>: an unrecognised <c>name</c> is not answered with a
    /// 400. Measured 2026-08-29 it answers <b>HTTP 200</b> and twelve bytes of <c>Invalid name</c>, which is
    /// not JSON — so a value that escaped this method would surface as a deserialisation failure in the
    /// transport rather than as an argument error at the call site.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this EconomicIndicator indicator) => indicator switch
    {
        EconomicIndicator.Gdp => "GDP",
        EconomicIndicator.RealGdp => "realGDP",
        EconomicIndicator.NominalPotentialGdp => "nominalPotentialGDP",
        EconomicIndicator.RealGdpPerCapita => "realGDPPerCapita",
        EconomicIndicator.FederalFunds => "federalFunds",
        EconomicIndicator.ConsumerPriceIndex => "CPI",
        EconomicIndicator.InflationRate => "inflationRate",
        EconomicIndicator.Inflation => "inflation",
        EconomicIndicator.RetailSales => "retailSales",
        EconomicIndicator.ConsumerSentiment => "consumerSentiment",
        EconomicIndicator.DurableGoods => "durableGoods",
        EconomicIndicator.UnemploymentRate => "unemploymentRate",
        EconomicIndicator.TotalNonfarmPayroll => "totalNonfarmPayroll",
        EconomicIndicator.InitialClaims => "initialClaims",
        EconomicIndicator.IndustrialProductionTotalIndex => "industrialProductionTotalIndex",
        EconomicIndicator.NewPrivatelyOwnedHousingUnitsStartedTotalUnits
            => "newPrivatelyOwnedHousingUnitsStartedTotalUnits",
        EconomicIndicator.TotalVehicleSales => "totalVehicleSales",
        EconomicIndicator.RetailMoneyFunds => "retailMoneyFunds",
        EconomicIndicator.SmoothedUsRecessionProbabilities => "smoothedUSRecessionProbabilities",
        EconomicIndicator.ThreeMonthCertificateOfDepositRate
            => "3MonthOr90DayRatesAndYieldsCertificatesOfDeposit",
        EconomicIndicator.CreditCardInterestRate
            => "commercialBankInterestRateOnCreditCardPlansAllAccounts",
        EconomicIndicator.Mortgage30Year => "30YearFixedRateMortgageAverage",
        EconomicIndicator.Mortgage15Year => "15YearFixedRateMortgageAverage",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known economic indicator."),
    };
}
