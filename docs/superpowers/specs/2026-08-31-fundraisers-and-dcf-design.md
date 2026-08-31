# Fundraisers and DCF — design

Issue #39. Ten paths, two facades, ten records, one new converter.

The measurements this argues from are in
[`2026-08-31-fundraisers-and-dcf-measurements.md`](2026-08-31-fundraisers-and-dcf-measurements.md)
(commit `2be9017`, 145 captures, 13,287 rows). **This document adds sixteen further measurements taken
during the design phase on 2026-08-31**, listed in "Measured after the measure phase" at the end. Every
claim below carries its date. Nothing is taken from FMP's documentation except where the text says so, and
where it does, the claim is labelled as documented and — in two cases — recorded as refuted by measurement.

---

## The shape of the problem

Ten paths that FMP groups together and that have almost nothing in common. Six are SEC filing feeds: Reg CF
crowdfunding offerings (Form C) and Reg D exempt equity offerings (Form D). Four are valuation endpoints.
They share no argument, no response shape, and no vocabulary.

Three traps make this slice different from a routine coverage slice, and all three are silent — HTTP 200,
well-formed rows, no exception:

1. **A field called `date` is encoded four different ways across the group**, and one of those encodings is
   `MM-DD-YYYY`, which the SDK's existing ISO converter reads as `null` without throwing. Binding it
   naively loses a field populated on 100% of rows.
2. **The two custom-DCF paths honour different override vocabularies.** A name accepted by one is discarded
   by the other at HTTP 200 with no indication the caller's assumption was dropped.
3. **Every wrong argument in this group answers `[]` at HTTP 200.** All three CIK and name constants the
   smoke sweep already owns produce that empty answer here, so six endpoints would record `outcome empty`
   as their healthy baseline and match green for ever.

### Five decisions were the user's and are settled

1. **Two facades, not one** — `fmp.Fundraisers` and `fmp.DiscountedCashFlow`.
2. **`fmp.DiscountedCashFlow`**, spelled out rather than `fmp.Dcf`.
3. **One assumptions record per custom-DCF path**, not a shared type and not a long parameter list.
4. **Two records for the two custom-DCF shapes**, not one merged record.
5. **Two records for the two plain DCF paths**, despite an identical wire shape.

---

## The public surface

### `fmp.Fundraisers` — six paths, six methods

```csharp
Task<IReadOnlyList<CrowdfundingOffering>>  GetCrowdfundingOfferingsByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<CrowdfundingOffering>>  GetCrowdfundingOfferingsLatestAsync(int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<CrowdfundingSearchHit>> SearchCrowdfundingOfferingsAsync(string name, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingNotice>>     GetFundraisingByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingNotice>>     GetFundraisingLatestAsync(int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingSearchHit>>  SearchFundraisingAsync(string name, CancellationToken ct = default);
```

```csharp
public const int MaxCrowdfundingPageSize = 1000;   // crowdfunding-offerings-latest
public const int MaxFundraisingPageSize  = 100;    // fundraising-latest
```

**`ByCik` and `Search` are spelled out because the two are not interchangeable and the SDK should not imply
they are.** Measured 2026-08-31 in both directions: the crowdfunding CIK `0002152721` returns **0 rows** on
`fundraising`, and the fundraising CIK `0001617426` returns **0 rows** on `crowdfunding-offerings`. Form C
and Form D filers are disjoint populations.

**Only the two `-latest` methods take paging, and only because paging was measured to work on them.** On the
by-CIK paths `page` had no measured effect — `fundraising?cik=…` returned the same 14 rows at `page=0` and
`page=1` — and those paths return the filer's whole history in one response. On the four search paths
`limit` is ignored outright: measured 2026-08-31, `crowdfunding-offerings-search?name=Well&limit=2` returned
all **44** rows and `fundraising-search?name=Apple&limit=2` all **59**. A parameter the SDK offers that the
wire discards is worse than no parameter, so none of those six methods has one.

### `fmp.DiscountedCashFlow` — four paths, four methods

