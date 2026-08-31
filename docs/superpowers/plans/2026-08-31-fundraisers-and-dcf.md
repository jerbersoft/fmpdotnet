# Fundraisers and DCF Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two facades — `fmp.Fundraisers` and `fmp.DiscountedCashFlow` — covering the last ten actionable
FMP paths, taking SDK coverage from **226 to 236 of 243**.

**Architecture:** Ten paths, ten records, one new converter. The consolidation is not the work. The work is
that **every failure in this group is silent**: a field called `date` encoded four different ways across six
records, one of them `MM-DD-YYYY` which the SDK's existing ISO converter reads as `null` without throwing;
two custom-DCF endpoints that honour two different override vocabularies and discard the other's parameters
at HTTP 200; a `cik` parameter honoured on one `-latest` path and ignored on its sibling; and three CIK/name
constants the smoke sweep already owns that return `[]` on six of these ten paths, which would record
`outcome empty` as the healthy baseline and match green for ever. Two paging guards with different constants,
one new `MM-dd-uuuu` converter, thirteen numbered trap tests, and XML documentation that says what the wire
actually does everywhere a guard cannot.

**Tech Stack:** .NET 10, C# 13, NodaTime `Instant` and `LocalDate`, source-generated `System.Text.Json` via
`FmpJsonContext`, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-31-fundraisers-and-dcf-design.md` (committed `f8d325c`)

**Measurements:** `docs/superpowers/specs/2026-08-31-fundraisers-and-dcf-measurements.md` (committed
`2be9017`, 145 captures, 13,287 rows), **plus the sixteen design-phase findings recorded in the spec's
"Measured after the measure phase" section**, plus the four widths measured on 2026-08-31 during planning and
recorded under Ruling 1 below. Every number in this plan traces to one of those three. Read the spec's
addendum table as well as the measurements file: it is where `costOfEquity`, the Eastern `acceptedDate`, the
`filingDate` midnight and the `cik` asymmetry live.

## Global Constraints

- **`TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true`** (`Directory.Build.props`, all
  projects including both test projects). A `<see cref="...">` pointing at a type that does not exist yet is
  **CS1574, a build error, not a warning**. Use the **deferred-cref pattern**: write
  `<c>FundraisingNotice</c>` while the target does not exist, and promote it to a real `<see cref>` in the
  task that creates it. **There are exactly three forward references in this plan and each has a mechanical
  demote/promote pair:**

  | written in | points at | demoted by | promoted by |
  |---|---|---|---|
  | Task 2's two crowdfunding records | `FundraisingNotice`, `FundraisingSearchHit` | Task 2 Step 5 | Task 3 Step 4 |
  | Task 2's and Task 3's search records | `FundraisersEndpoints` | written as `<c>` from the start | Task 4 Step 3 |
  | Task 5's `CustomDcfProjection.cs` | `CustomDcfAssumptions`, `CustomLeveredDcfAssumptions` | Task 5 Step 4 | Task 6 Step 5 |

  **Task 6 Step 5 proves with a grep that no deferred cref remains**, listing by name the `<c>` references
  that stay `<c>` on purpose.
- **CS1591 is not suppressed project-wide.** Every public type, member and parameter needs an XML doc
  comment. Do not add `#pragma warning disable CS1591`.
- **The assembly declares `IsAotCompatible`.** Every deserialisation goes through `FmpJsonContext`. A
  reflection-based `JsonSerializer.Deserialize` overload in `src/FmpDotNet` fails the build with
  IL2026/IL3050. (The test project has no trim analyser, so
  `JsonSerializer.Deserialize(fixture, FmpJsonContext.Default.ListCrowdfundingOffering)` there is the same
  call the SDK makes and is what every existing binding test uses.)
- **Never state a fact that was not measured.** Every number, date and behaviour in a doc comment must come
  from the measurements file, the spec's addendum, or this plan's Ruling 1, and must carry its date —
  `measured 2026-08-27` or `measured 2026-08-31`. The two dates are not interchangeable; use the one the
  finding was made on. Nearly everything in this slice is 2026-08-31.
- **Never log a built URL and never write one into a fixture.** The API key travels in the query string.
  Fixtures are response bodies only: no request URL, no host, no `apikey`. (`issuerWebsite` *inside* a
  captured body is part of the body and stays.)
- **Never `source` the `.env` file and never `set -a` it.** It has clobbered `PATH` for a whole shell before.
  Extract the one variable into the one command, exactly as Task 1 and Task 8 show.
- **Do not set `FMPDOTNET_SMOKE_BULK`.** FMP's documented warning: "Frequent abuse on this API Endpoint may
  result in restrictions placed on this API Key." No task here needs the bulk sweep.
- **Line length is 120 characters** in `src/` and `tests/` — the target this slice holds itself to, not a
  repo-wide fact. Re-measured 2026-08-31, **80 of 238** `.cs` files under `src/` and `tests/` already exceed
  it and `Models/` routinely runs to 130–290. An over-long line is a **Minor** finding against house style, not an
  Important one against repo convention.
- **Every bound property is nullable.** The measured null counts go in the XML doc rather than into the type:
  "never null in 1,000 rows" and "cannot be null" are different statements and only the first was measured.
- **Five decisions are settled and re-litigating one in code is a spec violation:** two facades not one;
  `fmp.DiscountedCashFlow` spelled out rather than `fmp.Dcf`; one assumptions record per custom-DCF path
  rather than a shared type or a long parameter list; two records for the two custom-DCF response shapes; and
  two records for the two plain DCF paths despite an identical wire shape.
- **`yearOfIncorporation` is `string?`.** This was the user's explicit ruling during the design phase,
  overriding a proposed `int?`. Do not "improve" it.
- **Nine things the SDK deliberately does not do**, each with its reason recorded in the spec: no matching
  rule claimed for `crowdfunding-offerings-search`; no actual/projected flag on custom DCF rows; no
  deduplication of search results by `cik`; no reconciliation of any price across the DCF paths; no
  `sellingGeneralAndAdministrativeExpensesPct` on either assumptions record; no `cik` on either `-latest`
  method; no bound on any assumption value; no uppercase-symbol guard on the DCF paths; no page ceiling on
  either `-latest` method.

---

## Six rulings carried into this plan

### 1. Every numeric on the eight response records is `decimal?`, not `long?` and not `int?`

The spec says "**`totalAmountSold` is `long?`.** Measured max 13,475,150,514, which overflows Int32." The
argument it makes is against `int?`, and it is right. `long?` is the wrong answer to it.

Measured 2026-08-31 during planning, over every capture in the slice:

| corpus | rows | finding |
|---|---|---|
| fundraising notices | **406** | all eight amount fields whole on 406 of 406; `totalAmountSold` max **13,475,150,514**, `totalOfferingAmount` max 1,000,000,000, `totalNumberAlreadyInvested` max 10,000, `findersFees` **0 on every row** |
| crowdfunding offerings | **3,656** | `offeringPrice` **fractional on 884**, `maximumOfferingAmount` on 482, `offeringAmount` on 579, and every one of the eighteen fiscal-year fields fractional on 56–339; `netIncomeMostRecentFiscalYear` min **−27,665,487** |
| custom DCF, unlevered | **290** | `revenue` max **4.16 × 10¹⁶**, `terminalValue` max **2.07 × 10¹⁷**, `dilutedSharesOutstanding` 2,793,700,000 – 15,004,697,000 |
| custom DCF, levered | **250** | same ranges; `enterpriseValue` max **1.40 × 10¹⁷** |

So the crowdfunding money fields cannot be integral at all, and the fundraising ones merely have not been
seen fractional yet in 406 rows.

**The repo has already settled this exact question, and the argument is on
`FinancialScores.PiotroskiScore` (`src/FmpDotNet/Models/FinancialScores.cs:78-93`):** `int` "costs nothing
until the day FMP serialises this through a float, and on that day it costs the *entire response*… the throw
aborts the whole deserialisation rather than one field", with the precedent that "FMP does serialise counts
as floating point elsewhere — `profile-bulk`'s `volume` arrives as `73305.59636`." `long?` inherits that
failure mode in full. `decimal?` holds 2.07 × 10¹⁷ exactly, cannot throw on a fractional value, and is what
885 of the SDK's 1,070 numeric model properties already are; `long?` is used 7 times, all of them for share
volumes and trade sizes measured never fractional.

**Ruling: every numeric property on `CrowdfundingOffering`, `FundraisingNotice`, `DcfValuation`,
`LeveredDcfValuation`, `CustomDcfProjection` and `CustomLeveredDcfProjection` is `decimal?`. The single
exception is `Year` on the two custom-DCF projections, which stays `int?` as the spec says**, because the
wire sends it as a JSON *string* (`"2030"`) and the context's `AllowReadingFromString` binds it — a quoted
value cannot arrive as `9.0`. **Cost if wrong:** a caller who wants an integral count writes `(long)value`,
a small annoyance forever, against losing a whole response to one fractional cent. The spec's actual
requirement — that `totalAmountSold` must not be `int?` — is satisfied exactly, and Task 2's test still
fails if anyone types it `int?`.

### 2. `FmpRequest` is NOT modified; the assumptions records follow the `ScreenerCriteria` precedent

`FmpRequest` has `With` overloads for `string?`, `int?`, `LocalDate?` and `bool?` and **none for `decimal?`**
(`src/FmpDotNet/FmpRequest.cs:28-43`). The sixteen and ten assumption overrides are all `decimal?`.

Adding a `With(string, decimal?)` overload is the obvious move and it is not what this repo does.
`ScreenerCriteria` — the SDK's only other input record, twenty optional filters rendered onto a query — solves
it with a private generic helper (`src/FmpDotNet/ScreenerCriteria.cs:152-153`):

```csharp
private static string? Number<T>(T? value) where T : struct, IFormattable =>
    value?.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
```

and its doc records why the culture is load-bearing: "A market-cap bound formatted under a comma-decimal
culture becomes `1000000000,5` in the query string, and FMP does not reject it — an unparseable value is
treated like an unrecognised one, which on this endpoint means a silent empty result on a German or French
host and a correct one everywhere else." That failure applies here **verbatim**: the spec measured
`custom-discounted-cash-flow?symbol=AAPL&notARealParam=99` returning the baseline valuation at HTTP 200, so a
comma-decimal `beta` is silently discarded and the caller gets FMP's default assumptions back looking like
their own.

**Ruling: each assumptions record carries its own `private static string? Number(decimal? value)` and an
`internal FmpRequest Apply(FmpRequest request)`, mirroring `ScreenerCriteria.ToRequest()`. `FmpRequest.cs` is
not touched, and the spec's Files section — which does not list it — is correct as written.** **Cost if
wrong:** none; a `decimal?` overload can be added later if a third caller ever wants one.

### 3. The two harnesses that synthesise arguments both need an arm, and both get an instance rather than null

The spec says `Probe.Argument` "needs a case for `CustomDcfAssumptions` and `CustomLeveredDcfAssumptions`,
which it would otherwise throw on as unknown types. Both resolve to `null`." Two corrections:

- **There are two such harnesses, not one.** `tests/FmpDotNet.Tests/EndpointCoverageTests.cs:296` has its own
  `Argument` that also throws on an unknown type (`…cannot supply a {ParameterName} for '{name}'`). Without an
  arm there, the two custom-DCF methods drop out of the README coverage table and
  `Every_public_endpoint_method_reaches_the_api` goes red. The spec's Files section lists that file as
  modified but only for the coverage count; this is the real change it needs.
- **`null` does not fit either signature.** Both methods are declared `private/public static object Argument(…)`,
  non-nullable, and `SweepCoverageTests.cs:120-121` unboxes the result directly —
  `(NodaTime.LocalDate)Probe.Argument(…)` — which under `TreatWarningsAsErrors` becomes **CS8605 as an
  error** the moment the return type goes `object?`.

**Ruling: both harnesses gain an arm returning `new CustomDcfAssumptions()` / `new CustomLeveredDcfAssumptions()`,
following the `ScreenerCriteria` arm each file already has** (`Probe.cs:527`, `EndpointCoverageTests.cs:334`).
An all-null assumptions record writes **zero** query parameters — `FmpRequest.With(string, string?)` drops
nulls and empties — so the call that goes out is byte-identical to the one `null` would have produced, which
is what the spec asked for: the sweep baselines FMP's default valuation. **Cost if wrong:** none on the wire.
Task 8 adds a test asserting the probe's instance has every member null, so a future property with a non-null
initialiser cannot silently start sending an override.

### 4. The measurements file's crowdfunding census undercounts the fiscal-year fields by two

The census at `2026-08-31-fundraisers-and-dcf-measurements.md:349` reads "16 × `*MostRecentFiscalYear` /
`*PriorFiscalYear`". The captured key list has **eighteen** — nine pairs: `totalAsset`,
`cashAndCashEquiValent`, `accountsReceivable`, `shortTermDebt`, `longTermDebt`, `revenue`, `costGoodsSold`,
`taxesPaid`, `netIncome`. Thirty other keys plus eighteen is forty-eight, which is the count the spec, the
live captures and the Python `fmpsdk` `TypedDict` all agree on; sixteen would make forty-six.

**Ruling: the record carries eighteen fiscal-year properties. The measurements file is not rewritten** —
this plan and Task 1's doc comment record the corrected count, in keeping with the spec's own choice not to
rewrite the measurement document. **Cost if wrong:** the property count is asserted by Task 1's key-set test,
so a miscount fails immediately.

### 5. `EndpointCoverageTests.DocumentedPaths` stays at 243 and the 226 → 236 move is generated

The spec says "`EndpointCoverageTests` moves from 226 to **236** of 243 documented paths". `DocumentedPaths`
is the **denominator** and it does not change. The numerator is computed at `EndpointCoverageTests.cs:372` —
`modelled.Select(m => m.Path).Distinct().Count()` — and written into the README by the generator.

**Ruling: nobody edits a coverage number by hand. Task 7 runs `FMPDOTNET_UPDATE_README=1 dotnet test` and
commits the regenerated block, and the sentence "**236 of FMP's 243 endpoint paths are modelled**" is the
generator's output, not an assertion typed into a file. If it reads anything other than 236, that is a
missing or duplicated path in Tasks 3 and 6, not a number to correct.** **Cost if wrong:** none; the failure
is loud and immediate.

### 6. The endpoint inventory is a dated snapshot and is not rewritten

The spec's Documentation deliverables say "the endpoint inventory marked for these ten paths".
`docs/superpowers/specs/2026-08-27-endpoint-inventory.md` is not a live table. Its title says *enumerated
2026-08-27*; its `modelled` column still reads `0` for News, SEC Filings, Market Performance and six other
groups that have shipped since; it states "All 82 currently-modelled paths appear in Source A"; and its issue
table records a split made on one specific day. It has two commits in its whole history, both from the week
it was written. **Nine slices before this one left it alone**, and the News plan — working from a spec
carrying the same sentence — touched only the README prose that cites it.

**Ruling: the inventory file is not modified. Task 9 rewrites the README prose that cites it, which is where
a reader looks for current coverage and which `EndpointCoverageTests` asserts against the code on every
run.** **Cost if wrong:** a stale `modelled` column in a document whose stated purpose is to be the dated
provenance record for the 243 denominator — which is what it already is for nine other slices, so the
alternative would make it inconsistent rather than current.

---

## File Structure

**Created — `src/FmpDotNet/`**

| file | responsibility |
|---|---|
| `Models/CrowdfundingOffering.cs` | the 48-key Form C shape, shared by `crowdfunding-offerings` and its `-latest` sibling |
| `Models/CrowdfundingSearchHit.cs` | the 3-key crowdfunding search shape, whose `date` is `MM-DD-YYYY` |
| `Models/FundraisingNotice.cs` | the 43-key Form D shape, shared by `fundraising` and its `-latest` sibling |
| `Models/FundraisingSearchHit.cs` | the 3-key fundraising search shape, whose `date` is an acceptance timestamp |
| `Models/DcfValuation.cs` | `DcfValuation` **and** `LeveredDcfValuation` — identical wire shape, two types on purpose |
| `Models/CustomDcfProjection.cs` | `CustomDcfProjection` (47) **and** `CustomLeveredDcfProjection` (34) |
| `Models/CustomDcfAssumptions.cs` | `CustomDcfAssumptions` (16 inputs) **and** `CustomLeveredDcfAssumptions` (10 inputs) |
| `Endpoints/FundraisersEndpoints.cs` | six methods, two paging guards, two page-size constants |
| `Endpoints/DiscountedCashFlowEndpoints.cs` | four methods, no paging at all |

Two records per file where the pair exists only as a pair — the two plain DCF shapes, the two custom
projections, the two assumptions records. Splitting those into six files would hide the single fact each pair
exists to state: that the two are **not** interchangeable. The four filing shapes are one file each because
each is large and independently useful.

**Created — `tests/FmpDotNet.Tests/`**

| file | responsibility |
|---|---|
| `FundraisersTests.cs` | the six Fundraisers paths: four records, the two paging guards, the reflection pins |
| `DiscountedCashFlowTests.cs` | the four DCF paths: four response records, the two assumption vocabularies |
| `Fixtures/` × 10 | one captured body per path, listed in Task 1 |

**Modified**

| file | change |
|---|---|
| `src/FmpDotNet/Serialization/NodaConverters.cs` | `+ NullableMonthDayYearDateJsonConverter` (Task 1) |
| `src/FmpDotNet/Serialization/FmpJsonContext.cs` | `+ 8` `[JsonSerializable]` entries (Tasks 1, 2, 4) |
| `src/FmpDotNet/Models/ExchangeVariant.cs` | `Dcf`'s doc loses "none of them is modelled" (Task 7) |
| `src/FmpDotNet/FmpClient.cs` | `+ 2` constructor parameters and properties (Task 7) |
| `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` | `+ 2` registrations (Task 7) |
| `tests/FmpDotNet.Tests/AddFmpTests.cs` | `+ 2` `Assert.NotNull`, count `23` → `25` (Task 7) |
| `tests/FmpDotNet.Tests/EndpointCoverageTests.cs` | `+ 2` `Argument` arms (Task 7) |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | `+ 4` constants (Task 8) |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | `+ 2` name-dispatched arms, `+ 2` assumptions arms, doc figure (Task 8) |
| `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` | `+ 2` pinning tests (Task 8) |
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | regenerated (Task 8) |
| `README.md` | coverage block, remaining-paths prose, smoke statistics (Tasks 7, 8, 9) |
| `docs/superpowers/specs/2026-08-27-endpoint-inventory.md` | ten paths marked built (Task 9) |

**Not modified, and each for a measured reason**

- **`src/FmpDotNet/FmpRequest.cs`** — Ruling 2.
- **`src/FmpDotNet/FmpTransport.cs`** — the eight naked-request 400s carry a plain-text body served under
  `content-type: application/json`. Measured by reading the code path: `text[0]` is `Q`, so neither the `{`
  branch nor the `[` branch is taken and `message ??= text` passes the sentence through verbatim.
- **`tests/FmpDotNet.SmokeTests/Sweeps.cs`** — the sweep is reflection-driven. `Probe.Groups()` walks
  `FmpClient`'s public properties and `Probe.EndpointMethods()` walks each group's declared public methods,
  so the ten endpoints join the sweep the moment Task 7 adds the two facade properties. There are no
  per-endpoint entries in that file to add.

---

### Task 1: Capture the ten fixtures, and verify each one carries the row its test needs

**Files:**
- Create: `tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings.0002010670.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings-latest.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings-search.Wellness.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/fundraising.0001617426.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/fundraising-latest.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/fundraising-search.Schutt.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/discounted-cash-flow.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/levered-discounted-cash-flow.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/custom-discounted-cash-flow.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/custom-levered-discounted-cash-flow.AAPL.json`

**Interfaces:**
- Consumes: nothing.
- Produces: ten fixture files. `Fixtures\*.json` is already globbed into the test project with
  `CopyToOutputDirectory="PreserveNewest"` (`tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj:30`), so no project
  file changes. `Binding.Fixture(name)` reads them from the output directory. Tasks 2, 3, 5 and 6 assert
  against them, and the exact row properties each test depends on are pinned in Step 3 below.

**Why fixtures are copied rather than re-captured.** Three tests in this slice depend on a row with a
specific *shape* — a null search date, an empty `yearOfIncorporation`, an amount above Int32 — and a live
`-latest` call returns whatever filed that morning. The bodies captured 2026-08-31 during the measure and
design phases are the pinned record. Step 2 gives the live query behind each one and the predicate to select
by, so a fixture can be rebuilt from scratch; it will not be byte-identical, and that is expected.

- [ ] **Step 1: Copy and trim the ten fixtures**

The captures live in this session's scratchpad. Set `SRC` to that directory:

```bash
SRC=/private/tmp/claude-501/-Users-herbertsabanal-Projects-fmpdotnet/3d6d07c4-e264-4690-b3e8-8dcb74e8a125/scratchpad/f39
ls "$SRC"/cand-cf-cik.json "$SRC"/cfl-limit5.json "$SRC"/cfs-well.json "$SRC"/fr-by-cik.json \
   "$SRC"/frl-100-b.json "$SRC"/fr-search-name.json "$SRC"/dcf-aapl.json "$SRC"/ldcf-aapl.json \
   "$SRC"/cdcf-aapl.json "$SRC"/cldcf-aapl.json
```

If any is missing, the scratchpad is gone — go to Step 2 and capture live instead.

```bash
DST=tests/FmpDotNet.Tests/Fixtures
python3 - "$SRC" "$DST" <<'PY'
import json, os, sys
src, dst = sys.argv[1], sys.argv[2]

def load(name):
    with open(os.path.join(src, name)) as fh:
        return json.load(fh)

def write(name, rows):
    with open(os.path.join(dst, name), "w") as fh:
        json.dump(rows, fh, indent=2)
        fh.write("\n")
    print(f"{name}: {len(rows)} rows, {len(rows[0])} keys")

write("crowdfunding-offerings.0002010670.json", load("cand-cf-cik.json")[:3])
write("crowdfunding-offerings-latest.head.json", load("cfl-limit5.json")[:3])

# Rows 0, 7 and 8: one with a null date and two with MM-DD-YYYY dates. 461 of 7,003 measured search rows
# carry a null date (6.6%), and both shapes have to be in the fixture for the converter test to mean anything.
cfs = load("cfs-well.json")
write("crowdfunding-offerings-search.Wellness.json", [cfs[0], cfs[7], cfs[8]])

write("fundraising.0001617426.json", load("fr-by-cik.json")[:3])

# Selected by PREDICATE, not by index, so this reproduces against a fresh capture: one fully populated row,
# one whose dateOfFirstSale is the empty string, and one whose totalAmountSold exceeds Int32.
frl = load("frl-100-b.json")
def first(pred):
    return next(r for r in frl if pred(r))
write("fundraising-latest.head.json", [
    first(lambda r: not any(v is None or (isinstance(v, str) and not v.strip()) for v in r.values())),
    first(lambda r: r.get("dateOfFirstSale") == ""),
    first(lambda r: (r.get("totalAmountSold") or 0) > 2147483647),
])

write("fundraising-search.Schutt.json", load("fr-search-name.json")[:3])
write("discounted-cash-flow.AAPL.json", load("dcf-aapl.json"))
write("levered-discounted-cash-flow.AAPL.json", load("ldcf-aapl.json"))
write("custom-discounted-cash-flow.AAPL.json", load("cdcf-aapl.json")[:2])
write("custom-levered-discounted-cash-flow.AAPL.json", load("cldcf-aapl.json")[:2])
PY
```

Expected output, in order:

```
crowdfunding-offerings.0002010670.json: 3 rows, 48 keys
crowdfunding-offerings-latest.head.json: 3 rows, 48 keys
crowdfunding-offerings-search.Wellness.json: 3 rows, 3 keys
fundraising.0001617426.json: 3 rows, 43 keys
fundraising-latest.head.json: 3 rows, 43 keys
fundraising-search.Schutt.json: 3 rows, 3 keys
discounted-cash-flow.AAPL.json: 1 rows, 4 keys
levered-discounted-cash-flow.AAPL.json: 1 rows, 4 keys
custom-discounted-cash-flow.AAPL.json: 2 rows, 47 keys
custom-levered-discounted-cash-flow.AAPL.json: 2 rows, 34 keys
```

- [ ] **Step 2: Only if the scratchpad is gone — capture the ten bodies live**

Write this harness, which never prints or stores a built URL:

```bash
mkdir -p /tmp/f39src && cat > /tmp/f39src/cap.sh <<'SH'
#!/bin/bash
# usage: cap.sh <label> <path> [query]   — captures a live FMP response, never printing the built URL.
SP="$(cd "$(dirname "$0")" && pwd)"
KEY="$(awk -F= '/^FMP_API_KEY=/{print $2; exit}' /Users/herbertsabanal/Projects/fmpdotnet/.env)"
label="$1"; path="$2"; query="$3"
out="$SP/$label.json"
sep="?"; [ -n "$query" ] && sep="?$query&"
code=$(curl -sS -o "$out" -w '%{http_code}' "https://financialmodelingprep.com/${path}${sep}apikey=${KEY}" 2>/dev/null)
printf '%-24s http=%s bytes=%s\n' "$label" "$code" "$(wc -c < "$out" | tr -d ' ')"
SH
chmod +x /tmp/f39src/cap.sh

/tmp/f39src/cap.sh cand-cf-cik     stable/crowdfunding-offerings                'cik=0002010670'
/tmp/f39src/cap.sh cfl-limit5      stable/crowdfunding-offerings-latest         'limit=5'
/tmp/f39src/cap.sh cfs-well        stable/crowdfunding-offerings-search         'name=Wellness'
/tmp/f39src/cap.sh fr-by-cik       stable/fundraising                           'cik=0001617426'
/tmp/f39src/cap.sh frl-100-b       stable/fundraising-latest                    'limit=100'
/tmp/f39src/cap.sh fr-search-name  stable/fundraising-search                    'name=Schutt'
/tmp/f39src/cap.sh dcf-aapl        stable/discounted-cash-flow                  'symbol=AAPL'
/tmp/f39src/cap.sh ldcf-aapl       stable/levered-discounted-cash-flow          'symbol=AAPL'
/tmp/f39src/cap.sh cdcf-aapl       stable/custom-discounted-cash-flow           'symbol=AAPL'
/tmp/f39src/cap.sh cldcf-aapl      stable/custom-levered-discounted-cash-flow   'symbol=AAPL'
```

Expected: ten `http=200` lines. Then run Step 1's python with `SRC=/tmp/f39src`. The `cfs-well.json` index
selection (`cfs[0], cfs[7], cfs[8]`) is the one part that will not reproduce — replace it with

```python
cfs = load("cfs-well.json")
dated = [r for r in cfs if r.get("date")]
undated = [r for r in cfs if not r.get("date")]
write("crowdfunding-offerings-search.Wellness.json", [undated[0], dated[0], dated[1]])
```

and delete `/tmp/f39src` when the fixtures are written.

- [ ] **Step 3: Verify each fixture carries the row its test depends on**

