# Statements coverage — design

Issue #28: the 19 unmodelled paths of FMP's Statements section, taking `fmp.Statements` from 8 of 27
paths to 27 of 27.

Everything here argues from `docs/superpowers/specs/2026-08-27-statements-measurements.md`, measured
2026-08-27 against the live API. Where this document states a row count, a cap or a field set, that
file is the evidence.

## The shapes

The 19 paths are not 19 designs. They collapse into six families, and the family — not FMP's
documentation section — decides the type:

| family | paths | answer shape |
|---|---|---|
| TTM statements | `income-statement-ttm`, `balance-sheet-statement-ttm`, `cash-flow-statement-ttm` | the base statement's field set, rolling |
| growth and TTM metrics | the three `*-growth`, `key-metrics-ttm`, `ratios-ttm` | the CSV bulk field set, exactly |
| as-reported | the three `*-as-reported`, `financial-statement-full-as-reported` | envelope + open XBRL dictionary |
| segmentation | `revenue-product-segmentation`, `revenue-geographic-segmentation` | envelope + segment-name dictionary |
| report access | `financial-reports-dates`, `financial-reports-json`, `financial-reports-xlsx` | links, a document, a zip |
| market-wide feed | `latest-financial-statements` | paged recency feed |

Eight of the nineteen therefore need **no new model at all** — a result worth stating plainly,
because the naive reading of the issue is nineteen new record types.

## The seven things a shape-only reading would get wrong

### 1. The SDK is already truncating every statement history at 5 rows

`Periodic()` omits `limit` when the caller passes none, and FMP's undocumented default is 5.
`GetIncomeStatementAsync("AAPL")` returns 5 rows of 41 today, with nothing in the XML documentation
to say so and no `<param>` tag on `limit` at all.

This is a shipped defect, not a new-path concern, and it is fixed here: **when the caller passes no
limit, the SDK sends `limit=100000`**, so "no limit" means what it says. The number is chosen the way
`symbol-change`'s is — measured to sit above every observed total (the deepest series found was
`income-statement-ttm` at 164 rows) with no server cap above it, verified at `limit=1000`, `10000`
and `100000` all returning identical counts.

The alternative — document the 5 and leave it — was rejected on the precedent already set for
`symbol-change`, where the SDK sends an explicit limit precisely because FMP's default hid 98% of
the data. A default that silently returns 12% of a company's history is the same defect.

### 2. `period` has five values, and the SDK models two

`period` accepts `Q1` through `Q4` as *filters across years*, not just `annual` and `quarter`:

```
period=annual  ->  FY2025, FY2024, FY2023, FY2022 …
period=quarter ->  Q32026, Q22026, Q12026, Q42025 …
period=Q1      ->  Q12026, Q12025, Q12024, Q12023 …
```

This works on the eight paths the SDK already ships, so the gap is in `FiscalPeriod`, not in the new
work. `FiscalPeriod` gains `Q1`, `Q2`, `Q3` and `Q4` **after** the existing members, so `Annual` and
`Quarter` keep ordinals 0 and 1 and no shipped caller changes behaviour or recompiles differently.

`FiscalPeriod`'s own summary currently justifies the enum by saying FMP "uses two different
vocabularies for the same concept — the request takes `annual`/`quarter` while the response labels
rows `FY`/`Q1`-`Q4` — and an enum keeps a caller from posting a response value back as a request
value." That argument inverts once `Q1` is legal in both directions, so it is rewritten rather than
left to mislead. The enum still earns its place: it makes the five legal values discoverable and
rejects the sixth.

**An unrecognised `period` falls back to annual silently** — `period=bogus` answers FY rows at
HTTP 200. That is the reason `ToQueryValue` throws on an undeclared enum member instead of emitting
something FMP will quietly reinterpret.

### 3. `owner-earnings` truncates at 50 and the payload cannot say so

Every long-history filer returns exactly 50 rows at `limit=100000` — AAPL, MSFT, GE, KO, JPM, IBM
and PG all stop around 2013-2014 — while `income-statement-ttm` reaches 1985 for the same symbols.
SHOP returns 46, which is its real history.

So a count below 50 is data and a count of exactly 50 is a truncation, and nothing distinguishes
them. `MaxOwnerEarningsRows = 50` records the ceiling, and the method's documentation states the
ambiguity rather than implying the series is complete.