```csharp
Task<IReadOnlyList<DcfValuation>>              GetValuationAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<LeveredDcfValuation>>       GetLeveredValuationAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<CustomDcfProjection>>       GetCustomValuationAsync(string symbol, CustomDcfAssumptions? assumptions = null, CancellationToken ct = default);
Task<IReadOnlyList<CustomLeveredDcfProjection>> GetCustomLeveredValuationAsync(string symbol, CustomLeveredDcfAssumptions? assumptions = null, CancellationToken ct = default);
```

No `limit` and no `page`: measured 2026-08-31, `custom-discounted-cash-flow?symbol=AAPL&limit=3` returned
the full **10** rows.

---

## The models

Ten types. Every property is nullable, and the measured null counts live in the XML docs rather than in the
type — "never null in 1,000 rows" and "cannot be null" are different statements and only the first was
measured.

| record | properties | paths |
|---|---|---|
| `CrowdfundingOffering` | 48 | `crowdfunding-offerings`, `crowdfunding-offerings-latest` |
| `FundraisingNotice` | 43 | `fundraising`, `fundraising-latest` |
| `CrowdfundingSearchHit` | 3 | `crowdfunding-offerings-search` |
| `FundraisingSearchHit` | 3 | `fundraising-search` |
| `DcfValuation` | 4 | `discounted-cash-flow` |
| `LeveredDcfValuation` | 4 | `levered-discounted-cash-flow` |
| `CustomDcfProjection` | 47 | `custom-discounted-cash-flow` |
| `CustomLeveredDcfProjection` | 34 | `custom-levered-discounted-cash-flow` |
| `CustomDcfAssumptions` | 16 | input |
| `CustomLeveredDcfAssumptions` | 10 | input |

The four response field counts were confirmed twice: against the live captures, and against the independent
Python `fmpsdk` implementation in a sibling checkout, whose `TypedDict` definitions carry **47, 34, 48 and
43** fields with identical key sets (checked 2026-08-31).

### Why the two search records are separate

They carry the same three keys — `cik`, `name`, `date` — in the same order, and nothing else. They are still
two records, because `date` is a different **type** on each:

- `crowdfunding-offerings-search.date` is `MM-DD-YYYY` and **null on 6.6%** of rows (461 of 7,003) →
  `LocalDate?`.
- `fundraising-search.date` is `yyyy-MM-dd HH:mm:ss` and is the filing's **acceptance timestamp** →
  `Instant?`.

The second is not an assumption. Measured 2026-08-31, for CIK `0001617426` all **14** search timestamps
equal the 14 `acceptedDate` values returned by `fundraising?cik=…` exactly.

### Why the two plain DCF records are separate

The wire shape is genuinely identical — `symbol`, `date`, `dcf`, `Stock Price` — so this split buys nothing
structural. It buys type safety over a number that diverges enormously. Measured 2026-08-27/31: KO is
**83.71** unlevered against **49.77** levered, a 41% gap; JPM is 728.00 against 907.85. Neither is "the" DCF.
With one record, a `DcfValuation` variable that has drifted from the method call that produced it is
indistinguishable from the other model's answer; with two, passing one where the other is expected does not
compile. The Python `fmpsdk` reached the same conclusion independently and says so in a comment on the type.

### `Stock Price` — capitalised, with a space

`[JsonPropertyName("Stock Price")]` on both plain DCF records, reproduced exactly. Already documented for
`dcf-bulk`'s CSV on `BulkDiscountedCashFlow`; it appears in JSON here. The Python SDK had to abandon
class-body `TypedDict` syntax for this field because a Python identifier cannot contain a space — an
independent confirmation that the space is real and not a transcription slip.

### `CrowdfundingOffering.Date` is not the filing date

**The most easily-missed semantic trap in the slice**, and it was caught from FMP's own documented sample,
which shows `"date": "11-22-2011"` beside `"filingDate": "2026-07-30 00:00:00"` — fifteen years apart.

Measured 2026-08-31:

- `date` **precedes `filingDate` on 1,000 of 1,000** rows. Zero exceptions. Gaps run 0 to 43 years and the
  year range is 1983–2026.
- It is a property of the **company**, not of the filing: across 18 filer histories it is constant across
  every filing for **10 of them**, including one issuer whose **48** filings all carry `12-19-2023`.

