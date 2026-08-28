# Analyst and Calendar — design

Closes [#37](https://github.com/jerbersoft/fmpdotnet/issues/37): the fourteen documented `stable/` paths FMP
files under Analyst and Calendar. Takes SDK coverage from **126 of 243 paths to 140**.

Every type here is built from the [2026-08-28 measurement pass](2026-08-28-analyst-and-calendar-measurements.md)
rather than from FMP's documentation. Where the two disagree, the measurement wins and the disagreement is
documented.

## Architecture

**No new facade.** Seven paths join `fmp.Analyst` (1 → 8 methods) and seven join `fmp.Calendar` (2 → 9). Both
files are small today — 116 and 164 lines — and neither approaches the size at which `CompanyEndpoints` (17
methods) still reads well, so neither is split.

Every path is an ordinary `GET` returning a JSON array that `FmpTransport.GetListAsync` already serves. No new
transport primitive, no streaming, no CSV.

**Eleven new records, one result type, one converter, eleven `FmpJsonContext` registrations.**

### Which facade takes what

| `fmp.Analyst` | `fmp.Calendar` |
|---|---|
| `grades` | `dividends` |
| `grades-consensus` | `dividends-calendar` |
| `grades-historical` | `splits` |
| `price-target-consensus` | `splits-calendar` |
| `price-target-summary` | `ipos-calendar` |
| `ratings-snapshot` | `ipos-disclosure` |
| `ratings-historical` | `ipos-prospectus` |

The rule is the one the SDK already files by: what the path *returns*. Analyst rows are opinions about a
company; Calendar rows are dated corporate events.

## Records

### Three records serve six paths

Measured byte-identical field sets, so one record each. This follows `EmployeeCount` (two paths, #29) and
`SecFiling` (five paths, #30).

**`Dividend`** — `dividends` and `dividends-calendar`. Nine fields: `Symbol`, `Date`, `RecordDate`,
`PaymentDate`, `DeclarationDate`, `AdjDividend`, `DividendAmount`, `Yield`, `Frequency`.

The wire name `dividend` collides with the record name, so the property is `DividendAmount` with an explicit
`[JsonPropertyName("dividend")]`. All four date fields are `LocalDate?`: `recordDate`, `paymentDate` and
`declarationDate` arrive **blank rather than null** when absent, and `declarationDate` is blank on 2232 of
4000 calendar rows, so the converter must read `""` as null rather than throw.

**`StockSplit`** — `splits` and `splits-calendar`. Five fields: `Symbol`, `Date`, `Numerator`, `Denominator`,
`SplitType`.

`Numerator` and `Denominator` are `int?`. Measured whole in 961 of 961 rows, including 707/500 and 729/1000
from non-US listings. `SplitType` is `string?` and must be read carefully: it is JSON-null in 16 rows *and*
the literal string `"None"` in others.

**`CompanyRating`** — `ratings-snapshot` and `ratings-historical`. Ten fields: `Symbol`, `Date`, `Rating`,
`OverallScore`, `DiscountedCashFlowScore`, `ReturnOnEquityScore`, `ReturnOnAssetsScore`, `DebtToEquityScore`,
`PriceToEarningsScore`, `PriceToBookScore`.

`Date` is `LocalDate?` and is simply absent from `ratings-snapshot`, which sends the other nine. That is the
`EmployeeCount` pattern: one record, the discriminating field nullable.

**The shipped `BulkCompanyRating` is not reused.** It has no `overallScore`, which both ordinary paths send.
Two records with nine overlapping fields is the honest outcome; forcing one would either drop a measured field
or add a permanently-null one to the bulk shape.

### Eight singleton records

| record | path | notes |
|---|---|---|
| `StockGrade` | `grades` | `Symbol`, `Date`, `GradingCompany`, `PreviousGrade`, `NewGrade`, `Action` |
| `GradeConsensus` | `grades-consensus` | `Symbol`, `StrongBuy`, `Buy`, `Hold`, `Sell`, `StrongSell`, `Consensus` |
| `GradeHistory` | `grades-historical` | `Symbol`, `Date`, and five `AnalystRatings*` counts |
| `PriceTargetConsensus` | `price-target-consensus` | `Symbol`, `TargetHigh`, `TargetLow`, `TargetConsensus`, `TargetMedian` |
| `PriceTargetSummary` | `price-target-summary` | ten fields including `Publishers` |
| `IpoCalendarEntry` | `ipos-calendar` | nine fields including the redundant `Daa` |
| `IpoDisclosure` | `ipos-disclosure` | `Symbol`, `FilingDate`, `AcceptedDate`, `EffectivenessDate`, `Cik`, `Form`, `Url` |
| `IpoProspectus` | `ipos-prospectus` | thirteen fields, six of them money |

`GradeConsensus` and `GradeHistory` stay separate records **because the measurement says they are separate
data**, not two views of one thing — see the trap below.

Every record is a `public sealed record` with `init` properties, an explicit `[JsonPropertyName]` on every
member, no `required` members and no non-nullable properties, per the house rule. `Cik` is `string?`, never an
integer type: it arrives zero-padded to ten characters.

## `DividendCalendarResult`

`dividends-calendar` truncates at 4000 rows and drops them **from the front of the requested range** — a full
year returns its last three days. The already-shipped `EarningsCalendarResult` solves exactly this problem for
`stable/earnings-calendar`, and this design applies that proven type to the second endpoint measured to have
it rather than inventing anything.

It mirrors the original member for member:

- implements `IReadOnlyList<Dividend>`, so `GetDividendsCalendarAsync` still returns a list and nothing about
  the ordinary path changes;
- `RowCap = 4000`, documented with the measurement that establishes it;
- `RowsReturned` counted on the **raw** response, before any clamping or dropping — the ordering that makes the
  signal trustworthy, and the reason the original exists;
- `AtRowCap` — `RowsReturned >= RowCap`;
- `MissesStartOfRange` — nothing came back for the first requested day though later days did;
- `IsLikelyTruncated` — either tell fired;
- `RequestedFrom`, `RequestedTo`, `EarliestReturnedDate`.

**Auto-chunking is explicitly out of scope.** Neither calendar does it today; adding it needs request-count
limits, cancellation semantics and a decision about partial failure mid-walk, and that is a slice of its own.
The design records the safe width the measurement supports — roughly six days at the observed 340–876
rows/day, but season-dependent, which is precisely why the type reports rather than guesses.

`stable/earnings-calendar` behaviour is unchanged by this slice.

## Converters

**`PublisherListJsonConverter`** — new. `price-target-summary` sends `publishers` as a *string containing a
JSON array*. Unlike the `businessAddress` field measured in the previous slice, this string is properly
escaped JSON (`Investor's Business Daily` survives), so a real `JsonSerializer` parse is safe rather than
fragile. It binds to `IReadOnlyList<string>?`, matching the shipped `BulkPriceTargetSummary.Publishers` so
that the bulk and ordinary paths stop disagreeing about the type of one field.

It returns null rather than throwing on any token that is not a string, and null on a string that does not
parse — one bad field costs that field, never the response. That rule is the house convention and was made
explicit in the previous slice.

**Existing converters are reused everywhere else.** `NullableLocalDateJsonConverter` reads every date field in
this slice, including the blank-string cases — verified in the shipped source rather than assumed:
`LocalDatePattern.Iso.Parse("")` fails, `parsed.Success` is false, and the converter answers null without
throwing. That is what makes the 2232 blank `declarationDate` rows safe.

*Noted, not fixed:* that converter reads `reader.GetString()` with no token-type guard, so a non-string token
would throw and — because `FmpTransport` does not wrap `DeserializeAsync` — cost the whole response. It is the
same latent defect the previous slice found and fixed on `BusinessAddressJsonConverter`. Every date field
measured in this slice arrives as a string or is absent, so the risk here is hypothetical, and the converter is
shared by many shipped endpoints; changing it is a separate decision rather than a rider on a coverage slice.
`PublisherListJsonConverter`, being new, is written with the guard from the start.

**`NullableEasternInstantJsonConverter` must not be used here.** `acceptedDate` on the two ipos paths is a
plain 10-character date, not the 19-character `uuuu-MM-dd HH:mm:ss` stamp `SecFiling.AcceptedDate` carries.
The same field name means a different thing in a different endpoint family, and pointing the Eastern converter
at it answers null for every row without erroring.

## Signatures

A signature never accepts a parameter the endpoint ignores. That rule does real work here — the measurement
found four ignored parameters across seven methods.

```csharp
// fmp.Analyst
Task<IReadOnlyList<StockGrade>>  GetGradesAsync(string symbol, CancellationToken ct = default);
Task<GradeConsensus?>            GetGradeConsensusAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<GradeHistory>> GetGradeHistoryAsync(string symbol, int limit = 100, CancellationToken ct = default);
Task<PriceTargetConsensus?>      GetPriceTargetConsensusAsync(string symbol, CancellationToken ct = default);
Task<PriceTargetSummary?>        GetPriceTargetSummaryAsync(string symbol, CancellationToken ct = default);
Task<CompanyRating?>             GetRatingAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<CompanyRating>> GetRatingHistoryAsync(string symbol, int limit = 100, CancellationToken ct = default);

// fmp.Calendar
Task<IReadOnlyList<Dividend>>    GetDividendsAsync(string symbol, int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<Dividend>>    GetDividendsCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default);
Task<IReadOnlyList<StockSplit>>  GetSplitsAsync(string symbol, int limit = 100, CancellationToken ct = default);
Task<IReadOnlyList<StockSplit>>  GetSplitsCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default);
Task<IReadOnlyList<IpoCalendarEntry>> GetIpoCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default);
Task<IReadOnlyList<IpoDisclosure>>    GetIpoDisclosuresAsync(LocalDate from, LocalDate to, CancellationToken ct = default);
Task<IReadOnlyList<IpoProspectus>>    GetIpoProspectusesAsync(LocalDate from, LocalDate to, CancellationToken ct = default);
```

**`GetGradesAsync` takes only a symbol.** `grades` ignores `limit` *and* `page` — 1791 rows for AAPL under
every combination, with a byte-identical first row on `page=1`. It returns the whole series and there is no
way to ask for less.

**Single-row lookups return `T?`.** `grades-consensus`, `price-target-consensus`, `price-target-summary` and
`ratings-snapshot` each returned exactly one row, and an unknown-but-well-formed symbol answers an empty array
with HTTP 200 rather than a 404. This matches `CompanyEndpoints.GetProfileAsync` and the ruling made in the
previous slice.

**No per-symbol method takes `from`/`to`.** Measured ignored on all five that might plausibly accept them.

**`GetRatingHistoryAsync` defaults `limit` to 100, not to FMP's default.** Left absent, `ratings-historical`
returns **one row** — from an endpoint whose name promises a series. Defaulting to 1 would be faithful to FMP
and useless to a caller; defaulting to 100 matches every other paged method in this SDK. The XML documentation
states plainly what FMP does when the parameter is omitted, so the choice is visible rather than hidden.

`limit` on these paths is a "take N", not a cap: `ratings-historical` returned 6292 rows for `limit=10000` and
the same 6292 for `limit=50000`, because that is the whole series. Nothing here needs a `Max*PageSize`
constant, since no cap was measured.

The date-ranged Calendar methods take `from`/`to` as **required** `LocalDate` parameters and route through the
shared `DateRange.ThrowIfBackwards` promoted in the previous slice. No fifth copy of that guard.

## Traps, and what defends each

Twelve measured traps. Each gets a test that fails if the trap is reintroduced.

| # | trap | defence |
|---|---|---|
| 1 | `dividends-calendar` returns 4000 rows and eats the front of the range | `DividendCalendarResult` and its two tells |
| 2 | `ratings-historical` answers **one** row with no `limit` | `limit` defaults to 100; documented; test |
| 3 | `grades` ignores `limit` and `page` | signature accepts neither; test asserts the query string carries neither |
| 4 | `from`/`to` ignored on all five per-symbol paths | signatures accept neither |
| 5 | `grades-consensus` is **not** the newest `grades-historical` row | separate records; both documented with the measured counts |
| 6 | `ipos-calendar.daa` duplicates `date` at a constant `T04:00:00.000Z` | modelled and documented as redundant, never used as a distinct value |
| 7 | `acceptedDate` here is a date, not the SEC paths' timestamp | `LocalDate?` via the plain converter; test pins the 10-character form |
| 8 | `splitType` is null *and* the literal `"None"` | documented; test binds both |
| 9 | `declarationDate` blank on 2232/4000 rows | blank reads as null, not as an error |
| 10 | `ipos-calendar` shares / priceRange / marketCap mostly null | all nullable; fixture carries null rows |
| 11 | `ipos-disclosure` returns 132,332 rows uncapped for a wide range | documented on the method as a payload warning |
| 12 | `frequency` shows 2 values on one path and 8 on another | `string?`, never an enum |

## Testing

Fixtures are verbatim captures from the 2026-08-28 pass, five rows each, and must never contain the API key —
it travels in the query string, so no built URL is ever written to a fixture or a log line.

Fourteen path fixtures. Three earn extra captures because one response cannot carry the trap:
a `dividends-calendar` capture whose `declarationDate` is blank, an `ipos-calendar` capture with the
mostly-null numeric fields, and a `splits-calendar` capture containing both a null `splitType` and a `"None"`
one.

Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore
against a forced fresh build. Every model is registered in `FmpJsonContext` — a missing registration fails at
runtime, not at compile time.

The live smoke sweep reaches new endpoints by reflection, so all fourteen are already in it. `Probe.Argument`
needs no new argument names — `symbol`, `limit`, `from` and `to` are all known — but `from` now dispatches on
declaring type, and the two new date-ranged Calendar methods must be checked against that dispatch so they get
a range wide enough to answer rows and narrow enough not to truncate.

## What this design does not do

- **No auto-chunking** on either calendar. Recorded as the natural next slice.
- **No change to `stable/earnings-calendar`.** Its result type is the model being followed, not modified.
- **No enums** for `frequency`, `action`, `newGrade`, `splitType`, `actions` or `exchange`. Each observed set
  is a sample from one path and one symbol, not a domain.
- **No reuse of `BulkCompanyRating`**, which lacks `overallScore`.
- **`Numerator`/`Denominator` stay `int?`.** 961 of 961 whole. Recording what was measured beats widening to
  `decimal?` against a fractional value nobody has seen.
