# Live Smoke Suite

A runbook for `tests/FmpDotNet.SmokeTests` — the suite that calls the real FMP API and checks that what comes back
still matches what the SDK was built against.

## What it is for

The unit suite runs entirely on stubs, and **a stub keeps saying what it always said**. Nothing in ordinary CI can
notice FMP renaming a field, moving a plan gate or changing a media type.

Worse, **a rename does not fail**. Almost every model property is nullable and none are `required`, so
`System.Text.Json` deserialises the missing name to null, hands back the same number of rows of the same type, and
reports nothing at all. A smoke test asserting *"a non-empty list came back"* passes on the day the data stops
arriving.

**So this suite records which fields carried a value, not merely that a call succeeded.** That is the whole
design.

## The baseline files

Two checked-in records, one line per property:

| File | Covers |
|---|---|
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | the ordinary endpoints |
| `tests/FmpDotNet.SmokeTests/baseline-bulk.txt` | the `*-bulk` endpoints, swept separately |

```
[Statements.GetIncomeStatementAsync]
outcome rows
set NetIncome
```

* **`set`** — the property carried a value on at least one row FMP returned.
* **`null`** — it was null, blank or empty on **every** one of them.

**`set NetIncome` becoming `null NetIncome` is the alarm.** A rename is a one-line diff.

## Running it

```bash
# Ordinary endpoints. Seconds.
FMP_API_KEY=… dotnet test tests/FmpDotNet.SmokeTests

# The bulk endpoints as well. About eight minutes, nearly all of it waiting on the throttle.
FMP_API_KEY=… FMPDOTNET_SMOKE_BULK=1 dotnet test tests/FmpDotNet.SmokeTests

# Re-record — after reading the diff and satisfying yourself nothing was lost.
FMP_API_KEY=… FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
```

Without `FMP_API_KEY`, every live test **skips itself**, so a clone with no key runs the whole solution green and
offline.

### The environment variables

| Variable | Effect |
|---|---|
| `FMP_API_KEY` | Unset ⇒ every live test skips. This is what makes a keyless clone green. |
| `FMPDOTNET_SMOKE_BULK` | Set to any non-empty value to include the `*-bulk` sweep. |
| `FMPDOTNET_UPDATE_SMOKE_BASELINE` | Re-records the baseline instead of asserting against it. |

## Why bulk is opt-in

FMP's own throttle text warns that

> frequent abuse on this API Endpoint may result in restrictions placed on this API Key

**The cost of sweeping bulk weekly is the key, not the runner minutes.** So it is excluded by default and needs a
second, deliberate switch.

When it does run, it is paced by **the SDK's own bulk reservoir** — `BulkPerMinuteCap`, defaulting to 2 a minute.
There is no pacing code in the test suite; the probes simply queue behind the reservoir every caller shares.

Each probe reads the first **25** rows and then abandons the download rather than transferring a file that can
reach 69 MB. That number is not arbitrary: a 200-row sample, tried once, took **2 h 39 m** against roughly 8
minutes, and would still have been sampling a single shard.

## The two assertions, and why there are two

Each baseline is checked twice, with deliberately different meanings:

1. **Something that was arriving has stopped.** That is a defect in shipped code, and it is the one worth waking
   up for.
2. **Any difference at all**, including a field FMP has *started* sending — which asks for the record to be
   regenerated rather than reporting a break.

Folded into one assertion, a newly populated field and a newly missing one would produce the same red. Kept apart,
the failure tells you which kind of change happened.

## Re-recording the baseline

**Read the diff before you regenerate.** That is the entire safeguard — the file is the only record of what FMP
was sending, and regenerating without reading is how a real break gets committed as though it were drift.