That behaviour matches an issuer's date of formation. The SDK does **not** rename the property — the wire
says `date` and no reachable documentation labels it — but the XML doc states the measurement and says
plainly that this is not when the filing happened. A test pins `date < filingDate`.

### Three fields whose names do not describe their contents

| field | wire | bound as | why |
|---|---|---|---|
| `compensationAmount` | free prose | `string?` | e.g. *"7.9% of the offering amount upon a successful fundraise, and be entitled to reimbursement…"* — never a number |
| `financialInterest` | free prose | `string?` | 57 distinct values up to 256 chars; `"No"` is common but it is not a boolean |
| `overSubscriptionAccepted` | `"Y"` / `"N"` | `bool?` | via the existing `YesNoBooleanJsonConverter`, which maps any unmeasured third value to null rather than guessing |
| `taxRateCash` (custom DCF) | dollars | `decimal?` | a **cash tax amount** (13.3M–24.1M for AAPL), not a rate; `taxRate` beside it reads 15.61 |

### `yearOfIncorporation` is a string, and that is deliberate

Measured over 100 rows: **never null**, `""` on **30**, a four-digit year on the other 70 — and a JSON
string in both cases. It binds as `string?` through `SentinelStringJsonConverter`, which collapses `""` to
null so absence has one spelling.

It is **not** `int?`. `FmpJsonContext` sets `NumberHandling = AllowReadingFromString` globally, so `"1998"`
would bind — but `""` throws, and `System.Text.Json` aborts the entire list deserialisation rather than the
one field. Thirty percent of rows would cost the caller the whole response. `dateOfFirstSale` (`""` on 7)
needs no special handling: `NullableLocalDateJsonConverter` already reads `""` as null.

### Widths and shapes that are not the obvious ones

- **`totalAmountSold` is `long?`.** Measured max **13,475,150,514**, which overflows Int32.
- **`issuerZipCode` is `string?`.** Three forms measured: `99999` on 990 rows, `9999` on 5, `99999-9999` on
  5. Not an integer.
- **`CustomDcfProjection.Year` is `int?`.** The wire sends a JSON **string** (`"2030"`); the context's
  `AllowReadingFromString` binds it with no converter. Ten rows per response, descending 2030 → 2021.
- **`costofDebt` is spelled with a lowercase `o`** in "of" — the only field in the group that breaks
  camelCase. Confirmed on the wire and in the Python SDK's type. The `[JsonPropertyName]` reproduces it and
  a test pins it.
- **`cashAndCashEquiValentMostRecentFiscalYear` / `…PriorFiscalYear` carry a capital `V`** in "Equivalent".
  Present in FMP's documented sample *and* on the wire, so it is stable rather than a transient bug.

### No `IsProjected` flag

Both custom paths return ten rows mixing history and forecast, and the wire carries no field marking which
is which. Two fields imply different boundaries: `revenuePercentage` jitters through 2024 and smooths from
2025, while `taxRateCash` is constant at 16,785,417 for 2026–2030. The measurement declined to pick, and so
does this design. `Year` is surfaced; the caller decides.

---

## The assumptions records

`CustomDcfAssumptions` carries **16** nullable decimals; `CustomLeveredDcfAssumptions` carries **10**. Nine
are shared. Only non-null members are written to the query, so an unset member means "use FMP's default for
that assumption".

| parameter | unlevered | levered |
|---|---|---|
| `beta`, `capitalExpenditurePct`, `costOfDebt`, `costOfEquity`, `longTermGrowthRate`, `marketRiskPremium`, `revenueGrowthPct`, `riskFreeRate`, `taxRate` | yes | yes |
| `cashAndShortTermInvestmentsPct`, `depreciationAndAmortizationPct`, `ebitPct`, `ebitdaPct`, `inventoriesPct`, `payablePct`, `receivablesPct` | yes | **ignored** |
| `operatingCashFlowPct` | **ignored** | yes |
| `sellingGeneralAndAdministrativeExpensesPct` | **ignored** | **ignored** |

**`sellingGeneralAndAdministrativeExpensesPct` is exposed on neither record.** It moved nothing on either
path, so a property for it would be a control that does nothing.

