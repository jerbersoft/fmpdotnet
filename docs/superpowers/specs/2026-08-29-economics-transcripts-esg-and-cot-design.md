# Economics, Earnings Transcripts, ESG and COT — design

Issue [#40](https://github.com/jerbersoft/fmpdotnet/issues/40), twelve paths, three new facades and three
methods onto an existing one.

Every claim here rests on
[the measurements](2026-08-29-economics-transcripts-esg-and-cot-measurements.md) taken on **2026-08-29** across
seventeen probe passes and 126 captured responses. Where this document says a value "was measured", the
measurements file gives the row count behind it.

**Spec authority:** where this design and the measurements disagree, the measurements win and this document is
wrong.

## Scope

Twelve paths, all reachable on the current key, none of them `*-bulk`. Unlike #30, #31 and #36, this is **not
one subject** — it is four unrelated groups that share an issue number because each is too small to carry its
own slice. The design's job is to stop that accident of packaging from becoming an accident of API shape.

Coverage moves from **166 of 243 to 178 of 243**.

## Public surface

### `fmp.Economics` — existing facade, +3 paths

```csharp
Task<IReadOnlyList<EconomicObservation>> GetIndicatorAsync(
    EconomicIndicator indicator, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)

Task<IReadOnlyList<MarketRiskPremium>> GetMarketRiskPremiumsAsync(CancellationToken ct = default)

Task<IReadOnlyList<TreasuryRate>> GetTreasuryRatesAsync(
    LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
```

The facade's existing type-level documentation says the surface is "the calendar of scheduled and published
economic releases". That sentence stops being true here and must be rewritten as part of this work: the facade
becomes FMP's macroeconomic surface generally — a calendar, indicator series, treasury curves and country risk
premia.

### `fmp.Transcripts` — new, 3 paths

```csharp
Task<EarningsTranscript?> GetTranscriptAsync(
    string symbol, int year, int quarter, CancellationToken ct = default)

Task<IReadOnlyList<TranscriptDate>> GetDatesAsync(string symbol, CancellationToken ct = default)

Task<IReadOnlyList<LatestTranscript>> GetLatestAsync(
    int? limit = null, int? page = null, CancellationToken ct = default)
```

`GetTranscriptAsync` returns `T?` rather than a one-element list, following
`CompanyEndpoints.GetProfileAsync` and the four `AnalystEndpoints` consensus methods: `rows.Count > 0 ?
rows[0] : null`.

`fmp.Directory.GetTranscriptSymbolsAsync` already answers "which symbols have transcripts" from
`stable/earnings-transcript-list` and is **not** moved. Its documentation currently says the transcripts
themselves "are not modelled — three further paths in issue #25's long tail"; that sentence must be updated to
point at `fmp.Transcripts`.

### `fmp.Esg` — new, 3 paths

```csharp
Task<IReadOnlyList<EsgDisclosure>> GetDisclosuresAsync(string symbol, CancellationToken ct = default)

Task<IReadOnlyList<EsgRating>> GetRatingsAsync(string symbol, CancellationToken ct = default)

Task<IReadOnlyList<EsgBenchmark>> GetBenchmarkAsync(int? year = null, CancellationToken ct = default)
```

### `fmp.Cot` — new, 3 paths

```csharp
Task<IReadOnlyList<CotReport>> GetReportAsync(
    string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)

Task<IReadOnlyList<CotAnalysis>> GetAnalysisAsync(
    string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)

Task<IReadOnlyList<CotSymbol>> GetSymbolsAsync(CancellationToken ct = default)
```

`commitment-of-traders-list` becomes `GetSymbolsAsync`, not `GetListAsync`. The wire name would put a
`GetListAsync` on a facade whose transport already has one meaning something entirely different, and the
response is a symbol directory — the same thing `Directory.GetTranscriptSymbolsAsync` returns for transcripts.

## Naming rule: properties are corrected C#, attributes carry the wire verbatim

The COT records carry three misspellings and one inconsistent suffix, measured and listed in the measurements
file. **The `[JsonPropertyName]` attribute carries FMP's spelling exactly; the C# property carries the correct
English.** This is the existing house rule — `senateID` binds to `SenateId`, `growthEBITDA` to `GrowthEbitda` —
applied to a case where the wire is not merely styled differently but actually wrong.

| wire | property | why |
|---|---|---|
| `netPostion` | `NetPosition` | missing `i`; siblings `previousNetPosition` and `changeInNetPosition` are correct |
| `changeInNoncommSpeadAll` | `ChangeInNoncommSpreadAll` | missing `r` |
| `tradersNoncommSpeadOl` | `TradersNoncommSpreadOld` | **both** defects on one field |
| `…Ol` (26 fields) | `…Old` | the positions block spells it `Old`; the pct, traders and concentration blocks do not |

27 of `CotReport`'s 128 properties are renamed by this rule. All 128 property names remain distinct — checked,
no collisions. Every renamed field gets a `// sic` comment at its declaration, so the next reader sees that the
divergence is deliberate rather than a typo introduced here.

Acronyms follow the house convention rather than the wire: `ESGScore` binds to `EsgScore` and `ESGRiskRating`
to `EsgRiskRating`, as `cik → Cik` and `growthEPS → GrowthEps` already do.

## `EconomicIndicator` — a closed type over a case-sensitive name

`stable/economic-indicators` answers an unrecognised `name` with HTTP 200, `content-type: application/json`,
and twelve bytes of `Invalid name` that are not JSON at all. The name is case-sensitive: `GDP` works, `gdp`
does not. A caller who lower-cases an indicator gets a deserialisation failure out of a success response.

The parameter is therefore a `readonly record struct EconomicIndicator` wrapping the wire string, with 23
static members. All 23 documented names were probed individually and all 23 are valid, so the closed set is
complete as measured rather than merely convenient.

A plain C# `enum` cannot express this set: two wire names begin with a digit. The members are renamed and the
wire string is the source of truth.

| member | wire |
|---|---|
| `Gdp` | `GDP` |
| `RealGdp` | `realGDP` |
| `NominalPotentialGdp` | `nominalPotentialGDP` |
| `RealGdpPerCapita` | `realGDPPerCapita` |
| `FederalFunds` | `federalFunds` |
| `ConsumerPriceIndex` | `CPI` |
| `InflationRate` | `inflationRate` |
| `Inflation` | `inflation` |
| `RetailSales` | `retailSales` |
| `ConsumerSentiment` | `consumerSentiment` |
| `DurableGoods` | `durableGoods` |
| `UnemploymentRate` | `unemploymentRate` |
| `TotalNonfarmPayroll` | `totalNonfarmPayroll` |
| `InitialClaims` | `initialClaims` |
| `IndustrialProductionTotalIndex` | `industrialProductionTotalIndex` |
| `NewPrivatelyOwnedHousingUnitsStartedTotalUnits` | `newPrivatelyOwnedHousingUnitsStartedTotalUnits` |
| `TotalVehicleSales` | `totalVehicleSales` |
| `RetailMoneyFunds` | `retailMoneyFunds` |
| `SmoothedUsRecessionProbabilities` | `smoothedUSRecessionProbabilities` |
| `ThreeMonthCertificateOfDepositRate` | `3MonthOr90DayRatesAndYieldsCertificatesOfDeposit` |
| `CreditCardInterestRate` | `commercialBankInterestRateOnCreditCardPlansAllAccounts` |
| `Mortgage30Year` | `30YearFixedRateMortgageAverage` |
| `Mortgage15Year` | `15YearFixedRateMortgageAverage` |

Two members — `Inflation` and `ThreeMonthCertificateOfDepositRate` — returned a well-formed **empty array**
when measured on 2026-08-29. They are valid names carrying no rows, not invalid names, and they ship with that
recorded in their documentation.

## Transport: a non-JSON 200 must not leak a `JsonException`

Measured 2026-08-29 against the current code, `GetListAsync` on a body of `Invalid name` throws:

```
System.Text.Json.JsonException: 'I' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0.
```

A raw serialisation exception, naming neither the request nor what FMP actually said. `GetObjectAsync` already
handles this case and `ReadListAsync` does not — the two pipelines diverge for no reason anyone chose.

`ReadListAsync` gains the guard `GetObjectAsync` already has: catch `JsonException` from the deserialise call
and rethrow `FmpApiException($"FMP answered a body that is not JSON: {ex.Message}", request.ToString())`.

This is shared transport code on the SDK's busiest path, so it carries its own test and its own review
attention. It closes more than this slice: `stable/financial-reports-xlsx` is already documented as answering
a MISS with sixteen bytes of `Error with query` under the same lying content type.

## Row caps: documented, not guarded

Four of the twelve silently return fewer rows than asked for, keeping the newest and dropping the rest under a
200 with a well-formed array.

| path | cap | surfaced as |
|---|---|---|
| `economic-indicators` | 61 rows | XML docs on `GetIndicatorAsync` |
| `treasury-rates` | 61 rows | XML docs on `GetTreasuryRatesAsync` |
| `commitment-of-traders-analysis` | 13 rows | XML docs on `GetAnalysisAsync`, naming the sibling contrast |
| `earning-call-transcript-latest` | 100 rows | XML docs on `GetLatestAsync`; `limit` above 100 is clamped |

**No row-count guard is added**, for the reason already written into `GetEconomicCalendarAsync`: sparse is
legitimate here and a threshold rejects genuinely quiet ranges while accepting truncated wide ones. The
documented check is positional — did the returned rows reach both ends of the range asked for? The caller has
what they need for it: the range they passed and a date on every row.

The GDP family needs its own sentence in `GetIndicatorAsync`'s documentation, because it is worse than
truncation. All four quarterly series return **zero rows** for any range of a year or more while returning data
for a 90-day range inside it. A caller widening their window to get more history gets nothing, with no error.

## Paging on `GetLatestAsync`

Re-measured 2026-08-29. `page` is exposed, because it works — it just does not do what its name implies.

Two `page=0` calls in one burst returned identical sets. Pages two apart are disjoint. **Adjacent pages
overlap**: `page=0` against `page=1` shares 28 of 100 rows, `page=1` against `page=2` shares 21. The stride is
roughly 72–79 rows against a page size of 100, and the union of pages 0, 1 and 2 is 251 distinct rows of 300
returned.

The documentation states the stride, states that consecutive pages overlap, and tells the caller to
de-duplicate on `(Symbol, FiscalYear, Period, Date)` — the tuple that was unique within all four pages
measured. It also records that the bare call is **not** `page=0`: issued at the same instant they share 71 of
100 rows, so omitting `page` is its own query rather than a synonym for zero.

Separately, the feed churns on a timescale of tens of minutes — two bare calls twenty minutes apart shared 90
of 100 rows. That, not the page overlap, is why nothing here may be asserted by index against live data.

## Parameters deliberately not exposed

| path | parameter | why |
|---|---|---|
| `esg-benchmark` | `sector` | **silently ignored** — `?sector=APPAREL RETAIL` is byte-identical to the bare call, 1003 rows across 291 sectors |
| `economic-indicators` | `limit` | **silently ignored** — `name=CPI&limit=100` returns the same 2 rows as `name=CPI` |

Both are measured no-ops. Exposing them would promise filtering the API does not perform, which is the same
class of defect as the `-by-id` trap closed in #31 — a parameter the caller reasonably believes narrowed their
result when it did nothing.

`GetBenchmarkAsync`'s documentation records that the default year is **2023**, three years stale as of the
measurement date, and that the bare call is byte-identical to `year=2023`.

## Models

Twelve records, 203 properties. Types follow the measured wire types.

Every numeric that was measured `float` on any row is `decimal?`. Numerics measured `int` on every row are
`int?` where they count discrete things — open interest, positions, traders, years, quarters. The largest
integer measured on `CotReport` is 7,027,815 against `int.MaxValue` of 2,147,483,647, so `int` has three
orders of magnitude of headroom.

The percentage and concentration blocks are `decimal?` because they were measured mixed: `pctOfOiNoncommLongAll`
is `float` on 489 rows and `int` on 56.

### `EconomicObservation` — 3 properties

```csharp
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("value")] public decimal? Value { get; init; }
```

### `MarketRiskPremium` — 4 properties

```csharp
    [JsonPropertyName("country")] public string? Country { get; init; }
    [JsonPropertyName("continent")] public string? Continent { get; init; }
    [JsonPropertyName("countryRiskPremium")] public decimal? CountryRiskPremium { get; init; }
    [JsonPropertyName("totalEquityRiskPremium")] public decimal? TotalEquityRiskPremium { get; init; }
```

`country` and `continent` were non-empty on all 192 measured rows; both remain nullable, as every string on
every model in this SDK is.

### `TreasuryRate` — 13 properties

```csharp
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("month1")] public decimal? Month1 { get; init; }
    [JsonPropertyName("month2")] public decimal? Month2 { get; init; }
    [JsonPropertyName("month3")] public decimal? Month3 { get; init; }
    [JsonPropertyName("month6")] public decimal? Month6 { get; init; }
    [JsonPropertyName("year1")] public decimal? Year1 { get; init; }
    [JsonPropertyName("year2")] public decimal? Year2 { get; init; }
    [JsonPropertyName("year3")] public decimal? Year3 { get; init; }
    [JsonPropertyName("year5")] public decimal? Year5 { get; init; }
    [JsonPropertyName("year7")] public decimal? Year7 { get; init; }
    [JsonPropertyName("year10")] public decimal? Year10 { get; init; }
    [JsonPropertyName("year20")] public decimal? Year20 { get; init; }
    [JsonPropertyName("year30")] public decimal? Year30 { get; init; }
```

### `EarningsTranscript` — 5 properties

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("period")] public string? Period { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
```

`Content` is a single string measured at **46,546 characters** for AAPL 2025 Q3. It is not chunked, not
parsed into speaker turns, and not offered as a stream: it is one JSON string field and the SDK transcribes it.

### `TranscriptDate` — 3 properties

```csharp
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
```

### `LatestTranscript` — 4 properties

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("period")] public string? Period { get; init; }
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
```

**The three transcript records deliberately disagree with each other, because the wire does.** The same quarter
is `period: "Q3"` on two paths and `quarter: 3` on the third; the same year is `year` on one and `fiscalYear` on
two. Normalising them would require inventing values FMP did not send. Each record transcribes its own
endpoint, and the divergence is documented on all three types so a caller comparing them is not surprised.

The request/response mismatch on `GetTranscriptAsync` is documented too: it is *queried* with `quarter=3` and
*answers* `period: "Q3"`.

### `EsgDisclosure` — 11 properties

```csharp
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("acceptedDate")] [JsonConverter(typeof(NullableLocalDateJsonConverter))] public LocalDate? AcceptedDate { get; init; }
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("cik")] public string? Cik { get; init; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
    [JsonPropertyName("formType")] public string? FormType { get; init; }
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
```

`cik` is `string?`, not a number: the measured value is `"0000320193"` and the leading zeros are significant.
This matches every other `cik` in the SDK.

### `EsgRating` — 7 properties

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("cik")] public string? Cik { get; init; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
    [JsonPropertyName("industry")] public string? Industry { get; init; }
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }
    [JsonPropertyName("ESGRiskRating")] public string? EsgRiskRating { get; init; }
    [JsonPropertyName("industryRank")] public string? IndustryRank { get; init; }
