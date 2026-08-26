using System.Text.Json.Serialization;
using FinancialModelingPrep.Models;

namespace FinancialModelingPrep.Serialization;

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
internal sealed partial class FmpJsonContext : JsonSerializerContext;
