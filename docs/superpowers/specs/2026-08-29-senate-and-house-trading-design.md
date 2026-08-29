# Senate and House trading — design

Issue [#31](https://github.com/jerbersoft/fmpdotnet/issues/31), twelve paths, one new facade.

Every claim here rests on
[the measurements](2026-08-29-senate-and-house-trading-measurements.md) taken on **2026-08-29** across three
probe passes and 40 captured responses. Where this document says a value "was measured", the measurements file
gives the row count behind it.

**Spec authority:** where this design and the measurements disagree, the measurements win and this document is
wrong.

## Scope

Twelve paths, all reachable on the current key, none of them `*-bulk`. Congressional disclosure data: what
members of Congress traded, who they are, and — for the Senate feed only — what they are worth.

Nothing is redistributed between groups the way #30 and #36 redistributed theirs. All twelve paths belong to
one subject and one facade.

## Public surface

### `fmp.Congress` — new, 12 paths

```csharp
Task<IReadOnlyList<CongressionalTrade>> GetHouseLatestAsync(int? page = null, int? limit = null, CancellationToken ct = default)
Task<IReadOnlyList<CongressionalTrade>> GetSenateLatestAsync(int? page = null, int? limit = null, CancellationToken ct = default)

Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesAsync(string symbol, CancellationToken ct = default)
Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesAsync(string symbol, CancellationToken ct = default)

Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByMemberAsync(string senateId, CancellationToken ct = default)
Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesByMemberAsync(string senateId, CancellationToken ct = default)

Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByNameAsync(string name, CancellationToken ct = default)
Task<IReadOnlyList<CongressionalTrade>> GetSenateTradesByNameAsync(string name, CancellationToken ct = default)

Task<IReadOnlyList<CongressMemberPosition>> GetPositionsAsync(int? page = null, CancellationToken ct = default)
Task<IReadOnlyList<CongressMemberProfile>> GetProfilesAsync(int? page = null, CancellationToken ct = default)

Task<IReadOnlyList<SenateNetWorthLine>> GetNetWorthAsync(string senateId, CancellationToken ct = default)
Task<IReadOnlyList<SenateNetWorthSummary>> GetNetWorthSummaryAsync(string senateId, CancellationToken ct = default)
```

### `ByMember`, not `ById` — the naming closes a silent-green trap

`stable/house-trades-by-id` and `stable/senate-trades-by-id` are named for a parameter they do not accept.
They take **`senateID`**, and passing `id` is not rejected — it is discarded, and the endpoint answers 200 with
the unfiltered latest feed. Measured 2026-08-29: `?id=M001217` came back byte-identical to the bare call,
carrying **100 rows spanning 21 different members**.

Three decisions follow from that one measurement, and they are the reason this facade exists rather than
callers using `FmpTransport` directly:

- **The method is `…ByMemberAsync`, not `…ByIdAsync`.** A method named for `id` invites a caller to reason
  about a parameter the endpoint ignores.
- **The parameter is `senateId`**, spelled as FMP spells it, so the name a caller reads matches the name on
  the wire.
- **`senateId` is required, not optional.** The endpoint's willingness to answer without it is precisely the
  hazard; the SDK will not reproduce it. A caller who wants the unfiltered feed has `GetHouseLatestAsync`,
  which says so.

### `page` and `limit` are exposed only where they were measured to work

They are not uniform across this group, so a uniform signature would lie:

| method | `limit` | `page` |
|---|---|---|
| `GetHouseLatestAsync`, `GetSenateLatestAsync` | honoured, **capped at 250** | honoured |
| `GetPositionsAsync` | **ignored by FMP** — not exposed | honoured, 300/page |
| `GetProfilesAsync` | **ignored by FMP** — not exposed | honoured, 500/page, 535 total |
| the six filtered trade methods, `GetNetWorth*` | not probed / ignored | not exposed |

`limit` above 250 on the latest feeds is not an error and not honoured — `limit=1000` and `limit=5000` both
returned exactly 250. The XML doc records the ceiling so a caller does not conclude the data ran out.

`GetProfilesAsync` is the only method in the group whose universe was enumerated to exhaustion: page 0 gives
500, page 1 gives 35, page 2 gives 0.

## Models

Twelve paths, **five records** plus two nested ones. Eight paths share `CongressionalTrade`.

| record | paths | properties |
|---|---|---|
| `CongressionalTrade` | the eight trade paths | 16 |
| `CongressMemberPosition` | `senate-positions` | 8 |
| `CongressMemberProfile` | `senate-profile` | 10 |
| `SenateNetWorthLine` | `senate-net-worth` | 17 |
| `SenateNetWorthSummary` | `senate-net-worth-aggregated` | 16 |
| `NetWorthValueRange` | nested in `SenateNetWorthLine` | 2 |
| `NetWorthDebtDetails` | nested in `SenateNetWorthLine` | 4 |

### One trade record for eight paths, including the one that is a field short

`senate-latest` is the only trade feed that omits `capitalGainsOver200USD` — measured on 0 of its 100 rows,
against 100% on all seven others. That is one nullable property, not a second record: `senate-latest` simply
always leaves it null, and the XML doc says so on the property rather than leaving a caller to discover it.

### Nothing in this slice is an enum

`type` (3 values), `assetType` (7 across both feeds), `owner`, `party`, `position`, `section` and `formType`
all read like closed vocabularies. None of them becomes a C# enum, and the SDK has already written down why:

> *A closed C# enum over an open server-side list is a breaking change waiting for a Tuesday.*
> — `InsiderTransactionType`

The SDK's one response-side enum, `FinancialReports.Period`, earns its place on a test this slice fails: that
property's job is **to be handed back into a request**. No parameter in this group accepts `type`,
`assetType`, `party` or `position`, so an enum here would buy discoverability and pay for it with a breaking
change the first time FMP adds a value.

The measurements make that concrete rather than hypothetical: the two feeds **already disagree** —
`Cryptocurrency` appears only on the House side, `Mutual Fund` only on the Senate side. The union of seven is
a floor, not a vocabulary. All of these are `string?`, with the measured values listed in the XML docs so they
are still discoverable.

### Every quantity is `decimal?`, and two of them prove why

Two fields flip between bare-integer and decimal-point representation **inside a single response**:

| field | values | written with a decimal point |
|---|---|---|
| `senate-positions.yearsInTerm` | 300 | 34 |
| `senate-profile.yearsActive` | 500 | 493 |

`yearsInTerm` is the one that would have caught us: 266 of its 300 values are bare integers, so a smaller
sample sees no decimal point at all and types it `int`. Under `int?` those 34 rows do not merely bind wrong —
each one aborts the whole 300-row response.

The same flip runs through `SenateNetWorthSummary`, where 8 of the 14 money fields changed representation
across only six rows. The other 6 stayed integral over those six rows, and that is **not** an exemption:
six rows all landing on bare integers says nothing about the seventh. Every money field on both net-worth
records is `decimal?`.

`year` and `congressNumber` stay `int?` — whole by their own nature, which is the test `CONTRIBUTING.md` now
states.

### `debtDetails.rate` is `string?`, because parsing it would lose data

`rate` arrives as three JSON types across 100 rows. The string values are not placeholders:

```
"N/A%                        (10 years)"
"NA%                        (On Demand)"
```

64 of 100 look like that. They carry **a term as well as a rate**, so a tolerant numeric converter that binds
null on failure — the pattern `BeneficialOwnership` uses — would silently discard "10 years" and "On Demand".
The remaining 23 are numeric (1.4, 2.75, 5.25, 3).

`points` is `string?` for the same reason in reverse: 82 of 100 are `"-"`, 5 are `0`, and 13 are JSON `null`.
Mapping `"-"` to null would collapse two states FMP distinguishes.

Both properties document the measured forms so a caller can parse deliberately. This is the one place in the
slice where the SDK hands back a string it could have parsed, and the reason is that FMP has overloaded the
field rather than that the SDK is being lazy.

### `capitalGainsOver200USD` is `string?`, not `bool?`

It arrives as the JSON **string** `"False"`. Two measurements decide this:

- Only `"False"` was observed. **The spelling of the affirmative was never measured**, so a converter would be
  guessing at the value it exists to handle.
- Measured 2026-08-29 against this library's own `FmpJsonContext` options, **`bool?` throws on `"False"`** —
  `NumberHandling.AllowReadingFromString` covers numbers, not booleans. There is no free version of this.

`CongressMemberProfile.Active` is a genuine JSON `true` and is `bool?`. The slice therefore carries both a
string-boolean and a real boolean, and the two are deliberately not modelled alike.

### `amount` stays a string; `value` does not

`amount` is a bracketed band — seven distinct values, `$1,001 - $15,000` through `$1,000,001 - $5,000,000` —
and never a number. Congressional disclosure reports a range rather than a figure, so there is no exact amount
to model and none is invented.

`senate-net-worth` shows FMP's own answer to the same problem, and the SDK passes it through rather than
recomputing it: `valueRange` is `{min, max}` and `value` is their midpoint — verified on **214 of 214 rows
where both are present, failing on none**. That is where the `.5` endings across this group come from.

### Empty strings are preserved, not normalised to null

`comment` was empty on every trade row measured; `owner` on 54 of 100 House rows; `district` on 28;
`symbol` on 3.

They stay as they arrived. `senateID` is the one field in the trade shape that arrives as a JSON `null` (2 of
100), so both forms occur in the same record and mean different things — "FMP sent nothing" against "FMP sent
blank". Collapsing them would destroy a distinction the wire makes, against the standing rule that `null`
means an answer FMP gave.

### Dates

Every date field is ISO `yyyy-MM-dd` — **1,728 values across six fields, none non-conforming** — so all take
`LocalDate?` through the existing `NullableLocalDateJsonConverter`. `birthDate` ranges from 1932-12-31 to
1997-01-16, comfortably inside NodaTime's range.

`debtDetails.dateIncurred` is **not** a date: seven distinct values, all bare four-digit years (`"2003"`,
`"2010"`, …). It is `string?`, and typing it `LocalDate?` would fail on every row.

### `senateID` names House members too

`house-latest` carries `senateID` for Representatives. That is FMP's naming, not a capture error. It is the
Bioguide identifier, it is what `-by-id` filters on, and the property keeps FMP's spelling so the name a
caller reads is the name on the wire.

## Serialisation

Five records plus two nested ones are added to `FmpJsonContext` as `[JsonSerializable(typeof(List<X>))]`.
The two nested records need entries of their own; missing one fails at runtime, not at compile time.

No new converter. `NullableLocalDateJsonConverter` covers every date, and the three multi-typed fields
(`rate`, `points`, `capitalGainsOver200USD`) are `string?` precisely so that no converter is needed.

## Testing

### Row order is not stable, so nothing may be asserted by index against live data

Two calls seconds apart returned **the same 142 rows with 104 of 142 positions changed**. Fixture-bound unit
tests may index freely — a fixture is a frozen file. The live sweep may not: it asserts on counts and on set
membership, never on `rows[0]`.

### The traps that get a test each

Each fails if the trap is reintroduced:

1. **`?id=` is not the parameter.** The facade sends `senateID`; a test asserts the built query contains
   `senateID=` and no `id=`.
2. **`senateId` is required.** Passing null or blank throws before any request is made — asserted on the
   handler recording zero requests, the shape `A_quarter_outside_one_to_four_is_refused` uses.
3. **A `yearsInTerm` of `0.7` binds.** A fixture row carrying a decimal point, with whole-number rows either
   side, so reverting to `int?` fails rather than passing on the majority.
4. **`capitalGainsOver200USD` binds from `"False"`.** Fails the moment someone types it `bool?`.
5. **`senate-latest` binds with the field absent**, leaving it null and the other 15 populated.
6. **`debtDetails.rate` binds `"N/A%  (10 years)"` and `2.75` on adjacent rows.**
7. **`value` equals the midpoint of `valueRange`** on the captured fixture.
8. **`dateIncurred` binds `"2003"`** — guards against a later change to `LocalDate?`.

### Live guard

The twelve paths join the sweep with `senateId = "H000601"` (Hagerty, measured to answer 250 net-worth rows)
and `name = "Pelosi"` (142 trade rows). Both are chosen against the `FilerCik` lesson: a value that answers
zero rows records `rows 0` as its baseline and matches it green forever.

`GetHouseTradesByNameAsync` is **not** given a member with no trades. `name=Nunn` returns `[]`, and Zach Nunn
is a sitting Representative with no disclosures — real data, but useless as a guard.

## Out of scope

- **Parsing `amount` into bounds.** FMP publishes the band as text and publishes structured bounds only on the
  net-worth path. Deriving `min`/`max` from the trade string is a convenience the caller can write and the SDK
  cannot verify against anything measured.
- **Enumerating `senate-positions` to exhaustion.** Paging was measured to work with zero overlap and the
  total is at least 600; the exact figure was not established and no claim rests on it.
- **`limit` on the six filtered trade methods.** Not probed. Absent from the signature rather than exposed on
  an assumption.
