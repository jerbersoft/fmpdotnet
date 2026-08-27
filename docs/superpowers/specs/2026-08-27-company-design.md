# Company coverage — design, 2026-08-27

Issue #29. Thirteen unmodelled `stable/` paths, modelled from
[the measurement record](2026-08-27-company-measurements.md) and nothing else. Completing this group
takes `fmp.Company` from 4 methods to 17, closes FMP's `Company` section at 17 of 17, and takes the SDK
from 101 to 114 of 243 documented paths.

Everything asserted here was measured on 2026-08-27 against an Ultimate key. Where the measurement and
FMP's documentation disagree, the measurement wins and the doc comment says so.

## Goal

Thirteen new public methods on the existing `CompanyEndpoints`, eight new models, one existing model
reused, and a regression test for every trap the measurement pass found. No new transport primitives:
each of the thirteen is an ordinary `GET` returning a JSON array, which `FmpTransport.GetListAsync`
already serves.

## Global constraints

- Target `net10.0`. Root namespace and assembly `FmpDotNet`; types keep the `Fmp` prefix where they
  are transport-level.
- Every model binds through `FmpJsonContext` source generation. **A new `List<T>` must be added to
  `FmpJsonContext` or it fails at runtime, not at compile time.**
- Models are `public sealed record` with `init` properties and explicit `[JsonPropertyName]` on every
  member. No `required` members: an absent JSON key binds an `init` member to `default` rather than
  honouring a field initialiser, so every property is nullable except where the measurement proved it
  present on every row.
- Dates that carry no time of day are `LocalDate?` via `NullableLocalDateJsonConverter`. EDGAR
  acceptance stamps are `Instant?` via `NullableEasternInstantJsonConverter` — they are EDGAR's wall
  clock, matching `acceptedDate` on the statement endpoints.
- Money and market capitalisation are `decimal?` via `TolerantDecimalJsonConverter`. Never `long?` —
  see "Market capitalisation is not integral" below.
- Every public member carries an XML doc comment recording what was measured, with the date.
- Each trap named below gets a test that fails if the trap is reintroduced.
- Fixtures are captured from the live responses in the measurement pass and must not contain the API
  key.

## The three rulings the measurement forced

### Market capitalisation is not integral, anywhere

`market-capitalization-batch` answered `4098415617064.9995` for `GOOG` on 2026-08-27 — one fractional
row in twenty. `long?` throws `JsonException` on that row and takes the whole batch with it.

**Every market-cap-shaped field in this slice is `decimal?` with `TolerantDecimalJsonConverter`**:
`MarketCapitalization.MarketCap`, `StockPeer.MarketCap`, and the six money fields on
`ExecutiveCompensation`. This is the `ScreenerResult.Volume` defect, found one day later by the same
method, and caught before shipping rather than after.

The regression test binds a fixture containing the exact `GOOG` row. A single-symbol fixture would not
have caught it, so the fixture is the twenty-symbol batch capture, not an `AAPL` capture.

### Two paths, one dataset — both are shipped

`employee-count` and `historical-employee-count` returned byte-identical bodies on every symbol probed:
`AAPL` 32 rows, `JPM` 5, `SHOP` 11, `XOM` 0 on both.

Both are shipped as public methods against one `EmployeeCount` model. Coverage is counted by documented
path, a caller reading FMP's docs looks up whichever name they found there, and neither method is
marked obsolete — a working endpoint should not raise a build warning. Each method's doc comment states
the measured identity, names the symbols and row counts it was measured on, and cross-references the
other with `<see cref="..."/>`.

### A signature must not accept a parameter the endpoint ignores

Three parameters FMP documents are ignored by the live API, measured 2026-08-27:

| endpoint | ignored | evidence |
|---|---|---|
| `mergers-acquisitions-search` | `page`, `limit` | `name=Bank` answers 233 rows bare and 233 with `page=0&limit=5` |
| `governance-executive-compensation` | `year` | `symbol=AAPL` and `symbol=AAPL&year=2025` are byte-identical, 339 rows |

**`SearchMergersAcquisitionsAsync` takes no `page` or `limit`, and `GetExecutiveCompensationAsync`
takes no `year`.** Accepting a parameter that changes nothing is a lie the compiler cannot catch and
the caller cannot see. The doc comments record why the parameter is absent, so a future reader who
finds it in FMP's docs does not add it back.

