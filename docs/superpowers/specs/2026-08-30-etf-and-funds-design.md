# ETF and Mutual Funds — design

What issue [#34](https://github.com/jerbersoft/fmpdotnet/issues/34) builds: a new `fmp.EtfAndFunds` facade
covering all nine documented ETF And Mutual Funds paths.

Every fact this document argues from was measured on 2026-08-30 and is recorded, with its date, in
[the measurements](2026-08-30-etf-and-funds-measurements.md) (committed `0dbbaef`). Where this document states
a number, that file is where it came from. Nothing here was read from FMP's documentation.

## The shape of the problem

Nine paths, **nine key tuples, no two alike**. There is no consolidation to argue for and none is attempted;
#32's three-shapes-from-eleven-paths and #33's two-from-ten have no analogue here.

The work is elsewhere. This slice **contradicts itself on three fields that share a name across sibling
paths**, and it spells absence four ways:

| trap | measured behaviour | affected |
|---|---|---|
| one name, two types | `weightPercentage` is a number on one path, a `"97.52%"` string on its sibling | 2 paths |
| one name, two formats | `updatedAt` is `uuuu-MM-dd HH:mm:ss` on `holdings`, ISO-8601 `Z` on `info` | 2 paths |
| two timezones | `holdings.updatedAt` is UTC; `disclosure.acceptedDate` is Eastern | 2 paths |
| four spellings of absent | JSON `null`, `""`, `"N/A"`, `"NULL"` — `className` uses two of them | 4 paths |
| no pagination at all | `limit` and `page` ignored everywhere; worst case 66,065 rows / 27.4 MB | all 9 |
| a count that is not a count | `info.holdingsCount` agreed with `holdings` on **1 of 33** ETFs | 1 path |

The design's central claim: **every one of these is a binding decision, not a signature decision.** Unlike #32,
where two of three traps vanished by making a parameter required, nothing here can be fixed by the shape of a
method. The work lands in four new converters and in XML documentation that names what the wire actually does.

Three decisions were the user's and are settled: **one facade**, **converters for the sentinels**, and
**`sectorsList` modelled as its own record rather than reusing `EtfSectorWeighting`**.

## The public surface

One facade, `EtfAndFundsEndpoints`, reached as `fmp.EtfAndFunds`. Nine methods, one per path — the
`MarketPerformanceEndpoints` pattern, not the `TechnicalIndicatorsEndpoints` one, because the paths share no
parameter shape worth parameterising over.

| method | path | returns |
|---|---|---|
| `GetEtfAssetExposureAsync(symbol, ct)` | `stable/etf/asset-exposure` | `IReadOnlyList<EtfAssetExposure>` |
| `GetEtfCountryWeightingsAsync(symbol, ct)` | `stable/etf/country-weightings` | `IReadOnlyList<EtfCountryWeighting>` |
| `GetEtfHoldingsAsync(symbol, ct)` | `stable/etf/holdings` | `IReadOnlyList<EtfHolding>` |
| `GetEtfInfoAsync(symbol, ct)` | `stable/etf/info` | `EtfInfo?` |
| `GetEtfSectorWeightingsAsync(symbol, ct)` | `stable/etf/sector-weightings` | `IReadOnlyList<EtfSectorWeighting>` |
| `GetFundDisclosureAsync(symbol, year, quarter, ct)` | `stable/funds/disclosure` | `IReadOnlyList<FundDisclosure>` |
| `GetFundDisclosureDatesAsync(symbol, ct)` | `stable/funds/disclosure-dates` | `IReadOnlyList<FundDisclosureDate>` |
| `GetFundHoldersAsync(symbol, ct)` | `stable/funds/disclosure-holders-latest` | `IReadOnlyList<FundHolder>` |
| `SearchFundsByNameAsync(name, ct)` | `stable/funds/disclosure-holders-search` | `IReadOnlyList<FundShareClass>` |

### Why one facade

`etf/*` and `funds/*` are different subjects, and splitting them was considered. Against it: FMP documents
them as one section, #34 scopes them as one issue, and the two groups are not independent in practice — an
ETF symbol answers on all eight symbol paths, and `funds/disclosure-holders-latest` answers for ETFs and
ordinary stocks alike. `MarketPerformanceEndpoints` already carries eleven paths across three unrelated call
shapes, so the precedent for one facade over a heterogeneous group exists. Twenty facades after this.

### Why `Etf`/`Fund` prefixes on the method names

`GetHoldingsAsync` and `GetDisclosureAsync` on one facade read as two views of one thing. They are not:
`etf/holdings` is what an ETF owns, `funds/disclosure-holders-latest` is who owns a security. The prefixes
keep the direction of each question in its name.

### `GetEtfInfoAsync` returns one record, not a list

`etf/info` answered a single-element array on all 33 measured responses. `CompanyEndpoints.GetProfileAsync`
already sets the precedent for surfacing that as `Task<T?>` — `GetListAsync`, then `rows.Count > 0 ? rows[0] :
null`. An unknown symbol answers `[]`, which becomes `null`.

### `SearchFundsByNameAsync` is named for what it returns

The wire path is `disclosure-holders-search`, but the rows are not holders: they are SEC-registered **fund
share classes** — `symbol`, `classId`, `seriesId`, `entityName`, `className`, and a filer address. Nothing in
a row says who holds what. The house rule is that the property or method describes the data and the XML doc
carries the wire name, the same trade `MarketMover.ChangePercentage` makes. The doc names the path.

## The models

Ten records for nine paths. Nine row records, plus `EtfInfoSector` for the nested array.

| record | file | keys | notes |
|---|---|---|---|
| `EtfAssetExposure` | `Models/EtfAssetExposure.cs` | 5 | |
| `EtfCountryWeighting` | `Models/EtfCountryWeighting.cs` | 2 | percent-string |
| `EtfHolding` | `Models/EtfHolding.cs` | 9 | UTC `updatedAt`, `""` sentinels |
| `EtfInfo` | `Models/EtfInfo.cs` | 19 | ISO `updatedAt`, nested list |
| `EtfInfoSector` | `Models/EtfInfo.cs` | 2 | nested element, same file as its parent |
| `EtfSectorWeighting` | `Models/EtfSectorWeighting.cs` | 3 | |
| `FundDisclosure` | `Models/FundDisclosure.cs` | 23 | Eastern `acceptedDate`, `Y`/`N`, `"N/A"` |
| `FundDisclosureDate` | `Models/FundDisclosureDate.cs` | 3 | |
| `FundHolder` | `Models/FundHolder.cs` | 7 | |
| `FundShareClass` | `Models/FundShareClass.cs` | 13 | `"NULL"` on six fields |

`EtfInfoSector` lives in `EtfInfo.cs` rather than a file of its own: it is not reachable except through
`EtfInfo.SectorsList`, and a reader who wants to know what the nested list holds should find it without a
second file open.

### `EtfInfoSector` is modelled twice on purpose

Measured, `etf/info.sectorsList` and `etf/sector-weightings` carry **identical data** — same key set, same
values, no rounding difference, on all 13 ETFs cross-checked. They do not carry identical *keys*: the nested
objects say `industry` and `exposure` where the path says `sector` and `weightPercentage`.

Reuse was considered and rejected. Sharing `EtfSectorWeighting` would mean either two `JsonPropertyName`
attributes on one property (which System.Text.Json does not support) or a second converter that renames keys
on the way in. Both cost more than a two-property record, and both make the shared type's XML doc lie about
one of its two wire shapes. So:

- `EtfSectorWeighting` binds `symbol`, `sector`, `weightPercentage`.
- `EtfInfoSector` binds `industry`, `exposure`, and has no symbol — the parent record already names it.

Each record's XML doc points at the other and records that the two were measured equal on 2026-08-30, so a
caller who has one does not need to fetch the other, and a maintainer who sees the duplication finds out why
before deleting it.

Note the nested key is **`industry` carrying sector names** — `Basic Materials`, `Cash & Others`. The property
is `Sector`, the attribute is `[JsonPropertyName("industry")]`, and the doc says do not "fix" it, under the
same rule as `MarketMover.ChangePercentage`.

### Every property is nullable

The house rule: the deserialiser cannot promise a key is present, so every property is nullable regardless of
what the corpus showed. Two fields are nullable because FMP actually sent JSON `null` —
`FundDisclosure.Symbol` (176 of 11,522 rows) and `FundShareClass.Address` (1,540 of 5,869) — and the rest
follow the convention. The XML doc distinguishes the two cases, as `MarketMover.Symbol` does.

### Every number is `decimal`

Following the house rule the statement records state, with `BulkEndOfDayPrice` the single deliberate
exception. Nothing measured argues against it, and two things argue for it:

- Magnitudes reach `7,434,183,997,921.512` and `125,580,304,518.46` with 17 significant digits. `double`
  rounds those; `decimal` holds them.
- The extreme small value, `1.4210854715202004e-14` (SPY's `Cash & Others` weight — 2⁻⁴⁶, a floating-point
  subtraction residue), needs 30 decimal places and `decimal` has 28. Checked on .NET 10 rather than assumed:
  `JsonSerializer.Deserialize<decimal>` **rounds it to 28 places and does not throw**. The loss is ~4e-31 of a
  percentage point on a value that is already numerical noise.

That last point goes in `EtfSectorWeighting`'s XML doc so that nobody "fixes" it later by switching the slice
to `double`, which would round every large figure above far more damagingly.

**No percentage field is range-checked or documented as 0–100.** Measured maxima: `asset-exposure`'s
`weightPercentage` reached **50,506** and its minimum **−199.9869**; `FundHolder.WeightPercent` reached
**264.4**; `FundDisclosure.PctVal` reached **10.88**. `sharesNumber` and `balance` are fractional as well as
negative, so an integer type is wrong for both.

## Four new converters

All four go in `Serialization/NodaConverters.cs` beside the existing thirteen.

### 1. `PercentSuffixedDecimalJsonConverter` : `JsonConverter<decimal?>`

For `EtfCountryWeighting.WeightPercentage` alone. Reads `"97.52%"`, `"0%"`, `"100%"`, `"0.01%"` — 227 of 227
measured rows were strings, with a varying number of decimals. Strips a single trailing `%`, then parses with
`NumberStyles.Float` and `InvariantCulture`; a JSON number passes through unchanged; anything unparseable
becomes `null` rather than throwing, following the file's existing convention that one bad value costs one
field rather than the whole response.

This cannot reuse `TolerantDecimalJsonConverter`: `decimal.TryParse("97.52%", NumberStyles.Float, …)` is
`false`, so that converter would silently null all 227 rows.

### 2. `SentinelStringJsonConverter` : `JsonConverter<string?>`

The largest decision in the slice. Maps the three string spellings of absence — `""`, `"N/A"`, `"NULL"` — to
`null`, and passes everything else through verbatim.

**What this costs, stated plainly.** A caller can no longer tell "FMP sent nothing" from "FMP sent the word
NULL". That is the same trade `TolerantDecimalJsonConverter` already documents, and it is accepted here for a
reason that converter cannot claim: the alternative is asking every caller to know four spellings, on 27.6% of
rows, on a path where the six affected fields are the ones a caller most wants (`city`, `state`, `zipCode`,
`entityOrgType`, `reportingFileNumber`, `symbol`). A caller who writes `row.State ?? "unknown"` against
`FundShareClass` without this converter gets the string `"NULL"` on a quarter of the rows and no warning.

Applied to exactly the fields measured to carry a sentinel, and no others:

| record | properties |
|---|---|
| `EtfHolding` | `Asset`, `Isin`, `SecurityCusip` |
| `FundDisclosure` | `Name`, `Lei`, `Cusip`, `Isin`, `PayoffProfile`, `InvCountry` |
| `FundHolder` | `Holder`, `SecurityCusip` |
| `FundShareClass` | `Symbol`, `EntityOrgType`, `ReportingFileNumber`, `ClassName`, `City`, `ZipCode`, `State`,
  `Address` |

`Address` is on that list even though its measured absence was a real JSON `null`: eight rows sent `""` for it
in the same corpus, so the field carries both spellings and the converter normalises them.

The converter is **not** applied to `EtfHolding.Name`, which was populated on all 35,185 rows — an empty name
would be information, not absence — nor to `Title`, `Units`, `AssetCat`, `IssuerCat`, `Cik`, `ClassId`,
`SeriesId`, `EntityName` or `SeriesName`, none of which was ever a sentinel.

### 3. `YesNoBooleanJsonConverter` : `JsonConverter<bool?>`

For `FundDisclosure`'s four `is*` fields, which are `Y`/`N` strings and not JSON booleans. `"Y"` → `true`,
`"N"` → `false`, anything else — including `null`, `""` and `"N/A"` — → `null`.

Two of the four (`isRestrictedSec`, `isNonCashCollateral`) were `N` on all 3,861 sampled rows, so their `Y`
form is unmeasured. That is why the converter is written as a total function over a measured domain rather
than as a two-case parse: it never has to be right about a value it has not seen, and an unexpected third
value nulls one field instead of throwing away the row. Both records' XML docs say which two were never
observed as `Y`.

### 4. `NullableIsoInstantJsonConverter` : `JsonConverter<Instant?>`

For `EtfInfo.UpdatedAt`, which is `uuuu-MM-dd'T'HH:mm:ss.fff'Z'` — 33 of 33 measured. This form carries its
own offset and needs no zone measurement: it is UTC because it says so. Uses NodaTime's
`InstantPattern.ExtendedIso`, which reads the fractional seconds and the `Z`. Null on an unparseable value,
like the rest of the file.

No existing converter reads this shape. `NullableFmpInstantJsonConverter` expects a space separator and no
`Z`, and would null the field on every row.

## The two timestamps, and which converter each takes

Two paths in this slice send `uuuu-MM-dd HH:mm:ss`, the shape the SDK already reads two different ways. They
take different converters, and each reading was measured rather than inherited.

### `EtfHolding.UpdatedAt` → `NullableFmpInstantJsonConverter` (UTC)

Measured by falsification, with the evidence inside the same HTTP response. `etf/holdings?symbol=SCHD`
returned `updatedAt = 2026-08-30 06:51:13` in a response whose own `Date` header read
`Sun, 30 Aug 2026 10:05:35 GMT`. Read as Eastern, `06:51:13` EDT is `10:51:13Z` — **46 minutes after FMP
generated the response that carried it.** A cache stamp cannot postdate its own response. Read as UTC it is
3h14m old, which is ordinary. Reproduced 18 seconds later against a fresh response.

The value is a **per-symbol cache stamp, constant across every row** (33 of 33 responses had exactly one
distinct value), and staleness ranged from 3.2 hours to 284 hours on one sweep. The XML doc says so: it is not
an as-of date for the holdings and must not be used as one.

### `FundDisclosure.AcceptedDate` → `NullableEasternInstantJsonConverter` (Eastern)

Measured by identity against a field whose zone the SDK already established. Twenty NPORT-P filings across two
CIKs and ten quarters were looked up a second time through `stable/sec-filings-search/cik`, whose
`acceptedDate` was measured Eastern against EDGAR on 2026-08-26.

**12 of 19 matched to the second** (10 of 10 for the SPY trust); the rest are Vanguard's same-day sibling
filings, one per series, minutes apart. **Largest residual across all 19: 90 seconds.** One hour is 3,600
seconds and four hours are 14,400 — nothing in that distribution is a timezone offset. Same instant, same
encoding, so the established reading transfers.

SEC EDGAR's own `data.sec.gov` submissions API answered **HTTP 403** ("Undeclared Automated Tool"). That is an
access control and was not worked around; the cross-path identity is a stronger result anyway, because it
compares FMP against FMP.

## Numeric-string fields stay strings

`FundShareClass.EntityOrgType` (`"30"` ×3,635, `"32"` ×17, `"33"` ×5) and `FundDisclosure.FairValLevel`
(`"1"` ×3,829, `"2"` ×28, `"3"` ×4) are quoted integers on the wire, both with a non-numeric sentinel in the
same field. They stay `string?`, through `SentinelStringJsonConverter`.

They are **codes, not quantities**: an SEC entity organisation type and an ASC 820 fair-value level. Nothing a
caller does with either is arithmetic, and parsing them to `int?` would invent a numeric identity the source
does not have while gaining nothing. The XML doc lists the measured values on each.

## Guards

| method | guard | why |
|---|---|---|
| all eight symbol methods | `ArgumentException.ThrowIfNullOrWhiteSpace(symbol)` | `symbol=` is a 400 from FMP |
| all eight symbol methods | reject a `,` in `symbol` | see below |
| `SearchFundsByNameAsync` | `ArgumentException.ThrowIfNullOrWhiteSpace(name)` | `name` bare is a 400 |
| `GetFundDisclosureAsync` | `quarter` in 1–4 | see below |

### Rejecting a comma in `symbol`

Measured: `symbol=SPY,QQQ` returns **`[]` with HTTP 200** on `etf/info` and `etf/sector-weightings`. The
plural `symbols=` is a 400. So the comma-joined form used by `QuoteEndpoints.Batch` is not merely unsupported
here — it is a silent wrong answer, indistinguishable from "this ETF has no data".

This is the one place in the slice where a signature can prevent a silent wrong answer, and it is the same
argument `MarketPerformanceEndpoints` makes for requiring `exchange`. The guard throws `ArgumentException`
naming the parameter and saying that these paths take one symbol.

It is a narrow guard on purpose: it rejects the comma, not "anything that is not a known ETF". An unknown
symbol legitimately answers `[]`, and so does a stock — measured, `symbol=AAPL` returned `[]` on all four
ETF-only paths. Those are honest empties and are documented as such, not guarded.

### Guarding `quarter` but not `year`

Measured: `quarter=0`, `quarter=5`, `year=1990` and `year=2030` all return **HTTP 200 with `[]`**;
`quarter=Q1` and `year=abc` are 400s. So a caller who sends `quarter=0` is told "no holdings", not "bad
request".

`quarter` is guarded to 1–4, following `StatementEndpoints`, whose doc gives the reason: a caller who asks a
question FMP will answer wrongly should hear about it from the compiler's runtime guard rather than from an
empty list. Four quarters is not a measurement — it is what a quarter is.

`year` is **not** guarded. A lower bound would have to come from somewhere, and the only candidates are
measured coverage extents (2019-09-30 for SPY, 2019-11-30 for FXAIX, 2020-04-30 for ARKK) which differ per
fund and will move. Encoding one of them would be inventing a fact. The XML doc records the measured extents
and says an out-of-coverage year answers empty.

## What is documented rather than guarded

### There is no pagination, and responses get large

`limit` and `page` are ignored on all nine paths — verified by byte-identical responses with and without them,
including a 17,252-row, 4.9 MB `etf/holdings?symbol=BND`. Unknown parameters are ignored the same way.

So there are **no walk helpers in this facade, no page ceilings, and no `MaxPage`/`MaxPageSize` constants.**
That is a real absence and the docs must say so, because three of the SDK's nineteen facades do have walk
helpers and a caller may reasonably look for one.

The consequence goes on the two methods that can produce it:

- `GetEtfHoldingsAsync` — 17,252 rows / 4.9 MB measured for `BND`, 8,821 / 2.5 MB for `VXUS`.
- `SearchFundsByNameAsync` — **66,065 rows / 27.4 MB** for `name=Trust`, which is also the single most
  natural thing a caller might type.

There is no way to ask for less, so the XML doc is the only place a caller finds out before the allocation
happens.

### `EtfInfo.HoldingsCount` is not the number of holdings

Cross-checked on 33 ETFs against the row count `etf/holdings` returned for the same symbol on the same day,
**they agreed on 1**. BND reports 346 and returns 17,252. ARKK reports 10 and returns 47. GLD and SLV report 0
and return 1. Most gaps are small — the two paths refresh from different snapshots — but the field cannot be
used to pre-size a buffer, to page (there is none), or to decide whether calling `GetEtfHoldingsAsync` is
worthwhile. Its XML doc says exactly that, with the BND and ARKK numbers.

### Ordering is reported, not promised

Measured per path, and the two weightings paths — which look like a matched pair — sort differently:

| path | measured ordering |
|---|---|
| `etf/holdings` | `weightPercentage` descending (held over 17,252 rows) |
| `etf/country-weightings` | `weightPercentage` descending |
| `etf/sector-weightings` | **alphabetical by sector**, not by weight |
| `funds/disclosure-dates` | `date` descending |
| `etf/asset-exposure`, `funds/disclosure`, `funds/disclosure-holders-latest` | no order found |

Each `<returns>` says "in FMP's own order" and records what that order was on 2026-08-30, the way
`GetBiggestGainersAsync` does. Nothing is re-sorted client-side.

### `GetFundHoldersAsync` is per-holder latest, not an as-of snapshot

One response mixes reporting dates — SPY's 220 rows carried four (`2026-06-30` ×124, `2026-04-30` ×44,
`2026-05-31` ×30, `2026-03-31` ×2). So rows in one response are **not comparable as of one date**, and
`DateReported` must be read per row.

