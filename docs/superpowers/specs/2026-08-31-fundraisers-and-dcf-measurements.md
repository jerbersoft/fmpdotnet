# Fundraisers and DCF — measurements

Issue #39. Ten paths, measured against the live FMP API on **2026-08-31 UTC**: 145 captures, 13,287 rows.
Every number below came from one of those captures. Nothing here is taken from FMP's documentation.

The API key travels in the query string, so no built URL appears in this document and no capture was kept in
the repository.

## Entitlement — all ten answer, and eight of them refuse a naked request

| path | naked | with its required parameter |
|---|---|---|
| `stable/crowdfunding-offerings` | 400 | 200 |
| `stable/crowdfunding-offerings-latest` | **200, 100 rows** | — |
| `stable/crowdfunding-offerings-search` | 400 | 200 |
| `stable/fundraising` | 400 | 200 |
| `stable/fundraising-latest` | **200, 10 rows** | — |
| `stable/fundraising-search` | 400 | 200 |
| `stable/custom-discounted-cash-flow` | 400 | 200 |
| `stable/custom-levered-discounted-cash-flow` | 400 | 200 |
| `stable/discounted-cash-flow` | 400 | 200 |
| `stable/levered-discounted-cash-flow` | 400 | 200 |

No path returned 402 or 403. This group is entitled in full on the current tier.

The eight 400s name the missing parameter in the body, as plain text:

```
Query Error: Invalid or missing query parameter - cik      crowdfunding-offerings, fundraising
Query Error: Invalid or missing query parameter - name     crowdfunding-offerings-search, fundraising-search
Query Error: Invalid or missing query parameter - symbol   all four DCF paths
```

**The body is plain text served under `content-type: application/json`.** It parses as neither an object nor
an array. `FmpTransport` already handles this correctly — measured by reading the code path: `text[0]` is `Q`,
so neither the `{` branch nor the `[` branch is taken and `message ??= text` passes the sentence through
verbatim. `FmpRequest.ToString()` renders without the API key (`FmpRequest.cs:46`), so the exception is safe
to log. No transport change is needed for this slice.

## Six shapes across ten paths

| shape | keys | paths |
|---|---|---|
| crowdfunding offering | 48 | `crowdfunding-offerings`, `crowdfunding-offerings-latest` |
| fundraising notice | 43 | `fundraising`, `fundraising-latest` |
| search hit | 3 | `crowdfunding-offerings-search`, `fundraising-search` |
| DCF valuation | 4 | `discounted-cash-flow`, `levered-discounted-cash-flow` |
| custom DCF, unlevered | 47 | `custom-discounted-cash-flow` |
| custom DCF, levered | 34 | `custom-levered-discounted-cash-flow` |

Within each of the first four rows the key lists are **identical and in the same order** — verified by direct
list comparison, not by eye. The by-CIK path and its `-latest` sibling are the same shape in both groups.

The two custom-DCF shapes share 29 keys. 18 are unlevered-only, 5 are levered-only:

- unlevered-only: `depreciation`, `depreciationPercentage`, `ebiat`, `ebit`, `ebitPercentage`, `ebitda`,
  `ebitdaPercentage`, `inventories`, `inventoriesPercentage`, `payable`, `payablePercentage`, `receivables`,
  `receivablesPercentage`, `sumPvUfcf`, `taxRateCash`, `totalCash`, `totalCashPercentage`, `ufcf`
- levered-only: `freeCashFlow`, `operatingCashFlow`, `operatingCashFlowPercentage`, `pvLfcf`, `sumPvLfcf`

## `date` is encoded four different ways across this group

**This is the typing decision of the slice, and one of the four encodings is one no existing SDK converter
reads.**

| path | `date` | measured over |
|---|---|---|
| `crowdfunding-offerings`, `crowdfunding-offerings-latest` | `MM-DD-YYYY` | 1,000 rows, 1,000 populated |
| `crowdfunding-offerings-search` | `MM-DD-YYYY`, **null on 6.6%** | 7,003 rows, 461 null |
| `fundraising`, `fundraising-latest` | `yyyy-MM-dd` | 114 rows, 0 null |
| `fundraising-search` | `yyyy-MM-dd HH:mm:ss` | 1,038 rows, 0 null |
| `discounted-cash-flow`, `levered-discounted-cash-flow` | `yyyy-MM-dd` | 10 rows |
| both custom DCF paths | no `date` field at all — a `year` string instead | 20 rows |