## The API

Thirteen methods on `CompanyEndpoints`, alphabetical by path within the group.

```csharp
Task<IReadOnlyList<CompanyNote>>                    GetNotesAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<EmployeeCount>>                  GetEmployeeCountAsync(string symbol, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<EmployeeCount>>                  GetHistoricalEmployeeCountAsync(string symbol, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<ExecutiveCompensationBenchmark>> GetExecutiveCompensationBenchmarkAsync(int? year = null, CancellationToken ct = default);
Task<IReadOnlyList<ExecutiveCompensation>>          GetExecutiveCompensationAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<MarketCapitalization>>           GetHistoricalMarketCapAsync(string symbol, LocalDate? from = null, LocalDate? to = null, int? limit = null, CancellationToken ct = default);
Task<IReadOnlyList<KeyExecutive>>                   GetKeyExecutivesAsync(string symbol, CancellationToken ct = default);
Task<MarketCapitalization?>                         GetMarketCapAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<MarketCapitalization>>           GetMarketCapBatchAsync(IEnumerable<string> symbols, CancellationToken ct = default);
Task<IReadOnlyList<MergerAcquisition>>              GetLatestMergersAcquisitionsAsync(int page, int limit, CancellationToken ct = default);
Task<IReadOnlyList<MergerAcquisition>>              SearchMergersAcquisitionsAsync(string name, CancellationToken ct = default);
Task<CompanyProfile?>                               GetProfileByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<StockPeer>>                      GetPeersAsync(string symbol, CancellationToken ct = default);
```

`GetMarketCapAsync` and `GetProfileByCikAsync` return a nullable single value, matching
`GetProfileAsync` and `GetSharesFloatAsync`: the endpoint answers a single-element array, and an
unknown-but-well-formed input answers an empty array with HTTP 200 rather than a 404.

### `GetHistoricalMarketCapAsync` — optional range, documented window

`from` and `to` are optional, matching FMP's own signature. The doc comment carries the whole
measurement, because the defaults are surprising in two independent ways:

- **Bare, the endpoint answers ~3 months, not history.** 65 rows, `2026-05-27 → 2026-08-27`, measured
  on `AAPL`.
- **`limit` cannot widen that window.** It clamps downward — `limit=5` gives 5 — and is ignored
  upward: `limit=5000` and `limit=100000` both answered the same 65 rows. Only `from`/`to` reach
  history.
- **A range is capped at exactly 5,000 rows and the cap keeps the newest.** `from=2000-01-01` and
  `from=1990-01-01` both answered 5,000 rows starting `2006-10-11`. A caller asking for all history
  gets the most recent 5,000 sessions with no indication anything was dropped; reaching further means
  walking backwards with `to`.

`ThrowIfBackwards` from `ChartEndpoints` is the precedent for validating the range — apply it when both
`from` and `to` are supplied.

### `GetMarketCapBatchAsync` — takes `IEnumerable<string>`, returns rows to match by symbol

The endpoint silently drops symbols it has no row for. Measured on the first 100 plain tickers of
`stable/stock-list`: 100 requested, **99 returned**, `WDSP` missing — a symbol FMP's own directory
lists. `AAPL,ZZZZNOPE` answers one row the same way.

No upper bound was found up to 500 symbols; the endpoint neither errors nor truncates. So the method
does not chunk and does not validate a cap that was not measured. It throws `ArgumentException` on an
empty sequence, because empty `symbols` answers **400**.

The doc comment states plainly that **the response is not positionally aligned with the request** and
that rows must be matched by `MarketCapitalization.Symbol`. That is the trap: zipping the two lists
corrupts every row after the first gap.

### `GetLatestMergersAcquisitionsAsync` — required page and limit

`page` and `limit` are required, not defaulted, matching `GetAllSharesFloatAsync` and
`GetDelistedAsync`: a page size and a page index have to agree for a walk to be complete, and a default
lets them disagree invisibly. `ArgumentOutOfRangeException` on a negative page or a non-positive limit.