`SecurityCusip` is also not constant per response: AAPL's mixes the common stock `037833100` with the bonds
`037833EF3` and `037833DZ0`. The path answers "funds holding any security of this issuer".

### `name` matching is whole-word, single-word, case-insensitive

Measured: `Vanguard` / `vanguard` / `VANGUARD` all returned the same 548 rows; `Vangua` returned **0**; `van`
returned 201 (`VAN KAMPEN…`); `Fid` and `fidelit` returned 0; `Vanguard Group` returned **0**.

A prefix does not match and a two-word company name does not match — which is the most likely thing a caller
will type. The `<param>` doc says so and shows a working query. The exact tokenisation was not established and
the SDK does not assert one.

### `cur_cd` sends `USDUSD`

29 of 3,861 rows — all equity-futures lines (`units: NC`, `assetCat: DE`, `payoffProfile: N/A`). A doubled
currency code, recorded on `FundDisclosure.CurrencyCode` so that a strict three-letter currency type is not
chosen by mistake later. The property is `CurrencyCode`; the attribute is `[JsonPropertyName("cur_cd")]` —
this slice's only snake_case wire key, sitting between two camelCase ones.

### Fiscal quarters, not calendar quarters

`funds/disclosure-dates` returns fund fiscal period-ends — FXAIX reports on `2026-05-31`, ARKK on
`2026-04-30` — while `year` and `quarter` count **calendar** quarters, so FXAIX's May date reads as Q2.
Verified over 80 rows across three funds: `year = date.Year` and `quarter = (date.Month - 1) / 3 + 1` with
**0 mismatches**. The record's XML doc gives that relation, since it is what makes the two fields usable as
arguments to `GetFundDisclosureAsync`.