### The component order is measured, not assumed

Over the 1,000-row crowdfunding capture the first component ranges **1–12** and never exceeds 12, while the
second ranges **1–31**. The same holds over the 6,542 dated rows of a 7,003-row search capture. `MM-DD-YYYY`
is therefore the reading, and `DD-MM-YYYY` is ruled out by 7,542 rows that never place a value above 12 first.

`offeringDeadlineDate` on the crowdfunding shape carries the same `MM-DD-YYYY` encoding — 711 of 1,000 rows
populated, 289 null.

### The existing converter reads `MM-DD-YYYY` as null, silently

Measured 2026-08-31 by deserialising through `NullableLocalDateJsonConverter`:

| input | result |
|---|---|
| `"08-28-2026"` | **NULL** |
| `"04-30-2027"` | **NULL** |
| `"2026-08-31"` | 2026-08-31 |
| `""` | NULL |

`NullableLocalDateJsonConverter` parses with `LocalDatePattern.Iso` and returns `null` on failure rather than
throwing (`NodaConverters.cs:43-44`). Binding the crowdfunding `date` with it therefore yields **null on 100%
of rows, at HTTP 200, with no exception and no warning** — a field that is populated on every row arriving as
absent. A new converter for `MM-DD-YYYY` is required, and the trap deserves a test that fails when the ISO
converter is put back.

The empty-string case is already safe: `""` returns null rather than throwing, which matters because
`fundraising` carries empty strings where a null would be expected (below).

## Empty string, not null, on two fundraising fields

Over 100 `fundraising-latest` rows:

| field | null | empty string |
|---|---|---|
| `yearOfIncorporation` | 0 | **30** |
| `dateOfFirstSale` | 0 | **7** |

Neither field is ever `null`; absence is spelled `""`. A numeric or date binding that assumes `null` for
absence sees a token it must parse. The date converter tolerates it; an `int` binding on
`yearOfIncorporation` would not, which is an argument for reading that field as a string or through a
tolerant converter.

## The search paths share a shape and nothing else

Both search paths return `{cik, name, date}` and nothing more. `cik` and `name` were populated on every one
of the 9,237 search rows captured. Beyond that the two paths differ in every respect measured.

### They return one row per filing, not one per company

`fundraising-search?name=Schutt` returned 34 rows across **5 distinct CIKs**. The 14 rows sharing one CIK
carry a constant `name` and 14 distinct timestamps running from `2014-10-14 16:14:07` to 2026. The search is
a filing history. `crowdfunding-offerings-search?name=Well` returned 44 rows across **31 distinct CIKs**.
A caller populating a company picker has to dedupe by `cik`.

### Fundraising matches like a case-insensitive prefix; crowdfunding does not

`fundraising-search`, row counts by query: `a` 0, `ab` 979, `abc` 56, `Ap` 421, `App` 256, `Apple` 59,
`apple` 59, `APPLE` 59, `pple` 0. Monotonically narrowing as the query lengthens, case-insensitive, and a
one-character query returns nothing.

`crowdfunding-offerings-search` does not behave that way:

| query | rows |
|---|---|
| `W` | 5 |
| `We` | 0 |
| `Wel` | 0 |
| `Well` | 44 |
| `Welln` | 0 |
| `Wellnes` | 0 |
| `Wellness` | 44 |
| `O` | 1 |
| `Or` / `Ora` / `Orav` / `Oravant` | 0 |
| `Oravanti` | 1 |

`name=Well` and `name=Wellness` returned **byte-identical bodies**, yet only 7 of those 44 names contain a
standalone `well` token — the rest are `Wellness`. So it is not a substring match, not a prefix match, and
not a whole-word match. Every hypothesis tried was refuted by one of the rows above.