```bash
python3 - <<'PY'
import json, glob, os
d = "tests/FmpDotNet.Tests/Fixtures"
def L(n): return json.load(open(os.path.join(d, n)))
def unbound(r): return sorted(k for k, v in r.items()
                              if v is None or (isinstance(v, str) and not v.strip()))

cf = L("crowdfunding-offerings.0002010670.json")
assert all(r["cik"] == "0002010670" for r in cf)
assert all(unbound(r) == ["securityOfferedOtherDescription"] for r in cf), [unbound(r) for r in cf]
assert all(len(r["date"]) == 10 and r["date"][2] == "-" and r["date"][5] == "-" for r in cf)
assert all(r["filingDate"].endswith(" 00:00:00") for r in cf)

cfl = L("crowdfunding-offerings-latest.head.json")
assert all(unbound(r) == [] for r in cfl), [unbound(r) for r in cfl]
assert all(r["overSubscriptionAccepted"] in ("Y", "N") for r in cfl)

cfs = L("crowdfunding-offerings-search.Wellness.json")
assert sum(1 for r in cfs if r["date"] is None) == 1
assert sum(1 for r in cfs if r["date"]) == 2

fr = L("fundraising.0001617426.json")
assert all(r["cik"] == "0001617426" for r in fr)
assert all(unbound(r) == ["incorporatedWithinFiveYears", "revenueRange",
                          "securitiesOfferedAreOfEquityType", "yearOfIncorporation"] for r in fr)

frl = L("fundraising-latest.head.json")
assert any(unbound(r) == [] for r in frl)
assert any(r["dateOfFirstSale"] == "" for r in frl)
assert any(r["totalAmountSold"] > 2147483647 for r in frl)
assert any(r["yearOfIncorporation"] == "" for r in frl)

frs = L("fundraising-search.Schutt.json")
assert all(len(r["date"]) == 19 and r["date"][10] == " " for r in frs)

for n in ("discounted-cash-flow.AAPL.json", "levered-discounted-cash-flow.AAPL.json"):
    r = L(n)[0]
    assert list(r) == ["symbol", "date", "dcf", "Stock Price"], list(r)
    assert r["symbol"] == "AAPL"

assert all(isinstance(r["year"], str) for r in L("custom-discounted-cash-flow.AAPL.json"))
assert all("costofDebt" in r for r in L("custom-levered-discounted-cash-flow.AAPL.json"))

# No fixture may carry a request URL or a key.
for f in glob.glob(os.path.join(d, "*.json")):
    body = open(f).read()
    assert "apikey" not in body.lower(), f
    assert "financialmodelingprep.com" not in body, f
print("all ten fixtures verified")
PY
```

Expected: `all ten fixtures verified`, and nothing else.

- [ ] **Step 4: Commit**

```bash
git add tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings.0002010670.json \
        tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings-latest.head.json \
        tests/FmpDotNet.Tests/Fixtures/crowdfunding-offerings-search.Wellness.json \
        tests/FmpDotNet.Tests/Fixtures/fundraising.0001617426.json \
        tests/FmpDotNet.Tests/Fixtures/fundraising-latest.head.json \
        tests/FmpDotNet.Tests/Fixtures/fundraising-search.Schutt.json \
        tests/FmpDotNet.Tests/Fixtures/discounted-cash-flow.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/levered-discounted-cash-flow.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/custom-discounted-cash-flow.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/custom-levered-discounted-cash-flow.AAPL.json
git commit -m "test: capture the ten Fundraisers and DCF fixtures (#39)"
```

---

### Task 2: The `MM-DD-YYYY` converter and the two crowdfunding records

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append one converter after
  `LongFormLocalDateJsonConverter`, which ends at line 818)
- Create: `src/FmpDotNet/Models/CrowdfundingOffering.cs`
- Create: `src/FmpDotNet/Models/CrowdfundingSearchHit.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two entries)
- Create: `tests/FmpDotNet.Tests/FundraisersTests.cs`

**Interfaces:**
- Consumes: `crowdfunding-offerings.0002010670.json`, `crowdfunding-offerings-latest.head.json` and
  `crowdfunding-offerings-search.Wellness.json` from Task 1. `NullableDateAtMidnightJsonConverter`
  (`NodaConverters.cs:186`), `NullableEasternInstantJsonConverter` (`:105`) and `YesNoBooleanJsonConverter`
  (`:747`), all unchanged.
- Produces: `public sealed class NullableMonthDayYearDateJsonConverter : JsonConverter<LocalDate?>` in
  `FmpDotNet.Serialization`; `public sealed record CrowdfundingOffering` (48 `init`-only properties) and
  `public sealed record CrowdfundingSearchHit` (3) in `FmpDotNet.Models`;
  `FmpJsonContext.Default.ListCrowdfundingOffering` and `.ListCrowdfundingSearchHit`. Task 3 extends
  `FundraisersTests.cs`; Task 4 returns `IReadOnlyList<CrowdfundingOffering>` from three methods and
  `IReadOnlyList<CrowdfundingSearchHit>` from one.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/FundraisersTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The six Fundraisers paths, checked against captures taken live 2026-08-31.</summary>
public class FundraisersTests
{
    [Fact]
    public void A_crowdfunding_row_binds_every_one_of_its_forty_eight_keys()
    {
        // Binding.Unbound names every [JsonPropertyName] property that came back null, blank or empty, so
        // this is the WHOLE record binding rather than a spot check. Five models in this repo were measured
        // 2026-08-27 with most of their [JsonPropertyName] attributes doing nothing, which a two-field
        // assertion cannot see. Task 1 verified the -latest fixture's three rows carry no null at all.
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings-latest.head.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Equal(3, latest.Count);
        Assert.All(latest, r => Assert.Empty(Binding.Unbound(r)));

        // The by-CIK fixture is Finlete Funding, Inc., and its one absent field is named rather than waved
        // at: securityOfferedOtherDescription was null on 695 of 1,000 rows measured 2026-08-31, so a fixture
        // without it would be the unusual case, not this one.
        var byCik = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings.0002010670.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Equal(3, byCik.Count);
        Assert.All(byCik, r => Assert.Equal(["SecurityOfferedOtherDescription"], Binding.Unbound(r)));
        Assert.All(byCik, r => Assert.Equal("0002010670", r.Cik));
    }

    [Fact]
    public void The_offering_date_is_month_day_year_and_the_ISO_converter_reads_it_as_null()
    {
        // THE test this slice exists to protect, and the failure it guards is silent in both directions.
        // NullableLocalDateJsonConverter parses with LocalDatePattern.Iso and returns null on failure rather
        // than throwing (NodaConverters.cs:43-44), so binding crowdfunding's `date` with it yields null on
        // 100% of rows at HTTP 200 with no exception and no warning. Measured 2026-08-31 by deserialising
        // through it: "08-28-2026" -> null, "04-30-2027" -> null, "2026-08-31" -> 2026-08-31.
        //
        // The component order is measured, not assumed: over 1,000 crowdfunding rows and 6,542 dated search
        // rows the first component never exceeded 12 while the second reached 31, so DD-MM-YYYY is ruled out
        // by 7,542 rows.
        var row = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011","offeringDeadlineDate":"10-31-2026"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(new LocalDate(2011, 11, 22), row.Date);
        Assert.Equal(new LocalDate(2026, 10, 31), row.OfferingDeadlineDate);

        // The same two strings through the ISO converter, which is what a naive binding would have used.
        // FundraisingNotice.Date carries it, and reading a crowdfunding value with it gives NOTHING back.
        var throughIso = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Null(throughIso.Date);

        // And absence has one spelling on this converter, whichever way it arrives.
        var absent = JsonSerializer.Deserialize(
            """[{"date":null},{"date":""},{"date":"not a date"},{"companyName":"no date key at all"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.All(absent, r => Assert.Null(r.Date));
    }

    [Fact]
    public void The_offering_date_precedes_the_filing_date_on_every_row()
    {
        // The most easily-missed semantic trap in the slice, caught from FMP's own documented sample, which
        // shows "date": "11-22-2011" beside "filingDate": "2026-07-30 00:00:00" — fifteen years apart.
        // Measured 2026-08-31, `date` precedes `filingDate` on 1,000 of 1,000 rows with zero exceptions,
        // gaps of 0 to 43 years and a year range of 1983-2026; and it is constant across every filing for
        // 10 of 18 filers, including Finlete Funding, whose 48 filings all carry 12-19-2023. It is a property
        // of the company, not of the filing. This test fails if anyone renames Date to FilingDate or swaps
        // the two converters.
        foreach (var fixture in new[]
                 {
                     "crowdfunding-offerings.0002010670.json",
                     "crowdfunding-offerings-latest.head.json",
                 })
        {
            var rows = JsonSerializer.Deserialize(
                Binding.Fixture(fixture), FmpJsonContext.Default.ListCrowdfundingOffering)!;

            Assert.All(rows, r =>
            {
                Assert.NotNull(r.Date);
                Assert.NotNull(r.FilingDate);
                Assert.True(r.Date < r.FilingDate, $"{r.Cik}: {r.Date} is not before {r.FilingDate}");
            });
        }

        // And the by-CIK fixture pins the constancy: three filings by one issuer, one formation date.
        var finlete = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings.0002010670.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Single(finlete.Select(r => r.Date).Distinct());
    }

    [Fact]
    public void The_filing_date_is_a_date_and_the_accepted_date_is_an_Eastern_instant()
    {
        // Two fields, one wire format, two different converters — and swapping either is silent.
        //
        // filingDate: its time component was 00:00:00 on 3,575 of 3,575 rows measured 2026-08-31, exactly
        // what NullableDateAtMidnightJsonConverter was written for in the SEC Filings slice (2,115 of 2,115
        // there). Binding it as a timestamp leaks a meaningless midnight into every comparison a caller
        // writes, so the property type itself is asserted, not just the value.
        Assert.Equal(
            typeof(LocalDate?),
            typeof(CrowdfundingOffering).GetProperty(nameof(CrowdfundingOffering.FilingDate))!.PropertyType);

        var row = JsonSerializer.Deserialize(
            """[{"filingDate":"2026-07-30 00:00:00","acceptedDate":"2026-08-28 21:52:44"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(new LocalDate(2026, 7, 30), row.FilingDate);

        // acceptedDate: the SDK carries two converters for the identical "yyyy-MM-dd HH:mm:ss" shape and they
        // are four to five hours apart. NullableFmpInstantJsonConverter (UTC) compiles here, deserialises
        // here, and is wrong. The measurement that chose Eastern: over 1,395 acceptedDate values and 1,779
        // fundraising-search timestamps spanning 2009-2026, the window is 06:00-22:00 in EDT (n=1,060) and
        // 06:00-21:59 in EST (n=445) — it does NOT shift across the DST boundary, which a stored instant
        // would — and ZERO of 3,174 values fall in hours 22-05, which a UTC reading of an Eastern-window feed
        // would arithmetically require.
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 1, 52, 44), row.AcceptedDate);   // EDT, UTC-4

        var winter = JsonSerializer.Deserialize(
            """[{"acceptedDate":"2026-01-14 16:05:00"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(Instant.FromUtc(2026, 1, 14, 21, 5, 0), winter.AcceptedDate);  // EST, UTC-5

        // The two offsets differ, which rules out every FIXED-offset reading as well as UTC: a converter
        // hard-coding -4 or -5 would pass one of the assertions above and fail this one.
        var summer = JsonSerializer.Deserialize(
            """[{"acceptedDate":"2026-08-27 16:05:00"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.NotEqual(
            summer.AcceptedDate!.Value - Instant.FromUtc(2026, 8, 27, 16, 5, 0),
            winter.AcceptedDate!.Value - Instant.FromUtc(2026, 1, 14, 16, 5, 0));
    }

    [Fact]
    public void Over_subscription_is_a_Y_or_an_N_and_anything_else_is_null_rather_than_false()
    {
        // The wire sends "Y"/"N" strings, not booleans. YesNoBooleanJsonConverter maps any unmeasured third
        // value to null rather than guessing — which matters because `false` and "we have never seen this
        // value" are different answers, and only one of them is true.
        var rows = JsonSerializer.Deserialize(
            """
            [{"overSubscriptionAccepted":"Y"},{"overSubscriptionAccepted":"N"},
             {"overSubscriptionAccepted":"MAYBE"},{"overSubscriptionAccepted":null},{"cik":"0000000000"}]
            """,
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.True(rows[0].OverSubscriptionAccepted);
        Assert.False(rows[1].OverSubscriptionAccepted);
        Assert.Null(rows[2].OverSubscriptionAccepted);
        Assert.Null(rows[3].OverSubscriptionAccepted);
        Assert.Null(rows[4].OverSubscriptionAccepted);
    }

    [Fact]
    public void The_two_misspelled_wire_names_and_the_string_zip_code_are_reproduced_exactly()
    {
        // cashAndCashEquiValent* carries a capital V in "Equivalent". It is in FMP's own documented sample
        // AND on the wire, so it is stable rather than a transient bug — and a [JsonPropertyName] that
        // "corrects" it binds nothing, silently, on a property whose type gives no hint.
        //
        // issuerZipCode is a STRING: three forms measured 2026-08-31 over 1,000 rows — 99999 on 990, 9999 on
        // 5, and 99999-9999 on 5. An integer type loses the leading zero on the four-digit form and throws
        // outright on the hyphenated one, taking the whole response with it.
        var row = JsonSerializer.Deserialize(
            """
            [{"cashAndCashEquiValentMostRecentFiscalYear":1.5,
              "cashAndCashEquiValentPriorFiscalYear":-2.5,
              "issuerZipCode":"01234-5678",
              "compensationAmount":"7.9% of the offering amount upon a successful fundraise",
              "financialInterest":"No",
              "totalAssetMostRecentFiscalYear":220738384.75,
              "netIncomeMostRecentFiscalYear":-27665487}]
            """,
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(1.5m, row.CashAndCashEquivalentMostRecentFiscalYear);
        Assert.Equal(-2.5m, row.CashAndCashEquivalentPriorFiscalYear);
        Assert.Equal("01234-5678", row.IssuerZipCode);

        // compensationAmount and financialInterest are free prose despite their names — 57 distinct values up
        // to 256 characters on the second, and "No" is common but it is not a boolean.
        Assert.StartsWith("7.9%", row.CompensationAmount);
        Assert.Equal("No", row.FinancialInterest);

        // Fractional AND negative: offeringPrice was fractional on 884 of 3,656 rows measured 2026-08-31 and
        // netIncomeMostRecentFiscalYear reached -27,665,487. Every numeric here is decimal? for that reason.
        Assert.Equal(220738384.75m, row.TotalAssetMostRecentFiscalYear);
        Assert.Equal(-27665487m, row.NetIncomeMostRecentFiscalYear);
    }

    [Fact]
    public void A_crowdfunding_search_hit_carries_three_keys_and_a_date_that_is_often_absent()
    {
        // 461 of 7,003 measured search rows carry a null date — 6.6% — and FMP's own documented sample shows
        // one. The date is the SAME MM-DD-YYYY encoding as the offering record's, which is why this record
        // exists separately from FundraisingSearchHit: those three keys are identical and the date is not.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings-search.Wellness.json"),
            FmpJsonContext.Default.ListCrowdfundingSearchHit)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.NotNull(r.Cik));
        Assert.All(rows, r => Assert.NotNull(r.Name));
        Assert.Single(rows, r => r.Date is null);
        Assert.Equal(2, rows.Count(r => r.Date is not null));
        Assert.All(rows.Where(r => r.Date is not null), r => Assert.InRange(r.Date!.Value.Year, 1983, 2026));

        // Three keys and no more. This record is deliberately tiny; a field added here would be a field FMP
        // does not send.
        Assert.Equal(3, typeof(CrowdfundingSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [Fact]
    public void The_crowdfunding_offering_binds_all_forty_eight_wire_names_and_no_others()
    {
        // The count is the point. 48 keys were confirmed three ways on 2026-08-31: against the live captures,
        // against FMP's documented sample (same 48 keys in the same ORDER), and against the independent
        // Python fmpsdk, whose TypedDict carries 48 fields with an identical key set. The measurements file's
        // census says "16 x *MostRecentFiscalYear / *PriorFiscalYear"; the wire has NINE PAIRS, eighteen
        // fields, and 30 + 18 = 48. This test is what makes that arithmetic checkable.
        var names = typeof(CrowdfundingOffering)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .ToList();

        Assert.Equal(48, names.Count);
        Assert.All(names, n => Assert.NotNull(n));
        Assert.Equal(48, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, names.Count(n => n is not null
                                          && (n.EndsWith("MostRecentFiscalYear", StringComparison.Ordinal)
                                              || n.EndsWith("PriorFiscalYear", StringComparison.Ordinal))));
        Assert.Contains("cashAndCashEquiValentMostRecentFiscalYear", names);
        Assert.Contains("cashAndCashEquiValentPriorFiscalYear", names);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/FmpDotNet.Tests
```

Expected: FAIL. `error CS0246: The type or namespace name 'CrowdfundingOffering' could not be found`, the
same for `CrowdfundingSearchHit`, and `error CS1061` for `ListCrowdfundingOffering`,
`ListCrowdfundingSearchHit` and `ListFundraisingNotice` on `FmpJsonContext`. `ListFundraisingNotice` arrives
in Task 3; **until then this file does not compile, and that is expected** — Task 3's Step 2 is the first
green build of `FundraisersTests.cs`.

- [ ] **Step 3: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `LongFormLocalDateJsonConverter` (which ends
at line 818) and before `LocalTimeJsonConverter`:

```csharp
/// <summary>Reads FMP's <c>MM-DD-YYYY</c> dates — <c>"11-22-2011"</c> — as a <see cref="LocalDate"/>.
///
/// <para><b>The fifth converter for a date in this SDK, and the trap it closes is the reason it exists.</b>
/// <see cref="NullableLocalDateJsonConverter"/> parses with <c>LocalDatePattern.Iso</c> and answers
/// <see langword="null"/> on failure rather than throwing, so binding a <c>MM-DD-YYYY</c> field with it
/// yields <b>null on 100% of rows, at HTTP 200, with no exception and no warning</b>. Measured 2026-08-31 by
/// deserialising through it: <c>"08-28-2026"</c> and <c>"04-30-2027"</c> both read as null, while
/// <c>"2026-08-31"</c> reads correctly.</para>
///
/// <para><b>The component order is measured, not assumed.</b> Over 1,000 crowdfunding offering rows and
/// 6,542 dated search rows captured 2026-08-31, the first component never exceeded <b>12</b> while the
/// second reached <b>31</b> — so <c>DD-MM-YYYY</c> is ruled out by 7,542 rows. FMP's own documented sample
/// corroborates it independently with <c>"11-22-2011"</c> and <c>"10-31-2026"</c>: a 22 and a 31 in second
/// position can only be days.</para>
///
/// <para><b>Invariant culture is load-bearing, not boilerplate</b> — for the reason
/// <see cref="LongFormLocalDateJsonConverter"/> records. The separator and field order are fixed here rather
/// than taken from the host, so a French or German runtime reads the same value this one does.</para>
///
/// <para><b>One pattern, no fallback, deliberately.</b> If FMP ever switches this field to ISO, this reads
/// null rather than quietly accepting a second format, and the weekly smoke baseline reports it as
/// <c>Date: now always null, was populated</c> on the run after it happens. A silent fallback would make the
/// change invisible, which is the opposite of what a measured SDK is for.</para>
///
/// <para>Applied to <c>CrowdfundingOffering.Date</c>, <c>CrowdfundingOffering.OfferingDeadlineDate</c> and
/// <c>CrowdfundingSearchHit.Date</c>. Its sibling <c>FundraisingNotice.Date</c> is ISO on the same-named
/// field of a different path and keeps <see cref="NullableLocalDateJsonConverter"/> — the two are one
/// substitution apart and neither substitution throws.</para>
///
/// <para>Null on JSON null, on <c>""</c> and on any unparseable value, following the rest of this file: one
/// bad date costs one field rather than the whole response.</para></summary>
public sealed class NullableMonthDayYearDateJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDatePattern Pattern =
        LocalDatePattern.CreateWithInvariantCulture("MM-dd-uuuu");

    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}
```

---
- [ ] **Step 4: Write `CrowdfundingOffering`**