`limit` is clamped upward at 1,000 by the server — `limit=5000` answered 1,000 rows. The doc comment
records the clamp and the archive's measured shape: **4,704 rows across pages 0–4 at `limit=1000`,
spanning 1994-01-10 → 2026-08-25**, page 4 short at 704, page 5 and beyond answering `[]` with HTTP
200. Stopping at the first short page saves a request.

"Latest" names the ordering, not the contents — page 0 at `limit=1000` already reaches back to
2021-09-13. The doc comment says so, because the method name does not.

## The models

Seven new files, eight new types. `profile-cik` adds no type.

### `Models/MarketCapitalization.cs` — `MarketCapitalization`

Three fields, serving `market-capitalization`, `market-capitalization-batch` and
`historical-market-capitalization`, which answered the identical shape on all three.

| property | wire | type | notes |
|---|---|---|---|
| `Symbol` | `symbol` | `string?` | |
| `Date` | `date` | `LocalDate?` | `NullableLocalDateJsonConverter` |
| `MarketCap` | `marketCap` | `decimal?` | `TolerantDecimalJsonConverter` — fractional in the wild |

### `Models/StockPeer.cs` — `StockPeer`

| property | wire | type | notes |
|---|---|---|---|
| `Symbol` | `symbol` | `string?` | |
| `CompanyName` | `companyName` | `string?` | |
| `Price` | `price` | `decimal?` | |
| `MarketCap` | **`mktCap`** | `decimal?` | the one endpoint in this group that does not spell it `marketCap` |

The `mktCap` spelling is the reason this needs its own model rather than reusing
`MarketCapitalization`. The doc comment records the inconsistency so nobody "fixes" the mapping.
`SPY` answers peers, so this is not equity-only.

### `Models/EmployeeCount.cs` — `EmployeeCount`

Nine fields, all present on every row measured, serving both `employee-count` and
`historical-employee-count`.

| property | wire | type | notes |
|---|---|---|---|
| `Symbol` | `symbol` | `string?` | |
| `Cik` | `cik` | `string?` | zero-padded, `"0000320193"` — a string, not a number |
| `AcceptanceTime` | `acceptanceTime` | `Instant?` | `NullableEasternInstantJsonConverter`; `"2025-10-31 06:01:26"`, space-separated, no `T` |
| `PeriodOfReport` | `periodOfReport` | `LocalDate?` | |
| `CompanyName` | `companyName` | `string?` | |
| `FormType` | `formType` | `string?` | `"10-K"` |
| `FilingDate` | `filingDate` | `LocalDate?` | |
| `Employees` | `employeeCount` | `int?` | **not** `EmployeeCount` — see below |
| `Source` | `source` | `string?` | EDGAR URL |

The headcount property is `Employees`, not `EmployeeCount`. C# forbids a member sharing its enclosing
type's name (CS0542), and `Count` — the other obvious spelling — reads as a collection count on a type
that arrives in a list. The wire name stays `employeeCount` via `[JsonPropertyName]`.

The type doc records that `XOM` — a major filer — answers zero rows on both paths, so an empty result
is normal rather than a symptom.

### `Models/CompanyNotes.cs` — `CompanyNote`

Four fields, and three traps.

| property | wire | type | notes |
|---|---|---|---|
| `Cik` | `cik` | `string?` | |
| `Symbol` | `symbol` | `string?` | **not the issuer's ticker** |
| `Title` | `title` | `string?` | **HTML-escaped** |
| `Exchange` | `exchange` | `string?` | null on 19 of `T`'s 20 rows |

`Symbol` names the note, not the issuer: `symbol=T` answers 20 rows whose symbols are `T`, `T 25`,
`T 25B`, … `T PRA`, `T PRC` — 19 of 20 differ from the requested ticker and they contain spaces.
Anything that treats this as a tradeable ticker is wrong, and the doc comment says so in those words.

`Title` carries entities FMP does not decode: `"AT&amp;T Inc. 5.200% Global Notes due November 18,
2033"`. The SDK **does not decode them either** — decoding would be a silent transformation of the
upstream value, and a caller that wants display text can call `WebUtility.HtmlDecode`. The doc comment
names that call. `&amp;` was the only entity observed.

The dataset is sparse: `JPM`, `BAC`, `VZ`, `GS`, `MS`, `PG` and `JNJ` all answer `[]`. Empty is the
common case.