**This document does not claim a matching rule for `crowdfunding-offerings-search`.** What is measured is
that intermediate-length queries return nothing where both shorter and longer ones return rows, so a caller
cannot type-ahead against this path. The SDK should document the observed behaviour and pass the caller's
string through unchanged.

## Defaults and caps differ between the two `-latest` siblings

| path | default rows | `limit=1000` | `limit=5000` | ceiling |
|---|---|---|---|---|
| `crowdfunding-offerings-latest` | **100** | 1000 | 1000 | **1000** |
| `fundraising-latest` | **10** | 100 | — | **100** |

`limit=101` on `fundraising-latest` returned 100. Two sibling paths in the same group, a tenfold difference in
default and a tenfold difference in ceiling. Neither number is guessable from the other.

`page` had **no measured effect** on the by-CIK paths: `fundraising?cik=…` returned the same 14 rows at
`page=0` and at `page=1`. `limit` on `crowdfunding-offerings?cik=…&limit=5` returned the 1 row the CIK has.
The by-CIK paths return the filer's whole history; there is no measured paging surface on them.

## Silent green — every wrong argument answers `[]` at HTTP 200

| probe | rows |
|---|---|
| `crowdfunding-offerings?cik=0000000000` | 0 |
| `fundraising?cik=0000000000` | 0 |
| `crowdfunding-offerings-search?name=zzzznotacompany` | 0 |
| `fundraising-search?name=zzzznotacompany` | 0 |
| `discounted-cash-flow?symbol=ZZZZNOPE` | 0 |
| `custom-discounted-cash-flow?symbol=ZZZZNOPE` | 0 |

None of these is an error. All six are HTTP 200 with the body `[]`.

### The sweep's existing CIK constants all produce that silent green

`Probe.Argument` synthesises a `cik` argument from `LiveApi.Cik` (`"320193"`, Apple) or `LiveApi.FilerCik`
(`"0001067983"`, Berkshire). Measured against both by-CIK paths:

| constant | `crowdfunding-offerings` | `fundraising` |
|---|---|---|
| `LiveApi.Cik` = `320193` | **0 rows** | **0 rows** |
| `LiveApi.FilerCik` = `0001067983` | **0 rows** | **0 rows** |

Four hard zeros. Both paths would record `outcome empty` as their healthy baseline and match green for ever.
`LiveApi.AcquirerNameQuery` (`"Apple"`) is the same story on one of the two search paths: 59 rows on
`fundraising-search`, **0 rows** on `crowdfunding-offerings-search`.

**And a CIK from one group returns nothing on the other**, measured both directions: the crowdfunding CIK
`0002152721` returns 0 rows on `fundraising`, and the fundraising CIK `0001617426` returns 0 rows on
`crowdfunding-offerings`. Form C filers and Form D filers are disjoint populations here. One shared constant
cannot serve both paths.

Four new `LiveApi` constants are required. These were measured to return rows on 2026-08-31:

| constant | value | path | rows |
|---|---|---|---|
| crowdfunding CIK | `0002010670` (Finlete Funding, Inc.) | `crowdfunding-offerings` | 48 |
| crowdfunding name | `Finlete` | `crowdfunding-offerings-search` | 4 |
| fundraising CIK | `0001617426` (Schutt Private Investment Fund, LP) | `fundraising` | 14 |
| fundraising name | `Apple` | `fundraising-search` | 59 |

The crowdfunding CIK was chosen as the filer with the most filings (12) in a 1,000-row latest window rather
than the first one to hand, so the constant does not rest on a single filing.

## The custom DCF paths take overrides — two different vocabularies

Seventeen candidate parameter names were probed against each custom path, each with a value that would move
the result, and each result compared field-by-field against a baseline call. Price-driven fields were
excluded from the comparison because the endpoint recomputes off a live price between calls (below).

