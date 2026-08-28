# Form 13F and Insider Trades — design

What issue [#36](https://github.com/herbertsabanal/fmpdotnet/issues/36) builds, and why each choice is the one
it is. Every factual claim here traces to
[the measurements](2026-08-28-form-13f-and-insider-trades-measurements.md), taken 2026-08-28 across 99 live
responses.

**Fourteen paths. Two new facades. Thirteen new records, 195 fields. No new converter.**

Coverage goes from **140 of 243** to **154 of 243**.

## Scope, and one deliberate redistribution

Issue #36 groups the fourteen paths as *Form 13F* (8) and *Insider Trades* (6). The code splits them 9 and 5:
**`stable/acquisition-of-beneficial-ownership` moves to the institutional side.**

It is an SC 13D/G filing — the disclosure an investor makes on crossing 5% of a class. Its subject is an
institutional stake, its fields are voting and dispositive power, and its reporting person is an entity
(`"The Vanguard Group"`, `"General Star National Insurance Company"`). It shares nothing with a Form 4
transaction but the word "ownership". Filed next to `insider-trading/*` it would be the only path in that
facade that is not an insider transaction; filed next to `institutional-ownership/*` it is one more view of who
holds what.

The SEC Filings slice set this precedent, redistributing 3 of its 12 paths to `fmp.Directory` and `fmp.Search`
rather than letting an issue's grouping dictate the public surface. **The issue tracks the work; it does not
design the API.**

## Public surface

### `fmp.InstitutionalOwnership` — new, 9 paths

```csharp
public sealed class InstitutionalOwnershipEndpoints(FmpTransport transport)
{
    public const int MaxOwnershipPageSize = 1000;

    // — discovery —
    Task<IReadOnlyList<FilingQuarter>> GetFilingDatesAsync(
        string cik, CancellationToken ct = default);

    // — one filer, one quarter —
    Task<IReadOnlyList<InstitutionalHolding>> GetHoldingsAsync(
        string cik, int year, int quarter, CancellationToken ct = default);

    Task<IReadOnlyList<HolderIndustryBreakdown>> GetHolderIndustryBreakdownAsync(
        string cik, int year, int quarter, CancellationToken ct = default);

    // — one filer, every quarter —
    Task<IReadOnlyList<HolderPerformance>> GetHolderPerformanceAsync(
        string cik, CancellationToken ct = default);

    // — one symbol —
    Task<IReadOnlyList<HolderAnalytics>> GetHolderAnalyticsAsync(
        string symbol, int year, int quarter,
        int page = 0, int limit = 100, CancellationToken ct = default);

    Task<SymbolPositions?> GetSymbolPositionsAsync(
        string symbol, int year, int quarter, CancellationToken ct = default);

    Task<IReadOnlyList<BeneficialOwnership>> GetBeneficialOwnershipAsync(
        string symbol, int limit = 100, CancellationToken ct = default);

    // — market-wide —
    Task<IReadOnlyList<IndustryOwnershipSummary>> GetIndustrySummaryAsync(
        int year, int quarter, CancellationToken ct = default);

    Task<IReadOnlyList<InstitutionalFiling>> GetLatestFilingsAsync(
        int page = 0, int limit = 100, CancellationToken ct = default);
}
```

### `fmp.InsiderTrades` — new, 5 paths

```csharp
public sealed class InsiderTradesEndpoints(FmpTransport transport)
{
    Task<IReadOnlyList<InsiderTrade>> GetLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default);

    Task<IReadOnlyList<InsiderTrade>> SearchAsync(
        string? symbol = null, string? reportingCik = null, string? companyCik = null,
        string? transactionType = null,
        int page = 0, int limit = 100, CancellationToken ct = default);

    Task<IReadOnlyList<InsiderTradeStatistics>> GetStatisticsAsync(
        string symbol, CancellationToken ct = default);

    Task<IReadOnlyList<InsiderReportingName>> SearchReportingNameAsync(
        string name, CancellationToken ct = default);

    Task<IReadOnlyList<InsiderTransactionType>> GetTransactionTypesAsync(
        CancellationToken ct = default);
}
```

### Why the signatures differ from path to path

Three decisions are load-bearing, and all three are measured rather than conventional.

**`limit` appears on five methods and nowhere else.** Nine of the fourteen paths accept `limit` and ignore it:
`extract` returns all 4,177 rows for `limit=5`, byte-identical to no limit at all. A parameter that is
accepted, ignored, and invisible in the response is worse than no parameter — the caller has no way to learn it
did nothing. So the five that honour it get it, and the nine that do not are not offered it.

**`page` appears on four, not five.** `acquisition-of-beneficial-ownership` honours `limit` and ignores
`page` — `page=0` and `page=1` returned byte-identical responses. Honouring one does not predict honouring the
other, so each was measured separately, and `GetBeneficialOwnershipAsync` takes `limit` with no `page`.

**`year` and `quarter` are required, never optional.** All five quarter-keyed paths return
`400 … missing query parameter - quarter` when it is withheld. `GetFilingDatesAsync` exists precisely to tell a
caller which pairs are valid — it is the only path that enumerates them.

**`GetSymbolPositionsAsync` returns a single nullable record, not a list.** The path returns an array, but it
carried exactly one row for every symbol measured, and its 36 fields are all market-wide aggregates for that
symbol and quarter. It follows the shipped precedent exactly — `SecFilingsEndpoints.GetProfileAsync` calls
`GetListAsync` and then `rows.Count > 0 ? rows[0] : null` (`SecFilingsEndpoints.cs:49-56`), rather than
`GetObjectAsync`, because the wire shape really is a list. An unknown symbol returns `[]`, which surfaces as
`null`.

**`SearchAsync` takes four optional discriminators.** All four were measured to filter correctly against
`insider-trading/search`; with none supplied it degenerates to the same feed as `GetLatestAsync`. That is not a
duplicate method — `latest` is a distinct path and stays mapped to its own.

## Models

Thirteen records, 195 fields. Every one is a `sealed record` with `init` properties and explicit
`[JsonPropertyName]`, matching the shipped models.

| record | path(s) | fields |
|---|---|---|
| `FilingQuarter` | `institutional-ownership/dates` | 3 |
| `InstitutionalHolding` | `institutional-ownership/extract` | 14 |
| `HolderAnalytics` | `institutional-ownership/extract-analytics/holder` | 39 |
| `HolderIndustryBreakdown` | `institutional-ownership/holder-industry-breakdown` | 12 |
| `HolderPerformance` | `institutional-ownership/holder-performance-summary` | 33 |
| `IndustryOwnershipSummary` | `institutional-ownership/industry-summary` | 3 |
| `InstitutionalFiling` | `institutional-ownership/latest` | 8 |
| `SymbolPositions` | `institutional-ownership/symbol-positions-summary` | 36 |
| `BeneficialOwnership` | `acquisition-of-beneficial-ownership` | 15 |
| `InsiderTrade` | `insider-trading/latest` **and** `insider-trading/search` | 16 |
| `InsiderTradeStatistics` | `insider-trading/statistics` | 13 |
| `InsiderReportingName` | `insider-trading/reporting-name` | 2 |
| `InsiderTransactionType` | `insider-trading-transaction-type` | 1 |

### `InsiderTrade` is shared by two paths, and nothing else is

`insider-trading/latest` and `insider-trading/search` return the same sixteen keys **in the same order** —
verified, not assumed. One record serves both. No other pair in this slice shares a shape: `formType` appears
on both `institutional-ownership/latest` (`13F-HR`, `13F-HR/A`, `13F-NT`, `13F-NT/A`) and
`insider-trading/latest` (`3`, `4`, `4/A`), but those are two different vocabularies wearing one field name,
and unifying them would model a coincidence.

### Every money, share and percentage field is `decimal?`

The single most consequential decision, and it is deliberately made *against* the local evidence.

`marketValue`, `value`, `performance` and their siblings were integral on **all 7,946 rows** sampled across
`extract` and `extract-analytics/holder`. On that evidence `long?` is the obvious type.

`industryValue` on `industry-summary` is the same kind of quantity — an aggregate dollar value — and it is
fractional on **53 of 394 rows** in 2025 Q4, with values like `523604028974.8208`. The family does it. Which
member does it, and in which quarter, is not stable.

This is exactly the reasoning that shipped `CompanyProfile.Volume` as `long?` against a wire value that turns
out fractional, corrected 2026-08-28 after it broke a live sweep. `System.Text.Json` **throws** on a fractional
value bound to an integer property, and `FmpTransport` does not wrap `DeserializeAsync` — so one such field
costs the caller the entire response, not the one field.

`decimal?` throughout, including the fields that look like counts:

- `shares`, `sharesNumber`, `numberOf13Fshares` and their `last`/`change` variants — `sharesNumber` already
  sits at 90% of `int`'s ceiling, and `securitiesOwned` on the insider paths is fractional on 5.9% of rows.
- `securitiesTransacted` — fractional on 4.0% of 1,000 rows.
- every `*Percentage`, `weight`, `ownership`, `turnover`, `price` and `ratio` field.

**Genuine counts stay `int?`.** `portfolioSize`, `securitiesAdded`, `securitiesRemoved`, `investorsHolding`,
`newPositions`, `increasedPositions`, `closedPositions`, `reducedPositions`, `holdingPeriod`,
`averageHoldingPeriod*`, `acquiredTransactions`, `disposedTransactions`, `totalPurchases`, `totalSales`,
`year`, `quarter` — these count filings, positions, quarters and transactions. None was ever fractional and
none approaches `int`'s range. Typing them `decimal?` to be safe would make the API worse to read for no
measured reason.

`totalCalls`, `totalPuts` and their variants are option **contract** counts, max 188,086,543 observed — `int?`
holds them, but they are share-adjacent quantities on a path whose every sibling is `decimal?`, so they take
`decimal?` for consistency within `SymbolPositions`.

### `ownershipPercent` gets no validation

It exceeds 100 on two of six symbols measured (MSFT 128.2744, AAPL 110.1329). 13F double-counts shares held
through multiple reporting managers, so a sum over filers legitimately passes shares outstanding. **No clamp,
no range check, no percentage wrapper type.** The doc comment says why, so the next reader does not "fix" it.

### `InstitutionalHolding.Symbol` is nullable, and that matters

`symbol` is null on **2,209 of 7,346 rows — 30.1%**. A 13F holding need not have a ticker: bonds, warrants and
private placements do not. A consumer keying holdings by symbol silently drops three in ten. The property is
`string?` and its doc comment carries the measured rate.

### `putCallShare` is modelled even though it is always blank

Blank on **all 7,346 rows** of `extract` across three filers — never null, never populated. The same field on
`extract-analytics/holder` *is* populated (`"Share"`).

It is modelled anyway. Omitting a field FMP sends leaves a consumer no way to reach it if FMP starts
populating it; modelling a constant costs one property. The doc comment records that it was blank on every row
measured, with the date, so the emptiness reads as a measurement rather than a bug.

### `InsiderTransactionType` is a one-field record, not an enum and not a bare string

`insider-trading-transaction-type` returns 18 codes (`A-Award` … `Z-Trust`), and every `transactionType` on
1,000 measured rows is drawn from that list — plus the empty string on 40 of them.

**Not an enum:** the list is served by an endpoint, so FMP can extend it without an SDK release, and the blank
would have no member to map to. A closed C# enum over an open server-side list is a breaking change waiting
for a Tuesday.

**Not `IReadOnlyList<string>`:** the wire shape is `[{"transactionType": "A-Award"}, …]`, and projecting it to
bare strings needs a converter whose only job is to discard a key. If FMP adds a description field, the record
absorbs it and the projection would have to be unpicked.

## Serialisation

**No new converter.** The shipped set covers every shape in this slice — which is worth stating, because two
of the mappings are not the obvious ones.

| shape | converter |
|---|---|
| ISO date (`2026-08-14`) | `NullableLocalDateJsonConverter` |
| date-at-midnight (`2026-08-28 00:00:00`) | `NullableDateAtMidnightJsonConverter` |
| real timestamp (`2026-08-28 15:47:03`) | `NullableLocalDateTimeJsonConverter` |
| numeric string (`"1099168953"`) | `TolerantDecimalJsonConverter` |
| mixed `int`/`float` number | `decimal?`, no converter |

### The date trap, and the test that guards it

`acceptedDate` and `filingDate` change wire format by path:

| path | `filingDate` | `acceptedDate` |
|---|---|---|
| `institutional-ownership/latest` | `2026-08-28 00:00:00` | `2026-08-28 15:47:03` |
| everything else in this slice | `2026-08-14` | `2026-08-14` |

On `institutional-ownership/latest`, `filingDate` is midnight on **1000 of 1000** rows — a date wearing a
datetime's clothes — while `acceptedDate` is midnight on **0 of 1000**. So `InstitutionalFiling` takes
`NullableDateAtMidnightJsonConverter` for one and `NullableLocalDateTimeJsonConverter` for the other, and every
other record in the slice takes `NullableLocalDateJsonConverter`.

**Why this earns a test rather than a comment:** `NullableLocalDateJsonConverter` parses with
`LocalDatePattern.Iso` and **returns null on a parse failure rather than throwing**
(`NodaConverters.cs:35-48`). Point it at `institutional-ownership/latest.filingDate` and every row reads
`null` — no exception, no failing assertion, nothing in a diff. The guard is a fixture-backed test asserting
the parsed date, so repointing the converter turns it red.

### `BeneficialOwnership`'s six string numerics

All six arrive as JSON **strings**: `{"soleVotingPower": "0", "percentOfClass": "7.48"}`. Across 422 rows every
non-null value parsed as a number — no `"N/A"`, no separators. `TolerantDecimalJsonConverter` already reads a
`String` token via `decimal.TryParse` with `NumberStyles.Float`, invariant, returning null on failure and never
throwing. It is used as shipped.

### `FmpJsonContext`

Thirteen `[JsonSerializable(typeof(List<T>))]` entries, one per record. **A missing registration fails at
runtime, not compile time** — the reason every slice checks this explicitly rather than trusting the build.
`SymbolPositions` needs both `List<SymbolPositions>` (the wire shape) and `SymbolPositions` if unwrapped
through `GetObjectAsync`.

## Error surface

Guards match the shipped convention exactly:

- `ArgumentException.ThrowIfNullOrWhiteSpace` on every required `string` — `cik`, `symbol`, `name`.
  **`ArgumentNullException` for a null argument, `ArgumentException` for blank** — two exception types, so the
  tests need two `[Fact]`s, because `Assert.ThrowsAsync<T>` matches exactly.
- `ArgumentOutOfRangeException.ThrowIfNegative(page)`, `ThrowIfNegativeOrZero(limit)`,
  `ThrowIfGreaterThan(limit, MaxOwnershipPageSize)` on the five paged methods.
- `quarter` is validated to 1–4 with `ThrowIfLessThan`/`ThrowIfGreaterThan`. `year` is **not** range-checked —
  an out-of-range year returns `[]` with HTTP 200, which is a legitimate "no data" answer, and guessing a floor
  would invent a fact the measurements do not have.

`SearchAsync`'s four discriminators are all optional and unvalidated beyond null-or-whitespace being treated as
absent — a search with no criteria is a valid call.

## Testing

Unit tests follow the shipped pattern: a `StubHandler` fixture per path, asserting the built query string and
the parsed record. **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the
response after reading the body — so a test needing two calls needs two handlers.

Fourteen fixtures, captured from the live responses already taken, **with the API key stripped**. The key
travels in the query string; no fixture and no test may contain a built URL.

### The traps that get a test each

Each of these fails if the trap is reintroduced — the house rule:

1. **Fractional share counts.** `InsiderTrade.SecuritiesOwned` parses `28447.467` and
   `SecuritiesTransacted` parses `8375.5601` — IBM's real measured values. Retyping either as `long?` throws
   and the test goes red.
2. **`int` overflow.** `HolderAnalytics.MarketValue` parses `336524794350`; `SymbolPositions.TotalInvested`
   parses `2840158192185`; `IndustryOwnershipSummary.IndustryValue` parses `523604028974.8208`. Any `int?`
   retyping fails.
3. **The date-format divergence.** `InstitutionalFiling` parses `filingDate` `"2026-08-28 00:00:00"` to
   `2026-08-28` and `acceptedDate` `"2026-08-28 15:47:03"` to a `LocalDateTime` with its time intact.
   Repointing either converter fails.
4. **Null `symbol` on holdings.** A fixture row with `"symbol": null` parses to a record with `Symbol is
   null` and every other field populated.
5. **String numerics.** `BeneficialOwnership.PercentOfClass` parses `"7.48"` to `7.48m`, and a null
   `sharedVotingPower` parses to null rather than throwing.
6. **Blank vs null.** `InsiderTrade` fixture rows carrying `"transactionType": ""` and
   `"directOrIndirect": null` both parse, and the blank stays a blank rather than becoming null.
7. **The ignored `limit`.** `GetHoldingsAsync` has no `limit` parameter. The guard is a test asserting the
   built query contains no `limit` key — so adding one that FMP would silently drop fails the build.
8. **`ownershipPercent` over 100.** `SymbolPositions.OwnershipPercent` parses `128.2744` unclamped.

### Live guard

`SweepCoverageTests` walks every endpoint with arguments `Probe.Argument` synthesises from the parameter name.
Its `string` arm ends `_ => LiveApi.Symbol`, so an unrecognised name silently becomes `"AAPL"` — which against
a path that answers `[]` with HTTP 200 produces a baseline that agrees with itself forever. **This slice walks
into that blind spot twice**, and both were measured, not predicted.

**Hazard 1 — `cik` means the wrong thing here.** `Probe.Argument` maps `cik` to `LiveApi.Cik`, which is
`"320193"`: Apple's CIK, an *issuer*. The four `cik`-keyed 13F paths want an institutional *filer's* CIK. All
four return `[]` for Apple's:

| path, with `cik=320193` | rows |
|---|---|
| `institutional-ownership/dates` | 0 |
| `institutional-ownership/extract` | 0 |
| `institutional-ownership/holder-industry-breakdown` | 0 |
| `institutional-ownership/holder-performance-summary` | 0 |

A new **`LiveApi.FilerCik`** constant is required, distinct from `LiveApi.Cik`, with `Probe.Argument` selecting
between them by declaring type — the mechanism the `from` arm already uses to separate calendar from economics
semantics. Berkshire's `0001067983` returned 53, 29, 24 and 53 rows against those four.

**Hazard 2 — `SearchAsync`'s optional discriminators all collapse to `"AAPL"`.** Nothing in the smoke suite
inspects `IsOptional`, so `Probe` supplies *every* parameter, optional ones included. `reportingCik`,
`companyCik` and `transactionType` are all unknown to the `string` arm and all fall to `LiveApi.Symbol`.
Measured 2026-08-28: `insider-trading/search?symbol=AAPL&reportingCik=AAPL&companyCik=AAPL&transactionType=AAPL`
returns **`[]`** — and so does the two-parameter form with only `transactionType` bogus. The sweep would record
`outcome empty` for a method that works perfectly.

`Probe.Argument` needs cases for all three, keyed to real values: a reporting CIK, a company CIK, and a
transaction type drawn from the eighteen `insider-trading-transaction-type` serves.

Two smaller notes on the same mechanism, recorded so the next reader does not rediscover them:

- **`quarter` fails loudly, which is correct.** `Probe.Argument`'s `int` arm ends
  `_ => throw Unknown(parameter)` (`Probe.cs:404`) and does not know `quarter`, so it throws rather than
  silently defaulting. It needs a case; the failure mode is the right one.
- **`reporting-name` works by luck.** `name` maps to `LiveApi.AcquirerNameQuery` (`"Apple"`), which happens to
  match a real reporting person, `Apple Allan Victor`. It returns rows for a reason unrelated to intent. Left
  alone, with a comment saying so, rather than tidied into looking deliberate.

**One new guard `[Fact]` joins `SweepCoverageTests`**, not one per path: the file's nine existing facts are
semantic guards, each pinning one argument choice the generic walk cannot check — the shape of
`The_sweep_asks_the_ma_search_for_a_company_name_rather_than_a_ticker`. The new one asserts the sweep asks the
institutional-ownership paths for a *filer* CIK rather than an issuer CIK. The two generic facts —
`The_sweep_can_supply_arguments_for_every_endpoint_method` and
`The_sweep_can_read_rows_out_of_every_endpoint_return_type` — cover the fourteen new methods without change.

`baseline-ordinary.txt` gains fourteen blocks. **`baseline-bulk.txt` is not touched.**

## Documentation

`README.md`'s coverage table is generated by `EndpointCoverageTests` driving every endpoint against a stub — it
is not hand-edited. Fourteen rows appear across the two new facades. The prose counts move: **140 → 154
modelled**, 103 → 89 remaining, 96 → 82 actionable, ten → nine open issues, nine → eight actionable.

Epic #25 is re-reconciled the same way as the last two slices: #36 moves to the Shipped table, its row leaves
the remainder, and the subtotals and the `243 − 154` partition sentence are re-verified rather than assumed.

## Out of scope

Named so they are decisions rather than omissions:

- **`NullableLocalDateJsonConverter`'s non-string-token defect.** It calls `reader.GetString()` without
  guarding `TokenType`, the same latent bug fixed on `BusinessAddressJsonConverter` and avoided on
  `PublisherListJsonConverter`. Every date field in this slice arrives as a string or absent, so the risk stays
  hypothetical here — and the converter is shared by many shipped endpoints, which is exactly why changing it
  deserves its own slice rather than riding along on this one.
- **Auto-paging the five paged paths.** They report nothing about total size, so a caller cannot tell a full
  page from a last page. Walking pages needs request-count limits and partial-failure semantics — a slice of
  its own, and the same one `CalendarResult<T>`'s truncation reporting is waiting on.
- **A truncation wrapper like `CalendarResult<T>`.** No path in this slice truncates. `extract` returns all
  4,177 rows; the paged paths return what they are asked for. There is nothing to report.
- **Folding `EarningsCalendarResult` into `CalendarResult<T>`.** Still outstanding from the last slice, still
  not a rider on this one.
- **Any `*-bulk` variant.** Untouched by policy.