### `Models/KeyExecutive.cs` — `KeyExecutive`

| property | wire | type | notes |
|---|---|---|---|
| `Title` | `title` | `string?` | |
| `Name` | `name` | `string?` | |
| `Pay` | `pay` | `decimal?` | null on 32 of the first 64 rows |
| `CurrencyPay` | `currencyPay` | `string?` | **`"USD"` and `"TWD"` observed** |
| `Gender` | `gender` | `string?` | `"male"`, `"female"` or null |
| `YearBorn` | `yearBorn` | `int?` | null on 24 of the first 64 rows |
| `TitleSince` | `titleSince` | `string?` | **null on all 203 rows measured** — see below |
| `Active` | `active` | `bool?` | **`true` on all 203 rows measured** |

`TitleSince` and `Active` are kept rather than dropped: they are documented fields that carry nothing
*on this plan on this date*, which is a measurement that can change, not a schema fact. Both doc
comments say exactly that — 203 rows across 18 symbols, all null / all true — so a caller does not
build logic on a constant.

**`TitleSince` is `string?`, not a date type, and that is deliberate.** Not one populated value was
ever observed, so there is no measured shape to infer a format from. Typing it as `Instant?` or
`LocalDate?` would be a guess, and a wrong guess throws at bind time the day FMP starts populating it —
turning a new field into a broken endpoint. A `string?` cannot throw. The doc comment records that the
type is provisional and says to re-measure before narrowing it.

`CurrencyPay` gets a doc comment warning that `Pay` is not comparable across rows without it. `SPY`
answers `[]`.

### `Models/ExecutiveCompensation.cs` — `ExecutiveCompensation` and `ExecutiveCompensationBenchmark`

Two types in one file: they are the two halves of the same question and neither is large.

`ExecutiveCompensation`, 15 fields, all populated on both filers measured:

| property | wire | type |
|---|---|---|
| `Cik` | `cik` | `string?` |
| `Symbol` | `symbol` | `string?` |
| `CompanyName` | `companyName` | `string?` |
| `FilingDate` | `filingDate` | `LocalDate?` |
| `AcceptedDate` | `acceptedDate` | `Instant?` |
| `NameAndPosition` | `nameAndPosition` | `string?` |
| `Year` | `year` | `int?` |
| `Salary` | `salary` | `decimal?` |
| `Bonus` | `bonus` | `decimal?` |
| `StockAward` | `stockAward` | `decimal?` |
| `OptionAward` | `optionAward` | `decimal?` |
| `IncentivePlanCompensation` | `incentivePlanCompensation` | `decimal?` |
| `AllOtherCompensation` | `allOtherCompensation` | `decimal?` |
| `Total` | `total` | `decimal?` |
| `Link` | `link` | `string?` |

`NameAndPosition` runs name and title together in one string — `"Luca Maestri Former Senior Vice
President, Chief Financial Officer"` — with no separator to split on. The doc comment says it is one
opaque string, because the obvious split is wrong.

The type doc records that the endpoint returns the filer's whole history in one call: `AAPL` 339 rows
spanning 1999 → 2025, `JPM` 160.

`ExecutiveCompensationBenchmark`, three fields:

| property | wire | type |
|---|---|---|
| `IndustryTitle` | `industryTitle` | `string?` |
| `Year` | `year` | `int?` |
| `AverageCompensation` | `averageCompensation` | `decimal?` |

`AverageCompensation` is fractional — `609504.1428571428`. The type doc records that **omitting `year`
answers last year, not this year**: bare answered 377 rows stamped 2024 on 2026-08-27, `year=2025`
answered 365. It also records that the first cold call took **37.18 s** against 0.53 s warm, which is
slow enough to trip a default HTTP timeout.

### `Models/MergerAcquisition.cs` — `MergerAcquisition`

| property | wire | type | notes |
|---|---|---|---|
| `Symbol` | `symbol` | `string?` | acquirer |
| `CompanyName` | `companyName` | `string?` | acquirer |
| `Cik` | `cik` | `string?` | acquirer |
| `TargetedCompanyName` | `targetedCompanyName` | `string?` | null on 1 of 1,000 |
| `TargetedCik` | `targetedCik` | `string?` | **null on 390 of 1,000**, and `"0000000000"` is a sentinel |
| `TargetedSymbol` | `targetedSymbol` | `string?` | null on 181 of 1,000 |
| `TransactionDate` | `transactionDate` | `LocalDate?` | |
| `AcceptedDate` | `acceptedDate` | `Instant?` | |
| `Link` | `link` | `string?` | |

