using System.Text.Json.Serialization;
using FmpDotNet.Models;

namespace FmpDotNet.Serialization;

/// <summary>Source-generated metadata for every model the SDK deserialises.
///
/// <para>Every typed endpoint goes through this rather than through reflection, so a consumer can publish trimmed
/// or Native AOT without the SDK silently losing properties. New JSON models must be added here.</para></summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(List<CompanyProfile>))]
[JsonSerializable(typeof(List<IncomeStatement>))]
[JsonSerializable(typeof(List<BalanceSheetStatement>))]
[JsonSerializable(typeof(List<CashFlowStatement>))]
[JsonSerializable(typeof(List<FinancialRatios>))]
[JsonSerializable(typeof(List<KeyMetrics>))]
[JsonSerializable(typeof(List<FinancialGrowth>))]
[JsonSerializable(typeof(List<EnterpriseValues>))]
[JsonSerializable(typeof(List<SharesFloat>))]
[JsonSerializable(typeof(List<SectorName>))]
[JsonSerializable(typeof(List<IndustryName>))]
[JsonSerializable(typeof(List<StockListRow>))]
[JsonSerializable(typeof(List<ActivelyTradingRow>))]
[JsonSerializable(typeof(List<CountryName>))]
[JsonSerializable(typeof(List<CommodityInfo>))]
[JsonSerializable(typeof(List<CryptocurrencyInfo>))]
[JsonSerializable(typeof(List<ForexPair>))]
[JsonSerializable(typeof(List<IndexInfo>))]
[JsonSerializable(typeof(List<ExchangeInfo>))]
[JsonSerializable(typeof(List<FinancialStatementSymbol>))]
[JsonSerializable(typeof(List<TranscriptSymbol>))]
[JsonSerializable(typeof(List<SymbolChange>))]
[JsonSerializable(typeof(List<CikEntry>))]
[JsonSerializable(typeof(List<CompanyNote>))]
[JsonSerializable(typeof(List<DelistedCompany>))]
[JsonSerializable(typeof(List<EmployeeCount>))]
[JsonSerializable(typeof(List<ExecutiveCompensation>))]
[JsonSerializable(typeof(List<ExecutiveCompensationBenchmark>))]
[JsonSerializable(typeof(List<KeyExecutive>))]
[JsonSerializable(typeof(List<MarketCapitalization>))]
[JsonSerializable(typeof(List<MergerAcquisition>))]
[JsonSerializable(typeof(List<StockPeer>))]
[JsonSerializable(typeof(List<ScreenerResult>))]
[JsonSerializable(typeof(List<SymbolSearchResult>))]
[JsonSerializable(typeof(List<CikSearchResult>))]
[JsonSerializable(typeof(List<CusipSearchResult>))]
[JsonSerializable(typeof(List<IsinSearchResult>))]
[JsonSerializable(typeof(List<ExchangeVariant>))]
[JsonSerializable(typeof(List<EconomicRelease>))]
[JsonSerializable(typeof(List<FinancialScores>))]
[JsonSerializable(typeof(List<AnalystEstimate>))]
[JsonSerializable(typeof(List<StockGrade>))]
[JsonSerializable(typeof(List<GradeConsensus>))]
[JsonSerializable(typeof(List<GradeHistory>))]
[JsonSerializable(typeof(List<PriceTargetConsensus>))]
[JsonSerializable(typeof(List<PriceTargetSummary>))]
[JsonSerializable(typeof(List<CompanyRating>))]
[JsonSerializable(typeof(List<EarningsReport>))]
[JsonSerializable(typeof(List<EarningsCalendarEntry>))]
[JsonSerializable(typeof(List<Dividend>))]
[JsonSerializable(typeof(List<StockSplit>))]
[JsonSerializable(typeof(List<IpoCalendarEntry>))]
[JsonSerializable(typeof(List<IpoDisclosure>))]
[JsonSerializable(typeof(List<IpoProspectus>))]
[JsonSerializable(typeof(List<Quote>))]
[JsonSerializable(typeof(List<ShortQuote>))]
[JsonSerializable(typeof(List<AftermarketTrade>))]
[JsonSerializable(typeof(List<AftermarketQuote>))]
[JsonSerializable(typeof(List<PriceChange>))]
[JsonSerializable(typeof(List<EndOfDayPrice>))]
[JsonSerializable(typeof(List<EndOfDayBar>))]
[JsonSerializable(typeof(List<AdjustedEndOfDayBar>))]
[JsonSerializable(typeof(List<IntradayBar>))]
[JsonSerializable(typeof(List<IndustryClassification>))]
[JsonSerializable(typeof(List<SecFiling>))]
[JsonSerializable(typeof(List<SecProfile>))]
[JsonSerializable(typeof(List<SicCodeEntry>))]
[JsonSerializable(typeof(List<FilingQuarter>))]
[JsonSerializable(typeof(List<InstitutionalHolding>))]
[JsonSerializable(typeof(List<HolderAnalytics>))]
[JsonSerializable(typeof(List<HolderIndustryBreakdown>))]
[JsonSerializable(typeof(List<HolderPerformance>))]
[JsonSerializable(typeof(List<IndustryOwnershipSummary>))]
[JsonSerializable(typeof(List<InstitutionalFiling>))]
[JsonSerializable(typeof(List<SymbolPositions>))]
[JsonSerializable(typeof(List<BeneficialOwnership>))]
// The five below were built for the *-bulk CSV surface and are registered here because their per-symbol JSON
// twins carry the identical field set, measured 2026-08-27. Every property still carries [JsonPropertyName], but
// measured 2026-08-27 only 108 of the 237 attributes across these five types are load-bearing — the TTM suffixes
// on RatiosTtm and KeyMetricsTtm, FMP's four "Activites" typos on CashFlowGrowth, and two abbreviation mismatches
// on IncomeStatementGrowth. The rest, including all 56 on BalanceSheetGrowth, would bind the same way from the
// C# property name alone; they are kept so the mapping stays explicit rather than contingent on
// PropertyNameCaseInsensitive staying set. See StatementReuseBindingTests.
[JsonSerializable(typeof(List<RatiosTtm>))]
[JsonSerializable(typeof(List<KeyMetricsTtm>))]
[JsonSerializable(typeof(List<IncomeStatementGrowth>))]
[JsonSerializable(typeof(List<BalanceSheetGrowth>))]
[JsonSerializable(typeof(List<CashFlowGrowth>))]
[JsonSerializable(typeof(List<AsReportedStatement>))]
[JsonSerializable(typeof(List<RevenueSegmentation>))]
[JsonSerializable(typeof(List<OwnerEarnings>))]
[JsonSerializable(typeof(List<LatestFinancialStatement>))]
[JsonSerializable(typeof(List<FinancialReportLink>))]
// Not a list — the only object-shaped response in this slice. See FinancialReport.
[JsonSerializable(typeof(FinancialReport))]
// Not an endpoint response. `price-target-summary-bulk` carries a JSON array inside one of its CSV fields, and
// BulkPriceTargetSummary parses it — through the source generator like everything else, because this assembly
// declares IsAotCompatible and a reflection-based Deserialize would fail the build on IL2026/IL3050.
[JsonSerializable(typeof(List<string>))]
internal sealed partial class FmpJsonContext : JsonSerializerContext;