## Serialisation and wiring

**`FmpJsonContext`** — nine new `[JsonSerializable(typeof(List<T>))]` entries. `EtfInfoSector`'s metadata is
generated as part of `List<EtfInfo>` and needs no entry of its own.

**Five edits to add the facade**, the count this repo has paid for before:

1. `FmpClient` constructor parameter — `EtfAndFundsEndpoints etfAndFunds`
2. `FmpClient` property — `public EtfAndFundsEndpoints EtfAndFunds { get; } = etfAndFunds;`
3. `FmpServiceCollectionExtensions` — `services.TryAddTransient<EtfAndFundsEndpoints>();`
4. `AddFmpTests` — the hard-coded property count, **19 → 20**
5. `AddFmpTests` — `Assert.NotNull(client.EtfAndFunds);`

**The live sweep needs teaching, and this is not optional.** `Probe.Argument` supplies `symbol` as
`LiveApi.Symbol`, which is `AAPL` — and measured, `AAPL` returns `[]` on all four ETF-only paths and on
`funds/disclosure-dates`. Without a new arm the sweep would record `outcome empty` as the baseline for five of
nine endpoints and agree with itself green forever. That is precisely the failure `Probe.Argument`'s own
comments describe for `exchange` and for the 13F CIKs.

- New `LiveApi.EtfSymbol = "QQQ"`. Chosen by measurement: of the ETFs probed it is the smallest that answers
  **non-empty on all eight symbol paths** — 30 / 8 / 107 / 1 / 11 / 28 / 87 rows, and 101 rows for
  `funds/disclosure` at `SettledYear`/`SettledQuarter` (2025 Q3). Roughly 124 KB across the eight, against
  SPY's ~500 KB.