```

`IndustryRank` is `string?` because the measured value is `"3 out of 9"` — a sentence, not a rank. The
natural guess is `int?`, and it would throw on every row.

### `EsgBenchmark` — 7 properties

```csharp
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }
    [JsonPropertyName("period")] public string? Period { get; init; }
    [JsonPropertyName("sector")] public string? Sector { get; init; }
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }
```

`Sector` is present on the record and absent from the method signature. That is deliberate and stated above:
the field is returned, the query parameter is ignored.

### `CotSymbol` — 2 properties

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
```

### `CotAnalysis` — 16 properties

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("sector")] public string? Sector { get; init; }
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
    [JsonPropertyName("currentLongMarketSituation")] public decimal? CurrentLongMarketSituation { get; init; }
    [JsonPropertyName("currentShortMarketSituation")] public decimal? CurrentShortMarketSituation { get; init; }
    [JsonPropertyName("marketSituation")] public string? MarketSituation { get; init; }
    [JsonPropertyName("previousLongMarketSituation")] public decimal? PreviousLongMarketSituation { get; init; }
    [JsonPropertyName("previousShortMarketSituation")] public decimal? PreviousShortMarketSituation { get; init; }
    [JsonPropertyName("previousMarketSituation")] public string? PreviousMarketSituation { get; init; }
    [JsonPropertyName("netPostion")] public int? NetPosition { get; init; }  // sic: wire drops the "i"
    [JsonPropertyName("previousNetPosition")] public int? PreviousNetPosition { get; init; }
    [JsonPropertyName("changeInNetPosition")] public decimal? ChangeInNetPosition { get; init; }
    [JsonPropertyName("marketSentiment")] public string? MarketSentiment { get; init; }
    [JsonPropertyName("reversalTrend")] public bool? ReversalTrend { get; init; }