| parameter | `custom-discounted-cash-flow` | `custom-levered-discounted-cash-flow` |
|---|---|---|
| `beta` | honoured | honoured |
| `capitalExpenditurePct` | honoured | honoured |
| `costOfDebt` | honoured | honoured |
| `longTermGrowthRate` | honoured | honoured |
| `marketRiskPremium` | honoured | honoured |
| `revenueGrowthPct` | honoured | honoured |
| `riskFreeRate` | honoured | honoured |
| `taxRate` | honoured | honoured |
| `cashAndShortTermInvestmentsPct` | honoured | **ignored** |
| `depreciationAndAmortizationPct` | honoured | **ignored** |
| `ebitPct` | honoured | **ignored** |
| `ebitdaPct` | honoured | **ignored** |
| `inventoriesPct` | honoured | **ignored** |
| `payablePct` | honoured | **ignored** |
| `receivablesPct` | honoured | **ignored** |
| `operatingCashFlowPct` | **ignored** | honoured |
| `sellingGeneralAndAdministrativeExpensesPct` | **ignored** | **ignored** |

Fifteen honoured on the unlevered path, nine on the levered, eight shared.
`sellingGeneralAndAdministrativeExpensesPct` moved nothing on either.

**An ignored parameter is silent.** `custom-discounted-cash-flow?symbol=AAPL&notARealParam=99` returned HTTP
200 with `longTermGrowthRate`, `beta` and `equityValuePerShare` identical to the baseline call — the only
fields that moved were the eight that track live price. A caller who misspells an override gets the default
valuation and no indication that their assumption was discarded. **This is the argument against a single
shared options type across the two paths:** handing `ebitdaPct` to the levered endpoint compiles, runs,
returns 200, and quietly ignores the caller's assumption.

### An override can drive the valuation negative

`longTermGrowthRate=10` against AAPL returned `equityValuePerShare = -1253.46`, versus 145.72 at the default
rate of 4. The measured `wacc` on that call is 9.47. A long-term growth rate at or above WACC inverts the
terminal-value denominator, and the endpoint returns the result rather than rejecting the input. `beta=2.5`
returned 63.22. The endpoint validates nothing.

## The plain and custom DCF paths do not agree, and neither reconciles with its own price

Five symbols, captured back to back:

| symbol | `discounted-cash-flow.dcf` | custom `equityValuePerShare` | Δ | `levered.dcf` | custom levered | Δ |
|---|---|---|---|---|---|---|
| AAPL | 145.66 | 145.72 | −0.06 | 139.27 | 139.32 | −0.05 |
| MSFT | 301.70 | 301.76 | −0.06 | 335.17 | 335.24 | −0.07 |
| KO | 83.71 | 83.74 | −0.03 | 49.77 | 49.79 | −0.02 |
| JPM | 728.00 | 728.14 | −0.14 | 907.85 | 908.03 | −0.18 |
| XOM | 121.02 | 120.99 | **+0.03** | 125.12 | 125.09 | **+0.03** |

Close on every symbol, **exact on none**, and the sign is not consistent. The custom path at its default
assumptions is the same model as the plain path, evaluated against a different price snapshot.

### The plain path is a stored daily value; the custom path recomputes live

`discounted-cash-flow?symbol=AAPL` returned `dcf = 145.66380328033068` and `Stock Price = 319.7` on two
captures minutes apart — identical to all 14 decimal places, and `date` reads `2026-08-31`. Over the same
window the custom path's `price` moved 314.74 → 314.85 → 314.87, carrying `enterpriseValue` and
`equityValue` with it. Same nominal quantity, two different computation models.

### The two price columns disagree, in both directions

| symbol | `Stock Price` (plain) | `price` (custom) | Δ |
|---|---|---|---|
| AAPL | 319.70 | 314.87 | −4.83 |
| MSFT | 513.53 | 511.03 | −2.50 |
| KO | 89.66 | 89.19 | −0.47 |
| JPM | 357.62 | 355.89 | −1.73 |
| XOM | 156.70 | 159.20 | **+2.50** |

**This is the same failure mode already documented on `ExchangeVariant.DcfDiff`** — measured 2026-08-27,
`dcf + dcfDiff` disagreed with `price` on every row and not in a consistent direction. The finding replicates
here on a different pair of paths: do not reconstruct or reconcile a price across these two endpoints.

### Levered and unlevered are not near each other