- New `LiveApi.FundNameQuery = "Schwab"` — 211 rows / 90 KB, measured. Its own constant rather than reusing
  `CompanyNameQuery`, for the reason the insider and congressional `name` arms each have one: a change to
  another probe must not silently move this one.
- `Probe.Argument` gains `"symbol" when DeclaringType == typeof(EtfAndFundsEndpoints) => LiveApi.EtfSymbol`
  and `"name" when DeclaringType == typeof(EtfAndFundsEndpoints) => LiveApi.FundNameQuery`, dispatched on the
  declaring type like the existing `cik` and `name` arms.
- `year` and `quarter` already resolve to `SettledYear`/`SettledQuarter` by name and need no new arm.

**README coverage table** — regenerated by `EndpointCoverageTests`, which drives the code rather than reading
it, so the nine paths appear once the methods exist. `DocumentedPaths` stays 243.

**Smoke baseline** — `baseline-ordinary.txt` gains nine rows, recorded on a live run.

## Testing

Unit tests in `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`, against fixtures captured from the 2026-08-30
sweep. Every trap named above gets a test that **fails when the trap is reintroduced** — that is the house
rule, and it is what most of this list is.

**Fixtures** (`tests/FmpDotNet.Tests/Fixtures/`), heads of real captures:

`etf-asset-exposure.SPY.head.json`, `etf-country-weightings.SPY.json`, `etf-holdings.SPY.head.json`,
`etf-holdings.BND.sentinels.json`, `etf-info.SPY.json`, `etf-sector-weightings.SPY.json`,
`funds-disclosure.SPY.2026q1.head.json`, `funds-disclosure.dst-pair.json`, `funds-disclosure-dates.SPY.json`,
`funds-disclosure-holders-latest.SPY.head.json`, `funds-disclosure-holders-search.nulls.json`.

