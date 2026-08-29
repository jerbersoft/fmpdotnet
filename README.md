# FmpDotNet

A .NET 10 SDK for the [Financial Modeling Prep](https://site.financialmodelingprep.com/developer/docs) `stable` API.

The root namespace and assembly are `FmpDotNet`. That is deliberately not the vendor's own name: a package
called `FinancialModelingPrep` reads as something FMP publishes and supports, and this is an independent
client. Types keep the `Fmp` prefix (`FmpClient`, `FmpOptions`, `FmpTransport`) because they name the API
being spoken to, not the publisher.

Built to be adopted by the `trader` repository, so build order follows what trader calls rather than what FMP
documents first: FMP documents 243 unique `stable/` paths across 29 sections — the asset-class sections
re-document `/stable/quote` and friends rather than adding endpoints. That count was
[enumerated and cross-checked](docs/superpowers/specs/2026-08-27-endpoint-inventory.md) against two independent
sources on 2026-08-27. See [endpoint coverage](#endpoint-coverage) for exactly which of them are modelled, and how
to reach the rest.

## Status

Every endpoint `Trader.Adapters.MarketData.Fmp` calls is modelled, which is what that adapter's removal was
waiting on — along with the whole `*-bulk` surface and the universe and directory lists. The supporting
machinery is in place too: options and validation, `AddFmp`, the two throttle reservoirs, per-attempt timeouts,
the JSON and CSV pipelines, and a developer disk cache for bulk responses.

The upstream behaviour recorded throughout this README was measured rather than read from the documentation, and
it is re-checked against the live API every week — see [the live smoke suite](#the-live-smoke-suite).

## Usage

```csharp
using FmpDotNet;
using FmpDotNet.DependencyInjection;
using FmpDotNet.Models;
using NodaTime;

services.AddFmp(configuration);              // binds the "Fmp" section
// or
services.AddFmp(o => o.ApiKey = "…");

var fmp = provider.GetRequiredService<FmpClient>();

var profile = await fmp.Company.GetProfileAsync("AAPL");

// The period-shaped endpoints share one signature: symbol, cadence, limit.
var income  = await fmp.Statements.GetIncomeStatementAsync("AAPL", FiscalPeriod.Annual, limit: 5);
var ratios  = await fmp.Statements.GetRatiosAsync("AAPL", FiscalPeriod.Quarter, limit: 8);

// Share float. One row or none — the endpoint holds no history.
var shares = await fmp.Company.GetSharesFloatAsync("AAPL");

// The reference vocabularies the profile's `sector` and `industry` are drawn from.
IReadOnlyList<string> sectors    = await fmp.Directory.GetSectorsAsync();
IReadOnlyList<string> industries = await fmp.Directory.GetIndustriesAsync();

// The symbol universe. `actively-trading` is a strict subset of `stock-list` — measured, every
// symbol, no exceptions — so the difference is a defined set, not an inference.
var listed = await fmp.Directory.GetStockListAsync();          // 91,844 symbols
var live   = await fmp.Directory.GetActivelyTradingAsync();    // 68,869 symbols

// The delisting archive, newest first. 100 rows per page is a hard cap, not a default:
// asking for more returns the same 100 with HTTP 200. GetDelistedAsync rejects it instead.
var gone = await fmp.Company.GetDelistedAsync(page: 0, limit: 100);

// Screening. Unset properties are never sent, so an empty ScreenerCriteria is a request for
// the whole universe rather than a request for nothing.
var large = await fmp.Search.ScreenAsync(new ScreenerCriteria
{
    MarketCapMoreThan = 10_000_000_000m,
    Sector = "Technology",     // spelling must come from GetSectorsAsync — see below
    Country = "US",
    IsEtf = false,
    Limit = 500,
});

// Altman Z and Piotroski, plus the seven figures the Z score is computed from.
var scores = await fmp.Statements.GetScoresAsync("AAPL");

// Earnings history, newest first — note the head row is usually the NEXT report, unreported.
var earnings = await fmp.Calendar.GetEarningsAsync("AAPL", limit: 8);

// Forward consensus. `Period` is stamped from the request, so annual and quarterly rows
// stay distinguishable when concatenated — their fiscal period ends collide otherwise.
var annual  = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Annual, limit: 5);
var quarter = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter, limit: 5);

// The whole-market earnings calendar. It truncates silently at 4000 rows, so ask.
var day = new LocalDate(2026, 5, 13);
var cal = await fmp.Calendar.GetEarningsCalendarAsync(day, day, includeReportTimes: true);
if (EarningsCalendarResult.IsLikelyTruncated(cal))
    { /* narrow the range and retry — see the note below */ }

// Macro releases. Global and unfiltered: filtering by country or impact is yours to do.
var macro = await fmp.Economics.GetEconomicCalendarAsync(day, day.PlusDays(7));
var et = DateTimeZoneProviders.Tzdb["America/New_York"];
foreach (var r in macro.Where(r => r.Country == "US" && r.Impact == "High"))
    Console.WriteLine($"{r.Timestamp?.InZone(et).LocalDateTime} {r.Event}");

await foreach (var bar in fmp.Bulk.StreamEndOfDayAsync(new LocalDate(2025, 10, 22), ct))
    Console.WriteLine($"{bar.Symbol} {bar.Close}");

// The whole-universe profile feed, streamed a part at a time.
await foreach (var p in fmp.Bulk.StreamAllProfilesAsync(ct))
    Console.WriteLine($"{p.Symbol} {p.Sector} {p.Industry}");
```

## Endpoint coverage

**The table below is generated from the code**, not maintained by hand. Every public method is driven against a
stub and the path it actually requests is recorded, so renaming a method, deleting one, or adding an endpoint
without a table entry fails the build rather than leaving a page that reads as current.

<!-- BEGIN GENERATED: endpoint coverage -->
<!-- Generated from the code by EndpointCoverageTests. Do not edit by hand — run
     `FMPDOTNET_UPDATE_README=1 dotnet test` and commit the result. -->

**178 of FMP's 243 endpoint paths are modelled.**

`fmp.Analyst`

| FMP endpoint | Method |
|---|---|
| `stable/analyst-estimates` | `GetEstimatesAsync` |
| `stable/grades` | `GetGradesAsync` |
| `stable/grades-consensus` | `GetGradeConsensusAsync` |
| `stable/grades-historical` | `GetGradeHistoryAsync` |
| `stable/price-target-consensus` | `GetPriceTargetConsensusAsync` |
| `stable/price-target-summary` | `GetPriceTargetSummaryAsync` |
| `stable/ratings-historical` | `GetRatingHistoryAsync` |
| `stable/ratings-snapshot` | `GetRatingAsync` |

`fmp.Bulk`

| FMP endpoint | Method |
|---|---|
| `stable/balance-sheet-statement-bulk` | `StreamBalanceSheetsAsync` |
| `stable/balance-sheet-statement-growth-bulk` | `StreamBalanceSheetGrowthAsync` |
| `stable/cash-flow-statement-bulk` | `StreamCashFlowsAsync` |
| `stable/cash-flow-statement-growth-bulk` | `StreamCashFlowGrowthAsync` |
| `stable/dcf-bulk` | `StreamDiscountedCashFlowsAsync` |
| `stable/earnings-surprises-bulk` | `StreamEarningsSurprisesAsync` |
| `stable/eod-bulk` | `StreamEndOfDayAsync` |
| `stable/etf-holder-bulk` | `StreamAllEtfHoldingsAsync`, `StreamEtfHoldingsAsync` |
| `stable/income-statement-bulk` | `StreamIncomeStatementsAsync` |
| `stable/income-statement-growth-bulk` | `StreamIncomeStatementGrowthAsync` |
| `stable/key-metrics-ttm-bulk` | `StreamKeyMetricsTtmAsync` |
| `stable/peers-bulk` | `StreamPeersAsync` |
| `stable/price-target-summary-bulk` | `StreamPriceTargetSummariesAsync` |
| `stable/profile-bulk` | `StreamAllProfilesAsync`, `StreamProfilesAsync` |
| `stable/rating-bulk` | `StreamRatingsAsync` |
| `stable/ratios-ttm-bulk` | `StreamRatiosTtmAsync` |
| `stable/scores-bulk` | `StreamScoresAsync` |
| `stable/upgrades-downgrades-consensus-bulk` | `StreamAnalystConsensusAsync` |

`fmp.Calendar`

| FMP endpoint | Method |
|---|---|
| `stable/dividends` | `GetDividendsAsync` |
| `stable/dividends-calendar` | `GetDividendsCalendarAsync` |
| `stable/earnings` | `GetEarningsAsync` |
| `stable/earnings-calendar` | `GetEarningsCalendarAsync` |
| `stable/ipos-calendar` | `GetIpoCalendarAsync` |
| `stable/ipos-disclosure` | `GetIpoDisclosuresAsync` |
| `stable/ipos-prospectus` | `GetIpoProspectusesAsync` |
| `stable/splits` | `GetSplitsAsync` |
| `stable/splits-calendar` | `GetSplitsCalendarAsync` |

`fmp.Chart`

| FMP endpoint | Method |
|---|---|
| `stable/historical-chart/15min` | `GetIntradayAsync` |
| `stable/historical-chart/1hour` | `GetIntradayAsync` |
| `stable/historical-chart/1min` | `GetIntradayAsync` |
| `stable/historical-chart/30min` | `GetIntradayAsync` |
| `stable/historical-chart/4hour` | `GetIntradayAsync` |
| `stable/historical-chart/5min` | `GetIntradayAsync` |
| `stable/historical-price-eod/dividend-adjusted` | `GetDividendAdjustedAsync` |
| `stable/historical-price-eod/full` | `GetEndOfDayFullAsync` |
| `stable/historical-price-eod/light` | `GetEndOfDayAsync` |
| `stable/historical-price-eod/non-split-adjusted` | `GetUnadjustedAsync` |

`fmp.Company`

| FMP endpoint | Method |
|---|---|
| `stable/company-notes` | `GetNotesAsync` |
| `stable/delisted-companies` | `GetDelistedAsync` |
| `stable/employee-count` | `GetEmployeeCountAsync` |
| `stable/executive-compensation-benchmark` | `GetExecutiveCompensationBenchmarkAsync` |
| `stable/governance-executive-compensation` | `GetExecutiveCompensationAsync` |
| `stable/historical-employee-count` | `GetHistoricalEmployeeCountAsync` |
| `stable/historical-market-capitalization` | `GetHistoricalMarketCapAsync` |
| `stable/key-executives` | `GetKeyExecutivesAsync` |
| `stable/market-capitalization` | `GetMarketCapAsync` |
| `stable/market-capitalization-batch` | `GetMarketCapBatchAsync` |
| `stable/mergers-acquisitions-latest` | `GetLatestMergersAcquisitionsAsync` |
| `stable/mergers-acquisitions-search` | `SearchMergersAcquisitionsAsync` |
| `stable/profile` | `GetProfileAsync` |
| `stable/profile-cik` | `GetProfileByCikAsync` |
| `stable/shares-float` | `GetSharesFloatAsync` |
| `stable/shares-float-all` | `GetAllSharesFloatAsync` |
| `stable/stock-peers` | `GetPeersAsync` |

`fmp.Congress`

| FMP endpoint | Method |
|---|---|
| `stable/house-latest` | `GetHouseLatestAsync` |
| `stable/house-trades` | `GetHouseTradesAsync` |
| `stable/house-trades-by-id` | `GetHouseTradesByMemberAsync` |
| `stable/house-trades-by-name` | `GetHouseTradesByNameAsync` |
| `stable/senate-latest` | `GetSenateLatestAsync` |
| `stable/senate-net-worth` | `GetNetWorthAsync` |
| `stable/senate-net-worth-aggregated` | `GetNetWorthSummaryAsync` |
| `stable/senate-positions` | `GetPositionsAsync` |
| `stable/senate-profile` | `GetProfilesAsync` |
| `stable/senate-trades` | `GetSenateTradesAsync` |
| `stable/senate-trades-by-id` | `GetSenateTradesByMemberAsync` |
| `stable/senate-trades-by-name` | `GetSenateTradesByNameAsync` |

`fmp.Cot`

| FMP endpoint | Method |
|---|---|
| `stable/commitment-of-traders-analysis` | `GetAnalysisAsync` |
| `stable/commitment-of-traders-list` | `GetSymbolsAsync` |
| `stable/commitment-of-traders-report` | `GetReportAsync` |

`fmp.Directory`

| FMP endpoint | Method |
|---|---|
| `stable/actively-trading-list` | `GetActivelyTradingAsync` |
| `stable/all-industry-classification` | `GetAllIndustryClassificationsAsync`, `GetIndustryClassificationsAsync` |
| `stable/available-countries` | `GetCountriesAsync` |
| `stable/available-exchanges` | `GetExchangesAsync` |
| `stable/available-industries` | `GetIndustriesAsync` |
| `stable/available-sectors` | `GetSectorsAsync` |
| `stable/cik-list` | `GetCikListAsync`, `StreamCikListAsync` |
| `stable/commodities-list` | `GetCommodityListAsync` |
| `stable/cryptocurrency-list` | `GetCryptocurrencyListAsync` |
| `stable/earnings-transcript-list` | `GetTranscriptSymbolsAsync` |
| `stable/etf-list` | `GetEtfListAsync` |
| `stable/financial-statement-symbol-list` | `GetFinancialStatementSymbolsAsync` |
| `stable/forex-list` | `GetForexListAsync` |
| `stable/index-list` | `GetIndexListAsync` |
| `stable/standard-industrial-classification-list` | `GetSicCodesAsync` |
| `stable/stock-list` | `GetStockListAsync` |
| `stable/symbol-change` | `GetSymbolChangesAsync` |

`fmp.Economics`

| FMP endpoint | Method |
|---|---|
| `stable/economic-calendar` | `GetEconomicCalendarAsync` |
| `stable/economic-indicators` | `GetIndicatorAsync` |
| `stable/market-risk-premium` | `GetMarketRiskPremiumsAsync` |
| `stable/treasury-rates` | `GetTreasuryRatesAsync` |

`fmp.Esg`

| FMP endpoint | Method |
|---|---|
| `stable/esg-benchmark` | `GetBenchmarkAsync` |
| `stable/esg-disclosures` | `GetDisclosuresAsync` |
| `stable/esg-ratings` | `GetRatingsAsync` |

`fmp.InsiderTrades`

| FMP endpoint | Method |
|---|---|
| `stable/insider-trading-transaction-type` | `GetTransactionTypesAsync` |
| `stable/insider-trading/latest` | `GetLatestAsync` |
| `stable/insider-trading/reporting-name` | `SearchReportingNameAsync` |
| `stable/insider-trading/search` | `SearchAsync` |
| `stable/insider-trading/statistics` | `GetStatisticsAsync` |

`fmp.InstitutionalOwnership`

| FMP endpoint | Method |
|---|---|
| `stable/acquisition-of-beneficial-ownership` | `GetBeneficialOwnershipAsync` |
| `stable/institutional-ownership/dates` | `GetFilingDatesAsync` |
| `stable/institutional-ownership/extract` | `GetHoldingsAsync` |
| `stable/institutional-ownership/extract-analytics/holder` | `GetHolderAnalyticsAsync` |
| `stable/institutional-ownership/holder-industry-breakdown` | `GetHolderIndustryBreakdownAsync` |
| `stable/institutional-ownership/holder-performance-summary` | `GetHolderPerformanceAsync` |
| `stable/institutional-ownership/industry-summary` | `GetIndustrySummaryAsync` |
| `stable/institutional-ownership/latest` | `GetLatestFilingsAsync` |
| `stable/institutional-ownership/symbol-positions-summary` | `GetSymbolPositionsAsync` |

`fmp.Quote`

| FMP endpoint | Method |
|---|---|
| `stable/aftermarket-quote` | `GetAftermarketQuoteAsync` |
| `stable/aftermarket-trade` | `GetAftermarketTradeAsync` |
| `stable/batch-aftermarket-quote` | `GetAftermarketQuotesAsync` |
| `stable/batch-aftermarket-trade` | `GetAftermarketTradesAsync` |
| `stable/batch-commodity-quotes` | `GetCommodityQuotesAsync`, `GetCommodityQuotesFullAsync` |
| `stable/batch-crypto-quotes` | `GetCryptoQuotesAsync`, `GetCryptoQuotesFullAsync` |
| `stable/batch-etf-quotes` | `GetEtfQuotesAsync`, `GetEtfQuotesFullAsync` |
| `stable/batch-exchange-quote` | `GetExchangeQuotesAsync`, `GetExchangeQuotesFullAsync` |
| `stable/batch-forex-quotes` | `GetForexQuotesAsync`, `GetForexQuotesFullAsync` |
| `stable/batch-index-quotes` | `GetIndexQuotesAsync`, `GetIndexQuotesFullAsync` |
| `stable/batch-mutualfund-quotes` | `GetMutualFundQuotesAsync`, `GetMutualFundQuotesFullAsync` |
| `stable/batch-quote` | `GetQuotesAsync` |
| `stable/batch-quote-short` | `GetShortQuotesAsync` |
| `stable/quote` | `GetQuoteAsync` |
| `stable/quote-short` | `GetShortQuoteAsync` |
| `stable/stock-price-change` | `GetPriceChangeAsync` |

`fmp.Search`

| FMP endpoint | Method |
|---|---|
| `stable/company-screener` | `ScreenAsync` |
| `stable/industry-classification-search` | `FindIndustryClassificationAsync` |
| `stable/search-cik` | `FindByCikAsync` |
| `stable/search-cusip` | `FindByCusipAsync` |
| `stable/search-exchange-variants` | `GetExchangeVariantsAsync` |
| `stable/search-isin` | `FindByIsinAsync` |
| `stable/search-name` | `FindByNameAsync` |
| `stable/search-symbol` | `FindBySymbolAsync` |

`fmp.SecFilings`

| FMP endpoint | Method |
|---|---|
| `stable/sec-filings-8k` | `Get8KFilingsAsync` |
| `stable/sec-filings-company-search/cik` | `FindCompanyByCikAsync` |
| `stable/sec-filings-company-search/name` | `FindCompanyByNameAsync` |
| `stable/sec-filings-company-search/symbol` | `FindCompanyBySymbolAsync` |
| `stable/sec-filings-financials` | `GetFilingsWithFinancialsAsync` |
| `stable/sec-filings-search/cik` | `SearchByCikAsync` |
| `stable/sec-filings-search/form-type` | `SearchByFormTypeAsync` |
| `stable/sec-filings-search/symbol` | `SearchBySymbolAsync` |
| `stable/sec-profile` | `GetProfileAsync`, `GetProfileByCikAsync` |

`fmp.Statements`

| FMP endpoint | Method |
|---|---|
| `stable/balance-sheet-statement` | `GetBalanceSheetAsync` |
| `stable/balance-sheet-statement-as-reported` | `GetBalanceSheetAsReportedAsync` |
| `stable/balance-sheet-statement-growth` | `GetBalanceSheetGrowthAsync` |
| `stable/balance-sheet-statement-ttm` | `GetBalanceSheetTtmAsync` |
| `stable/cash-flow-statement` | `GetCashFlowAsync` |
| `stable/cash-flow-statement-as-reported` | `GetCashFlowAsReportedAsync` |
| `stable/cash-flow-statement-growth` | `GetCashFlowGrowthAsync` |
| `stable/cash-flow-statement-ttm` | `GetCashFlowTtmAsync` |
| `stable/enterprise-values` | `GetEnterpriseValuesAsync` |
| `stable/financial-growth` | `GetFinancialGrowthAsync` |
| `stable/financial-reports-dates` | `GetFinancialReportDatesAsync` |
| `stable/financial-reports-json` | `GetFinancialReportAsync` |
| `stable/financial-reports-xlsx` | `GetFinancialReportWorkbookAsync` |
| `stable/financial-scores` | `GetScoresAsync` |
| `stable/financial-statement-full-as-reported` | `GetFullStatementAsReportedAsync` |
| `stable/income-statement` | `GetIncomeStatementAsync` |
| `stable/income-statement-as-reported` | `GetIncomeStatementAsReportedAsync` |
| `stable/income-statement-growth` | `GetIncomeStatementGrowthAsync` |
| `stable/income-statement-ttm` | `GetIncomeStatementTtmAsync` |
| `stable/key-metrics` | `GetKeyMetricsAsync` |
| `stable/key-metrics-ttm` | `GetKeyMetricsTtmAsync` |
| `stable/latest-financial-statements` | `GetLatestStatementsAsync`, `StreamLatestStatementsAsync` |
| `stable/owner-earnings` | `GetOwnerEarningsAsync` |
| `stable/ratios` | `GetRatiosAsync` |
| `stable/ratios-ttm` | `GetRatiosTtmAsync` |
| `stable/revenue-geographic-segmentation` | `GetRevenueByGeographyAsync` |
| `stable/revenue-product-segmentation` | `GetRevenueByProductAsync` |

`fmp.Transcripts`

| FMP endpoint | Method |
|---|---|
| `stable/earning-call-transcript` | `GetTranscriptAsync` |
| `stable/earning-call-transcript-dates` | `GetDatesAsync` |
| `stable/earning-call-transcript-latest` | `GetLatestAsync` |

<!-- END GENERATED: endpoint coverage -->

### Reaching an endpoint that is not modelled

The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **77 paths
remain**, of which **70 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest group is Economics/Transcripts/ESG/COT (12), then
Market Performance (11), News (10) and Fundraisers & DCF (10); ETF & Mutual Funds, Technical Indicators and
Indexes & Market Hours carry 9 apiece.

The balance is lopsided toward equities, and for a structural reason. What has been built so far is price plumbing
— Quote, Chart and Bulk are complete — and one `GetQuoteAsync` serves equities, ETFs, indices, commodities, forex
and crypto alike, so the asset-class breadth came free while the equity depth never got built. The
[endpoint inventory](docs/superpowers/specs/2026-08-27-endpoint-inventory.md) splits the remainder section by
section and marks which side of that line each falls on.

That remainder is tracked as eight issues under the epic, seven of them actionable, each 9 to 12 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 166 modelled plus 77 remaining, with no path counted twice and none missing.

Commodity, Forex and Crypto contribute **one path each** to that remainder — their symbol lists, and
`fmp.Directory` now covers all three. Everything else under those headings, and most of what is under Indexes, is
`stable/quote` and `stable/historical-price-eod` re-documented, which `fmp.Quote` and `fmp.Chart` already reach.
`GetQuoteAsync("BTCUSD")`, `GetQuoteAsync("EURUSD")`, `GetQuoteAsync("^GSPC")` and `GetQuoteAsync("GCUSD")` were
each measured returning the ordinary seventeen-field quote. That re-documentation is why the denominator here is
the unique-path count rather than the larger number of documented API pages.

**`FmpTransport` is public precisely so none of that blocks you.** Reach an unmodelled endpoint through it rather
than building a second `HttpClient`: the transport carries the throttle, the timeout, the 429 handling and the
error classification described below, and a call made any other way has none of them — including the shared
reservoir, so it would not even count against the budget the rest of your calls are pacing themselves within.

The SDK is AOT-compatible and never reflects over your model, so `GetListAsync` takes a `JsonTypeInfo` where a
reflection-based client would take a `T`. Declare a context for your own types:

```csharp
public sealed record RatingSnapshot
{
    [JsonPropertyName("symbol")] public required string Symbol { get; init; }
    [JsonPropertyName("rating")] public string? Rating { get; init; }
}

[JsonSerializable(typeof(List<RatingSnapshot>))]
public sealed partial class MyFmpJson : JsonSerializerContext;
```

Then go through the transport — the same instance the typed endpoints use, resolved from DI:

```csharp
var transport = provider.GetRequiredService<FmpTransport>();

IReadOnlyList<RatingSnapshot> rows = await transport.GetListAsync(
    new FmpRequest("stable/ratings-snapshot").With("symbol", "AAPL"),
    MyFmpJson.Default.ListRatingSnapshot,
    ct);
```

An unmodelled `*-bulk` endpoint goes through `FmpBulkTransport` instead, which is the same transport bound to the
bulk client — the tighter throttle and the ten-minute timeout come with it, and CSV is mapped a row at a time so
nothing buffers:

```csharp
var transport = provider.GetRequiredService<FmpBulkTransport>();

await foreach (var row in transport.StreamCsvAsync(
    new FmpRequest("stable/some-bulk"),
    csv => new MyRow { Symbol = csv.GetString("symbol")!, Price = csv.GetDecimal("price") },
    ct))
{
    // ...
}
```

## Dates and times are NodaTime

The SDK's time surface is NodaTime throughout — `LocalDate`, `Instant`, `Duration`, `IClock`. No `DateOnly`,
`DateTime`, `DateTimeOffset`, `TimeSpan` or `TimeProvider` appears in any public signature.

`TimeSpan` survives only where a BCL API leaves no choice — `Task.Delay`, `CancellationTokenSource.CancelAfter`,
`HttpClient.Timeout`, and the `Retry-After` header's own type — and is converted at that boundary and nowhere else.

Substitute `NodaTime.Testing.FakeClock` for `IClock` to drive throttle behaviour in tests without a real clock.

## Two pipelines, kept apart

FMP keeps them apart, so the SDK does too.

| | Ordinary endpoints | `*-bulk` endpoints |
|---|---|---|
| Format | JSON array | CSV |
| Return shape | `IReadOnlyList<T>` | `IAsyncEnumerable<T>` |
| Payload | kilobytes | up to **69 MB** in one response |
| Throttle | `PerMinuteCap` (default 660) | `BulkPerMinuteCap` (default 2) |
| Timeout | `RequestTimeout` (30 s) | `BulkRequestTimeout` (10 min) |
| Errors | status codes | **also HTTP 200 with a JSON error body** |

`PerMinuteCap` defaults to 660 because that is ~88% of **Premium's 750/min**, the lowest paid tier this SDK
targets, and the emitted rate runs about 10% above target under real concurrency. The default is deliberately not
tuned to the key you hold — one sized for a higher tier would trip 429s for everyone below it. **On a higher tier,
raise it:** Ultimate allows 3,000/min, so leaving the default in place spends roughly a fifth of the budget you are
paying for. `2640` keeps the same headroom on Ultimate.

## Upstream behaviour the SDK handles for you

Measured against the live API on 2026-08-26 unless noted.

- **Bulk errors arrive as HTTP 200.** A throttled bulk call returns `{"Error Message": "Limit Reach…"}` — JSON,
  on an endpoint whose success shape is CSV. `EnsureSuccessStatusCode` passes and a naive CSV parse yields zero
  rows, so a caller sees "no data today" instead of "you were throttled". The transport inspects the payload and
  raises `FmpApiException`.
- **Bulk is throttled separately** from the account's per-minute cap, and much more tightly. FMP warns that
  frequent use "may result in restrictions placed on this API Key". Bulk data refreshes only once every few hours
  — cache a successful download rather than repeating it.
- **Three bulk endpoints send no `Content-Length`** (`profile-bulk`, `etf-holder-bulk`, `eod-bulk`), so nothing
  can pre-size a buffer or show a progress percentage.
- **`acceptedDate` is Eastern, and the economic calendar is UTC.** Both use the same
  `"yyyy-MM-dd HH:mm:ss"` shape with no offset, so the shape tells you nothing. Cross-checked against SEC EDGAR's
  own UTC acceptance times: Apple's 10-K reads `2025-10-31 06:01:26` where EDGAR says `10:01:26Z` (4 hours, EDT),
  and JPM's reads `2026-02-13 16:20:00` where EDGAR says `21:20:00Z` (5 hours, EST). Two different offsets six
  months apart means a fixed `-5` is wrong for half the year, so the SDK converts through the tz database.
  Reading these as UTC — as a naive port would — puts every filing timestamp 4-5 hours early.
- **`enterprise-values` is not shaped like its six siblings.** It sends no `fiscalYear` and no `period`, so a row
  cannot say which series it came from. `period=` *is* still honoured and does change the dates returned, so the
  SDK keeps sending it. Consequence for storage: `(symbol, date)` is **not** a unique key across both cadences,
  because a Q4 end and a fiscal year end are the same day — `2025-09-27` appears in Apple's annual series and its
  quarterly one.
- **`shares-float`'s `date` is UTC — the opposite of `acceptedDate`.** Same `"yyyy-MM-dd HH:mm:ss"` shape as the
  Eastern one above, so the string cannot tell you which is which and the wrong converter is a silent 4-5 hour
  shift. Established by probing 40 symbols: the stamps spread evenly from `00:09:20` to `14:13:45`, the latest
  sitting 26 minutes *before* UTC-now and never ahead of it. Read as Eastern that stamp would be 3.5 hours in the
  future, which a value recording when a row was last refreshed cannot be.
- **Share counts are JSON floating-point.** `floatShares` has been seen as `25595002.125` — a computation artifact
  of outstanding x free-float %. Reading them into `long` throws and aborts the *whole* response, not just the
  field, so the SDK reads `decimal` and lets the caller round. A clean sample proves nothing here; the fractions
  appear intermittently rather than for particular symbols.
- **Class-share tickers need FMP's hyphenated spelling.** `BRK.B` and `BF.B` answer `[]`; `BRK-B` and `BF-B` answer
  a row. It affects `shares-float` and `profile` alike, and it surfaces as an empty result rather than an error, so
  a dotted ticker looks exactly like a symbol FMP has no data for.
- **ETFs report `freeFloat: 0` and `floatShares: 0`** against a real `outstandingShares`, with a null `source` —
  SPY, QQQ, VOO and IWM all do. The zero means "not computed for this security", not "no shares freely tradable",
  so it must not be fed into a float-based calculation as though it were measured.
- **`earnings-calendar` truncates silently at exactly 4000 rows, dropping the *earliest* dates.** One day
  (`2026-05-13`) answers 2039 rows; `from=05-13&to=05-14` answers exactly 4000, of which only 1969 fall on 05-13
  — 70 rows of a day that was complete on its own just vanish, mid-day. A one-week request came back with an
  entire requested day absent. `limit=6000` is accepted and ignored. There is no cursor, so the SDK cannot page
  around it and instead reports it: the returned list is an `EarningsCalendarResult` carrying `RowsReturned`,
  `AtRowCap`, `MissesStartOfRange` and `LikelyTruncated`. **Day-at-a-time is the only chunk width measured to be
  safe** — a 7-day peak-season window measured 3676 rows, 92% of the cap without crossing it.
- **That truncation signal is computed before clamping, and the order is load-bearing.** Filtering the rows first
  and then testing `Count >= 4000` is how a truncated response gets judged complete: measured live, a two-day
  request returned 4000 raw rows that clamping reduced to 3935. `Count` is what you were handed; `RowsReturned` is
  what FMP sent, and only the second can answer the question.
- **`includeReportTimes=true` re-dates rows; it does not add them.** A `from=to=2026-05-13` request returns the
  identical 2039-symbol set either way — but with the flag on, 51 of those rows report `2026-05-14`. None of those
  51 appear in the `2026-05-14` request, checked symbol by symbol, so selection happens on the un-shifted date and
  only the reported date moves. **Clamping to `[from, to]` therefore removes no duplicates — there are none — and
  permanently drops rows no other chunk will ever return.** The SDK returns rows unclamped and offers
  `clampToRange: true` for callers writing into a store that cannot reject a duplicate and would rather lose a row
  than double one. The flag also changes `lastUpdated`, not just `date`.
- **`economic-calendar` truncates wide windows too, but differently** — no row cap to test against, and the
  reduction is not proportional: one month → 1855 rows, three months → 4051, but six months → **535**, fewer than
  the three-month window it contains, and a 15-month window → 0. A row-count guard is the wrong instinct here,
  because macro density legitimately varies enormously: January 2027 really does hold only 2 rows. The honest
  completeness test is whether the returned rows reach both ends of the range you asked for.
- **`analyst-estimates` is ordered furthest-future first, so `limit=N` gives the N most distant estimates**, not
  the next N. Nothing on the wire says which cadence a row came from, and an annual row and a Q4 row share the
  same fiscal period end — so the SDK stamps `Period` from the request. Without it, concatenating an annual and a
  quarterly call silently collapses colliding rows. There is also no revision or as-of stamp anywhere on the
  response: if you need to know when a consensus was struck, stamp it on arrival.
- **`earnings` puts an unreported row at the head.** The list is newest-first and the newest row is the *next*
  report — `epsActual` and `revenueActual` null, estimates populated. "The last N earnings" therefore includes one
  that has not happened. With no `limit` the endpoint returns full history: 165 rows for Apple, back to 1985.
- **`financial-scores` carries no date, and its inputs are not the latest annual statement.** Eleven fields, no
  `date`, no `period`, no `fiscalYear` — nothing says when it was computed, yet it moves: the figures are
  trailing/quote-time, and Apple's `retainedEarnings` and `workingCapital` both come back with the *opposite sign*
  to the FY2025 balance sheet captured the same day. They cannot be reconciled against `balance-sheet-statement`.
  The seven accompanying figures do reproduce the reported Altman Z exactly, which is what they are there for.
- **`profile-bulk` terminates paging with an error, not an empty response.** An out-of-range `part` answers HTTP
  **400** carrying the plain text `Query Error: Invalid or missing query parameter - part` — under a
  `content-type` of `application/json` that is a lie, since the body is not JSON. The transport surfaces that text
  as an `FmpApiException` rather than discarding it behind a bare `HttpRequestException`. `StreamAllProfilesAsync`
  reads a 400 as "past the last part", which is a documented heuristic, not a contract.
- **Neither whole-universe feed's first page is a sample of the universe**, for opposite reasons.
  `shares-float-all` pages *are* symbol-ordered, so page 0 is entirely Shenzhen listings — which is exactly how a
  consumer once read "a partial, mostly foreign page" as a plan restriction when it was simply page zero of a
  global list requested without `page` or `limit`. `profile-bulk` part 0 is *not* symbol-ordered at all. The bulk
  float rows also carry five fields where the per-symbol endpoint carries six: there is no `source`, so a null
  there means "this shape omits it", not "FMP names no source".
- **`available-industries` is not alphabetical.** Its 159 rows are grouped by sector, and since no row carries a
  sector field that ordering is the only signal of which sector an industry belongs to. The SDK preserves wire
  order, trims labels and drops blanks, but deliberately does *not* de-duplicate — that would change the
  cardinality of a directory response without saying so.
- **Some numerics arrive as strings** — `"fiscalYear":"2026"`, `"fullTimeEmployees":"166000"`. Without
  `AllowReadingFromString` the first quoted number aborts the whole response, not just that field. It rescues a
  quoted `"9"` and does nothing for an unquoted `9.0`, which is why integral-looking counts are still read as
  `decimal`: a `piotroskiScore` of `9.0` would throw on `int` and cost the caller all eleven fields.
- **On the economic calendar, `changePercentage` cannot distinguish zero from absent.** Across a 713-row week it
  was null on 153 rows — but of the 15 rows with `previous`, `estimate`, `actual` and `change` all null, 12
  carried `0` and 3 carried `null`. Both shapes occur on rows that mean the same thing, so neither the zero nor
  the null is a usable "unreported" marker. The only sound gate is `Actual is not null`.
- **A bulk profile's `currency` is not always USD and its `country` tracks the issuer, not the venue.** A TSX
  listing reports `CAD` and `US` on the same row. Summing `marketCap` across the universe therefore mixes
  currencies silently, and filtering a US universe on `country` is not the same as filtering on `exchange`.
- **Identifiers stay strings.** `cik` is zero-padded (`"0000320193"`); parsing it to a number loses the padding
  SEC filings use.
- **429 is answered, not just reported.** The shared reservoir is drained and held for `Retry-After`, clamped by
  `MaxRetryAfter` so an upstream value cannot idle the process for a day.
- **Timeouts sit inside the throttle,** so waiting on the rate limiter never consumes the request budget, and
  expiry raises `TimeoutException` rather than the `TaskCanceledException` callers mistake for a shutdown.
  `HttpClient.Timeout` is deliberately infinite.
- **Plan gating changes.** `profile-bulk` and `shares-float-all` were previously recorded as 402-on-Premium; both
  answered 200 when re-probed. Catch `FmpPlanRestrictedException` to degrade a fast path — and read its
  `StatusCode` before reporting it, since 403 points at the key at least as often as at the plan.
- **`/stable/company-symbol-list` does not exist, and says so in the success shape.** It answers **404 with the
  body `[]`** — a JSON array, which is what this API returns when a request *works*. A client that reads the body
  for an explanation finds a valid empty result on a failed request; this SDK's own error path did exactly that
  and reported `FmpApiException: []`, naming neither the status nor the path. It now ignores an array body and
  reports the status. The working directory endpoints are `stock-list` and `actively-trading-list`.
- **`delisted-companies` caps `limit` at 100 and does not say so.** `limit=1000` and `limit=100` returned
  byte-identical bodies. A caller who trusted the larger value and stepped `page` by their own limit would read a
  tenth of the archive with HTTP 200 throughout, so `GetDelistedAsync` rejects a limit above
  `CompanyEndpoints.MaxDelistedPageSize` at the call site rather than letting the clamp happen silently. The
  archive is 9,782 rows over 98 pages, ordered newest-first — which is why **page 0 carries delistings scheduled
  for the future**; the top row was dated four months ahead of the call.
- **The two symbol lists send the same value under different names.** `stock-list` sends `companyName` and
  `actively-trading-list` sends `name`, and the values agree character for character across all 68,869 shared
  symbols. Both map to `CompanySymbol`. `actively-trading-list` is a strict subset of `stock-list` — 0 symbols
  outside it — so "listed but not actively trading" is a defined set of 22,975, not an inference.
- **The screener reports bad input as data, in both directions.** An unrecognised parameter *name* is ignored:
  `bogusParam=1&limit=3` returns the same three rows as `limit=3` alone, so a typo in a filter silently widens
  the query and looks like a query that worked. An unrecognised parameter *value* returns `[]` with HTTP 200,
  indistinguishable from a real filter that matched nothing. `ScreenerCriteria` closes the first — a misspelled
  filter will not compile. The second cannot be closed without freezing a vocabulary FMP grows, so an empty
  screen is a reason to check the values against `GetSectorsAsync` and `GetIndustriesAsync` before concluding the
  universe is empty.
- **The screener's `…MoreThan` and `…LowerThan` bounds are both inclusive**, despite the names — `priceLowerThan=1`
  returns securities priced at exactly 1. Two adjacent ranges written as `LowerThan = x` and `MoreThan = x`
  overlap on the boundary rather than partitioning. Its `exchange` filter also takes only the short code, so a
  result's own `Exchange` (`NASDAQ Global Select`) fed back into a query matches nothing; `ExchangeShortName` is
  the field that round-trips.

## Plan gating — 402 and 403

FMP refuses an endpoint your key is not entitled to with **402**, and refuses a key it does not like with **403**.
The SDK treats both as `FmpPlanRestrictedException`, but **does not conflate them**:

```csharp
catch (FmpPlanRestrictedException ex)
{
    if (ex.IsRejectedCredential)   // 403 — check the key before the invoice
        logger.LogError("FMP rejected the key: {Message}", ex.Message);
    else                           // 402 — genuinely an entitlement answer
        logger.LogWarning("Not on this plan: {Message}", ex.Message);
}
```

`ex.StatusCode` carries the actual status. This matters more than it looks: FMP's own error text warns that
"frequent abuse on this API Endpoint may result in restrictions placed on this API Key", so a 403 is a plausible
outcome of hammering the bulk endpoints — and reporting that as "upgrade your plan" sends someone to the wrong
page entirely.

**Every failure is an exception. Nothing signals one by returning.**

There is no `Try`-prefixed method anywhere in the SDK, and that is a decision rather than an omission. C# forbids
`out` parameters on async methods (CS1988), so the BCL's `bool TryX(out T)` shape cannot be written for an async
API at all — which is why the framework has no `TryReadAsync` either, and why `ChannelReader<T>` pairs a
*synchronous* `TryRead` with an *asynchronous* `ReadAsync` that throws. An earlier version of this SDK imitated
the pattern with a nullable return, which was worse than either option: it put two error channels on one surface
and gave `null` a meaning the signature could not carry, so you had to read the docs to learn that it meant
"refused" rather than "nothing there".

To degrade instead of failing — an optional whole-universe fast path falling back to a per-symbol loop, say —
catch the exception. It is self-describing at the catch site and tells you *which* refusal arrived.

**Null still means something, just never an error.** Endpoints returning `T?` use null for an answer FMP
genuinely gave:

| Returns null when | Meaning |
|---|---|
| `Company.GetProfileAsync` | FMP has no such symbol |
| `Company.GetSharesFloatAsync` | likewise |
| `Statements.GetScoresAsync` | an ETF, which genuinely has no scores |

An entitled call with nothing to say returns an **empty list**, not null and not an exception. Collapsing a 402
into an empty result is what makes a paywalled endpoint indistinguishable from a real empty answer *and* from the
provider being down — a defect the SDK's predecessor shipped.

**The SDK carries no tier map**, and will not. `profile-bulk` and `shares-float-all` were both recorded as 402 on
Premium and both answered 200 when re-probed on 2026-08-26. Entitlement moves and varies per key, so anything
claiming "this needs Ultimate" would be confidently wrong sooner or later. Probe, catch, and re-probe.

## Installing and versioning

The package is published to this repository's **GitHub Packages** NuGet feed, not to nuget.org. Add the source,
then `dotnet add package FmpDotNet`.

**Every push to `master` publishes a prerelease** — `0.1.0-ci.7`, `0.1.0-ci.8`, and so on, where the suffix is
the CI run number. That shape is forced by the feed: GitHub Packages refuses to overwrite an existing version, so
a fixed version would publish once and fail every push after it. Run numbers never reset, so the versions are
monotonic; a re-run keeps its number and is pushed with `--skip-duplicate`, which makes re-running a green build a
no-op rather than a failure.

**Pin an exact prerelease.** A floating reference to a feed that gains a version on every push is a build that
changes under you. Pinning also makes "which SDK did this commit build against" answerable from your own git
history — which is how `trader` consumes it.

A release is cut by packing without a suffix, giving a plain `0.1.0`. NuGet orders a release above every
prerelease of the same version, so a hand-cut build always supersedes the CI ones it follows. Until 1.0, treat a
minor bump as potentially breaking: the surface is still being shaped by what the live API turns out to do, and
two releases so far have removed public members after measurement showed they were the wrong shape.

Each package ships the XML documentation, and a matching `.snupkg` carries the PDBs. With Source Link, a debugger
steps from your code into this SDK's source at the exact commit the binary was built from.

## Configuration

```json
{
  "Fmp": {
    "ApiKey": "…",
    "BaseUrl": "https://financialmodelingprep.com",
    "PerMinuteCap": 660,
    "BulkPerMinuteCap": 2,
    "RequestTimeout": "00:00:30",
    "BulkRequestTimeout": "00:10:00",
    "MaxRetryAfter": "00:02:00",
    "DeveloperBulkCacheDirectory": null
  }
}
```

Timeouts bind to NodaTime `Duration` and accept both `"00:00:30"` and a bare number of seconds (`"30"`). The
bare-number form is checked first on purpose: `TimeSpan.TryParse("45")` means *45 days*, so the other order would
turn `RequestTimeout=45` into a timeout that never fires.

The API key is not validated — an SDK cannot know whether its caller intends to make a request; assert it in the
host that does.

## The live smoke suite

Everything under [upstream behaviour](#upstream-behaviour-the-sdk-handles-for-you) was measured against the real
API on a particular day. A stub suite cannot tell you when one of those measurements stops being true, so a
second suite does: `FmpDotNet.SmokeTests` calls every modelled endpoint against FMP once a week and compares what
comes back with a record checked into the repository.

**It records which fields carried a value, not merely that a call succeeded.** That is the whole design, and the
reason is that a rename does not fail. Almost every property on these models is nullable and none are `required`,
so when FMP renames a field `System.Text.Json` deserialises the missing name to null, hands back the same number
of rows of the same type, and reports nothing at all. A smoke test asserting "a non-empty list came back" passes
on the day the data stops arriving. The record is one line per property, so a rename is a one-line diff:

```
[Statements.GetIncomeStatementAsync]
outcome rows
set NetIncome
```

`set NetIncome` becoming `null NetIncome` is the alarm.

Measured 2026-08-27: **49 ordinary endpoints, 702 properties recorded as populated**, and exactly three recorded
empty — `Source` on the first page of `shares-float-all`, which is all Shenzhen listings and carries no EDGAR
filing URL, plus `MarketCap` on the commodity and forex quote batches, where a market capitalisation is not a
meaningful thing to ask for. There is no blind spot on any wire field the SDK models: of the models' public properties 752 are
nullable, 20 are strings defaulting to `""` and one is a collection defaulting to empty, all of which read
correctly, and the only four non-nullable value types are three on a list wrapper that is never inspected plus
one `[JsonIgnore]` property the SDK sets from the request rather than from the response.

Two assertions run against each record, with deliberately different meanings. One fails when something the record
showed arriving has stopped — that is a defect in shipped code, and it is the one worth waking up for. The other
fails on any difference at all, including a field FMP has *started* sending, and asks for the record to be
regenerated. Folded together, a newly-populated field and a newly-missing one would produce the same red.

**The `*-bulk` endpoints are excluded by default** and need a second, deliberate switch. FMP's own throttle text
warns that "frequent abuse on this API Endpoint may result in restrictions placed on this API Key", so the cost
of sweeping them weekly is the key rather than the runner minutes. When they do run, they are paced by the SDK's
own bulk reservoir — `BulkPerMinuteCap`, defaulting to 2 a minute — and each probe reads the first 25 rows and
then abandons the download rather than transferring a file that can reach 69 MB. The throttle is the SDK's, not
the test suite's: there is no pacing code here, the probes simply queue behind the reservoir every caller shares.

Measured 2026-08-27: **20 bulk endpoints, 527 properties populated**, three empty, in 9 m 30 s — nearly all of it
waiting on the throttle. The same three were empty on 2026-08-26, and all three were checked rather than assumed:
`cik` is present in the `profile-bulk` header and read correctly by a passing unit test, and
`priceToEarningsDilutedGrowthRatioTTM` is blank for the sampled rows in the captured `ratios-ttm-bulk` fixture
too. They are sparse data, not a broken mapper — see the caveat on bulk `null` below.

```bash
# Ordinary endpoints. Seconds.
FMP_API_KEY=… dotnet test tests/FmpDotNet.SmokeTests

# The bulk endpoints as well. About eight minutes, nearly all of it waiting on the throttle.
FMP_API_KEY=… FMPDOTNET_SMOKE_BULK=1 dotnet test tests/FmpDotNet.SmokeTests

# Re-record — after reading the diff and satisfying yourself nothing was lost.
FMP_API_KEY=… FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
```

Without `FMP_API_KEY` every live test skips itself, so a clone with no key runs the whole solution green and
offline.

**What it does not tell you.** Three gaps, named rather than papered over.

The sweep asks about one symbol over one recent window, so a property recorded as populated is populated *for a
company that files everything*. It cannot distinguish a field FMP populates universally from one it populates
only for large US issuers, and it is not checking that any value is correct.

It watches shape, not volume. If `stock-list` fell from 91,844 rows to 500 with every field still populated,
nothing here would notice. A row-count band would catch it, but the calendars swing across an order of magnitude
between a quiet week and earnings season, and a band set today would either flap or be too loose to mean
anything. Setting one honestly needs a few months of recorded runs — which this suite now produces.

A `null` in the **bulk** record is weaker evidence than a `null` in the ordinary one. A bulk probe reads the
first 25 rows of one part, and a part is an unordered shard FMP republishes every few hours, so a sparse column
can read as absent one week and populated the next. That is a property of the data rather than a fault: it costs
one regeneration when it happens, and no affordable sample size fixes it — reading 200 rows instead of 25 was
measured at 2 h 39 m against 8 minutes, and would still be sampling one shard.

It answers one question: is the SDK still reading the shape FMP is still sending.

## Working on a bulk mapper

Set `Fmp:DeveloperBulkCacheDirectory` while you are writing or changing a `*-bulk` model. The first call to each
bulk URL is written to that directory; every later call to the same URL is replayed from disk, so you can iterate
on a `FromCsv` mapper without re-downloading it.

```json
{ "Fmp": { "DeveloperBulkCacheDirectory": ".fmp-bulk-cache" } }
```

Delete the directory to refetch. Entries are keyed by the request URL with the API key stripped, so rotating your
key does not orphan the cache.

Every bulk model in this repository was written against a response captured this way and verified by streaming
the whole of it through the mapper, not a sample. Across the milestone that is **3.2 million rows and roughly
560 MB** — including `etf-holder-bulk`'s single 298 MB part, which streamed 2,571,137 rows at 0.2 MB of peak live
memory.

**Why it exists.** Bulk is throttled separately and far more tightly than the ordinary endpoints — measured
2026-08-26, a second call moments after the first was already refused — and FMP's own error text warns that
"frequent abuse on this API Endpoint may result in restrictions placed on this API Key". The payloads reach 69 MB,
and FMP refreshes them only once every few hours, so re-fetching while you iterate buys nothing and spends your
key's standing.

**It is not a caching layer.** Entries never expire, nothing is invalidated, nothing is bounded, and a stale entry
is served forever. Setting this in a deployed application means that application silently stops reading live data.
It is off by default, it applies only to the bulk client — never to the per-symbol endpoints — and it logs a
warning the first time it serves anything, so it cannot be on without saying so. Responses that look like an error
payload are delivered but never kept, so a failure cannot be replayed forever as if it were data.