The two models disagree by far more than they disagree with each other's price. JPM: 728.00 unlevered against
907.85 levered. KO: 83.71 against 49.77 — the levered valuation is 41% lower. Neither is "the" DCF, and an
SDK surface that lets a caller pick one without noticing the other is choosing for them.

## The custom DCF response mixes history and forecast, and does not say where the line is

Both custom paths returned exactly **10 rows** for every symbol probed, `year` descending from `2030` to
`2021`. `year` is a **JSON string**, not a number.

There is no field marking a row as actual or projected, and two fields imply different boundaries:

- `revenuePercentage` runs 0, 7.79, −2.80, 2.02, 6.43, 5.87, 5.36, 4.90, 4.47, 4.09 for 2021→2030. The
  negative value at 2023 and the jitter through 2024 look like actuals; the smooth decay from 2025 onward
  looks like a projection.
- `taxRateCash` is **constant at 16,785,417 for 2026–2030** and varies across 2021–2025 (13.3M–24.1M).

Read the first way the forecast starts in 2025; read the second it starts in 2026. **This document does not
claim which.** The SDK should surface `year` and let the caller decide, and should not invent an
`IsProjected` flag the wire does not carry.

`taxRateCash` is misnamed on the wire: it is a **cash tax amount in dollars** (13.3M–24.1M for AAPL), not a
rate. `taxRate` alongside it reads 15.61 on all ten rows. Similarly `costofDebt` is spelled with a lowercase
`o` in "of" — the only field in this group that breaks camelCase.

## Field census — crowdfunding offering, 1,000 rows

Every field populated on 1,000/1,000 unless noted.

| field | type | notes |
|---|---|---|
| `cik`, `intermediaryCommissionCik` | string | zero-padded to 10 digits on 1,000/1,000 |
| `companyName`, `nameOfIssuer` | string | 652 distinct each |
| `date`, `offeringDeadlineDate` | string | **`MM-DD-YYYY`**; deadline null on 289 |
| `filingDate`, `acceptedDate` | string | `yyyy-MM-dd HH:mm:ss` |
| `formType` / `formSignification` | string | 6 distinct each |
| `legalStatusForm` | string | 4 values: Corporation, Limited Liability Company, Limited Partnership, Other |
| `jurisdictionOrganization` | string | null on 3; 41 distinct, 2 chars |
| `issuerStateOrCountry` | string | null on 4 |
| `issuerZipCode` | string | `99999` on 990, `9999` on 5, `99999-9999` on 5 — **not an integer** |
| `issuerWebsite` | string | null on 70 |
| `intermediaryCompanyName` | string | null on 288 |
| `intermediaryCommissionFileNumber` | string | null on 288; `999-99999` |
| `compensationAmount` | **string** | null on 289. **Free prose despite the name** — e.g. "7.9% of the offering amount upon a successful fundraise, and be entitled to reimbursement…" |
| `financialInterest` | **string** | null on 298; 57 distinct, up to 256 chars; "No" is common but it is not a boolean |
| `securityOfferedType` | string | null on 289; 4 values |
| `securityOfferedOtherDescription` | string | null on 695 |
| `overSubscriptionAccepted` | **string** | `"Y"` / `"N"` — **not a boolean** |
| `overSubscriptionAllocationType` | string | null on 297; 3 values |
| `numberOfSecurityOffered` | int | 0 – 10,000,000 |
| `offeringPrice`, `offeringAmount`, `maximumOfferingAmount` | number | never null; mixed int/float on the wire |
| `currentNumberOfEmployees` | int | 0 – 320 |
| 16 × `*MostRecentFiscalYear` / `*PriorFiscalYear` | number | never null; **routinely negative** — `netIncomeMostRecentFiscalYear` is negative on **682 of 1,000** rows |

## Field census — fundraising notice, 100 rows