**Why two records rather than one.** An unrecognised or wrong-path parameter is silent. Measured 2026-08-31,
`custom-discounted-cash-flow?symbol=AAPL&notARealParam=99` returned HTTP 200 with `longTermGrowthRate`,
`beta` and `equityValuePerShare` identical to the baseline — the only fields that moved were the eight that
track live price. So a caller who hands `ebitdaPct` to the levered endpoint gets a valuation that silently
ignored their assumption.

**This is not hypothetical.** The Python `fmpsdk` assembles both custom calls through one shared
18-parameter helper, which means **eight of its eighteen levered parameters do nothing** and two of its
eighteen unlevered ones do nothing. Two records make that a compile error.

**`costOfEquity` came from reading that SDK, not from the wire.** The measure phase probed seventeen
candidate names chosen by guesswork and missed it; `fmpsdk` documents eighteen. Probed 2026-08-31, it is
honoured on **both** paths, moving `costOfEquity`, `wacc`, `terminalValue`, `presentTerminalValue`, and
`sumPvUfcf` / `pvLfcf` + `sumPvLfcf` respectively. The lesson is recorded rather than hidden: a
self-selected probe list is a lower bound on a parameter vocabulary, never a census.

**No validation of assumption values.** Measured 2026-08-27/31, `longTermGrowthRate=10` against AAPL
returned `equityValuePerShare = -1253.46` against 145.72 at the default rate of 4, because a terminal growth
rate at or above the measured `wacc` of 9.47 inverts the terminal-value denominator. FMP returns the result
rather than rejecting the input. The SDK does not invent a bound FMP does not enforce; the behaviour is
documented on the property.

---

## Converters — one new, five existing

### New: `NullableMonthDayYearDateJsonConverter`

Reads `MM-dd-uuuu` with an invariant culture. Null on JSON null, on `""`, and on any unparseable value,
following the rest of `NodaConverters.cs`: one bad field costs one field, never the whole response.

**The trap it exists to close.** `NullableLocalDateJsonConverter` parses with `LocalDatePattern.Iso` and
returns null on failure rather than throwing (`NodaConverters.cs:43-44`). Measured 2026-08-31 by
deserialising through it:

| input | result |
|---|---|
| `"08-28-2026"` | **null** |
| `"04-30-2027"` | **null** |
| `"2026-08-31"` | 2026-08-31 |

Binding crowdfunding's `date` with the ISO converter yields **null on 100% of rows, at HTTP 200, with no
exception and no warning**. The component order is measured, not assumed: over 1,000 crowdfunding rows and
6,542 dated search rows the first component never exceeds 12 while the second reaches 31, so `DD-MM-YYYY` is
ruled out by 7,542 rows. FMP's own documented sample corroborates it independently with `"11-22-2011"` and
`"10-31-2026"` — a 22 and a 31 in second position, which can only be days.

Applied to: `CrowdfundingOffering.Date`, `CrowdfundingOffering.OfferingDeadlineDate`,
`CrowdfundingSearchHit.Date`.

### Existing converters, and which field earns each

| converter | fields | wire |
|---|---|---|
| `NullableLocalDateJsonConverter` | `FundraisingNotice.Date`, `.DateOfFirstSale`, `DcfValuation.Date`, `LeveredDcfValuation.Date` | `yyyy-MM-dd` |
| `NullableDateAtMidnightJsonConverter` | `FilingDate` on both filing records | `yyyy-MM-dd 00:00:00` |
| `NullableEasternInstantJsonConverter` | `AcceptedDate` on both filing records, `FundraisingSearchHit.Date` | `yyyy-MM-dd HH:mm:ss` |
| `YesNoBooleanJsonConverter` | `CrowdfundingOffering.OverSubscriptionAccepted` | `"Y"` / `"N"` |
| `SentinelStringJsonConverter` | `FundraisingNotice.YearOfIncorporation` | `""` or `9999` |

### `filingDate` is a date, not a timestamp

Its time component is `00:00:00` on **3,575 of 3,575** rows measured 2026-08-31 — a date with a dummy
midnight bolted on, exactly what `NullableDateAtMidnightJsonConverter` was written for in the SEC Filings
slice (2,115 of 2,115 there). FMP's own documented sample shows `"2026-07-30 00:00:00"`. Binding it as a
timestamp would leak a meaningless midnight into every comparison a caller writes.

