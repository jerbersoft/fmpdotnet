# Indexes and Market Hours Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two facades, `fmp.Indexes` and `fmp.MarketHours`, covering all nine remaining Indexes and Market
Hours paths, taking SDK coverage from 207 to 216 of 243.

**Architecture:** Nine paths, **four** records — the opposite of the previous slice, where nine paths produced
nine record shapes. The consolidation is the design: three constituent paths share one record, three change
feeds share another, and the two market-hours paths were measured **byte-equal row for row**. The work is not
in the shapes but in what they carry: a field named `founded` that is not a date, a string sentinel where a
time belongs, an optional pair that carries a whole afternoon session, and a boolean that is never `false`.
Two new converters, one existing converter applied to four fields, two guards, and XML documentation that
names what the wire actually does.

**Tech Stack:** .NET 10, C# 13, NodaTime `LocalDate`, `LocalTime` and `OffsetTime`, source-generated
`System.Text.Json` via `FmpJsonContext`, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-indexes-and-market-hours-design.md` (committed `25551e8`)

**Measurements:** `docs/superpowers/specs/2026-08-30-indexes-and-market-hours-measurements.md` (committed
`c308957`, extended `b614571`) — every number in this plan traces to that file. **Read the extended version**:
`b614571` adds the half-open range finding, which was discovered during the spec pass and is not in `c308957`.

## Global Constraints

- **`TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true`.** A `<see cref="...">` pointing at a
  type that does not exist yet is **CS1574, which is a build error, not a warning.** Use the **deferred-cref
  pattern**: write `<c>MarketHoursEndpoints</c>` while the target does not exist, and promote it to a real
  `<see cref>` in the task that creates it. **Task 7 promotes every deferred cref in this plan** and lists them
  by file and anchor.
- **CS1591 is not suppressed project-wide.** Every public type, member and parameter needs an XML doc comment.
  Do not add `#pragma warning disable CS1591`.
- **The assembly declares `IsAotCompatible`.** Every deserialisation goes through `FmpJsonContext`. A
  reflection-based `JsonSerializer.Deserialize` overload in `src/FmpDotNet` fails the build with IL2026/IL3050.
  (The test project has no trim analyser, so `JsonSerializer.Deserialize(fixture, FmpJsonContext.Default.ListX)`
  there is the same call the SDK makes and is what every existing test uses.)
- **Never state a fact that was not measured.** Every number, date and behaviour in a doc comment must come
  from the measurements file and must carry its date — `measured 2026-08-30`.
- **Never log a built URL and never write one into a fixture.** The API key travels in the query string.
  Fixtures are response bodies only: no URL, no host, no `apikey`.
- **Do not set `FMPDOTNET_SMOKE_BULK`.** FMP's documented warning: "Frequent abuse on this API Endpoint may
  result in restrictions placed on this API Key." No task here needs the bulk sweep.
- **Never `source` the `.env` file and never `set -a` it.** It has clobbered `PATH` for a whole shell before.
  Extract the one variable into the one command, exactly as Tasks 1 and 9 show.
- **Line length is 120 characters** in `src/` and `tests/` — the target this slice holds itself to.
  Measured 2026-08-30: **81 of 229** `.cs` files in `src/` and `tests/` already exceed it, and `Models/`
  routinely runs to 130-290 (`CotReport.cs:142` is 141, `EnterpriseValues.cs:19` is 290). So an
  over-long line is a **Minor** finding against house style, not an Important one against repo
  convention. An earlier wording of this bullet claimed the 120 limit matched every file already
  there; that was not measured and is not true.
- **Every bound property is nullable**, including the two booleans. The deserialiser cannot promise a key is
  present. Where no measured row omitted a key, the XML doc says so — the nullability is a statement about the
  deserialiser, not about the data.
- **The five computed members are `[JsonIgnore]`, and that attribute is load-bearing.** Without it the source
  generator emits metadata for `OffsetTime`, which has no converter registered anywhere in this SDK. The
  build is the thing that catches this; do not remove the attribute to "simplify".
- **No client-side compensation for the half-open holiday range, no exchange-code validation, no
  `DateTimeZone` resolution, no `Sector` enum on the response side, and no method that reconstructs index
  membership at a date.** All five are decisions the spec records with its reasons; re-litigating one in code
  is a spec violation.

## Two rulings carried into this plan

### 1. The `AddFmpTests` facade count is edited twice, not once

The spec says "The count moves **20 → 22** in one edit, not two." That is true of the finished diff and false
of the task sequence: Task 6 adds `fmp.Indexes` and Task 7 adds `fmp.MarketHours`, and a task that leaves the
suite red is not a task. **Ruling: Task 6 sets the count to 21, Task 7 sets it to 22.** The spec's sentence is
about how many *lines* change (one), not about how many times the sequence touches it. **Cost if wrong:** one
extra line in one diff, visible in review, no runtime effect.

### 2. `The_holiday_range_excludes_its_own_from_date` is renamed

The spec's falsifiability table names this test for the **upstream's** behaviour — that `from` is exclusive.
A unit test with a stubbed transport cannot observe the upstream at all; it can only observe the query this
SDK sends. Written as the spec names it, the test would either assert nothing or assert a fact it has no
access to, which is exactly the defect class the falsifiability table exists to prevent.

**Ruling: the test is `The_holiday_range_is_sent_verbatim_and_never_widened`**, and it asserts that
`GetHolidaysAsync(x, d, d, ct)` sends `from=d&to=d`. That is falsifiable against the one alternative
implementation anybody would actually write — `from.PlusDays(-1)` to compensate for the exclusive boundary —
which the spec explicitly rejects. The upstream half of the contract is recorded in the measurements file and
documented on the method; no test in this repo can pin it. **Cost if wrong:** the upstream silently changes
`from` to inclusive and this SDK's documentation becomes stale, which no test anywhere would have caught
either way.

## File Structure

**Created (14)**

| file | responsibility |
|---|---|
| `src/FmpDotNet/Models/IndexConstituent.cs` | a current member of an index; `founded` is not a date |
| `src/FmpDotNet/Models/IndexConstituentChange.cs` | one addition *or* one removal; two dates that disagree |
| `src/FmpDotNet/Models/ExchangeMarketHours.cs` | 8 bound + 5 computed; the `"CLOSED"` sentinel and the lunch break |
| `src/FmpDotNet/Models/ExchangeHoliday.cs` | 7 bound + 1 computed; a boolean that is never `false` |
| `src/FmpDotNet/Endpoints/IndexesEndpoints.cs` | the first facade, six methods, no parameters |
| `src/FmpDotNet/Endpoints/MarketHoursEndpoints.cs` | the second facade, three methods, two guards |
| `tests/FmpDotNet.Tests/IndexesTests.cs` | binding, the two constituent traps, the sentinels, request shapes |
| `tests/FmpDotNet.Tests/MarketHoursTests.cs` | the hour parse, the closures, the early close, the guards |
| `tests/FmpDotNet.Tests/Fixtures/dowjones-constituent.head.json` | ISO `founded`, the shape at its friendliest |
| `tests/FmpDotNet.Tests/Fixtures/sp500-constituent.founded.json` | the bare year and the multi-valued forms |
| `tests/FmpDotNet.Tests/Fixtures/nasdaq-constituent.head.json` | a `null` `dateFirstAdded` |
| `tests/FmpDotNet.Tests/Fixtures/historical-dowjones-constituent.head.json` | absence spelled `""` |
| `tests/FmpDotNet.Tests/Fixtures/historical-sp500-constituent.dates.json` | absence spelled `null`; both paddings; the 1957 pair |
| `tests/FmpDotNet.Tests/Fixtures/all-exchange-market-hours.head.json` | six-key, eight-key and `"CLOSED"` rows |
| `tests/FmpDotNet.Tests/Fixtures/exchange-market-hours.NASDAQ.json` | the single-element array |
| `tests/FmpDotNet.Tests/Fixtures/holidays-by-exchange.NASDAQ.json` | a closure, an early close, and the `13:30` outlier |

(That table lists 16 rows because the eight fixtures are itemised; the spec's "8 + fixtures" counts the two
test files and six source files.)

**Modified (10)**

| file | change |
|---|---|
| `src/FmpDotNet/Serialization/NodaConverters.cs` | two new converters, appended; nothing existing changed |
| `src/FmpDotNet/Serialization/FmpJsonContext.cs` | **four** `[JsonSerializable]` entries, not nine |
| `src/FmpDotNet/FmpClient.cs` | two constructor parameters, two properties |
| `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` | two `TryAddTransient` calls |
| `tests/FmpDotNet.Tests/AddFmpTests.cs` | count 20 → 21 → 22, and two `Assert.NotNull` lines |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | `HolidayRangeStart` and `HolidayRangeEnd` |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | two `LocalDate` arms, narrowed by declaring type |
| `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` | one pinning test for the two new arms |
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | nine new outcome blocks, from a live run |
| `README.md` | generated block regenerated; prose arithmetic by hand |
| `docs/…/2026-08-30-indexes-and-market-hours-measurements.md` | Task 1's weekday addendum |

That is eleven rows against the spec's ten. The extra one is the measurements file, which Task 1 amends —
the spec names the two open measurement gaps but not the file that would record their closure.

## What this plan does NOT need to touch

Checked rather than assumed, so no task wastes a step rediscovering it:

- **`EndpointCoverageTests.Argument` needs no new arm.** Read at `tests/FmpDotNet.Tests/EndpointCoverageTests.cs:296`.
  Its string case ends `_ => "AAPL"`, which `ThrowIfNotOneExchange` accepts (no comma, not blank), and its
  `LocalDate` case returns `new LocalDate(2026, 1, 2)` for **both** `from` and `to` — so
  `DateRange.ThrowIfBackwards` does not fire on an equal pair. All nine methods drive cleanly.
- **`Probe.Argument` needs no new *string* arm.** Read at `tests/FmpDotNet.SmokeTests/Probe.cs:356`:
  `"exchange" => LiveApi.Exchange` already exists and `LiveApi.Exchange` is `"NASDAQ"`, which answered 200 on
  both market-hours paths on 2026-08-30. Only the date range is missing.
- **`DateRange.ThrowIfBackwards` already exists** at `src/FmpDotNet/DateRange.cs` and takes `LocalDate?` on
  both ends. Nothing new is needed there.
- **`SentinelStringJsonConverter` already exists** at `src/FmpDotNet/Serialization/NodaConverters.cs:660` and
  folds `null`, `""`, `"N/A"` and `"NULL"` to `null`. It is applied, not written.
- **`DirectoryEndpoints.GetExchangesAsync` already exists** at `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs:266`,
  so the cross-reference in `GetExchangeAsync`'s doc is a real `<see cref>` from the moment it is written.

---

### Task 1: Close the two weekday measurement gaps

Every capture behind this slice was taken on **Sunday 2026-08-30**, and two facts that the models will
document could not be observed on a Sunday:

1. **`isMarketOpen` was `false` on all 81 rows, on every capture.** The field's *type* is measured — a JSON
   boolean on all 81 rows — and nothing else about it is.
2. **Every observed UTC offset in an hour string was positive** (`+03:00` to `+12:00`), because only
   Asia-Pacific and Gulf exchanges were on a trading day. `hh:mm tt o<m>` was verified against `-05:00` and
   `-04:00` inputs directly, so the negative form is covered by *test* rather than by *capture*.

The spec names both as items that "should be closed before this design is implemented", and says three calls
settle them. This task makes those calls and writes down what came back. **It changes no code**, and Tasks 2
onward do not depend on its result: the doc text in Task 4 is given twice below, once for each outcome.

**Files:**
- Modify: `docs/superpowers/specs/2026-08-30-indexes-and-market-hours-measurements.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a dated section headed `## Weekday addendum` whose two findings Task 4 quotes verbatim in the XML
  doc for `ExchangeMarketHours.IsMarketOpen` and `ExchangeMarketHours.OpeningHour`.

- [ ] **Step 1: Decide whether this task can run at all**

```bash
date -u '+%Y-%m-%d %H:%M UTC  %A'
```

The three calls are only worth making inside a window where **some** exchange is trading. Read the result:

- **A weekday, between roughly 00:00 and 21:00 UTC** — proceed. That window covers Tokyo's open through New
  York's close.
- **A Saturday or Sunday** — stop, and record the refusal (Step 5). Every exchange will answer
  `isMarketOpen: false` and every hour string will be `"CLOSED"` or a Gulf-market positive offset, which is
  precisely the capture set already in hand. Making the calls would spend three requests to re-measure
  Sunday.
- **A weekday outside that window** — proceed anyway. Even with every market shut, the *hour strings* are
  still published for the Americas, and those carry the negative offsets that gap 2 is about. Note in Step 5
  that gap 1 remains open.

- [ ] **Step 2: Write the capture harness**

Never `source` the `.env`, and never let a built URL reach a log, a file, or the terminal.

```bash
WORK=$(mktemp -d) && mkdir -p "$WORK/raw" "$WORK/hdr" && cat > "$WORK/probe.sh" <<'SH'
#!/bin/bash
# probe.sh NAME PATH [QUERY] — writes raw/NAME.json and hdr/NAME.txt.
# Prints ONLY name, status, bytes, content-type. Never the URL.
SP="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
KEY="$(awk -F= '/^FMP_API_KEY=/{print $2; exit}' "$REPO/.env")"
[ -n "$KEY" ] || { echo "NO KEY"; exit 1; }
name="$1"; path="$2"; query="$3"
url="https://financialmodelingprep.com/${path}?"
[ -n "$query" ] && url="${url}${query}&"
url="${url}apikey=${KEY}"
code=$(curl -sS -o "$SP/raw/$name.json" -D "$SP/hdr/$name.txt" -w '%{http_code}' \
       --max-time 120 "$url" 2>"$SP/hdr/$name.err")
rc=$?
if [ $rc -ne 0 ]; then
  sed -i '' -E 's/apikey=[A-Za-z0-9]+/apikey=REDACTED/g' "$SP/hdr/$name.err" 2>/dev/null
  echo "$name  CURL_ERR=$rc  $(head -c 200 "$SP/hdr/$name.err" | tr -d '\n')"; exit $rc
fi
echo "$name  $code  $(wc -c < "$SP/raw/$name.json" | tr -d ' ')B"
SH
chmod +x "$WORK/probe.sh" && echo "$WORK"
```

- [ ] **Step 3: Make the three calls**

```bash
REPO=/Users/herbertsabanal/Projects/fmpdotnet
export REPO
"$WORK/probe.sh" wk-all-hours   stable/all-exchange-market-hours
"$WORK/probe.sh" wk-nasdaq      stable/exchange-market-hours       "exchange=NASDAQ"
"$WORK/probe.sh" wk-lse         stable/exchange-market-hours       "exchange=LSE"
```

Expected: three lines each reading `200` with a non-zero byte count. The first is ~10 KB; the two singles are
a couple of hundred bytes each. Anything other than `200` — stop and report; do not proceed to Step 4 with a
partial capture.

- [ ] **Step 4: Read what came back**

```bash
python3 - "$WORK/raw" "$(date -u '+%Y-%m-%d')" <<'PY'
import json, re, sys, collections
raw, today = sys.argv[1], sys.argv[2]
rows = json.load(open(f"{raw}/wk-all-hours.json"))
print(f"date            {today}")
print(f"rows            {len(rows)}")
open_rows = [r for r in rows if r.get("isMarketOpen") is True]
print(f"isMarketOpen T  {len(open_rows)}  {[r['exchange'] for r in open_rows][:12]}")
print(f"isMarketOpen F  {sum(1 for r in rows if r.get('isMarketOpen') is False)}")
print(f"isMarketOpen ?  {sum(1 for r in rows if not isinstance(r.get('isMarketOpen'), bool))}")

offsets = collections.Counter()
for r in rows:
    for key in ("openingHour", "closingHour", "openingAdditional", "closingAdditional"):
        m = re.search(r"([+-]\d{2}:\d{2})$", str(r.get(key) or ""))
        if m: offsets[m.group(1)] += 1
neg = {k: v for k, v in offsets.items() if k.startswith("-")}
print(f"offsets         {len(offsets)} distinct, {sum(offsets.values())} slots")
print(f"negative        {sorted(neg.items())}")
print(f"CLOSED slots    {sum(1 for r in rows for k in ('openingHour','closingHour') if r.get(k) == 'CLOSED')}")
for name in ("wk-nasdaq", "wk-lse"):
    single = json.load(open(f"{raw}/{name}.json"))
    print(f"{name:14}  {json.dumps(single)}")
PY
```

Read the four numbers that matter and nothing else:

- **`isMarketOpen T`** — if this is greater than zero, **gap 1 is closed** and the exchanges named are the
  evidence.
- **`negative`** — if this is non-empty, **gap 2 is closed** by capture rather than by test.
- **`isMarketOpen ?`** must be `0`. Anything else means the field is not always a JSON boolean, which
  contradicts the 81-row measurement and is a finding in its own right.
- The two single-exchange bodies must still be single-element arrays. If either is an object or a
  multi-element array, `GetExchangeAsync`'s "take the first row" design needs revisiting before Task 7 — stop
  and report.

- [ ] **Step 5: Write the addendum**

Append to `docs/superpowers/specs/2026-08-30-indexes-and-market-hours-measurements.md`, filling the bracketed
values from Step 4's output. **Only measured values go in**; if the task was refused at Step 1, write the
refusal section instead and leave both gaps open.

````markdown
## Weekday addendum — measured <DATE>

The two gaps the original capture set could not close, both of which needed a trading day. Three calls,
`all-exchange-market-hours` and `exchange-market-hours` for NASDAQ and LSE.

