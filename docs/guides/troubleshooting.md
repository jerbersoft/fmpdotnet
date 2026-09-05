# Troubleshooting

Organised by **what you observed**, not by what is wrong. Most entries here are cases where the failure looks like
success.

---

## Restore and packaging

### `Unable to find package FmpDotNet`

Either the version does not exist, or you are asking for a prerelease without saying so.

`dotnet add package` resolves **stable versions only**. Everything published between releases is a prerelease —
the version being prepared, with `-ci.<CI run number>` on the end — so a build that wants one has to ask:

```bash
dotnet add package FmpDotNet --prerelease
dotnet add package FmpDotNet --version 0.11.0-ci.110
```

If even a stable version cannot be found, check the source list rather than the version: `dotnet nuget list source`
should include `https://api.nuget.org/v3/index.json`, and a `nuget.config` containing `<clear />` removes the
default source without saying so.

Every published version is listed at
[nuget.org/packages/FmpDotNet](https://www.nuget.org/packages/FmpDotNet).

### The build worked yesterday and fails today with a different SDK version

You are floating rather than pinned. A floating reference picks up each release as it lands, and one that opts
into prereleases — a `*-*` range, or a reference added with `--prerelease` — picks up a new build on every push to
`master` that passes CI. Either way the build changes underneath you with no commit of yours. **Pin the exact
version** — see **[Releases and Versioning](releases-and-versioning.md)**.

### `IL2026` / `IL3050` when publishing trimmed or AOT

Not from this SDK. It declares `IsAotCompatible` and its CI turns those two into build errors, so any
reflection-based JSON or configuration binding fails in *this* repository's build rather than in yours.

If you see them, the reflecting code is yours or another dependency's. If you are reaching an unmodelled endpoint
through `FmpTransport`, make sure you passed a `JsonTypeInfo` from a source-generated context rather than relying
on reflection — **[Endpoint Coverage](endpoint-coverage.md)** has the pattern.

---

## Startup

### `OptionsValidationException` on startup

Working as intended — options are validated at startup rather than at first call. The message names the property.
Most common:

| Message mentions | Cause |
|---|---|
| `Fmp:BaseUrl must be an absolute URI` | Missing scheme, or the section did not bind at all so it is `""`. |
| `Fmp:PerMinuteCap must be > 0` | Set to `0`, or bound from a non-numeric string. At `0` the reservoir never refills and calls would **hang forever**, which is why this is rejected. |

### The `Fmp` section is not binding

Options are bound **by name, explicitly** — not by reflection, because `ConfigurationBinder.Bind` is neither trim-
nor AOT-safe. So a **misspelled key is silently ignored** rather than throwing.

Check the property names against **[Configuration](configuration.md)**. `PerMinuteCap` binds; `PerMinuteLimit` is
silently nothing.

### A timeout I set is never firing

You wrote a bare number and expected minutes, or you wrote a clock string with the wrong field order.

**A bare number always means seconds.** `"RequestTimeout": "45"` is 45 seconds. This is deliberate: the bare form
is parsed *first* because `TimeSpan.TryParse("45")` yields **45 days**, which would turn the most natural thing
anyone writes into a timeout that never fires — silently, with no parse error.

Use `"00:00:45"` if you want to be explicit.

---

## Calls that "succeed" but return nothing

### A symbol returns null or an empty list, and you are sure it exists

**Class-share tickers need FMP's hyphenated spelling.** `BRK.B` and `BF.B` answer `[]`; `BRK-B` and `BF-B` answer
a row. It affects `shares-float` and `profile` alike, and it surfaces as an empty result rather than an error — so
a dotted ticker looks exactly like a symbol FMP has never heard of.

Normalise `.` to `-` in class-share tickers before calling.

### The screener returns zero rows for a filter that should match

An **unrecognised parameter *value*** returns `[]` with HTTP 200, indistinguishable from a real filter that
matched nothing.

Get the vocabulary from the API before concluding the universe is empty:

```csharp
var sectors    = await fmp.Directory.GetSectorsAsync(ct);
var industries = await fmp.Directory.GetIndustriesAsync(ct);
```

Also check `Exchange`: the filter takes the **short code only**. A result's own `Exchange` is
`NASDAQ Global Select`, which fed back into a query matches nothing. `ExchangeShortName` round-trips.

### The screener returns *more* than it should

An unrecognised parameter **name** is ignored by FMP: `bogusParam=1&limit=3` returns the same three rows as
`limit=3` alone — so a typo in a filter silently widens the query and looks like a query that worked.

`ScreenerCriteria` closes this: a misspelled filter will not compile. If you are constructing raw requests through
`FmpTransport`, you are exposed to it again.

### A range filter is returning boundary rows twice

`…MoreThan` and `…LowerThan` are **both inclusive**, despite the names. `PriceLowerThan = 1` returns securities
priced at exactly 1, so two adjacent buckets written as `LowerThan = x` and `MoreThan = x` overlap on the edge
rather than partitioning. Offset one side.

### Every field on a model is null, but rows came back

Two very different causes, and it matters which.

**FMP renamed a field.** Almost every model property is nullable and none are `required`, so `System.Text.Json`
deserialises a missing name to null, hands back the same number of rows of the same type, and reports nothing at all.
This is precisely the failure the **[Live Smoke Suite](live-smoke-suite.md)** exists to catch. If you suspect it, run
the sweep and read the baseline diff — `set X` becoming `null X` is the alarm.

**You are looking at a bulk shape that genuinely omits it.** The bulk float rows carry five fields where the
per-symbol endpoint carries six: there is no `source`. Null there means "this shape omits it".

### A bulk column is populated one week and null the next

Expected, and not a mapper fault. A bulk part is an **unordered shard** FMP republishes every few hours, and a
probe reads only the first rows of one part — so a sparse column moves in and out of a sample. See
**[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)**.

---

## Data that looks wrong

### Timestamps are 4–5 hours off

You are reading a field in the wrong zone. FMP sends `"yyyy-MM-dd HH:mm:ss"` **with no offset** for timestamps in
two different zones, so the wire format tells you nothing:

* `acceptedDate` on filings is **Eastern** — and the offset is −4 or −5 depending on the date, so a fixed `-5` is
  wrong for half the year.
* `shares-float`'s `date` is **UTC** — the opposite convention, same string shape.
* The economic calendar is **UTC**.

The SDK already converts each one correctly. If you are seeing a shift, you are probably re-interpreting an
`Instant` rather than converting it for display:

```csharp
var et = DateTimeZoneProviders.Tzdb["America/New_York"];
Console.WriteLine(filing.AcceptedDate?.InZone(et).LocalDateTime);   // right
```

### The earnings calendar is missing a whole day

`earnings-calendar` **truncates silently at exactly 4000 rows and drops the earliest dates**. A one-week request
came back with an entire requested day absent, under HTTP 200 throughout.

Fetch **day at a time** — the only chunk width measured to be safe — and test each response:

```csharp
if (EarningsCalendarResult.IsLikelyTruncated(chunk)) { /* narrow and retry */ }
```

Test **each chunk**, never the concatenation: concatenating discards the per-response evidence that makes the
check exact. Full recipe in **[Recipes](recipes.md)**.

### An earnings row has null actuals

Not an error. `earnings` puts an **unreported row at the head** — the list is newest-first and the newest row is
the *next* report, with `epsActual` and `revenueActual` null and estimates populated. "The last N earnings"
therefore includes one that has not happened.

### Analyst estimates are for the wrong years

`analyst-estimates` is ordered **furthest-future first**, so `limit: N` gives the N most *distant* estimates, not
the next N. Ask for more and take from the end.

### Annual and quarterly rows collapsed when I merged them

Two causes, both about the same underlying fact — an annual row and a Q4 row share a fiscal period end.

* **Analyst estimates**: the SDK stamps `Period` from the request precisely so the rows stay distinguishable.
  Do not discard it.
* **`enterprise-values`**: it sends no `fiscalYear` and no `period`, so a row genuinely cannot say which series it
  came from. `(symbol, date)` is **not** a unique key across both cadences — `2025-09-27` appears in Apple's
  annual series and its quarterly one. Stamp the cadence yourself.

### An ETF reports zero float

`freeFloat: 0` and `floatShares: 0` against a real `outstandingShares`, with a null `source` — SPY, QQQ, VOO and
IWM all do it. The zero means **"not computed for this security"**, not "no shares freely tradable". Do not feed
it into a float-based calculation.

### Financial scores do not reconcile with the balance sheet

They are not supposed to. `financial-scores` carries **no date, no period and no fiscal year**, yet it moves — the
figures are trailing/quote-time. Apple's `retainedEarnings` and `workingCapital` came back with the **opposite
sign** to the FY2025 balance sheet captured the same day.

The seven accompanying figures **do** reproduce the reported Altman Z exactly, which is what they are there for.
Do not try to reconcile the rest against `balance-sheet-statement`.

### `MarketCap` is empty on commodity or forex quotes

Correct behaviour. A market capitalisation is not a meaningful thing to ask for there.

### Summing market cap across the universe gives a strange number

A bulk profile's `currency` is **not always USD**, and its `country` tracks the **issuer**, not the venue — a TSX
listing reports `CAD` and `US` on the same row. You are summing mixed currencies. Filter on `exchange`, not
`country`, if you want a single-venue universe.

### The delisting archive seems to be a tenth of its real size

You trusted a `limit` above 100. **`delisted-companies` caps `limit` at 100 and does not say so** — `limit=1000`
and `limit=100` returned byte-identical bodies, so stepping `page` by your own larger limit reads a tenth of the
archive with HTTP 200 throughout.

`GetDelistedAsync` rejects a larger limit at the call site. If you are hitting the raw path through
`FmpTransport`, this trap is live again.

### Page 0 of the delisting archive contains future dates

Expected — the archive is newest-first, and the top row measured four months ahead of the call. Filter on the date
if you only want what has happened.

### The first page of a universe feed looks geographically skewed

Not a plan restriction, and not a sample. `shares-float-all` pages **are** symbol-ordered, so page 0 is entirely
Shenzhen listings. `profile-bulk` part 0 is **not** symbol-ordered at all. Neither first page samples the universe.

For the float universe, call `fmp.Company.StreamAllSharesFloatAsync(ct)` rather than paging it yourself.

### A `shares-float-all` walk never finishes, or stops half a universe short

Two different causes, both FMP silently ignoring what you asked for.

* **You asked for a `limit` above 5,000.** It is capped there and says nothing — `limit=10000` answers 5,000 rows
  with HTTP 200. Advance the page index by 10,000 after that and you read every *second* block of 5,000 symbols,
  then terminate cleanly on an empty page. `GetAllSharesFloatAsync` now rejects the oversized `limit` rather than
  letting FMP clamp it, so this surfaces as an `ArgumentOutOfRangeException` before a request is spent.
* **Your `limit` is small enough that the walk hits FMP's page ceiling.** The offset resolves as
  `min(page, 1000) × limit`, so pages 1000 and up all hand back page 1000's rows. At `limit=50` that stops the
  walk advancing at row 50,000 of 85,821 — the rest is unreachable and a loop that pages until it sees an empty
  list never ends. The ceiling is FMP-wide, not specific to this endpoint; `cik-list` behaves the same way.

`StreamAllSharesFloatAsync` avoids both: it asks for the 5,000 cap, which puts the ceiling at row 5,000,000, and
stops at the first short page.

---

## Throttling and timeouts

### `FmpRateLimitedException` under normal load

`PerMinuteCap` is set above what your tier actually allows, or **more than one process is sharing the key**. The
reservoir paces itself **per process** — two processes on one key emit at twice the rate the throttle was measured
to be safe at.

Set the cap to ~88% of your tier's published limit: `660` for Premium, `2640` for Ultimate.

### `FmpApiException` on a bulk call with a null `StatusCode`

You were **throttled**, not given empty data. Bulk reports throttling as HTTP 200 with a JSON error body, and a null
`StatusCode` is exactly that signal. Retry later — and read
**[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)**, because FMP warns it restricts keys for frequent bulk
abuse.

### `TimeoutException` on a bulk download

`BulkRequestTimeout` defaults to 10 minutes, which covers the measured payloads on a normal connection. If you are
hitting it, either the connection is slow or you are on `etf-holder-bulk`, whose single part is 298 MB. Raise the
option rather than retrying — a retry costs another bulk token and starts the transfer from zero.

### A bulk call blocks for ~30 seconds before starting

That is the reservoir, at two calls a minute. It is not a hang. Waiting on the throttle **does not consume** the
request timeout, which starts once the request actually goes out.

### Something is hanging forever with no exception

Check `PerMinuteCap` and `BulkPerMinuteCap` are not `0`. At `0` a bucket never refills and the first acquire
blocks indefinitely. Startup validation rejects this, so it can only happen if you bypassed `AddFmp`.

---

## Still stuck

* Confirm the endpoint is modelled at all — **[Endpoint Coverage](endpoint-coverage.md)**.
* Check whether the behaviour is already recorded in
  [upstream behaviour](../../README.md#upstream-behaviour-the-sdk-handles-for-you).
  Most surprises here are already written down there with the measurement that established them.
* Run the **[Live Smoke Suite](live-smoke-suite.md)** — it answers "is the SDK still reading the shape FMP is still
  sending".
* [Open an issue](https://github.com/jerbersoft/fmpdotnet/issues), including the endpoint, the exception type, and
  the `StatusCode` if there was one.