### `acceptedDate` is Eastern, and the measurement earns it

The wire sends `"yyyy-MM-dd HH:mm:ss"` with no offset and no zone marker. The SDK carries two converters for
that exact shape — `NullableFmpInstantJsonConverter` reads UTC, `NullableEasternInstantJsonConverter` reads
Eastern — and they are four to five hours apart. Choosing wrong is silent.

FMP's documentation does not settle it. The endpoint pages are unreachable (every page on
`site.financialmodelingprep.com` answers HTTP 403 to automated fetch), and the documented sample response
supplied by the user carries no offset, no `Z`, and no timezone note. So the wire was measured instead, over
**1,395 distinct `acceptedDate` values and 1,779 distinct `fundraising-search` timestamps spanning
2009–2026**:

| season | n | window |
|---|---|---|
| EDT (summer) | 1,060 | **06:00 – 22:00** |
| EST (winter) | 445 | **06:00 – 21:59** |

**The window does not shift across the DST boundary.** That is the same discriminator the News slice used: a
stored instant rendered in a fixed zone moves by an hour across the boundary, a stripped wall clock does not.

**A UTC reading is refuted arithmetically.** 20:00 EDT is 00:00 UTC, so an Eastern-window feed read as UTC
must place rows in hours 22–03. There are **zero** in 3,174 values. The only two outside 06:00–21:59 are
`2013-10-22 22:00:00` and `2015-10-06 22:00:44`, landing on the window's closing minute rather than beyond
it. The drop between hour 17 (114 rows) and hour 18 (59) sits on the same boundary as EDGAR's 17:30 ET
same-day cutoff.

If FMP ever changes the encoding, the weekly smoke baseline reports it as a property that stopped arriving.

---

## Guards

### Two paging guards, deliberately not shared

```csharp
private static void ThrowIfCrowdfundingPagingOutOfRange(int? limit, int? page);   // limit ≤ 1000
private static void ThrowIfFundraisingPagingOutOfRange(int? limit, int? page);    // limit ≤ 100
```

Merging them would be a defect rather than a tidy-up, and the numbers are measured: `crowdfunding-offerings-latest`
returned 1000 rows at both `limit=1000` and `limit=5000`, while `fundraising-latest` returned 100 at
`limit=1000` and 100 at `limit=101`. Their **defaults** differ by the same factor of ten — 100 rows against
10. A merged guard would either reject a legal request on one path or accept an illegal one on the other.
`FundraisersTests` has a test for each direction, in the shape `NewsTests` uses for the same situation.

`limit` is rejected at zero and below rather than passed through, because measured 2026-08-31 `limit=0`
returns **one row** on both paths — not an error and not nothing. `page` is rejected below zero, because
`page=-1` silently returns page 0 (identical first row).

### There is no page ceiling, on purpose

Measured 2026-08-31, `page=1000` answered HTTP 200 with rows on both `-latest` paths, where the News feeds
answer HTTP 400 past page 100. A ceiling invented here would reject requests FMP serves. This follows the
`GetArticlesAsync` precedent, and the real hazard — a page-until-empty loop that never terminates — is
documented on both methods rather than guarded.

Paging does genuinely advance: measured 2026-08-31, `page=0` and `page=1` at `limit=5` share **zero** rows
on both paths, and `acceptedDate` descends continuously across the boundary (crowdfunding `15:13:14` →
`15:02:41`).

### Required arguments

`ArgumentException.ThrowIfNullOrWhiteSpace` on every `cik`, `name` and `symbol`. Eight of the ten paths
answer a naked request with HTTP 400 and a plain-text body naming the missing parameter; rejecting locally
saves a call against the key's quota.

---

## What is documented rather than guarded

**No uppercase symbol guard on the DCF paths.** Measured 2026-08-31, `discounted-cash-flow?symbol=aapl`
returned `"symbol":"AAPL"` with values byte-identical to the uppercase call, and the custom path likewise
normalised and returned all 10 rows. The News slice guards case because lowercase there returns 0 rows at
HTTP 200; that reasoning does not transfer, and a guard invented here would reject a request FMP serves.