```

`ChangeInNetPosition` is `decimal?` while `NetPosition` and `PreviousNetPosition` beside it are `int?`, and
that asymmetry is deliberate: the field is a **percent change, not a delta**. Measured across all 545 rows,
545 match a percent reading and 4 match an absolute one. Its documentation must say so — a caller who adds it
to a position count is wrong by three orders of magnitude and gets no signal.

`ReversalTrend` is `bool?` because the wire sends a real JSON boolean on all 545 measured rows. This is worth
stating because #31 met the opposite case — `capitalGainsOver200Usd` arrives as the *string* `"False"`, which
`bool?` will not bind. The two look identical in documentation and differ on the wire, so each is typed from
its own measurement rather than from the other's precedent.

### `CotReport` — 128 properties

The widest record in the SDK, against `FinancialRatios` at 66. It carries a file-scoped
`#pragma warning disable CS1591` with the count and reasoning at the top of the file, in the form the seven
fundamentals models already use.

**This makes eight exemptions where the csproj comment currently says seven.** That comment is load-bearing —
it records why CS1591 is not suppressed project-wide — and it must be updated in the same change rather than
left to drift.

```csharp
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("sector")] public string? Sector { get; init; }
    [JsonPropertyName("marketAndExchangeNames")] public string? MarketAndExchangeNames { get; init; }
    [JsonPropertyName("cftcContractMarketCode")] public string? CftcContractMarketCode { get; init; }
    [JsonPropertyName("cftcMarketCode")] public string? CftcMarketCode { get; init; }
    [JsonPropertyName("cftcRegionCode")] public string? CftcRegionCode { get; init; }
    [JsonPropertyName("cftcCommodityCode")] public string? CftcCommodityCode { get; init; }
    [JsonPropertyName("openInterestAll")] public int? OpenInterestAll { get; init; }
    [JsonPropertyName("noncommPositionsLongAll")] public int? NoncommPositionsLongAll { get; init; }
    [JsonPropertyName("noncommPositionsShortAll")] public int? NoncommPositionsShortAll { get; init; }
    [JsonPropertyName("noncommPositionsSpreadAll")] public int? NoncommPositionsSpreadAll { get; init; }
    [JsonPropertyName("commPositionsLongAll")] public int? CommPositionsLongAll { get; init; }
    [JsonPropertyName("commPositionsShortAll")] public int? CommPositionsShortAll { get; init; }
    [JsonPropertyName("totReptPositionsLongAll")] public int? TotReptPositionsLongAll { get; init; }
    [JsonPropertyName("totReptPositionsShortAll")] public int? TotReptPositionsShortAll { get; init; }
    [JsonPropertyName("nonreptPositionsLongAll")] public int? NonreptPositionsLongAll { get; init; }
    [JsonPropertyName("nonreptPositionsShortAll")] public int? NonreptPositionsShortAll { get; init; }
    [JsonPropertyName("openInterestOld")] public int? OpenInterestOld { get; init; }
    [JsonPropertyName("noncommPositionsLongOld")] public int? NoncommPositionsLongOld { get; init; }
    [JsonPropertyName("noncommPositionsShortOld")] public int? NoncommPositionsShortOld { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOld")] public int? NoncommPositionsSpreadOld { get; init; }
    [JsonPropertyName("commPositionsLongOld")] public int? CommPositionsLongOld { get; init; }
    [JsonPropertyName("commPositionsShortOld")] public int? CommPositionsShortOld { get; init; }
    [JsonPropertyName("totReptPositionsLongOld")] public int? TotReptPositionsLongOld { get; init; }
    [JsonPropertyName("totReptPositionsShortOld")] public int? TotReptPositionsShortOld { get; init; }
    [JsonPropertyName("nonreptPositionsLongOld")] public int? NonreptPositionsLongOld { get; init; }
    [JsonPropertyName("nonreptPositionsShortOld")] public int? NonreptPositionsShortOld { get; init; }
    [JsonPropertyName("openInterestOther")] public int? OpenInterestOther { get; init; }
    [JsonPropertyName("noncommPositionsLongOther")] public int? NoncommPositionsLongOther { get; init; }
    [JsonPropertyName("noncommPositionsShortOther")] public int? NoncommPositionsShortOther { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOther")] public int? NoncommPositionsSpreadOther { get; init; }
    [JsonPropertyName("commPositionsLongOther")] public int? CommPositionsLongOther { get; init; }
    [JsonPropertyName("commPositionsShortOther")] public int? CommPositionsShortOther { get; init; }
    [JsonPropertyName("totReptPositionsLongOther")] public int? TotReptPositionsLongOther { get; init; }
    [JsonPropertyName("totReptPositionsShortOther")] public int? TotReptPositionsShortOther { get; init; }
    [JsonPropertyName("nonreptPositionsLongOther")] public int? NonreptPositionsLongOther { get; init; }
    [JsonPropertyName("nonreptPositionsShortOther")] public int? NonreptPositionsShortOther { get; init; }
    [JsonPropertyName("changeInOpenInterestAll")] public int? ChangeInOpenInterestAll { get; init; }
    [JsonPropertyName("changeInNoncommLongAll")] public int? ChangeInNoncommLongAll { get; init; }
    [JsonPropertyName("changeInNoncommShortAll")] public int? ChangeInNoncommShortAll { get; init; }
    [JsonPropertyName("changeInNoncommSpeadAll")] public int? ChangeInNoncommSpreadAll { get; init; }  // sic: wire spells it "Spead"
    [JsonPropertyName("changeInCommLongAll")] public int? ChangeInCommLongAll { get; init; }
    [JsonPropertyName("changeInCommShortAll")] public int? ChangeInCommShortAll { get; init; }
    [JsonPropertyName("changeInTotReptLongAll")] public int? ChangeInTotReptLongAll { get; init; }
    [JsonPropertyName("changeInTotReptShortAll")] public int? ChangeInTotReptShortAll { get; init; }
    [JsonPropertyName("changeInNonreptLongAll")] public int? ChangeInNonreptLongAll { get; init; }
    [JsonPropertyName("changeInNonreptShortAll")] public int? ChangeInNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestAll")] public int? PctOfOpenInterestAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongAll")] public decimal? PctOfOiNoncommLongAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortAll")] public decimal? PctOfOiNoncommShortAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadAll")] public decimal? PctOfOiNoncommSpreadAll { get; init; }
    [JsonPropertyName("pctOfOiCommLongAll")] public decimal? PctOfOiCommLongAll { get; init; }
    [JsonPropertyName("pctOfOiCommShortAll")] public decimal? PctOfOiCommShortAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongAll")] public decimal? PctOfOiTotReptLongAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortAll")] public decimal? PctOfOiTotReptShortAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongAll")] public decimal? PctOfOiNonreptLongAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortAll")] public decimal? PctOfOiNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestOl")] public int? PctOfOpenInterestOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommLongOl")] public decimal? PctOfOiNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommShortOl")] public decimal? PctOfOiNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommSpreadOl")] public decimal? PctOfOiNoncommSpreadOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommLongOl")] public decimal? PctOfOiCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommShortOl")] public decimal? PctOfOiCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptLongOl")] public decimal? PctOfOiTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptShortOl")] public decimal? PctOfOiTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptLongOl")] public decimal? PctOfOiNonreptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptShortOl")] public decimal? PctOfOiNonreptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOpenInterestOther")] public int? PctOfOpenInterestOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongOther")] public decimal? PctOfOiNoncommLongOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortOther")] public decimal? PctOfOiNoncommShortOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadOther")] public decimal? PctOfOiNoncommSpreadOther { get; init; }
    [JsonPropertyName("pctOfOiCommLongOther")] public decimal? PctOfOiCommLongOther { get; init; }
    [JsonPropertyName("pctOfOiCommShortOther")] public decimal? PctOfOiCommShortOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongOther")] public decimal? PctOfOiTotReptLongOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortOther")] public decimal? PctOfOiTotReptShortOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongOther")] public decimal? PctOfOiNonreptLongOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortOther")] public decimal? PctOfOiNonreptShortOther { get; init; }
    [JsonPropertyName("tradersTotAll")] public int? TradersTotAll { get; init; }
    [JsonPropertyName("tradersNoncommLongAll")] public int? TradersNoncommLongAll { get; init; }
    [JsonPropertyName("tradersNoncommShortAll")] public int? TradersNoncommShortAll { get; init; }
    [JsonPropertyName("tradersNoncommSpreadAll")] public int? TradersNoncommSpreadAll { get; init; }
    [JsonPropertyName("tradersCommLongAll")] public int? TradersCommLongAll { get; init; }
    [JsonPropertyName("tradersCommShortAll")] public int? TradersCommShortAll { get; init; }
    [JsonPropertyName("tradersTotReptLongAll")] public int? TradersTotReptLongAll { get; init; }
    [JsonPropertyName("tradersTotReptShortAll")] public int? TradersTotReptShortAll { get; init; }
    [JsonPropertyName("tradersTotOl")] public int? TradersTotOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommLongOl")] public int? TradersNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommShortOl")] public int? TradersNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommSpeadOl")] public int? TradersNoncommSpreadOld { get; init; }  // sic: BOTH defects — "Spead" and "Ol"
    [JsonPropertyName("tradersCommLongOl")] public int? TradersCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersCommShortOl")] public int? TradersCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptLongOl")] public int? TradersTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptShortOl")] public int? TradersTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotOther")] public int? TradersTotOther { get; init; }
    [JsonPropertyName("tradersNoncommLongOther")] public int? TradersNoncommLongOther { get; init; }
    [JsonPropertyName("tradersNoncommShortOther")] public int? TradersNoncommShortOther { get; init; }
    [JsonPropertyName("tradersNoncommSpreadOther")] public int? TradersNoncommSpreadOther { get; init; }
    [JsonPropertyName("tradersCommLongOther")] public int? TradersCommLongOther { get; init; }
    [JsonPropertyName("tradersCommShortOther")] public int? TradersCommShortOther { get; init; }
    [JsonPropertyName("tradersTotReptLongOther")] public int? TradersTotReptLongOther { get; init; }
    [JsonPropertyName("tradersTotReptShortOther")] public int? TradersTotReptShortOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongAll")] public decimal? ConcGrossLe4TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortAll")] public decimal? ConcGrossLe4TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongAll")] public decimal? ConcGrossLe8TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortAll")] public decimal? ConcGrossLe8TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe4TdrLongAll")] public decimal? ConcNetLe4TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe4TdrShortAll")] public decimal? ConcNetLe4TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe8TdrLongAll")] public decimal? ConcNetLe8TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe8TdrShortAll")] public decimal? ConcNetLe8TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongOl")] public decimal? ConcGrossLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrShortOl")] public decimal? ConcGrossLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrLongOl")] public decimal? ConcGrossLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrShortOl")] public decimal? ConcGrossLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrLongOl")] public decimal? ConcNetLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrShortOl")] public decimal? ConcNetLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrLongOl")] public decimal? ConcNetLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrShortOl")] public decimal? ConcNetLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrLongOther")] public decimal? ConcGrossLe4TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortOther")] public decimal? ConcGrossLe4TdrShortOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongOther")] public decimal? ConcGrossLe8TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortOther")] public decimal? ConcGrossLe8TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe4TdrLongOther")] public decimal? ConcNetLe4TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe4TdrShortOther")] public decimal? ConcNetLe4TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe8TdrLongOther")] public decimal? ConcNetLe8TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe8TdrShortOther")] public decimal? ConcNetLe8TdrShortOther { get; init; }
    [JsonPropertyName("contractUnits")] public string? ContractUnits { get; init; }
```