| test | pins |
|---|---|
| nine `binds_all_its_fields` tests | every key of every shape, with `Binding.Unbound` empty |
| `Country_weight_parses_the_percent_suffix` | `"97.52%"` → `97.52m`; `"0%"` → `0m`; `"100%"` → `100m` |
| `Country_weight_is_null_when_unparseable` | garbage → `null`, not a throw, and the other field survives |
| `Sector_weight_is_a_number_not_a_string` | the sibling path binds `1.62` with no converter |
| `Holdings_updated_at_reads_as_utc` | `2026-08-30 06:51:13` → `2026-08-30T06:51:13Z`, **not** `10:51:13Z` |
| `Info_updated_at_reads_the_iso_form` | `2026-08-29T23:12:50.006Z` → that instant, ms preserved |
| `Accepted_date_reads_as_eastern_across_dst` | `2026-05-28 15:11:03` → `19:11:03Z` (EDT) and
  `2026-02-26 16:49:39` → `21:49:39Z` (EST) |
| `Sentinel_strings_become_null` | `""`, `"N/A"`, `"NULL"` → `null` on one property of each of the four records |
| `A_real_value_survives_the_sentinel_converter` | `"PA"`, `"Investor B"` pass through |
| `The_null_row_binds_every_other_field` | the BlackRock `"NULL"` row keeps `cik`, `classId`, `seriesId`, `entityName` |
| `Entity_org_type_stays_a_string` | `"30"` binds as `"30"`; `"NULL"` binds as `null` |
| `Yes_and_no_become_true_and_false` | `"Y"` → `true`, `"N"` → `false`, `"X"` → `null` |
| `Nested_sector_binds_industry_not_sector` | `{"industry":…,"exposure":…}` binds; **fails if the attribute is
  "fixed"** |