**No validation of the search string, and no type-ahead.** The matching rule for
`crowdfunding-offerings-search` is **not known**, and this design does not claim one. Measured: `Well` and
`Wellness` return byte-identical 44-row bodies while `Welln` and `Wellnes` return **zero**; `Or`, `Ora` and
`Orav` return zero while `Oravanti` returns one. Substring, prefix and whole-word are each refuted by one of
those rows. FMP's documentation describes the endpoint as searching "by company name, campaign name, or
platform" — **the platform clause is refuted by measurement**: `name=NetCapital` returns **0 rows**, though
"NetCapital Funding Portal Inc." is the intermediary in FMP's own documented sample response, and
`name=Wefunder` returns 4 rows that are all the company *Wefunder, Inc.* itself. The SDK passes the caller's
string through unchanged and the XML doc states that intermediate-length queries can return nothing.

`fundraising-search` does behave like a case-insensitive prefix match — `a` 0, `ab` 979, `abc` 56, `Ap` 421,
`App` 256, `Apple` / `apple` / `APPLE` 59 each, `pple` 0 — but the SDK still validates nothing, because that
is upstream's rule and it will go stale.

**Search returns one row per filing, not one per company.** `fundraising-search?name=Schutt` returned 34
rows across **5** distinct CIKs; `crowdfunding-offerings-search?name=Well` returned 44 rows across **31**.
A caller populating a company picker must dedupe by `cik`. The SDK does not deduplicate: the row is what the
wire sent.

**`cik` is accepted on `fundraising-latest` and silently ignored on its crowdfunding sibling, and neither is
exposed.** Measured 2026-08-31: `fundraising-latest?cik=0001617426&limit=100` returned **14 rows, all one
CIK** — identical in count to what `fundraising?cik=…` returns — while
`crowdfunding-offerings-latest?cik=0002010670&limit=100` returned **100 rows across 85 distinct CIKs**. The
parameter adds no capability that `GetFundraisingByCikAsync` does not already provide, and offering it on
one `-latest` method but not the other would invite a caller to try the one that fails silently. A
reflection test pins the absence on both methods so the finding is not lost.

**The plain and custom DCF paths do not reconcile, and neither reconciles with its own price.** Five symbols
captured back to back agreed to within ±0.18 and **matched exactly on none**, with the sign inconsistent
(XOM +0.03 against AAPL −0.06). The plain path is a stored daily value — `dcf = 145.66380328033068` and
`Stock Price = 319.7`, identical to all 14 decimal places across captures minutes apart — while the custom
path recomputes off a live price that moved 314.74 → 314.85 → 314.87 over the same window. Their two price
columns disagree **in both directions**: AAPL −4.83, MSFT −2.50, XOM **+2.50**. This replicates the finding
already documented on `ExchangeVariant.DcfDiff` (measured 2026-08-27) on a different pair of paths. Both
facades' XML docs say plainly: do not reconstruct or reconcile a price across these endpoints.

---

## Serialisation and wiring

`FmpJsonContext` gains **eight** `[JsonSerializable(typeof(List<T>))]` entries — the eight response records.
The two assumptions records are request inputs and are never deserialised.

`FmpClient` gains two constructor parameters and two properties, `Fundraisers` and `DiscountedCashFlow`.
`AddFmp` registers both endpoint types, and `AddFmpTests` gains them in its expected-registration set.

---

## The smoke sweep

### Four new `LiveApi` constants

The three the sweep already owns produce silent green on every path in this group. Measured 2026-08-31:

| existing constant | `crowdfunding-offerings` | `fundraising` | `crowdfunding-offerings-search` |
|---|---|---|---|
| `LiveApi.Cik` = `320193` | **0 rows** | **0 rows** | — |
| `LiveApi.FilerCik` = `0001067983` | **0 rows** | **0 rows** | — |
| `LiveApi.AcquirerNameQuery` = `"Apple"` | — | — | **0 rows** |

Every one is HTTP 200 with `[]`, so six endpoints would record `outcome empty` as their healthy baseline.
Four constants replace them, each measured to return rows:

