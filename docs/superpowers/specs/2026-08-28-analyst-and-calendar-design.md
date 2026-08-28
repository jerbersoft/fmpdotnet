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

**Eleven new records, one generic result type, one converter, eleven `FmpJsonContext` registrations.**

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
from non-US listings, with maxima of 1,011,977 and 1,000,000. `SplitType` is `string?` and is **JSON-null on 16
of 961 rows**; the three string values are `stock-split`, `stock-dividend` and `spin-off`. (This paragraph
first claimed a literal `"None"` sentinel as well. Re-measured field by field over all 961 rows while planning,
that string appears nowhere in the response — see the measurements.)

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
| `IpoCalendarEntry` | `ipos-calendar` | nine fields including the redundant `Daa`; `PriceRange` is `string?` and `Shares`/`MarketCap` are `decimal?` — see below |
| `IpoDisclosure` | `ipos-disclosure` | `Symbol`, `FilingDate`, `AcceptedDate`, `EffectivenessDate`, `Cik`, `Form`, `Url` |
| `IpoProspectus` | `ipos-prospectus` | thirteen fields, six of them money |

`GradeConsensus` and `GradeHistory` stay separate records **because the measurement says they are separate
data**, not two views of one thing — see the trap below.

Every record is a `public sealed record` with `init` properties, an explicit `[JsonPropertyName]` on every
member, no `required` members and no non-nullable properties, per the house rule. `Cik` is `string?`, never an
integer type: it arrives zero-padded to ten characters.

**Three numeric typings were corrected while planning, from a magnitude sweep the first pass did not run.**

- **`IpoCalendarEntry.PriceRange` is `string?`, not a number.** It is null on 441 of 450 rows, and the nine
  populated ones are formatted strings — `"5.00 - 7.00"`, `"10.00"`, `"15 - 17"`. Typed `decimal?` it would
  read null on all 450: null where FMP sent null, and null where FMP sent a price, indistinguishably. Same
  shape as `SecProfile.FiftyTwoWeekRange` from the previous slice.
- **`IpoCalendarEntry.MarketCap` and `Shares` are `decimal?`**, matching `MarketCapitalization.MarketCap` and
  `SharesFloat.OutstandingShares`. `marketCap` was measured at 74,999,999,925 — thirty-five times
  `int.MaxValue`. An `int?` there does not answer null; `System.Text.Json` throws, and `FmpTransport` does not
  wrap `DeserializeAsync`, so one row costs the whole response.
- **Every money field on `IpoProspectus` is `decimal?`**, for the same reason plus a fractional one:
  `pricePublicTotal` reaches 74,999,999,925 and 13 of 165 rows carry a fractional value in it.

## `CalendarResult<T>`

**Three of the five date-ranged methods truncate, by two different mechanisms.** This section originally
specified a single `DividendCalendarResult` for `dividends-calendar`, on the measurement that the other
calendar paths "do not hit the cap". They do not — and two of them truncate anyway:

| path | mechanism | what a caller sees |
|---|---|---|
| `dividends-calendar` | 4000-row cap | a full year answers its last three days |
| `splits-calendar` | **90-day window from `to`** | a full year answers Q4, at 737 rows |
| `ipos-calendar` | **90-day window from `to`** | a full year answers Q4, at 358 rows |
| `ipos-disclosure` | none measured | the whole range, 25,689 rows for 2024 |
| `ipos-prospectus` | none measured | the whole range, 1,048 rows for 2024 |

Both mechanisms drop rows from the **front** of the requested range and report nothing. They differ in what can
detect them: 737 rows is nowhere near any cap, so a row-count test is blind to the window clamp. Only comparing
the earliest returned date against the requested `from` sees both.

**So the type is generic: `CalendarResult<T>`, one class serving all three.** Three hand-mirrored copies of one
result type would be verbatim duplication of a logic block, which the review rubric treats as a defect, and the
second and third copies would differ only in which tell can fire.

- implements `IReadOnlyList<T>`, so every signature in the block below is unchanged and nothing about the
  ordinary path changes;
- `RowsReturned` counted on the **raw** response, before any clamping or dropping — the ordering that makes the
  signal trustworthy, and the reason `EarningsCalendarResult` exists;
- `RowCap` — `4000` for `dividends-calendar`, `null` for the two window-clamped paths, because no row cap was
  measured on them and a made-up one would be a fact nobody checked;
- `LookbackLimitDays` — `90` for the two window-clamped paths, `null` for `dividends-calendar`, where the row
  cap always fires first and so no window limit is observable;
- `AtRowCap` — `RowCap is { } cap && RowsReturned >= cap`; never fires where `RowCap` is null;
- `ExceedsLookbackLimit` — the requested range is wider than `LookbackLimitDays`; never fires where that is null;
- `MissesStartOfRange` — the earliest row returned is later than the requested `from`. **This is the only tell
  that sees both mechanisms**, and the reason it is not merely a second opinion;
- `LikelyTruncated` — any tell fired;
- `RequestedFrom`, `RequestedTo`, `EarliestReturnedDate`;
- `static bool IsLikelyTruncated(IReadOnlyList<T> rows)`, mirroring the shipped helper, for callers holding
  the result as a plain list.

Naming follows the shipped type exactly: the **property** is `LikelyTruncated` and the **static method** is
`IsLikelyTruncated`. An earlier draft of this section wrote the property as `IsLikelyTruncated`, which is the
method's name on `EarningsCalendarResult` and would not compile beside it.

