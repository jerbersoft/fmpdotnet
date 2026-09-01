# The `senate-net-worth-aggregated` field set — measurements

Issue #57. Probed against the live FMP API on **2026-09-01 UTC**. Every number below came from a response this
session fetched; nothing is taken from FMP's documentation.

**This is a census, not a sample.** `senate-profile` enumerates every member FMP knows — 535, measured to
exhaustion on 2026-08-29 and again here — and every one of the 535 was asked. The figures describe the whole
of what this path holds today, which is the one thing three earlier samples of it could not do.

The API key travels in the query string, so no built URL appears in this document and no capture was kept in
the repository. The one fixture the implementation adds is assembled from response bodies, which carry no key.

## The correction that matters

`SenateNetWorthSummary` has been counted three times, and each count was drawn from a sample:

| measured on | members | rows | keys found | source |
|---|---|---|---|---|
| 2026-08-29 | 1 (`H000601`) | 6 | **16** | the model, as shipped |
| 2026-09-01 | 25 | 119 | **25** | issue #57 |
| 2026-09-01 | **535 — all of them** | **3,425** | **27** | this document |

Each sample was read correctly, and each conclusion was wider than its sample. The issue's own warning — *"25
members is a bigger sample than one, and still a sample"* — was right about itself: the population holds two
keys its 25 members never showed, `spousalIncome` and `investmentAndCapitalGains`.

So the model is missing **eleven** fields, not nine, and the row shape is per-member: every row of a given
member carries the same key set — checked on all 455 members with rows, and not one has two shapes — and that
set is the categories the member has ever disclosed. `H000601` disclosed exactly the sixteen the model has,
which is why one member looked like a whole.

## The census

| | |
|---|---|
| members enumerated from `senate-profile` | 535 (pages 0, 1, 2 → 500, 35, 0) |
| requests to `senate-net-worth-aggregated` | 535, **all HTTP 200** |
| members answering at least one row | **455** |
| members answering `[]` | 80 — a 200 with an empty array, not an error |
| rows | **3,425** |
| reporting years | 2013 through 2024 |
| rows per member | 1 to 12 |

**Deterministic.** `G000581`, `M001157` and `H000601` were re-fetched after the census and each body was
SHA-256 identical to the first fetch.

## The 27 keys

Three of the 27 are the envelope — `senateID`, `year`, `total` — present on all 3,425 rows. The other 24 are
money categories, and no category is present on every row.

### The 16 the model has

| key | rows carrying it (of 3,425) |
|---|---|
| `senateID`, `year`, `total` | 3,425 |
| `cashAndCashEquivalents` | 3,087 |
| `mutualFundsAndETFs` | 2,954 |
| `realEstateLiabilities` | 2,860 |
| `pensionAndRetirementAssets` | 2,561 |
| `realEstate` | 2,247 |
| `ownershipInterest` | 2,239 |
| `stock` | 2,205 |
| `salaryAndWages` | 2,033 |
| `otherAssets` | 1,712 |
| `governmentSecurities` | 1,104 |
| `revolvingAndCreditLines` | 1,064 |
| `trusts` | 1,007 |
| `businessLiabilities` | 221 |

### The 11 it does not

| key | rows carrying it | members carrying it | rows where it is **non-zero** | issue #57's count (of 119) |
|---|---|---|---|---|
| `Other` | **2,552** | 312 | **518** | 81 |
| `pensionAndRetirementIncome` | 1,193 | 124 | 4 | 51 |
| `businessAndSelfEmployment` | 1,118 | 134 | 0 | 70 |
| `personalLiabilities` | 777 | 90 | **280** | 12 |
| `educationLiabilities` | 462 | 67 | **306** | 5 |
| `otherLiabilities` | 342 | 36 | 98 | 16 |
| `otherIncome` | 341 | 36 | 0 | 21 |
| `spousalIncome` | 153 | 16 | 0 | — |
| `investmentAndCapitalGains` | 100 | 11 | 0 | — |
| `options` | 66 | 11 | 12 | 19 |
| `assetBackedSecurities` | 42 | 5 | 12 | 12 |

`Other` — capital `O`, the only key on this path that is not camelCase — is on three-quarters of all rows. The
last column is why the sample ranked the eleven differently: `educationLiabilities` looked rare at 5 of 119
and is non-zero on 306 rows of the population, the second-most consequential of the eleven.

**Four of the eleven are never non-zero, and a fifth nearly so.** `businessAndSelfEmployment`, `otherIncome`,
`spousalIncome` and `investmentAndCapitalGains` are zero on every row that carries them, and
`pensionAndRetirementIncome` is non-zero on four. They are income categories, and this is a net-worth path —
see *`total` reconciles* below.

## What the eleven cost