| constant | value | path | rows |
|---|---|---|---|
| `CrowdfundingCik` | `0002010670` (Finlete Funding, Inc.) | `crowdfunding-offerings` | 48 |
| `CrowdfundingNameQuery` | `Finlete` | `crowdfunding-offerings-search` | 4 |
| `FundraisingCik` | `0001617426` (Schutt Private Investment Fund, LP) | `fundraising` | 14 |
| `FundraisingNameQuery` | `Apple` | `fundraising-search` | 59 |

`CrowdfundingCik` was chosen as the filer with the most filings (12) in a 1,000-row latest window rather
than the first one to hand, so the constant does not rest on a single filing. `FundraisingNameQuery` holds
the same literal as `AcquirerNameQuery` and is still its own constant: the value coincides by measurement,
not because the two paths share a vocabulary.

### `Probe.Argument` dispatches on the method name

Keying `cik` or `name` on the declaring type is not enough here — one facade holds both corpora, and a CIK
from one returns zero rows on the other. The dispatch keys on `parameter.Member.Name`, following the
`CongressEndpoints` precedent already in the file.

`Probe.Argument` also needs a case for `CustomDcfAssumptions` and `CustomLeveredDcfAssumptions`, which it
would otherwise throw on as unknown types. Both resolve to `null`, so the sweep baselines FMP's default
valuation rather than an arbitrary set of overrides.

---

## Testing

Fixture-backed unit tests in `tests/FmpDotNet.Tests/`, following `NewsTests`. Each trap below gets a test
that fails if the trap is reintroduced.

1. **The ISO converter regression.** Bind a crowdfunding fixture and assert `Date` equals the expected
   `LocalDate`. Swapping `NullableMonthDayYearDateJsonConverter` back to `NullableLocalDateJsonConverter`
   must fail this, not silently null the field.
2. **All four `date` encodings**, pinned against fixtures in one test — including `fundraising-search`'s
   `Instant?` beside `fundraising-latest`'s `LocalDate?`, same field name, same three-key shape, two types.