```bash
FMP_API_KEY=… FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Reading the diff, line by line:

| Change | Meaning | Action |
|---|---|---|
| `set X` → `null X` | **Stop.** FMP renamed a field, or stopped sending it. | Investigate before regenerating. This is the failure the suite exists for. |
| `null X` → `set X` | FMP started populating something. | Regenerate. |
| A new property line | You added a model property. | Regenerate. |
| A new `[Group.Method]` block | You added an endpoint. | Regenerate. |
| A **bulk** `set` → `null` | Weaker evidence — see below. | Check the fixture and the mapper before assuming a break. |

A failed sweep **cannot record itself as the baseline**. An offline test enforces that, because the alternative
leaves the weekly run green precisely because an endpoint had broken.

## A bulk `null` is weaker evidence than an ordinary one

A bulk probe reads the first 25 rows of **one part**, and a part is an **unordered shard** FMP republishes every
few hours. So a sparse column can read as absent one week and populated the next.

That is a property of the data rather than a fault. It costs one regeneration when it happens, and no affordable
sample size fixes it.

Before treating a bulk `set` → `null` as a break, check the two things that distinguish sparse data from a broken
mapper:

* Does the captured **fixture** for that endpoint show the column populated? If a unit test reads it correctly
  from the fixture, the mapper is fine.
* Was it also `null` on a nearby run? Persistent absence across shards is different from one sample missing it.

Three properties have been recorded empty on the bulk sweep and checked rather than assumed — `cik` is present in
the `profile-bulk` header and read correctly by a passing unit test, and
`priceToEarningsDilutedGrowthRatioTTM` is blank for the sampled rows in the captured fixture too. Sparse data, not
a broken mapper.

## Adding an endpoint to the sweep

Not optional, and the build enforces it. **An endpoint the sweep skips is an endpoint whose renamed field goes
unnoticed until a consumer hits it.**

An offline test — one that runs in ordinary CI with no key — asserts that the sweep can still reach every
endpoint. So forgetting this step fails the build on the commit that caused it, rather than on the next Monday.

After adding it, re-record the baseline so the new endpoint's properties are on record.

## The weekly workflow

`smoke.yml` runs **Mondays at 06:17 UTC**, plus manual dispatch with an optional *"also probe bulk"* checkbox.

A few deliberate choices worth knowing before you change it:

* **Not on the hour.** GitHub queues scheduled runs, and the top of the hour is the most contended slot, where a
  run can be delayed by tens of minutes or dropped outright.
* **`concurrency: smoke`, no cancellation.** Two concurrent runs would share the key but not the SDK's token
  bucket, which paces itself *per process* — so they would emit at twice the rate measured to be safe.
* **A missing key fails the job loudly.** Every live test skips itself without `FMP_API_KEY` — which is what makes
  a keyless checkout green — so an expired secret would otherwise turn this workflow into a weekly green tick that
  never called anything. An explicit guard step fails first.
* **Failures are emailed, not filed as issues.** GitHub notifies whoever last touched the cron. There is
  deliberately no issue-opening step: a bot that files an issue for a market holiday is a bot people learn to
  ignore.
* **A generous timeout that is not padding.** If a run ever approaches the ceiling, the sample size or FMP's bulk
  throughput has changed, and killing the job is the right answer — a bulk sweep that runs for hours is spending
  the key's standing the whole time.

## What it does not tell you

Three gaps, named rather than papered over.

**It samples one symbol over one recent window.** So a property recorded as populated is populated *for a company
that files everything*. It cannot distinguish a field FMP populates universally from one it populates only for
large US issuers — and it is not checking that any value is **correct**.

**It watches shape, not volume.** If `stock-list` fell from tens of thousands of rows to 500 with every field
still populated, nothing here would notice. A row-count band would catch it, but the calendars swing across an
order of magnitude between a quiet week and earnings season, and a band set today would either flap or be too
loose to mean anything. Setting one honestly needs a few months of recorded runs — which this suite now produces.

**A bulk `null` is weak evidence**, for the reason above.

It answers exactly one question: **is the SDK still reading the shape FMP is still sending.**

## Reference

* [The live smoke suite](../../README.md#the-live-smoke-suite) — including
  the current measured counts