### 4. `financial-reports-xlsx` lies about its content type, and its errors are 200s

It answers `Content-Type: application/json; charset=utf-8` with a body starting `PK\x03\x04` — a
1.4 MB XLSX zip. A miss is **also HTTP 200**: exactly 16 bytes, `Error with query`, still under the
JSON content type. Neither the status code nor the content type distinguishes success from failure.

The only reliable test is the magic number, so that is what the SDK uses: read the response, and
treat a body beginning `PK\x03\x04` as the workbook and anything else as a miss returning `null`.
Null is right rather than an exception because the same 16 bytes cover both "no such symbol" and
"no filing that year" — the two cases the SDK already returns null for on `GetScoresAsync`.

### 5. The as-reported dictionary is open, and its values are not all numbers

`financial-statement-full-as-reported` for AAPL FY2025 holds 234 ints, 47 strings and 19 floats in
one object, and its key count swings 300 → 923 between AAPL and JPM. The strings are filing
metadata: `documenttype: "10-K"`, `currentfiscalyearenddate: "--09-27"`.

No record can express that, and `Dictionary<string, decimal>` throws on the 47. It is
`IReadOnlyDictionary<string, JsonElement>`, which is honest about what arrived and costs the caller
one `GetDecimal()`.

### 6. Segmentation shares the envelope but not the problem

Both segmentation paths answer the same five-field envelope, so the temptation is one type for all
six. Measured across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B and KO, both endpoints, both cadences,
every row — not a sample — the values were 3,201 ints and 36 floats and **no strings at all**.

Segmentation is genuinely segment-name-to-number, so it gets its own type with
`IReadOnlyDictionary<string, decimal>`. Sharing a field layout is not a reason to share a type when
one of the two has a proven value domain and the other does not. If FMP ever emits a string there,
the throw is the correct outcome — a non-numeric segment revenue is a defect worth hearing about.

### 7. Reusing five models means adding JSON attributes they have never needed

`ratios-ttm`, `key-metrics-ttm` and the three `*-growth` paths carry field sets **identical** to the
CSV bulk headers the SDK already models — FMP's typos included
(`growthNetCashProvidedByOperatingActivites`) and its `TTM` suffixes included
(`grossProfitMarginTTM`).

But those five records were built for CSV, which maps them by an explicit wire-name lookup, and
their C# property names deliberately drop the `TTM` suffix (`GrossProfitMargin`). The serializer
context sets `PropertyNameCaseInsensitive` and **no naming policy**, so JSON binding falls back to
the property name: `grossProfitMarginTTM` would not match `GrossProfitMargin`.

The failure mode is the dangerous one. Not an exception — 61 null metrics and a populated `symbol`.
So each of the five gains `[JsonPropertyName]` attributes carrying the wire spelling, and the tests
assert a non-null value for at least one suffixed field per type, so that deleting the attributes
fails the suite rather than emptying the data.

`balance-sheet-statement-ttm` reuses `BalanceSheetStatement` and omits exactly one of its 61 fields,
`capitalLeaseObligationsNonCurrent` — structurally, across all ten filers checked, not per filer. It
binds as null, which is an absence rather than a zero, and the method says so.

## Surface

All 19 land on `fmp.Statements`, which already owns the eight they extend. No new facade group.

