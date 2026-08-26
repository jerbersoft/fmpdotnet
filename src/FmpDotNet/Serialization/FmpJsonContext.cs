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
[JsonSerializable(typeof(List<EconomicRelease>))]
[JsonSerializable(typeof(List<FinancialScores>))]
[JsonSerializable(typeof(List<AnalystEstimate>))]
[JsonSerializable(typeof(List<EarningsReport>))]
[JsonSerializable(typeof(List<EarningsCalendarEntry>))]
// Not an endpoint response. `price-target-summary-bulk` carries a JSON array inside one of its CSV fields, and
// BulkPriceTargetSummary parses it — through the source generator like everything else, because this assembly
// declares IsAotCompatible and a reflection-based Deserialize would fail the build on IL2026/IL3050.
[JsonSerializable(typeof(List<string>))]
internal sealed partial class FmpJsonContext : JsonSerializerContext;
