# The `senate-net-worth-aggregated` field set — design

What issue [#57](https://github.com/jerbersoft/fmpdotnet/issues/57) fixes: `SenateNetWorthSummary` models 16 of
the 27 keys FMP sends on `stable/senate-net-worth-aggregated`, so eleven categories are dropped without a
trace on 91% of rows, and `Total` stops reconciling on 31% of them. No new endpoint, no change to the coverage
count.

Every fact this document argues from was measured against the live API on 2026-09-01 — across **all 535
members**, not a sample — and is recorded in
[the measurements](2026-09-01-senate-net-worth-fields-measurements.md). Nothing here was read from FMP's
documentation.

## The decision

**Add the eleven as typed `decimal?` properties, and add a catch-all for anything the type does not name.**

The eleven typed fields fix what was measured: with them, the type matches the population key-for-key, and
`total` reconciles as assets minus liabilities on 3,381 of 3,425 rows instead of 2,379. The catch-all fixes
what cannot be measured: the 24 buckets are FMP's undocumented normalisation of **266 free-text disclosure
categories**, and adding a 25th is a change on their side that no census here would see coming. Three
successive counts of this type — 16, 25, 27 — were each drawn from a sample and each wrong, and the population
count is only a sample of tomorrow.

The two together mean the common case stays typed and the uncommon case stays *visible*: a key this type does
not know lands in a dictionary instead of vanishing.

### Why not the two alternatives

- **The eleven fields only.** Rejected. It is the cleanest change and the same trap: the twenty-eighth key is
  dropped silently, exactly as the eleven were, and this path has now demonstrated three times that its shape
  is wider than any sample of it. The failure mode is the worst kind — a `Total` that looks right and is not.
- **One dictionary in place of the 24 money fields**, the `RevenueSegmentation` shape. Rejected. It cannot
  drop anything, and it breaks every caller of the thirteen shipped money properties to gain that, on a
  vocabulary that has been stable across every year from 2013 to 2024. The typed fields earn their place; the
  dictionary is for what they cannot cover.

## The catch-all holds `JsonElement`, not `decimal`

This is the one place the design departs from the obvious choice, and the reason is measured rather than
theoretical.

A `Dictionary<string, decimal>` throws on a non-numeric value, and a throw inside a row costs the **whole
response** — every row for that member — not the field. So the type of the catch-all is decided by what an
unmodelled key is most likely to be. It is not a twenty-fifth money bucket. The likeliest addition to this
path is an envelope field copied from its sibling: `senate-net-worth` carries `formType`, `filingDate` and
`link` on all 67,801 of its rows, and all three are **strings**. Under a `decimal` catch-all, the day FMP adds
one of those here, every call to `GetNetWorthSummaryAsync` throws — a partial silent loss replaced by a total
outage, which is worse than the defect being fixed.

`IReadOnlyDictionary<string, JsonElement>` cannot fail on anything FMP sends. It costs the caller one
`.GetDecimal()` on a key they have not measured, and one `.ValueKind` check if they are careful. It is what
`AsReportedStatement.Data` already uses, for the same reason.

The 24 typed categories stay `decimal?`. Those *are* measured numeric — a JSON number on every one of the 3,425 rows
that carries them, never null, never a string — and `RevenueSegmentation`'s reasoning applies to them: a `stock` that arrives as
a string is a defect worth hearing about, not one to read as zero.

The property is named **`UnmappedFields`**, not `UnmappedCategories`, because the argument above says it may
hold envelope fields rather than categories.

## The mechanism

A hand-written `SenateNetWorthSummaryJsonConverter`, applied to the type with `[JsonConverter]`, modelled on
`FinancialReportJsonConverter` — which already splits a flat object into named scalars plus everything else,
and whose doc explains why that is hand-written rather than `[JsonExtensionData]`: the attribute demands a
public, mutable `Dictionary<string, JsonElement>` on a record whose other collections are read-only.

The converter walks the object one property at a time. A name found in its table of the 27 wire names binds
the matching typed member; any other name goes, with its value as a `JsonElement`, to `UnmappedFields`. Three
rules bind it:

1. **Named members bind exactly as they would under `FmpJsonContext`.** The context sets
   `PropertyNameCaseInsensitive = true` and `AllowReadingFromString`, and a hand-written converter bypasses
   both, so the converter re-implements them: a name matches regardless of case, a money field reads a JSON
   number or a numeric string, and a null reads as `null`. A mismatch — a string that is not a number, an
   object where a number belongs — throws `JsonException`, as the generated binder would. The point is that no
   caller can tell from the typed members that a converter is present.
2. **Only keys the type does not name reach `UnmappedFields`.** Its keys keep FMP's spelling and are compared
   ordinally, so a caller reads `row.UnmappedFields["newBucket"]` with the name as it appeared on the wire.
   Never `null`: an object with no unrecognised keys binds an empty dictionary, and every row measured today
   binds an empty one.
3. **The name table is the binding, and the `[JsonPropertyName]` attributes are documentation.** The
   attributes stay on every property, because they are what a reader looks at. A test asserts the two agree,
   so they cannot drift.

The write path mirrors the read path — the named members that are non-null, then the unmapped fields — so a
row survives a round trip. Null members are skipped on write because absence and null bind identically on
read.

A type-level converter also settles a detail `AsReportedStatement.Data` had to work around: the source
generator emits the value-converter path for a type carrying `[JsonConverter]`, so `UnmappedFields` can be a
plain auto-property with a `= Empty` initialiser, as `FinancialReport.Sections` is. `FmpJsonContext` needs no
new registration — `List<SenateNetWorthSummary>` is already there, and the element's converter resolves from
the attribute. The test that proves this is the one that deserialises an unknown key through
`FmpJsonContext.Default.ListSenateNetWorthSummary` and finds it in the dictionary.

## The result type

`SenateNetWorthSummary` stays a `sealed record`, and every change is additive.

| member | wire name | type | note |
|---|---|---|---|
| existing 16 | unchanged | unchanged | |
| `Other` | `Other` | `decimal?` | capital `O` on the wire; on three-quarters of rows; **either sign** — see below |
| `BusinessAndSelfEmployment` | `businessAndSelfEmployment` | `decimal?` | income; zero on every row measured |
| `PensionAndRetirementIncome` | `pensionAndRetirementIncome` | `decimal?` | income; non-zero on four rows |
| `OtherIncome` | `otherIncome` | `decimal?` | income; zero on every row measured |
| `SpousalIncome` | `spousalIncome` | `decimal?` | income; zero on every row measured; **absent from the issue's sample** |
| `InvestmentAndCapitalGains` | `investmentAndCapitalGains` | `decimal?` | income; zero on every row measured; **absent from the issue's sample** |
| `Options` | `options` | `decimal?` | asset |
| `AssetBackedSecurities` | `assetBackedSecurities` | `decimal?` | asset |
| `PersonalLiabilities` | `personalLiabilities` | `decimal?` | liability; non-zero on 280 rows |
| `EducationLiabilities` | `educationLiabilities` | `decimal?` | liability; non-zero on 306 rows |
| `OtherLiabilities` | `otherLiabilities` | `decimal?` | liability |
| `UnmappedFields` | — | `IReadOnlyDictionary<string, JsonElement>` | **new.** Never null; empty on all 3,425 rows today |

**`Other` is documented as two-signed and left as sent.** It reconciles as an asset on 246 rows and as a
liability on 228, and the SDK cannot tell which from the row. Its doc says so, and says that every row on
which it is zero reconciles exactly — so a caller reconstructing net worth knows precisely where the
uncertainty lives.

**The income fields are documented as zero.** The six income categories sum to zero on 3,421 of 3,425 rows;
`SalaryAndWages`, already shipped, is zero on all 2,033 rows that carry it. They are present because the
member disclosed that section, and they do not enter `total`. The five new ones say so; `SalaryAndWages`'s doc
is corrected to say so.

**One wart, stated rather than hidden.** A dictionary member on a `record` compares by reference, so two
`SenateNetWorthSummary` rows that are byte-identical on the wire are no longer `==`. `AsReportedStatement` and
`RevenueSegmentation` already ship this way. Nothing in the SDK depends on this type's equality — the calendar
walk's seam detector uses it on `Dividend` and `EarningsCalendarEntry`, not here — and the type's doc records
the loss.

## The live sweep is blind, and gets a second member

`LiveApi.SenateId` is `H000601` — the member whose six rows established the 16-field record, and whose rows
carry **none** of the eleven. The sweep records which properties bind, so it would have stayed green through
this defect for ever.

The fix is a **second constant**, not a swap. `H000601` was chosen because he answers all three
`senateID`-keyed Senate paths, and none of the candidates who carry the eleven answers a row on
`senate-trades-by-id` — pointing the shared constant at one of them would record `rows 0` on the trade probe,
the exact "matches itself green forever" baseline the constant's own doc warns about. `LiveApi` already
separates constants per endpoint for precisely this reason (`InsiderNameQuery`, `HouseNameQuery`,
`FundNameQuery`), and `Probe.cs` already dispatches `senateId` by method name.

- `LiveApi.NetWorthSummarySenateId = "G000581"` — 8 rows carrying **21 of the 27 keys**, the most of any
  member. No member carries all 27, so the choice is a trade: he carries seven of the eleven, including the
  three that are non-zero most often (`Other`, `personalLiabilities`, `educationLiabilities`), and lacks
  `salaryAndWages` and `businessLiabilities` of the existing sixteen — `H000601` carried both. The constant's
  doc names all six he lacks, so the next reader knows what the probe cannot see.
- `Probe.cs` gains the arm `"senateId" when parameter.Member.Name == nameof(GetNetWorthSummaryAsync)`.
- The baseline is re-recorded, and the diff is read against this expectation before it is accepted: seven new
  `set` lines for the seven he carries; `null` lines for `Options`, `AssetBackedSecurities`, `SpousalIncome`
  and `InvestmentAndCapitalGains`; `SalaryAndWages` and `BusinessLiabilities` flipping from `set` to `null`;
  and **`null UnmappedFields`** — the sweep's `Populated` treats an empty collection as not populated, and the
  baseline spells that `null`. That last line is the detector: the day FMP adds a bucket, it flips to `set`,
  and the diff says so.

## Documentation to correct

Each of these currently asserts something the census makes false or narrower than it need be, and each is
corrected with the measurement behind it:

1. **`SenateNetWorthSummary` class summary** (`src/FmpDotNet/Models/SenateNetWorth.cs`) — "fourteen money
   fields" becomes 24; "`H000601` answered six" becomes the population figures; gains the per-member-shape
   finding, the three-count history, and the equality wart.
2. **`SalaryAndWages`** — "salary and wage income" gains "zero on all 2,033 rows that carry it".
3. **`GetNetWorthSummaryAsync` remarks** (`src/FmpDotNet/Endpoints/CongressEndpoints.cs`) — the
   "`H000601` answered six" paragraph becomes the census, and gains a line pointing at `UnmappedFields`.
4. **`LiveApi.SenateId` remarks** (`tests/FmpDotNet.SmokeTests/LiveApi.cs`) — records that this member is
   why the aggregated probe moved to its own constant.
5. **`CongressTests` money-field test comment** — "8 of the 14 money fields" becomes 18 of 25 across 3,425
   rows.
6. **`2026-08-29-senate-and-house-trading-measurements.md`** — its shape table's "16" and its "8 of its 14
   money fields" paragraph get a correction blockquote pointing here, in the form #49 used on the #46
   measurements. The claims are kept, because they were a correct reading of six rows and deleting them would
   erase why one member looked like a whole.
7. **`NetWorthRangeJsonConverter` remarks** (`src/FmpDotNet/Serialization/ShapeConverters.cs`) — not wrong,
   but its "250 rows for one filer" and "214 rows" claims now hold on 67,801, and an upgrade from sample to
   population is worth one sentence each.

`README.md` is untouched: no endpoint is added and no count changes.

## Testing

Offline, in `CongressTests`, against a new fixture assembled from real rows of six members — `G000581`,
`K000375`, `M001160`, `Q000023`, `C001061`, `S001145` — which between them carry all 27 keys (verified: the
first five's union of unmodelled keys is exactly the eleven; the sixth supplies the one non-zero,
decimal-point `pensionAndRetirementIncome` the population offers). The existing two-row `H000601` fixture stays for the test that already
uses it.

| test | what would break it |
|---|---|
| each of the eleven new properties binds from the fixture | a wire name mistyped in the name table |
| `UnmappedFields` is empty and non-null on every fixture row | the catch-all swallowing a named key, or a null slipping through |
| an unknown **numeric** key lands in `UnmappedFields` under its wire spelling, and nowhere else | the converter not being applied through `FmpJsonContext`, or a key being dropped |
| an unknown **string** key lands in `UnmappedFields` and the response does not throw | the catch-all typed `decimal` |
| a named money field reads a numeric string | the converter bypassing `AllowReadingFromString` |
| a named money field given a non-numeric string throws `JsonException` | the converter reading garbage as zero or null |
| a named key spelled in a different case still binds to its property, not to `UnmappedFields` | the converter bypassing `PropertyNameCaseInsensitive` |
| a JSON `null` on a named money field binds `null` | a throw on null |
| the converter's name table equals the set of `[JsonPropertyName]` values on the type (reflection, test-side) | the two drifting |
| a row round-trips through `Write` then `Read` with the same typed values and the same unmapped keys | the write path omitting a member |
| the existing 16-field fixture still binds every field it did | a regression in the rewrite |

**The fixture is real rows, not synthesised ones.** The eleven are rare enough per member that a fixture
needs five members to cover them, and that is fine; what it must not be is a hand-typed object with all 27
keys set to round numbers, which would test the name table against itself.

**Live**, the baseline diff above is the assertion, and it is read rather than blindly re-recorded.

## Out of scope

- **Deriving anything.** No `AssetsTotal`, no `LiabilitiesTotal`, no reconciliation helper. The SDK passes
  through what FMP sends, and the reconciliation finding lives in the docs so a caller can do it knowingly.
- **Resolving `Other`'s sign.** Joining the line path per row might predict it; that is a separate
  investigation with its own measurement.
- **A wider catch-all convention.** `FinancialReport`, `AsReportedStatement` and now this type each carry an
  open dictionary because each was measured to need one. That is three measured cases, not a rule, and no
  other model gains one on the strength of this change.
- **Folding `SenateNetWorthSummary` into a shared shape with `SenateNetWorthLine`.** They are different
  objects and stay so.