Create `src/FmpDotNet/Models/CrowdfundingOffering.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One Regulation Crowdfunding offering — a Form C filing — from <c>stable/crowdfunding-offerings</c>
/// and <c>stable/crowdfunding-offerings-latest</c>.
///
/// <para><b>Forty-eight keys, in the same order on both paths</b>, verified by direct list comparison rather
/// than by eye on 2026-08-31, and confirmed twice more: FMP's own documented sample carries the same 48 keys
/// in the same order, and the independent Python <c>fmpsdk</c> models it as a 48-field type with an
/// identical key set.</para>
///
/// <para><b>Form C filers and Form D filers are disjoint populations.</b> Measured 2026-08-31 in both
/// directions: crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> on <c>stable/fundraising</c>, and
/// fundraising CIK <c>0001617426</c> answers <b>0 rows</b> here. A CIK from one corpus is not a lookup that
/// failed on the other — it is a query for something that was never there, and it arrives as HTTP 200 with an
/// empty array either way. See <see cref="FundraisingNotice"/>.</para>
///
/// <para><b>Four fields on this record are not what their names suggest.</b>
/// <see cref="Date"/> is not the filing date, <see cref="CompensationAmount"/> is not an amount,
/// <see cref="FinancialInterest"/> is not a flag, and <see cref="OverSubscriptionAccepted"/> arrives as a
/// string. Each carries its measurement below.</para>
///
/// <para><b>Every numeric here is <see cref="decimal"/>, and both halves of that are measured.</b> Fractional:
/// <see cref="OfferingPrice"/> on <b>884</b> of 3,656 rows, <see cref="OfferingAmount"/> on 579,
/// <see cref="MaximumOfferingAmount"/> on 482, and every one of the eighteen fiscal-year fields on 56-339.
/// Negative: <see cref="NetIncomeMostRecentFiscalYear"/> reaches <b>-27,665,487</b> and is negative on 682 of
/// 1,000 rows. An integral type would throw on the first and take the whole response with it — the reasoning
/// is on <see cref="FinancialScores.PiotroskiScore"/>.</para>
///
/// <para>Every property is nullable and the measured null counts are in the docs rather than in the type.
/// "Never null in 1,000 rows" and "cannot be null" are different statements, and only the first was
/// measured.</para></summary>
public sealed record CrowdfundingOffering
{
    /// <summary>The issuer's SEC CIK, zero-padded to ten digits on 1,000 of 1,000 rows measured
    /// 2026-08-31 — <c>"0002010670"</c>. A string, because the padding is part of the identifier as EDGAR
    /// writes it.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name as EDGAR holds it. 652 distinct values in 1,000 rows measured 2026-08-31.
    /// Usually but not always equal to <see cref="NameOfIssuer"/>, which is the name on the offering.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary><b>Not the filing date.</b> Measured 2026-08-31, this precedes <see cref="FilingDate"/> on
    /// <b>1,000 of 1,000</b> rows with zero exceptions, gaps running 0 to 43 years and a year range of
    /// 1983-2026 — and it is <i>constant across every filing</i> for <b>10 of 18</b> filers sampled,
    /// including one issuer whose <b>48</b> filings all carry <c>12-19-2023</c>.
    ///
    /// <para>That behaviour is a property of the company rather than of the document, which is what a date of
    /// formation looks like. The SDK does not rename it: the wire says <c>date</c> and no reachable FMP
    /// documentation labels it, so inventing a name would be stating a fact nobody measured. What is measured
    /// is stated here, and a test pins <c>Date &lt; FilingDate</c>. Use <see cref="FilingDate"/> when you want
    /// to know when the filing happened.</para>
    ///
    /// <para><b><c>MM-DD-YYYY</c>, and the SDK's ISO converter reads it as null without throwing.</b> See
    /// <see cref="NullableMonthDayYearDateJsonConverter"/>. Never null in 1,000 rows measured
    /// 2026-08-31.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When the filing was submitted. <b>A date, not a timestamp</b> — its time component was
    /// <c>00:00:00</c> on <b>3,575 of 3,575</b> rows measured 2026-08-31, a dummy midnight bolted on to a
    /// date. Binding it as an instant would leak a meaningless midnight into every comparison a caller
    /// writes. Reaches this type through <see cref="NullableDateAtMidnightJsonConverter"/>; the same field on
    /// <see cref="FundraisingNotice.FilingDate"/> is measured identically and takes the same converter.
    /// Never null in 1,000 rows.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, read as <b>Eastern</b> wall clock.
    ///
    /// <para><b>The typing decision of this record, and the intuitive answer is wrong.</b> The wire sends
    /// <c>"yyyy-MM-dd HH:mm:ss"</c> with no offset and no zone marker, and this SDK carries two converters for
    /// that exact shape. <see cref="NullableFmpInstantJsonConverter"/> reads it as UTC and would put every
    /// acceptance <b>four to five hours early</b>. It compiles, it deserialises, and nothing in the data
    /// reveals it. FMP's documentation does not settle it either: every endpoint page answers HTTP 403 to
    /// automated fetch, and the documented sample carries no offset and no timezone note.</para>
    ///
    /// <para><b>So the wire was measured, over 1,395 distinct values here and 1,779 more on
    /// <see cref="FundraisingSearchHit.Date"/>, spanning 2009-2026.</b> EDT (n=1,060) window
    /// <b>06:00-22:00</b>; EST (n=445) window <b>06:00-21:59</b>. <b>The window does not shift across the DST
    /// boundary</b> — a stored instant would move by an hour, a stripped wall clock does not. And a UTC
    /// reading is refuted arithmetically: 20:00 EDT is 00:00 UTC, so an Eastern-window feed read as UTC must
    /// place rows in hours 22-03, and there are <b>zero</b> in 3,174 values. The only two outside 06:00-21:59
    /// land on EDGAR's 22:00 ET closing minute rather than beyond it, and the drop between hour 17 (114 rows)
    /// and hour 18 (59) sits on EDGAR's 17:30 ET same-day cutoff.</para>
    ///
    /// <para>Never null in 1,000 rows measured 2026-08-31.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form code — <c>"C"</c>, <c>"C/A"</c>, <c>"C-U"</c> and three others. 6 distinct
    /// values in 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The form code spelled out — <c>"Offering Statement"</c>. 6 distinct values, one per
    /// <see cref="FormType"/>, measured 2026-08-31.</summary>
    [JsonPropertyName("formSignification")] public string? FormSignification { get; init; }

    /// <summary>The issuer's name as it appears on the offering. Never null in 1,000 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("nameOfIssuer")] public string? NameOfIssuer { get; init; }

    /// <summary>The issuer's legal form. Four values measured 2026-08-31: <c>Corporation</c>,
    /// <c>Limited Liability Company</c>, <c>Limited Partnership</c>, <c>Other</c> — the same vocabulary
    /// <see cref="FundraisingNotice.EntityType"/> uses under a different name.</summary>
    [JsonPropertyName("legalStatusForm")] public string? LegalStatusForm { get; init; }

    /// <summary>The two-character jurisdiction the issuer is organised under. 41 distinct values, null on 3
    /// of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("jurisdictionOrganization")] public string? JurisdictionOrganization { get; init; }

    /// <summary>The issuer's street address.</summary>
    [JsonPropertyName("issuerStreet")] public string? IssuerStreet { get; init; }

    /// <summary>The issuer's city.</summary>
    [JsonPropertyName("issuerCity")] public string? IssuerCity { get; init; }

    /// <summary>The issuer's state or country code. Null on 4 of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("issuerStateOrCountry")] public string? IssuerStateOrCountry { get; init; }

    /// <summary>The issuer's postal code, as a <b>string</b>.
    ///
    /// <para>Three forms measured 2026-08-31 over 1,000 rows: <c>99999</c> on 990, <c>9999</c> on 5, and
    /// <c>99999-9999</c> on 5. An integer type loses the leading zero on the four-digit form and throws
    /// outright on the hyphenated one — and a throw here costs the whole response, not one
    /// field.</para></summary>
    [JsonPropertyName("issuerZipCode")] public string? IssuerZipCode { get; init; }

    /// <summary>The issuer's website. Null on 70 of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("issuerWebsite")] public string? IssuerWebsite { get; init; }

    /// <summary>The funding portal or broker-dealer intermediating the offering. Null on 288 of 1,000 rows
    /// measured 2026-08-31, together with the four other intermediary and security fields — they arrive as
    /// a block or not at all.</summary>
    [JsonPropertyName("intermediaryCompanyName")] public string? IntermediaryCompanyName { get; init; }

    /// <summary>The intermediary's own CIK, zero-padded to ten digits on every non-null row measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("intermediaryCommissionCik")] public string? IntermediaryCommissionCik { get; init; }

    /// <summary>The intermediary's SEC file number, in <c>999-99999</c> form. Null on 288 of 1,000 rows
    /// measured 2026-08-31.</summary>
    [JsonPropertyName("intermediaryCommissionFileNumber")]
    public string? IntermediaryCommissionFileNumber { get; init; }

    /// <summary><b>Free prose, despite the name — never a number.</b> Measured 2026-08-31, a typical value
    /// is <i>"7.9% of the offering amount upon a successful fundraise, and be entitled to reimbursement…"</i>.
    /// Parsing a figure out of it is the caller's decision to make explicitly, not the SDK's to make
    /// silently. Null on 289 of 1,000 rows.</summary>
    [JsonPropertyName("compensationAmount")] public string? CompensationAmount { get; init; }

    /// <summary><b>Free prose, not a flag.</b> 57 distinct values up to 256 characters measured 2026-08-31.
    /// <c>"No"</c> is common, which is exactly what makes a boolean tempting and wrong: the other 56 values
    /// are sentences. Null on 298 of 1,000 rows.</summary>
    [JsonPropertyName("financialInterest")] public string? FinancialInterest { get; init; }

    /// <summary>What is being offered — 4 values measured 2026-08-31. Null on 289 of 1,000 rows.</summary>
    [JsonPropertyName("securityOfferedType")] public string? SecurityOfferedType { get; init; }

    /// <summary>Free text used when <see cref="SecurityOfferedType"/> is "Other". Null on <b>695</b> of 1,000
    /// rows measured 2026-08-31 — the most frequently absent field on this record.</summary>
    [JsonPropertyName("securityOfferedOtherDescription")]
    public string? SecurityOfferedOtherDescription { get; init; }

    /// <summary>How many securities are on offer. 0 to 10,000,000 measured 2026-08-31, never fractional in
    /// 3,656 rows — and <see cref="decimal"/> anyway, for the reason on the type.</summary>
    [JsonPropertyName("numberOfSecurityOffered")] public decimal? NumberOfSecurityOffered { get; init; }

    /// <summary>Price per security. <b>Fractional on 884 of 3,656 rows measured 2026-08-31</b>, 0 to 1,000.
    /// The single clearest reason this record's numerics are not integral.</summary>
    [JsonPropertyName("offeringPrice")] public decimal? OfferingPrice { get; init; }

    /// <summary>The target raise. Fractional on 579 of 3,656 rows measured 2026-08-31, 0 to
    /// 1,000,000.</summary>
    [JsonPropertyName("offeringAmount")] public decimal? OfferingAmount { get; init; }

    /// <summary>Whether the issuer will accept over-subscriptions. <b>The wire sends <c>"Y"</c> or
    /// <c>"N"</c>, not a boolean</b> — never null in 1,000 rows measured 2026-08-31. Reaches this type
    /// through <see cref="YesNoBooleanJsonConverter"/>, which maps any third value to
    /// <see langword="null"/> rather than guessing: <see langword="false"/> and "this SDK has never seen that
    /// value" are different answers.</summary>
    [JsonPropertyName("overSubscriptionAccepted")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? OverSubscriptionAccepted { get; init; }

    /// <summary>How over-subscriptions would be allocated. 3 values measured 2026-08-31; null on 297 of
    /// 1,000 rows.</summary>
    [JsonPropertyName("overSubscriptionAllocationType")]
    public string? OverSubscriptionAllocationType { get; init; }

    /// <summary>The ceiling on the raise. Fractional on 482 of 3,656 rows measured 2026-08-31, 0 to
    /// 5,000,000.</summary>
    [JsonPropertyName("maximumOfferingAmount")] public decimal? MaximumOfferingAmount { get; init; }

    /// <summary>When the offering closes. <b><c>MM-DD-YYYY</c>, like <see cref="Date"/></b> and unlike
    /// <see cref="FilingDate"/> beside it — see <see cref="NullableMonthDayYearDateJsonConverter"/>. Null on
    /// 289 of 1,000 rows measured 2026-08-31. Unlike <see cref="Date"/> this one <i>is</i> about the offering:
    /// it is dated in the future relative to the filing.</summary>
    [JsonPropertyName("offeringDeadlineDate")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? OfferingDeadlineDate { get; init; }

    /// <summary>Headcount at filing. 0 to 320 measured 2026-08-31.</summary>
    [JsonPropertyName("currentNumberOfEmployees")] public decimal? CurrentNumberOfEmployees { get; init; }

    // ---- The nine financial pairs. Eighteen fields, not sixteen: the measurements file's census says "16 x"
    // and the wire, FMP's documented sample and the Python fmpsdk all carry nine pairs. Thirty other keys
    // plus eighteen is the 48 all three agree on.
    //
    // Every one of them is decimal? and every one was measured BOTH fractional and negative on 2026-08-31
    // across 3,656 rows. These are unaudited figures self-reported on a Form C by companies that are, in the
    // main, pre-revenue: netIncomeMostRecentFiscalYear is negative on 682 of 1,000 rows. Reading a negative
    // here as a data error would be reading the population wrong.

    /// <summary>Total assets, most recent fiscal year. Fractional on 326 of 3,656 rows measured 2026-08-31;
    /// range -228,414.57 to 220,738,384.</summary>
    [JsonPropertyName("totalAssetMostRecentFiscalYear")]
    public decimal? TotalAssetMostRecentFiscalYear { get; init; }

    /// <summary>Total assets, prior fiscal year. Fractional on 205 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("totalAssetPriorFiscalYear")] public decimal? TotalAssetPriorFiscalYear { get; init; }

    /// <summary>Cash and cash equivalents, most recent fiscal year.
    ///
    /// <para><b>The wire name carries a capital <c>V</c> in "Equivalent"</b> —
    /// <c>cashAndCashEquiValentMostRecentFiscalYear</c> — and it appears that way in FMP's own documented
    /// sample as well as on the wire, so it is stable rather than a transient bug. A
    /// <c>[JsonPropertyName]</c> that "corrects" the spelling binds nothing, silently, on a nullable property
    /// that gives no hint. A test pins both spellings.</para>
    ///
    /// <para>Fractional on 312 of 3,656 rows measured 2026-08-31; range -292,945.30 to
    /// 30,153,080.</para></summary>
    [JsonPropertyName("cashAndCashEquiValentMostRecentFiscalYear")]
    public decimal? CashAndCashEquivalentMostRecentFiscalYear { get; init; }

    /// <summary>Cash and cash equivalents, prior fiscal year. Same capital <c>V</c> on the wire — see
    /// <see cref="CashAndCashEquivalentMostRecentFiscalYear"/>. Fractional on 197 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("cashAndCashEquiValentPriorFiscalYear")]
    public decimal? CashAndCashEquivalentPriorFiscalYear { get; init; }

    /// <summary>Accounts receivable, most recent fiscal year. Fractional on 114 of 3,656 rows measured
    /// 2026-08-31; goes negative to -17,625.45.</summary>
    [JsonPropertyName("accountsReceivableMostRecentFiscalYear")]
    public decimal? AccountsReceivableMostRecentFiscalYear { get; init; }

    /// <summary>Accounts receivable, prior fiscal year. Fractional on 56 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("accountsReceivablePriorFiscalYear")]
    public decimal? AccountsReceivablePriorFiscalYear { get; init; }

    /// <summary>Short-term debt, most recent fiscal year. Fractional on 213 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("shortTermDebtMostRecentFiscalYear")]
    public decimal? ShortTermDebtMostRecentFiscalYear { get; init; }

    /// <summary>Short-term debt, prior fiscal year. Fractional on 139 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("shortTermDebtPriorFiscalYear")]
    public decimal? ShortTermDebtPriorFiscalYear { get; init; }

    /// <summary>Long-term debt, most recent fiscal year. Fractional on 136 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("longTermDebtMostRecentFiscalYear")]
    public decimal? LongTermDebtMostRecentFiscalYear { get; init; }

    /// <summary>Long-term debt, prior fiscal year. Fractional on 61 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("longTermDebtPriorFiscalYear")]
    public decimal? LongTermDebtPriorFiscalYear { get; init; }

    /// <summary>Revenue, most recent fiscal year. Fractional on 198 of 3,656 rows measured 2026-08-31; range
    /// 0 to 128,625,869.</summary>
    [JsonPropertyName("revenueMostRecentFiscalYear")]
    public decimal? RevenueMostRecentFiscalYear { get; init; }

    /// <summary>Revenue, prior fiscal year. Fractional on 147 of 3,656 rows measured 2026-08-31, and
    /// <b>negative</b> on at least one — measured minimum -0.1, which a caller assuming revenue cannot be
    /// negative will not expect.</summary>
    [JsonPropertyName("revenuePriorFiscalYear")] public decimal? RevenuePriorFiscalYear { get; init; }

    /// <summary>Cost of goods sold, most recent fiscal year. Fractional on 207 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("costGoodsSoldMostRecentFiscalYear")]
    public decimal? CostGoodsSoldMostRecentFiscalYear { get; init; }

    /// <summary>Cost of goods sold, prior fiscal year. Fractional on 123 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("costGoodsSoldPriorFiscalYear")]
    public decimal? CostGoodsSoldPriorFiscalYear { get; init; }

    /// <summary>Taxes paid, most recent fiscal year. Fractional on 77 of 3,656 rows measured 2026-08-31;
    /// goes negative to -8,756,000.</summary>
    [JsonPropertyName("taxesPaidMostRecentFiscalYear")]
    public decimal? TaxesPaidMostRecentFiscalYear { get; init; }

    /// <summary>Taxes paid, prior fiscal year. Fractional on 77 of 3,656 rows measured 2026-08-31.</summary>
    [JsonPropertyName("taxesPaidPriorFiscalYear")] public decimal? TaxesPaidPriorFiscalYear { get; init; }

    /// <summary>Net income, most recent fiscal year. <b>Negative on 682 of 1,000 rows measured
    /// 2026-08-31</b>, reaching -27,665,487, and fractional on 339 of 3,656. This is the field that makes the
    /// case for the whole record's typing: an unsigned or integral type would be wrong twice over on the
    /// majority of rows.</summary>
    [JsonPropertyName("netIncomeMostRecentFiscalYear")]
    public decimal? NetIncomeMostRecentFiscalYear { get; init; }

    /// <summary>Net income, prior fiscal year. Fractional on 210 of 3,656 rows measured 2026-08-31; reaches
    /// -28,009,000.</summary>
    [JsonPropertyName("netIncomePriorFiscalYear")] public decimal? NetIncomePriorFiscalYear { get; init; }
}
```

- [ ] **Step 5: Write `CrowdfundingSearchHit`**

Create `src/FmpDotNet/Models/CrowdfundingSearchHit.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/crowdfunding-offerings-search</c> — three keys and nothing else.
///
/// <para><b>A row is one filing, not one company.</b> Measured 2026-08-31,
/// <c>name=Well</c> answered <b>44 rows across 31 distinct CIKs</b>. A caller populating a company picker
/// must dedupe by <see cref="Cik"/>; this SDK does not, because the row is what the wire sent.</para>
///
/// <para><b>Identical in shape to <see cref="FundraisingSearchHit"/> and deliberately a separate type</b>,
/// because <see cref="Date"/> is a different <i>kind</i> of value on each: <c>MM-DD-YYYY</c> here, and an
/// acceptance timestamp there. One record for both would need one converter for two encodings, and the wrong
/// one reads as null without throwing.</para>
///
/// <para><b>The matching rule is not known, and this SDK does not claim one.</b> Measured 2026-08-31:
/// <c>Well</c> and <c>Wellness</c> return byte-identical 44-row bodies while <c>Welln</c> and <c>Wellnes</c>
/// return <b>zero</b>; <c>Or</c>, <c>Ora</c> and <c>Orav</c> return zero while <c>Oravanti</c> returns one.
/// Substring, prefix and whole-word are each refuted by one of those rows. FMP's documentation describes the
/// endpoint as searching "by company name, campaign name, or platform" — <b>the platform clause is refuted by
/// measurement</b>: <c>name=NetCapital</c> returns 0 rows, though "NetCapital Funding Portal Inc." is the
/// intermediary in FMP's own documented sample. An intermediate-length query returning nothing is not an
/// error and not proof of absence.</para></summary>
public sealed record CrowdfundingSearchHit
{
    /// <summary>The issuer's CIK, zero-padded to ten digits. The key to
    /// <c>FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync</c>, and the field to dedupe on.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The matched name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The issuer's date as this corpus records it — <b><c>MM-DD-YYYY</c></b>, the same encoding
    /// and the same meaning as <see cref="CrowdfundingOffering.Date"/>, which is <i>not</i> a filing date.
    /// See <see cref="NullableMonthDayYearDateJsonConverter"/> for why the SDK's ISO converter would read
    /// every one of these as null without throwing.
    ///
    /// <para><b>Null on 461 of 7,003 rows measured 2026-08-31</b> — 6.6% — and FMP's own documented sample
    /// response shows one. A hit without a date is a normal hit.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? Date { get; init; }
}
```

**Then demote the forward crefs.** The two code blocks above are written as they will finally read, and five
of their `<see cref>`s point at `FundraisingNotice` and `FundraisingSearchHit`, which Task 3 creates. CS1574
is a build error, so demote them now; Task 3 Step 4 promotes them back.

```bash
sed -i '' -E 's|<see cref="(Fundraising[A-Za-z]*(\.[A-Za-z]*)?)"/>|<c>\1</c>|g' \
  src/FmpDotNet/Models/CrowdfundingOffering.cs src/FmpDotNet/Models/CrowdfundingSearchHit.cs
grep -c '<c>Fundraising' src/FmpDotNet/Models/CrowdfundingOffering.cs \
  src/FmpDotNet/Models/CrowdfundingSearchHit.cs
```

Expected: `4` and `1`. References to `FundraisersEndpoints` are a different word, are already written as
`<c>`, and this regex does not touch them — Task 4 Step 3 promotes those.

- [ ] **Step 6: Register both with the serialiser**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, append after the two News entries and before the class
declaration:

```csharp
// Fundraisers and DCF (#39). EIGHT entries for ten paths: the by-CIK path and its -latest sibling share one
// shape in each of the two filing corpora, and the two search paths carry the same three keys under two
// different date encodings — which is why they are two records rather than one.
[JsonSerializable(typeof(List<CrowdfundingOffering>))]
[JsonSerializable(typeof(List<CrowdfundingSearchHit>))]
```

- [ ] **Step 7: Build**

```bash
dotnet build src/FmpDotNet
```

Expected: PASS with no warnings. `dotnet build tests/FmpDotNet.Tests` still fails on
`ListFundraisingNotice`, which Task 3 adds — that is the only remaining error, and any other error is a
defect in this task.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/CrowdfundingOffering.cs \
        src/FmpDotNet/Models/CrowdfundingSearchHit.cs \
        tests/FmpDotNet.Tests/FundraisersTests.cs
git commit -m "feat: model the two crowdfunding shapes and the MM-DD-YYYY date they carry (#39)"
```

---

### Task 3: The two fundraising records — the empty string, and the same field name under two types

**Files:**
- Create: `src/FmpDotNet/Models/FundraisingNotice.cs`
- Create: `src/FmpDotNet/Models/FundraisingSearchHit.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two entries)
- Modify: `tests/FmpDotNet.Tests/FundraisersTests.cs` (append six tests)

**Interfaces:**
- Consumes: `fundraising.0001617426.json`, `fundraising-latest.head.json` and
  `fundraising-search.Schutt.json` from Task 1. `NullableLocalDateJsonConverter` (`NodaConverters.cs:37`),
  `NullableDateAtMidnightJsonConverter` (`:186`), `NullableEasternInstantJsonConverter` (`:105`) and
  `SentinelStringJsonConverter` (`:660`), all unchanged. `CrowdfundingOffering` from Task 2, for the
  side-by-side date test.