| | |
|---|---|
| rows carrying at least one of the eleven | **3,130 of 3,425** — 91% |
| rows losing a **non-zero** amount | **1,054** — 31% |
| members with at least one such row | **266 of 455** |

The five largest single-row losses, each the sum of the dropped keys on that row:

| member | year | `total` | dropped |
|---|---|---|---|
| `M001157` | 2020 | 822,320,014 | **460,267,509** |
| `W000821` | 2015 | 167,834,003 | 118,001,002 |
| `I000056` | 2014 | −137,500,001 | 87,500,001 |
| `I000056` | 2013 | −87,500,001 | 87,500,001 |
| `W000816` | 2023 | 117,407,560 | 54,500,003 |

The issue's two examples hold in the census: `K000389` 2017 carries `otherLiabilities: 6000000` against a
`total` of −73,000, and `H001082` 2019 carries `Other: 4000001` against a `total` of −4,000,001 with every
modelled field at zero.

## `total` reconciles as assets minus liabilities, and `Other` has two signs

`total` is FMP's figure and the SDK passes it through. But whether the parts *can* reproduce it is the sharpest
test of whether the field set is complete, so it was run on every row. The 24 money keys split by name into
eleven assets, six liabilities, six income categories, and `Other`:

| group | keys |
|---|---|
| assets | `cashAndCashEquivalents`, `mutualFundsAndETFs`, `pensionAndRetirementAssets`, `realEstate`, `ownershipInterest`, `stock`, `governmentSecurities`, `otherAssets`, `trusts`, `options`, `assetBackedSecurities` |
| liabilities | `revolvingAndCreditLines`, `businessLiabilities`, `realEstateLiabilities`, `personalLiabilities`, `educationLiabilities`, `otherLiabilities` |
| income | `salaryAndWages`, `businessAndSelfEmployment`, `pensionAndRetirementIncome`, `otherIncome`, `spousalIncome`, `investmentAndCapitalGains` |
| unplaced | `Other` |

Testing `total == Σ assets − Σ liabilities`, income excluded:

| field set | rows reconciling exactly (of 3,425) |
|---|---|
| today's 16 | 2,379 — 69% |
| all 27, `Other` counted as an asset | **3,153** — 92% |
| all 27, `Other` counted as a liability | 3,135 |
| all 27, `Other` allowed either sign per row | **3,381** — 98.7% |

**`Other` is a liability on some rows and an asset on others.** It is non-zero on 518 rows: on 246 of them
`total` reconciles only with `Other` added, on 228 only with `Other` subtracted, and on 44 with neither. Those
44 are the whole of the residual — **every row on which `Other` is zero reconciles exactly, 2,907 of 2,907.**
So with all 27 fields the parts reproduce `total` everywhere except inside `Other`, and the model cannot say
which sign a given `Other` carries. The SDK does not attempt to.

**Income does not enter `total`.** The six income keys sum to zero on 3,421 rows; the four exceptions are all
`pensionAndRetirementIncome` (`J000288` 2019, `S001145` 2018 and 2019, `S001183` 2016). `salaryAndWages`, the
one income key the model already has, is present on 2,033 rows and **zero on every one of them**. Its doc
comment calls it "salary and wage income", which is its name and not its behaviour.

## Value types

- **Every one of the 25 numeric keys is a JSON number on every row it appears on.** `senateID` is a string,
  `year` a number, and the 24 categories plus `total` are numbers — no nulls, no strings, no nested objects,
  3,425 of 3,425 rows. This is what licenses `decimal?` on all 24 typed categories.
- **No category value is negative anywhere.** 1,283 of the 3,425 `total`s are; none of the parts. Liabilities
  are sent positive and subtracted.
- **Largest magnitude:** 1,113,129,615 — comfortably inside `decimal`.
- **18 of the 25 numeric keys appear with a decimal point somewhere in the population.** The 08-29 measurement
  saw 8 of 14 flip across six rows. The seven that never flip — `salaryAndWages`, `businessAndSelfEmployment`,
  `otherIncome`, `spousalIncome`, `investmentAndCapitalGains`, `options`, `assetBackedSecurities` — include the
  five that are never non-zero, which is the same lesson as before: an integral sample says nothing about the
  next row.
- **No two of the 27 keys collide under case-insensitive comparison**, which matters because
  `FmpJsonContext` sets `PropertyNameCaseInsensitive = true`.

## The sibling is clean, and now at population scale

Issue #57 checked `senate-net-worth` across 8 members. The same 535 were asked here.

| | |
|---|---|
| rows | **67,801** |
| distinct keys | **17** — exactly the 17 `SenateNetWorthLine` models |
| members answering `[]` | 80 — **the same 80** as the aggregated path |