The `Other` block is 36 of these 128 and is **not** dead weight: 118 of 545 measured rows carry a non-zero
value in at least one `Other` field, across 14 distinct symbols. Dropping the block to save width would
silently lose real data for those contracts.

## Serialisation

**No new converter is written.** This is the first coverage slice of this size to need none, and it is worth
recording why each candidate did not become one:

- COT's `date` arrives as `"2024-02-27 00:00:00"` — 19 characters with a ` 00:00:00` tail on **every** row of
  both COT paths. `NullableDateAtMidnightJsonConverter` already parses exactly `uuuu-MM-dd HH:mm:ss` to a
  `LocalDate`.
- Every other date in the slice is a plain `uuuu-MM-dd` and takes `NullableLocalDateJsonConverter`.
- No field in the slice is a scalar-or-string union, so `ScalarAsStringJsonConverter` from #31 is not needed.
- No field is an object-or-empty-string union, so `NetWorthRangeJsonConverter`'s shape does not recur.

Twelve `[JsonSerializable]` entries are added to `FmpJsonContext` — one `List<T>` per record.

## Testing

Every trap gets a test that fails when the trap is reintroduced.

| test | fails when |
|---|---|
| the three misspellings bind from a fixture | any `[JsonPropertyName]` is "corrected" to the English spelling |
| a fixture carrying both `Ol` and `Old` fields binds both | the suffix is normalised in the attribute rather than the property |
| all 23 `EconomicIndicator` wire strings asserted verbatim | a member's wire string is edited or a member dropped |
| `GetListAsync` on `Invalid name` raises `FmpApiException` | the transport guard is removed |
| `Cik` keeps `"0000320193"` | `cik` is retyped numeric |
| `IndustryRank` binds `"3 out of 9"` | it is retyped `int?` |
| `ReversalTrend` binds a real `true` | it is retyped `string?` on #31's precedent |
| COT `"2024-02-27 00:00:00"` parses to 2024-02-27 | the plain-date converter is used instead |
| the three transcript records each bind their own field names | one is "harmonised" with its siblings |
| `GetBenchmarkAsync(2020)` builds `?year=2020` and no `sector` | a sector parameter is added |
| `GetTranscriptAsync` builds `symbol`, `year` and `quarter` | a parameter is renamed to match the response |

