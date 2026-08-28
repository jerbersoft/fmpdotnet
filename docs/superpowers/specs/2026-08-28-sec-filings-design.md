# SEC Filings — design

Closing [#30](https://github.com/jerbersoft/fmpdotnet/issues/30): the twelve documented `stable/` paths FMP files
under SEC Filings. Every fact below is drawn from
[the measurements](2026-08-28-sec-filings-measurements.md), measured 2026-08-28.

**Coverage:** 114 → **126 of 243** documented paths.

## Scope, and one deliberate redistribution

FMP files all twelve under SEC Filings, but three of them are not filings. `all-industry-classification` and
`standard-industrial-classification-list` are reference lists, and `industry-classification-search` is a lookup
over the first of those. They are shipped to the facades whose job they already are.

That follows existing practice rather than departing from it: `commodities-list`, `forex-list` and `index-list`
all live in `fmp.Directory` today even though FMP documents them under Commodity, Forex and Indexes. This SDK
files paths by what they return, not by which documentation page names them.

| facade | paths | note |
|---|---|---|
| `fmp.SecFilings` | 9 | new |
| `fmp.Directory` | +2 | joins `available-sectors` and `available-industries` |
| `fmp.Search` | +1 | joins the six existing search methods |

## Public surface

### `fmp.SecFilings` — new, 9 of 9 paths

```csharp
Task<IReadOnlyList<SecProfile>>  GetProfileAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<SecProfile>>  GetProfileByCikAsync(string cik, CancellationToken ct = default);

Task<IReadOnlyList<SecFiling>>   Get8KFilingsAsync(
    LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<SecFiling>>   GetFilingsWithFinancialsAsync(
    LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100, CancellationToken ct = default);

Task<IReadOnlyList<SecFiling>>   SearchBySymbolAsync(
    string symbol, LocalDate from, LocalDate to, int page = 0, int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<SecFiling>>   SearchByCikAsync(
    string cik, LocalDate from, LocalDate to, int page = 0, int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<SecFiling>>   SearchByFormTypeAsync(
    string formType, LocalDate from, LocalDate to, int page = 0, int limit = 100, CancellationToken ct = default);

Task<IReadOnlyList<IndustryClassification>> FindCompanyBySymbolAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<IndustryClassification>> FindCompanyByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<IndustryClassification>> FindCompanyByNameAsync(string company, CancellationToken ct = default);
```

`from` and `to` are **required, non-nullable** on the three search methods. FMP returns 400 without either one,
so an optional parameter would ship a signature whose default can only fail. They stay optional on the two feed
methods, which answer without them.

`MaxSecFilingPageSize = 1000` is exposed as a public constant, matching `MaxDelistedPageSize` and
`MaxMergerAcquisitionPageSize`. A `limit` above it is clamped by FMP silently; the constant is how a caller
learns the ceiling without discovering it.

The three `FindCompany*` methods take no `limit`: `company=Apple` returned 52 rows with and without one. A
parameter the endpoint ignores does not appear in the signature.

### `fmp.Directory` — two additions

```csharp
Task<IReadOnlyList<IndustryClassification>> GetIndustryClassificationsAsync(
    int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<IndustryClassification>> GetAllIndustryClassificationsAsync(CancellationToken ct = default);
Task<IReadOnlyList<SicCodeEntry>>           GetSicCodesAsync(CancellationToken ct = default);
```

There is no `page` parameter, because the endpoint has no working pagination. Page 0 caps at 1,000 rows
regardless of `limit`; every non-zero page returns the whole 25,952-row universe, byte-identical and ignoring
`limit`. Exposing `page` would be exposing a control that does not control anything.

The two methods model the two behaviours the endpoint actually has. `GetAllIndustryClassificationsAsync` sends
`page=1` and is documented as depending on that anomaly — it is the only route to rows 1,001 onward, so the
choice is between depending on it and leaving 96% of the dataset unreachable.

`MaxIndustryClassificationPageSize = 1000` is exposed alongside it. A `limit` above the cap is not an error —
FMP answers 1,000 rows and says nothing — so the constant is how a caller learns the ceiling rather than
inferring it from a short response.

`GetSicCodesAsync` takes no parameters. The endpoint returned all 444 rows for every combination of `page` and
`limit` tried.

### `fmp.Search` — one addition

```csharp
Task<IReadOnlyList<IndustryClassification>> FindIndustryClassificationAsync(
    string? symbol = null, string? cik = null, string? sicCode = null, CancellationToken ct = default);
```

Throws `ArgumentException` when all three are null, mirroring FMP's own "Please enter at least one search value:
cik, sicCode, or symbol." The SDK raises it before spending a call.

## Models

Four new records. All properties are nullable reference or value types, per the house rule that an absent field
must not cost the caller the response.

### `IndustryClassification` — shared by five paths across three facades

| property | wire | type |
|---|---|---|
| `Symbol` | `symbol` | `string?` |
| `Name` | `name` | `string?` |
| `Cik` | `cik` | `string?` |
| `SicCode` | `sicCode` | `string?` |
| `IndustryTitle` | `industryTitle` | `string?` |
| `BusinessAddress` | `businessAddress` | `string?`, normalised — see below |
| `PhoneNumber` | `phoneNumber` | `string?` |

One record rather than one per facade, because the five paths return the same data and not merely the same field
names: for CIK `0000070858` all six non-address fields were byte-identical across two paths.

`Cik` and `SicCode` stay `string?`. Both are zero-padded fixed-width identifiers on the wire — `"0000320193"`,
`"0100"` — and an integer type would destroy the padding that makes them match EDGAR.

### `SecFiling` — shared by five paths

| property | wire | type |
|---|---|---|
| `Symbol` | `symbol` | `string?` |
| `Cik` | `cik` | `string?` |
| `FilingDate` | `filingDate` | `LocalDate?` via `NullableDateAtMidnightJsonConverter` |
| `AcceptedDate` | `acceptedDate` | `Instant?` via `NullableEasternInstantJsonConverter` |
| `FormType` | `formType` | `string?` |
| `HasFinancials` | `hasFinancials` | `bool?` |
| `Link` | `link` | `string?` |
| `FinalLink` | `finalLink` | `string?` |

`HasFinancials` is null on the three `sec-filings-search/*` paths, which do not send the field. One record with a
nullable is the right modelling for a one-field difference across an otherwise identical seven; a second record
would duplicate seven properties to express one absence.

`FormType` is a raw string, not an enum, for the reason `EconomicRelease.Impact` gives: a form type the SDK has
never seen must not cost the caller the response. `sec-filings-financials` alone returned three distinct values.

### `SecProfile` — 35 fields, its own record

Not a reuse of `CompanyProfile`. That models `stable/profile`, which is market data — it carries `price` and
`marketCap`. This is EDGAR registrant data and carries `taxIdentificationNumber`, `secFilingsUrl` and
`stateOfIncorporation`. Different sources, different field sets.

Typed from measurement rather than from the field names:

- `Employees` → `int?`. The wire sends `"166000"` as a string; `AllowReadingFromString` is already global.
- `IpoDate` → `LocalDate?` via the existing `NullableLocalDateJsonConverter`. The wire sends plain ISO.
- `FiscalYearEnd` → **`string?`**. The wire sends `"09-30"` — a month and day with no year, which no date type
  can hold without inventing one.
- `FiftyTwoWeekRange` → **`string?`**. The wire sends `"225.95 - 344.57"`, one formatted string rather than two
  numbers. Splitting it would be the SDK asserting a format FMP has not promised.
- `IsActive`, `IsEtf`, `IsAdr`, `IsFund` → `bool?`. These four are real JSON booleans; every other value is a
  string.
- `SecurityType` → `string?`, modelled although it was null on all six symbols sampled. An always-null field is
  recorded and flagged, never dropped — dropping it would make its later arrival invisible.

The remaining 26 are `string?` and map one-to-one.

### `SicCodeEntry` — 3 fields

`Office`, `SicCode`, `IndustryTitle`, from `office`, `sicCode`, `industryTitle`. Named for the existing
`CikEntry` precedent. The endpoint returns a fixed 444 rows.

## Two new converters

### `NullableDateAtMidnightJsonConverter`

Reads `uuuu-MM-dd HH:mm:ss` and yields `LocalDate?`, discarding the time. Justified by measurement: across 2,115
rows from three paths, the time component of `filingDate` was `00:00:00` in 2,115 of 2,115 cases. The existing
`NullableLocalDateJsonConverter` cannot read this — it uses `LocalDatePattern.Iso`, which rejects the trailing
time — and `NullableLocalDateTimeJsonConverter` would bind it but leak a meaningless midnight into every value.

### `BusinessAddressJsonConverter`

Normalises the bracketed encoding to the comma-joined one, so `IndustryClassification.BusinessAddress` means the
same thing on all five paths. The target is not the SDK's invention: `", ".join(parts)` reproduced the sibling
path's string exactly on 5 of 5 randomly sampled CIKs.

The transform is **textual, not a parse** — strip a leading `['` and trailing `']`, then replace `', '` with
`, `. This is deliberate. One of 1,000 sampled values is
`"['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"`, where `XI'AN` carries an unescaped
apostrophe inside a single-quoted repr. A literal parse fails on that row; the textual route handles it, because
the apostrophe is not followed by `', '`. Any apostrophe in an address reproduces the fault, so this is a class
of row rather than one bad row.

Input that does not match the bracketed shape is returned verbatim. The converter never throws and never drops a
value: a string it does not recognise is passed through unchanged, which is also what makes it safe on the four
paths that never send the bracketed form.

The normalisation runs in one direction only. Splitting a joined address back into parts would be lossy —
nineteen of the 1,000 sampled values contain a comma inside an element.

## Serialisation

Four `[JsonSerializable]` entries — `List<IndustryClassification>`, `List<SecFiling>`, `List<SecProfile>`,
`List<SicCodeEntry>` — added to `FmpJsonContext` in alphabetical order within the appropriate cluster, never
inside the bulk-CSV comment block. A missing registration fails at runtime, not at compile time.

## Error surface

`FindIndustryClassificationAsync` throws `ArgumentException` when given no search value. The three search methods
take `from` and `to` as required parameters, so the compiler enforces what FMP would otherwise answer 400 for.

A backwards range — `to` earlier than `from` — throws `ArgumentOutOfRangeException` before the call is spent, on
all five methods that accept a range. `CompanyEndpoints` already carries a private `ThrowIfBackwards` for this;
`SecFilingsEndpoints` gets its own copy rather than the two sharing an extracted helper. That matches the ruling
made in #29 for the duplicated `Batch` helper: two call sites is where extraction is still premature. A third
occurrence is the point to promote it, and that is recorded here as the trigger rather than left to be
rediscovered.

Everything else follows the transport's existing behaviour; no new exception types.

## Testing

Roughly 45–55 unit tests. Twelve fixtures capture one response per path at 5 rows each, following the ruling
from #29 that a fixture records a shape rather than a page — a 370 KB capture is not worth 5 rows of evidence.
Three further fixtures exist to pin traps rather than paths: the `XI'AN` address row, a filing row whose
`FilingDate` falls past the requested `to`, and a search-path row with `hasFinancials` absent. Fifteen in total,
and each of the three trap fixtures is named for the trap rather than for its endpoint.

Each measured trap gets a test that fails if the trap is reintroduced:

- **Address normalisation**: the Bank of America bracketed value → the joined form; the `XI'AN` row → binds
  without throwing; a plain string → returned untouched; a null → stays null.
- **`to` filters `acceptedDate`**: a fixture carrying a row whose `FilingDate` is later than the requested `to`,
  asserted to bind. The behaviour lives in a test, not only in prose.
- **`filingDate` midnight**: `"2025-03-06 00:00:00"` → `LocalDate` 2025-03-06, with no time surviving.
- **`acceptedDate` Eastern**: a known wall-clock value → the correct `Instant`, guarding against the UTC
  converter being substituted, which would shift every value silently.
- **`HasFinancials` absent**: a search-path fixture with no `hasFinancials` field → binds null rather than false.
- **`SecProfile` string-typed numerics**: `"166000"` → `Employees` 166000.
- **`FiscalYearEnd` and `FiftyTwoWeekRange`**: bind as sent, unparsed.

`Binding.Unbound<T>` runs over each new record, as it does for every model in the SDK.

### Live guard

`SweepCoverageTests` gains an assertion that `GetAllIndustryClassificationsAsync` returns **more than 1,000
rows**. That is the tripwire for FMP fixing the `page=1` anomaly: the shape would not change, so the existing
baseline would not notice, and callers would silently drop from 25,952 rows to 5. The row-count assertion is the
only thing that can catch it.

The sweep also gains the twelve new paths. `Probe.Argument` needs new arms for `company`, `formType` and
`sicCode`; `LiveApi` gains the matching constants.

## Documentation

The README coverage block is regenerated by `EndpointCoverageTests` with `FMPDOTNET_UPDATE_README=1`, taking the
headline to 126 of 243. Every XML doc comment that states a fact states the date it was measured.

## Out of scope

- **Parsing `businessAddress` into parts.** The joined form is lossy to split, and the bracketed form is not
  reliably parseable. A structured address type would have to guess on both.
- **An enum for `formType`.** Three values were observed on one path; EDGAR has hundreds.
- **Deriving `fiscalYearEnd` into a date.** It has no year.
- **Any `*-bulk` path.** None of the twelve has one.