The path was walked twice — once for `section` and `category`, once for the key set. Nine of the 535 requests
in the second walk answered nothing and were retried individually; the retries answered, and the per-member row
counts of the two walks reconcile exactly (66,344 + 1,457 = 67,801). The HTTP code of the nine was not
captured, so they are recorded as transient rather than explained.

Two existing doc claims on `SenateNetWorthLine` were made on 250 rows and now hold on 67,801:

| key | types seen | the claim it confirms |
|---|---|---|
| `incomeRange` | `null`, object, **string** | the empty-string case `NetWorthRangeJsonConverter` exists for |
| `valueRange` | `null`, object — **never a string** | why `ValueRange` carries no converter |
| `income`, `value` | `null`, number | |
| `debtDetails` | `null`, object | |
| `year` | number | |
| `filingDate`, `formType`, `link`, `section`, `senateID` | string, never null | |
| every other key | `null`, string | |

## FMP normalises 266 free-text categories into 24 buckets

The line path's `category` is the filer's own wording. Across the 67,801 rows it takes **266 distinct values**
in three sections — Asset 55,810 rows, Liabilities 7,818, Income 4,173 — and the aggregated path folds those
into the 24 money keys above. `Stocks`, `Stock`, `stocks`, `Corporate Securities`, `Corporate Stock` and
`Stocks & Other Securities` are six of the 266 `category` values in the Asset section; the aggregated path has
one `stock` key. Which of the 266 land in which bucket was not measured — only that 266 go in and 24 come
out.

That mapping is FMP's, it is not published, and adding a bucket to it is a change on their side that no
sample here would see coming. **It is why the design adds a catch-all rather than stopping at eleven typed
fields.** The bucket vocabulary is not drifting with time — every one of the eleven spans 2013 through 2024 —
but it is FMP's to extend.

## The live sweep cannot see this defect

`LiveApi.SenateId` is `H000601`, chosen on 2026-08-29 because he answers all three `senateID`-keyed paths. He
is also the member whose six rows established the 16-field record, and **his rows carry exactly the 16 modelled
keys and none of the eleven.** A sweep that records which properties bind would stay green through this defect
for as long as that constant stands.

Members whose rows carry the most of the eleven:

| member | rows | of the eleven | which |
|---|---|---|---|
| **`G000581`** | 8 | **7** | `Other`, `businessAndSelfEmployment`, `educationLiabilities`, `otherIncome`, `otherLiabilities`, `pensionAndRetirementIncome`, `personalLiabilities` |
| `C001061` | 12 | 6 | adds `investmentAndCapitalGains` |
| `K000375` | 11 | 6 | adds `assetBackedSecurities` |
| `M001160` | 11 | 6 | adds `options` |
| `Q000023` | 11 | 6 | adds `spousalIncome` |

**No member carries all 27 keys.** `G000581`'s 21 is the most, and he lacks `salaryAndWages` and
`businessLiabilities` of the modelled sixteen — both of which `H000601` carries — as well as `options`,
`assetBackedSecurities`, `spousalIncome` and `investmentAndCapitalGains`. `S000344` carries 20 and lacks only
`businessLiabilities` of the sixteen, but only five of the eleven, and not `educationLiabilities`.

None of `G000581`, `K000375` or `Q000023` answers a row on `senate-trades-by-id` (0, 0, 0; and 250, 250, 99
on `senate-net-worth`). So the existing constant cannot simply be pointed at one of them without emptying the
trade probe, which is the "`rows 0` baseline that matches itself green forever" the constant's own doc warns
about.

**A fixture covering all 27 keys needs five members' rows, and a sixth makes it a better fixture:** `G000581`
for seven, plus one row each from `K000375` (`assetBackedSecurities`), `M001160` (`options`), `Q000023`
(`spousalIncome`) and `C001061` (`investmentAndCapitalGains`). Verified: that union is exactly the eleven.
`S001145`'s 2018 row is added for `pensionAndRetirementIncome` — one of its four non-zero rows in the
population, and a decimal-point value, `289473.83`, where every other member's rows carry a zero.

## What was not measured

- **Which sign `Other` carries on a given row.** The line path's `section` might predict it — an `Other`
  liability line versus an `Other` asset line — but joining the two paths per row was not attempted. The SDK
  reports `Other` as sent.
- **Whether a string can arrive on this path.** No row sent one. The design's catch-all is typed for the case
  anyway, and the reason is measured on the sibling rather than here: the sibling's envelope fields are
  strings, and they are the likeliest thing to be copied across.
- **A House equivalent.** There is no `house-net-worth` path in the endpoint inventory to compare against.
- **The 80 empty members.** Whether they have filed nothing, or FMP has not ingested them, was not
  distinguished; they are the same 80 on both paths, which is consistent with either.