```csharp
// TTM statements — reuse the base models, newest first
Task<IReadOnlyList<IncomeStatement>>        GetIncomeStatementTtmAsync(string symbol, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<BalanceSheetStatement>>  GetBalanceSheetTtmAsync(string symbol, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<CashFlowStatement>>      GetCashFlowTtmAsync(string symbol, int? limit = null, CancellationToken ct = default);

// growth — reuse the CSV models
Task<IReadOnlyList<IncomeStatementGrowth>>  GetIncomeStatementGrowthAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<BalanceSheetGrowth>>     GetBalanceSheetGrowthAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<CashFlowGrowth>>         GetCashFlowGrowthAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);

// TTM metrics — one row, no date, no period, no limit
Task<KeyMetricsTtm?>                        GetKeyMetricsTtmAsync(string symbol, CancellationToken ct = default);
Task<RatiosTtm?>                            GetRatiosTtmAsync(string symbol, CancellationToken ct = default);

// as-reported — open dictionaries
Task<IReadOnlyList<AsReportedStatement>>    GetIncomeStatementAsReportedAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<AsReportedStatement>>    GetBalanceSheetAsReportedAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<AsReportedStatement>>    GetCashFlowAsReportedAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<AsReportedStatement>>    GetFullStatementAsReportedAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default);

// segmentation — no limit; the endpoint ignores it
Task<IReadOnlyList<RevenueSegmentation>>    GetRevenueByProductAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, CancellationToken ct = default);
Task<IReadOnlyList<RevenueSegmentation>>    GetRevenueByGeographyAsync(string symbol, FiscalPeriod period = FiscalPeriod.Annual, CancellationToken ct = default);

// owner earnings — quarterly only, capped at 50
Task<IReadOnlyList<OwnerEarnings>>          GetOwnerEarningsAsync(string symbol, int? limit = null, CancellationToken ct = default);

// report access
Task<IReadOnlyList<FinancialReportLink>>    GetFinancialReportDatesAsync(string symbol, CancellationToken ct = default);
Task<FinancialReport?>                      GetFinancialReportAsync(string symbol, int year, FiscalPeriod period, CancellationToken ct = default);
Task<byte[]?>                               GetFinancialReportWorkbookAsync(string symbol, int year, FiscalPeriod period, CancellationToken ct = default);

// market-wide recency feed
Task<IReadOnlyList<LatestFinancialStatement>> GetLatestStatementsAsync(int page, int limit, CancellationToken ct = default);
IAsyncEnumerable<LatestFinancialStatement>    StreamLatestStatementsAsync(CancellationToken ct = default);
```

`period` is absent from `GetIncomeStatementTtmAsync` and `GetOwnerEarningsAsync` because both accept
it and ignore it — the TTM statements are a rolling series and owner earnings is quarterly only.
Sending a parameter the endpoint discards is the habit `GetScoresAsync` already avoids.

`limit` is absent from the segmentation and report-dates methods for the same reason: measured, they
ignore it and transfer the full set regardless.

The `structure` parameter FMP documents on the segmentation paths is **not sent**. Measured on AAPL
and on JPM — a filer with genuinely nested segments — `structure=flat` and `structure=hierarchical`
returned byte-identical payloads to sending nothing at all. It is inert, and a parameter that does
nothing still costs a caller the belief that it does something.

`GetKeyMetricsTtmAsync` and `GetRatiosTtmAsync` return a single nullable record rather than a list,
matching `GetScoresAsync`: the endpoint answers a one-element array, and `[]` for an unknown symbol.

## Constants

```csharp
public const int MaxOwnerEarningsRows      = 50;   // measured ceiling; 50 rows means "possibly truncated"
public const int MaxLatestStatementsPage   = 100;  // page 101 is HTTP 400
public const int MaxLatestStatementsPageSize = 250; // limit=1000 answers 250
```

`GetLatestStatementsAsync` rejects a page above 100 or a limit above 250 rather than letting FMP
clamp them, matching `MaxCikListPageSize`: a caller who asks for 1,000 rows a page and advances by
1,000 skips three quarters of the feed and never sees an error.

## Models

Six new types. Every numeric field is `decimal?`, matching the existing statements — the measured
maximum across as-reported dictionaries was 7.1e12 and the SDK's rule is that money is not a double.

- **`AsReportedStatement`** — `Symbol`, `FiscalYear`, `Period`, `ReportedCurrency`, `Date`, and
  `IReadOnlyDictionary<string, JsonElement> Data`. Serves all four as-reported paths.
- **`RevenueSegmentation`** — the same five fields, and `IReadOnlyDictionary<string, decimal> Data`.
- **`OwnerEarnings`** — 10 fields: `Symbol`, `ReportedCurrency`, `FiscalYear`, `Period`, `Date`,
  `AveragePpe`, `MaintenanceCapex`, `OwnersEarnings`, `GrowthCapex`, `OwnersEarningsPerShare`.
- **`FinancialReportLink`** — `Symbol`, `FiscalYear`, `Period`, `LinkJson`, `LinkXlsx`. The two
  links carry the literal string `YOUR_API_KEY` rather than a key, so they are not usable as-is;
  the type says so.