- Produces: `public sealed record FundraisingNotice` (43 `init`-only properties) and
  `public sealed record FundraisingSearchHit` (3) in `FmpDotNet.Models`;
  `FmpJsonContext.Default.ListFundraisingNotice` and `.ListFundraisingSearchHit`. Task 4 returns
  `IReadOnlyList<FundraisingNotice>` from two methods and `IReadOnlyList<FundraisingSearchHit>` from one.
  **This task is the first green build of `FundraisersTests.cs`.**

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/FundraisersTests.cs`, inside the class:

```csharp
    [Fact]
    public void A_fundraising_row_binds_every_one_of_its_forty_three_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising.0001617426.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("0001617426", r.Cik));

        // The four absent fields are NAMED rather than waved at, and every one of them is a measured
        // structural absence rather than a binding failure: incorporatedWithinFiveYears was null on 30 of 100
        // rows measured 2026-08-31, securitiesOfferedAreOfEquityType on 64, revenueRange on 29, and
        // yearOfIncorporation is the empty string on 30 — which SentinelStringJsonConverter collapses to null
        // so that absence has one spelling. If this list grows, a [JsonPropertyName] stopped binding.
        Assert.All(rows, r => Assert.Equal(
            ["IncorporatedWithinFiveYears", "RevenueRange", "SecuritiesOfferedAreOfEquityType",
             "YearOfIncorporation"],
            Binding.Unbound(r)));

        // Zero is a value, not an absence: findersFees was 0 on all 100 rows measured 2026-08-31 and
        // Binding.Unbound does not flag it. A caller reading 0 there is reading what FMP sent.
        Assert.All(rows, r => Assert.NotNull(r.FindersFees));
    }

    [Fact]
    public void The_empty_string_reads_as_null_and_the_other_forty_one_fields_survive()
    {
        // The trap that made yearOfIncorporation a string. Measured 2026-08-31 over 100 rows it is NEVER
        // null, is "" on 30, and is a four-digit year on the other 70 — a JSON string in both cases. It is
        // NOT int?: FmpJsonContext sets NumberHandling = AllowReadingFromString globally, so "1998" would
        // bind — but "" THROWS, and System.Text.Json aborts the entire list deserialisation rather than the
        // one field. Thirty percent of rows would cost the caller the whole response.
        //
        // dateOfFirstSale ("" on 7 of 100) needs no special handling: NullableLocalDateJsonConverter already
        // reads "" as null. This test pins both, and pins that the row around them survives.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-latest.head.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        var emptyYear = Assert.Single(rows, r => r.YearOfIncorporation is null);
        Assert.NotNull(emptyYear.Cik);
        Assert.NotNull(emptyYear.EntityType);
        Assert.NotNull(emptyYear.FilingDate);
        Assert.NotNull(emptyYear.TotalAmountSold);

        var emptyFirstSale = Assert.Single(rows, r => r.DateOfFirstSale is null);
        Assert.NotNull(emptyFirstSale.Cik);
        Assert.NotNull(emptyFirstSale.YearOfIncorporation);

        // And the same two shapes through a literal, so the test states the wire form rather than depending
        // on which rows the fixture happened to catch.
        var literal = JsonSerializer.Deserialize(
            """[{"yearOfIncorporation":"","dateOfFirstSale":"","cik":"0000000000"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Null(literal.YearOfIncorporation);
        Assert.Null(literal.DateOfFirstSale);
        Assert.Equal("0000000000", literal.Cik);

        // A real year stays a string. This is the user's settled decision: the wire sends a string, so the
        // SDK surfaces a string.
        var present = JsonSerializer.Deserialize(
            """[{"yearOfIncorporation":"1998","dateOfFirstSale":"2014-10-03"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Equal("1998", present.YearOfIncorporation);
        Assert.Equal(new LocalDate(2014, 10, 3), present.DateOfFirstSale);
    }

    [Fact]
    public void A_field_called_date_is_encoded_four_different_ways_across_this_group()
    {
        // The single fact that shapes six of this slice's ten records, pinned in one place. Four records
        // carry a field literally named `date`, and no two of the four agree on what it is:
        //
        //   crowdfunding-offerings        MM-DD-YYYY               -> LocalDate?  (issuer formation date)
        //   crowdfunding-offerings-search MM-DD-YYYY               -> LocalDate?  (same, null on 6.6%)
        //   fundraising / -latest         yyyy-MM-dd               -> LocalDate?
        //   fundraising-search            yyyy-MM-dd HH:mm:ss      -> Instant?    (Eastern acceptance)
        //
        // Each wrong pairing fails differently and NONE of them throws: the ISO converter nulls a
        // MM-DD-YYYY value, the MM-DD-YYYY converter nulls an ISO one, and the UTC instant converter binds
        // an Eastern timestamp four to five hours early.
        var crowdfunding = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""", FmpJsonContext.Default.ListCrowdfundingOffering)![0];
        var crowdfundingHit = JsonSerializer.Deserialize(
            """[{"date":"12-19-2022"}]""", FmpJsonContext.Default.ListCrowdfundingSearchHit)![0];
        var fundraising = JsonSerializer.Deserialize(
            """[{"date":"2026-08-28"}]""", FmpJsonContext.Default.ListFundraisingNotice)![0];
        var fundraisingHit = JsonSerializer.Deserialize(
            """[{"date":"2026-08-31 11:34:51"}]""", FmpJsonContext.Default.ListFundraisingSearchHit)![0];

        Assert.Equal(new LocalDate(2011, 11, 22), crowdfunding.Date);
        Assert.Equal(new LocalDate(2022, 12, 19), crowdfundingHit.Date);
        Assert.Equal(new LocalDate(2026, 8, 28), fundraising.Date);
        Assert.Equal(Instant.FromUtc(2026, 8, 31, 15, 34, 51), fundraisingHit.Date);   // EDT, UTC-4

        // Cross-fed, each converter answers null rather than throwing — which is the whole reason a wrong
        // pairing is silent and needs a test rather than an exception to catch it.
        Assert.Null(JsonSerializer.Deserialize(
            """[{"date":"2026-08-28"}]""", FmpJsonContext.Default.ListCrowdfundingOffering)![0].Date);
        Assert.Null(JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""", FmpJsonContext.Default.ListFundraisingNotice)![0].Date);

        // And the two `date` properties on the two three-key search records are different CLR types. This is
        // the assertion that fails if anyone merges CrowdfundingSearchHit and FundraisingSearchHit on the
        // grounds that they carry the same three key names — which they do.
        Assert.Equal(typeof(LocalDate?),
            typeof(CrowdfundingSearchHit).GetProperty(nameof(CrowdfundingSearchHit.Date))!.PropertyType);
        Assert.Equal(typeof(Instant?),
            typeof(FundraisingSearchHit).GetProperty(nameof(FundraisingSearchHit.Date))!.PropertyType);
    }

    [Fact]
    public void An_amount_above_Int32_binds_rather_than_overflowing_the_response()
    {
        // Measured 2026-08-31 over 406 rows, totalAmountSold reaches 13,475,150,514 — 6.3x Int32.MaxValue.
        // An int? property does not lose the value: System.Text.Json THROWS on the overflow and aborts the
        // whole list, so one large raise costs the caller every other row in the response.
        //
        // decimal? rather than long? for the reason recorded on FinancialScores.PiotroskiScore: all eight
        // amount fields were whole on 406 of 406 rows, but "not seen fractional yet" is not "cannot be
        // fractional", and long? inherits the same abort-the-response failure the day one arrives with cents.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-latest.head.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        var big = Assert.Single(rows, r => r.TotalAmountSold > int.MaxValue);
        Assert.NotNull(big.Cik);
        Assert.NotNull(big.TotalOfferingAmount);

        var literal = JsonSerializer.Deserialize(
            """[{"totalAmountSold":13475150514,"totalOfferingAmount":1000000000.5,"cik":"0000000000"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Equal(13475150514m, literal.TotalAmountSold);
        Assert.Equal(1000000000.5m, literal.TotalOfferingAmount);
        Assert.Equal("0000000000", literal.Cik);
    }

    [Fact]
    public void The_fundraising_search_date_is_the_acceptance_timestamp_of_the_filing()
    {
        // Not an assumption. Measured 2026-08-31 for CIK 0001617426, all 14 fundraising-search timestamps
        // equal the 14 acceptedDate values returned by fundraising?cik=... EXACTLY. The field is named
        // `date` and it is not a date; a LocalDate? here would silently discard the time of day, and the
        // UTC converter would move it four to five hours.
        var hits = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-search.Schutt.json"),
            FmpJsonContext.Default.ListFundraisingSearchHit)!;

        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal("0001617426", h.Cik));
        Assert.All(hits, h => Assert.NotNull(h.Date));

        // Every measured value falls in the Eastern 06:00-22:00 window, which is the finding that chose the
        // converter: zero of 3,174 values landed in hours 22-05, which a UTC reading would require.
        var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
        Assert.All(hits, h =>
            Assert.InRange(h.Date!.Value.InZone(eastern).Hour, 6, 22));

        // Three keys and no more, same as the crowdfunding hit and a different type from it.
        Assert.Equal(3, typeof(FundraisingSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [Fact]
    public void The_fundraising_notice_binds_all_forty_three_wire_names_and_no_others()
    {
        // 43 keys, confirmed on 2026-08-31 against the live captures and against the independent Python
        // fmpsdk, whose TypedDict carries 43 fields with an identical key set.
        var names = typeof(FundraisingNotice)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .ToList();

        Assert.Equal(43, names.Count);
        Assert.All(names, n => Assert.NotNull(n));
        Assert.Equal(43, names.Distinct(StringComparer.Ordinal).Count());

        // The two corpora are disjoint and this record must not grow the other one's fields. Measured
        // 2026-08-31: a crowdfunding CIK answers 0 rows on stable/fundraising and vice versa.
        Assert.DoesNotContain("overSubscriptionAccepted", names);
        Assert.DoesNotContain("intermediaryCompanyName", names);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/FmpDotNet.Tests
```

Expected: FAIL. `error CS0246: The type or namespace name 'FundraisingNotice' could not be found` and the
same for `FundraisingSearchHit`, plus `CS1061` for `ListFundraisingNotice` and `ListFundraisingSearchHit`.

- [ ] **Step 3: Write `FundraisingNotice`**

Create `src/FmpDotNet/Models/FundraisingNotice.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One Regulation D exempt-offering notice — a Form D filing — from <c>stable/fundraising</c> and
/// <c>stable/fundraising-latest</c>.
///
/// <para><b>Forty-three keys, in the same order on both paths</b>, verified by direct list comparison on
/// 2026-08-31 and matched field-for-field by the independent Python <c>fmpsdk</c>.</para>
///
/// <para><b>Form D filers and Form C filers are disjoint populations</b>, measured in both directions on
/// 2026-08-31: fundraising CIK <c>0001617426</c> answers <b>0 rows</b> on <c>stable/crowdfunding-offerings</c>
/// and crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> here. Both answers arrive as HTTP 200 with an
/// empty array, so a CIK sent to the wrong corpus reads exactly like a company with no filings. See
/// <see cref="CrowdfundingOffering"/>.</para>
///
/// <para><b><see cref="Date"/> here is ISO; the same field on <see cref="CrowdfundingOffering"/> is
/// <c>MM-DD-YYYY</c>.</b> Four records in this group carry a field named <c>date</c> and no two of the four
/// encodings agree. Swapping the converters is silent in both directions — each answers
/// <see langword="null"/> rather than throwing.</para>
///
/// <para><b>Two fields say "absent" with an empty string rather than with null</b> —
/// <see cref="YearOfIncorporation"/> on 30 of 100 rows and <see cref="DateOfFirstSale"/> on 7, measured
/// 2026-08-31. Both collapse to <see langword="null"/> here so that absence has one spelling.</para>
///
/// <para>Every property is nullable and the measured null counts are in the docs rather than in the
/// type.</para></summary>
public sealed record FundraisingNotice
{
    /// <summary>The issuer's SEC CIK, zero-padded to ten digits on every row measured 2026-08-31.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name as EDGAR holds it. Usually equal to <see cref="EntityName"/>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The notice's own date, <b>ISO <c>yyyy-MM-dd</c></b>.
    ///
    /// <para><b>Not the same encoding as <see cref="CrowdfundingOffering.Date"/>, which is
    /// <c>MM-DD-YYYY</c></b> — and unlike that field, this one tracks the filing rather than the company:
    /// measured 2026-08-31 it sits within days of <see cref="FilingDate"/> rather than years before it.
    /// Reaches this type through <see cref="NullableLocalDateJsonConverter"/>, which reads <c>""</c> and
    /// <c>"0000-00-00"</c> as null as well.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When the filing was submitted. <b>A date, not a timestamp</b> — <c>00:00:00</c> on 3,575 of
    /// 3,575 rows measured 2026-08-31 across both filing corpora. See
    /// <see cref="CrowdfundingOffering.FilingDate"/>.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, read as <b>Eastern</b> wall clock — the full account of the
    /// measurement is on <see cref="CrowdfundingOffering.AcceptedDate"/>. <b>This is also exactly what
    /// <see cref="FundraisingSearchHit.Date"/> carries</b>: measured 2026-08-31 for CIK <c>0001617426</c>, all
    /// 14 search timestamps matched these 14 values exactly.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form code. Two values measured 2026-08-31: <c>"D"</c> and <c>"D/A"</c>.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The form code spelled out — <c>"Notice of Exempt Offering of Securities"</c>, or the
    /// amendment wording for <c>D/A</c>.</summary>
    [JsonPropertyName("formSignification")] public string? FormSignification { get; init; }

    /// <summary>The issuing entity's name as it appears on the notice.</summary>
    [JsonPropertyName("entityName")] public string? EntityName { get; init; }

    /// <summary>The issuer's street address.</summary>
    [JsonPropertyName("issuerStreet")] public string? IssuerStreet { get; init; }

    /// <summary>The issuer's city.</summary>
    [JsonPropertyName("issuerCity")] public string? IssuerCity { get; init; }

    /// <summary>The issuer's state or country code — <c>"CA"</c>.</summary>
    [JsonPropertyName("issuerStateOrCountry")] public string? IssuerStateOrCountry { get; init; }

    /// <summary>The same jurisdiction spelled out — <c>"CALIFORNIA"</c>. Redundant with
    /// <see cref="IssuerStateOrCountry"/> and surfaced anyway, because the wire sends both and the SDK does
    /// not decide which one a caller wanted.</summary>
    [JsonPropertyName("issuerStateOrCountryDescription")]
    public string? IssuerStateOrCountryDescription { get; init; }

    /// <summary>The issuer's postal code, as a <b>string</b> — four- and five-digit forms both measured
    /// 2026-08-31. See <see cref="CrowdfundingOffering.IssuerZipCode"/>.</summary>
    [JsonPropertyName("issuerZipCode")] public string? IssuerZipCode { get; init; }

    /// <summary>The issuer's telephone number, <b>in three different formats</b>. Measured 2026-08-31 over
    /// 100 rows: <c>999-999-9999</c> on 33, <c>9999999999</c> on 18, and <c>999 999 9999</c> on 8. A caller
    /// comparing two of these strings is comparing formats, not numbers.</summary>
    [JsonPropertyName("issuerPhoneNumber")] public string? IssuerPhoneNumber { get; init; }

    /// <summary>Where the entity is incorporated — <c>"DELAWARE"</c>.</summary>
    [JsonPropertyName("jurisdictionOfIncorporation")] public string? JurisdictionOfIncorporation { get; init; }

    /// <summary>The entity's legal form. Four values measured 2026-08-31 — the same vocabulary
    /// <see cref="CrowdfundingOffering.LegalStatusForm"/> carries under a different name.</summary>
    [JsonPropertyName("entityType")] public string? EntityType { get; init; }

    /// <summary>Whether the entity was incorporated within the last five years. <b>Null on 30 of 100 rows
    /// measured 2026-08-31</b>, and the null is not a defect: a Form D filer that does not answer the
    /// question leaves it blank, and <see langword="false"/> would be a different claim.</summary>
    [JsonPropertyName("incorporatedWithinFiveYears")] public bool? IncorporatedWithinFiveYears { get; init; }

    /// <summary>The year the entity was incorporated — <b>a string, and deliberately so</b>.
    ///
    /// <para>Measured 2026-08-31 over 100 rows: <b>never null</b>, <c>""</c> on <b>30</b>, and a four-digit
    /// year on the other 70 — a JSON string in both cases. It is <b>not</b> <see cref="int"/>.
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = AllowReadingFromString</c> globally, so <c>"1998"</c>
    /// would bind — but <c>""</c> throws, and <c>System.Text.Json</c> aborts the <i>entire list</i>
    /// deserialisation rather than the one field. Thirty percent of rows would cost the caller the whole
    /// response.</para>
    ///
    /// <para>Reaches this type through <see cref="SentinelStringJsonConverter"/>, which collapses <c>""</c>
    /// (and <c>"N/A"</c> and <c>"NULL"</c>) to <see langword="null"/> so absence has one spelling. A caller
    /// who wants a number writes <c>int.Parse</c> and decides for themselves what an absent year
    /// means.</para></summary>
    [JsonPropertyName("yearOfIncorporation")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? YearOfIncorporation { get; init; }

    /// <summary>The related person's first name. <b>Carries placeholders rather than nulls</b> — <c>"-"</c>,
    /// <c>"--"</c> and <c>"N/A"</c> all measured 2026-08-31, on a field that is never null. Left unconverted
    /// on purpose: unlike <see cref="YearOfIncorporation"/>, no measurement establishes that these three
    /// spellings all mean the same thing, and collapsing them would be a guess presented as a
    /// fact.</summary>
    [JsonPropertyName("relatedPersonFirstName")] public string? RelatedPersonFirstName { get; init; }

    /// <summary>The related person's last name — which for an entity holds the whole entity name.</summary>
    [JsonPropertyName("relatedPersonLastName")] public string? RelatedPersonLastName { get; init; }

    /// <summary>The related person's street address.</summary>
    [JsonPropertyName("relatedPersonStreet")] public string? RelatedPersonStreet { get; init; }

    /// <summary>The related person's city.</summary>
    [JsonPropertyName("relatedPersonCity")] public string? RelatedPersonCity { get; init; }

    /// <summary>The related person's state or country code.</summary>
    [JsonPropertyName("relatedPersonStateOrCountry")] public string? RelatedPersonStateOrCountry { get; init; }

    /// <summary>The same jurisdiction spelled out.</summary>
    [JsonPropertyName("relatedPersonStateOrCountryDescription")]
    public string? RelatedPersonStateOrCountryDescription { get; init; }

    /// <summary>The related person's postal code, as a string.</summary>
    [JsonPropertyName("relatedPersonZipCode")] public string? RelatedPersonZipCode { get; init; }

    /// <summary>How the related person relates to the issuer — <c>"Director"</c>,
    /// <c>"Executive Officer"</c>.</summary>
    [JsonPropertyName("relatedPersonRelationship")] public string? RelatedPersonRelationship { get; init; }

    /// <summary>The issuer's industry as Form D classifies it — <c>"Pooled Investment Fund"</c>.</summary>
    [JsonPropertyName("industryGroupType")] public string? IndustryGroupType { get; init; }

    /// <summary>The issuer's revenue band as a phrase, not a number. 5 distinct values measured 2026-08-31;
    /// null on 29 of 100 rows.</summary>
    [JsonPropertyName("revenueRange")] public string? RevenueRange { get; init; }

    /// <summary>The Securities Act exemptions claimed, as a <b>comma-joined list in one string</b> —
    /// <c>"06b, 3C, 3C.7"</c>, measured 2026-08-31. Splitting it is the caller's decision; the SDK surfaces
    /// what the wire sent.</summary>
    [JsonPropertyName("federalExemptionsExclusions")] public string? FederalExemptionsExclusions { get; init; }

    /// <summary>Whether this notice amends an earlier one. Agrees with <see cref="FormType"/> being
    /// <c>"D/A"</c>. Never null in 100 rows measured 2026-08-31.</summary>
    [JsonPropertyName("isAmendment")] public bool? IsAmendment { get; init; }

    /// <summary>When the first sale under the offering occurred. <b><c>""</c> on 7 of 100 rows measured
    /// 2026-08-31</b> and never JSON null — <see cref="NullableLocalDateJsonConverter"/> already reads the
    /// empty string as null, so unlike <see cref="YearOfIncorporation"/> this one needs no sentinel
    /// converter.</summary>
    [JsonPropertyName("dateOfFirstSale")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateOfFirstSale { get; init; }

    /// <summary>Whether the offering is expected to last more than a year. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("durationOfOfferingIsMoreThanYear")]
    public bool? DurationOfOfferingIsMoreThanYear { get; init; }

    /// <summary>Whether equity is among the securities offered. <b>Null on 64 of 100 rows measured
    /// 2026-08-31</b> — the most frequently absent field on this record, and absent rather than
    /// <see langword="false"/>.</summary>
    [JsonPropertyName("securitiesOfferedAreOfEquityType")]
    public bool? SecuritiesOfferedAreOfEquityType { get; init; }

    /// <summary>Whether the offering is part of a business combination. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("isBusinessCombinationTransaction")]
    public bool? IsBusinessCombinationTransaction { get; init; }

    /// <summary>The smallest accepted investment. 0 to 5,000,000 measured 2026-08-31 over 406 rows.</summary>
    [JsonPropertyName("minimumInvestmentAccepted")] public decimal? MinimumInvestmentAccepted { get; init; }

    /// <summary>The total size of the offering. 0 to 1,000,000,000 measured 2026-08-31 over 406
    /// rows — <b>within Int32 by 0.5 orders of magnitude and typed the same as
    /// <see cref="TotalAmountSold"/> anyway</b>, because "has not overflowed yet" is not a
    /// type.</summary>
    [JsonPropertyName("totalOfferingAmount")] public decimal? TotalOfferingAmount { get; init; }

    /// <summary>How much has actually been sold.
    ///
    /// <para><b>Measured maximum 13,475,150,514 on 2026-08-31 — 6.3 times <see cref="int.MaxValue"/>.</b>
    /// An <see cref="int"/> property does not truncate that: <c>System.Text.Json</c> throws on the overflow
    /// and aborts the whole list, so one large raise costs the caller every other row in the
    /// response.</para>
    ///
    /// <para><see cref="decimal"/> rather than <see cref="long"/> for the reason recorded on
    /// <see cref="FinancialScores.PiotroskiScore"/>: all eight amount fields on this record were whole on
    /// 406 of 406 rows, and "not seen fractional yet" is not "cannot be fractional" — FMP is known to
    /// serialise counts through a float elsewhere, and <see cref="long"/> inherits the same
    /// abort-the-response failure the day one arrives with cents.</para></summary>
    [JsonPropertyName("totalAmountSold")] public decimal? TotalAmountSold { get; init; }

    /// <summary>The unsold balance. 0 to 881,533,305 measured 2026-08-31.</summary>
    [JsonPropertyName("totalAmountRemaining")] public decimal? TotalAmountRemaining { get; init; }

    /// <summary>Whether any non-accredited investor participated. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("hasNonAccreditedInvestors")] public bool? HasNonAccreditedInvestors { get; init; }

    /// <summary>How many investors have already subscribed. 0 to 10,000 measured 2026-08-31.</summary>
    [JsonPropertyName("totalNumberAlreadyInvested")] public decimal? TotalNumberAlreadyInvested { get; init; }

    /// <summary>Commissions paid on the sale. 0 to 8,000,000 measured 2026-08-31.</summary>
    [JsonPropertyName("salesCommissions")] public decimal? SalesCommissions { get; init; }

    /// <summary>Finders' fees paid. <b>Zero on all 100 rows measured 2026-08-31</b>, and surfaced anyway —
    /// zero is what the wire said, and the sweep records it as populated rather than as an absence.</summary>
    [JsonPropertyName("findersFees")] public decimal? FindersFees { get; init; }

    /// <summary>Gross proceeds applied to the uses disclosed on the notice. 0 to 8,715,408 measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("grossProceedsUsed")] public decimal? GrossProceedsUsed { get; init; }
}
```

- [ ] **Step 4: Write `FundraisingSearchHit`**

Create `src/FmpDotNet/Models/FundraisingSearchHit.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/fundraising-search</c> — three keys and nothing else.
///
/// <para><b>A row is one filing, not one company.</b> Measured 2026-08-31, <c>name=Schutt</c> answered 34
/// rows across <b>5</b> distinct CIKs. Dedupe by <see cref="Cik"/> before populating a picker; this SDK does
/// not, because the row is what the wire sent.</para>
///
/// <para><b>Three keys identical to <see cref="CrowdfundingSearchHit"/>'s, and a separate type on
/// purpose</b>: <see cref="Date"/> is an acceptance <i>timestamp</i> here and a <c>MM-DD-YYYY</c> issuer date
/// there. One record for both would need one converter for two encodings, and both wrong pairings answer
/// null rather than throwing.</para>
///
/// <para><b>This path does behave like a case-insensitive prefix match</b> — measured 2026-08-31,
/// <c>a</c> 0, <c>ab</c> 979, <c>abc</c> 56, <c>Ap</c> 421, <c>App</c> 256, <c>Apple</c>/<c>apple</c>/<c>APPLE</c>
/// 59 each, <c>pple</c> 0 — and the SDK still validates nothing, because that is upstream's rule and it will
/// go stale. Its crowdfunding sibling behaves differently and is documented as unknown.</para></summary>
public sealed record FundraisingSearchHit
{
    /// <summary>The issuer's CIK, zero-padded to ten digits. The key to
    /// <c>FundraisersEndpoints.GetFundraisingByCikAsync</c>, and the field to dedupe on.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The matched name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary><b>The filing's acceptance timestamp, not a date</b>, read as Eastern wall clock.
    ///
    /// <para>Measured 2026-08-31 for CIK <c>0001617426</c>: all <b>14</b> values here matched the 14
    /// <see cref="FundraisingNotice.AcceptedDate"/> values from <c>stable/fundraising</c> <i>exactly</i>. The
    /// field is named <c>date</c> and a <c>LocalDate?</c> would silently discard the time of day; the UTC
    /// converter for the same wire shape would move it four to five hours. The full account of the zone
    /// measurement — 3,174 values, both DST seasons, zero in hours 22-05 — is on
    /// <see cref="CrowdfundingOffering.AcceptedDate"/>.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? Date { get; init; }
}
```

**Then promote the crefs Task 2 deferred.** Both types exist now:

```bash
sed -i '' -E 's|<c>(Fundraising[A-Za-z]*(\.[A-Za-z]*)?)</c>|<see cref="\1"/>|g' \
  src/FmpDotNet/Models/CrowdfundingOffering.cs src/FmpDotNet/Models/CrowdfundingSearchHit.cs
grep -rn '<c>Fundraising' src/FmpDotNet/ || echo "no deferred FundraisingNotice/SearchHit crefs remain"
```

Expected: the echo line prints. `<c>FundraisersEndpoints…</c>` references survive on purpose — Task 4
promotes those.

- [ ] **Step 5: Register both with the serialiser**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, extend the `#39` block from Task 2:

```csharp
[JsonSerializable(typeof(List<FundraisingNotice>))]
[JsonSerializable(typeof(List<FundraisingSearchHit>))]
```

- [ ] **Step 6: Run the tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~FundraisersTests
```

Expected: PASS, 14 tests. This is the first green build of `FundraisersTests.cs` — the eight tests Task 2
wrote and the six this task added.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Models/FundraisingNotice.cs \
        src/FmpDotNet/Models/FundraisingSearchHit.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/FundraisersTests.cs
git commit -m "feat: model the two fundraising shapes, the empty-string year and the search timestamp (#39)"
```

---

### Task 4: `FundraisersEndpoints` — six methods, two paging guards that must not be merged

**Files:**
- Create: `src/FmpDotNet/Endpoints/FundraisersEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/FundraisersTests.cs` (append a `Build` helper and seven tests)

**Interfaces:**
- Consumes: `CrowdfundingOffering`, `CrowdfundingSearchHit` (Task 2), `FundraisingNotice`,
  `FundraisingSearchHit` (Task 3) and their four `FmpJsonContext` list accessors. `FmpTransport.GetListAsync`
  and `FmpRequest.With(string, string?)` / `.With(string, int?)`, all unchanged.
- Produces: `public sealed class FundraisersEndpoints(FmpTransport transport)` in `FmpDotNet.Endpoints`, with
  the six method signatures below and two `public const int` page-size constants. Task 7 adds it to
  `FmpClient` as the `Fundraisers` property and registers it in the container. Task 8's `Probe.Argument`
  dispatches on `nameof(FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync)` and
  `nameof(FundraisersEndpoints.SearchCrowdfundingOfferingsAsync)`, so those two names are load-bearing
  outside this file.

```csharp
Task<IReadOnlyList<CrowdfundingOffering>>  GetCrowdfundingOfferingsByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<CrowdfundingOffering>>  GetCrowdfundingOfferingsLatestAsync(int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<CrowdfundingSearchHit>> SearchCrowdfundingOfferingsAsync(string name, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingNotice>>     GetFundraisingByCikAsync(string cik, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingNotice>>     GetFundraisingLatestAsync(int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<FundraisingSearchHit>>  SearchFundraisingAsync(string name, CancellationToken ct = default);

public const int MaxCrowdfundingPageSize = 1000;
public const int MaxFundraisingPageSize  = 100;
```

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/FundraisersTests.cs`, inside the class. Add
`using FmpDotNet.Endpoints;`, `using Microsoft.Extensions.Options;` and `using System.Web;` to the file's
using block.

```csharp
    private static (FundraisersEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new FundraisersEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Each_of_the_six_paths_is_asked_exactly_once()
    {
        var (fundraisers, handler) = Build();

        await fundraisers.GetCrowdfundingOfferingsByCikAsync("0002010670");
        await fundraisers.GetCrowdfundingOfferingsLatestAsync();
        await fundraisers.SearchCrowdfundingOfferingsAsync("Wellness");
        await fundraisers.GetFundraisingByCikAsync("0001617426");
        await fundraisers.GetFundraisingLatestAsync();
        await fundraisers.SearchFundraisingAsync("Schutt");

        Assert.Equal(
            [
                "/stable/crowdfunding-offerings",
                "/stable/crowdfunding-offerings-latest",
                "/stable/crowdfunding-offerings-search",
                "/stable/fundraising",
                "/stable/fundraising-latest",
                "/stable/fundraising-search",
            ],
            handler.Requests.Select(u => u.AbsolutePath));
    }

    [Fact]
    public async Task The_two_paging_ceilings_differ_by_a_factor_of_ten_and_are_not_shared()
    {
        // THE test that fails if someone tidies the two paging guards into one. Measured 2026-08-31:
        // crowdfunding-offerings-latest returned 1000 rows at BOTH limit=1000 and limit=5000, while
        // fundraising-latest returned 100 at limit=1000 and 100 at limit=101. Their DEFAULTS differ by the
        // same factor of ten — 100 rows against 10. A merged guard would either reject a legal request on
        // crowdfunding or accept an illegal one on fundraising.
        var (fundraisers, handler) = Build();

        // Legal on crowdfunding, illegal on fundraising.
        await fundraisers.GetCrowdfundingOfferingsLatestAsync(limit: FundraisersEndpoints.MaxCrowdfundingPageSize);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetFundraisingLatestAsync(limit: FundraisersEndpoints.MaxCrowdfundingPageSize));

        // Legal on crowdfunding, illegal on fundraising — one past the fundraising ceiling.
        await fundraisers.GetCrowdfundingOfferingsLatestAsync(limit: FundraisersEndpoints.MaxFundraisingPageSize + 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetFundraisingLatestAsync(limit: FundraisersEndpoints.MaxFundraisingPageSize + 1));

        // Legal on both, at the fundraising ceiling.
        await fundraisers.GetFundraisingLatestAsync(limit: FundraisersEndpoints.MaxFundraisingPageSize);

        // Illegal on both.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetCrowdfundingOfferingsLatestAsync(
                limit: FundraisersEndpoints.MaxCrowdfundingPageSize + 1));

        Assert.Equal(1000, FundraisersEndpoints.MaxCrowdfundingPageSize);
        Assert.Equal(100, FundraisersEndpoints.MaxFundraisingPageSize);
        Assert.Equal(4, handler.Requests.Count);   // only the four legal calls reached the wire
    }

    [Fact]
    public async Task Zero_rows_and_a_negative_page_are_rejected_on_both_latest_paths()
    {
        // limit is rejected at zero and below rather than passed through, because measured 2026-08-31
        // limit=0 returns ONE row on both paths — not an error and not nothing. page is rejected below zero
        // because page=-1 silently returns page 0, identical first row.
        var (fundraisers, handler) = Build();

        var limitThrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetCrowdfundingOfferingsLatestAsync(limit: 0));
        var pageThrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetFundraisingLatestAsync(page: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetFundraisingLatestAsync(limit: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fundraisers.GetCrowdfundingOfferingsLatestAsync(page: -1));
        Assert.Empty(handler.Requests);

        // Both guards pattern-match into locals named `rows` and `index`; without the explicit
        // nameof(limit)/nameof(page) arguments, CallerArgumentExpression reports THOSE names instead of the
        // caller's own parameter names. Pinned so deleting those arguments goes red.
        Assert.Equal("limit", limitThrown.ParamName);
        Assert.Equal("page", pageThrown.ParamName);
    }

    [Fact]
    public async Task There_is_no_page_ceiling_on_either_latest_path()
    {
        // Measured 2026-08-31, page=1000 answered HTTP 200 with rows on BOTH -latest paths, where the News
        // feeds answer HTTP 400 past page 100. A ceiling invented here would reject requests FMP serves.
        // This follows the GetArticlesAsync precedent, and the real hazard — a page-until-empty loop that
        // never terminates — is documented on both methods rather than guarded.
        var (fundraisers, handler) = Build();

        await fundraisers.GetCrowdfundingOfferingsLatestAsync(page: 1000);
        await fundraisers.GetFundraisingLatestAsync(page: 1000);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, u =>
            Assert.Equal("1000", HttpUtility.ParseQueryString(u.Query)["page"]));
    }

    [Fact]
    public async Task An_unparameterised_latest_call_sends_no_limit_and_no_page()
    {
        // limit and page are int? rather than SDK-defaulted. An SDK-chosen default invents a page size the
        // wire did not ask for; sending nothing lets FMP's own measured defaults apply — 100 rows on
        // crowdfunding-offerings-latest and 10 on fundraising-latest, which is itself a difference a caller
        // should be able to observe rather than have papered over.
        var (fundraisers, handler) = Build();

        await fundraisers.GetCrowdfundingOfferingsLatestAsync();
        await fundraisers.GetFundraisingLatestAsync();

        Assert.All(handler.Requests, u =>
        {
            Assert.DoesNotContain("limit=", u.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("page=", u.Query, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_cik_or_name_is_rejected_before_anything_reaches_the_wire(string? blank)
    {
        // Eight of the ten paths in this group answer a naked request with HTTP 400 and a plain-text body
        // naming the missing parameter, measured 2026-08-31. Rejecting locally saves a call against the
        // key's quota and gives the caller the parameter name in an exception type they can catch.
        var (fundraisers, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fundraisers.GetCrowdfundingOfferingsByCikAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fundraisers.GetFundraisingByCikAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fundraisers.SearchCrowdfundingOfferingsAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fundraisers.SearchFundraisingAsync(blank!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Paging_is_offered_only_where_it_was_measured_to_work_and_cik_is_offered_nowhere_on_latest()
    {
        // Two absences, pinned by reflection so the measurements behind them are not lost.
        //
        // 1. PAGING. On the by-CIK paths `page` had no measured effect — fundraising?cik=... returned the
        //    same 14 rows at page=0 and page=1 — and those paths return the filer's whole history in one
        //    response. On the four search paths `limit` is IGNORED outright: measured 2026-08-31,
        //    crowdfunding-offerings-search?name=Well&limit=2 returned all 44 rows and
        //    fundraising-search?name=Apple&limit=2 all 59. A parameter the SDK offers that the wire discards
        //    is worse than no parameter.
        var withoutPaging = new[]
        {
            nameof(FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync),
            nameof(FundraisersEndpoints.SearchCrowdfundingOfferingsAsync),
            nameof(FundraisersEndpoints.GetFundraisingByCikAsync),
            nameof(FundraisersEndpoints.SearchFundraisingAsync),
        };

        foreach (var name in withoutPaging)
        {
            var parameters = typeof(FundraisersEndpoints).GetMethod(name)!
                .GetParameters().Select(p => p.Name).ToList();
            Assert.DoesNotContain("limit", parameters);
            Assert.DoesNotContain("page", parameters);
        }

        // 2. CIK ON -LATEST. Measured 2026-08-31, `cik` is HONOURED on fundraising-latest —
        //    cik=0001617426&limit=100 returned 14 rows, all one CIK, the same count
        //    GetFundraisingByCikAsync returns — and SILENTLY IGNORED on its crowdfunding sibling:
        //    crowdfunding-offerings-latest?cik=0002010670&limit=100 returned 100 rows across 85 distinct
        //    CIKs. The parameter adds no capability the by-CIK method does not already provide, and offering
        //    it on one -latest method but not the other would invite a caller to try the one that fails
        //    silently. So it is on neither, and this is the record of why.
        foreach (var name in new[]
                 {
                     nameof(FundraisersEndpoints.GetCrowdfundingOfferingsLatestAsync),
                     nameof(FundraisersEndpoints.GetFundraisingLatestAsync),
                 })
        {
            var parameters = typeof(FundraisersEndpoints).GetMethod(name)!
                .GetParameters().Select(p => p.Name).ToList();
            Assert.Equal(["limit", "page", "ct"], parameters);
        }
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/FmpDotNet.Tests
```

Expected: FAIL, `error CS0246: The type or namespace name 'FundraisersEndpoints' could not be found`.

- [ ] **Step 3: Write the facade**

Create `src/FmpDotNet/Endpoints/FundraisersEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Fundraisers — Regulation Crowdfunding (Form C) and Regulation D (Form D) offerings, six paths.
///
/// <para><b>Two corpora, three shapes each, and they do not overlap.</b> Measured 2026-08-31 in both
/// directions: crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> on the fundraising paths, and
/// fundraising CIK <c>0001617426</c> answers <b>0 rows</b> on the crowdfunding ones. That is why the methods
/// are spelled out rather than parameterised by corpus — a CIK sent to the wrong one produces HTTP 200 with
/// an empty array, which reads exactly like a company that has never filed.</para>
///
/// <para><b>Five things hold across this group, every one of them measured, and not one of them catchable by
/// a caller.</b> Every case below arrives at HTTP 200 with well-formed rows.</para>
///
/// <list type="number">
///   <item><description><b>The four non-<c>-latest</c> paths ignore paging, so this facade does not offer
///     it.</b> Measured 2026-08-31: <c>fundraising?cik=…</c> returned the same 14 rows at <c>page=0</c> and
///     <c>page=1</c>, and both search paths ignore <c>limit</c> outright —
///     <c>crowdfunding-offerings-search?name=Well&amp;limit=2</c> returned all <b>44</b> rows and
///     <c>fundraising-search?name=Apple&amp;limit=2</c> all <b>59</b>.</description></item>
///   <item><description><b>The two <c>-latest</c> paths have different ceilings and different defaults.</b>
///     <see cref="MaxCrowdfundingPageSize"/> is ten times <see cref="MaxFundraisingPageSize"/>, and their
///     defaults differ by the same factor — 100 rows against 10. The two guards are deliberately not
///     shared.</description></item>
///   <item><description><b><c>cik</c> is accepted on <c>fundraising-latest</c> and silently ignored on its
///     crowdfunding sibling, and this facade exposes it on neither.</b> Measured 2026-08-31:
///     <c>fundraising-latest?cik=0001617426&amp;limit=100</c> returned <b>14 rows, all one CIK</b>, while
///     <c>crowdfunding-offerings-latest?cik=0002010670&amp;limit=100</c> returned <b>100 rows across 85
///     distinct CIKs</b>. <see cref="GetFundraisingByCikAsync"/> already provides what the working one adds,
///     and offering the parameter on one method but not the other would invite a caller to try the one that
///     fails silently.</description></item>
///   <item><description><b>A search row is one filing, not one company.</b>
///     <c>fundraising-search?name=Schutt</c> returned 34 rows across <b>5</b> distinct CIKs;
///     <c>crowdfunding-offerings-search?name=Well</c> returned 44 across <b>31</b>. A caller populating a
///     company picker must dedupe by CIK. This SDK does not: the row is what the wire
///     sent.</description></item>
///   <item><description><b>A field called <c>date</c> means four different things across these six
///     paths.</b> <see cref="CrowdfundingOffering.Date"/> is <c>MM-DD-YYYY</c> and is the issuer's formation
///     date rather than the filing's; <see cref="FundraisingNotice.Date"/> is ISO;
///     <see cref="FundraisingSearchHit.Date"/> is an acceptance timestamp. Each record's own doc carries the
///     measurement.</description></item>
/// </list>
///
/// <para><b>Neither search path's matching rule is claimed by this SDK.</b> The fundraising one behaves like
/// a case-insensitive prefix match and the crowdfunding one refutes substring, prefix and whole-word alike —
/// see <see cref="CrowdfundingSearchHit"/>. Both take the caller's string unchanged, because the rule is
/// upstream's and it will go stale.</para></summary>
public sealed class FundraisersEndpoints(FmpTransport transport)
{
    /// <summary>The largest <c>limit</c> <c>stable/crowdfunding-offerings-latest</c> honours. Measured
    /// 2026-08-31, <c>limit=1000</c> and <c>limit=5000</c> both returned 1000 rows. FMP's own default when
    /// the parameter is omitted is <b>100</b>.</summary>
    public const int MaxCrowdfundingPageSize = 1000;

    /// <summary>The largest <c>limit</c> <c>stable/fundraising-latest</c> honours — <b>a tenth of
    /// <see cref="MaxCrowdfundingPageSize"/></b>. Measured 2026-08-31, <c>limit=1000</c> and
    /// <c>limit=101</c> both returned 100 rows. FMP's own default when the parameter is omitted is
    /// <b>10</b>.</summary>
    public const int MaxFundraisingPageSize = 100;

    /// <summary>Every Form C offering one issuer has filed, from <c>stable/crowdfunding-offerings</c>.
    ///
    /// <para><b>The filer's whole history in one response — there is no paging here and none is offered.</b>
    /// Measured 2026-08-31, <c>page=1</c> returned the same rows as <c>page=0</c>. Finlete Funding
    /// (<c>0002010670</c>) answered 48 rows.</para>
    ///
    /// <para><b>A Form D filer's CIK answers zero rows here</b>, at HTTP 200, which is indistinguishable
    /// from an issuer that has never crowdfunded. Use <see cref="GetFundraisingByCikAsync"/> for Form
    /// D.</para></summary>
    /// <param name="cik">The issuer's SEC CIK, as EDGAR writes it — zero-padded to ten digits on every
    /// measured row, though the endpoint also accepts the unpadded form. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The issuer's offerings. Empty when the CIK has no Form C filings — and equally empty when it
    /// belongs to the other corpus. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingOffering>> GetCrowdfundingOfferingsByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings").With("cik", cik),
            FmpJsonContext.Default.ListCrowdfundingOffering, ct);
    }

    /// <summary>The newest Form C offerings across every issuer, from
    /// <c>stable/crowdfunding-offerings-latest</c>.
    ///
    /// <para><b>There is no page ceiling, and that is measured rather than an oversight.</b> Measured
    /// 2026-08-31, <c>page=1000</c> answered HTTP 200 with rows, where the News feeds answer HTTP 400 past
    /// page 100. A bound invented here would reject requests FMP serves. <b>So a page-until-empty loop is
    /// the caller's to terminate</b> — paging does genuinely advance (<c>page=0</c> and <c>page=1</c> at
    /// <c>limit=5</c> shared <b>zero</b> rows and <c>acceptedDate</c> descended continuously across the
    /// boundary), but nothing here promises it ever runs out.</para>
    ///
    /// <para><b><c>cik</c> is accepted by this path and silently ignored</b> — measured 2026-08-31,
    /// <c>cik=0002010670&amp;limit=100</c> returned 100 rows across 85 distinct CIKs. It is not offered
    /// here; use <see cref="GetCrowdfundingOfferingsByCikAsync"/>.</para></summary>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCrowdfundingPageSize"/>. Omit to take FMP's own
    /// default of 100.</param>
    /// <param name="page">Zero-based page index. No upper bound — see the summary.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's offerings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxCrowdfundingPageSize"/>, or <paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingOffering>> GetCrowdfundingOfferingsLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        ThrowIfCrowdfundingPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListCrowdfundingOffering, ct);
    }

    /// <summary>Finds Form C issuers by name, from <c>stable/crowdfunding-offerings-search</c>.
    ///
    /// <para><b>The matching rule is not known, and this SDK does not claim one.</b> Measured 2026-08-31:
    /// <c>Well</c> and <c>Wellness</c> return byte-identical 44-row bodies while <c>Welln</c> and
    /// <c>Wellnes</c> return <b>zero</b>; <c>Or</c>, <c>Ora</c> and <c>Orav</c> return zero while
    /// <c>Oravanti</c> returns one. Substring, prefix and whole-word are each refuted by one of those rows.
    /// <b>An intermediate-length query returning nothing is not evidence the issuer is absent.</b></para>
    ///
    /// <para><b>FMP's documented "or platform" clause is refuted by measurement.</b> The documentation says
    /// this searches "by company name, campaign name, or platform"; <c>name=NetCapital</c> returns
    /// <b>0 rows</b>, though "NetCapital Funding Portal Inc." is the intermediary in FMP's own documented
    /// sample response, and <c>name=Wefunder</c> returns 4 rows that are all the company <i>Wefunder, Inc.</i>
    /// itself.</para>
    ///
    /// <para><b><c>limit</c> is ignored by this path and is not offered.</b> Measured 2026-08-31,
    /// <c>name=Well&amp;limit=2</c> returned all 44 rows.</para></summary>
    /// <param name="name">The name to match. Passed through unchanged — see the summary. Required and
    /// non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per matching <i>filing</i>, not per company — dedupe by
    /// <see cref="CrowdfundingSearchHit.Cik"/>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CrowdfundingSearchHit>> SearchCrowdfundingOfferingsAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/crowdfunding-offerings-search").With("name", name),
            FmpJsonContext.Default.ListCrowdfundingSearchHit, ct);
    }

    /// <summary>Every Form D notice one issuer has filed, from <c>stable/fundraising</c>.
    ///
    /// <para><b>The filer's whole history in one response.</b> Measured 2026-08-31, Schutt Private Investment
    /// Fund (<c>0001617426</c>) answered 14 rows, and <c>page=1</c> returned the same 14 — which is why no
    /// paging is offered.</para>
    ///
    /// <para><b>A Form C filer's CIK answers zero rows here</b>, at HTTP 200. Use
    /// <see cref="GetCrowdfundingOfferingsByCikAsync"/> for Form C.</para></summary>
    /// <param name="cik">The issuer's SEC CIK. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The issuer's notices. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingNotice>> GetFundraisingByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising").With("cik", cik),
            FmpJsonContext.Default.ListFundraisingNotice, ct);
    }

    /// <summary>The newest Form D notices across every issuer, from <c>stable/fundraising-latest</c>.
    ///
    /// <para><b>A tenth of its crowdfunding sibling's capacity, in both directions.</b>
    /// <see cref="MaxFundraisingPageSize"/> is 100 against 1000, and FMP's default when <c>limit</c> is
    /// omitted is 10 against 100. Measured 2026-08-31.</para>
    ///
    /// <para><b>No page ceiling</b>, same as the crowdfunding path — <c>page=1000</c> answered HTTP 200 with
    /// rows. A page-until-empty loop is the caller's to terminate.</para>
    ///
    /// <para><b><c>cik</c> is honoured by this path and is still not offered.</b> Measured 2026-08-31,
    /// <c>cik=0001617426&amp;limit=100</c> returned 14 rows all under that CIK — the same answer
    /// <see cref="GetFundraisingByCikAsync"/> gives. It adds no capability, and its crowdfunding sibling
    /// accepts the same parameter and <i>ignores</i> it, so offering it here would teach a caller to reach
    /// for the one that fails silently.</para></summary>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxFundraisingPageSize"/>. Omit to take FMP's own
    /// default of 10.</param>
    /// <param name="page">Zero-based page index. No upper bound — see the summary.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's notices, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxFundraisingPageSize"/>, or <paramref name="page"/> is negative.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingNotice>> GetFundraisingLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        ThrowIfFundraisingPagingOutOfRange(limit, page);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListFundraisingNotice, ct);
    }

    /// <summary>Finds Form D issuers by name, from <c>stable/fundraising-search</c>.
    ///
    /// <para><b>This one does behave like a case-insensitive prefix match</b>, measured 2026-08-31:
    /// <c>a</c> 0, <c>ab</c> 979, <c>abc</c> 56, <c>Ap</c> 421, <c>App</c> 256,
    /// <c>Apple</c>/<c>apple</c>/<c>APPLE</c> 59 each, <c>pple</c> 0. The SDK still validates nothing,
    /// because that is upstream's rule and it will go stale — and its crowdfunding sibling, which looks like
    /// the same endpoint, does <b>not</b> behave this way.</para>
    ///
    /// <para><b><c>limit</c> is ignored by this path and is not offered.</b> Measured 2026-08-31,
    /// <c>name=Apple&amp;limit=2</c> returned all 59 rows.</para></summary>
    /// <param name="name">The name to match. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per matching <i>filing</i> — dedupe by <see cref="FundraisingSearchHit.Cik"/>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundraisingSearchHit>> SearchFundraisingAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/fundraising-search").With("name", name),
            FmpJsonContext.Default.ListFundraisingSearchHit, ct);
    }

    /// <summary>Rejects paging <c>stable/crowdfunding-offerings-latest</c> cannot serve.
    ///
    /// <para><b>Deliberately NOT shared with <see cref="ThrowIfFundraisingPagingOutOfRange"/>, and merging
    /// the two would be a defect rather than a tidy-up.</b> The two <c>-latest</c> paths measured different
    /// ceilings on 2026-08-31: this one returned 1000 rows at both <c>limit=1000</c> and <c>limit=5000</c>,
    /// while its sibling returned 100 at <c>limit=1000</c> and 100 at <c>limit=101</c>. Their defaults differ
    /// by the same factor of ten. A merged guard would either reject a legal request here or accept an
    /// illegal one there. <c>FundraisersTests</c> has a test for each direction.</para>
    ///
    /// <para><b>There is no upper bound on <c>page</c>, on purpose.</b> Measured 2026-08-31, <c>page=1000</c>
    /// answered HTTP 200 with rows. A ceiling invented here would reject requests FMP serves, and the real
    /// hazard — a page-until-empty loop that never terminates — is not something a bound can fix. It is
    /// documented on <see cref="GetCrowdfundingOfferingsLatestAsync"/> instead.</para>
    ///
    /// <para><c>limit</c> is rejected at zero and below rather than passed on, because <c>limit=0</c>
    /// returns <b>one row</b> — not an error, and not nothing.</para></summary>
    private static void ThrowIfCrowdfundingPagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxCrowdfundingPageSize, nameof(limit));
        }

        if (page is { } index) ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
    }

    /// <summary>Rejects paging <c>stable/fundraising-latest</c> cannot serve — a tenth of what its
    /// crowdfunding sibling accepts.
    ///
    /// <para>See <see cref="ThrowIfCrowdfundingPagingOutOfRange"/> for why these are two methods and not
    /// one. The distinct names are what make the divergence legible at every call site.</para></summary>
    private static void ThrowIfFundraisingPagingOutOfRange(int? limit, int? page)
    {
        if (limit is { } rows)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxFundraisingPageSize, nameof(limit));
        }

        if (page is { } index) ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(page));
    }
}
```

**Then promote the crefs Tasks 2 and 3 deferred.** `CrowdfundingSearchHit` and `FundraisingSearchHit` each
name a method on this facade, written as `<c>` because the facade did not exist:

```bash
sed -i '' -E 's|<c>(FundraisersEndpoints\.[A-Za-z]*)</c>|<see cref="Endpoints.\1"/>|g' \
  src/FmpDotNet/Models/CrowdfundingSearchHit.cs src/FmpDotNet/Models/FundraisingSearchHit.cs
grep -rn '<c>FundraisersEndpoints' src/FmpDotNet/ || echo "no deferred FundraisersEndpoints crefs remain"
```

Expected: the echo line prints. The `Endpoints.` prefix is needed because both records live in
`FmpDotNet.Models` and the facade in `FmpDotNet.Endpoints`.

- [ ] **Step 4: Run the tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~FundraisersTests
```

Expected: PASS, 23 tests.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/Endpoints/FundraisersEndpoints.cs tests/FmpDotNet.Tests/FundraisersTests.cs
git commit -m "feat: add the fmp.Fundraisers facade — six paths, two unshared paging guards (#39)"
```

---

### Task 5: The four DCF response records — two pairs, each pair two types on purpose

**Files:**
- Create: `src/FmpDotNet/Models/DcfValuation.cs` (`DcfValuation` **and** `LeveredDcfValuation`)
- Create: `src/FmpDotNet/Models/CustomDcfProjection.cs` (`CustomDcfProjection` **and**
  `CustomLeveredDcfProjection`)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (four entries)
- Create: `tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs`

**Interfaces:**
- Consumes: `discounted-cash-flow.AAPL.json`, `levered-discounted-cash-flow.AAPL.json`,
  `custom-discounted-cash-flow.AAPL.json` and `custom-levered-discounted-cash-flow.AAPL.json` from Task 1.
  `NullableLocalDateJsonConverter` (`NodaConverters.cs:37`), unchanged.
- Produces: four `public sealed record` types in `FmpDotNet.Models` — `DcfValuation` (4 properties),
  `LeveredDcfValuation` (4), `CustomDcfProjection` (47), `CustomLeveredDcfProjection` (34) — and four
  `FmpJsonContext` list accessors. Task 6 returns one of each from its four methods.

**The pairs are pairs, not duplicates.** Both splits are settled decisions and a reviewer must not "simplify"
either:

- `DcfValuation` / `LeveredDcfValuation` share a wire shape exactly — `symbol`, `date`, `dcf`,
  `Stock Price` — so the split buys nothing structural. It buys type safety over a number that **diverges
  enormously**: measured 2026-08-27/31, KO reads **83.71** unlevered against **49.77** levered, a 41% gap,
  and JPM 728.00 against 907.85. Neither is "the" DCF. With one record a variable that has drifted from the
  call that produced it is indistinguishable from the other model's answer; with two, passing one where the
  other is expected does not compile. The independent Python `fmpsdk` reached the same conclusion and says so
  in a comment on its type.
- `CustomDcfProjection` / `CustomLeveredDcfProjection` share **29** keys; 18 are unlevered-only and 5
  levered-only.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The four Discounted Cash Flow paths, checked against captures taken live 2026-08-31.</summary>
public class DiscountedCashFlowTests
{
    [Fact]
    public void Both_plain_valuations_bind_all_four_keys_including_the_one_with_a_space_in_it()
    {
        // `Stock Price` is capitalised and contains a space. It is already documented for dcf-bulk's CSV on
        // BulkDiscountedCashFlow; it appears here in JSON. The Python fmpsdk had to abandon class-body
        // TypedDict syntax for this field because a Python identifier cannot contain a space — an
        // independent confirmation that the space is real and not a transcription slip.
        var unlevered = JsonSerializer.Deserialize(
            Binding.Fixture("discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListDcfValuation)!;
        var levered = JsonSerializer.Deserialize(
            Binding.Fixture("levered-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListLeveredDcfValuation)!;

        var u = Assert.Single(unlevered);
        var l = Assert.Single(levered);

        Assert.Empty(Binding.Unbound(u));
        Assert.Empty(Binding.Unbound(l));
        Assert.Equal("AAPL", u.Symbol);
        Assert.Equal("AAPL", l.Symbol);
        Assert.NotNull(u.StockPrice);
        Assert.NotNull(l.StockPrice);

        // The wire name, spelled exactly. A [JsonPropertyName("stockPrice")] binds nothing and leaves the
        // property null on every row, silently.
        Assert.Equal("Stock Price", typeof(DcfValuation)
            .GetProperty(nameof(DcfValuation.StockPrice))!
            .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
        Assert.Equal("Stock Price", typeof(LeveredDcfValuation)
            .GetProperty(nameof(LeveredDcfValuation.StockPrice))!
            .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
    }

    [Fact]
    public void The_levered_and_unlevered_valuations_are_two_types_that_cannot_be_assigned_to_each_other()
    {
        // The split is the point. Measured 2026-08-27/31, KO reads 83.71 unlevered against 49.77 levered —
        // a 41% gap — and JPM 728.00 against 907.85. Neither is "the" DCF, and a single record would let a
        // variable that has drifted from its call site pass silently for the other model's answer.
        //
        // This is a compile-time guarantee, so the test asserts what reflection can see: two distinct types,
        // neither assignable to the other, carrying the same four wire names.
        Assert.NotEqual(typeof(DcfValuation), typeof(LeveredDcfValuation));
        Assert.False(typeof(DcfValuation).IsAssignableFrom(typeof(LeveredDcfValuation)));
        Assert.False(typeof(LeveredDcfValuation).IsAssignableFrom(typeof(DcfValuation)));

        static string[] WireNames(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)];

        // Sorted, because GetProperties does not promise declaration order.
        Assert.Equal(["Stock Price", "date", "dcf", "symbol"],
            WireNames(typeof(DcfValuation)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            WireNames(typeof(DcfValuation)).OrderBy(n => n, StringComparer.Ordinal),
            WireNames(typeof(LeveredDcfValuation)).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void The_custom_projection_year_arrives_as_a_JSON_string_and_binds_as_an_int()
    {
        // The wire sends "2030", quoted. FmpJsonContext sets NumberHandling = AllowReadingFromString
        // globally, so it binds to int? with no converter at all — and this test is what proves the global
        // setting is doing that work, because deleting it would null this field on every row.
        //
        // int? rather than decimal? here, against the rule the rest of this slice follows, precisely BECAUSE
        // the value is quoted: a quoted year cannot arrive as 9.0 the way an unquoted number can.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("custom-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListCustomDcfProjection)!;

        Assert.Equal(2, rows.Count);
        Assert.Equal(2030, rows[0].Year);
        Assert.Equal(2029, rows[1].Year);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));

        var levered = JsonSerializer.Deserialize(
            Binding.Fixture("custom-levered-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListCustomLeveredDcfProjection)!;

        Assert.Equal(2, levered.Count);
        Assert.Equal(2030, levered[0].Year);
        Assert.All(levered, r => Assert.Empty(Binding.Unbound(r)));

        // Ten rows per response measured 2026-08-31, descending 2030 -> 2021; the fixture holds the first
        // two. Nothing on the wire marks which rows are history and which are forecast — two fields imply
        // two different boundaries — so the SDK surfaces Year and lets the caller decide.
        Assert.True(rows[0].Year > rows[1].Year);
    }

    [Fact]
    public void The_lowercase_o_in_costofDebt_is_reproduced_exactly()
    {
        // The only field in this group that breaks camelCase — `costofDebt`, with a lowercase o in "of".
        // Confirmed on the wire AND in the Python fmpsdk's type. A [JsonPropertyName("costOfDebt")] binds
        // nothing and leaves the property null on every row, on a nullable decimal that gives no hint.
        //
        // Note the contrast with `costOfEquity` beside it, which IS camelCase. The two sit next to each
        // other in the response and only one of them is misspelled.
        foreach (var (type, property) in new (Type, string)[]
                 {
                     (typeof(CustomDcfProjection), nameof(CustomDcfProjection.CostOfDebt)),
                     (typeof(CustomLeveredDcfProjection), nameof(CustomLeveredDcfProjection.CostOfDebt)),
                 })
        {
            Assert.Equal("costofDebt", type.GetProperty(property)!
                .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
        }

        var row = JsonSerializer.Deserialize(
            """[{"costofDebt":4.48,"costOfEquity":8.31,"taxRateCash":16785417,"taxRate":15.61}]""",
            FmpJsonContext.Default.ListCustomDcfProjection)![0];

        Assert.Equal(4.48m, row.CostOfDebt);
        Assert.Equal(8.31m, row.CostOfEquity);

        // taxRateCash is a CASH TAX AMOUNT in dollars, not a rate — 13.3M to 24.1M for AAPL measured
        // 2026-08-31 — while taxRate beside it reads 15.61. The SDK keeps FMP's name and says so in the doc
        // rather than renaming a field the caller will look up in FMP's own documentation.
        Assert.Equal(16785417m, row.TaxRateCash);
        Assert.Equal(15.61m, row.TaxRate);
    }

    [Fact]
    public void The_two_custom_shapes_share_twenty_nine_keys_and_disagree_on_twenty_three()
    {
        // 47 and 34 keys, confirmed twice on 2026-08-31: against the live captures, and against the
        // independent Python fmpsdk, whose TypedDicts carry 47 and 34 fields with identical key sets.
        static HashSet<string> WireNames(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)];

        var unlevered = WireNames(typeof(CustomDcfProjection));
        var levered = WireNames(typeof(CustomLeveredDcfProjection));

        Assert.Equal(47, unlevered.Count);
        Assert.Equal(34, levered.Count);
        Assert.Equal(29, unlevered.Intersect(levered, StringComparer.Ordinal).Count());
        Assert.Equal(18, unlevered.Except(levered, StringComparer.Ordinal).Count());
        Assert.Equal(5, levered.Except(unlevered, StringComparer.Ordinal).Count());

        // The five levered-only names, spelled out — this is the half of the split a merged record would
        // have to make nullable-and-meaningless on the other path.
        Assert.Equal(
            ["freeCashFlow", "operatingCashFlow", "operatingCashFlowPercentage", "pvLfcf", "sumPvLfcf"],
            levered.Except(unlevered, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/FmpDotNet.Tests
```

Expected: FAIL with `CS0246` for `DcfValuation`, `LeveredDcfValuation`, `CustomDcfProjection` and
`CustomLeveredDcfProjection`.

- [ ] **Step 3: Write the two plain valuation records**

Create `src/FmpDotNet/Models/DcfValuation.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's own <b>unlevered</b> discounted-cash-flow valuation for one symbol, from
/// <c>stable/discounted-cash-flow</c>.
///
/// <para><b>A stored daily value, not a live calculation.</b> Measured 2026-08-31, AAPL read
/// <c>dcf = 145.66380328033068</c> against <c>Stock Price = 319.7</c>, identical to all 14 decimal places
/// across captures taken minutes apart — while <c>stable/custom-discounted-cash-flow</c> recomputed off a
/// price that moved 314.74 → 314.85 → 314.87 over the same window.</para>
///
/// <para><b>Do not reconcile this against any other price the SDK carries.</b> The two DCF families' price
/// columns disagree in <i>both</i> directions: AAPL -4.83, MSFT -2.50, XOM <b>+2.50</b>, measured 2026-08-31.
/// Five symbols captured back to back agreed on their valuations to within ±0.18 and matched exactly on
/// <b>none</b>, with the sign inconsistent (XOM +0.03 against AAPL -0.06). This replicates the finding already
/// documented on <see cref="ExchangeVariant.DcfDiff"/>, measured 2026-08-27 on a different pair of
/// paths.</para></summary>
public sealed record DcfValuation
{
    /// <summary>The ticker, uppercased by FMP. Measured 2026-08-31, <c>symbol=aapl</c> answers
    /// <c>"AAPL"</c> with values byte-identical to the uppercase call — which is why this facade has no
    /// uppercase guard, unlike the News searches.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The valuation date, ISO <c>yyyy-MM-dd</c>. The day FMP computed the stored value.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The unlevered per-share valuation.
    ///
    /// <para><b>Not comparable with <see cref="LeveredDcfValuation.Dcf"/>, and the gap is not small.</b>
    /// Measured 2026-08-27/31: KO <b>83.71</b> here against <b>49.77</b> levered — 41% — and JPM 728.00
    /// against 907.85, in the opposite direction. The two answer different valuation questions and neither
    /// is "the" DCF.</para></summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>The market price FMP compared the valuation against.
    ///
    /// <para><b>The wire name is <c>Stock Price</c> — capitalised, with a space.</b> Reproduced exactly; a
    /// <c>[JsonPropertyName("stockPrice")]</c> binds nothing and leaves this null on every row. Already
    /// documented for <c>dcf-bulk</c>'s CSV on <c>BulkDiscountedCashFlow</c>; it appears in JSON
    /// here.</para>
    ///
    /// <para><b>Do not reconstruct a price from this field.</b> See the type's summary.</para></summary>
    [JsonPropertyName("Stock Price")] public decimal? StockPrice { get; init; }
}

/// <summary>FMP's own <b>levered</b> discounted-cash-flow valuation for one symbol, from
/// <c>stable/levered-discounted-cash-flow</c>.
///
/// <para><b>Deliberately not shared with <see cref="DcfValuation"/> despite the identical field set.</b>
/// Unlevered and levered DCF answer different valuation questions, and the numbers diverge enormously —
/// measured 2026-08-27/31, KO reads 83.71 unlevered against <b>49.77</b> here, a 41% gap, and JPM 728.00
/// against 907.85 in the opposite direction. With one record a variable that has drifted from the call that
/// produced it is indistinguishable from the other model's answer; with two, passing one where the other is
/// expected does not compile. The independent Python <c>fmpsdk</c> made the same split, with the same
/// reasoning recorded on its type.</para>
///
/// <para>Everything else — the stored-daily-value behaviour, the <c>Stock Price</c> spelling, the refusal to
/// reconcile prices across paths — is as <see cref="DcfValuation"/> records it.</para></summary>
public sealed record LeveredDcfValuation
{
    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The valuation date, ISO <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The levered per-share valuation. <b>Not comparable with <see cref="DcfValuation.Dcf"/></b> —
    /// see the type's summary for the measured gap.</summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>The market price FMP compared the valuation against. Wire name <c>Stock Price</c>,
    /// capitalised and with a space — see <see cref="DcfValuation.StockPrice"/>.</summary>
    [JsonPropertyName("Stock Price")] public decimal? StockPrice { get; init; }
}
```

- [ ] **Step 4: Write the two custom projection records**

Create `src/FmpDotNet/Models/CustomDcfProjection.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One projected year of an <b>unlevered</b> custom discounted-cash-flow model, from
/// <c>stable/custom-discounted-cash-flow</c> — 47 keys.
///
/// <para><b>Ten rows per response, descending 2030 → 2021, mixing history and forecast — and the wire
/// carries no field saying which is which.</b> Measured 2026-08-31, two fields imply two different
/// boundaries: <see cref="RevenuePercentage"/> jitters through 2024 and smooths from 2025, while
/// <see cref="TaxRateCash"/> is constant at 16,785,417 for 2026-2030. The measurement declined to pick a
/// line and so does this SDK: <see cref="Year"/> is surfaced and the caller decides.</para>
///
/// <para><b>This path recomputes off a live price; <see cref="DcfValuation"/> is a stored daily value.</b>
/// Measured 2026-08-31, <see cref="Price"/> moved 314.74 → 314.85 → 314.87 across captures minutes apart
/// while the plain path's figures did not change at all. The two families' price columns disagree in both
/// directions — AAPL -4.83, MSFT -2.50, XOM +2.50 — so <b>do not reconcile a price across these
/// endpoints</b>.</para>
///
/// <para><b>Every numeric is <see cref="decimal"/>, and the ranges are why.</b> Measured 2026-08-31 over 290
/// rows including override probes: <see cref="Revenue"/> reaches 4.16 × 10¹⁶ and <see cref="TerminalValue"/>
/// 2.07 × 10¹⁷, while <see cref="EquityValuePerShare"/> was fractional on 289 of 290 and reached
/// <b>-1,498.72</b>. <see cref="Year"/> is the one exception, and quoting is what earns it — see its own
/// doc.</para></summary>
public sealed record CustomDcfProjection
{
    /// <summary>The projected fiscal year.
    ///
    /// <para><b>The wire sends a JSON <i>string</i> — <c>"2030"</c>, quoted.</b> It binds to
    /// <see cref="int"/> with no converter because <c>FmpJsonContext</c> sets
    /// <c>NumberHandling = AllowReadingFromString</c> globally. That quoting is also what makes
    /// <see cref="int"/> safe here where the rest of this record is <see cref="decimal"/>: a quoted value
    /// cannot arrive as <c>9.0</c>.</para></summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Projected revenue for the year.</summary>
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }

    /// <summary>Revenue growth for the year, as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.RevenueGrowthPct"/>. <b>Jitters through 2024 and smooths from
    /// 2025</b>, measured 2026-08-31 — one of the two fields that hint at where history ends.</summary>
    [JsonPropertyName("revenuePercentage")] public decimal? RevenuePercentage { get; init; }

    /// <summary>Projected EBITDA.</summary>
    [JsonPropertyName("ebitda")] public decimal? Ebitda { get; init; }

    /// <summary>EBITDA as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.EbitdaPct"/> — <b>which the levered path silently ignores</b>.</summary>
    [JsonPropertyName("ebitdaPercentage")] public decimal? EbitdaPercentage { get; init; }

    /// <summary>Projected EBIT.</summary>
    [JsonPropertyName("ebit")] public decimal? Ebit { get; init; }

    /// <summary>EBIT as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.EbitPct"/>.</summary>
    [JsonPropertyName("ebitPercentage")] public decimal? EbitPercentage { get; init; }

    /// <summary>Projected depreciation and amortisation.</summary>
    [JsonPropertyName("depreciation")] public decimal? Depreciation { get; init; }

    /// <summary>Depreciation as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.DepreciationAndAmortizationPct"/>.</summary>
    [JsonPropertyName("depreciationPercentage")] public decimal? DepreciationPercentage { get; init; }

    /// <summary>Projected cash and short-term investments.</summary>
    [JsonPropertyName("totalCash")] public decimal? TotalCash { get; init; }

    /// <summary>Cash as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.CashAndShortTermInvestmentsPct"/>.</summary>
    [JsonPropertyName("totalCashPercentage")] public decimal? TotalCashPercentage { get; init; }

    /// <summary>Projected receivables.</summary>
    [JsonPropertyName("receivables")] public decimal? Receivables { get; init; }

    /// <summary>Receivables as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.ReceivablesPct"/>.</summary>
    [JsonPropertyName("receivablesPercentage")] public decimal? ReceivablesPercentage { get; init; }

    /// <summary>Projected inventories.</summary>
    [JsonPropertyName("inventories")] public decimal? Inventories { get; init; }

    /// <summary>Inventories as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.InventoriesPct"/>.</summary>
    [JsonPropertyName("inventoriesPercentage")] public decimal? InventoriesPercentage { get; init; }

    /// <summary>Projected payables.</summary>
    [JsonPropertyName("payable")] public decimal? Payable { get; init; }

    /// <summary>Payables as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.PayablePct"/>.</summary>
    [JsonPropertyName("payablePercentage")] public decimal? PayablePercentage { get; init; }

    /// <summary>Projected capital expenditure. <b>Negative</b> on measured rows.</summary>
    [JsonPropertyName("capitalExpenditure")] public decimal? CapitalExpenditure { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.CapitalExpenditurePct"/>.</summary>
    [JsonPropertyName("capitalExpenditurePercentage")]
    public decimal? CapitalExpenditurePercentage { get; init; }

    /// <summary>The share price the model is running against. <b>Live, and it moves between calls</b> —
    /// 314.74 → 314.85 → 314.87 for AAPL across captures minutes apart on 2026-08-31.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The beta used. Overridden by <see cref="CustomDcfAssumptions.Beta"/>.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Diluted shares outstanding. 2,793,700,000 to 15,004,697,000 measured 2026-08-31 — above
    /// Int32 on every measured row.</summary>
    [JsonPropertyName("dilutedSharesOutstanding")] public decimal? DilutedSharesOutstanding { get; init; }

    /// <summary>Cost of debt, as a percentage.
    ///
    /// <para><b>The wire name is <c>costofDebt</c> — a lowercase <c>o</c> in "of", the only field in this
    /// group that breaks camelCase.</b> Confirmed on the wire and in the independent Python <c>fmpsdk</c>'s
    /// type. Note <see cref="CostOfEquity"/> sitting beside it <i>is</i> camelCase: only one of the pair is
    /// misspelled, which is exactly the shape a copy-paste gets wrong. A test pins it.</para>
    ///
    /// <para>Overridden by <see cref="CustomDcfAssumptions.CostOfDebt"/>, whose query parameter <b>is</b>
    /// spelled <c>costOfDebt</c>. The wire is inconsistent between request and response; the SDK reproduces
    /// each side as it is.</para></summary>
    [JsonPropertyName("costofDebt")] public decimal? CostOfDebt { get; init; }

    /// <summary>The tax rate as a percentage — 15.61 to 30.11 measured 2026-08-31. <b>Not to be confused
    /// with <see cref="TaxRateCash"/></b>, which is an amount. Overridden by
    /// <see cref="CustomDcfAssumptions.TaxRate"/>.</summary>
    [JsonPropertyName("taxRate")] public decimal? TaxRate { get; init; }

    /// <summary>Cost of debt after tax, as a percentage.</summary>
    [JsonPropertyName("afterTaxCostOfDebt")] public decimal? AfterTaxCostOfDebt { get; init; }

    /// <summary>The risk-free rate as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.RiskFreeRate"/>.</summary>
    [JsonPropertyName("riskFreeRate")] public decimal? RiskFreeRate { get; init; }

    /// <summary>The equity risk premium as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.MarketRiskPremium"/>.</summary>
    [JsonPropertyName("marketRiskPremium")] public decimal? MarketRiskPremium { get; init; }

    /// <summary>Cost of equity as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.CostOfEquity"/> — <b>the eighteenth override, found by reading the
    /// Python <c>fmpsdk</c> rather than by probing</b>, and honoured on both custom paths.</summary>
    [JsonPropertyName("costOfEquity")] public decimal? CostOfEquity { get; init; }

    /// <summary>Total debt.</summary>
    [JsonPropertyName("totalDebt")] public decimal? TotalDebt { get; init; }

    /// <summary>Total equity at market value.</summary>
    [JsonPropertyName("totalEquity")] public decimal? TotalEquity { get; init; }

    /// <summary>Debt plus equity.</summary>
    [JsonPropertyName("totalCapital")] public decimal? TotalCapital { get; init; }

    /// <summary>Debt's share of total capital, as a percentage.</summary>
    [JsonPropertyName("debtWeighting")] public decimal? DebtWeighting { get; init; }

    /// <summary>Equity's share of total capital, as a percentage.</summary>
    [JsonPropertyName("equityWeighting")] public decimal? EquityWeighting { get; init; }

    /// <summary>The weighted average cost of capital, as a percentage — 5.28 to 45.96 measured 2026-08-31.
    /// <b>A <see cref="LongTermGrowthRate"/> at or above this inverts the terminal-value denominator</b> and
    /// FMP returns the negative result rather than rejecting the input; see
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>.</summary>
    [JsonPropertyName("wacc")] public decimal? Wacc { get; init; }

    /// <summary><b>A cash tax <i>amount</i> in dollars, not a rate</b> — 13,113,384 to 24,100,000 for AAPL
    /// measured 2026-08-31, while <see cref="TaxRate"/> beside it reads 15.61. The SDK keeps FMP's name
    /// rather than renaming a field a caller will look up in FMP's own documentation, and says here what it
    /// actually contains. <b>Constant at 16,785,417 for 2026-2030</b> on the measured response — one of the
    /// two fields that hint at where history ends.</summary>
    [JsonPropertyName("taxRateCash")] public decimal? TaxRateCash { get; init; }

    /// <summary>Earnings before interest after tax.</summary>
    [JsonPropertyName("ebiat")] public decimal? Ebiat { get; init; }

    /// <summary>Unlevered free cash flow for the year. <b>Levered-only in reverse</b>: the levered shape has
    /// no counterpart to this field and carries <c>freeCashFlow</c> instead.</summary>
    [JsonPropertyName("ufcf")] public decimal? Ufcf { get; init; }

    /// <summary>The sum of present-valued unlevered free cash flows. Moves when
    /// <see cref="CustomDcfAssumptions.CostOfEquity"/> is supplied.</summary>
    [JsonPropertyName("sumPvUfcf")] public decimal? SumPvUfcf { get; init; }

    /// <summary>The terminal growth rate as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>; -3.7 to 10 measured 2026-08-31.</summary>
    [JsonPropertyName("longTermGrowthRate")] public decimal? LongTermGrowthRate { get; init; }

    /// <summary>The terminal value.</summary>
    [JsonPropertyName("terminalValue")] public decimal? TerminalValue { get; init; }

    /// <summary>The terminal value discounted to today.</summary>
    [JsonPropertyName("presentTerminalValue")] public decimal? PresentTerminalValue { get; init; }

    /// <summary>The enterprise value the model arrives at.</summary>
    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }

    /// <summary>Debt less cash.</summary>
    [JsonPropertyName("netDebt")] public decimal? NetDebt { get; init; }

    /// <summary>Enterprise value less net debt.</summary>
    [JsonPropertyName("equityValue")] public decimal? EquityValue { get; init; }

    /// <summary>The model's per-share answer. <b>Can be deeply negative</b> — measured -1,498.72 on
    /// 2026-08-31 when a terminal growth rate at or above <see cref="Wacc"/> was supplied. FMP returns it
    /// rather than rejecting the input, and this SDK does not invent a bound FMP does not
    /// enforce.</summary>
    [JsonPropertyName("equityValuePerShare")] public decimal? EquityValuePerShare { get; init; }

    /// <summary>Free cash flow in the first terminal year.</summary>
    [JsonPropertyName("freeCashFlowT1")] public decimal? FreeCashFlowT1 { get; init; }
}

/// <summary>One projected year of a <b>levered</b> custom discounted-cash-flow model, from
/// <c>stable/custom-levered-discounted-cash-flow</c> — 34 keys.
///
/// <para><b>Deliberately not merged with <see cref="CustomDcfProjection"/>.</b> The two share 29 keys; 18 are
/// unlevered-only and 5 levered-only. A merged record would carry 23 properties that are null on whichever
/// path the caller happened to use, on a type that gives no hint which half is live.</para>
///
/// <para><b>And the split is not cosmetic — it is what makes the assumption vocabularies checkable.</b> This
/// path honours <see cref="CustomLeveredDcfAssumptions.OperatingCashFlowPct"/> and <b>silently ignores</b>
/// seven overrides its unlevered sibling honours. The independent Python <c>fmpsdk</c> assembles both calls
/// through one shared 18-parameter helper, which means eight of its eighteen levered parameters do nothing.
/// Two records make that a compile error.</para>
///
/// <para>Everything the two shapes share — the ten descending rows with no actual/projected flag, the live
/// price, the <see cref="CostOfDebt"/> misspelling, the <see cref="decimal"/> typing — is recorded on
/// <see cref="CustomDcfProjection"/>.</para></summary>
public sealed record CustomLeveredDcfProjection
{
    /// <summary>The projected fiscal year. Arrives as a quoted JSON string — see
    /// <see cref="CustomDcfProjection.Year"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Projected revenue for the year.</summary>
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }

    /// <summary>Revenue growth for the year, as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.RevenueGrowthPct"/>.</summary>
    [JsonPropertyName("revenuePercentage")] public decimal? RevenuePercentage { get; init; }

    /// <summary>Projected capital expenditure.</summary>
    [JsonPropertyName("capitalExpenditure")] public decimal? CapitalExpenditure { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CapitalExpenditurePct"/>.</summary>
    [JsonPropertyName("capitalExpenditurePercentage")]
    public decimal? CapitalExpenditurePercentage { get; init; }

    /// <summary>The share price the model is running against. Live, and it moves between calls.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The beta used. Overridden by <see cref="CustomLeveredDcfAssumptions.Beta"/>.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Diluted shares outstanding.</summary>
    [JsonPropertyName("dilutedSharesOutstanding")] public decimal? DilutedSharesOutstanding { get; init; }

    /// <summary>Cost of debt, as a percentage. <b>Wire name <c>costofDebt</c>, with a lowercase
    /// <c>o</c></b> — see <see cref="CustomDcfProjection.CostOfDebt"/>. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CostOfDebt"/>.</summary>
    [JsonPropertyName("costofDebt")] public decimal? CostOfDebt { get; init; }

    /// <summary>The tax rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.TaxRate"/>.</summary>
    [JsonPropertyName("taxRate")] public decimal? TaxRate { get; init; }

    /// <summary>Cost of debt after tax, as a percentage.</summary>
    [JsonPropertyName("afterTaxCostOfDebt")] public decimal? AfterTaxCostOfDebt { get; init; }

    /// <summary>The risk-free rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.RiskFreeRate"/>.</summary>
    [JsonPropertyName("riskFreeRate")] public decimal? RiskFreeRate { get; init; }

    /// <summary>The equity risk premium as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.MarketRiskPremium"/>.</summary>
    [JsonPropertyName("marketRiskPremium")] public decimal? MarketRiskPremium { get; init; }

    /// <summary>Cost of equity as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CostOfEquity"/>, which moves this,
    /// <see cref="Wacc"/>, <see cref="TerminalValue"/>, <see cref="PresentTerminalValue"/>,
    /// <see cref="PvLfcf"/> and <see cref="SumPvLfcf"/> — measured 2026-08-31.</summary>
    [JsonPropertyName("costOfEquity")] public decimal? CostOfEquity { get; init; }

    /// <summary>Total debt.</summary>
    [JsonPropertyName("totalDebt")] public decimal? TotalDebt { get; init; }

    /// <summary>Total equity at market value.</summary>
    [JsonPropertyName("totalEquity")] public decimal? TotalEquity { get; init; }

    /// <summary>Debt plus equity.</summary>
    [JsonPropertyName("totalCapital")] public decimal? TotalCapital { get; init; }

    /// <summary>Debt's share of total capital, as a percentage.</summary>
    [JsonPropertyName("debtWeighting")] public decimal? DebtWeighting { get; init; }

    /// <summary>Equity's share of total capital, as a percentage.</summary>
    [JsonPropertyName("equityWeighting")] public decimal? EquityWeighting { get; init; }

    /// <summary>The weighted average cost of capital, as a percentage.</summary>
    [JsonPropertyName("wacc")] public decimal? Wacc { get; init; }

    /// <summary>Projected operating cash flow. <b>Levered-only</b> — the unlevered shape has no counterpart.
    /// Overridden by <see cref="CustomLeveredDcfAssumptions.OperatingCashFlowPct"/>, which is the one
    /// override the <i>unlevered</i> path silently ignores.</summary>
    [JsonPropertyName("operatingCashFlow")] public decimal? OperatingCashFlow { get; init; }

    /// <summary>The present value of levered free cash flow for the year. <b>Levered-only.</b></summary>
    [JsonPropertyName("pvLfcf")] public decimal? PvLfcf { get; init; }

    /// <summary>The sum of present-valued levered free cash flows. <b>Levered-only.</b></summary>
    [JsonPropertyName("sumPvLfcf")] public decimal? SumPvLfcf { get; init; }

    /// <summary>The terminal growth rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.LongTermGrowthRate"/>.</summary>
    [JsonPropertyName("longTermGrowthRate")] public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Free cash flow for the year. <b>Levered-only</b>; the unlevered shape carries
    /// <see cref="CustomDcfProjection.Ufcf"/> instead, and the two are not the same
    /// quantity.</summary>
    [JsonPropertyName("freeCashFlow")] public decimal? FreeCashFlow { get; init; }

    /// <summary>The terminal value.</summary>
    [JsonPropertyName("terminalValue")] public decimal? TerminalValue { get; init; }

    /// <summary>The terminal value discounted to today.</summary>
    [JsonPropertyName("presentTerminalValue")] public decimal? PresentTerminalValue { get; init; }

    /// <summary>The enterprise value the model arrives at.</summary>
    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }

    /// <summary>Debt less cash.</summary>
    [JsonPropertyName("netDebt")] public decimal? NetDebt { get; init; }

    /// <summary>Enterprise value less net debt.</summary>
    [JsonPropertyName("equityValue")] public decimal? EquityValue { get; init; }

    /// <summary>The model's per-share answer. Can be deeply negative — see
    /// <see cref="CustomDcfProjection.EquityValuePerShare"/>.</summary>
    [JsonPropertyName("equityValuePerShare")] public decimal? EquityValuePerShare { get; init; }

    /// <summary>Free cash flow in the first terminal year.</summary>
    [JsonPropertyName("freeCashFlowT1")] public decimal? FreeCashFlowT1 { get; init; }

    /// <summary>Operating cash flow as a percentage of revenue. <b>Levered-only</b>, and the last key in
    /// FMP's own ordering rather than beside <see cref="OperatingCashFlow"/>.</summary>
    [JsonPropertyName("operatingCashFlowPercentage")]
    public decimal? OperatingCashFlowPercentage { get; init; }
}
```

**Then demote the forward crefs.** `CustomDcfProjection.cs` above is written as it will finally read, and
many of its `<see cref>`s point at `CustomDcfAssumptions` and `CustomLeveredDcfAssumptions`, which Task 6
creates. CS1574 is a build error, so demote them now; Task 6 Step 5 promotes them back.

```bash
sed -i '' -E 's|<see cref="(Custom(Levered)?DcfAssumptions(\.[A-Za-z]*)?)"/>|<c>\1</c>|g' \
  src/FmpDotNet/Models/CustomDcfProjection.cs
grep -c '<c>Custom' src/FmpDotNet/Models/CustomDcfProjection.cs
```

Expected: a non-zero count, and `dotnet build src/FmpDotNet` clean of CS1574.

- [ ] **Step 5: Register all four with the serialiser**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, extend the `#39` block:

```csharp
[JsonSerializable(typeof(List<DcfValuation>))]
[JsonSerializable(typeof(List<LeveredDcfValuation>))]
[JsonSerializable(typeof(List<CustomDcfProjection>))]
[JsonSerializable(typeof(List<CustomLeveredDcfProjection>))]
```

That completes the eight entries the spec calls for. The two assumptions records Task 6 adds are request
inputs and are **never** deserialised, so they do not appear here.

- [ ] **Step 6: Run the tests**

```bash
dotnet build src/FmpDotNet
dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~DiscountedCashFlowTests
```

Expected: PASS, 5 tests, and a clean build with **no CS1574** — which is what Step 4's demotion buys. If
CS1574 appears naming `CustomDcfAssumptions` or `CustomLeveredDcfAssumptions`, the demotion missed a cref;
re-run its `sed` rather than deleting the reference.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Models/DcfValuation.cs \
        src/FmpDotNet/Models/CustomDcfProjection.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs
git commit -m "feat: model the four DCF response shapes — levered and unlevered stay separate types (#39)"
```

---

### Task 6: The two assumption vocabularies and the `fmp.DiscountedCashFlow` facade

**Files:**
- Create: `src/FmpDotNet/Models/CustomDcfAssumptions.cs` (`CustomDcfAssumptions` **and**
  `CustomLeveredDcfAssumptions`)
- Create: `src/FmpDotNet/Endpoints/DiscountedCashFlowEndpoints.cs`
- Modify: `src/FmpDotNet/Models/CustomDcfProjection.cs` (promote the deferred `<c>` crefs)
- Modify: `tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs` (append a `Build` helper and six tests)

**Interfaces:**
- Consumes: the four response records from Task 5 and their `FmpJsonContext` accessors. `FmpTransport`,
  `FmpRequest` and `ScreenerCriteria`'s `Number<T>` pattern (`src/FmpDotNet/ScreenerCriteria.cs:152`), all
  unchanged.
- Produces: `public sealed record CustomDcfAssumptions` (16 `decimal?` properties, one
  `internal FmpRequest Apply(FmpRequest)`) and `public sealed record CustomLeveredDcfAssumptions` (10, same
  method) in `FmpDotNet.Models`; `public sealed class DiscountedCashFlowEndpoints(FmpTransport transport)` in
  `FmpDotNet.Endpoints` with the four signatures below. Task 7 adds the facade to `FmpClient` as the
  `DiscountedCashFlow` property. Task 8's `Probe.Argument` and Task 7's `EndpointCoverageTests.Argument` both
  construct `new CustomDcfAssumptions()` and `new CustomLeveredDcfAssumptions()`, so **both records must have
  a public parameterless constructor** — which a record with all-`init` properties and no positional
  parameters has.

```csharp
Task<IReadOnlyList<DcfValuation>>               GetValuationAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<LeveredDcfValuation>>        GetLeveredValuationAsync(string symbol, CancellationToken ct = default);
Task<IReadOnlyList<CustomDcfProjection>>        GetCustomValuationAsync(string symbol, CustomDcfAssumptions? assumptions = null, CancellationToken ct = default);
Task<IReadOnlyList<CustomLeveredDcfProjection>> GetCustomLeveredValuationAsync(string symbol, CustomLeveredDcfAssumptions? assumptions = null, CancellationToken ct = default);
```

**The eighteen overrides and where each one lands.** Probed 2026-08-31; the eighteen names come from FMP's
wire plus the independent Python `fmpsdk`, which is where `costOfEquity` was found after a self-selected
list of seventeen candidates missed it.

| query parameter | unlevered | levered | property |
|---|---|---|---|
| `beta` | yes | yes | `Beta` |
| `capitalExpenditurePct` | yes | yes | `CapitalExpenditurePct` |
| `costOfDebt` | yes | yes | `CostOfDebt` |
| `costOfEquity` | yes | yes | `CostOfEquity` |
| `longTermGrowthRate` | yes | yes | `LongTermGrowthRate` |
| `marketRiskPremium` | yes | yes | `MarketRiskPremium` |
| `revenueGrowthPct` | yes | yes | `RevenueGrowthPct` |
| `riskFreeRate` | yes | yes | `RiskFreeRate` |
| `taxRate` | yes | yes | `TaxRate` |
| `cashAndShortTermInvestmentsPct` | yes | **ignored** | `CashAndShortTermInvestmentsPct` |
| `depreciationAndAmortizationPct` | yes | **ignored** | `DepreciationAndAmortizationPct` |
| `ebitPct` | yes | **ignored** | `EbitPct` |
| `ebitdaPct` | yes | **ignored** | `EbitdaPct` |
| `inventoriesPct` | yes | **ignored** | `InventoriesPct` |
| `payablePct` | yes | **ignored** | `PayablePct` |
| `receivablesPct` | yes | **ignored** | `ReceivablesPct` |
| `operatingCashFlowPct` | **ignored** | yes | `OperatingCashFlowPct` |
| `sellingGeneralAndAdministrativeExpensesPct` | **ignored** | **ignored** | **exposed on neither** |

9 shared + 7 unlevered-only + 1 levered-only + 1 dead on both = **18**, giving records of **16** and **10**.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs`, inside the class. Add
`using FmpDotNet.Endpoints;`, `using Microsoft.Extensions.Options;`, `using System.Globalization;` and
`using System.Web;` to the file's using block.

```csharp
    private static (DiscountedCashFlowEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DiscountedCashFlowEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Each_of_the_four_paths_is_asked_exactly_once()
    {
        var (dcf, handler) = Build();

        await dcf.GetValuationAsync("AAPL");
        await dcf.GetLeveredValuationAsync("AAPL");
        await dcf.GetCustomValuationAsync("AAPL");
        await dcf.GetCustomLeveredValuationAsync("AAPL");

        Assert.Equal(
            [
                "/stable/discounted-cash-flow",
                "/stable/levered-discounted-cash-flow",
                "/stable/custom-discounted-cash-flow",
                "/stable/custom-levered-discounted-cash-flow",
            ],
            handler.Requests.Select(u => u.AbsolutePath));

        // No limit and no page on any of the four. Measured 2026-08-31,
        // custom-discounted-cash-flow?symbol=AAPL&limit=3 returned the full 10 rows — the parameter is
        // ignored, so offering it would be worse than not offering it.
        Assert.All(handler.Requests, u =>
        {
            Assert.DoesNotContain("limit=", u.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("page=", u.Query, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Omitting_the_assumptions_sends_nothing_but_the_symbol()
    {
        // An absent assumptions object and an empty one are the same request, deliberately: every property
        // is nullable and FmpRequest.With drops nulls, so "use FMP's default for that assumption" has one
        // spelling. The smoke sweep depends on this — it probes both custom paths with an empty record and
        // baselines FMP's own default valuation rather than an arbitrary set of overrides.
        var (dcf, handler) = Build();

        await dcf.GetCustomValuationAsync("AAPL");
        await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions());
        await dcf.GetCustomLeveredValuationAsync("AAPL");
        await dcf.GetCustomLeveredValuationAsync("AAPL", new CustomLeveredDcfAssumptions());

        Assert.All(handler.Requests, u =>
            Assert.Equal(["symbol", "apikey"], HttpUtility.ParseQueryString(u.Query).AllKeys));
        Assert.Equal(handler.Requests[0].Query, handler.Requests[1].Query);
        Assert.Equal(handler.Requests[2].Query, handler.Requests[3].Query);
    }

    [Fact]
    public async Task Every_set_assumption_reaches_the_query_under_its_own_wire_name()
    {
        var (dcf, handler) = Build();

        await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions
        {
            RevenueGrowthPct = 12.5m,
            EbitdaPct = 30m,
            DepreciationAndAmortizationPct = 3m,
            CashAndShortTermInvestmentsPct = 20m,
            ReceivablesPct = 15m,
            InventoriesPct = 2m,
            PayablePct = 18m,
            EbitPct = 28m,
            CapitalExpenditurePct = -3m,
            TaxRate = 16m,
            LongTermGrowthRate = 3m,
            CostOfDebt = 4.5m,
            CostOfEquity = 8.31m,
            MarketRiskPremium = 4.72m,
            Beta = 1.1m,
            RiskFreeRate = 4.48m,
        });

        var query = HttpUtility.ParseQueryString(handler.Requests[0].Query);

        Assert.Equal("AAPL", query["symbol"]);
        Assert.Equal("12.5", query["revenueGrowthPct"]);
        Assert.Equal("30", query["ebitdaPct"]);
        Assert.Equal("3", query["depreciationAndAmortizationPct"]);
        Assert.Equal("20", query["cashAndShortTermInvestmentsPct"]);
        Assert.Equal("15", query["receivablesPct"]);
        Assert.Equal("2", query["inventoriesPct"]);
        Assert.Equal("18", query["payablePct"]);
        Assert.Equal("28", query["ebitPct"]);
        Assert.Equal("-3", query["capitalExpenditurePct"]);
        Assert.Equal("16", query["taxRate"]);
        Assert.Equal("3", query["longTermGrowthRate"]);
        Assert.Equal("4.5", query["costOfDebt"]);
        Assert.Equal("8.31", query["costOfEquity"]);
        Assert.Equal("4.72", query["marketRiskPremium"]);
        Assert.Equal("1.1", query["beta"]);
        Assert.Equal("4.48", query["riskFreeRate"]);

        // 16 overrides plus symbol plus the key.
        Assert.Equal(18, query.AllKeys.Length);

        // The response-side misspelling does NOT appear on the request side: the wire wants `costOfDebt`
        // here and sends `costofDebt` back. Both spellings are reproduced as they are.
        Assert.DoesNotContain("costofDebt", handler.Requests[0].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_assumption_is_formatted_invariantly_whatever_the_ambient_culture_is()
    {
        // The culture is load-bearing, for exactly the reason ScreenerCriteria records: a value formatted
        // under a comma-decimal culture becomes `beta=1,1` in the query string and FMP does not reject it.
        // Measured 2026-08-31, custom-discounted-cash-flow?symbol=AAPL&notARealParam=99 returned HTTP 200
        // with longTermGrowthRate, beta and equityValuePerShare identical to the baseline — an unparseable
        // value is treated like an unrecognised one, so a German or French host would silently receive FMP's
        // DEFAULT valuation while believing it applied the caller's assumptions.
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var (dcf, handler) = Build();
            await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions { Beta = 1.1m });
            await dcf.GetCustomLeveredValuationAsync(
                "AAPL", new CustomLeveredDcfAssumptions { Beta = 1.1m });

            Assert.All(handler.Requests, u =>
                Assert.Equal("1.1", HttpUtility.ParseQueryString(u.Query)["beta"]));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void The_two_assumption_vocabularies_are_pinned_and_neither_carries_the_dead_parameter()
    {
        // The reason there are two records rather than one. An unrecognised or wrong-path parameter is
        // SILENT: measured 2026-08-31, a wrong-path override returns HTTP 200 with a valuation identical to
        // the baseline, so a caller who hands ebitdaPct to the levered endpoint gets a number that ignored
        // their assumption. Two records make that a compile error.
        //
        // This is not hypothetical. The independent Python fmpsdk assembles BOTH custom calls through one
        // shared 18-parameter helper, which means eight of its eighteen levered parameters do nothing and two
        // of its eighteen unlevered ones do nothing.
        static HashSet<string> Names(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)];

        var unlevered = Names(typeof(CustomDcfAssumptions));
        var levered = Names(typeof(CustomLeveredDcfAssumptions));

        Assert.Equal(
            ["Beta", "CapitalExpenditurePct", "CashAndShortTermInvestmentsPct", "CostOfDebt", "CostOfEquity",
             "DepreciationAndAmortizationPct", "EbitPct", "EbitdaPct", "InventoriesPct", "LongTermGrowthRate",
             "MarketRiskPremium", "PayablePct", "ReceivablesPct", "RevenueGrowthPct", "RiskFreeRate",
             "TaxRate"],
            unlevered.OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(
            ["Beta", "CapitalExpenditurePct", "CostOfDebt", "CostOfEquity", "LongTermGrowthRate",
             "MarketRiskPremium", "OperatingCashFlowPct", "RevenueGrowthPct", "RiskFreeRate", "TaxRate"],
            levered.OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(9, unlevered.Intersect(levered, StringComparer.Ordinal).Count());
        Assert.Equal(7, unlevered.Except(levered, StringComparer.Ordinal).Count());
        Assert.Single(levered.Except(unlevered, StringComparer.Ordinal));

        // sellingGeneralAndAdministrativeExpensesPct is FMP's eighteenth override and it moved NOTHING on
        // either path, measured 2026-08-31. A property for it would be a control that does nothing, so it is
        // on neither record — and this assertion is what stops it being "helpfully" added back.
        Assert.DoesNotContain("SellingGeneralAndAdministrativeExpensesPct", unlevered);
        Assert.DoesNotContain("SellingGeneralAndAdministrativeExpensesPct", levered);

        // Every property on both is decimal?, which is what lets one Number() helper serve all of them.
        foreach (var t in new[] { typeof(CustomDcfAssumptions), typeof(CustomLeveredDcfAssumptions) })
            Assert.All(t.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                p => Assert.Equal(typeof(decimal?), p.PropertyType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_rejected_on_all_four_paths(string? blank)
    {
        // All four answer a naked request with HTTP 400 and a plain-text body naming `symbol`, measured
        // 2026-08-31. Rejecting locally saves a call against the key's quota.
        //
        // There is deliberately NO uppercase guard: measured 2026-08-31, symbol=aapl returned "AAPL" with
        // values byte-identical to the uppercase call on the plain path, and the custom path normalised and
        // returned all 10 rows. The News slice guards case because lowercase THERE returns 0 rows at HTTP
        // 200; that reasoning does not transfer, and a guard invented here would reject a request FMP serves.
        var (dcf, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetLeveredValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetCustomValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetCustomLeveredValuationAsync(blank!));
        Assert.Empty(handler.Requests);

        // And a lowercase symbol goes through untouched, which is the absence this asserts.
        await dcf.GetValuationAsync("aapl");
        Assert.Equal("aapl", HttpUtility.ParseQueryString(handler.Requests[0].Query)["symbol"]);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/FmpDotNet.Tests
```

Expected: FAIL with `CS0246` for `CustomDcfAssumptions`, `CustomLeveredDcfAssumptions` and
`DiscountedCashFlowEndpoints`.

- [ ] **Step 3: Write the two assumptions records**

Create `src/FmpDotNet/Models/CustomDcfAssumptions.cs`:

```csharp
using System.Globalization;

namespace FmpDotNet.Models;

/// <summary>Assumption overrides for the <b>unlevered</b> custom DCF,
/// <c>stable/custom-discounted-cash-flow</c>. Sixteen optional inputs; the ones left unset are not sent, so
/// an empty <see cref="CustomDcfAssumptions"/> asks for FMP's own default valuation.
///
/// <para><b>This type exists because a wrong or unrecognised parameter is silent.</b> Measured 2026-08-31,
/// <c>custom-discounted-cash-flow?symbol=AAPL&amp;notARealParam=99</c> returned HTTP 200 with
/// <c>longTermGrowthRate</c>, <c>beta</c> and <c>equityValuePerShare</c> identical to the baseline — the only
/// fields that moved were the eight that track the live price. A misspelled override therefore produces a
/// valuation that ignored it and looks exactly like one that applied it.</para>
///
/// <para><b>And it is separate from <see cref="CustomLeveredDcfAssumptions"/> because the two endpoints
/// honour different vocabularies.</b> Seven of the properties here — <see cref="EbitdaPct"/>,
/// <see cref="DepreciationAndAmortizationPct"/>, <see cref="CashAndShortTermInvestmentsPct"/>,
/// <see cref="ReceivablesPct"/>, <see cref="InventoriesPct"/>, <see cref="PayablePct"/> and
/// <see cref="EbitPct"/> — are accepted and <b>discarded</b> by the levered path, and its
/// <c>operatingCashFlowPct</c> is discarded here. Two records make handing one to the wrong endpoint a
/// compile error. <b>This is not hypothetical:</b> the independent Python <c>fmpsdk</c> assembles both calls
/// through one shared 18-parameter helper, so eight of its eighteen levered parameters do nothing.</para>
///
/// <para><b>FMP's eighteenth documented override,
/// <c>sellingGeneralAndAdministrativeExpensesPct</c>, is on neither record.</b> Probed 2026-08-31, it moved
/// nothing on either path. A property for it would be a control that does nothing.</para>
///
/// <para><b>No value is validated.</b> Measured 2026-08-27/31, <c>longTermGrowthRate=10</c> against AAPL
/// returned <c>equityValuePerShare = -1253.46</c> against 145.72 at the default rate of 4, because a
/// terminal growth rate at or above the measured <c>wacc</c> of 9.47 inverts the terminal-value denominator.
/// FMP returns the result rather than rejecting the input, and this SDK does not invent a bound FMP does not
/// enforce.</para></summary>
public sealed record CustomDcfAssumptions
{
    /// <summary>Revenue growth per year, as a percentage. Wire name <c>revenueGrowthPct</c>. Honoured on both
    /// custom paths.</summary>
    public decimal? RevenueGrowthPct { get; init; }

    /// <summary>EBITDA as a percentage of revenue. Wire name <c>ebitdaPct</c>. <b>Discarded by the levered
    /// path</b>, which is why it is not on <see cref="CustomLeveredDcfAssumptions"/>.</summary>
    public decimal? EbitdaPct { get; init; }

    /// <summary>Depreciation and amortisation as a percentage of revenue. Wire name
    /// <c>depreciationAndAmortizationPct</c>. <b>Discarded by the levered path.</b></summary>
    public decimal? DepreciationAndAmortizationPct { get; init; }

    /// <summary>Cash and short-term investments as a percentage of revenue. Wire name
    /// <c>cashAndShortTermInvestmentsPct</c>. <b>Discarded by the levered path.</b></summary>
    public decimal? CashAndShortTermInvestmentsPct { get; init; }

    /// <summary>Receivables as a percentage of revenue. Wire name <c>receivablesPct</c>. <b>Discarded by the
    /// levered path.</b></summary>
    public decimal? ReceivablesPct { get; init; }

    /// <summary>Inventories as a percentage of revenue. Wire name <c>inventoriesPct</c>. <b>Discarded by the
    /// levered path.</b></summary>
    public decimal? InventoriesPct { get; init; }

    /// <summary>Payables as a percentage of revenue. Wire name <c>payablePct</c> — singular, as FMP spells
    /// it. <b>Discarded by the levered path.</b></summary>
    public decimal? PayablePct { get; init; }

    /// <summary>EBIT as a percentage of revenue. Wire name <c>ebitPct</c>. <b>Discarded by the levered
    /// path.</b></summary>
    public decimal? EbitPct { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue — negative on measured rows. Wire name
    /// <c>capitalExpenditurePct</c>. Honoured on both custom paths.</summary>
    public decimal? CapitalExpenditurePct { get; init; }

    /// <summary>The effective tax rate, as a percentage. Wire name <c>taxRate</c>. <b>Not the same quantity
    /// as <see cref="CustomDcfProjection.TaxRateCash"/></b>, which is an amount in dollars.</summary>
    public decimal? TaxRate { get; init; }

    /// <summary>The terminal growth rate, as a percentage. Wire name <c>longTermGrowthRate</c>.
    ///
    /// <para><b>Setting this at or above the model's <c>wacc</c> inverts the valuation, and FMP returns the
    /// negative result rather than rejecting it.</b> Measured 2026-08-27/31, <c>10</c> against AAPL produced
    /// <c>equityValuePerShare = -1253.46</c> where the default 4 produced 145.72, against a measured
    /// <c>wacc</c> of 9.47. The SDK does not bound it — see the type's summary.</para></summary>
    public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Cost of debt, as a percentage. <b>Wire name <c>costOfDebt</c>, camelCase</b> — note that the
    /// <i>response</i> spells the same concept <c>costofDebt</c> with a lowercase <c>o</c>. See
    /// <see cref="CustomDcfProjection.CostOfDebt"/>.</summary>
    public decimal? CostOfDebt { get; init; }

    /// <summary>Cost of equity, as a percentage. Wire name <c>costOfEquity</c>.
    ///
    /// <para><b>The eighteenth override, and it was found by reading the independent Python <c>fmpsdk</c>
    /// rather than by probing.</b> The measure phase tried seventeen candidate names chosen by guesswork and
    /// missed it. Probed 2026-08-31 it is honoured on <b>both</b> custom paths, moving <c>costOfEquity</c>,
    /// <c>wacc</c>, <c>terminalValue</c>, <c>presentTerminalValue</c> and <c>sumPvUfcf</c>. The lesson is
    /// recorded rather than hidden: a self-selected probe list is a lower bound on a parameter vocabulary,
    /// never a census.</para></summary>
    public decimal? CostOfEquity { get; init; }

    /// <summary>The equity risk premium, as a percentage. Wire name <c>marketRiskPremium</c>.</summary>
    public decimal? MarketRiskPremium { get; init; }

    /// <summary>The beta to use. Wire name <c>beta</c>.</summary>
    public decimal? Beta { get; init; }

    /// <summary>The risk-free rate, as a percentage. Wire name <c>riskFreeRate</c>.</summary>
    public decimal? RiskFreeRate { get; init; }

    /// <summary>Writes every set assumption onto <paramref name="request"/> and returns it.
    ///
    /// <para><see cref="FmpRequest.With(string, string?)"/> already drops nulls, so the unset properties never
    /// reach the query string — which is what makes an empty <see cref="CustomDcfAssumptions"/> a request for
    /// FMP's own default valuation rather than a request for nothing.</para></summary>
    internal FmpRequest Apply(FmpRequest request) =>
        request
            .With("revenueGrowthPct", Number(RevenueGrowthPct))
            .With("ebitdaPct", Number(EbitdaPct))
            .With("depreciationAndAmortizationPct", Number(DepreciationAndAmortizationPct))
            .With("cashAndShortTermInvestmentsPct", Number(CashAndShortTermInvestmentsPct))
            .With("receivablesPct", Number(ReceivablesPct))
            .With("inventoriesPct", Number(InventoriesPct))
            .With("payablePct", Number(PayablePct))
            .With("ebitPct", Number(EbitPct))
            .With("capitalExpenditurePct", Number(CapitalExpenditurePct))
            .With("taxRate", Number(TaxRate))
            .With("longTermGrowthRate", Number(LongTermGrowthRate))
            .With("costOfDebt", Number(CostOfDebt))
            .With("costOfEquity", Number(CostOfEquity))
            .With("marketRiskPremium", Number(MarketRiskPremium))
            .With("beta", Number(Beta))
            .With("riskFreeRate", Number(RiskFreeRate));

    /// <summary>Formats an assumption invariantly.
    ///
    /// <para>The culture is the point, and the reasoning is <see cref="ScreenerCriteria"/>'s: a value
    /// formatted under a comma-decimal culture becomes <c>beta=1,1</c> in the query string, and FMP does not
    /// reject it — an unparseable value is treated like an unrecognised one, which on this endpoint means the
    /// caller silently receives FMP's <i>default</i> valuation on a German or French host and their own
    /// everywhere else.</para></summary>
    internal static string? Number(decimal? value) =>
        value?.ToString(null, CultureInfo.InvariantCulture);
}

/// <summary>Assumption overrides for the <b>levered</b> custom DCF,
/// <c>stable/custom-levered-discounted-cash-flow</c>. Ten optional inputs — nine shared with
/// <see cref="CustomDcfAssumptions"/> and one of its own.
///
/// <para><b>Seven of the unlevered record's sixteen properties are missing here on purpose</b>, because the
/// levered endpoint accepts them and <b>discards</b> them at HTTP 200: <c>ebitdaPct</c>,
/// <c>depreciationAndAmortizationPct</c>, <c>cashAndShortTermInvestmentsPct</c>, <c>receivablesPct</c>,
/// <c>inventoriesPct</c>, <c>payablePct</c> and <c>ebitPct</c>, all probed 2026-08-31. A caller who hands one
/// of them here gets a valuation that ignored their assumption and no indication of it. With two records
/// that does not compile.</para>
///
/// <para>Everything else — that a wrong parameter is silent, that
/// <c>sellingGeneralAndAdministrativeExpensesPct</c> is exposed on neither record, and that no value is
/// validated — is as <see cref="CustomDcfAssumptions"/> records it.</para></summary>
public sealed record CustomLeveredDcfAssumptions
{
    /// <summary>Revenue growth per year, as a percentage. Wire name <c>revenueGrowthPct</c>.</summary>
    public decimal? RevenueGrowthPct { get; init; }

    /// <summary>Operating cash flow as a percentage of revenue. Wire name <c>operatingCashFlowPct</c>.
    /// <b>The one override this path honours and the unlevered path discards</b>, probed
    /// 2026-08-31.</summary>
    public decimal? OperatingCashFlowPct { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Wire name
    /// <c>capitalExpenditurePct</c>.</summary>
    public decimal? CapitalExpenditurePct { get; init; }

    /// <summary>The effective tax rate, as a percentage. Wire name <c>taxRate</c>.</summary>
    public decimal? TaxRate { get; init; }

    /// <summary>The terminal growth rate, as a percentage. Wire name <c>longTermGrowthRate</c>. Setting it
    /// at or above the model's <c>wacc</c> inverts the valuation and FMP returns the negative result — see
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>.</summary>
    public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Cost of debt, as a percentage. Wire name <c>costOfDebt</c> on the request and
    /// <c>costofDebt</c> on the response — see <see cref="CustomLeveredDcfProjection.CostOfDebt"/>.</summary>
    public decimal? CostOfDebt { get; init; }

    /// <summary>Cost of equity, as a percentage. Wire name <c>costOfEquity</c>. Probed 2026-08-31 it moves
    /// <c>costOfEquity</c>, <c>wacc</c>, <c>terminalValue</c>, <c>presentTerminalValue</c>, <c>pvLfcf</c> and
    /// <c>sumPvLfcf</c> — see <see cref="CustomDcfAssumptions.CostOfEquity"/> for how it was
    /// found.</summary>
    public decimal? CostOfEquity { get; init; }

    /// <summary>The equity risk premium, as a percentage. Wire name <c>marketRiskPremium</c>.</summary>
    public decimal? MarketRiskPremium { get; init; }

    /// <summary>The beta to use. Wire name <c>beta</c>.</summary>
    public decimal? Beta { get; init; }

    /// <summary>The risk-free rate, as a percentage. Wire name <c>riskFreeRate</c>.</summary>
    public decimal? RiskFreeRate { get; init; }

    /// <summary>Writes every set assumption onto <paramref name="request"/> and returns it. Unset properties
    /// are dropped — see <see cref="CustomDcfAssumptions.Apply"/>.
    ///
    /// <para><b>Deliberately written out rather than shared with the unlevered record.</b> Nine of these ten
    /// lines are identical to nine of that record's sixteen, and the duplication is the point: it is the
    /// only place in the SDK where the two vocabularies sit side by side and can be compared line for
    /// line.</para></summary>
    internal FmpRequest Apply(FmpRequest request) =>
        request
            .With("revenueGrowthPct", CustomDcfAssumptions.Number(RevenueGrowthPct))
            .With("operatingCashFlowPct", CustomDcfAssumptions.Number(OperatingCashFlowPct))
            .With("capitalExpenditurePct", CustomDcfAssumptions.Number(CapitalExpenditurePct))
            .With("taxRate", CustomDcfAssumptions.Number(TaxRate))
            .With("longTermGrowthRate", CustomDcfAssumptions.Number(LongTermGrowthRate))
            .With("costOfDebt", CustomDcfAssumptions.Number(CostOfDebt))
            .With("costOfEquity", CustomDcfAssumptions.Number(CostOfEquity))
            .With("marketRiskPremium", CustomDcfAssumptions.Number(MarketRiskPremium))
            .With("beta", CustomDcfAssumptions.Number(Beta))
            .With("riskFreeRate", CustomDcfAssumptions.Number(RiskFreeRate));
}
```

- [ ] **Step 4: Write the facade**

Create `src/FmpDotNet/Endpoints/DiscountedCashFlowEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Discounted cash flow — FMP's own valuations, and two models you can drive with your own
/// assumptions. Four paths.
///
/// <para><b>Four things hold across this group, every one of them measured 2026-08-31, and not one of them
/// catchable by a caller.</b></para>
///
/// <list type="number">
///   <item><description><b>Levered and unlevered are not near each other.</b> KO reads <b>83.71</b>
///     unlevered against <b>49.77</b> levered — a 41% gap — and JPM 728.00 against 907.85, in the opposite
///     direction. Neither is "the" DCF, which is why <see cref="DcfValuation"/> and
///     <see cref="LeveredDcfValuation"/> are two types despite an identical wire
///     shape.</description></item>
///   <item><description><b>The plain and custom paths do not reconcile, and neither reconciles with its own
///     price.</b> Five symbols captured back to back agreed to within ±0.18 and matched exactly on
///     <b>none</b>, with the sign inconsistent. The plain path is a stored daily value — AAPL read
///     <c>dcf = 145.66380328033068</c> identically across captures minutes apart — while the custom path
///     recomputes off a price that moved 314.74 → 314.85 → 314.87 in the same window. Their two price columns
///     disagree <b>in both directions</b>: AAPL -4.83, MSFT -2.50, XOM <b>+2.50</b>. <b>Do not reconstruct or
///     reconcile a price across these endpoints.</b> This replicates the finding already documented on
///     <see cref="ExchangeVariant.DcfDiff"/>, measured 2026-08-27 on a different pair of
///     paths.</description></item>
///   <item><description><b>The two custom paths honour two different override vocabularies, and the
///     difference is silent.</b> A parameter one accepts is discarded by the other at HTTP 200 with a
///     valuation identical to the baseline. Hence one assumptions record per path — see
///     <see cref="CustomDcfAssumptions"/>.</description></item>
///   <item><description><b>The custom responses mix history and forecast and do not say where the line
///     is.</b> Ten rows, descending 2030 → 2021, no flag on the wire, and two fields implying two different
///     boundaries. See <see cref="CustomDcfProjection"/>.</description></item>
/// </list>
///
/// <para><b>No <c>limit</c> and no <c>page</c> on any of the four</b>, because neither is honoured:
/// <c>custom-discounted-cash-flow?symbol=AAPL&amp;limit=3</c> returned the full 10 rows. <b>And no uppercase
/// guard on <c>symbol</c></b>: <c>symbol=aapl</c> answered <c>"AAPL"</c> with byte-identical values, so a
/// guard invented here would reject a request FMP serves — unlike the News searches, where lowercase returns
/// 0 rows at HTTP 200.</para></summary>
public sealed class DiscountedCashFlowEndpoints(FmpTransport transport)
{
    /// <summary>FMP's own unlevered DCF for one symbol, from <c>stable/discounted-cash-flow</c>.
    ///
    /// <para><b>A stored daily value.</b> Measured 2026-08-31, repeated calls minutes apart returned
    /// figures identical to all 14 decimal places. Use <see cref="GetCustomValuationAsync"/> for a model that
    /// recomputes.</para></summary>
    /// <param name="symbol">The ticker. Case is not checked — FMP normalises it. Required and
    /// non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<DcfValuation>> GetValuationAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/discounted-cash-flow").With("symbol", symbol),
            FmpJsonContext.Default.ListDcfValuation, ct);
    }

    /// <summary>FMP's own levered DCF for one symbol, from <c>stable/levered-discounted-cash-flow</c>.
    ///
    /// <para><b>Not a refinement of <see cref="GetValuationAsync"/> — a different question with a different
    /// answer.</b> Measured 2026-08-27/31, KO reads 83.71 unlevered against 49.77 here and JPM 728.00 against
    /// 907.85. The return type differs from the unlevered method's so the two cannot be confused after the
    /// call.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<LeveredDcfValuation>> GetLeveredValuationAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/levered-discounted-cash-flow").With("symbol", symbol),
            FmpJsonContext.Default.ListLeveredDcfValuation, ct);
    }

    /// <summary>An unlevered DCF driven by your own assumptions, from
    /// <c>stable/custom-discounted-cash-flow</c>.
    ///
    /// <para><b>Ten rows per response, mixing history and forecast with nothing on the wire marking which is
    /// which.</b> See <see cref="CustomDcfProjection"/>.</para>
    ///
    /// <para><b>Passing <see langword="null"/> asks for FMP's own default assumptions</b>, which is the same
    /// request an empty <see cref="CustomDcfAssumptions"/> produces: unset properties are not
    /// sent.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="assumptions">Overrides to apply. Omit for FMP's defaults. <b>Sixteen inputs, seven of
    /// which the levered path would discard</b> — see <see cref="CustomDcfAssumptions"/>. No value is
    /// validated: FMP accepts a terminal growth rate that inverts the valuation and returns the negative
    /// result.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Ten projected years, descending. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CustomDcfProjection>> GetCustomValuationAsync(
        string symbol, CustomDcfAssumptions? assumptions = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var request = new FmpRequest("stable/custom-discounted-cash-flow").With("symbol", symbol);
        return transport.GetListAsync(
            assumptions?.Apply(request) ?? request,
            FmpJsonContext.Default.ListCustomDcfProjection, ct);
    }

    /// <summary>A levered DCF driven by your own assumptions, from
    /// <c>stable/custom-levered-discounted-cash-flow</c>.
    ///
    /// <para><b>Takes a different assumptions type from <see cref="GetCustomValuationAsync"/>, and that is
    /// the point.</b> Seven overrides the unlevered path honours are accepted and <b>discarded</b> here, at
    /// HTTP 200, with a valuation identical to the baseline. The separate parameter type turns that into a
    /// compile error.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="assumptions">Overrides to apply. Omit for FMP's defaults. <b>Ten inputs</b> — see
    /// <see cref="CustomLeveredDcfAssumptions"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Ten projected years, descending. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CustomLeveredDcfProjection>> GetCustomLeveredValuationAsync(
        string symbol, CustomLeveredDcfAssumptions? assumptions = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var request = new FmpRequest("stable/custom-levered-discounted-cash-flow").With("symbol", symbol);
        return transport.GetListAsync(
            assumptions?.Apply(request) ?? request,
            FmpJsonContext.Default.ListCustomLeveredDcfProjection, ct);
    }
}
```

- [ ] **Step 5: Promote the deferred crefs and prove none remain**

Task 5 wrote every reference to the two assumptions types as `<c>…</c>` because the types did not exist.
They exist now. Promote them in `src/FmpDotNet/Models/CustomDcfProjection.cs`:

```bash
sed -i '' -E 's|<c>(Custom(Levered)?DcfAssumptions(\.[A-Za-z]*)?)</c>|<see cref="\1"/>|g' \
  src/FmpDotNet/Models/CustomDcfProjection.cs
```

Then prove no deferred cref is left anywhere in the SDK:

```bash
grep -rn '<c>Custom\(Levered\)\?Dcf' src/FmpDotNet/ || echo "no deferred assumption crefs remain"
grep -rn '<c>FundraisersEndpoints\|<c>DiscountedCashFlowEndpoints\|<c>Fundraising[A-Z]' src/FmpDotNet/ \
  || echo "no deferred facade or record crefs remain"
```

Expected: both echo lines print. **Four kinds of `<c>` reference stay `<c>` on purpose and must not be
promoted**, because none of them names a `cref` target: `<c>stable/…</c>` path literals; `<c>fmpsdk</c>` and
`<c>BulkDiscountedCashFlow</c>`-style references to things outside this assembly's doc graph;
wire names such as `<c>costofDebt</c>`, `<c>revenueGrowthPct</c>` and
`<c>cashAndCashEquiValentMostRecentFiscalYear</c>`, which are strings rather than members; and literal
values such as `<c>"Y"</c> / <c>"N"</c>.

- [ ] **Step 6: Run the tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~DiscountedCashFlowTests
```

Expected: PASS, 13 tests.

```bash
dotnet build src/FmpDotNet
```

Expected: PASS with no warnings — in particular no CS1574 and no CS1591.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Models/CustomDcfAssumptions.cs \
        src/FmpDotNet/Models/CustomDcfProjection.cs \
        src/FmpDotNet/Endpoints/DiscountedCashFlowEndpoints.cs \
        tests/FmpDotNet.Tests/DiscountedCashFlowTests.cs
git commit -m "feat: add the fmp.DiscountedCashFlow facade and its two override vocabularies (#39)"
```

---

### Task 7: Wire both facades onto the client, the container and the two argument harnesses

**Files:**
- Modify: `src/FmpDotNet/FmpClient.cs` (two constructor parameters, two properties)
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs:144` (two registrations)
- Modify: `src/FmpDotNet/Models/ExchangeVariant.cs:144-149` (one doc sentence)
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs:57,62` (two assertions, one count)
- Modify: `tests/FmpDotNet.Tests/EndpointCoverageTests.cs:334` (two `Argument` arms)
- Modify: `README.md` (the generated coverage block only — the prose is Task 9)

**Interfaces:**
- Consumes: `FundraisersEndpoints` (Task 4) and `DiscountedCashFlowEndpoints` (Task 6).
- Produces: `fmp.Fundraisers` and `fmp.DiscountedCashFlow` as public properties on `FmpClient`. **The moment
  these two properties exist, the reflection-driven smoke sweep begins probing ten new endpoints** — which is
  why Task 8 follows immediately.

**Why all six files and not just `FmpClient`.** The spec's Files section names four of them. Two more are
load-bearing and one of the four is load-bearing for a different reason than the spec gives:

- **`FmpServiceCollectionExtensions`** — without `TryAddTransient`, `FmpClient` cannot be constructed at all
  and every `AddFmpTests` case fails at the first resolve.
- **`AddFmpTests`** asserts `Assert.Equal(23, …GetProperties(…).Length)`. A 25th property fails that by
  design; the assertion exists so the `Assert.NotNull` list above it cannot fall silently out of date.
- **`EndpointCoverageTests.Argument`** throws on unknown types (Ruling 3). Without two arms, the two custom
  DCF methods vanish from the coverage table and
  `Every_public_endpoint_method_reaches_the_api` goes red.
- **`EndpointCoverageTests.DocumentedPaths`** does **not** change (Ruling 5). The 226 → 236 move is generated.

- [ ] **Step 1: Add both groups to `FmpClient`**

Extend the primary constructor's last line in `src/FmpDotNet/FmpClient.cs`:

```csharp
    NewsEndpoints news, FundraisersEndpoints fundraisers,
    DiscountedCashFlowEndpoints discountedCashFlow)
```

and append two properties after `News`, before the closing brace:

```csharp
    /// <summary>Fundraisers — Regulation Crowdfunding (Form C) and Regulation D (Form D) offerings.
    ///
    /// <para><b>Two corpora that do not overlap</b>, which is why the six methods name their corpus rather
    /// than taking it as an argument: measured 2026-08-31, a Form C issuer's CIK answers <b>0 rows</b> on the
    /// Form D paths and vice versa, both at HTTP 200 with an empty array. Read
    /// <see cref="FundraisersEndpoints.GetCrowdfundingOfferingsLatestAsync"/> before paging — neither
    /// <c>-latest</c> path has a page ceiling, and the two have ceilings and defaults that differ by a factor
    /// of ten from each other.</para></summary>
    public FundraisersEndpoints Fundraisers { get; } = fundraisers;

    /// <summary>Discounted cash flow — FMP's own valuations, and two models you can drive with your own
    /// assumptions.
    ///
    /// <para><b>Levered and unlevered are different questions with different answers</b> — measured
    /// 2026-08-27/31, KO reads 83.71 unlevered against 49.77 levered — so the SDK gives them separate return
    /// types. And <b>the plain and custom paths do not reconcile with each other or with their own price
    /// columns</b>, in both directions: do not reconstruct a price from any of them. Read
    /// <see cref="CustomDcfAssumptions"/> before passing overrides — the two custom paths honour two
    /// different vocabularies and each silently discards the other's.</para></summary>
    public DiscountedCashFlowEndpoints DiscountedCashFlow { get; } = discountedCashFlow;
```

`FmpClient.cs` already has `using FmpDotNet.Endpoints;`; add `using FmpDotNet.Models;` for the
`CustomDcfAssumptions` cref.

- [ ] **Step 2: Register both in the container**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, after line 144's
`services.TryAddTransient<NewsEndpoints>();`:

```csharp
        services.TryAddTransient<FundraisersEndpoints>();
        services.TryAddTransient<DiscountedCashFlowEndpoints>();
```

- [ ] **Step 3: Update `AddFmpTests`**

In `tests/FmpDotNet.Tests/AddFmpTests.cs`, after `Assert.NotNull(client.News);`:

```csharp
        Assert.NotNull(client.Fundraisers);
        Assert.NotNull(client.DiscountedCashFlow);
```

and change the count on line 62 from `23` to `25`.

- [ ] **Step 4: Teach the coverage harness to build an assumptions object**

In `tests/FmpDotNet.Tests/EndpointCoverageTests.cs`, in `Argument`, after the existing
`if (type == typeof(ScreenerCriteria)) return new ScreenerCriteria();`:

```csharp
        // The two custom-DCF assumption records, following the ScreenerCriteria arm above. An EMPTY record
        // rather than a populated one: every property is nullable and FmpRequest.With drops nulls, so this
        // sends `symbol` and nothing else — which is the call whose path this harness is recording. Without
        // these two arms Argument throws, the two custom methods issue no request, and they drop out of the
        // README coverage table while Every_public_endpoint_method_reaches_the_api goes red.
        if (type == typeof(CustomDcfAssumptions)) return new CustomDcfAssumptions();
        if (type == typeof(CustomLeveredDcfAssumptions)) return new CustomLeveredDcfAssumptions();
```

The file already resolves `ScreenerCriteria` unqualified; add `using FmpDotNet.Models;` if the two new type
names do not resolve.

- [ ] **Step 5: Correct the one doc comment this slice makes false**

`src/FmpDotNet/Models/ExchangeVariant.cs:144-149` says of `Dcf`: *"The only DCF value the SDK currently
surfaces: FMP's Discounted Cash Flow group is four further paths in the long tail of issue #25, and none of
them is modelled."* All four are modelled now. Replace that paragraph with:

```csharp
    /// <para><b>Not the same number as anything <c>fmp.DiscountedCashFlow</c> returns, and not reconcilable
    /// with it.</b> FMP's four Discounted Cash Flow paths are modelled as of #39 — see
    /// <see cref="Endpoints.DiscountedCashFlowEndpoints"/> — and measured 2026-08-31 the plain and custom
    /// families disagree with each other and with their own price columns in both directions. See
    /// <see cref="DcfDiff"/> for why the pair here does not reconcile with <see cref="Price"/>
    /// either.</para>
```

- [ ] **Step 6: Regenerate the README coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EndpointCoverageTests
git diff --stat README.md
```

Then read the regenerated headline:

```bash
grep -n 'endpoint paths are modelled' README.md
```

Expected: `**236 of FMP's 243 endpoint paths are modelled.**` — the generator's own output.
**If it reads anything else, stop and find the missing or duplicated path** (Ruling 5); do not edit the
number. `git diff README.md` should show exactly two new `fmp.` sections, `fmp.DiscountedCashFlow` with 4
rows and `fmp.Fundraisers` with 6, in alphabetical position.

- [ ] **Step 7: Run the whole unit suite**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: PASS with no filter and no key. Every count assertion in `AddFmpTests` and every README assertion
in `EndpointCoverageTests` is now consistent with the code.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        src/FmpDotNet/Models/ExchangeVariant.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs \
        tests/FmpDotNet.Tests/EndpointCoverageTests.cs \
        README.md
git commit -m "feat: wire fmp.Fundraisers and fmp.DiscountedCashFlow onto the client — 236 of 243 (#39)"
```

---

### Task 8: Teach the live sweep the two corpora, and re-record the baseline

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` (four constants, appended before the closing brace)
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` (two name-dispatched string arms, two assumptions arms)
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` (two pinning tests)
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` (regenerated)

**Interfaces:**
- Consumes: `FundraisersEndpoints`, `DiscountedCashFlowEndpoints`, `CustomDcfAssumptions` and
  `CustomLeveredDcfAssumptions`; the two facade properties on `FmpClient` from Task 7.
- Produces: `LiveApi.CrowdfundingCik`, `.CrowdfundingNameQuery`, `.FundraisingCik`, `.FundraisingNameQuery`;
  ten new blocks in `baseline-ordinary.txt`.

**The silent green this task exists to prevent.** The sweep is reflection-driven — `Probe.Groups()` walks
`FmpClient`'s public properties — so ten endpoints joined it the moment Task 7 landed, **already being
probed with the wrong arguments**. Measured 2026-08-31:

| existing constant | `crowdfunding-offerings` | `fundraising` | `crowdfunding-offerings-search` |
|---|---|---|---|
| `LiveApi.Cik` = `320193` | **0 rows** | **0 rows** | — |
| `LiveApi.FilerCik` = `0001067983` | **0 rows** | **0 rows** | — |
| `LiveApi.AcquirerNameQuery` = `"Apple"` | — | — | **0 rows** |

Every one is HTTP 200 with `[]`. Six of the ten endpoints would record `outcome empty` as their healthy
baseline and match it green every week thereafter — the exact failure `LiveApi.Exchange`, `LiveApi.Cik` and
`LiveApi.EtfSymbol` were each added to prevent.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`:

```csharp
    [Fact]
    public void The_sweep_asks_each_fundraising_corpus_for_a_CIK_and_a_name_that_belong_to_it()
    {
        // Probe.Argument maps any unrecognised `cik` to LiveApi.Cik and any unrecognised `name` to
        // LiveApi.AcquirerNameQuery. Measured 2026-08-31, all three of the sweep's existing constants answer
        // ZERO ROWS on these paths at HTTP 200: LiveApi.Cik (320193) and LiveApi.FilerCik (0001067983) on
        // both crowdfunding-offerings and fundraising, and AcquirerNameQuery ("Apple") on
        // crowdfunding-offerings-search. Six endpoints would record `outcome empty` as their healthy
        // baseline and agree with themselves every week after — the same silent green LiveApi.Exchange and
        // LiveApi.EtfSymbol exist to prevent.
        //
        // And the dispatch has to key on the METHOD, not just the declaring type: one facade holds both
        // corpora, and measured 2026-08-31 a Form C CIK answers 0 rows on the Form D paths and vice versa.
        // This is the CongressEndpoints pattern, where the chamber is a property of the method rather than
        // of the parameter name.
        static ParameterInfo Param(string method, string name) =>
            typeof(Endpoints.FundraisersEndpoints).GetMethod(method)!
                .GetParameters().Single(p => p.Name == name);

        Assert.Equal(LiveApi.CrowdfundingCik, Probe.Argument(
            Param(nameof(Endpoints.FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync), "cik")));
        Assert.Equal(LiveApi.FundraisingCik, Probe.Argument(
            Param(nameof(Endpoints.FundraisersEndpoints.GetFundraisingByCikAsync), "cik")));
        Assert.Equal(LiveApi.CrowdfundingNameQuery, Probe.Argument(
            Param(nameof(Endpoints.FundraisersEndpoints.SearchCrowdfundingOfferingsAsync), "name")));
        Assert.Equal(LiveApi.FundraisingNameQuery, Probe.Argument(
            Param(nameof(Endpoints.FundraisersEndpoints.SearchFundraisingAsync), "name")));

        // The two CIKs are different values, and the two name queries are separate constants even though
        // FundraisingNameQuery happens to hold the same literal as AcquirerNameQuery: the value coincides by
        // measurement, not because the two paths share a vocabulary.
        Assert.NotEqual(LiveApi.CrowdfundingCik, LiveApi.FundraisingCik);
        Assert.NotEqual(LiveApi.Cik, LiveApi.CrowdfundingCik);
        Assert.NotEqual(LiveApi.Cik, LiveApi.FundraisingCik);
    }

    [Fact]
    public void The_sweep_probes_both_custom_DCF_paths_with_FMPs_own_default_assumptions()
    {
        // Probe.Argument throws on any type it has no arm for, so without these two the sweep cannot call
        // the custom DCF methods at all — The_sweep_can_supply_arguments_for_every_endpoint_method goes red.
        //
        // An EMPTY record rather than a populated one, and rather than null: every property is nullable and
        // FmpRequest.With drops nulls, so the call that goes out is `symbol=AAPL` and nothing else. The
        // baseline therefore records FMP's own default valuation rather than an arbitrary set of overrides —
        // which is what makes a week-over-week diff mean something. (null does not fit: Probe.Argument
        // returns a non-nullable object, and SweepCoverageTests unboxes its result directly.)
        static ParameterInfo Param(string method) =>
            typeof(Endpoints.DiscountedCashFlowEndpoints).GetMethod(method)!
                .GetParameters().Single(p => p.Name == "assumptions");

        var unlevered = Assert.IsType<Models.CustomDcfAssumptions>(Probe.Argument(
            Param(nameof(Endpoints.DiscountedCashFlowEndpoints.GetCustomValuationAsync))));
        var levered = Assert.IsType<Models.CustomLeveredDcfAssumptions>(Probe.Argument(
            Param(nameof(Endpoints.DiscountedCashFlowEndpoints.GetCustomLeveredValuationAsync))));

        // Every member null, so a future property with a non-null initialiser cannot silently start sending
        // an override into the weekly baseline.
        Assert.All(unlevered.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.Null(p.GetValue(unlevered)));
        Assert.All(levered.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.Null(p.GetValue(levered)));
    }
```

Add `using System.Reflection;` to the file if it is not already there.

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet build tests/FmpDotNet.SmokeTests
```

Expected: FAIL with `CS0117: 'LiveApi' does not contain a definition for 'CrowdfundingCik'` and the same for
the other three constants.

- [ ] **Step 3: Add the four constants**

Append to `tests/FmpDotNet.SmokeTests/LiveApi.cs`, before the closing brace:

```csharp
    /// <summary>The CIK the Regulation Crowdfunding paths are probed with — <c>"0002010670"</c>, Finlete
    /// Funding, Inc.
    ///
    /// <para><b>Named rather than falling out of the <c>cik</c> default, for the reason recorded on
    /// <see cref="Exchange"/>, and the measurement here is unusually stark.</b> <c>Probe.Argument</c> maps an
    /// unrecognised <c>cik</c> to <see cref="Cik"/> — Apple's issuer CIK — and measured 2026-08-31 that
    /// answers <b>0 rows at HTTP 200</b> on <c>crowdfunding-offerings</c>, as does
    /// <see cref="FilerCik"/>. Both would record <c>outcome empty</c> as this endpoint's healthy baseline
    /// and match it every week thereafter.</para>
    ///
    /// <para><b>Form C and Form D filers are disjoint populations</b>, measured in both directions on
    /// 2026-08-31, which is why this constant and <see cref="FundraisingCik"/> are separate: each answers
    /// zero rows on the other's paths.</para>
    ///
    /// <para>Chosen as the filer with the <b>most filings — 12</b> — in a 1,000-row latest window rather
    /// than the first one to hand, so the constant does not rest on a single filing that could be amended
    /// away. It answered <b>48 rows</b> on 2026-08-31.</para></summary>
    public const string CrowdfundingCik = "0002010670";

    /// <summary>The name the Regulation Crowdfunding search is probed with — <c>"Finlete"</c>.
    ///
    /// <para><b>Named for the reason <see cref="Exchange"/> is.</b> <c>Probe.Argument</c> maps an
    /// unrecognised <c>name</c> to <see cref="AcquirerNameQuery"/>, and measured 2026-08-31
    /// <c>crowdfunding-offerings-search?name=Apple</c> answers <b>0 rows</b> with HTTP 200.</para>
    ///
    /// <para>Chosen to agree with <see cref="CrowdfundingCik"/> — it is the same issuer — so a diff on one
    /// can be read against the other. It answered <b>4 rows</b> on 2026-08-31. <b>Do not shorten it:</b> this
    /// endpoint's matching rule is not known and intermediate-length queries return nothing —
    /// <c>Well</c> and <c>Wellness</c> both answer 44 rows while <c>Welln</c> answers zero.</para></summary>
    public const string CrowdfundingNameQuery = "Finlete";

    /// <summary>The CIK the Regulation D paths are probed with — <c>"0001617426"</c>, Schutt Private
    /// Investment Fund, LP.
    ///
    /// <para><b>Its own constant, separate from <see cref="CrowdfundingCik"/>, because the two corpora are
    /// disjoint</b> — measured 2026-08-31, this CIK answers <b>0 rows</b> on <c>crowdfunding-offerings</c>
    /// and <see cref="CrowdfundingCik"/> answers 0 rows on <c>fundraising</c>. And separate from
    /// <see cref="Cik"/> and <see cref="FilerCik"/>, both of which answer 0 rows here.</para>
    ///
    /// <para>It answered <b>14 rows</b> on 2026-08-31, spanning 2013-2026 — a filer with enough history that
    /// a single amendment cannot empty it.</para></summary>
    public const string FundraisingCik = "0001617426";

    /// <summary>The name the Regulation D search is probed with — <c>"Apple"</c>, which answered <b>59
    /// rows</b> on 2026-08-31.
    ///
    /// <para><b>Its own constant although it holds the same literal as <see cref="AcquirerNameQuery"/> and
    /// <see cref="CompanyNameQuery"/>.</b> The value coincides by measurement, not because the three paths
    /// share a vocabulary — and a future change to one probe must not silently move the other two. That is
    /// the same reasoning those two constants carry about each other.</para>
    ///
    /// <para>Unlike its crowdfunding sibling this path <i>does</i> behave like a case-insensitive prefix
    /// match, measured 2026-08-31 — <c>Ap</c> 421, <c>App</c> 256, <c>Apple</c> 59, <c>pple</c> 0 — so the
    /// value is not fragile here for the reason <see cref="CrowdfundingNameQuery"/> is.</para></summary>
    public const string FundraisingNameQuery = "Apple";
```

- [ ] **Step 4: Add the four `Probe.Argument` arms**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, in the `string` switch, insert **immediately before**
`"cik" => LiveApi.Cik,`:

```csharp
                // One facade holds BOTH filing corpora and they are disjoint, so the declaring type is not
                // enough — the dispatch keys on the METHOD, following the CongressEndpoints arm above.
                // Measured 2026-08-31 in both directions: a Form C issuer's CIK answers 0 rows on
                // stable/fundraising and a Form D issuer's answers 0 rows on stable/crowdfunding-offerings,
                // both at HTTP 200 with an empty array. And LiveApi.Cik itself — the value this arm exists
                // to shadow — answers 0 rows on BOTH.
                "cik" when parameter.Member.DeclaringType == typeof(Endpoints.FundraisersEndpoints)
                    => parameter.Member.Name
                            == nameof(Endpoints.FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync)
                        ? LiveApi.CrowdfundingCik
                        : LiveApi.FundraisingCik,
```

and **immediately before** `"name" => LiveApi.AcquirerNameQuery,`:

```csharp
                // Same split, same reason: the two search paths match names in two disjoint corpora, and
                // measured 2026-08-31 crowdfunding-offerings-search?name=Apple — which is what
                // AcquirerNameQuery would send — answers 0 rows at HTTP 200.
                "name" when parameter.Member.DeclaringType == typeof(Endpoints.FundraisersEndpoints)
                    => parameter.Member.Name
                            == nameof(Endpoints.FundraisersEndpoints.SearchCrowdfundingOfferingsAsync)
                        ? LiveApi.CrowdfundingNameQuery
                        : LiveApi.FundraisingNameQuery,
```

Then, after the existing `if (type == typeof(ScreenerCriteria)) return new ScreenerCriteria { Limit = 10 };`:

```csharp
        // The two custom-DCF assumption records. An EMPTY record on purpose: every property is nullable and
        // FmpRequest.With drops nulls, so the sweep asks for `symbol` alone and baselines FMP's OWN default
        // valuation rather than an arbitrary set of overrides — which is what makes a week-over-week diff
        // readable. Two arms rather than one, because the two paths honour two different override
        // vocabularies and each silently discards the other's.
        if (type == typeof(Models.CustomDcfAssumptions)) return new Models.CustomDcfAssumptions();
        if (type == typeof(Models.CustomLeveredDcfAssumptions))
            return new Models.CustomLeveredDcfAssumptions();
```

- [ ] **Step 5: Run the offline sweep-coverage tests**

```bash
dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests
```

Expected: PASS. These need no key. In particular
`The_sweep_can_supply_arguments_for_every_endpoint_method` now covers the ten new endpoints, and
`The_sweep_can_read_rows_out_of_every_endpoint_return_type` covers their eight return types.

- [ ] **Step 6: Verify the four constants live before trusting them**

```bash
FMP_API_KEY="$(awk -F= '/^FMP_API_KEY=/{print $2; exit}' .env)" \
  dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests
```

Expected: PASS. Then check the ten new blocks in the recorded run — Step 7 produces them.

**Never `source` the `.env` file and never `set -a` it.** The `awk` form above puts the one variable into the
one command's environment and nothing else. Do **not** set `FMPDOTNET_SMOKE_BULK`.

- [ ] **Step 7: Re-record the live baseline**

```bash
FMP_API_KEY="$(awk -F= '/^FMP_API_KEY=/{print $2; exit}' .env)" \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

Then read the diff before committing it:

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | head -200
grep -n '^\[Fundraisers\.\|^\[DiscountedCashFlow\.' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Expected: **exactly ten new blocks**, six under `[Fundraisers.*]` and four under `[DiscountedCashFlow.*]`,
and the header's `# Measured` date updated. **Every one of the ten must read `outcome rows`.** An
`outcome empty` on any of them means a constant is wrong and this whole task has failed at its purpose —
stop and re-probe rather than recording the empty as a baseline.

Two `null` lines are expected and are measured structural absences rather than blind spots; say so when you
report:

- `null SecurityOfferedOtherDescription` on the crowdfunding blocks — null on 695 of 1,000 rows measured
  2026-08-31, so a probe page can easily hold none.
- `null IncorporatedWithinFiveYears`, `null RevenueRange`, `null SecuritiesOfferedAreOfEquityType` and
  `null YearOfIncorporation` on `[Fundraisers.GetFundraisingByCikAsync]` — that CIK's 14 rows carry all four
  as absences, measured 2026-08-31. They should be **set** on `[Fundraisers.GetFundraisingLatestAsync]`,
  which sweeps the whole market; if they are null there too, report it rather than filing it as noise.

Anything else that records `null` is a wire field the sweep saw empty and belongs in the report.

- [ ] **Step 8: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs \
        tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "test: probe the two fundraising corpora with CIKs and names that belong to them (#39)"
```

---

### Task 9: Bring every number and every claim in the README with the code

**Files:**
- Modify: `README.md` (the "Reaching an endpoint that is not modelled" section, and the smoke-suite
  statistics paragraph)
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs:266-285` (the model-statistics doc figure)
- Create then delete: `tests/FmpDotNet.Tests/ModelStatisticsDelta.cs` (a throwaway that prints the delta)

**Interfaces:**
- Consumes: everything Tasks 2-8 produced, and the regenerated `baseline-ordinary.txt` from Task 8.
- Produces: nothing the code depends on. This is the task that keeps the package's front page from becoming
  a document a reader cannot tell is wrong.

**`docs/superpowers/specs/2026-08-27-endpoint-inventory.md` is NOT modified** — see Ruling 6. It is a dated
provenance record rather than a live table, and nine slices before this one left it alone.

- [ ] **Step 1: Rewrite the remaining-paths section**

In `README.md`, replace the paragraph beginning "The rest is unbuilt rather than blocked" (currently lines
462-465) with:

```markdown
What remains is **blocked rather than unbuilt**. **7 paths remain and none of them is actionable** — they are
the seven `tipranks-*` paths, which need a separately-purchased add-on and return 402 even on FMP's top tier,
so they cannot be built or tested by buying a bigger plan. Every path FMP documents that a top-tier key can
reach is now modelled.
```

Then replace the paragraph beginning "That remainder is tracked as two issues" (currently lines 473-475)
with:

```markdown
That remainder is tracked as one issue under the epic, of 7 paths, carrying the measured path list for its
group. The counts above reconcile exactly against the 243-path inventory: 236 modelled plus 7 remaining, with
no path counted twice and none missing.
```

Leave the "balance is lopsided toward equities" paragraph and the Commodity/Forex/Crypto paragraph exactly as
they are: both are about the *shape* of what was built rather than about what is left, and both are still
true.

- [ ] **Step 2: Check the surrounding claims still hold rather than leaving them standing under new numbers**

```bash
grep -n 'Fundraisers\|Discounted Cash Flow\|not modelled\|unmodelled' README.md
```

Two sentences elsewhere in the README named this group as unbuilt and must be read and corrected if they
still say so. In particular the line that previously read "Fundraisers & DCF is every actionable path that
remains" is gone with Step 1; make sure nothing else repeats it.

- [ ] **Step 3: Measure the model-statistics delta**

The README's smoke-suite paragraph and `Probe.cs`'s blind-spot doc both carry a count of the SDK's nullable
properties, currently **1775**. This slice adds ten records. Write a throwaway that measures the delta rather
than guessing it.

Create `tests/FmpDotNet.Tests/ModelStatisticsDelta.cs`:

```csharp
using System.Reflection;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

public class ModelStatisticsDelta
{
    [Fact]
    public void Print()
    {
        var context = new NullabilityInfoContext();
        int nullable = 0, emptyString = 0, collection = 0;
        var nonNullableValue = new List<string>();

        foreach (var p in new[]
                 {
                     typeof(CrowdfundingOffering), typeof(CrowdfundingSearchHit),
                     typeof(FundraisingNotice), typeof(FundraisingSearchHit),
                     typeof(DcfValuation), typeof(LeveredDcfValuation),
                     typeof(CustomDcfProjection), typeof(CustomLeveredDcfProjection),
                     typeof(CustomDcfAssumptions), typeof(CustomLeveredDcfAssumptions),
                 }
                 .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            var type = p.PropertyType;
            if (Nullable.GetUnderlyingType(type) is not null
                || (!type.IsValueType && context.Create(p).ReadState == NullabilityState.Nullable))
            {
                nullable++;
            }
            else if (type == typeof(string)) emptyString++;
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) collection++;
            else nonNullableValue.Add($"{p.DeclaringType!.Name}.{p.Name} ({type.Name})");
        }

        Assert.Fail($"+{nullable} nullable, +{emptyString} string-defaulting, +{collection} collection, "
                    + $"+{nonNullableValue.Count} non-nullable value: {string.Join(", ", nonNullableValue)}");
    }
}
```

```bash
dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~ModelStatisticsDelta
```

Expected: **`+212 nullable, +0 string-defaulting, +0 collection, +0 non-nullable value:`** —
48 + 3 + 43 + 3 + 4 + 4 + 47 + 34 + 16 + 10 = **212** properties across the ten records. So `1775` becomes
**`1987`**, and the other three figures — 26, 4 and nineteen — are unchanged, because every property this
slice adds is a nullable reference or a nullable value type and none of them is `[JsonIgnore]`d or
computed.

**If the output differs from `+212 / +0 / +0 / +0`, do not paper over it.** A non-zero fourth number names a
property that was typed non-nullable somewhere in Tasks 2-6, which is a defect in those tasks rather than a
number to write down. A different first number means a record has the wrong property count, which Tasks 2, 3,
5 and 6 each have a test for — run them.

```bash
rm tests/FmpDotNet.Tests/ModelStatisticsDelta.cs
```

- [ ] **Step 4: Update both places the statistics live**

In `README.md`, the paragraph under `## The live smoke suite` currently reads "Measured 2026-08-31: **206
ordinary endpoints, 2,510 properties recorded as populated**, and 25 recorded empty…". Read the regenerated
`baseline-ordinary.txt` for the true figures rather than adjusting the old ones:

```bash
grep -c '^\[' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt          # ordinary endpoints
grep -c '^set ' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt        # properties recorded populated
grep -c '^null ' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt       # properties recorded empty
grep -n '^null ' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt       # and WHICH ones
```

Expected: 206 → **216** endpoints, and the other two figures up by whatever the ten new blocks contributed.
Write the measured numbers, not arithmetic on the old ones. Then check the paragraph's three surviving claims
one at a time rather than leaving them standing under new numbers — the discipline commit `a804837`
established:

- **"Two of the 25 are not wire fields but `[JsonIgnore]` parses of them, both on the single-exchange
  market-hours path."** This slice adds no `[JsonIgnore]` property, so that clause should survive unchanged
  against a larger denominator. Re-read it and correct the "of 25" if the count moved.
- **"There is no blind spot on any wire field the SDK models."** Any new `null` line on a `[Fundraisers.*]`
  or `[DiscountedCashFlow.*]` block is a wire field the sweep saw empty. Task 8's Step 7 predicts five, and
  every one is a **measured structural absence** rather than a blind spot —
  `SecurityOfferedOtherDescription` (null on 695 of 1,000 rows) and the four fields that a single Form D
  filer's history leaves blank. Say so in one clause, as the paragraph already does for `Symbol` on
  `News.GetGeneralLatestAsync`, rather than deleting the claim.
- **"of the models' public properties 1775 are nullable"** → the figure Step 3 measured.

Then update the same figure in `tests/FmpDotNet.SmokeTests/Probe.cs:273-275`, which currently reads
"superseding a 2026-08-30 count of 1757 that this slice's own eighteen new nullable properties had already
outgrown: 1775 are nullable". Rewrite that clause for this slice — the superseded count is now 1775 and the
new one is what Step 3 measured — and keep the sentence's shape, because it is the record of how the number
is produced rather than a number someone typed.

- [ ] **Step 5: Run everything, twice**

```bash
dotnet test
```

Expected: PASS, with no filter and no key — the whole solution green offline.

```bash
FMP_API_KEY="$(awk -F= '/^FMP_API_KEY=/{print $2; exit}' .env)" \
  dotnet test tests/FmpDotNet.SmokeTests
```

Expected: PASS against the baseline Task 8 recorded — this is the run that proves the baseline is a baseline
and not a one-off capture. Do **not** set `FMPDOTNET_UPDATE_SMOKE_BASELINE` here; a re-record would hide any
disagreement.

- [ ] **Step 6: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/Probe.cs
git commit -m "docs: bring the README's coverage and smoke figures to 236 of 243 (#39)"
```

- [ ] **Step 7: Report what the sweep actually saw**

The last deliverable is not a file. Report, in the finishing summary:

- the ten new baseline blocks and their `outcome` lines, named individually;
- every `null` line among them, each labelled as a measured structural absence with its count, or flagged as
  an unexplained blind spot if it is not one of the five Task 8 predicted;
- the three measured figures from Step 4, and the delta from Step 3;
- and the two claims this slice **refutes** rather than confirms, since both are now shipped in XML docs that
  a reader will take as fact: FMP's documented "or platform" clause on `crowdfunding-offerings-search`
  (`name=NetCapital` returns 0 rows) and the Python `fmpsdk`'s shared 18-parameter helper (8 of its 18
  levered parameters do nothing).

---
