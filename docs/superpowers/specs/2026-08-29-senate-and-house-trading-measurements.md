# Senate and House trading — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-29** across three probe passes, **40 captured responses**, all ordinary JSON endpoints. No `*-bulk`
path was touched.

Issue [#31](https://github.com/jerbersoft/fmpdotnet/issues/31) lists twelve paths. All twelve were probed.
Where a claim rests on a single response, the row count is given so the claim can be read at its real strength.

## Entitlement — all twelve are reachable

No path returned 402. Six answered 200 with no parameters at all; the other six returned 400 naming the one
parameter they wanted, which is itself proof of reachability.

| path | bare call |
|---|---|
| `stable/house-latest` | 200, 100 rows |
| `stable/house-trades-by-id` | 200, 100 rows |
| `stable/senate-latest` | 200, 100 rows |
| `stable/senate-trades-by-id` | 200, 100 rows |
| `stable/senate-positions` | 200, 300 rows |
| `stable/senate-profile` | 200, 500 rows |
| `stable/house-trades` | 400 — `symbol` |
| `stable/senate-trades` | 400 — `symbol` |
| `stable/house-trades-by-name` | 400 — `name` |
| `stable/senate-trades-by-name` | 400 — `name` |
| `stable/senate-net-worth` | 400 — `senateID` |
| `stable/senate-net-worth-aggregated` | 400 — `senateID` |

Every 400 carried the same wording as the previous slices: `Query Error: Invalid or missing query parameter -
<name>`.

## The worst trap in this slice: `-by-id` does not take `id`

`house-trades-by-id` and `senate-trades-by-id` are named for a parameter they do not accept. They take
**`senateID`**. The natural guess is not rejected — it is **silently ignored**, and the endpoint answers 200
with the unfiltered latest feed.

| call | rows | whose trades |
|---|---|---|
| `house-trades-by-id` (bare) | 100 | **21 different members** |
| `house-trades-by-id?id=M001217` | 100 | byte-identical to the bare call — the parameter is discarded |
| `house-trades-by-id?senateID=M001217` | 100 | one member, `M001217` only |

The senate path behaves identically: `?id=` byte-identical to bare, `?senateID=M001243` returns only
`M001243`. A caller who guesses `id` gets a hundred plausible, well-formed rows belonging to people they did
not ask about, with no error anywhere. This is the same class of silent green that `FilerCik` was named for in
the #36 slice, and it must be closed by the facade: the SDK's parameter is named for what the wire wants.

## Row order is not stable between calls

`house-trades-by-name?name=Pelosi` and `?name=Nancy Pelosi` were issued seconds apart and returned **the same
142 rows in a different order** — 104 of 142 positions differ, and sorting both makes them equal. Nothing in
this group may be tested by index against live data, and the live sweep must assert on counts and sets rather
than on `rows[0]`.

## `-by-name` matches the last name

| call | rows |
|---|---|
| `name=Pelosi` | 142, all `P000197`, all `lastName` = Pelosi |
| `name=Nancy Pelosi` | 142, same member, same set |
| `name=Zach` (a first name) | 0 |
| `name=Nunn` | 0 |

**The two empty results are real data, not a broken lookup.** Zach Nunn appears in `senate-profile` as
`N000193`, a sitting Representative, and has no disclosed trades. Checking that before calling the parameter
form wrong is the only reason this table says "matches the last name" rather than "rejects full names".

## Caps and paging differ per path, and `limit` is not always honoured

| path | default | `limit` | paging | total |
|---|---|---|---|---|
| `house-latest` | 100 | honoured to **250**; `limit=1000` and `limit=5000` both return 250 | `page=1` returns 100 more | not enumerated |
| `senate-positions` | 300 | **ignored** — `limit=500` returns 300 | `page=1` returns 300 more, **zero overlap** | at least 600 |
| `senate-profile` | 500 | **ignored** — `limit=1000` returns 500 | `page=1` returns 35, `page=2` returns 0 | **exactly 535** |
| `senate-net-worth` | 250 | **ignored** — `limit=1000` returns 250 | not probed | 250 for `H000601` |

`senate-profile` is the only path in the group whose universe was enumerated to exhaustion.

## Five record shapes, not twelve

Eight of the twelve paths return the same congressional-trade row. The other four are each their own shape.

| shape | paths | fields |
|---|---|---|
| congressional trade | `house-latest`, `house-trades`, `house-trades-by-id`, `house-trades-by-name`, `senate-latest`, `senate-trades`, `senate-trades-by-id`, `senate-trades-by-name` | 16 (15 on `senate-latest`) |
| position | `senate-positions` | 8 |
| profile | `senate-profile` | 10 |
| net worth line | `senate-net-worth` | 17, two of them nested objects |
| net worth aggregate | `senate-net-worth-aggregated` | 16 |

### `senate-latest` is the one trade feed missing a field

`capitalGainsOver200USD` is present on every row of all seven other trade feeds and on **none** of
`senate-latest`'s.

| feed | rows | carry `capitalGainsOver200USD` |
|---|---|---|
| `house-latest` | 100 | 100 |
| `house-trades-by-id` | 100 | 100 |
| `house-trades?symbol=AAPL` | 100 | 100 |
| `house-trades-by-name?name=Pelosi` | 142 | 142 |
| `senate-trades-by-id` | 100 | 100 |
| `senate-trades?symbol=AAPL` | 100 | 100 |
| `senate-trades-by-name?name=McCormick` | 145 | 145 |
| **`senate-latest`** | **100** | **0** |

One nullable property on one shared record covers all eight; `senate-latest` will simply always leave it null.
That is a documented asymmetry, not a second record.

## Numeric typing — the fields that would have been typed `int`

Two fields flip between bare-integer and decimal-point representation **within a single response**, which is
exactly the failure the typing convention in `CONTRIBUTING.md` now warns about.

| field | path | values | written with a decimal point |
|---|---|---|---|
| `yearsInTerm` | `senate-positions` | 300 | **34** |
| `yearsActive` | `senate-profile` | 500 | **493** |

`yearsInTerm` is the dangerous one: 266 of its 300 values are bare integers, so a smaller sample could easily
have seen none of the 34 and typed it `int`. Both are `decimal?`.

The same flip runs through `senate-net-worth-aggregated`, where **6 of its 14 money fields** changed
representation across only six rows:

| field | float representation |
|---|---|
| `stock` | 5 of 6 rows |
| `total`, `businessLiabilities`, `mutualFundsAndETFs`, `governmentSecurities` | 3 of 6 |
| `ownershipInterest`, `realEstate` | 2 of 6 |
| `cashAndCashEquivalents` | 1 of 6 |
| `revolvingAndCreditLines`, `salaryAndWages`, `realEstateLiabilities`, `otherAssets`, `pensionAndRetirementAssets`, `trusts` | 0 of 6 |

The last row is the trap, not the exemption: six rows all landing on bare integers says nothing about the
seventh. Every money field on this record is `decimal?`.

## `debtDetails` carries three JSON types on one field

`senate-net-worth.debtDetails` is a nested object, null on 150 of 250 rows, with four keys:

| key | JSON types observed |
|---|---|
| `dateIncurred` | `string` |
| `source` | `string` |
| `points` | `int`, `string` |
| `rate` | `float`, `int`, `string` |

`rate` arriving as any of three types — including the placeholder `"-"` — is not something `decimal?` survives
on its own. This needs either a tolerant converter or `string?`, and the design must choose deliberately.

## `value` is the midpoint of `valueRange`

On `senate-net-worth`, where both are present, `value` equals `(valueRange.min + valueRange.max) / 2` on
**214 of 214 rows, failing on none**. Both are null together on the other 36. `valueRange` is a nested object
of two `int` keys, `min` and `max`.

This is why the `.5` endings appear throughout the group: `$1,000,001 - $5,000,000` becomes `3000000.5`.

## `capitalGainsOver200USD` is a string, not a boolean

It arrives as `"False"` — a JSON string. Only `"False"` was observed across the sample, so **the vocabulary is
not established** and no claim is made here about how `true` is spelled. `senate-profile.active`, by contrast,
is a real JSON `true`. The slice therefore contains both a string-boolean and a genuine boolean, and they must
not be modelled the same way.

## Empty strings are pervasive, and they are not nulls

| field | path | empty-string rows |
|---|---|---|
| `comment` | `house-latest` | 100 of 100 |
| `comment` | `senate-latest` | 100 of 100 |
| `owner` | `house-latest` | 54 of 100 |
| `district` | `house-latest` | 28 of 100 |
| `symbol` | `house-latest` | 3 of 100 |
| `owner` | `senate-latest` | 2 of 100 |

`comment` was empty on every row measured. `senateID` is the only field in the trade shape that arrives as a
JSON `null` (2 of 100 on `house-latest`). The design has to rule on whether an empty string becomes `null`,
against the standing convention that `null` means "an answer FMP gave".

## Vocabularies

| field | path | distinct values |
|---|---|---|
| `type` | trade feeds | `Exchange`, `Purchase`, `Sale` |
| `assetType` | `house-latest` | `Corporate Bond`, `Cryptocurrency`, `ETF`, `REIT`, `Stock`, `Stock Option` |
| `assetType` | `senate-latest` | `Corporate Bond`, `ETF`, `Mutual Fund`, `REIT`, `Stock`, `Stock Option` |
| `owner` | trade feeds | `""`, `Joint`, `Self`, `Spouse` |
| `party` / `latestParty` | positions, profile | `Democrat`, `Republican`, `Independent` |
| `position` / `latestPosition` | positions, profile | `Representative`, `Senator`, `Vice President` |
| `section` | `senate-net-worth` | `Asset`, `Income`, `Liabilities` |
| `formType` | `senate-net-worth` | `Annual Report`, `Candidate Report` |
| `owner` | `senate-net-worth` | `Child`, `Joint`, `Self` |

**`assetType` is measured, not closed.** The two feeds disagree — `Cryptocurrency` appears only on the House
side and `Mutual Fund` only on the Senate side — so the union of seven is a floor, not a vocabulary. The
standing convention puts an enum where *FMP takes* a fixed vocabulary; these are response values, and an
unknown one arriving later must not cost the caller the response.

## `amount` is a bracketed range string

Seven distinct values across both latest feeds, never a number:

`$1,001 - $15,000`, `$15,001 - $50,000`, `$50,001 - $100,000`, `$100,001 - $250,000`, `$250,001 - $500,000`,
`$500,001 - $1,000,000`, `$1,000,001 - $5,000,000`.

Congressional disclosure reports a band rather than a figure, so there is no exact amount to model. Whether
the SDK also exposes parsed bounds is a design question, and `senate-net-worth` already shows FMP's own answer
to it — `valueRange` plus a midpoint.

## `senateID` names House members too

`house-latest` carries `senateID` for Representatives — FMP's naming, not a mistake in the capture. It is the
Bioguide identifier (`M001217`, `P000197`), it is the key `-by-id` filters on, and it is the only field in the
trade shape that arrives as a JSON `null` (2 of 100 rows).

## Nulls on `senate-net-worth`, over 250 rows

| field | null rows |
|---|---|
| `debtDetails` | 150 |
| `income` | 101 |
| `incomeRange` | 100 |
| `incomeType` | 87 |
| `valueRange`, `value` | 36 each |
| `name` | 13 |
| `comment` | 5 |