**Gap 1 — `isMarketOpen` in its `true` state.** <N> of <ROWS> rows answered `true`: <EXCHANGES>. The field was
a JSON boolean on all <ROWS> rows, as on 2026-08-30. *(If zero: "Still `false` on all <ROWS> rows. The call
landed at <TIME> UTC, outside every measured session; the gap stays open and
`ExchangeMarketHours.IsMarketOpen` continues to document the `true` case as unmeasured.")*

**Gap 2 — negative UTC offsets in an hour string.** <LIST>, over <M> hour slots. The Americas publish their
hours with a negative offset, which the 2026-08-30 Sunday capture could not show because those exchanges all
read `"CLOSED"`. *(If empty: "No negative offset appeared. `hh:mm tt o<m>` remains verified against `-05:00`
and `-04:00` by direct pattern test rather than by capture, and the doc says so.")*
````

If Step 1 refused the task, append this instead and stop:

````markdown
## Weekday addendum — not taken

Attempted <DATE> (<WEEKDAY>). Both open gaps need a trading day and this was not one, so the three calls were
not made: they would have re-measured the Sunday state already recorded above at the cost of three requests
against the key's quota. `ExchangeMarketHours.IsMarketOpen` ships documenting the `true` case as unmeasured,
and the negative-offset hour form ships covered by pattern test rather than by capture. Both are stated that
way in the XML docs, which is the honest position and not a defect.
````

- [ ] **Step 6: Check no key leaked, then discard the captures**

```bash
grep -rlE "apikey=[A-Za-z0-9]{8,}" "$WORK" || echo "clean"
rm -rf "$WORK"
```

Expected: `clean`. If it names a file, that file is a header capture that recorded the request line —
`rm -rf "$WORK"` still removes it, but say so in the report.

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/specs/2026-08-30-indexes-and-market-hours-measurements.md
git commit -m "docs: close (or record as open) the two weekday measurement gaps (#38)"
```

---

### Task 2: `LongFormLocalDateJsonConverter`, and `IndexConstituentChange`

The three `historical-*-constituent` paths answer one row shape, measured across **2,055 rows** (86 Dow, 1,525
S&P, 444 Nasdaq) on 2026-08-30. It carries three traps at once: a date in US long form rather than ISO, two
date fields that disagree on 205 rows, and absence spelled **two ways depending on which path you read**.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append one converter)
- Create: `src/FmpDotNet/Models/IndexConstituentChange.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (one entry)
- Create: `tests/FmpDotNet.Tests/IndexesTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/historical-dowjones-constituent.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/historical-sp500-constituent.dates.json`

**Interfaces:**
- Consumes: `SentinelStringJsonConverter` (existing, `NodaConverters.cs:660`), `NullableLocalDateJsonConverter`
  (existing, `NodaConverters.cs:37`).
- Produces: `LongFormLocalDateJsonConverter : JsonConverter<LocalDate?>`, `Models.IndexConstituentChange` with
  properties `DateAdded`, `AddedSecurity`, `RemovedTicker`, `RemovedSecurity`, `Date`, `Symbol`, `Reason`, and
  the context entry `FmpJsonContext.Default.ListIndexConstituentChange`. Task 6 calls all three.

- [ ] **Step 1: Write the two fixtures**

Both are verbatim rows from the 2026-08-30 capture set. Nothing is constructed.

`tests/FmpDotNet.Tests/Fixtures/historical-dowjones-constituent.head.json` — the first two rows of
`stable/historical-dowjones-constituent`, plus the PFE row, which is the removal shape:

```json
[
  { "dateAdded": "June 29, 2026", "addedSecurity": "Alphabet Inc.", "removedTicker": "VZ",
    "removedSecurity": "Verizon Communications Inc.", "date": "2026-06-29", "symbol": "GOOGL",
    "reason": "To better reflect today's U.S. economy, where AI, cloud computing, and digital services play a much larger role than traditional telecommunications." },
  { "dateAdded": "November 8, 2024", "addedSecurity": "Sherwin-Williams", "removedTicker": "DOW",
    "removedSecurity": "Dow Inc.", "date": "2024-11-07", "symbol": "SHW",
    "reason": "Market capitalization change" },
  { "dateAdded": "August 31, 2020", "addedSecurity": "", "removedTicker": "PFE",
    "removedSecurity": "Pfizer Inc", "date": "2020-08-31", "symbol": "PFE",
    "reason": "Market capitalization change" }
]
```

`tests/FmpDotNet.Tests/Fixtures/historical-sp500-constituent.dates.json` — five rows drawn from the same
1,525-row `stable/historical-sp500-constituent` capture, chosen for what each one proves. **This fixture
assembles rows from one response, not from several**; the ordering below is not the wire's:

```json
[
  { "dateAdded": "August 05, 2026", "addedSecurity": "Ferguson plc", "removedTicker": "EA",
    "removedSecurity": "Electronic Arts ", "date": "2026-08-05", "symbol": "FERG",
    "reason": "Electronic Arts was acquired by an investor consortium consisting of Saudi Arabia's Public Investment Fund (PIF), Silver Lake, and Affinity Partners." },
  { "dateAdded": "July 9, 2025", "addedSecurity": "Datadog", "removedTicker": "JNPR",
    "removedSecurity": "Juniper Networks", "date": "2025-07-08", "symbol": "DDOG",
    "reason": "S&P 500 constituent Hewlett Packard Enterprise Co. acquired Juniper Networks." },
  { "dateAdded": "July 7, 2003", "addedSecurity": "Prologis", "removedTicker": null,
    "removedSecurity": null, "date": "2003-07-17", "symbol": "PLD",
    "reason": "Market capitalization changes" },
  { "dateAdded": "March 04, 1957", "addedSecurity": "St. Regis Corp", "removedTicker": "",
    "removedSecurity": "", "date": "1957-03-03", "symbol": "SRT", "reason": "" },
  { "dateAdded": "March 04, 1957", "addedSecurity": "American Electric Power", "removedTicker": "",
    "removedSecurity": "", "date": "1957-03-04", "symbol": "AEP", "reason": "" }
]
```

Row by row: **FERG** is the zero-padded day (`August 05`); **DDOG** is the unpadded day *and* the one-day
disagreement (`July 9, 2025` against `2025-07-08`); **PLD** is absence spelled as JSON `null`, which the Dow
Jones feed never sends; the **two 1957 rows** carry the *identical* `dateAdded` and different `date` values,
which is what proves neither field is derived from the other.

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/IndexesTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The six Indexes paths, checked against captures taken live 2026-08-30.</summary>
public class IndexesTests
{
    [Fact]
    public void A_change_row_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 29), rows[0].DateAdded);
        Assert.Equal("Alphabet Inc.", rows[0].AddedSecurity);
        Assert.Equal("VZ", rows[0].RemovedTicker);
        Assert.Equal("Verizon Communications Inc.", rows[0].RemovedSecurity);
        Assert.Equal(new LocalDate(2026, 6, 29), rows[0].Date);
        Assert.Equal("GOOGL", rows[0].Symbol);
        Assert.StartsWith("To better reflect", rows[0].Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void A_long_form_date_binds_under_any_culture(string culture)
    {
        // `dateAdded` is US long form with ENGLISH month names — "June 29, 2026" — on all 2,055 rows measured
        // 2026-08-30. A pattern built from the ambient culture parses none of them on a German or French
        // host: "June" is "Juni" there, and NodaTime answers a parse failure, which this file's converters
        // turn into null. The whole column would arrive empty in production and green in CI.
        //
        // WHAT THIS TEST CATCHES, stated exactly: an implementation that builds its pattern from
        // CultureInfo.CurrentCulture PER CALL fails here every time. One that builds a static pattern from
        // the current culture fails here only if this test runs before anything else touches the converter,
        // because a static pattern captures the culture at type-initialisation time. The invariant pattern
        // the converter actually uses is immune to both, which is the point.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(culture, CultureInfo.CurrentCulture.Name);   // the setter must actually have taken

            var rows = JsonSerializer.Deserialize(
                Binding.Fixture("historical-dowjones-constituent.head.json"),
                FmpJsonContext.Default.ListIndexConstituentChange)!;

            Assert.Equal(new LocalDate(2026, 6, 29), rows[0].DateAdded);
            Assert.Equal(new LocalDate(2024, 11, 8), rows[1].DateAdded);
            Assert.Equal(new LocalDate(2020, 8, 31), rows[2].DateAdded);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Both_day_paddings_parse_because_the_wire_sends_both()
    {
        // Measured 2026-08-30 over historical-sp500-constituent alone: 213 rows carry a zero-padded
        // single-digit day and 407 carry an unpadded one. A pattern of "MMMM dd, yyyy" parses only the first
        // group and a pattern of "MMMM d, yyyy" parses BOTH, which is why the converter uses the latter.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(new LocalDate(2026, 8, 5), rows[0].DateAdded);   // "August 05, 2026" — padded
        Assert.Equal(new LocalDate(2025, 7, 9), rows[1].DateAdded);   // "July 9, 2025"    — unpadded
    }

    [Fact]
    public void dateAdded_and_date_are_read_separately()
    {
        // They disagree on 205 of 2,055 rows measured 2026-08-30 — 202 by exactly one day with `date` the
        // earlier — so deriving either from the other is wrong 205 times. The 1957 pair is the proof that
        // they are two facts and not one value rendered twice: identical `dateAdded`, different `date`.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(new LocalDate(2025, 7, 9), rows[1].DateAdded);
        Assert.Equal(new LocalDate(2025, 7, 8), rows[1].Date);
        Assert.NotEqual(rows[1].DateAdded, rows[1].Date);

        Assert.Equal(rows[3].DateAdded, rows[4].DateAdded);           // both "March 04, 1957"
        Assert.Equal(new LocalDate(1957, 3, 3), rows[3].Date);
        Assert.Equal(new LocalDate(1957, 3, 4), rows[4].Date);
    }

    [Fact]
    public void The_dow_jones_feed_spells_absence_with_an_empty_string()
    {
        // 136 empty strings and ZERO JSON nulls across all 86 Dow Jones rows, measured 2026-08-30. An
        // implementer who tests only against this path never meets the other spelling, which is why the
        // sentinel converter is applied to all four text fields rather than to the ones that looked null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Null(rows[2].AddedSecurity);                            // wire sent ""
        Assert.Equal("PFE", rows[2].RemovedTicker);
    }

    [Fact]
    public void The_sp500_feed_spells_absence_with_a_json_null_instead()
    {
        // 823 empty strings AND 20 JSON nulls across the same four fields on historical-sp500-constituent,
        // measured 2026-08-30; historical-nasdaq-constituent adds 83 and 8. Two spellings of one fact, and
        // which one arrives depends on the path. Both must land on null or a caller needs to know both.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Null(rows[2].RemovedTicker);                            // wire sent JSON null
        Assert.Null(rows[2].RemovedSecurity);
        Assert.Null(rows[3].RemovedTicker);                            // wire sent ""
        Assert.Null(rows[3].Reason);
        Assert.Equal("Prologis", rows[2].AddedSecurity);
    }

    [Fact]
    public void A_row_is_an_addition_or_a_removal_and_symbol_names_whichever_it_is()
    {
        // Measured across 2,055 rows: never both, never neither. `symbol` follows the populated side, so a
        // caller reading `symbol` as "the security that joined" is wrong on every removal row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal("GOOGL", rows[0].Symbol);                         // an addition: symbol is the joiner
        Assert.Equal("Alphabet Inc.", rows[0].AddedSecurity);

        Assert.Equal("PFE", rows[2].Symbol);                           // a removal: symbol is the leaver
        Assert.Null(rows[2].AddedSecurity);
        Assert.Equal("PFE", rows[2].RemovedTicker);
    }

    [Fact]
    public void The_long_form_converter_does_not_round_trip_a_zero_padded_day()
    {
        // Not a defect and not a TODO — a measured impossibility, pinned so nobody "fixes" it into a
        // pattern that stops parsing half the corpus. The wire sends BOTH paddings and no single NodaTime
        // pattern emits both, so Write normalises to the unpadded form. Read accepts either, so nothing is
        // lost on a round trip through this SDK; only the exact bytes differ.
        var row = JsonSerializer.Deserialize(
            """[{"dateAdded":"August 05, 2026"}]""",
            FmpJsonContext.Default.ListIndexConstituentChange)![0];

        Assert.Equal(new LocalDate(2026, 8, 5), row.DateAdded);
        Assert.Contains(
            "\"dateAdded\":\"August 5, 2026\"",
            JsonSerializer.Serialize(new List<Models.IndexConstituentChange> { row },
                FmpJsonContext.Default.ListIndexConstituentChange),
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_iso_date_in_dateAdded_would_not_parse_and_that_is_why_it_needs_its_own_converter()
    {
        // NullableLocalDateJsonConverter uses LocalDatePattern.Iso, which rejects "June 29, 2026" outright
        // and returns null rather than throwing — so reusing it here would have emptied the column with no
        // error anywhere. The inverse is true too, which this asserts: the long-form pattern does not accept
        // ISO. Neither converter can cover the other's path, and this record uses both, one per field.
        var row = JsonSerializer.Deserialize(
            """[{"dateAdded":"2026-06-29","date":"2026-06-29"}]""",
            FmpJsonContext.Default.ListIndexConstituentChange)![0];

        Assert.Null(row.DateAdded);
        Assert.Equal(new LocalDate(2026, 6, 29), row.Date);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests"
```

Expected: the project does not compile — `ListIndexConstituentChange` is not a member of
`FmpJsonContext.Default`, and `Models.IndexConstituentChange` does not exist. A compile failure is the correct
first result here; a passing run would mean the fixtures are not being read.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `YesNoBooleanJsonConverter`:

```csharp
/// <summary>Reads FMP's US long-form dates — <c>"June 29, 2026"</c> — as a <see cref="LocalDate"/>.
///
/// <para><b>Written for the three <c>historical-*-constituent</c> paths</b>, whose <c>dateAdded</c> is the
/// only long-form date in this SDK. Every one of the <b>2,055</b> values measured 2026-08-30 parsed with
/// <c>MMMM d, yyyy</c>. Its sibling field <c>date</c> on the same row is ISO and takes
/// <see cref="NullableLocalDateJsonConverter"/> — two date formats in one object, which is why this record
/// carries two date converters rather than one.</para>
///
/// <para><b>Invariant culture is load-bearing, not boilerplate.</b> The month names are English. A pattern
/// built from the ambient culture parses <b>nothing</b> on a German or French host — and because this file's
/// converters answer an unparseable value with <see langword="null"/> rather than throwing, the column would
/// arrive empty in production and green in CI. That is the failure this converter is shaped to prevent.</para>
///
/// <para><b><see cref="Write"/> cannot round-trip the wire byte for byte, and that is measured rather than
/// sloppy.</b> The wire uses <b>both</b> day paddings — measured 2026-08-30 on
/// <c>historical-sp500-constituent</c> alone, 213 values carry a zero-padded single-digit day
/// (<c>"August 05, 2026"</c>) and 407 carry an unpadded one (<c>"November 8, 2024"</c>). No single NodaTime
/// pattern emits both, so <c>d</c> is chosen because it <b>parses</b> both; a zero-padded input therefore
/// comes back unpadded. Nothing is lost — <see cref="Read"/> accepts either form — but a test that asserts a
/// byte-identical round trip on this converter is asserting something untrue, and the guard test asserts the
/// parsed value instead.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad date costs one field rather
/// than the whole response.</para></summary>
public sealed class LongFormLocalDateJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDatePattern Pattern =
        LocalDatePattern.CreateWithInvariantCulture("MMMM d, yyyy");

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

- [ ] **Step 5: Write `IndexConstituentChange`**

Create `src/FmpDotNet/Models/IndexConstituentChange.cs`. **`<c>IndexesEndpoints</c>` is a deferred cref** —
that type arrives in Task 6, which promotes it.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One change to an index's membership — an addition <b>or</b> a removal, never both — from the
/// three <c>stable/historical-*-constituent</c> paths.
///
/// <para><b>A row is a change, not a constituent, and this record is named for that.</b> Measured across
/// <b>2,055</b> rows on 2026-08-30 (86 Dow Jones, 1,525 S&amp;P 500, 444 Nasdaq), each row is <i>either</i> an
/// addition — <see cref="AddedSecurity"/> populated, <see cref="RemovedTicker"/> empty — <i>or</i> a removal,
/// with <see cref="Symbol"/> naming whichever it is. A caller reading <see cref="Symbol"/> as "the security
/// that joined" is wrong on every removal row.</para>
///
/// <para><b>This feed cannot answer "who was in the index on date X".</b> Of the 628 current constituents
/// carrying a <c>dateFirstAdded</c>, <b>24 have no addition row at all</b> in the matching feed, so replaying
/// the changes does not reconstruct the membership. That is why the methods are named
/// <c>…ConstituentChangesAsync</c> rather than <c>GetHistorical…ConstituentsAsync</c>, and why this SDK
/// offers no as-of-date membership method — see <c>IndexesEndpoints</c>.</para>
///
/// <para><b>One record serves all three paths.</b> The key tuple was identical on every row of all three
/// responses, measured 2026-08-30. What differs between them is not the shape but how they spell absence —
/// see <see cref="AddedSecurity"/>.</para></summary>
public sealed record IndexConstituentChange
{
    /// <summary>The date the change was announced or recorded, in FMP's US long form on the wire —
    /// <c>"June 29, 2026"</c>.
    ///
    /// <para><b>Not the same value as <see cref="Date"/>, and not derived from it.</b> The two disagree on
    /// <b>205 of 2,055</b> rows measured 2026-08-30 — 202 of them by exactly one day, with <see cref="Date"/>
    /// the earlier — plus three larger outliers. The disagreement is not a legacy artefact: 151 of the 205
    /// come from a single 1957 backfill, but <b>40 fall in 2024–2026 against 47 agreeing rows in the same
    /// span</b>, so in recent data the two differ on 46% of rows.</para>
    ///
    /// <para>Parsed by <see cref="LongFormLocalDateJsonConverter"/>, which is invariant-culture and cannot
    /// round-trip a zero-padded day. Read that converter before changing this attribute.</para></summary>
    [JsonPropertyName("dateAdded")]
    [JsonConverter(typeof(LongFormLocalDateJsonConverter))]
    public LocalDate? DateAdded { get; init; }

    /// <summary>The security that joined the index, by name — <see langword="null"/> on a removal row.
    ///
    /// <para><b>Absence is spelled two ways and which one arrives depends on the path.</b> Measured
    /// 2026-08-30 across the four text fields on this record: <c>historical-dowjones-constituent</c> sent
    /// <b>136 empty strings and zero JSON nulls</b> over all 86 rows; <c>historical-sp500-constituent</c> sent
    /// 823 empty strings <b>and 20 JSON nulls</b>; <c>historical-nasdaq-constituent</c> sent 83 and 8. An
    /// implementer who tests against the Dow Jones path alone never meets the second spelling.
    /// <see cref="SentinelStringJsonConverter"/> folds both to <see langword="null"/> so a caller needs to
    /// know neither.</para></summary>
    [JsonPropertyName("addedSecurity")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? AddedSecurity { get; init; }

    /// <summary>The ticker that left the index — <see langword="null"/> on an addition row. Absence is spelled
    /// two ways; see <see cref="AddedSecurity"/>.</summary>
    [JsonPropertyName("removedTicker")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? RemovedTicker { get; init; }

    /// <summary>The security that left the index, by name — <see langword="null"/> on an addition row.
    /// Absence is spelled two ways; see <see cref="AddedSecurity"/>.</summary>
    [JsonPropertyName("removedSecurity")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? RemovedSecurity { get; init; }

    /// <summary>The effective date of the change, ISO on the wire — <c>"2026-06-29"</c>.
    ///
    /// <para><b>A different field from <see cref="DateAdded"/> and a different wire format.</b> Both are
    /// surfaced because neither can be computed from the other; the measurement is on
    /// <see cref="DateAdded"/>.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The ticker the row is about — the security that joined on an addition row, and the one that
    /// left on a removal row. Never both.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Free text explaining the change — <c>"Market capitalization change"</c> and similar.
    ///
    /// <para><see langword="null"/> where the wire sent a sentinel; the whole 1957 backfill sends
    /// <c>""</c> here. Absence is spelled two ways; see <see cref="AddedSecurity"/>.</para></summary>
    [JsonPropertyName("reason")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Reason { get; init; }
}
```

- [ ] **Step 6: Register the record with the source generator**

Append to `src/FmpDotNet/Serialization/FmpJsonContext.cs`, immediately before the closing
`internal sealed partial class FmpJsonContext : JsonSerializerContext;` line:

```csharp
// Indexes and Market Hours (#38). FOUR entries for nine paths, not nine: three constituent paths share
// IndexConstituent, three change feeds share IndexConstituentChange, and the two market-hours paths were
// measured byte-equal row for row and share ExchangeMarketHours.
[JsonSerializable(typeof(List<IndexConstituentChange>))]
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests"
```

Expected: PASS, 10 tests (8 `[Fact]` plus the 2-case `[Theory]`).

- [ ] **Step 8: Run the whole suite**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: PASS. This task adds no endpoint, so `EndpointCoverageTests` is unaffected and the suite is fully
green — the last task in this plan for which that is true until Task 9.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/IndexConstituentChange.cs tests/FmpDotNet.Tests/IndexesTests.cs \
        tests/FmpDotNet.Tests/Fixtures/historical-dowjones-constituent.head.json \
        tests/FmpDotNet.Tests/Fixtures/historical-sp500-constituent.dates.json
git commit -m "feat: bind the index constituent change feed, with a long-form date converter (#38)"
```

---

### Task 3: `IndexConstituent`, and the field that is not a date

The three current-membership paths answer one row shape over **635 rows** measured 2026-08-30 (30 Dow Jones,
503 S&P 500, 102 Nasdaq). It has one trap, and it is the single most consequential binding decision in this
slice: **`founded` looks like a date on two of the three paths and is not one on the third.**

**Files:**
- Create: `src/FmpDotNet/Models/IndexConstituent.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (one entry)
- Modify: `tests/FmpDotNet.Tests/IndexesTests.cs` (add tests)
- Create: `tests/FmpDotNet.Tests/Fixtures/dowjones-constituent.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sp500-constituent.founded.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/nasdaq-constituent.head.json`

**Interfaces:**
- Consumes: `NullableLocalDateJsonConverter` (existing).
- Produces: `Models.IndexConstituent` with properties `Symbol`, `Name`, `Sector`, `SubSector`,
  `Headquarters`, `DateFirstAdded`, `Cik`, `Founded`, and the context entry
  `FmpJsonContext.Default.ListIndexConstituent`. Task 6 calls both.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/dowjones-constituent.head.json` — the first two rows. This is the shape at its
friendliest: `founded` is ISO on **30 of 30** Dow Jones rows, which is exactly how an implementer talks
themselves into a `LocalDate?`.

```json
[
  { "symbol": "GOOGL", "name": "Alphabet Inc.", "sector": "Communication Services",
    "subSector": "Internet Content & Information", "headQuarter": "Mountain View, California",
    "dateFirstAdded": "2026-06-29", "cik": "0001652044", "founded": "1998-09-04" },
  { "symbol": "NVDA", "name": "Nvidia", "sector": "Technology", "subSector": "Semiconductors",
    "headQuarter": "Santa Clara, CA", "dateFirstAdded": "2024-11-08", "cik": "0001045810",
    "founded": "1993-04-05" }
]
```

`tests/FmpDotNet.Tests/Fixtures/sp500-constituent.founded.json` — five rows from the 503-row capture, one for
each form `founded` takes. Assembled from one response; the ordering is not the wire's.

```json
[
  { "symbol": "MMM", "name": "3M", "sector": "Industrials", "subSector": "Conglomerates",
    "headQuarter": "Saint Paul, Minnesota", "dateFirstAdded": "1957-03-04", "cik": "0000066740",
    "founded": "1902" },
  { "symbol": "KLAC", "name": "KLA Corporation", "sector": "Technology", "subSector": "Semiconductors",
    "headQuarter": "Milpitas, California", "dateFirstAdded": "1997-09-30", "cik": "0000319201",
    "founded": "1975/1977" },
  { "symbol": "LOW", "name": "Lowe's", "sector": "Consumer Cyclical", "subSector": "Home Improvement",
    "headQuarter": "Mooresville, North Carolina", "dateFirstAdded": "1984-02-29", "cik": "0000060667",
    "founded": "1904/1946/1959" },
  { "symbol": "NSC", "name": "Norfolk Southern Railway", "sector": "Industrials", "subSector": "Railroads",
    "headQuarter": "Atlanta, Georgia", "dateFirstAdded": "1957-03-04", "cik": "0000702165",
    "founded": "1881/1894" },
  { "symbol": "RDDT", "name": "Reddit, Inc.", "sector": "Communication Services",
    "subSector": "Internet Content & Information", "headQuarter": "San Francisco, CA",
    "dateFirstAdded": "2026-08-18", "cik": "0001713445", "founded": "2005-06-23" }
]
```

`tests/FmpDotNet.Tests/Fixtures/nasdaq-constituent.head.json` — the `null` `dateFirstAdded` case, which
appears on exactly 7 of 102 Nasdaq rows (ADBE, AMAT, CSCO, FAST, MSFT, PAYX, QCOM) and on neither other path:

```json
[
  { "symbol": "ADBE", "name": "Adobe Inc.", "sector": "Technology",
    "subSector": "Software - Infrastructure", "headQuarter": "San Jose, CA", "dateFirstAdded": null,
    "cik": "0000796343", "founded": "1982-12-01" },
  { "symbol": "SPCX", "name": "Space Exploration Technologies Corp.", "sector": "Industrials",
    "subSector": "Aerospace & Defense", "headQuarter": "Starbase, TX", "dateFirstAdded": "2026-07-07",
    "cik": "0001181412", "founded": "2002-03-14" }
]
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/FmpDotNet.Tests/IndexesTests.cs`:

```csharp
    [Fact]
    public void A_constituent_binds_all_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("GOOGL", rows[0].Symbol);
        Assert.Equal("Alphabet Inc.", rows[0].Name);
        Assert.Equal("Communication Services", rows[0].Sector);
        Assert.Equal("Internet Content & Information", rows[0].SubSector);
        Assert.Equal("Mountain View, California", rows[0].Headquarters);
        Assert.Equal(new LocalDate(2026, 6, 29), rows[0].DateFirstAdded);
        Assert.Equal("0001652044", rows[0].Cik);
        Assert.Equal("1998-09-04", rows[0].Founded);
    }

    [Fact]
    public void Founded_is_a_string_because_the_sp500_sends_bare_years()
    {
        // THE test of this task, and the one most likely to be written unfalsifiably. Fed only the Dow
        // Jones fixture — 30 of 30 rows ISO — it passes against a LocalDate? binding too, which is exactly
        // how the wrong type gets shipped. It must be fed the S&P forms.
        //
        // Measured 2026-08-30 across 635 rows: dowjones-constituent 30/30 ISO, nasdaq-constituent 102/102
        // ISO, sp500-constituent 23 ISO, 477 BARE YEARS and 3 multi-valued. A LocalDate? binding is correct
        // on 155 of 635 rows and silently drops 95.4% of the S&P values, because
        // NullableLocalDateJsonConverter answers an unparseable string with null rather than throwing. The
        // loss surfaces as an error nowhere.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sp500-constituent.founded.json"),
            FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.Equal("1902", rows[0].Founded);                 // a bare year: not a date at all
        Assert.Equal("1975/1977", rows[1].Founded);            // KLAC — two foundings
        Assert.Equal("1904/1946/1959", rows[2].Founded);       // LOW — three
        Assert.Equal("1881/1894", rows[3].Founded);            // NSC — two
        Assert.Equal("2005-06-23", rows[4].Founded);           // and the ISO form, on the same path

        // Every row carried a value. Under a LocalDate? binding four of these five arrive null, and this
        // test would not even COMPILE — comparing a LocalDate? to "1902" is a type error — which is the
        // strongest falsifiability available and the reason the assertions are string comparisons rather
        // than a null check.
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Founded)));
    }

    [Fact]
    public void DateFirstAdded_is_a_real_date_and_is_null_on_seven_nasdaq_rows()
    {
        // The other date-shaped field on this record IS a date — ISO on all 628 non-null values measured
        // 2026-08-30, with no second pattern anywhere. It is null on exactly 7 of 102 Nasdaq rows and never
        // null on the other two paths, so a non-nullable binding would have thrown on a live Nasdaq call.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("nasdaq-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.Null(rows[0].DateFirstAdded);
        Assert.Equal(["DateFirstAdded"], Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 7, 7), rows[1].DateFirstAdded);
        Assert.Empty(Binding.Unbound(rows[1]));
    }

    [Fact]
    public void Sector_is_a_string_and_not_the_query_side_enum()
    {
        // All 11 distinct sector values measured across 635 rows fall inside FmpDotNet.Sector and none
        // outside it — and the record still binds a string. That enum exists to BUILD a `sector=` query
        // value; nothing measured says what happens when FMP adds a twelfth, and a response-side enum would
        // turn that into a deserialisation failure on a row the caller could otherwise have read. Every
        // other response record in this SDK binds `sector` as a string for the same reason.
        //
        // subSector is free text by any reading: 114 distinct values over the same 635 rows.
        var rows = JsonSerializer.Deserialize(
            """[{"sector":"Wormholes","subSector":"Traversable"}]""",
            FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.Equal("Wormholes", rows[0].Sector);
        Assert.Equal("Traversable", rows[0].SubSector);
    }

    [Fact]
    public void The_row_count_is_not_a_company_count()
    {
        // sp500-constituent returned 503 rows over 500 distinct CIKs measured 2026-08-30 — FOX/FOXA,
        // NWS/NWSA and GOOGL/GOOG are the three pairs — and nasdaq-constituent 102 rows over 101. Every
        // `name` is distinct too, so neither `name` nor `symbol` identifies a company and a caller
        // de-duplicating on either gets the wrong answer. The record therefore promises no uniqueness; this
        // test pins that Cik is surfaced, which is the only field that could support one.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sp500-constituent.founded.json"),
            FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Cik)));
        Assert.Equal("0000066740", rows[0].Cik);
    }

    [Fact]
    public void The_headquarters_key_is_spelled_headQuarter_on_the_wire()
    {
        // One wire key, one house name, and the attribute is the only thing joining them. Deleting it binds
        // nothing, silently — Binding.Unbound above is the only other thing that would notice.
        var rows = JsonSerializer.Deserialize(
            """[{"headQuarter":"Starbase, TX"}]""", FmpJsonContext.Default.ListIndexConstituent)!;

        Assert.Equal("Starbase, TX", rows[0].Headquarters);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests"
```

Expected: compile failure — `ListIndexConstituent` is not a member of `FmpJsonContext.Default`.

- [ ] **Step 4: Write `IndexConstituent`**

Create `src/FmpDotNet/Models/IndexConstituent.cs`. **`<c>IndexesEndpoints</c>` is a deferred cref**; Task 6
promotes it.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One current member of an index, from <c>stable/dowjones-constituent</c>,
/// <c>stable/sp500-constituent</c> and <c>stable/nasdaq-constituent</c>.
///
/// <para><b>One record for three paths, and that is what the wire sends.</b> The key tuple was identical on
/// every row of all three responses measured 2026-08-30 — 635 rows in total, 30 Dow Jones, 503 S&amp;P 500 and
/// 102 Nasdaq.</para>
///
/// <para><b>A row count is not a company count.</b> <c>sp500-constituent</c> returned 503 rows over
/// <b>500 distinct CIKs</b> — FOX/FOXA, NWS/NWSA and GOOGL/GOOG — and <c>nasdaq-constituent</c> 102 rows over
/// 101. Every <see cref="Name"/> is distinct as well, so neither <see cref="Name"/> nor <see cref="Symbol"/>
/// identifies a company; <see cref="Cik"/> is the only field that does.</para>
///
/// <para><b>This is the membership as of the call, with no history in it.</b> The change feeds are a
/// different record — see <see cref="IndexConstituentChange"/> — and they cannot be replayed to reconstruct
/// membership at a past date.</para></summary>
public sealed record IndexConstituent
{
    /// <summary>The ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name. Distinct on all 635 rows measured 2026-08-30, which is not the same as
    /// unique per company — see the note on this record about FOX/FOXA.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>FMP's sector label — <c>"Technology"</c>, <c>"Industrials"</c>.
    ///
    /// <para><b>A string, not <see cref="FmpDotNet.Sector"/>, and that is deliberate.</b> All 11 distinct
    /// values measured across 635 rows on 2026-08-30 fall inside that enum and none outside it. The enum
    /// exists to build a <c>sector=</c> <b>query</b> value; nothing measured says what FMP does when it adds a
    /// twelfth sector, and a response-side enum would turn that into a deserialisation failure on a row the
    /// caller could otherwise have read. Every other response record in this SDK binds <c>sector</c> as a
    /// string.</para></summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>FMP's finer classification — <c>"Semiconductors"</c>, <c>"Home Improvement"</c>. Free text:
    /// 114 distinct values over 635 rows, measured 2026-08-30.</summary>
    [JsonPropertyName("subSector")] public string? SubSector { get; init; }

    /// <summary>Where the company is based, as free text — <c>"Mountain View, California"</c>,
    /// <c>"Starbase, TX"</c>. The wire spells this key <c>headQuarter</c>, singular.</summary>
    [JsonPropertyName("headQuarter")] public string? Headquarters { get; init; }

    /// <summary>When the company joined the index.
    ///
    /// <para><b>This one is a real date</b>, unlike <see cref="Founded"/>: ISO on all 628 non-null values
    /// measured 2026-08-30, with no second pattern on any path. It is <see langword="null"/> on <b>7 of 102</b>
    /// Nasdaq rows — ADBE, AMAT, CSCO, FAST, MSFT, PAYX and QCOM — and never null on the other two
    /// paths.</para></summary>
    [JsonPropertyName("dateFirstAdded")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateFirstAdded { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten digits on every row measured 2026-08-30. The
    /// only field on this record that identifies a company.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>When the company was founded — <b>as text, because it is not a date</b>.
    ///
    /// <para><b>The most consequential binding decision on this record, and it is measured rather than
    /// cautious.</b> Across 635 rows on 2026-08-30 the field takes three forms, and which one arrives depends
    /// entirely on the path:</para>
    ///
    /// <list type="bullet">
    ///   <item><description><c>dowjones-constituent</c> — ISO <c>uuuu-MM-dd</c> on <b>30 of 30</b> rows.</description></item>
    ///   <item><description><c>nasdaq-constituent</c> — ISO on <b>102 of 102</b> rows.</description></item>
    ///   <item><description><c>sp500-constituent</c> — ISO on 23, a <b>bare year</b> on <b>477 of 503</b>,
    ///     and three values that are neither.</description></item>
    /// </list>
    ///
    /// <para>An implementer who models this from the Dow Jones response types it <see cref="LocalDate"/> and
    /// is correct on 155 of 635 rows. On <c>sp500-constituent</c> that binding drops <b>95.4%</b> of the
    /// values <b>silently</b>, because <see cref="NullableLocalDateJsonConverter"/> answers an unparseable
    /// string with <see langword="null"/> rather than throwing.</para>
    ///
    /// <para>The three remaining values are not malformed dates — they are multi-valued company history.
    /// <c>KLAC</c> sends <c>1975/1977</c>, <c>LOW</c> sends <c>1904/1946/1959</c>, <c>NSC</c> sends
    /// <c>1881/1894</c>. There is nothing in that field for a date pattern to return, on any path, so the SDK
    /// hands the caller what FMP sent and lets them decide.</para></summary>
    [JsonPropertyName("founded")] public string? Founded { get; init; }
}
```

- [ ] **Step 5: Register the record with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`, beside the entry added in Task 2:

```csharp
[JsonSerializable(typeof(List<IndexConstituent>))]
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests"
```

Expected: PASS, 16 tests.

- [ ] **Step 7: Run the whole suite**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: PASS. Still no endpoint added, so `EndpointCoverageTests` remains green.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/IndexConstituent.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/IndexesTests.cs tests/FmpDotNet.Tests/Fixtures/dowjones-constituent.head.json \
        tests/FmpDotNet.Tests/Fixtures/sp500-constituent.founded.json \
        tests/FmpDotNet.Tests/Fixtures/nasdaq-constituent.head.json
git commit -m "feat: bind index constituents, with founded as text because it is not a date (#38)"
```

---

### Task 4: `ExchangeMarketHours` — the sentinel, the lunch break, and the parsed time

The two market-hours paths answer **one** shape. That is not a simplification: for each of seven exchanges
cross-checked on 2026-08-30, the single row from `exchange-market-hours?exchange=X` compared **equal, key for
key and value for value**, to that exchange's row inside the 81-row `all-exchange-market-hours` response.

This record is the design's most subtle decision, and the reason is worth stating before writing it. The
user's decision was "parse the hours to a real time type, plus a flag that says why `null` is `null`". A
converter cannot deliver that: a `JsonConverter<OffsetTime?>` sees one field and can set one property, so
nothing could populate `IsClosedToday` — and two properties cannot share one `[JsonPropertyName]`. **Binding
the raw text and computing the rest is the only shape that gives the caller a real time type and keeps
`"CLOSED"` distinguishable from "FMP sent something we could not parse."**

**Files:**
- Create: `src/FmpDotNet/Models/ExchangeMarketHours.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (one entry)
- Create: `tests/FmpDotNet.Tests/MarketHoursTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/all-exchange-market-hours.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/exchange-market-hours.NASDAQ.json`

**Interfaces:**
- Consumes: nothing new.
- Produces: `Models.ExchangeMarketHours` with bound properties `Exchange`, `Name`, `OpeningHourText`,
  `ClosingHourText`, `OpeningAdditionalText`, `ClosingAdditionalText`, `Timezone`, `IsMarketOpen`; computed
  `OpeningHour`, `ClosingHour`, `OpeningAdditional`, `ClosingAdditional` (all `OffsetTime?`) and
  `IsClosedToday` (`bool`); and the context entry `FmpJsonContext.Default.ListExchangeMarketHours`. Task 7
  calls all of it, and uses the **one** list entry for **both** its list method and its single-row method.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/all-exchange-market-hours.head.json` — five rows drawn from the 81-row
capture, one for each thing this record has to survive. Assembled from one response; the ordering is not the
wire's.

```json
[
  { "exchange": "ASX", "name": "Australian Securities Exchange", "openingHour": "10:00 AM +10:00",
    "closingHour": "04:00 PM +10:00", "timezone": "Australia/Sydney", "isMarketOpen": false },
  { "exchange": "JPX", "name": "Tokyo Stock Exchange", "openingHour": "09:00 AM +09:00",
    "closingHour": "11:30 AM +09:00", "openingAdditional": "12:30 PM +09:00",
    "closingAdditional": "03:30 PM +09:00", "timezone": "Asia/Tokyo", "isMarketOpen": false },
  { "exchange": "EGX", "name": "Egyptian Exchange", "openingHour": "10:00 AM +03:00",
    "closingHour": "02:15 PM +03:00", "timezone": "Africa/Cairo", "isMarketOpen": false },
  { "exchange": "NASDAQ", "name": "NASDAQ", "openingHour": "CLOSED", "closingHour": "CLOSED",
    "timezone": "America/New_York", "isMarketOpen": false },
  { "exchange": "KLS", "name": "Malaysian Stock Exchange", "openingHour": "CLOSED",
    "closingHour": "CLOSED", "timezone": "Asia/Kuala_Lumpur", "isMarketOpen": false }
]
```

Row by row: **ASX** is the ordinary six-key row; **JPX** is one of the seven lunch-break exchanges and the
only shape carrying eight keys; **EGX** is a Gulf market, showing hours on a Sunday because its Sunday is a
trading day; **NASDAQ** is the `"CLOSED"` sentinel on a local weekend; **KLS** is the sentinel on a local
*weekday* — Monday 2026-08-31, Malaysian National Day, corroborated by `holidays-by-exchange` naming that date
with `isClosed: true`.

`tests/FmpDotNet.Tests/Fixtures/exchange-market-hours.NASDAQ.json` — the verbatim single-exchange response,
which is what `GetExchangeAsync` takes the first row of:

```json
[
  { "exchange": "NASDAQ", "name": "NASDAQ", "openingHour": "CLOSED", "closingHour": "CLOSED",
    "timezone": "America/New_York", "isMarketOpen": false }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/MarketHoursTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three Market Hours paths, checked against captures taken live 2026-08-30.</summary>
public class MarketHoursTests
{
    [Fact]
    public void An_ordinary_exchange_row_binds_its_six_keys_and_parses_both_hours()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(5, rows.Count);
        var asx = rows[0];

        Assert.Equal("ASX", asx.Exchange);
        Assert.Equal("Australian Securities Exchange", asx.Name);
        Assert.Equal("Australia/Sydney", asx.Timezone);
        Assert.False(asx.IsMarketOpen);
        Assert.Equal("10:00 AM +10:00", asx.OpeningHourText);
        Assert.Equal(new OffsetTime(new LocalTime(10, 0), Offset.FromHours(10)), asx.OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(16, 0), Offset.FromHours(10)), asx.ClosingHour);
        Assert.False(asx.IsClosedToday);

        // The afternoon pair is ABSENT on this row, and on 74 of the 81 measured. That is normal, not
        // missing data — see the lunch-break test below.
        Assert.Equal(
            ["ClosingAdditionalText", "OpeningAdditionalText"], Binding.Unbound(asx));
        Assert.Null(asx.OpeningAdditional);
        Assert.Null(asx.ClosingAdditional);
    }

    [Fact]
    public void A_closed_exchange_parses_no_hours_and_says_why()
    {
        // "CLOSED" fills 124 of 176 hour slots measured 2026-08-30. Without IsClosedToday a caller sees a
        // null OffsetTime and cannot tell "the exchange is shut today" from "FMP sent something this SDK
        // could not parse" — two states that call for completely different responses.
        //
        // This test fails if IsClosedToday is dropped, and it fails if the raw text stops being bound: both
        // are the shortcut an implementer takes when a converter looks like the obvious answer.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var nasdaq = rows[3];

        Assert.True(nasdaq.IsClosedToday);
        Assert.Null(nasdaq.OpeningHour);
        Assert.Null(nasdaq.ClosingHour);
        Assert.Equal("CLOSED", nasdaq.OpeningHourText);   // the wire is preserved exactly
        Assert.Equal("CLOSED", nasdaq.ClosingHourText);

        // And an unparseable value that is NOT the sentinel reads as null hours WITHOUT claiming a closure.
        var garbled = JsonSerializer.Deserialize(
            """[{"openingHour":"half past nine"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)![0];

        Assert.Null(garbled.OpeningHour);
        Assert.False(garbled.IsClosedToday);
    }

    [Fact]
    public void The_lunch_break_exchanges_keep_their_afternoon_session()
    {
        // The keys were present on 7 of 81 rows measured 2026-08-30 and absent from 74. All seven break for
        // lunch: SET, JKT, JPX, SHH, SHZ, SES and HOSE. A record built from the response's FIRST row — ASX,
        // six keys — reports Tokyo closing at 11:30 AM and loses the larger half of its trading day.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var jpx = rows[1];

        Assert.Equal(new OffsetTime(new LocalTime(9, 0), Offset.FromHours(9)), jpx.OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(11, 30), Offset.FromHours(9)), jpx.ClosingHour);
        Assert.Equal(new OffsetTime(new LocalTime(12, 30), Offset.FromHours(9)), jpx.OpeningAdditional);
        Assert.Equal(new OffsetTime(new LocalTime(15, 30), Offset.FromHours(9)), jpx.ClosingAdditional);
        Assert.Empty(Binding.Unbound(jpx));
        Assert.False(jpx.IsClosedToday);
    }

    [Fact]
    public void A_negative_offset_hour_parses()
    {
        // Every offset in the 2026-08-30 capture set was POSITIVE, +03:00 to +12:00, because the captures
        // were taken on a Sunday when only Asia-Pacific and Gulf exchanges were trading — every American
        // exchange read "CLOSED". The negative form is therefore covered by this test rather than by a
        // capture, and the test is the only thing standing between this SDK and an offset-blind pattern.
        var rows = JsonSerializer.Deserialize(
            """[{"openingHour":"09:30 AM -05:00","closingHour":"04:00 PM -04:00"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(new OffsetTime(new LocalTime(9, 30), Offset.FromHours(-5)), rows[0].OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(16, 0), Offset.FromHours(-4)), rows[0].ClosingHour);
    }

    [Fact]
    public void Noon_and_midnight_land_on_the_right_hour()
    {
        // The classic 12-hour-clock defect: "12:00 PM" is noon and "12:00 AM" is midnight, and a pattern
        // that gets either backwards is wrong by twelve hours with nothing to reveal it. SES (Singapore)
        // closes its morning session at 12:00 PM +08:00 on the live wire, measured 2026-08-30.
        var rows = JsonSerializer.Deserialize(
            """[{"openingHour":"12:00 PM +08:00","closingHour":"12:00 AM +00:00"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(new OffsetTime(new LocalTime(12, 0), Offset.FromHours(8)), rows[0].OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(0, 0), Offset.FromHours(0)), rows[0].ClosingHour);
    }

    [Fact]
    public void IsClosedToday_and_IsMarketOpen_are_different_questions()
    {
        // IsClosedToday is about the exchange's own LOCAL CALENDAR DAY; IsMarketOpen is about this instant.
        // EGX shows hours on the Sunday the captures were taken — its Sunday is a trading day — and still
        // reports isMarketOpen false, because the capture landed outside its session. A caller who reads
        // IsClosedToday as "the market is not open right now" is wrong on exactly this row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var egx = rows[2];

        Assert.False(egx.IsClosedToday);
        Assert.False(egx.IsMarketOpen);
        Assert.Equal(new OffsetTime(new LocalTime(14, 15), Offset.FromHours(3)), egx.ClosingHour);
    }

    [Fact]
    public void The_single_exchange_response_is_the_same_row_as_the_list_carries()
    {
        // Not a restatement of the fixture — the reason ONE record serves TWO paths. For each of seven
        // exchanges cross-checked 2026-08-30, the single-exchange row compared equal key for key and value
        // for value to that exchange's row inside all-exchange-market-hours. If that ever stops being true,
        // this test is where it surfaces.
        var single = JsonSerializer.Deserialize(
            Binding.Fixture("exchange-market-hours.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var fromList = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!
            .Single(r => r.Exchange == "NASDAQ");

        Assert.Single(single);
        Assert.Equal(fromList, single[0]);          // record equality: every bound property, all eight
    }

    [Fact]
    public void The_timezone_is_left_as_a_string_for_the_caller_to_resolve()
    {
        // All 81 values resolved as IANA zone identifiers (52 distinct) with no abbreviation and no fixed
        // offset among them, so the caller can hand this straight to DateTimeZoneProviders.Tzdb. The record
        // does not do it for them: which tzdb version to trust is an application decision, and resolving it
        // here would bake this SDK's NodaTime version into the answer.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(
            ["Australia/Sydney", "Asia/Tokyo", "Africa/Cairo", "America/New_York", "Asia/Kuala_Lumpur"],
            rows.Select(r => r.Timezone).ToArray());
        Assert.All(rows, r => Assert.NotNull(DateTimeZoneProviders.Tzdb.GetZoneOrNull(r.Timezone!)));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~MarketHoursTests"
```

Expected: compile failure — `ListExchangeMarketHours` is not a member of `FmpJsonContext.Default`.

- [ ] **Step 4: Write `ExchangeMarketHours`**

Create `src/FmpDotNet/Models/ExchangeMarketHours.cs`. **`<c>MarketHoursEndpoints</c>` is a deferred cref**;
Task 7 promotes it.

**`[JsonIgnore]` on the five computed members is load-bearing.** Without it the source generator emits
metadata for `OffsetTime`, which has no converter registered anywhere in this SDK. Do not remove it.

```csharp
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace FmpDotNet.Models;

/// <summary>One exchange's trading hours, from <c>stable/all-exchange-market-hours</c> and
/// <c>stable/exchange-market-hours</c>.
///
/// <para><b>One record for both paths, because the wire sends one row.</b> For each of seven exchanges
/// cross-checked 2026-08-30, the single row from <c>exchange-market-hours?exchange=X</c> compared <b>equal,
/// key for key and value for value</b>, to that exchange's row inside the 81-row
/// <c>all-exchange-market-hours</c> response.</para>
///
/// <para><b>The hours arrive as text and are parsed here rather than by a converter, and that is a decision
/// with a reason.</b> A <c>JsonConverter&lt;OffsetTime?&gt;</c> sees one field and can set one property, so
/// nothing could populate <see cref="IsClosedToday"/> — and two properties cannot share one
/// <see cref="JsonPropertyNameAttribute"/>. Binding the text and computing the time is the only shape that
/// gives a caller a real time type <b>and</b> keeps the <c>"CLOSED"</c> sentinel distinguishable from "FMP
/// sent something this SDK could not parse". It also preserves the wire exactly, which is the house
/// rule.</para>
///
/// <para><b>Nothing on this record says whether you can trade right now.</b> <see cref="IsClosedToday"/> is
/// about the exchange's own local calendar day and <see cref="IsMarketOpen"/> is about the instant of the
/// call. They answer different questions and both are surfaced.</para></summary>
public sealed record ExchangeMarketHours
{
    /// <summary>The pattern every hour string on this record is read with.
    ///
    /// <para><c>o&lt;m&gt;</c> and not <c>o&lt;G&gt;</c>: verified against NodaTime 3.2.2 on 2026-08-30, this
    /// pattern formats back <b>byte-identically</b> to what FMP sent — <c>+09:00</c> — while <c>o&lt;G&gt;</c>
    /// emits <c>+09</c> for a whole-hour offset and <c>Z</c> for zero.</para></summary>
    private static readonly OffsetTimePattern HourPattern =
        OffsetTimePattern.CreateWithInvariantCulture("hh:mm tt o<m>");

    /// <summary>FMP's exchange code — <c>"NASDAQ"</c>, <c>"JPX"</c>, <c>"KLS"</c>. 81 distinct values measured
    /// 2026-08-30, of which the 63 that <see cref="FmpDotNet.Endpoints.DirectoryEndpoints.GetExchangesAsync"/>
    /// returns are a subset. The code is case-insensitive on the wire.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The exchange's full name — <c>"Tokyo Stock Exchange"</c>. Populated on all 81 rows measured
    /// 2026-08-30. <b>Not</b> accepted as the <c>exchange</c> argument: measured the same day,
    /// <c>exchange=NASDAQ%20Global%20Market</c> is an HTTP 400.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The opening bell exactly as FMP sent it — <c>"09:00 AM +09:00"</c>, or the literal
    /// <c>"CLOSED"</c>.
    ///
    /// <para>Bound as text so that <see cref="IsClosedToday"/> can exist; the parsed value is
    /// <see cref="OpeningHour"/>. Measured 2026-08-30, <c>"CLOSED"</c> filled <b>124 of 176</b> hour slots
    /// across the 81 rows.</para></summary>
    [JsonPropertyName("openingHour")] public string? OpeningHourText { get; init; }

    /// <summary>The closing bell exactly as FMP sent it, or <c>"CLOSED"</c>. See
    /// <see cref="OpeningHourText"/>.</summary>
    [JsonPropertyName("closingHour")] public string? ClosingHourText { get; init; }

    /// <summary>The <b>afternoon</b> session's opening, exactly as FMP sent it — present on only seven
    /// exchanges.
    ///
    /// <para><b>Absent from 74 of 81 rows measured 2026-08-30, and that is normal rather than missing
    /// data.</b> The seven that carry it all break for lunch: SET (Bangkok), JKT (Jakarta), JPX (Tokyo), SHH
    /// (Shanghai), SHZ (Shenzhen), SES (Singapore) and HOSE (Ho Chi Minh). A record built from the response's
    /// first row — ASX, six keys — reports Tokyo closing at 11:30 AM and loses the larger half of its trading
    /// day.</para></summary>
    [JsonPropertyName("openingAdditional")] public string? OpeningAdditionalText { get; init; }

    /// <summary>The afternoon session's close, exactly as FMP sent it. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonPropertyName("closingAdditional")] public string? ClosingAdditionalText { get; init; }

    /// <summary>The exchange's IANA time zone identifier — <c>"Asia/Tokyo"</c>, <c>"America/New_York"</c>.
    ///
    /// <para>All 81 values measured 2026-08-30 resolved as IANA identifiers (52 distinct), with no
    /// abbreviation and no fixed offset among them, so this can be handed straight to
    /// <c>DateTimeZoneProviders.Tzdb</c>. The SDK does not resolve it: which tzdb version to trust is an
    /// application decision, and resolving it here would bake this SDK's NodaTime version into the
    /// answer.</para></summary>
    [JsonPropertyName("timezone")] public string? Timezone { get; init; }

    /// <summary>Whether the exchange was trading at the instant of the call.
    ///
    /// <para><b>Measured <see langword="false"/> on all 81 rows, on every capture, and the <see langword="true"/>
    /// case is unmeasured.</b> Every capture behind this record was taken on Sunday 2026-08-30. What is
    /// measured is the field's <i>type</i> — a JSON boolean on all 81 rows — and nothing else. This
    /// documentation deliberately describes no behaviour nobody observed.</para>
    ///
    /// <para>Not the same question as <see cref="IsClosedToday"/>, which is about the exchange's local
    /// calendar day rather than this instant.</para></summary>
    [JsonPropertyName("isMarketOpen")] public bool? IsMarketOpen { get; init; }

    /// <summary>The opening bell as a time with its UTC offset, or <see langword="null"/> when the wire sent
    /// <c>"CLOSED"</c> or anything else unparseable. Read <see cref="IsClosedToday"/> to tell those two
    /// apart.</summary>
    [JsonIgnore] public OffsetTime? OpeningHour => ParseHour(OpeningHourText);

    /// <summary>The closing bell as a time with its UTC offset, or <see langword="null"/>. See
    /// <see cref="OpeningHour"/>.</summary>
    [JsonIgnore] public OffsetTime? ClosingHour => ParseHour(ClosingHourText);

    /// <summary>The afternoon session's opening as a time with its UTC offset, or <see langword="null"/> on
    /// the 74 of 81 exchanges that do not break for lunch. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonIgnore] public OffsetTime? OpeningAdditional => ParseHour(OpeningAdditionalText);

    /// <summary>The afternoon session's close as a time with its UTC offset, or <see langword="null"/>. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonIgnore] public OffsetTime? ClosingAdditional => ParseHour(ClosingAdditionalText);

    /// <summary>The exchange is not trading on its own local date — the wire sent the literal
    /// <c>"CLOSED"</c> rather than a time.
    ///
    /// <para><b>This is about the exchange's local calendar day, not about this instant.</b> Established
    /// rather than assumed: resolving each row's <see cref="Timezone"/> against the capture's HTTP
    /// <c>Date</c> header on 2026-08-30, 61 of the 62 closures were local <b>weekends</b>, and the four
    /// exchanges showing hours on a local weekend were exactly the Gulf markets EGX, DOH, KUW and SAU, whose
    /// Sunday is a trading day. The single local-weekday closure — KLS on its Monday 2026-08-31 — is
    /// corroborated by <c>holidays-by-exchange</c> naming that date <c>"National Day"</c> with
    /// <c>isClosed: true</c>. Zero unexplained exceptions across all 81 rows.</para>
    ///
    /// <para>A caller must not read this as "the market is not open right now" — that is
    /// <see cref="IsMarketOpen"/>.</para></summary>
    [JsonIgnore] public bool IsClosedToday => OpeningHourText is "CLOSED";

    private static OffsetTime? ParseHour(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var parsed = HourPattern.Parse(text);
        return parsed.Success ? parsed.Value : null;
    }
}
```

**If Task 1 closed either measurement gap**, replace the corresponding paragraph before committing:

- Gap 1 closed — swap the `IsMarketOpen` paragraph beginning "**Measured `false` on all 81 rows**" for:
  *"Measured on `<DATE>`, `<N>` of `<ROWS>` exchanges reported <see langword="true"/> — `<EXCHANGES>` — against
  all 81 rows <see langword="false"/> on the Sunday captures of 2026-08-30. Both states are observed."*
- Gap 2 closed — append to `OpeningHour`'s summary: *"Negative offsets are measured as well as tested: `<LIST>`
  appeared on `<DATE>`."* and drop "rather than by a capture" from the negative-offset test's comment.

If Task 1 recorded the gaps as still open, change nothing — the text above is already the honest statement.

- [ ] **Step 5: Register the record with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`, beside the two entries from Tasks 2 and 3:

```csharp
// ONE entry serves both market-hours methods: GetExchangeAsync deserialises the same list type and takes
// its first row, following CompanyEndpoints.GetProfileAsync.
[JsonSerializable(typeof(List<ExchangeMarketHours>))]
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~MarketHoursTests"
```

Expected: PASS, 8 tests.

- [ ] **Step 7: Build with no warnings, then run the whole suite**

```bash
dotnet build && dotnet test tests/FmpDotNet.Tests
```

Expected: build clean and suite green. **The build is the step that matters here**: if `[JsonIgnore]` is
missing from any computed member, the source generator tries to emit metadata for `OffsetTime` and the build
tells you so. A green test run with a warning-laden build is a failure of this step.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/ExchangeMarketHours.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/MarketHoursTests.cs \
        tests/FmpDotNet.Tests/Fixtures/all-exchange-market-hours.head.json \
        tests/FmpDotNet.Tests/Fixtures/exchange-market-hours.NASDAQ.json
git commit -m "feat: bind exchange market hours, parsing the bells and naming the CLOSED sentinel (#38)"
```

---

### Task 5: `LocalTimeJsonConverter`, and `ExchangeHoliday`

446 rows measured 2026-08-30 across five exchanges. The trap here is a **boolean that is never `false`**: the
field that would answer "is the exchange closed that day?" has exactly two states, `true` and `null`, and
`null` means *an early close* rather than *unknown*.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append one converter)
- Create: `src/FmpDotNet/Models/ExchangeHoliday.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (one entry)
- Modify: `tests/FmpDotNet.Tests/MarketHoursTests.cs` (add tests)
- Create: `tests/FmpDotNet.Tests/Fixtures/holidays-by-exchange.NASDAQ.json`

**Interfaces:**
- Consumes: `NullableLocalDateJsonConverter` (existing), `Models.ExchangeMarketHours` from Task 4 (referenced
  by a `<see cref>` in the doc for `AdjustedCloseTime`).
- Produces: `LocalTimeJsonConverter : JsonConverter<LocalTime?>`, `Models.ExchangeHoliday` with properties
  `Exchange`, `Date`, `Name`, `IsClosed`, `AdjustedOpenTime`, `AdjustedCloseTime`, `IsFullyClosed` and the
  computed `ClosesEarly`, and the context entry `FmpJsonContext.Default.ListExchangeHoliday`. Task 7 calls
  all three.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/holidays-by-exchange.NASDAQ.json` — four verbatim rows from the 446-row
capture, one per state. Assembled from one response; the ordering is not the wire's.

```json
[
  { "exchange": "NASDAQ", "date": "2026-07-03", "name": "Independence Day", "isClosed": true,
    "adjOpenTime": null, "adjCloseTime": null },
  { "exchange": "NASDAQ", "date": "2026-12-24", "name": "Christmas", "isClosed": null,
    "adjOpenTime": null, "adjCloseTime": "13:00", "isFullyClosed": false },
  { "exchange": "NASDAQ", "date": "2015-11-27", "name": "Thanksgiving", "isClosed": null,
    "adjOpenTime": null, "adjCloseTime": "13:30", "isFullyClosed": false },
  { "exchange": "NASDAQ", "date": "2032-12-31", "name": "Christmas", "isClosed": true,
    "adjOpenTime": null, "adjCloseTime": null }
]
```

Row by row: **2026-07-03** is a full closure and the six-key shape; **2026-12-24** is an early close and the
seven-key shape; **2015-11-27** is the single `"13:30"` in the whole corpus, the only value that is not
`"13:00"`; **2032-12-31** is the far end of the range, which is what makes the point that the default window
hides the future.

- [ ] **Step 2: Write the failing tests**

Add to `tests/FmpDotNet.Tests/MarketHoursTests.cs`:

```csharp
    [Fact]
    public void A_holiday_row_binds_its_six_ordinary_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.Equal(4, rows.Count);
        Assert.Equal("NASDAQ", rows[0].Exchange);
        Assert.Equal(new LocalDate(2026, 7, 3), rows[0].Date);
        Assert.Equal("Independence Day", rows[0].Name);
        Assert.True(rows[0].IsClosed);

        // Three properties come back empty on a full-closure row and every one of them is CORRECT.
        // adjOpenTime was null on all 446 rows measured 2026-08-30 — never once populated — adjCloseTime
        // is null wherever the exchange shut completely, and isFullyClosed is absent from that shape.
        Assert.Equal(
            ["AdjustedCloseTime", "AdjustedOpenTime", "IsFullyClosed"], Binding.Unbound(rows[0]));
    }

    [Fact]
    public void An_early_close_is_not_a_closure()
    {
        // THE trap on this path. Measured across 446 rows the two states are exact complements:
        //   isClosed true,  isFullyClosed absent, no adjusted time  — 396 rows
        //   isClosed null,  isFullyClosed false,  adjCloseTime set  —  50 rows
        //   isClosed false                                          —   0 rows
        // So IsClosed alone cannot answer "is the exchange closed that day?": null means an EARLY CLOSE,
        // not "unknown", and a caller who reads it as unknown treats 50 measured rows as unanswerable.
        //
        // This test fails against `bool IsClosed` (which cannot represent the null) and against any model
        // that folds the two wire fields into one enum.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;
        var early = rows[1];

        Assert.Null(early.IsClosed);                      // NOT false — the wire never sends false
        Assert.False(early.IsFullyClosed);
        Assert.Equal(new LocalTime(13, 0), early.AdjustedCloseTime);
        Assert.True(early.ClosesEarly);

        Assert.False(rows[0].ClosesEarly);                // a full closure does not "close early"
        Assert.True(rows[0].IsClosed);
        Assert.Null(rows[0].IsFullyClosed);
    }

    [Fact]
    public void ClosesEarly_is_derived_from_the_time_and_not_from_the_absent_flag()
    {
        // Both candidate signals — AdjustedCloseTime is not null, and IsFullyClosed == false — selected the
        // IDENTICAL 50 rows across all 446 measured 2026-08-30. The time won because it does not depend on
        // a key that is absent from 89% of rows: a future response that stops sending isFullyClosed would
        // silently turn every early close into a non-event under the other reading.
        var row = JsonSerializer.Deserialize(
            """[{"adjCloseTime":"13:00"}]""", FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.True(row.ClosesEarly);
        Assert.Null(row.IsFullyClosed);                   // and it still says so with the flag missing
    }

    [Fact]
    public void The_adjusted_times_parse_as_a_wall_clock_and_round_trip()
    {
        // All 50 non-null values matched HH:mm — 49 of them "13:00" and one "13:30" on 2015-11-27. Unlike
        // the long-form date converter, this pattern round-trips exactly, so this test may assert the
        // serialised form and does.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.Equal(new LocalTime(13, 0), rows[1].AdjustedCloseTime);
        Assert.Equal(new LocalTime(13, 30), rows[2].AdjustedCloseTime);

        Assert.Contains(
            "\"adjCloseTime\":\"13:30\"",
            JsonSerializer.Serialize(new List<Models.ExchangeHoliday> { rows[2] },
                FmpJsonContext.Default.ListExchangeHoliday),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_adjusted_times_carry_no_offset_which_is_the_other_half_of_the_two_time_spellings()
    {
        // "13:00" here against "09:30 AM +09:00" on ExchangeMarketHours — two spellings of a time in one
        // slice. This one is the sharper case: holidays-by-exchange has NO timezone key at all, verified
        // absent on all 446 rows, so the zone must come from the matching ExchangeMarketHours row. A
        // converter that guessed a zone here would be fabricating one.
        using var wire = JsonDocument.Parse(Binding.Fixture("holidays-by-exchange.NASDAQ.json"));

        Assert.All(
            wire.RootElement.EnumerateArray(),
            row => Assert.False(row.TryGetProperty("timezone", out _)));

        var parsed = JsonSerializer.Deserialize(
            """[{"adjCloseTime":"13:00 PM +09:00"}]""",
            FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.Null(parsed.AdjustedCloseTime);            // an offset-bearing value is not this shape
    }

    [Fact]
    public void AdjustedOpenTime_is_modelled_but_was_never_observed_carrying_a_value()
    {
        // null on ALL 446 rows measured 2026-08-30. It is modelled because the KEY is always present on the
        // six-key shape, and documented as never observed populated — which is a different statement from
        // "it is always null", and the doc must not make the stronger one.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.All(rows, r => Assert.Null(r.AdjustedOpenTime));

        // And it binds when a value does arrive, so the modelling is not decorative.
        var populated = JsonSerializer.Deserialize(
            """[{"adjOpenTime":"10:30"}]""", FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.Equal(new LocalTime(10, 30), populated.AdjustedOpenTime);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~MarketHoursTests"
```

Expected: compile failure — `ListExchangeHoliday` is not a member of `FmpJsonContext.Default`.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `LongFormLocalDateJsonConverter`:

```csharp
/// <summary>Reads a bare wall-clock time — <c>"13:00"</c> — as a <see cref="LocalTime"/>.
///
/// <para><b>Written for <c>stable/holidays-by-exchange</c>'s <c>adjOpenTime</c> and <c>adjCloseTime</c></b>,
/// which are the only <see cref="LocalTime"/> fields in this SDK. All 50 non-null values measured 2026-08-30
/// matched <c>HH:mm</c> — 49 of them <c>"13:00"</c> and one <c>"13:30"</c> on 2015-11-27.</para>
///
/// <para><b>The value carries no offset and the response carries no zone.</b> <c>holidays-by-exchange</c> has
/// no <c>timezone</c> key at all — verified absent on all 446 rows — so a caller who needs an instant must
/// take the zone from the matching <c>ExchangeMarketHours.Timezone</c>, fetched from
/// <c>stable/exchange-market-hours</c>. This converter does not guess one, and could not: the same wire
/// format on <c>all-exchange-market-hours</c> is spelled <c>"09:30 AM +09:00"</c> instead, which is the
/// sharper half of this group's two-spellings-of-a-time problem.</para>
///
/// <para>This pattern round-trips exactly, unlike <see cref="LongFormLocalDateJsonConverter"/>, so a guard
/// test for this converter may assert the serialised form. Null on an unparseable value, following the rest
/// of this file.</para></summary>
public sealed class LocalTimeJsonConverter : JsonConverter<LocalTime?>
{
    private static readonly LocalTimePattern Pattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    /// <inheritdoc/>
    public override LocalTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}
```

- [ ] **Step 5: Write `ExchangeHoliday`**

Create `src/FmpDotNet/Models/ExchangeHoliday.cs`. **`<c>MarketHoursEndpoints</c>` and
`<c>GetHolidaysAsync</c>` are deferred crefs**; Task 7 promotes them.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One day an exchange is closed, or closes early, from <c>stable/holidays-by-exchange</c>.
///
/// <para><b>Two shapes, and the difference is the whole point of this record.</b> Measured across 446 rows on
/// 2026-08-30, every row is one of exactly two states and they are exact complements:</para>
///
/// <list type="bullet">
///   <item><description><b>396 rows</b> — <c>isClosed: true</c>, <c>isFullyClosed</c> <b>absent</b>, no
///     adjusted time. The exchange did not trade.</description></item>
///   <item><description><b>50 rows</b> — <c>isClosed: null</c>, <c>isFullyClosed: false</c>,
///     <c>adjCloseTime</c> set. The exchange traded a shortened session.</description></item>
///   <item><description><b>0 rows</b> — <c>isClosed: false</c>. The wire has never been observed sending
///     it.</description></item>
/// </list>
///
/// <para><b>So <see cref="IsClosed"/> alone cannot answer "is the exchange closed that day?"</b> Its
/// <see langword="null"/> means <i>an early close</i>, not <i>unknown</i>, and a caller who reads it as
/// unknown treats 50 measured rows as unanswerable. <see cref="ClosesEarly"/> is the derived predicate that
/// says which state a row is in; the wire pair is kept verbatim beside it, because
/// <c>isClosed: false</c> has never been observed and an enum collapsing the two states would have nowhere
/// to put it if it appeared.</para>
///
/// <para><b>The response carries no time zone.</b> Verified absent on all 446 rows. A caller who needs an
/// instant from <see cref="AdjustedCloseTime"/> must take the zone from
/// <see cref="ExchangeMarketHours.Timezone"/> on the matching exchange.</para></summary>
public sealed record ExchangeHoliday
{
    /// <summary>The exchange code the row belongs to, echoed from the request.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The date of the holiday, ISO on the wire. Populated on all 446 rows measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The holiday's name — <c>"Independence Day"</c>, <c>"Christmas"</c>. Not unique within an
    /// exchange: the name repeats once a year, and NASDAQ's 446 rows reach 2032-12-31.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Whether the exchange was fully shut — <b><see langword="true"/> or
    /// <see langword="null"/>, never <see langword="false"/></b>.
    ///
    /// <para><b>Read the record's summary before using this field.</b> Measured across 446 rows on
    /// 2026-08-30 it was <see langword="true"/> on 396 and <see langword="null"/> on 50, and
    /// <see langword="false"/> on none. The <see langword="null"/> rows are early closes, not unknowns —
    /// <see cref="ClosesEarly"/> is the predicate that says so.</para></summary>
    [JsonPropertyName("isClosed")] public bool? IsClosed { get; init; }

    /// <summary>The shortened session's opening time, with no zone attached.
    ///
    /// <para><b>Never observed carrying a value.</b> It was <see langword="null"/> on all 446 rows measured
    /// 2026-08-30. It is modelled because the key is present on every row, and this doc records the absence
    /// rather than claiming the field is always null — those are different statements and only the first is
    /// measured.</para></summary>
    [JsonPropertyName("adjOpenTime")]
    [JsonConverter(typeof(LocalTimeJsonConverter))]
    public LocalTime? AdjustedOpenTime { get; init; }

    /// <summary>The shortened session's closing time, with no zone attached — <c>13:00</c> on 49 of the 50
    /// early closes measured 2026-08-30, and <c>13:30</c> on the fiftieth (2015-11-27).
    ///
    /// <para><b>No offset and no zone.</b> The response has no <c>timezone</c> key, verified absent on all
    /// 446 rows, so an instant needs <see cref="ExchangeMarketHours.Timezone"/> from
    /// <c>stable/exchange-market-hours</c> for the same exchange.</para></summary>
    [JsonPropertyName("adjCloseTime")]
    [JsonConverter(typeof(LocalTimeJsonConverter))]
    public LocalTime? AdjustedCloseTime { get; init; }

    /// <summary>FMP's own flag for the early-close shape — <see langword="false"/> on the 50 early closes
    /// and <b>absent</b> on the other 396, measured 2026-08-30.
    ///
    /// <para>Kept verbatim rather than folded into <see cref="ClosesEarly"/>, because it is what the wire
    /// sent and because a future <see langword="true"/> would carry information this SDK has no measurement
    /// for.</para></summary>
    [JsonPropertyName("isFullyClosed")] public bool? IsFullyClosed { get; init; }

    /// <summary>The exchange traded a shortened session that day rather than closing.
    ///
    /// <para><b>Derived from <see cref="AdjustedCloseTime"/> and not from <see cref="IsFullyClosed"/>,
    /// deliberately.</b> Both candidate signals selected the <b>identical</b> 50 rows across all 446 measured
    /// 2026-08-30, so the choice is not about accuracy. <see cref="AdjustedCloseTime"/> wins because it does
    /// not depend on a key that is absent from 89% of rows: a response that stopped sending
    /// <c>isFullyClosed</c> would silently turn every early close into a non-event under the other
    /// reading.</para></summary>
    [JsonIgnore] public bool ClosesEarly => AdjustedCloseTime is not null;
}
```

- [ ] **Step 6: Register the record with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`, beside the three entries from Tasks 2, 3 and 4:

```csharp
[JsonSerializable(typeof(List<ExchangeHoliday>))]
```

That is the fourth and last entry this slice adds. **Four entries for nine paths** — the consolidation paying
off a second time.

- [ ] **Step 7: Run the tests to verify they pass**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~MarketHoursTests"
```

Expected: PASS, 14 tests.

- [ ] **Step 8: Build with no warnings, then run the whole suite**

```bash
dotnet build && dotnet test tests/FmpDotNet.Tests
```

Expected: build clean and suite green. This is the **last task before the facades land**, so it is the last
point at which the whole unit suite passes until Task 9 regenerates the README.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/ExchangeHoliday.cs tests/FmpDotNet.Tests/MarketHoursTests.cs \
        tests/FmpDotNet.Tests/Fixtures/holidays-by-exchange.NASDAQ.json
git commit -m "feat: bind exchange holidays, distinguishing an early close from a closure (#38)"
```

---

### Task 6: `IndexesEndpoints` — six methods that take nothing but a token

**Every one of the six takes only a `CancellationToken`, and that is a measurement rather than an oversight.**
On all six paths, `limit`, `page`, `symbol` and an unknown `wibble=42` each returned a response
**byte-identical** to the bare request; on the three historical paths so did `from=2020-01-01&to=2026-12-31`.
There is no parameter to offer that FMP would honour, and offering one would be a signature that lies.

**Files:**
- Create: `src/FmpDotNet/Endpoints/IndexesEndpoints.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `src/FmpDotNet/Models/IndexConstituentChange.cs` (promote one deferred cref)
- Check: `src/FmpDotNet/Models/IndexConstituent.cs` — as written in Task 3 it carries no deferred cref, so the
  expected result of Step 4's grep is no match on this file. Verify rather than assume.
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`
- Modify: `tests/FmpDotNet.Tests/IndexesTests.cs` (add request-shape tests)

**Interfaces:**
- Consumes: `Models.IndexConstituent` and `FmpJsonContext.Default.ListIndexConstituent` (Task 3);
  `Models.IndexConstituentChange` and `FmpJsonContext.Default.ListIndexConstituentChange` (Task 2);
  `FmpTransport.GetListAsync`, `FmpRequest` (existing).
- Produces: `Endpoints.IndexesEndpoints` with `GetDowJonesConstituentsAsync`, `GetSp500ConstituentsAsync`,
  `GetNasdaqConstituentsAsync`, `GetDowJonesConstituentChangesAsync`, `GetSp500ConstituentChangesAsync`,
  `GetNasdaqConstituentChangesAsync`, each `(CancellationToken ct = default)`; and `FmpClient.Indexes`.
  Task 9's README regeneration counts them.

- [ ] **Step 1: Write the failing tests**

Add to `tests/FmpDotNet.Tests/IndexesTests.cs`. Note the extra `using` lines the file needs — add
`using System.Web;`, `using FmpDotNet.Endpoints;` and `using Microsoft.Extensions.Options;` at the top:

```csharp
    private static (IndexesEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new IndexesEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Each_of_the_six_paths_is_asked_exactly_once_and_none_twice()
    {
        // Six methods, six distinct paths, no shared prefix. The failure this catches is a copy-paste
        // between the three sibling methods, which returns plausible data from the wrong index and would
        // read as correct in every other test in this file.
        var (endpoints, handler) = Build();

        await endpoints.GetDowJonesConstituentsAsync();
        await endpoints.GetSp500ConstituentsAsync();
        await endpoints.GetNasdaqConstituentsAsync();
        await endpoints.GetDowJonesConstituentChangesAsync();
        await endpoints.GetSp500ConstituentChangesAsync();
        await endpoints.GetNasdaqConstituentChangesAsync();

        Assert.Equal(
            [
                "/stable/dowjones-constituent",
                "/stable/sp500-constituent",
                "/stable/nasdaq-constituent",
                "/stable/historical-dowjones-constituent",
                "/stable/historical-sp500-constituent",
                "/stable/historical-nasdaq-constituent",
            ],
            handler.Requests.Select(u => u.AbsolutePath).ToArray());
        Assert.Equal(6, handler.Requests.Select(u => u.AbsolutePath).Distinct().Count());
    }

    [Fact]
    public async Task None_of_the_six_sends_a_query_parameter_except_the_key()
    {
        // Measured 2026-08-30: on all six paths, limit, page, symbol and an unknown wibble=42 each returned
        // a response BYTE-IDENTICAL to the bare request, and on the three historical paths so did
        // from/to. There is no parameter to offer, so the signatures offer none — and this test fails the
        // moment somebody adds one back "for convenience", which would be a signature that lies.
        var (endpoints, handler) = Build();

        await endpoints.GetSp500ConstituentsAsync();
        await endpoints.GetSp500ConstituentChangesAsync();

        Assert.All(handler.Requests, uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal(["apikey"], query.AllKeys.Where(k => k is not null).Select(k => k!).ToArray());
        });
    }

    [Fact]
    public async Task An_empty_response_is_an_empty_list_and_never_null()
    {
        var (endpoints, _) = Build("[]");

        Assert.Empty(await endpoints.GetDowJonesConstituentsAsync());
        Assert.Empty(await endpoints.GetDowJonesConstituentChangesAsync());
    }
```

`HttpUtility` needs `using System.Web;` — the same import `CompanyScreenerTests.cs:44` already uses, so no
package reference is involved.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests"
```

Expected: compile failure — `IndexesEndpoints` does not exist.

- [ ] **Step 3: Write the facade**

Create `src/FmpDotNet/Endpoints/IndexesEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Index membership — who is in the Dow Jones, the S&amp;P 500 and the Nasdaq 100 now, and every
/// change to those lists that FMP records.
///
/// <para><b>Three things hold across all six paths, measured 2026-08-30, and a caller should read them
/// once.</b></para>
///
/// <list type="number">
///   <item><description><b>None of them takes a parameter, and that is measured.</b> <c>limit</c>,
///     <c>page</c>, <c>symbol</c> and an unknown <c>wibble=42</c> each returned a response
///     <b>byte-identical</b> to the bare request on every path; on the three change feeds so did
///     <c>from=2020-01-01&amp;to=2026-12-31</c>. There is nothing to narrow with and no pagination to walk.
///     The largest response is <c>historical-sp500-constituent</c> at 1,525 rows and 365,284
///     bytes.</description></item>
///   <item><description><b>A row count is not a company count.</b> <c>sp500-constituent</c> returned 503
///     rows over 500 distinct CIKs and <c>nasdaq-constituent</c> 102 over 101. See
///     <see cref="IndexConstituent"/>.</description></item>
///   <item><description><b>The change feeds are not membership history.</b> Of the 628 current constituents
///     carrying a <c>dateFirstAdded</c>, 24 have no addition row at all, so replaying the changes does not
///     reconstruct who was in an index on a past date. That is why the three methods are named for
///     <b>changes</b> rather than for the paths they call, and why this SDK offers no as-of-date membership
///     method.</description></item>
/// </list>
///
/// <para>Market hours and exchange holidays are a separate facade — <c>MarketHoursEndpoints</c> — because
/// the two groups share no path prefix, no parameter, no record and no concept.</para></summary>
public sealed class IndexesEndpoints(FmpTransport transport)
{
    /// <summary>The Dow Jones Industrial Average's current members, from
    /// <c>stable/dowjones-constituent</c>.
    ///
    /// <para>30 rows measured 2026-08-30, and <see cref="IndexConstituent.Founded"/> was ISO on
    /// <b>30 of 30</b> — which is exactly the sample that makes that field look like a date. It is not; read
    /// its documentation before binding it yourself.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points
    /// at the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetDowJonesConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/dowjones-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>The S&amp;P 500's current members, from <c>stable/sp500-constituent</c>.
    ///
    /// <para><b>503 rows over 500 distinct CIKs</b>, measured 2026-08-30 — FOX/FOXA, NWS/NWSA and GOOGL/GOOG
    /// are the three dual-class pairs. Counting rows counts share classes.</para>
    ///
    /// <para>This is the path on which <see cref="IndexConstituent.Founded"/> shows what it really is: a bare
    /// year on <b>477 of 503</b> rows.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetSp500ConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/sp500-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>The Nasdaq 100's current members, from <c>stable/nasdaq-constituent</c>.
    ///
    /// <para>102 rows over 101 distinct CIKs, measured 2026-08-30. The only path on which
    /// <see cref="IndexConstituent.DateFirstAdded"/> is ever <see langword="null"/> — 7 rows: ADBE, AMAT,
    /// CSCO, FAST, MSFT, PAYX and QCOM.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every current member, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituent>> GetNasdaqConstituentsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/nasdaq-constituent"), FmpJsonContext.Default.ListIndexConstituent, ct);

    /// <summary>Every recorded change to the Dow Jones Industrial Average's membership, from
    /// <c>stable/historical-dowjones-constituent</c>.
    ///
    /// <para><b>Named for changes, not for the path, because a row is a change and not a
    /// constituent.</b> 86 rows measured 2026-08-30, each one an addition <i>or</i> a removal. See
    /// <see cref="IndexConstituentChange"/> for what that means for <c>symbol</c>, and for why this feed
    /// cannot answer "who was in the index on date X".</para>
    ///
    /// <para><b>This path is where absence is spelled only one way.</b> All 86 rows use <c>""</c> and none
    /// uses JSON <see langword="null"/> — unlike its two siblings. An implementer testing here alone never
    /// meets the second spelling.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetDowJonesConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-dowjones-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);

    /// <summary>Every recorded change to the S&amp;P 500's membership, from
    /// <c>stable/historical-sp500-constituent</c>.
    ///
    /// <para><b>The largest response in this facade</b> — 1,525 rows and 365,284 bytes measured 2026-08-30,
    /// reaching back to a 1957 backfill, and it cannot be narrowed: <c>from</c>/<c>to</c> are accepted and
    /// discarded here, verified byte-identical.</para>
    ///
    /// <para>Named for changes rather than for the path; see
    /// <see cref="GetDowJonesConstituentChangesAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetSp500ConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-sp500-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);

    /// <summary>Every recorded change to the Nasdaq 100's membership, from
    /// <c>stable/historical-nasdaq-constituent</c>. 444 rows measured 2026-08-30.
    ///
    /// <para>Named for changes rather than for the path; see
    /// <see cref="GetDowJonesConstituentChangesAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded change, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndexConstituentChange>> GetNasdaqConstituentChangesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/historical-nasdaq-constituent"),
            FmpJsonContext.Default.ListIndexConstituentChange, ct);
}
```

- [ ] **Step 4: Promote the two deferred crefs in the model files**

In `src/FmpDotNet/Models/IndexConstituentChange.cs`, in the record's summary, change:

```
/// offers no as-of-date membership method — see <c>IndexesEndpoints</c>.</para>
```

to:

```
/// offers no as-of-date membership method — see <see cref="FmpDotNet.Endpoints.IndexesEndpoints"/>.</para>
```

`src/FmpDotNet/Models/IndexConstituent.cs` has no `<c>IndexesEndpoints</c>` to promote as written above —
check it with `grep -n "IndexesEndpoints" src/FmpDotNet/Models/IndexConstituent.cs` and promote any match the
same way. If there is none, that is the expected result; do not invent one.

- [ ] **Step 5: Wire the facade into `FmpClient`**

Add `IndexesEndpoints indexes` to the primary constructor's parameter list in `src/FmpDotNet/FmpClient.cs`,
after `EtfAndFundsEndpoints etfAndFunds`, and add the property after `EtfAndFunds`:

```csharp
    /// <summary>Index membership — the Dow Jones, S&amp;P 500 and Nasdaq 100 member lists, and every change
    /// FMP records to them.
    ///
    /// <para><b>No method here takes a parameter</b>, which is measured rather than incidental, and the
    /// change feeds cannot be replayed into a membership list. See <see cref="IndexesEndpoints"/> before
    /// reaching for either.</para></summary>
    public IndexesEndpoints Indexes { get; } = indexes;
```

- [ ] **Step 6: Register the facade for dependency injection**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, after
`services.TryAddTransient<EtfAndFundsEndpoints>();`:

```csharp
        services.TryAddTransient<IndexesEndpoints>();
```

- [ ] **Step 7: Update `AddFmpTests`**

Two edits in `tests/FmpDotNet.Tests/AddFmpTests.cs`, both inside
`Resolves_the_client_and_every_endpoint_group`. After `Assert.NotNull(client.EtfAndFunds);`:

```csharp
        Assert.NotNull(client.Indexes);
```

and change `Assert.Equal(20, typeof(FmpClient)` to `Assert.Equal(21, typeof(FmpClient)`.

**Task 7 changes it again, to 22.** That is the ruling recorded at the top of this plan: a task that leaves
the suite red is not a task, so the count moves once per facade rather than once per plan.

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IndexesTests|FullyQualifiedName~AddFmpTests"
```

Expected: PASS.

- [ ] **Step 9: Run the whole suite and confirm the ONE expected failure**

```bash
dotnet build && dotnet test tests/FmpDotNet.Tests
```

Expected: build clean, and **exactly one** failing test —
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`,
because the README's generated block does not yet list the six new paths. Task 9 regenerates it.

**Any second failure is a real defect.** In particular, `Every_public_endpoint_method_reaches_the_api` must
stay green: it drives every public method with synthesised arguments and fails if a method requests nothing.
These six take no arguments at all, so it has nothing to synthesise and no reason to fail.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Endpoints/IndexesEndpoints.cs src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        src/FmpDotNet/Models/IndexConstituentChange.cs src/FmpDotNet/Models/IndexConstituent.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Tests/IndexesTests.cs
git commit -m "feat: add the fmp.Indexes facade, six paths with no parameters to offer (#38)"
```

---

### Task 7: `MarketHoursEndpoints`, its two guards, and every remaining deferred cref

Three methods, and the design work is in the signatures rather than the bodies: one required date range that
replaces a default window with no future in it, and one guard that turns a wasted call into an exception.

**Files:**
- Create: `src/FmpDotNet/Endpoints/MarketHoursEndpoints.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `src/FmpDotNet/Models/ExchangeMarketHours.cs` (promote deferred crefs)
- Modify: `src/FmpDotNet/Models/ExchangeHoliday.cs` (promote deferred crefs)
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`
- Modify: `tests/FmpDotNet.Tests/MarketHoursTests.cs` (add request-shape and guard tests)

**Interfaces:**
- Consumes: `Models.ExchangeMarketHours` and `FmpJsonContext.Default.ListExchangeMarketHours` (Task 4);
  `Models.ExchangeHoliday` and `FmpJsonContext.Default.ListExchangeHoliday` (Task 5); `DateRange.ThrowIfBackwards`
  and `FmpRequest.With(string, LocalDate?)` (existing).
- Produces: `Endpoints.MarketHoursEndpoints` with `GetAllExchangesAsync(ct)`,
  `GetExchangeAsync(string exchange, ct)` returning `Task<ExchangeMarketHours?>`, and
  `GetHolidaysAsync(string exchange, LocalDate from, LocalDate to, ct)`; and `FmpClient.MarketHours`.
  **Task 8's `Probe.Argument` arms are narrowed by `typeof(Endpoints.MarketHoursEndpoints)` and by the method
  name `nameof(Endpoints.MarketHoursEndpoints.GetHolidaysAsync)`** — those exact spellings.

- [ ] **Step 1: Write the failing tests**

Add to `tests/FmpDotNet.Tests/MarketHoursTests.cs`, with `using System.Web;`, `using FmpDotNet.Endpoints;` and
`using Microsoft.Extensions.Options;` added at the top:

```csharp
    private static (MarketHoursEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new MarketHoursEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Each_of_the_three_paths_is_asked_exactly_once()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetAllExchangesAsync();
        await endpoints.GetExchangeAsync("NASDAQ");
        await endpoints.GetHolidaysAsync("NASDAQ", new LocalDate(2024, 1, 1), new LocalDate(2026, 12, 31));

        Assert.Equal(
            [
                "/stable/all-exchange-market-hours",
                "/stable/exchange-market-hours",
                "/stable/holidays-by-exchange",
            ],
            handler.Requests.Select(u => u.AbsolutePath).ToArray());
    }

    [Fact]
    public async Task The_whole_market_path_sends_no_exchange_and_the_single_one_does()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetAllExchangesAsync();
        await endpoints.GetExchangeAsync("NASDAQ");

        Assert.Equal(
            ["apikey"],
            HttpUtility.ParseQueryString(handler.Requests[0].Query)
                .AllKeys.Where(k => k is not null).Select(k => k!).ToArray());
        Assert.Equal("NASDAQ", HttpUtility.ParseQueryString(handler.Requests[1].Query)["exchange"]);
    }

    [Fact]
    public async Task The_holiday_range_is_sent_verbatim_and_never_widened()
    {
        // Measured 2026-08-30 against NASDAQ's 2026-07-03 holiday, the upstream window is HALF-OPEN —
        // (from, to]: from=2026-07-03&to=2026-07-03 returns [], from=2026-07-03&to=2026-07-04 returns [],
        // and from=2026-07-02&to=2026-07-03 returns the row. `to` is inclusive, `from` is not, and a
        // single-day range therefore always answers [] no matter what falls on that day.
        //
        // The obvious "fix" is to send from.PlusDays(-1) so the signature behaves the way a caller expects
        // a date range to behave. The design rejects it: the request this SDK sends would then not match
        // the arguments the caller passed, which turns every debugging session into a puzzle. The behaviour
        // is documented on the method instead, and this test is what stops the compensation being added —
        // it fails the moment either bound is altered on the way out.
        //
        // A unit test cannot observe the upstream's half of this contract; that lives in the measurements
        // file. What it CAN pin is this SDK's half, which is the half anybody would change.
        var (endpoints, handler) = Build();
        var day = new LocalDate(2026, 7, 3);

        await endpoints.GetHolidaysAsync("NASDAQ", day, day);

        var query = HttpUtility.ParseQueryString(handler.Requests.Single().Query);
        Assert.Equal("2026-07-03", query["from"]);
        Assert.Equal("2026-07-03", query["to"]);
    }

    [Fact]
    public async Task The_single_exchange_call_returns_one_record_and_null_on_an_empty_array()
    {
        // Every measured response was a single-element array, so the method takes the first row — the
        // CompanyEndpoints.GetProfileAsync shape. null was NEVER observed and probably cannot happen: an
        // unknown exchange is HTTP 400 "Invalid Exchange Provided.", an exception rather than an empty
        // list, so the empty array that would produce null has no measured cause. The nullable return is
        // honesty about what the deserialiser can promise, not a hint that emptiness is expected.
        var (found, _) = Build(Binding.Fixture("exchange-market-hours.NASDAQ.json"));
        var row = await found.GetExchangeAsync("NASDAQ");

        Assert.NotNull(row);
        Assert.Equal("NASDAQ", row.Exchange);
        Assert.True(row.IsClosedToday);

        var (empty, _) = Build("[]");
        Assert.Null(await empty.GetExchangeAsync("NASDAQ"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NASDAQ,NYSE")]
    public async Task An_exchange_that_is_not_one_exchange_is_rejected_before_the_call(string exchange)
    {
        // Measured 2026-08-30, exchange=NASDAQ,NYSE is HTTP 400 "Invalid Exchange Provided." — so unlike
        // the comma case on the ETF paths this is ALREADY an error and not a silent empty list. The guard
        // is still worth having: it turns a wasted call against the key's quota into an ArgumentException
        // that names the fix. The message must not claim the wire answers silently, because it does not.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetExchangeAsync(exchange));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            endpoints.GetHolidaysAsync(exchange, new LocalDate(2024, 1, 1), new LocalDate(2026, 12, 31)));

        Assert.Empty(handler.Requests);          // and nothing reached the network
    }

    [Fact]
    public async Task A_backwards_holiday_range_is_rejected_before_the_call()
    {
        // Measured 2026-08-30, a reversed range answers [] with HTTP 200 — "no holidays in that window",
        // indistinguishable from a genuinely quiet range. This is exactly the case DateRange was extracted
        // for, and the shared guard is used rather than a fifth copy of the check.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            endpoints.GetHolidaysAsync("NASDAQ", new LocalDate(2026, 12, 31), new LocalDate(2024, 1, 1)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_exchange_code_of_the_wrong_case_is_sent_as_given()
    {
        // Measured 2026-08-30, exchange=nasdaq returned a response BYTE-IDENTICAL to exchange=NASDAQ on
        // both paths. There is nothing to normalise, and normalising would silently rewrite the caller's
        // identifier — the same rule CompanyEndpoints.GetProfileByCikAsync follows for a padded CIK.
        var (endpoints, handler) = Build();

        await endpoints.GetExchangeAsync("nasdaq");

        Assert.Equal("nasdaq", HttpUtility.ParseQueryString(handler.Requests.Single().Query)["exchange"]);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~MarketHoursTests"
```

Expected: compile failure — `MarketHoursEndpoints` does not exist.

- [ ] **Step 3: Write the facade**

Create `src/FmpDotNet/Endpoints/MarketHoursEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>When exchanges trade — opening and closing bells for 81 exchanges, and the holiday calendar
/// behind them.
///
/// <para><b>Three things hold across all three paths, measured 2026-08-30.</b></para>
///
/// <list type="number">
///   <item><description><b>The exchange code is case-insensitive and the exchange NAME is not accepted.</b>
///     <c>exchange=nasdaq</c> returned a byte-identical response to <c>exchange=NASDAQ</c> on both
///     single-exchange paths; <c>exchange=NASDAQ%20Global%20Market</c> is an HTTP 400. Codes come from
///     <see cref="DirectoryEndpoints.GetExchangesAsync"/> — all <b>63</b> codes it returned appear in
///     <see cref="GetAllExchangesAsync"/>, which carries 18 more.</description></item>
///   <item><description><b>An unknown exchange is an error, not an empty list.</b> <c>exchange=ZZZZ</c> and
///     <c>exchange=NASDAQ,NYSE</c> are both HTTP 400 <c>Invalid Exchange Provided.</c> This SDK does not
///     validate the code itself: the vocabulary is 81 entries that will change, and a client-side list
///     would go stale.</description></item>
///   <item><description><b>Nothing paginates.</b> <c>limit</c> and <c>page</c> were ignored on all three
///     paths — byte-identical responses.</description></item>
/// </list>
///
/// <para>Index membership is a separate facade — <see cref="IndexesEndpoints"/> — because the two groups
/// share no path prefix, no parameter, no record and no concept.</para></summary>
public sealed class MarketHoursEndpoints(FmpTransport transport)
{
    /// <summary>Trading hours for every exchange FMP knows, from <c>stable/all-exchange-market-hours</c>.
    ///
    /// <para>81 rows measured 2026-08-30 — 18 more than
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/> returns. Read
    /// <see cref="ExchangeMarketHours.OpeningAdditionalText"/> before building anything on the first row:
    /// seven of these exchanges break for lunch and carry two extra keys that 74 rows lack.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every exchange, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points
    /// at the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeMarketHours>> GetAllExchangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/all-exchange-market-hours"),
            FmpJsonContext.Default.ListExchangeMarketHours, ct);

    /// <summary>Trading hours for one exchange, from <c>stable/exchange-market-hours</c>.
    ///
    /// <para><b>The row is the same row <see cref="GetAllExchangesAsync"/> carries.</b> For each of seven
    /// exchanges cross-checked 2026-08-30, this path's single row compared <b>equal, key for key and value
    /// for value</b>, to that exchange's row in the 81-row response. Call this when you want one exchange
    /// and that one when you want them all.</para>
    ///
    /// <para><b><see langword="null"/> was never observed and probably cannot happen.</b> Every measured
    /// response was a single-element array, and an unknown exchange is an HTTP 400 rather than an empty
    /// array — so the emptiness that would produce <see langword="null"/> here has no measured cause. The
    /// nullable return is honesty about what the deserialiser can promise, not a hint that emptiness is
    /// expected.</para>
    ///
    /// <para>The code is sent exactly as given: <c>nasdaq</c> and <c>NASDAQ</c> answered byte-identically
    /// on 2026-08-30, so there is nothing to normalise and normalising would rewrite the caller's
    /// identifier.</para></summary>
    /// <param name="exchange">The exchange code — <c>"NASDAQ"</c>, <c>"JPX"</c>. One exchange; a
    /// comma-joined list is rejected. Case-insensitive upstream. The exchange's full <i>name</i> is not
    /// accepted.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The exchange's hours, or <see langword="null"/> on an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status — including <b>400</b> for an
    /// exchange it does not know.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<ExchangeMarketHours?> GetExchangeAsync(string exchange, CancellationToken ct = default)
    {
        ThrowIfNotOneExchange(exchange);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/exchange-market-hours").With("exchange", exchange),
            FmpJsonContext.Default.ListExchangeMarketHours, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>The days an exchange is closed or closes early, over a required date range, from
    /// <c>stable/holidays-by-exchange</c>.
    ///
    /// <para><b>The range is required because the default answer hides the future.</b> Measured 2026-08-30
    /// across five exchanges, the bare call returned <b>67 rows, every one dated between 2025-08-30 and that
    /// day, and not one dated after it</b> — while <c>from=1990-01-01&amp;to=2035-12-31</c> returned 446 rows
    /// reaching <b>2032-12-31</b>. The most natural question a caller has for this endpoint — <i>when is the
    /// market next closed?</i> — is the one question its default answer can never answer. Making the range
    /// required costs the caller one obvious line and removes a wrong answer that arrives at HTTP 200 with
    /// no warning.</para>
    ///
    /// <para><b>The window is half-open — <c>(from, to]</c> — and this SDK does not compensate for it.</b>
    /// Measured 2026-08-30 against NASDAQ's 2026-07-03 holiday: <c>from=2026-07-03&amp;to=2026-07-03</c>
    /// returns <c>[]</c>, <c>from=2026-07-03&amp;to=2026-07-04</c> returns <c>[]</c>, and
    /// <c>from=2026-07-02&amp;to=2026-07-03</c> returns the row. <c>to</c> is inclusive, <c>from</c> is not,
    /// and <b>a single-day range therefore always answers an empty list</b> no matter what falls on that
    /// day. Pass a <paramref name="from"/> one day before the earliest date you care about.</para>
    ///
    /// <para>Sending <c>from.PlusDays(-1)</c> upstream on the caller's behalf would make this signature
    /// behave the way a date range is expected to, and is deliberately <b>not</b> done: the request would
    /// then not match the arguments passed, which turns every debugging session into a puzzle.</para>
    ///
    /// <para><b>These are the only two date parameters honoured in this group.</b> On the three
    /// <c>historical-*-constituent</c> paths <c>from</c> and <c>to</c> are accepted and discarded, which is
    /// why <see cref="IndexesEndpoints"/>'s methods do not offer them.</para></summary>
    /// <param name="exchange">The exchange code. One exchange; a comma-joined list is rejected.</param>
    /// <param name="from">The day <b>before</b> the earliest date wanted — the bound is exclusive
    /// upstream.</param>
    /// <param name="to">The latest date wanted; this bound is inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holiday in the window, in FMP's own order. Measured 2026-08-30 that order is <b>by
    /// date, descending</b>. Never <see langword="null"/>; an empty list means either no holidays or a range
    /// one day wide.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is blank or contains a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>. FMP answers a reversed range with an empty list at HTTP 200, which reads as
    /// "no holidays".</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ExchangeHoliday>> GetHolidaysAsync(
        string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ThrowIfNotOneExchange(exchange);
        DateRange.ThrowIfBackwards(from, to);
        return transport.GetListAsync(
            new FmpRequest("stable/holidays-by-exchange")
                .With("exchange", exchange).With("from", from).With("to", to),
            FmpJsonContext.Default.ListExchangeHoliday, ct);
    }

    /// <summary>Rejects an exchange argument FMP would answer with a 400.
    ///
    /// <para>Measured 2026-08-30, <c>exchange=NASDAQ,NYSE</c> answers <b>HTTP 400</b>
    /// <c>Invalid Exchange Provided.</c> — so unlike the comma case on the ETF paths, this is already an
    /// error rather than a silent empty list. The guard is still worth having: it turns a wasted call
    /// against the key's quota into an <see cref="ArgumentException"/> that names the fix, and it matches
    /// <c>ThrowIfNotOneSymbol</c>'s established shape. It does <b>not</b> validate the code's spelling —
    /// the vocabulary is upstream's and will change.</para></summary>
    private static void ThrowIfNotOneExchange(string exchange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (exchange.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "These paths take one exchange. Measured 2026-08-30, a comma-joined list answers HTTP 400 "
                + "'Invalid Exchange Provided.' — a wasted call against the key's quota. Call once per "
                + "exchange, or use GetAllExchangesAsync.",
                nameof(exchange));
        }
    }
}
```

- [ ] **Step 4: Wire the facade into `FmpClient`**

Add `MarketHoursEndpoints marketHours` to the primary constructor's parameter list in
`src/FmpDotNet/FmpClient.cs`, after `IndexesEndpoints indexes`, and add the property after `Indexes`:

```csharp
    /// <summary>When exchanges trade — opening and closing bells for 81 exchanges, and the holiday calendar
    /// behind them.
    ///
    /// <para>Its own facade rather than a corner of <see cref="Indexes"/>: the two groups share no path
    /// prefix, no parameter, no record and no concept. Read
    /// <see cref="MarketHoursEndpoints.GetHolidaysAsync"/> before passing a date range — the window is
    /// half-open and a single-day range always answers empty.</para></summary>
    public MarketHoursEndpoints MarketHours { get; } = marketHours;
```

- [ ] **Step 5: Register the facade for dependency injection**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, after
`services.TryAddTransient<IndexesEndpoints>();`:

```csharp
        services.TryAddTransient<MarketHoursEndpoints>();
```

- [ ] **Step 6: Update `AddFmpTests`**

After `Assert.NotNull(client.Indexes);`:

```csharp
        Assert.NotNull(client.MarketHours);
```

and change `Assert.Equal(21, typeof(FmpClient)` to `Assert.Equal(22, typeof(FmpClient)`.

- [ ] **Step 7: Promote every remaining deferred cref**

Find them all rather than trusting this list:

```bash
grep -n "<c>MarketHoursEndpoints</c>\|<c>GetHolidaysAsync</c>\|<c>IndexesEndpoints</c>" src/FmpDotNet/
```

Expected matches, and what each becomes:

| file | from | to |
|---|---|---|
| `Models/ExchangeMarketHours.cs` | `<c>MarketHoursEndpoints</c>` | `<see cref="FmpDotNet.Endpoints.MarketHoursEndpoints"/>` |
| `Models/ExchangeHoliday.cs` | `<c>MarketHoursEndpoints</c>` | `<see cref="FmpDotNet.Endpoints.MarketHoursEndpoints"/>` |
| `Models/ExchangeHoliday.cs` | `<c>GetHolidaysAsync</c>` | `<see cref="FmpDotNet.Endpoints.MarketHoursEndpoints.GetHolidaysAsync"/>` |

`Models/IndexConstituentChange.cs` was promoted in Task 6; the grep should not find it again. If the grep
returns a match this table does not list, promote it too — the table is a checklist, not a boundary.

`ExchangeMarketHours`'s reference to `DirectoryEndpoints.GetExchangesAsync` is already a real `<see cref>`;
that type has existed since before this slice and needs nothing.

- [ ] **Step 8: Build and confirm the crefs resolve**

```bash
dotnet build
```

Expected: **zero warnings.** `TreatWarningsAsErrors=true` makes an unresolvable `<see cref>` a build failure,
so a clean build is the proof the promotions landed.

- [ ] **Step 9: Run the whole suite and confirm the ONE expected failure**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: **exactly one** failure — `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`,
now nine paths behind rather than six. Task 9 regenerates it.

`Every_public_endpoint_method_reaches_the_api` must stay green. Its argument synthesiser gives `"AAPL"` for
`exchange` (accepted: not blank, no comma) and `new LocalDate(2026, 1, 2)` for **both** `from` and `to` — an
equal pair, which `DateRange.ThrowIfBackwards` does not reject. If it fails, one of the two guards is
stricter than this plan specifies.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Endpoints/MarketHoursEndpoints.cs src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        src/FmpDotNet/Models/ExchangeMarketHours.cs src/FmpDotNet/Models/ExchangeHoliday.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Tests/MarketHoursTests.cs
git commit -m "feat: add the fmp.MarketHours facade, with a required holiday range (#38)"
```

---

### Task 8: Teach the live sweep to ask the holiday path something worth answering

Eight of the nine new endpoints need nothing: six take no arguments at all, and `Probe.Argument` already has
`"exchange" => LiveApi.Exchange` (`Probe.cs:356`), which is `"NASDAQ"` and answered 200 on both market-hours
paths on 2026-08-30.

The ninth is the problem. `GetHolidaysAsync` takes a date range, and the generic `LocalDate` arm would give it
`RangeStart`..`SettledWeekday` — a ninety-day trailing window. Measured against the 446-row corpus, that
window (2026-05-23 .. 2026-08-21) holds **3** NASDAQ holidays, and a quiet quarter takes it to **zero**: the
endpoint would record `outcome empty` as its healthy baseline and match that green for ever. That is the exact
silent-green failure `LiveApi.RangeStart`'s own doc was written about.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`

**Interfaces:**
- Consumes: `Endpoints.MarketHoursEndpoints.GetHolidaysAsync` (Task 7) — by exact type and method name.
- Produces: `LiveApi.HolidayRangeStart` and `LiveApi.HolidayRangeEnd`, both `static readonly LocalDate`.

- [ ] **Step 1: Write the failing pinning test**

Add to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`, at the end of the class. Every other named constant
in `LiveApi` has such a test — `Exchange`, `Industry`, `FilerCik`, `CotContract` — and without one the two new
arms could be deleted and the sweep would go quietly back to a three-holiday window.

```csharp
    [Fact]
    public void The_sweep_asks_the_holiday_path_for_a_window_with_holidays_in_it()
    {
        // The generic LocalDate arm gives `from` LiveApi.RangeStart and `to` LiveApi.SettledWeekday — a
        // ninety-day trailing window. Measured 2026-08-30 against the 446-row NASDAQ corpus, that window
        // (2026-05-23 .. 2026-08-21) holds THREE holidays and a quiet quarter takes it to zero, which
        // records `outcome empty` as this endpoint's healthy baseline and matches itself green for ever.
        //
        // This is the THIRD fixed range in the sweep, after LiveApi.IndicatorRangeStart and
        // LiveApi.CotRangeStart, and it is fixed for its own reason: not that the data stops, but that the
        // holiday calendar is SPARSE — about 13 rows a year for NASDAQ — so a window has to be years wide
        // before it is safely non-empty. 2024-01-01 .. 2026-12-31 returned 38 rows on 2026-08-30.
        var holidays = typeof(Endpoints.MarketHoursEndpoints)
            .GetMethod(nameof(Endpoints.MarketHoursEndpoints.GetHolidaysAsync))!;

        Assert.Equal(LiveApi.Exchange, Probe.Argument(holidays.GetParameters()[0]));
        Assert.Equal(LiveApi.HolidayRangeStart, Probe.Argument(holidays.GetParameters()[1]));
        Assert.Equal(LiveApi.HolidayRangeEnd, Probe.Argument(holidays.GetParameters()[2]));
        Assert.NotEqual(LiveApi.RangeStart, Probe.Argument(holidays.GetParameters()[1]));

        // Wide enough to be safe, and the SDK's own documented boundary rule means a one-day range would
        // answer empty no matter what falls on that day.
        Assert.True(
            NodaTime.Period.Between(LiveApi.HolidayRangeStart, LiveApi.HolidayRangeEnd).Years >= 2,
            "The holiday calendar is sparse; a window narrower than two years is one quiet stretch away "
            + "from an empty baseline.");

        // And the single-exchange path keeps the existing arm — no new string constant was needed, because
        // NASDAQ answered 200 on both market-hours paths on 2026-08-30.
        var single = typeof(Endpoints.MarketHoursEndpoints)
            .GetMethod(nameof(Endpoints.MarketHoursEndpoints.GetExchangeAsync))!;

        Assert.Equal(LiveApi.Exchange, Probe.Argument(single.GetParameters()[0]));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"
```

Expected: compile failure — `LiveApi.HolidayRangeStart` does not exist. The smoke project's other tests skip
without a key; `SweepCoverageTests` needs none, which is the point of that class.

- [ ] **Step 3: Add the two `LiveApi` constants**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `IndicatorRangeEnd` and before `CotContract`:

```csharp
    /// <summary>The window the holiday probe asks for — <b>fixed dates, deliberately</b>, and the third of
    /// three fixed ranges in this file. See <see cref="IndicatorRangeStart"/> and <see cref="CotRangeStart"/>
    /// for the other two, each frozen for a different reason.
    ///
    /// <para><b>Fixed because the calendar is SPARSE, not because the data stops.</b> That is what makes this
    /// one different from the other two. Measured 2026-08-30, <c>holidays-by-exchange</c> answers about 13
    /// rows a year for NASDAQ, so the ninety-day window <see cref="RangeStart"/> and
    /// <see cref="SettledWeekday"/> produce — 2026-05-23 to 2026-08-21 — contains exactly <b>three</b>
    /// holidays, and a quiet quarter takes it to zero. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties and matches that baseline every week thereafter, which is the
    /// silent green <see cref="Exchange"/> and <see cref="Cik"/> were each named for.</para>
    ///
    /// <para>2024-01-01 to 2026-12-31 answered <b>38 rows</b> on 2026-08-30, spanning full closures and the
    /// early-close shape, so the baseline records both of the record's two states rather than one.</para>
    ///
    /// <para><b>The <c>from</c> bound is EXCLUSIVE upstream</b> — measured 2026-08-30, the window is
    /// <c>(from, to]</c> and a single-day range always answers an empty array. A relative range narrowed to
    /// a day here would be empty by construction, not by drift.</para></summary>
    public static readonly LocalDate HolidayRangeStart = new(2024, 1, 1);

    /// <summary>The end of <see cref="HolidayRangeStart"/>'s window. Inclusive upstream.</summary>
    public static readonly LocalDate HolidayRangeEnd = new(2026, 12, 31);
```

- [ ] **Step 4: Add the two `Probe.Argument` arms**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, inside `if (type == typeof(LocalDate))`, **before** the
`"from" => LiveApi.RangeStart` fallthrough. Narrowed by declaring type, following the COT arms directly above
them:

```csharp
                // The holiday calendar is sparse — about 13 NASDAQ rows a year, measured 2026-08-30 — so the
                // ninety-day window the generic arm gives holds THREE holidays and a quiet quarter holds
                // none. FIXED rather than relative for that reason; see LiveApi.HolidayRangeStart. Narrowed
                // by declaring type because MarketHoursEndpoints has exactly one date-ranged method and a
                // second one should be measured before it inherits this window.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.MarketHoursEndpoints)
                    => LiveApi.HolidayRangeStart,
                "to" when parameter.Member.DeclaringType == typeof(Endpoints.MarketHoursEndpoints)
                    => LiveApi.HolidayRangeEnd,
```

**No new string arm.** `"exchange" => LiveApi.Exchange` already exists at `Probe.cs:356` and serves both
market-hours methods. Adding a second is the mistake this step exists to prevent.

- [ ] **Step 5: Run the sweep-coverage suite to verify it passes**

```bash
dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"
```

Expected: PASS. No key is read and no request is made.

- [ ] **Step 6: Run the whole suite and confirm the ONE expected failure**

```bash
dotnet build && dotnet test
```

Expected: build clean; the only failure is still the README coverage table. The live smoke tests skip without
a key, which is the intended state here.

- [ ] **Step 7: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs
git commit -m "test: probe the holiday path over a window that actually holds holidays (#38)"
```

---

### Task 9: Regenerate the README, re-record the live baseline, and close the issue

The last task, and the one that turns the suite green.

**Files:**
- Modify: `README.md`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

**Interfaces:** none — nothing downstream depends on this task.

- [ ] **Step 1: Regenerate the coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

Then check the result rather than trusting it:

```bash
git diff --stat README.md
grep -n "of FMP.s 243 endpoint paths are modelled" README.md
```

Expected: the headline reads **216 of FMP's 243 endpoint paths are modelled**, and two new sections appear —
`` `fmp.Indexes` `` with six paths against six methods, and `` `fmp.MarketHours` `` with three against three.
The generator orders groups with `StringComparer.Ordinal` on the property name
(`EndpointCoverageTests.Render`), so `Indexes` lands between `EtfAndFunds` and `InsiderTrades` — <b>not</b>
after `InsiderTrades`, which is where an alphabetical eye puts it — and `MarketHours` between
`InstitutionalOwnership` and `MarketPerformance`. If the headline is not 216, an endpoint is not being
discovered — `Every_public_endpoint_method_reaches_the_api`
names which one.

- [ ] **Step 2: Fix the prose the generator does not read**

Two paragraphs carry arithmetic this slice changes. `EndpointCoverageTests` regenerates the table above them
but never reads them, so they rot silently.

Replace this paragraph (README ~line 428):

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **36 paths
remain**, of which **29 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are News (10) and Fundraisers & DCF (10),
and Indexes & Market Hours carries 9.
```

with:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **27 paths
remain**, of which **20 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the two largest groups are News (10) and Fundraisers & DCF
(10), which between them are three quarters of what is left.
```

and replace this one (README ~line 440):

```markdown
That remainder is tracked as four issues under the epic, three of them actionable, each 7 to 10 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 207 modelled plus 36 remaining, with no path counted twice and none missing.
```

with:

```markdown
That remainder is tracked as three issues under the epic, two of them actionable, each 7 to 10 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 216 modelled plus 27 remaining, with no path counted twice and none missing.
```

Then check the paragraph two below those, which begins "Commodity, Forex and Crypto contribute **one path
each**". It ends "and **most of what is under Indexes** is `stable/quote` and `stable/historical-price-eod`
re-documented, which `fmp.Quote` and `fmp.Chart` already reach." **That sentence is now stale in a way the
tests cannot see** — Indexes is finished. Replace the clause "and most of what is under Indexes, is" with
"is", leaving the rest of the sentence intact, and read the result once to confirm it still parses.

- [ ] **Step 3: Verify the arithmetic against the issues rather than trusting it**

The three issues remaining under the epic once #38 closes are **#33** (News), **#39** (Fundraisers and DCF)
and **#41** (TipRanks):

```bash
for n in 33 39 41; do
  gh issue view $n --json body --jq .body | grep -coE 'stable/[a-z0-9-]'
done | paste -sd+ | bc
```

Expected: `27`. That is `243 - 216`, so the partition holds with no gap and no double count. If it prints
anything else, the prose is wrong — fix the prose, not this check.

- [ ] **Step 4: Run the unit suite green**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: PASS, all of it, including `EndpointCoverageTests`. **This is the first point since Task 5 at which
the whole unit suite is green** — the known single failure from Tasks 6-8 is now resolved.

- [ ] **Step 5: Re-record the live baseline**

The baseline is a measurement, not a specification — **never hand-edit it**. Record it in one run so its
header date is true of every line:

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY\s*=\s*\"?([^\"\s]+)\"?', open('.env').read(), re.M).group(1))") \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

Do **not** `source` the `.env`. Do **not** set `FMPDOTNET_SMOKE_BULK`: `baseline-bulk.txt` is untouched by
this slice, and re-recording it would spend the key's standing on twenty of FMP's most restricted endpoints
for nothing.

`ShapeAssertions.Updated` refuses to write a baseline from a run in which any endpoint errored, so a transport
fault or a throttled key fails loudly here rather than writing `outcome error` in as an endpoint's recorded
truth. If it refuses, wait and re-run rather than working around it.

**Expect this run to move a few hundred kilobytes.** The nine new calls are dominated by
`historical-sp500-constituent` at 365,284 bytes; everything else is small.

- [ ] **Step 6: Read the baseline diff before committing it**

```bash
git diff --stat tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
grep -c '^\[' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | grep '^[+-]\['
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | grep 'outcome'
```

Expected, each item a thing to check rather than assume:

1. The entry count goes from **187 to 196** — six new `[Indexes.*]` blocks and three new `[MarketHours.*]`.
2. Every one of the nine reads `outcome rows`. **Not one may read `empty`.** An empty on
   `MarketHours.GetHolidaysAsync` means Task 8's arms are not being reached; an empty on any Indexes method
   means the path is wrong, since none of them takes an argument that could be wrong.
3. The header date is today's.
4. Nothing else changed. Any `now always null, was populated` line on an endpoint this slice did not touch is
   a real finding — stop and investigate rather than committing it.

**Properties expected to record `null` inside the new blocks, all of them correct.** Check against this list
rather than treating a `null` as automatically wrong:

- `ExchangeMarketHours.OpeningAdditionalText` and `ClosingAdditionalText` record `set` only if one of the
  seven lunch-break exchanges appears in the sweep's probe — `GetAllExchangesAsync` returns all 81, so they
  should record `set`; `GetExchangeAsync` probes NASDAQ alone and they will record `null` there, correctly.
- `ExchangeHoliday.AdjustedOpenTime` records `null` — it was null on all 446 rows measured 2026-08-30 and
  has never been observed populated. **This is the one `null` in the diff that is expected to persist for
  ever.**
- `ExchangeHoliday.IsFullyClosed` records `set` only if an early-close row falls inside
  2024-01-01 .. 2026-12-31. Measured 2026-08-30 the window held both shapes, so it should record `set`; if it
  records `null`, the window is not reaching the early closes and Task 8's constants need re-measuring.
- `IndexConstituent.DateFirstAdded` records `set` — it is null only on 7 Nasdaq rows and the shape recorder
  sees the whole list.

If `ExchangeMarketHours.OpeningHourText` records `null` on either method, that is a converter or attribute
defect, not a market state: the field carries `"CLOSED"` rather than nothing when the exchange is shut.

- [ ] **Step 7: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "docs: regenerate the coverage table at 216 of 243, and re-record the live baseline (#38)"
```

- [ ] **Step 8: Final whole-suite run**

```bash
dotnet build && dotnet test
```

Expected: build with **no warnings** (`TreatWarningsAsErrors=true` makes any doc-comment defect a failure),
and the whole suite green. The live smoke tests skip without a key, which is the intended state for a normal
run.

- [ ] **Step 9: Close the issue and correct the epic**

```bash
gh issue view 38 --json title,state
gh issue view 25 --json body --jq .body | head -40
```

Close #38 with a house-style comment naming what shipped: two facades, nine methods, four records, two new
converters, coverage 207 → 216 of 243, and the two traps a reader would most want flagged — `founded` is not
a date, and the holiday window is `(from, to]`. Then edit epic #25 to move #38 into Shipped and correct the
remainder arithmetic to **three open children, 27 paths, 20 actionable**.

Both are outward-facing edits to a shared tracker. **Draft them, show them, and get a yes before posting** —
they are not covered by the authorisation to execute this plan.

---

## Self-review notes

Checked while writing, recorded so a reviewer does not repeat the work.

**Spec coverage.** Every section of the design spec maps to a task: the nine methods → Tasks 6-7; the four
records → Tasks 2-5; the two new converters → Tasks 2 and 5; `SentinelStringJsonConverter` applied to four
fields → Task 2; the two guards → Task 7; "why the hour fields get no converter" → Task 4's record and its
`A_closed_exchange_parses_no_hours_and_says_why` test; "what is documented rather than guarded" → the XML docs
in Tasks 4-7 and the tests that pin them; serialisation and wiring → Tasks 2-7; the smoke sweep → Task 8; the
README, the baseline and the issue → Task 9; the two open measurement gaps → Task 1.

**All nine tests in the spec's falsifiability table are present**, eight under the spec's own names:

| spec test | task |
|---|---|
| `Founded_is_a_string_because_the_sp500_sends_bare_years` | 3 |
| `A_closed_exchange_parses_no_hours_and_says_why` | 4 |
| `The_lunch_break_exchanges_keep_their_afternoon_session` | 4 |
| `An_early_close_is_not_a_closure` | 5 |
| `dateAdded_and_date_are_read_separately` | 2 |
| `The_dow_jones_feed_spells_absence_with_an_empty_string` | 2 |
| `A_long_form_date_binds_under_any_culture` | 2 |
| `A_negative_offset_hour_parses` | 4 |
| `The_holiday_range_excludes_its_own_from_date` → **renamed** `The_holiday_range_is_sent_verbatim_and_never_widened` | 7 |

**Two spec statements were ruled on rather than left for the implementer** — the `AddFmpTests` count and that
rename. Both are at the top of this plan with their reasoning and their cost if wrong.

**One test's guarantee is narrower than its name suggests, and the plan says so in the test itself.**
`A_long_form_date_binds_under_any_culture` catches a pattern built from `CultureInfo.CurrentCulture`
**per call** every time, and one built **statically** from it only when the test runs before anything else
touches the converter — because a static pattern captures the culture at type-initialisation time. Writing the
name without that caveat would have been the same defect class the falsifiability table exists to prevent, so
the caveat is in the test's own comment where a reader meets it.

**Every NodaTime pattern in this plan was verified against NodaTime 3.2.2 on 2026-08-30 before being written
into it**, not after, using a `dotnet run` file-based app outside the solution:

- `OffsetTimePattern "hh:mm tt o<m>"` parses all measured forms and both negative-offset forms, formats back
  byte-identically (`09:00 AM +09:00`), gets noon and midnight right, and fails on `"CLOSED"` and `""`.
  `o<G>` was rejected because it emits `+09` and `Z`.
- `LocalDatePattern "MMMM d, yyyy"` (invariant) parses both wire paddings; `"MMMM dd, yyyy"` **fails** on
  `"July 9, 2025"`, which is why `d` is used; `Format` emits `August 5, 2026` for a padded input, which is why
  the round-trip test asserts what it asserts; a `fr-FR` pattern **fails** on `"June 29, 2026"`, which is what
  makes the culture test falsifiable.
- `LocalTimePattern "HH:mm"` parses `13:00` and `13:30`, round-trips exactly, and fails on
  `"13:00 PM +09:00"` and `"1:00 PM"`.
- `LocalDatePattern.Iso` fails on `"June 29, 2026"`, `"2012"` and `"1904/1946/1959"` — the three failures that
  make the `Founded` and `DateAdded` decisions load-bearing rather than stylistic.
- `Period.Between(2024-01-01, 2026-12-31).Years` is **2**, and the ninety-day window's is **0**, so Task 8's
  width assertion fails against the arm it replaces.

**Every fixture row in this plan is verbatim from the 2026-08-30 capture set.** Three fixtures assemble rows
from a single response rather than taking its head — `historical-sp500-constituent.dates.json`,
`sp500-constituent.founded.json` and `all-exchange-market-hours.head.json` — and each says so where it is
defined. **No row is constructed.** The inline JSON literals inside tests are the exception and are marked as
such: they are minimal probes for a converter's behaviour (`"half past nine"`, `"09:30 AM -05:00"`,
`"Wormholes"`), not claims about what FMP sends.

**Four repo facts were read rather than assumed**, each recorded under "What this plan does NOT need to
touch": `EndpointCoverageTests.Argument` needs no new arm, `Probe.Argument` already has its `exchange` arm,
`DateRange.ThrowIfBackwards` and `SentinelStringJsonConverter` already exist. The first two are where the
spec's first draft was wrong, which is why they are named explicitly rather than left implicit.

**The test project's fixture glob is `Fixtures\*.json` with `CopyToOutputDirectory="PreserveNewest"`**, so the
eight new fixtures need no `.csproj` edit. Checked at `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj`.

**The README paragraphs quoted verbatim in Task 9 were diffed against the live file** and match byte for byte,
so the replacements will apply. Task 9 also names a **third** stale sentence — "most of what is under Indexes"
— which no test can see and which this slice makes false.

**Task 1 is the only task whose output this plan cannot state in advance**, because it is a measurement.
Its bracketed values are each named against the exact line of Step 4's output that fills them, and Task 4
carries both candidate doc texts — the one for a closed gap and the one for an open gap — so no later task
waits on it.