3. **A null search date.** The `crowdfunding-offerings-search` fixture carries a null-date row (6.6% of rows
   measured; FMP's own documented sample shows one).
4. **Empty string, not null.** A `fundraising` fixture row with `yearOfIncorporation: ""` and
   `dateOfFirstSale: ""`, asserting both read as null and that the other 41 fields survive.
5. **`filingDate` is a date.** Assert the property is `LocalDate?` and binds `"2026-07-30 00:00:00"`.
6. **`acceptedDate` is Eastern.** Assert a known wire value binds to the expected `Instant`, so substituting
   `NullableFmpInstantJsonConverter` shifts it and fails.
7. **`Date` precedes `FilingDate`** on every fixture row.
8. **`totalAmountSold` above Int32.** A fixture row above 2,147,483,647.
9. **Exact wire spellings.** `Stock Price`, `costofDebt`, `cashAndCashEquiValent*` — assert each binds.
10. **The two override vocabularies.** Pin the 16 and 10 property sets, so adding `EbitdaPct` to the levered
    assumptions record fails and so `SellingGeneralAndAdministrativeExpensesPct` cannot be added to either.
11. **The two paging guards, in both directions.** `limit=1000` accepted on crowdfunding and rejected on
    fundraising; `limit=101` rejected on fundraising and accepted on crowdfunding. Plus `limit=0` rejected
    and `page=-1` rejected on both.
12. **No paging on the six non-`-latest` methods, and no `cik` on the two `-latest` methods** — reflection
    tests pinning the absences, each carrying its measurement in a comment.
13. **The four sweep constants**, in the shape of the existing crypto/forex vocabulary test.

`EndpointCoverageTests` moves from 226 to **236** of 243 documented paths, and the README's generated
coverage table moves with it.

---

## Documentation deliverables

- XML docs on both facades carrying the six measured group-level behaviours, with dates.
- XML docs on every property whose name misleads, carrying the measured counts.
- README coverage table regenerated; the endpoint inventory marked for these ten paths.
- The measurements document is **not** rewritten. The sixteen design-phase measurements below are recorded
  here, in this document, dated.

---

## Measured after the measure phase

All 2026-08-31, during design. Sixteen findings the measure phase did not have.

| # | finding |
|---|---|
| 1 | `page` genuinely advances on both `-latest` paths — page 0 and page 1 share zero rows, `acceptedDate` descends across the boundary |
| 2 | No page ceiling — `page=1000` is HTTP 200 with rows on both |
| 3 | `page=-1` silently returns page 0 |
| 4 | `limit=0` returns **one** row on both `-latest` paths |
| 5 | Search paths ignore `limit` — 44 and 59 rows returned at `limit=2` |
| 6 | Custom DCF ignores `limit` — 10 rows at `limit=3` |
| 7 | DCF symbols are case-insensitive and normalise — `aapl` returns `"AAPL"`, byte-identical |
| 8 | `acceptedDate` window is 06:00–22:00 in **both** DST seasons (EDT n=1,060, EST n=445), zero rows in hours 22–05 of 3,174 |
| 9 | `filingDate` time component is `00:00:00` on 3,575 of 3,575 rows |
| 10 | `fundraising-search.date` **is** `acceptedDate` — 14 of 14 exact matches for CIK `0001617426` |
| 11 | `CrowdfundingOffering.date` precedes `filingDate` on 1,000 of 1,000 rows, range 1983–2026 |
| 12 | …and is constant across all filings for 10 of 18 filers, including one with 48 filings |
| 13 | `costOfEquity` is an **18th** override, honoured on both custom paths — found by reading the Python `fmpsdk`, not by guessing |
| 14 | `cik` is honoured on `fundraising-latest` (14 rows, 1 CIK) and **silently ignored** on `crowdfunding-offerings-latest` (100 rows, 85 CIKs) |
| 15 | FMP's documented sample matches the wire exactly — 48 keys, same set **and** same order |
| 16 | FMP's documented "or platform" search clause is **refuted** — `name=NetCapital` returns 0 rows |

---

## Files

**Created**

```
src/FmpDotNet/Endpoints/FundraisersEndpoints.cs
src/FmpDotNet/Endpoints/DiscountedCashFlowEndpoints.cs
src/FmpDotNet/Models/CrowdfundingOffering.cs
src/FmpDotNet/Models/CrowdfundingSearchHit.cs
src/FmpDotNet/Models/FundraisingNotice.cs
src/FmpDotNet/Models/FundraisingSearchHit.cs
src/FmpDotNet/Models/DcfValuation.cs            (DcfValuation + LeveredDcfValuation)
src/FmpDotNet/Models/CustomDcfProjection.cs     (unlevered + levered)
src/FmpDotNet/Models/CustomDcfAssumptions.cs    (both assumptions records)
tests/FmpDotNet.Tests/FundraisersTests.cs
tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs
tests/FmpDotNet.Tests/Fixtures/…               (10 fixtures, one per path)
```

**Modified**

```
src/FmpDotNet/Serialization/NodaConverters.cs   (+ NullableMonthDayYearDateJsonConverter)
src/FmpDotNet/Serialization/FmpJsonContext.cs   (+ 8 entries)
src/FmpDotNet/FmpClient.cs                      (+ 2 properties)
src/FmpDotNet/DependencyInjection/…             (+ 2 registrations)
tests/FmpDotNet.SmokeTests/LiveApi.cs           (+ 4 constants)
tests/FmpDotNet.SmokeTests/Probe.cs             (+ name dispatch, + 2 assumptions cases)
tests/FmpDotNet.Tests/AddFmpTests.cs
tests/FmpDotNet.Tests/EndpointCoverageTests.cs
README.md                                        (coverage 226 → 236)
```

---

## What this design does not do

- **Does not claim a matching rule for `crowdfunding-offerings-search`.** Three hypotheses refuted by
  measurement, and FMP's documented "platform" clause refuted too.
- **Does not mark custom DCF rows as actual or projected.** The wire carries no such flag and two fields
  imply two different boundaries.
- **Does not deduplicate search results by CIK**, or reconcile any price across the DCF paths.
- **Does not expose `sellingGeneralAndAdministrativeExpensesPct`**, or `cik` on either `-latest` method.
- **Does not bound assumption values.** FMP accepts a growth rate that inverts the valuation and returns the
  negative result; inventing a bound here would reject calls FMP serves.
- **Does not touch `ExchangeVariant.Dcf`.** Its XML doc gains one cross-reference to the new facade,
  replacing the note that says the DCF group is unmodelled.