| `Nested_sectors_equal_the_sector_weightings_path` | the two fixtures agree value for value |
| `Negative_and_exponent_values_survive` | `-2920694176`, `-199.9869`, `-2.4437904357910156e-05`, `50506` |
| `The_thirty_place_weight_rounds_and_does_not_throw` | `1.4210854715202004e-14` binds; pins the `decimal` choice |
| `Currency_code_can_be_usdusd` | `"USDUSD"` binds verbatim |
| `A_comma_in_symbol_is_rejected` | `ArgumentException` from all eight symbol methods |
| `A_blank_symbol_is_rejected` | `ArgumentException` from all eight |
| `A_blank_name_is_rejected` | `ArgumentException` |
| `Quarter_outside_one_to_four_is_rejected` | `0` and `5` → `ArgumentOutOfRangeException` |
| `Every_path_is_requested_correctly` | nine stubbed calls; asserts path and query, one row per path |

`funds-disclosure.dst-pair.json` is two rows hand-assembled from measured captures — SPY's 2026 Q1 filing
(accepted in EDT) and its 2025 Q4 filing (accepted in EST) — so the Eastern reading is pinned on **both**
offsets. A fixed −5 or −4 fails one of the two, which is the whole point.

## Documentation deliverables

1. **Nine `<summary>` blocks** on the facade methods, each carrying the measured behaviour of its path:
   parameter rules, empty-not-error, ordering, and the size warnings on the two big ones.