**Auto-chunking is explicitly out of scope.** No calendar does it today; adding it needs request-count limits,
cancellation semantics and a decision about partial failure mid-walk, and that is a slice of its own. What the
measurement supports is recorded instead: roughly six days for `dividends-calendar` at the observed 340–876
rows/day, season-dependent — which is precisely why the type reports rather than guesses — and a flat 90 days
for the other two, which is not season-dependent and which the type therefore states outright.

`stable/earnings-calendar` and its `EarningsCalendarResult` are **unchanged by this slice.** Retrofitting the
shipped type onto the generic one is a follow-up, not a rider on a coverage slice: it is public API on a
shipped path, its own tests pin it, and nothing in this slice needs it moved.

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

Fifteen measured traps — twelve from the first pass, three added by the re-measurement while planning. Each gets a test that fails if the trap is reintroduced.

| # | trap | defence |
|---|---|---|
| 1 | `dividends-calendar` returns 4000 rows and eats the front of the range | `CalendarResult<Dividend>`, `RowCap = 4000` |
| 2 | `ratings-historical` answers **one** row with no `limit` | `limit` defaults to 100; documented; test |
| 3 | `grades` ignores `limit` and `page` | signature accepts neither; test asserts the query string carries neither |
| 4 | `from`/`to` ignored on all five per-symbol paths | signatures accept neither |
| 5 | `grades-consensus` is **not** the newest `grades-historical` row | separate records; both documented with the measured counts |
| 6 | `ipos-calendar.daa` duplicates `date` at a constant `T04:00:00.000Z` | modelled and documented as redundant, never used as a distinct value |
| 7 | `acceptedDate` here is a date, not the SEC paths' timestamp | `LocalDate?` via the plain converter; test pins the 10-character form |
| 8 | `splitType` is JSON-null on 16 of 961 rows | `string?`; fixture carries null rows; test binds them |
| 9 | `declarationDate` blank on 2232/4000 rows | blank reads as null, not as an error |
| 10 | `ipos-calendar` shares / priceRange / marketCap mostly null | all nullable; fixture carries null rows |
| 11 | `ipos-disclosure` returns 132,332 rows uncapped for a wide range | documented on the method as a payload warning |
| 12 | `frequency` shows 2 values on one path and 8 on another | `string?`, never an enum |
| 13 | `splits-calendar` and `ipos-calendar` answer a year with one quarter | `CalendarResult<T>`, `LookbackLimitDays = 90` |
| 14 | `ipos-calendar.priceRange` is a formatted string, populated on 9 rows in 450 | `string?`; test binds a populated one |
| 15 | `marketCap` and two prospectus totals exceed `int` by ~35× | `decimal?`; test binds the measured maximum |

## Testing

Fixtures are verbatim captures from the 2026-08-28 pass, five rows each, and must never contain the API key —
it travels in the query string, so no built URL is ever written to a fixture or a log line.

Fourteen path fixtures, plus **two** extra captures — not three. The two dropped from the original count were
dropped because the path fixture measured on the same day already carries the trap verbatim, and a hand-built
duplicate is weaker evidence than a real capture:

- a `dividends-calendar` capture whose `declarationDate` is blank — **already the head fixture**: all five of
  its captured rows carry a blank `declarationDate`, and `dividends.AAPL.json` carries populated ones, so both
  states are covered by path fixtures with no third file;
- an `ipos-calendar` capture with the mostly-null numerics — **already the head fixture**, all five rows null
  in `shares`, `priceRange` and `marketCap`. What is *not* covered is the opposite, so the extra capture is
  `ipos-calendar.priced.json`: rows with all three populated, including the formatted `priceRange` string;
- a `splits-calendar` capture carrying a null `splitType` — genuinely needed, because the head fixture is
  `stock-split` on all five rows. `splits-calendar.split-types.json` carries two nulls, two `stock-dividend`
  and the single `spin-off`. It does not carry a `"None"`, because there is none to carry.

Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore
against a forced fresh build. Every model is registered in `FmpJsonContext` — a missing registration fails at
runtime, not at compile time.

The live smoke sweep reaches new endpoints by reflection, so all fourteen are already in it. `Probe.Argument`
needs no new argument names — `symbol`, `limit`, `from` and `to` are all known — but `from` dispatches on
declaring type, and **five** new date-ranged Calendar methods land on that dispatch, not two as this section
first said. Its `CalendarEndpoints` arm currently answers `SettledWeekday` for `from`, giving every one of them
a one-day window: correct for `dividends-calendar`, which answers 876 rows in a day, and wrong for the four
others, which are sparse enough that a single day can answer nothing and record an empty baseline that agrees
with itself forever. The width has to be chosen per method, and pinned by a keyless test.

## What this design does not do

- **No auto-chunking** on any calendar. Recorded as the natural next slice.
- **No change to `stable/earnings-calendar`.** Its result type is the model being followed, not modified, and
  not retrofitted onto `CalendarResult<T>` either — a separate, deliberate follow-up.
- **No enums** for `frequency`, `action`, `newGrade`, `splitType`, `actions` or `exchange`. Each observed set
  is a sample from one path and one symbol, not a domain.
- **No reuse of `BulkCompanyRating`**, which lacks `overallScore`.
- **`Numerator`/`Denominator` stay `int?`.** 961 of 961 whole, largest 1,011,977 against an `int.MaxValue` of
  2,147,483,647. Recording what was measured beats widening to `decimal?` against a fractional value nobody has
  seen. This is the opposite ruling from `marketCap` above, and the difference is the measurement: those two
  were checked against the limit and fit, `marketCap` was checked and does not.