URL-shape tests assert on the built request for every method, following the existing `Binding.cs` and
`CongressTests.cs` patterns. Fixtures are trimmed captures of the measured responses — with the API key never
present, since it travels in the query string.

`CotReport` gets a binding test that asserts a representative field from each of the four blocks rather than
all 128: `openInterestAll`, `pctOfOiNoncommLongOld`, `tradersTotOther` and `changeInNoncommSpreadAll`. The
generated-from-measurement property list is the guard against transcription error; a 128-assertion test would
restate it without adding a check.

## Live smoke sweep

Twelve new probes in `Probe.cs`, one per path, recorded in `baseline-ordinary.txt`. Every one must record
`outcome rows` — `outcome empty` is a failure, not a result.

Two probe constants need care, because the wrong choice records a false `empty`:

- the indicator probe uses `EconomicIndicator.Gdp`, **not** `Inflation` or
  `ThreeMonthCertificateOfDepositRate`, both of which return a legitimate empty array;
- the COT probes must not pass a date range wider than the caps above, or `analysis` returns 13 rows where
  `report` returns many more and the two look inconsistent for no reason.

Nothing is asserted by index: the transcript feed churns over tens of minutes.

## Documentation

- `README.md`'s generated coverage block is regenerated — 166 becomes 178 of 243. The block is machine-written
  between its markers by `EndpointCoverageTests`; the prose below it is hand-maintained and must be updated to
  match.
- `EconomicsEndpoints`' type-level summary is rewritten: it currently promises only a calendar.
- `DirectoryEndpoints.GetTranscriptSymbolsAsync`'s note that transcripts "are not modelled" is updated to point
  at `fmp.Transcripts`.
- The csproj's CS1591 comment goes from seven exemptions to eight.

## Out of scope

- **Chunking around the row caps.** The SDK does not silently issue multiple requests to work around
  truncation, here or on `economic-calendar`. The caps are documented and the caller chunks.
- **De-duplicating `GetLatestAsync` across pages.** The overlap is documented and the caller de-duplicates;
  hiding it would mean buffering pages and guessing when to stop.
- **Normalising the transcript field names** across the three records. Covered above.
- **A sector filter on `esg-benchmark`** implemented client-side. The endpoint returns 1003 rows across 291
  sectors and the caller can filter a list; a method parameter that looks like a query parameter but is
  applied locally would misrepresent what the request did.
- **`FinancialRatios.cs`'s header comment**, which says "The 56 properties below" and has 66. Noticed while
  measuring the widest existing record; unrelated to #40 and left alone.