2. **The facade's own `<summary>`** stating the three facts that hold across all nine — no pagination
   anywhere, unknown input answers `[]` at HTTP 200, one symbol per call — so a caller reads them once.
3. **Ten record docs**, each naming its wire path, its measured row and null counts with the date, and the
   sentinel spellings it carries.
4. **Four converter docs** in the style of the existing thirteen: what was measured, when, and what the
   converter refuses to guess. `NullableFmpInstantJsonConverter`'s and
   `NullableEasternInstantJsonConverter`'s docs each gain a line naming their new caller, so the list of what
   reads each zone stays in one place.
5. **`EtfInfo.HoldingsCount`** — the 1-of-33 measurement, with BND and ARKK named.
6. **`EtfInfo.SectorsList` and `EtfSectorWeighting`** — each pointing at the other, and recording that they
   were measured identical on 13 ETFs on 2026-08-30.
7. **README** — regenerated coverage table.

## Files

**Created (22):**

```
src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs
src/FmpDotNet/Models/EtfAssetExposure.cs
src/FmpDotNet/Models/EtfCountryWeighting.cs
src/FmpDotNet/Models/EtfHolding.cs
src/FmpDotNet/Models/EtfInfo.cs              (EtfInfo + EtfInfoSector)
src/FmpDotNet/Models/EtfSectorWeighting.cs
src/FmpDotNet/Models/FundDisclosure.cs
src/FmpDotNet/Models/FundDisclosureDate.cs
src/FmpDotNet/Models/FundHolder.cs
src/FmpDotNet/Models/FundShareClass.cs
tests/FmpDotNet.Tests/EtfAndFundsTests.cs
tests/FmpDotNet.Tests/Fixtures/  (11 files)
```