- **`LatestFinancialStatement`** — `Symbol`, `CalendarYear`, `Period`, `Date`, `DateAdded`. The only
  path in this group keyed on `calendarYear` rather than `fiscalYear`, and `dateAdded` is a
  space-separated datetime (`"2026-08-27 11:03:21"`), not ISO-8601 with a `T`.
- **`FinancialReport`** — `Symbol`, `Period`, `Year`, and
  `IReadOnlyDictionary<string, JsonElement> Sections`. The remaining 70 top-level keys are report
  section names truncated to about 30 characters (`"CONSOLIDATED STATEMENTS OF OPER"`), varying per
  filing, each holding a list of single-key objects mapping a column header to a list of cell
  strings. It is a rendered document, and the type does not pretend otherwise.

`fiscalYear` arrives as an `int` on six paths and a `string` on seven. One `int?` property reads both
**only because** `JsonNumberHandling.AllowReadingFromString` is set on the serializer context, which
makes that option load-bearing rather than incidental — a test asserts both wire forms bind.

Every new type is registered in `FmpJsonContext`, along with the five reused ones that have never
been JSON-deserialised before.

## Transport

Two gaps in `FmpTransport`, which today offers only `GetListAsync` and `StreamCsvAsync`:

- **`GetObjectAsync<T>`** — `financial-reports-json` answers a JSON object, not an array. Its miss is
  `{"Error Message": …}` at HTTP 200, which the transport's existing `ErrorTextFrom` already
  recognises, so this path inherits that handling rather than reimplementing it.
- **`GetBytesAsync`** — `financial-reports-xlsx` answers binary. It must not go near a JSON reader,
  and its miss detection is the `PK\x03\x04` magic number rather than a status code or a body parse.

## Testing

Every trap above gets a test that fails when the trap is reintroduced — the house rule, and the one
the `limit` guard violated last slice by being deletable with the suite still green.

Specifically:

- Deleting the `[JsonPropertyName]` attributes from any of the five reused CSV models fails a test.
  This is the highest-value test in the slice because the untested failure is silent nulls.
- The default `limit` sends `100000`, asserted on the request URI, for all 27 periodic paths — the
  nineteen new and the seven shipped.
- `period=Q1` reaches the wire as `Q1`; an undeclared `FiscalPeriod` throws.
- A body of `Error with query` returns null from `GetFinancialReportWorkbookAsync`; a body beginning
  `PK\x03\x04` returns the bytes.
- `page: 101` and `limit: 251` throw `ArgumentOutOfRangeException` before any request is made,
  asserted with `Assert.Empty(handler.Requests)`.
- `fiscalYear` binds from both `2025` and `"2025"`.
- An as-reported `data` object holding a string, an int and a float binds without throwing.
- The `latest-financial-statements` walker stops at a short page.

Fixtures are captured from the live responses recorded during the sweep, not hand-written, so the
tests assert against what FMP actually sent.

## Elsewhere

- **README** — the generated coverage block moves from "82 of FMP's 243 endpoint paths are modelled"
  to 101, regenerated via `FMPDOTNET_UPDATE_README` rather than edited by hand. The
  remaining-paths prose moves from 161 remaining / 154 actionable to 142 / 135.
- **`EndpointCoverageTests`** discovers endpoints by driving them against a stub, so the new methods
  are picked up automatically. Widening `FiscalPeriod` to six members drives each periodic method
  six times instead of twice; the paths deduplicate, so the table is unchanged in shape.
- **Issue #28** closes. **#25** loses 19 of its 154 actionable paths, and its thirteen actionable
  child issues become twelve. The README's "thirteen actionable issues" sentence moves with it.

## Deliberately not in scope

- **The seven shipped methods' signatures.** They gain the wider `FiscalPeriod` and the fixed default
  limit, but no renames and no new overloads. Retrofitting their documentation beyond the `limit`
  `<param>` tag belongs to whatever slice next touches them.
- **A typed reader for `financial-reports-json`'s sections.** The section names are truncated,
  filing-specific and unstable; anything typed over them would be a guess dressed as an API.
- **Anything that re-fetches `*-bulk`.** The CSV headers this design compares against were read from
  the on-disk developer cache captured 2026-08-26. Bulk is throttled at 2/min and FMP warns about
  abuse on those paths.