| field | type | notes |
|---|---|---|
| `cik` | string | zero-padded to 10 digits |
| `date` | string | **`yyyy-MM-dd`** — not the crowdfunding encoding |
| `filingDate`, `acceptedDate` | string | `yyyy-MM-dd HH:mm:ss` |
| `formType` | string | 2 values: `D`, `D/A` |
| `entityType` | string | 4 values, same vocabulary as crowdfunding's `legalStatusForm` |
| `issuerZipCode`, `relatedPersonZipCode` | string | 4- and 5-digit forms |
| `issuerPhoneNumber` | string | 3 formats measured: `999-999-9999` (33), `9999999999` (18), `999 999 9999` (8) |
| `incorporatedWithinFiveYears` | bool | **null on 30** |
| `securitiesOfferedAreOfEquityType` | bool | **null on 64** |
| `isAmendment`, `durationOfOfferingIsMoreThanYear`, `isBusinessCombinationTransaction`, `hasNonAccreditedInvestors` | bool | never null |
| `yearOfIncorporation` | string | **empty on 30, never null**; `9999` when present |
| `dateOfFirstSale` | string | **empty on 7, never null**; `yyyy-MM-dd` when present |
| `revenueRange` | string | null on 29; 5 distinct |
| `federalExemptionsExclusions` | string | comma-joined list, e.g. `"06b, 3C, 3C.1"` |
| `relatedPersonFirstName` | string | never null, but carries `"-"` and `"--"` as placeholders |
| `totalAmountSold` | int | max **13,475,150,514** — **exceeds Int32** |
| `totalOfferingAmount` | int | max 1,000,000,000 |
| `minimumInvestmentAccepted`, `totalAmountRemaining`, `salesCommissions`, `grossProceedsUsed` | int | never null, never negative in this capture |
| `findersFees` | int | **0 on all 100 rows** |
| `totalNumberAlreadyInvested` | int | 0 – 10,000 |

## Traps, and the test each one needs

1. **`MM-DD-YYYY` read as ISO yields null, silently.** Measured: `"08-28-2026"` → NULL through
   `NullableLocalDateJsonConverter`. A test must bind a crowdfunding fixture and assert the date is the
   expected `LocalDate`, so that swapping the converter back fails it.
2. **Four `date` encodings in one group.** A test must pin all four against fixtures, including
   `fundraising-search`'s datetime against `fundraising-latest`'s plain date — the same field name, the same
   three-key shape on the search paths, two different types.
3. **`crowdfunding-offerings-search.date` is null on 6.6%.** The fixture must contain a null date row.
4. **Empty string, not null.** `yearOfIncorporation` and `dateOfFirstSale` need a fixture row carrying `""`.
5. **The four sweep constants.** Four hard zeros measured against the existing `LiveApi.Cik`,
   `LiveApi.FilerCik` and `LiveApi.AcquirerNameQuery`. A pinning test must assert each new constant is used
   on its own path, in the shape of the existing crypto/forex vocabulary test.
6. **Two override vocabularies.** A test must pin which names each custom path accepts, so that adding
   `ebitdaPct` to the levered surface fails.
7. **`Stock Price` — capitalised, with a space.** Already documented for `dcf-bulk`'s CSV
   (`BulkDiscountedCashFlow.cs`); it appears in JSON here on both plain DCF paths.
8. **`totalAmountSold` exceeds Int32.** A fixture row above 2,147,483,647 pins the width.
9. **`compensationAmount`, `financialInterest`, `overSubscriptionAccepted`, `taxRateCash` are not what their
   names say** — prose, prose, `"Y"`/`"N"`, and a dollar amount respectively.

## Open questions this measurement did not settle

- **The `crowdfunding-offerings-search` matching rule.** Refuted as substring, prefix and whole-word. Not
  claimed here.
- **Where the custom DCF forecast begins.** `revenuePercentage` and `taxRateCash` imply different boundaries.
  Not claimed here.
- **Whether `findersFees` is ever non-zero.** 0 on all 100 rows measured; one capture cannot distinguish a
  constant from a sparse field.
- **Whether the 20 crowdfunding financial fields are ever null.** Never null in 1,000 rows, which is not the
  same as never null.
- **Whether `crowdfunding-offerings-latest`'s ceiling is exactly 1000 or merely ≥1000 with 1000 available.**
  `limit=5000` returned 1000, which is consistent with both.