**Modified (9):**

```
src/FmpDotNet/Serialization/NodaConverters.cs                    four converters, two doc additions
src/FmpDotNet/Serialization/FmpJsonContext.cs                    nine entries
src/FmpDotNet/FmpClient.cs                                       ctor parameter + property
src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs   one registration
tests/FmpDotNet.Tests/AddFmpTests.cs                             19 -> 20, one assertion
tests/FmpDotNet.SmokeTests/LiveApi.cs                            EtfSymbol, FundNameQuery
tests/FmpDotNet.SmokeTests/Probe.cs                              two Argument arms
README.md                                                        regenerated table
tests/FmpDotNet.SmokeTests/baseline-ordinary.txt                 nine rows, from a live run
```

## What this design does not do

- **No client-side sorting.** FMP's order is reported, never imposed.
- **No range checks on percentages.** Three of them exceed 100 in measured data.
- **No `year` bound.** Coverage differs per fund and would be a fabricated fact.
- **No streaming or paging for the large responses.** FMP offers no mechanism; inventing one client-side would
  mean fetching everything anyway.
- **No enum for `assetClass`.** Already inconsistent at n=33 — `Equity`, `Large Cap Equity` and
  `International Equity` are not one vocabulary.
- **No enum for `assetCat`, `issuerCat`, `units` or `payoffProfile`.** These are SEC N-PORT code lists; the
  corpus saw 5, 5, 3 and 2 values respectively, which is a sample and not a vocabulary. The measured values go
  in the XML docs as observations.