`TargetedCik` has **two distinct ways of saying nothing** — `null` and `"0000000000"` — and the doc
comment says so, with both counts. The type doc records that a 10-row sample shows none of the three
nullable fields; the nulls only appear at `limit=1000`.

## Serialization

Seven new `[JsonSerializable]` entries on `FmpJsonContext`:

```csharp
[JsonSerializable(typeof(List<CompanyNote>))]
[JsonSerializable(typeof(List<EmployeeCount>))]
[JsonSerializable(typeof(List<ExecutiveCompensation>))]
[JsonSerializable(typeof(List<ExecutiveCompensationBenchmark>))]
[JsonSerializable(typeof(List<KeyExecutive>))]
[JsonSerializable(typeof(List<MarketCapitalization>))]
[JsonSerializable(typeof(List<MergerAcquisition>))]
[JsonSerializable(typeof(List<StockPeer>))]
```

`List<CompanyProfile>` is already registered and serves `profile-cik` unchanged.

## Testing

Unit tests bind captured fixtures through `StubHandler`, the established pattern. Every trap in the
measurement record gets a test whose name states the trap and which fails if it is reintroduced.

| test | fixture | fails when |
|---|---|---|
| fractional market cap binds | `market-capitalization-batch.20.json` (contains the `GOOG` row) | `MarketCap` is narrowed to `long?` |
| batch drops unknown symbols | `market-capitalization-batch.partial.json` | anything assumes positional alignment |
| `mktCap` maps to `StockPeer.MarketCap` | `stock-peers.AAPL.json` | the wire name is "corrected" to `marketCap` |
| the two employee-count paths hit different URLs | — (`StubHandler` URL assertion) | one method is rewired to the other's path |
| note symbols are not tickers | `company-notes.T.json` | `Symbol` is normalised, trimmed or rewritten to the requested ticker |
| note titles keep their entities | `company-notes.T.json` | the SDK starts HTML-decoding `&amp;` |
| null exchange binds | `company-notes.T.json` | `Exchange` is made non-nullable |
| M&A nullable targets bind | `mergers-acquisitions-latest.p0.json` (1,000 rows) | any of the three target fields is made non-nullable |
| `GetHistoricalMarketCapAsync` omits absent range params | — (`StubHandler` URL assertion) | null `from`/`to` start emitting `from=&to=` |
| backwards range throws | — | `ThrowIfBackwards` is dropped |
| empty batch throws | — | the 400 is passed through instead |
| `GetMarketCapAsync` returns null on `[]` | `market-capitalization.unknown.json` | the empty array throws or returns a default row |
| coverage reaches 114 | — | `EndpointCoverageTests` |

`EndpointCoverageTests` discovers methods by reflection, so the count moves on its own; the 101 → 114
change is an assertion update, not new test code.

Smoke tests are reflection-driven through `Probe`, so all thirteen are swept without new test code —
but `Probe.Argument` throws rather than defaulting when it meets a parameter name it does not know, so
**`name`, `cik`, `symbols` and `year` must be added to it** or the sweep fails loudly. That is by
design and is a required task, not an incidental fix.

## What this does not do

- **No chunking helper for `GetMarketCapBatchAsync`.** No upper bound was found up to 500 symbols, so
  a chunk size would be invented rather than measured.
- **No HTML decoding of note titles.** Silent transformation of an upstream value; the doc names
  `WebUtility.HtmlDecode` instead.
- **No `year` filter emulation on `GetExecutiveCompensationAsync`.** The endpoint returns the whole
  history; filtering it client-side would hide that a caller is holding 339 rows.
- **No backward-walking helper for `historical-market-capitalization`.** The 5,000-row ceiling is
  documented; a paging helper for it is its own decision, not a rider on this slice.
- **`stable/profile` is untouched.** `profile-cik` reuses `CompanyProfile` and adds no type.
