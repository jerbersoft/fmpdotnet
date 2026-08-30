# Indexes and Market Hours — design

What issue [#38](https://github.com/jerbersoft/fmpdotnet/issues/38) builds: two new facades, `fmp.Indexes` and
`fmp.MarketHours`, covering all nine remaining Indexes and Market Hours paths. Coverage goes **207 → 216 of
243**.

Every fact this document argues from was measured on 2026-08-30 and is recorded, with its date, in
[the measurements](2026-08-30-indexes-and-market-hours-measurements.md) (committed `c308957`). Where this
document states a number, that file is where it came from. Nothing here was read from FMP's documentation.

## The shape of the problem

**Nine paths, four key tuples.** This is the opposite of [#34](https://github.com/jerbersoft/fmpdotnet/issues/34),
where nine paths produced nine tuples and there was no consolidation to argue for. Here the consolidation is
the design:

| record | serves | rows measured |
|---|---|---|
| `IndexConstituent` | `dowjones-`, `sp500-`, `nasdaq-constituent` | 635 |
| `IndexConstituentChange` | the three `historical-*-constituent` | 2,055 |
| `ExchangeMarketHours` | `all-exchange-market-hours`, `exchange-market-hours` | 81 |
| `ExchangeHoliday` | `holidays-by-exchange` | 446 |

The two market-hours paths are not merely similar: for each of seven exchanges cross-checked, the single row
from `exchange-market-hours?exchange=X` compared **equal, key for key and value for value**, to that
exchange's row inside the 81-row `all-exchange-market-hours` response. One record is not a simplification
there; it is what the wire sends.

The work is elsewhere. **Three of the four shapes carry keys absent from most rows**, and in two of them the
absent key changes the meaning of a key that is present:

| trap | measured behaviour | affected |
|---|---|---|
| a field that is not a date | `founded` is ISO on 30/30 Dow and 102/102 Nasdaq rows, a bare year on 477 of 503 S&P rows | 3 paths |
| a sentinel where a time belongs | `"CLOSED"` fills 124 of 176 hour slots | 2 paths |
| an optional pair carrying a whole session | `openingAdditional`/`closingAdditional` on 7 of 81 rows | 2 paths |
| an optional key that redefines a present one | `isFullyClosed` on 50 of 446 rows — exactly where `isClosed` is `null` | 1 path |
| two spellings of a time | `"09:30 AM +09:00"` against `"13:00"` | 2 records |
| two spellings of absent | `""` and `null`, path-dependently | 3 paths |
| two dates that disagree | `dateAdded` vs `date` on 205 of 2,055 rows, 202 by exactly one day | 3 paths |
| a default window with no future in it | the bare holiday call returned 67 rows, none dated after today | 1 path |
| a range that drops its own start date | the holiday window is `(from, to]`; a single-day range answers `[]` | 1 path |

Unlike #34, where every trap was a binding decision, **one of these is fixed by a signature** — the stale
holiday window, made a required argument, following the
[#32](https://github.com/jerbersoft/fmpdotnet/issues/32) precedent. The other eight land in two new
converters, one existing converter applied deliberately, and XML documentation that names what the wire
actually does. The half-open range is deliberately left alone; the reasoning is under
`GetHolidaysAsync` below.

### Four decisions were the user's and are settled

1. **Two facades**, `fmp.Indexes` and `fmp.MarketHours`, not one combined facade.
2. **The hour fields are parsed to `OffsetTime?`**, with a flag naming the `"CLOSED"` sentinel so that `null`
   is never ambiguous.
3. **The holiday closure pair is kept verbatim**, plus one derived predicate.
4. **`from` and `to` are required** on the holidays method.

## The public surface

### `fmp.Indexes` — six methods

| method | path | returns |
|---|---|---|
| `GetDowJonesConstituentsAsync(ct)` | `stable/dowjones-constituent` | `IReadOnlyList<IndexConstituent>` |
| `GetSp500ConstituentsAsync(ct)` | `stable/sp500-constituent` | `IReadOnlyList<IndexConstituent>` |
| `GetNasdaqConstituentsAsync(ct)` | `stable/nasdaq-constituent` | `IReadOnlyList<IndexConstituent>` |
| `GetDowJonesConstituentChangesAsync(ct)` | `stable/historical-dowjones-constituent` | `IReadOnlyList<IndexConstituentChange>` |
| `GetSp500ConstituentChangesAsync(ct)` | `stable/historical-sp500-constituent` | `IReadOnlyList<IndexConstituentChange>` |
| `GetNasdaqConstituentChangesAsync(ct)` | `stable/historical-nasdaq-constituent` | `IReadOnlyList<IndexConstituentChange>` |

**Every one of the six takes nothing but a `CancellationToken`, and that is a measurement, not an oversight.**
On all six paths, `limit`, `page`, `symbol` and an unknown `wibble=42` each returned a response
**byte-identical** to the bare request; on the three historical paths so did `from=2020-01-01&to=2026-12-31`.
There is no parameter to offer that FMP would honour. Adding one would be a signature that lies.

### `fmp.MarketHours` — three methods

| method | path | returns |
|---|---|---|
| `GetAllExchangesAsync(ct)` | `stable/all-exchange-market-hours` | `IReadOnlyList<ExchangeMarketHours>` |
| `GetExchangeAsync(exchange, ct)` | `stable/exchange-market-hours` | `ExchangeMarketHours?` |
| `GetHolidaysAsync(exchange, from, to, ct)` | `stable/holidays-by-exchange` | `IReadOnlyList<ExchangeHoliday>` |

### Why two facades and not one

The issue's title is a conjunction, and so would the facade's name be. The two groups share **no path
prefix, no parameter, no record and no concept**: nothing a caller learns from one applies to the other. The
endpoint inventory already counts them as two sections (Indexes 7 documented, Market Hours 3), and the README
coverage table is generated from the facades, so two facades keep that table honest.

This is the opposite call from #34, and the difference is real: #34's nine paths all took `symbol`, so one
facade was the thing they had in common. These nine have nothing in common but an issue number.

Cost: two sets of the five wiring edits rather than one, and the hard-coded facade count in `AddFmpTests`
moves **20 → 22**.

### Why the historical methods are named for changes

`GetSp500ConstituentChangesAsync`, not `GetHistoricalSp500ConstituentsAsync`. The repo usually mirrors FMP's
path name; here mirroring it would misdescribe the response twice over.

**A row is a change, not a constituent.** Measured across 2,055 rows, each row is *either* an addition
(`addedSecurity` populated, `removedTicker` empty) *or* a removal (`addedSecurity` is `""`,
`removedTicker` populated), and `symbol` names whichever it is. A removal row, verbatim:

```json
{"dateAdded": "June 24, 2024", "addedSecurity": "", "removedTicker": "RHI",
 "removedSecurity": "Robert Half", "date": "2024-06-24", "symbol": "RHI",
 "reason": "Market capitalization change."}
```

**And it is not a historical membership list.** Of the 628 current constituents carrying a `dateFirstAdded`,
**24 have no addition row at all** in the matching feed. A caller cannot reconstruct "who was in the S&P on
this date" from it, and a method called `GetHistoricalSp500ConstituentsAsync` invites exactly that attempt.
This follows the #34 precedent where `SearchFundsByNameAsync` was named for what it returns rather than for
the path it calls.

### `GetExchangeAsync` returns one record, not a list

All measured responses were single-element arrays. This follows `CompanyEndpoints.GetProfileAsync` and
`EtfAndFundsEndpoints.GetEtfInfoAsync`: take the first row, or `null` on an empty array.

**`null` was never observed and probably cannot happen**, and the documentation must say so rather than imply
a case the caller should handle. An unknown exchange is **HTTP 400 `Invalid Exchange Provided.`** — an
exception, not an empty list — so the empty array that would produce `null` has no measured cause. The
nullable return is honesty about what the deserialiser can promise, not a hint that emptiness is expected.

### `GetHolidaysAsync` takes a required range

```csharp
Task<IReadOnlyList<ExchangeHoliday>> GetHolidaysAsync(
    string exchange, LocalDate from, LocalDate to, CancellationToken ct = default);
```

This is the #32 precedent — a stale default window fixed by the signature — and here the default is worse
than stale. Measured across five exchanges, the bare call returned **67 rows, every one dated between
2025-08-30 and today, and not one dated after today**, while `from=1990-01-01&to=2035-12-31` returned 446 rows
reaching **2032-12-31**. The most natural question a caller has for this endpoint — *when is the market next
closed?* — is the one question its default answer can never answer.

Making the range required costs the caller one obvious line and removes a wrong answer that arrives with no
warning at HTTP 200.

`from`/`to` are honoured on **this path only**. On the three historical constituent paths they are accepted
and discarded, which is why those methods do not offer them.

**The window is half-open — `(from, to]` — and the SDK does not compensate for it.** Measured 2026-08-30
against NASDAQ's `2026-07-03` holiday: `from=2026-07-03&to=2026-07-03` returns `[]`, `from=2026-07-03&to=2026-07-04`
returns `[]`, and `from=2026-07-02&to=2026-07-03` returns the row. `to` is inclusive, `from` is not, and a
single-day range therefore always answers `[]` no matter what falls on that day.

Passing `from.PlusDays(-1)` upstream would make the signature behave the way a caller expects a date range to
behave, and it is deliberately **not** done: it would mean the request this SDK sends does not match the
arguments the caller passed, which turns every debugging session into a puzzle. The behaviour is documented on
the method instead, in the terms above, and pinned by a test.

> **Amended 2026-08-30, after the whole-branch review (finding 9).** The paragraph above settled *documented
> rather than compensated for*, and that still holds — no bound is rewritten on the way out. What it did not
> settle is the **degenerate** range, `from == to`, which spans no days and so can only answer `[]`. That is
> the same defect `DateRange.ThrowIfBackwards` exists to prevent — a wrong answer arriving at HTTP 200 in the
> shape of a right one, paid for out of the key's quota — so `GetHolidaysAsync` now rejects it with an
> `ArgumentOutOfRangeException` naming `from`, the bound the caller has to move. Pinned by
> `A_holiday_range_whose_bounds_are_equal_is_rejected_before_the_call`.
>
> The guard is **private to `MarketHoursEndpoints`, not added to `DateRange`.** The half-open window was
> measured on `holidays-by-exchange` and nowhere else; no other endpoint's `from` bound has ever been measured
> for inclusivity, and on the twenty-two other `ThrowIfBackwards` call sites an equal range is an ordinary
> single-day request. Two live probes prove it concretely: `Probe.Argument` hands both
> `CalendarEndpoints.GetEarningsCalendarAsync` and `EconomicsEndpoints.GetEconomicCalendarAsync` a
> `from` and `to` of `LiveApi.SettledWeekday` — equal on purpose, each narrowed to a day by an earlier
> slice's measurement. A shared guard would have thrown on both.

## The models

Four records, in `src/FmpDotNet/Models/`, one file each.

### Every property is nullable

The house rule, unchanged: the deserialiser cannot promise a key is present, so every bound property is
nullable regardless of what the corpus showed. Where no measured row omitted a key, the XML doc says so
explicitly — the nullability is about the deserialiser, not about the data.

### `IndexConstituent` — eight properties, and `founded` is a string

| property | wire | type |
|---|---|---|
| `Symbol` | `symbol` | `string?` |
| `Name` | `name` | `string?` |
| `Sector` | `sector` | `string?` |
| `SubSector` | `subSector` | `string?` |
| `Headquarters` | `headQuarter` | `string?` |
| `DateFirstAdded` | `dateFirstAdded` | `LocalDate?` |
| `Cik` | `cik` | `string?` |
| `Founded` | `founded` | **`string?`** |

**`Founded` is a `string` and this is the design's most consequential binding decision.** Measured across 635
rows:

| path | ISO `uuuu-MM-dd` | bare year `uuuu` | other |
|---|---|---|---|
| `dowjones-constituent` | **30 of 30** | 0 | 0 |
| `nasdaq-constituent` | **102 of 102** | 0 | 0 |
| `sp500-constituent` | 23 of 503 | **477 of 503** | **3** |

An implementer who models this field from the Dow Jones response — 30 rows, 100% ISO — types it `LocalDate?`
and is correct on 155 of 635 rows. On `sp500-constituent` that binding silently drops **95.4%** of the values,
because `NullableLocalDateJsonConverter` returns `null` on an unparseable string rather than throwing. The
loss would not surface as an error anywhere.

The three "other" values are not malformed dates. They are multi-valued company history — `KLAC` sends
`1975/1977`, `LOW` sends `1904/1946/1959`, `NSC` sends `1881/1894`. There is no date in that field to parse,
on any path.

`DateFirstAdded` **is** a real date: ISO on all 628 non-null values, with no other pattern. It takes
`NullableLocalDateJsonConverter`. It is `null` on 7 of 102 Nasdaq rows (ADBE, AMAT, CSCO, FAST, MSFT, PAYX,
QCOM) and never null on the other two paths.

`Sector` is a `string`, not the `Sector` enum. All 11 distinct values measured fall inside the enum and none
outside it — but that enum exists to build a `sector=` **query** value, and every other response record in
this SDK (`CompanyProfile`, `CotReport`, `EsgData`, `DirectoryNames`) binds `sector` as a string. Nothing
measured says what happens when FMP adds a twelfth sector, and a response-side enum would turn that into a
deserialisation failure. `SubSector` has 114 distinct values and is free text by any reading.

### `IndexConstituentChange` — seven properties, two of which are dates that disagree

| property | wire | type |
|---|---|---|
| `DateAdded` | `dateAdded` | `LocalDate?` (new converter) |
| `AddedSecurity` | `addedSecurity` | `string?` (sentinel converter) |
| `RemovedTicker` | `removedTicker` | `string?` (sentinel converter) |
| `RemovedSecurity` | `removedSecurity` | `string?` (sentinel converter) |
| `Date` | `date` | `LocalDate?` |
| `Symbol` | `symbol` | `string?` |
| `Reason` | `reason` | `string?` (sentinel converter) |

**Both dates are surfaced, and neither is derived from the other.** They disagree on 205 of 2,055 rows —
202 by exactly one day with `Date` the earlier, plus three larger outliers. The disagreement is not a legacy
artifact: 151 of the 205 come from a single 1957 backfill, but **40 fall in 2024–2026 against 47 agreeing rows
in the same span**, so in recent data the two fields differ on 46% of rows.

The 1957 rows settle the question of whether they are one value rendered twice: 151 rows say
`"March 04, 1957"` / `1957-03-03` while 54 rows with the **identical** `dateAdded` say `1957-03-04`. Deriving
either field from the other is wrong on 205 measured rows, so the record carries both and the documentation
names the discrepancy rather than hiding it.

The four text fields take `SentinelStringJsonConverter` — see below.

### `ExchangeMarketHours` — the sentinel, the lunch break, and how a parsed time keeps its meaning

| property | wire | type |
|---|---|---|
| `Exchange` | `exchange` | `string?` |
| `Name` | `name` | `string?` |
| `OpeningHourText` | `openingHour` | `string?` |
| `ClosingHourText` | `closingHour` | `string?` |
| `OpeningAdditionalText` | `openingAdditional` | `string?` |
| `ClosingAdditionalText` | `closingAdditional` | `string?` |
| `Timezone` | `timezone` | `string?` |
| `IsMarketOpen` | `isMarketOpen` | `bool?` |

plus five computed, `[JsonIgnore]` members:

```csharp
public OffsetTime? OpeningHour        => ParseHour(OpeningHourText);
public OffsetTime? ClosingHour        => ParseHour(ClosingHourText);
public OffsetTime? OpeningAdditional  => ParseHour(OpeningAdditionalText);
public OffsetTime? ClosingAdditional  => ParseHour(ClosingAdditionalText);

/// <summary>The exchange is not trading on its own local date — the wire sent the literal
/// <c>"CLOSED"</c> rather than a time.</summary>
public bool IsClosedToday => OpeningHourText is "CLOSED";
```

**Why the raw text is the bound property and the `OffsetTime` is computed.** The decision taken was "parsed,
plus a flag that says why `null` is `null`". A converter cannot deliver that: a `JsonConverter<OffsetTime?>`
sees one field and can set one property, so nothing could populate `IsClosedToday` — and two properties
cannot share one `[JsonPropertyName]`. Binding the text and computing the rest is the only shape that gives
the caller a real time type *and* keeps `"CLOSED"` distinguishable from "FMP sent something we could not
parse". It also preserves the wire exactly, which is the house rule.

`ParseHour` is a private static helper over
`OffsetTimePattern.CreateWithInvariantCulture("hh:mm tt o<m>")`, returning `null` on failure. Verified against
NodaTime 3.2.2 on 2026-08-30: it parses every measured form, formats back **byte-identically** (`+09:00`, not
`Z` and not `+09`), handles noon and midnight correctly (`12:00 PM` → 12:00, `12:00 AM` → 00:00), and fails
cleanly on `"CLOSED"` and `""`. It also parses the negative-offset form that no capture contained — see
*What is documented rather than guarded*.

**`OpeningAdditional` and `ClosingAdditional` are the afternoon session, and dropping them loses a market.**
The keys were present on 7 of 81 rows and absent from 74. All seven are exchanges that break for lunch:

| exchange | morning | afternoon |
|---|---|---|
| SET (Bangkok) | 10:00 AM – 12:30 PM +07:00 | 02:00 PM – 04:40 PM +07:00 |
| JKT (Jakarta) | 09:30 AM – 11:30 AM +07:00 | 01:30 PM – 03:00 PM +07:00 |
| JPX (Tokyo) | 09:00 AM – 11:30 AM +09:00 | 12:30 PM – 03:30 PM +09:00 |
| SHH (Shanghai) | 09:30 AM – 11:30 AM +08:00 | 01:00 PM – 03:00 PM +08:00 |
| SHZ (Shenzhen) | 09:30 AM – 11:30 AM +08:00 | 01:00 PM – 03:00 PM +08:00 |
| SES (Singapore) | 09:00 AM – 12:00 PM +08:00 | 01:00 PM – 05:00 PM +08:00 |
| HOSE (Ho Chi Minh) | 09:15 AM – 11:30 AM +07:00 | 01:00 PM – 02:30 PM +07:00 |

A record built from the response's first row — ASX, six keys — reports Tokyo closing at 11:30 AM. The
properties are absent from most rows and that is normal, not missing data; the XML doc says which seven
exchanges populated them and why.

`Timezone` stays a `string`. All 81 values resolved as IANA zone identifiers (52 distinct) with no
abbreviation and no fixed offset among them, so the caller can hand it straight to
`DateTimeZoneProviders.Tzdb`; the record does not do that for them, because resolving a zone is a decision
about which tzdb version to trust and that belongs to the application.

### `ExchangeHoliday` — a boolean that is never `false`

| property | wire | type |
|---|---|---|
| `Exchange` | `exchange` | `string?` |
| `Date` | `date` | `LocalDate?` |
| `Name` | `name` | `string?` |
| `IsClosed` | `isClosed` | `bool?` |
| `AdjustedOpenTime` | `adjOpenTime` | `LocalTime?` (new converter) |
| `AdjustedCloseTime` | `adjCloseTime` | `LocalTime?` (new converter) |
| `IsFullyClosed` | `isFullyClosed` | `bool?` |

plus one computed, `[JsonIgnore]` member:

```csharp
/// <summary>The exchange traded a shortened session that day rather than closing.</summary>
public bool ClosesEarly => AdjustedCloseTime is not null;
```

Measured across 446 rows, the two states are exact complements:

| | count |
|---|---|
| `isClosed: true`, `isFullyClosed` **absent**, no adjusted time | 396 |
| `isClosed: null`, `isFullyClosed: false`, `adjCloseTime` set | 50 |
| `isClosed: false` | **0** |

So `IsClosed` alone cannot answer "is the exchange closed that day?": `null` means *an early close*, not
*unknown*, and a caller who reads it as "unknown" will treat 50 rows as unanswerable. `ClosesEarly` is derived
from `AdjustedCloseTime` because that is the field whose presence carries the information; both candidate
signals — `AdjustedCloseTime is not null` and `IsFullyClosed == false` — selected the identical 50 rows across
all 446, and the chosen one does not depend on a key that is absent from 89% of rows.

The wire pair is kept verbatim beside it. `isClosed: false` has never been observed, and an enum collapsing
the two states would have nowhere to put it if it appeared.

`AdjustedOpenTime` was **`null` on all 446 rows** — never once populated. It is modelled because the key is
always present, and documented as never observed carrying a value.

## Two new converters, and one existing one applied

### 1. `LongFormLocalDateJsonConverter` : `JsonConverter<LocalDate?>`

For `IndexConstituentChange.DateAdded`, which is US long form — `"June 29, 2026"` — and not ISO. All **2,055
of 2,055** measured values parsed with `LocalDatePattern.CreateWithInvariantCulture("MMMM d, yyyy")`, verified
against NodaTime 3.2.2 on 2026-08-30. Invariant culture is load-bearing: the month names are English, and a
machine running under a non-English culture would fail every row without it. `null` on an unparseable value,
like the rest of the file.

**`Write` cannot round-trip, and the tests must not assume it does.** The wire uses both paddings — **215
values carry a zero-padded single-digit day** (`"August 05, 2026"`) and **532 carry an unpadded one**
(`"November 8, 2024"`). No single NodaTime pattern emits both, so `Write` emits `MMMM d, yyyy` and a
zero-padded input comes back unpadded. This is documented on the converter and is the reason
`DateAdded`'s guard test asserts the **parsed value**, not a serialisation round-trip.

### 2. `LocalTimeJsonConverter` : `JsonConverter<LocalTime?>`

For `ExchangeHoliday.AdjustedOpenTime` and `.AdjustedCloseTime`. The file has no `LocalTime` converter today.
All 50 measured non-null values matched `HH:mm` — 49× `"13:00"` and one `"13:30"` on 2015-11-27 — parsed with
`LocalTimePattern.CreateWithInvariantCulture("HH:mm")`, verified on 2026-08-30. This pattern round-trips
exactly, so this converter's guard test may assert the serialised form.

**The value carries no offset and the response carries no zone**, which is the sharper half of this slice's
two-time-spellings trap: `"13:00"` beside `ExchangeMarketHours`'s `"09:30 AM +09:00"`. `holidays-by-exchange`
has no `timezone` key at all — verified absent on all 446 rows — so the zone must come from the matching
`ExchangeMarketHours.Timezone`. The XML doc says so and names the path to fetch.

### 3. `SentinelStringJsonConverter`, applied to four fields

Existing, unchanged. Applied to `IndexConstituentChange`'s `AddedSecurity`, `RemovedTicker`, `RemovedSecurity`
and `Reason`, which spell absence **two ways, path-dependently**:

| path | `""` | `null` |
|---|---|---|
| `historical-dowjones-constituent` | 136 | **0** |
| `historical-sp500-constituent` | 823 | 20 |
| `historical-nasdaq-constituent` | 83 | 8 |

`historical-dowjones-constituent` uses only `""` across all 86 rows, so an implementer who tests against the
Dow Jones path alone never meets the `null` spelling. Folding both to `null` is the point of applying the
converter, and the choice is recorded here so it reads as measured rather than habitual.

### Why the hour fields get no converter

Covered above under `ExchangeMarketHours`: the sentinel flag needs the raw text, and a converter cannot both
return a parsed time and set a second property.

## Guards

Two, both narrow, both justified by a measured silent-wrong-answer.

**`ThrowIfNotOneExchange(exchange)`** on `GetExchangeAsync` and `GetHolidaysAsync`. Rejects null, whitespace
and a comma. Measured 2026-08-30, `exchange=NASDAQ,NYSE` returns **HTTP 400 `Invalid Exchange Provided.`** —
so unlike #34's comma case this is *already* an error and not a silent empty list. The guard is still worth
having: it turns a wasted call against the key's quota into an `ArgumentException` that names the fix, and it
matches `ThrowIfNotOneSymbol`'s established shape. The message must not claim the wire answers silently — it
does not.

**`DateRange.ThrowIfBackwards(from, to)`** on `GetHolidaysAsync`. The existing shared guard. Measured
2026-08-30, a reversed range returns `[]` with HTTP 200 — "no holidays in that window", indistinguishable from
a genuinely quiet range. This is exactly the case `DateRange` was extracted for.

**No guard on the exchange's spelling.** `exchange=ZZZZ` is an HTTP 400 and the exchange vocabulary is 81
codes that will change; validating it client-side would go stale. The XML doc points at
`DirectoryEndpoints.GetExchangesAsync` and records the cross-check: all **63** codes that path returned on
2026-08-30 appear in `all-exchange-market-hours`, which carries 18 more. Anything from that path is safe here.

## What is documented rather than guarded

**The exchange code is case-insensitive.** `exchange=nasdaq` returned a **byte-identical** response to
`exchange=NASDAQ` on both paths. The exchange's *name* is not accepted:
`exchange=NASDAQ%20Global%20Market` is an HTTP 400.

**Nothing paginates.** `limit` and `page` are ignored on all nine paths — byte-identical responses. The
largest is `historical-sp500-constituent` at 1,525 rows and 365,284 bytes. Two facades' worth of methods
return the complete set every time, and the XML doc says so on each.

**`"CLOSED"` tracks the exchange's own local calendar day, not UTC.** Resolving each row's `timezone` against
the capture's HTTP `Date` header (`Sun, 30 Aug 2026 17:58:46 GMT`), 61 of the 62 closures were local weekends,
and the four exchanges showing hours on a local weekend were exactly the Gulf markets EGX, DOH, KUW and SAU,
whose Sunday is a trading day. The single weekday closure, KLS on its local Monday 2026-08-31, is corroborated
by `holidays-by-exchange` naming that date `"National Day"` with `isClosed: true`. This is what
`IsClosedToday` means, and the doc says it in those terms — a caller must not read it as "the market is not
open right now", which is `IsMarketOpen`.

**The historical feed is not a membership ledger.** 24 of 628 current constituents have no addition row.
Documented on the three `…ConstituentChangesAsync` methods.

**Row counts are not company counts.** `sp500-constituent` returns 503 rows over 500 distinct CIKs — FOX/FOXA,
NWS/NWSA and GOOGL/GOOG — and `nasdaq-constituent` 102 rows over 101. Every `name` is distinct too, so neither
`name` nor `symbol` identifies a company.

**A bad date in `from` is silently ignored** by the upstream — `from=nonsense` returned a response identical to
omitting `from`. This SDK never sends one, because `from` is a `LocalDate`; the behaviour is recorded because
it explains why the required-range signature matters.

**`isMarketOpen` was `false` on all 81 rows, on every capture, and the `true` case is unmeasured.** Every
capture was taken on Sunday 2026-08-30. The field's *type* is measured — a JSON boolean on all 81 rows — and
nothing else about it is. The doc states this rather than describing behaviour nobody observed. For the same
reason, **every observed UTC offset in an hour string was positive** (`+03:00` to `+12:00`): only Asia-Pacific
and Gulf exchanges were on a trading day. `ParseHour` was verified against `-05:00` and `-04:00` inputs
directly, so the negative form is covered by test rather than by capture, and the doc says which.

Both gaps close with three calls on any weekday. **They should be closed before this design is implemented**,
and the measurements file names them as the open items.

## Serialisation and wiring

**Four** new entries in `FmpJsonContext`, not nine. Following the existing pattern for single-record
returns, `List<ExchangeMarketHours>` serves `GetExchangeAsync` as well as `GetAllExchangesAsync`:

```csharp
[JsonSerializable(typeof(List<IndexConstituent>))]
[JsonSerializable(typeof(List<IndexConstituentChange>))]
[JsonSerializable(typeof(List<ExchangeMarketHours>))]
[JsonSerializable(typeof(List<ExchangeHoliday>))]
```

The records are shared, so the context entries are too. This is the consolidation paying off a second time.

**Adding a facade is five edits, and there are two facades.** Per facade: the `FmpClient` constructor
parameter, the `FmpClient` property, the DI `TryAddTransient`, the hard-coded count in `AddFmpTests` and an
`Assert.NotNull(client.X)`. The count moves **20 → 22** in one edit, not two.

`IsAotCompatible` is declared on `src/FmpDotNet` only. The computed properties are `[JsonIgnore]` so the
source generator does not try to write them, and no reflection-based path is introduced.

## Testing

Unit tests in one new file per facade, `IndexesTests.cs` and `MarketHoursTests.cs`, against fixtures under
`tests/FmpDotNet.Tests/Fixtures/`. Every fixture row is copied from a real capture in the measurement corpus —
none is invented.

**Every trap in the table above gets a test that fails when the trap is reintroduced**, and each test must be
shown to fail against the pre-fix code. The four defects the #34 review found were all guard tests that could
not fail — a test named for a converter, fed a value that converter does not transform. The falsifiability
check is mandatory here:

| test | reintroduces | must fail when |
|---|---|---|
| `Founded_is_a_string_because_the_sp500_sends_bare_years` | `LocalDate?` on `Founded` | fed `"2012"` and `"1904/1946/1959"` |
| `A_closed_exchange_parses_no_hours_and_says_why` | dropping `IsClosedToday` | fed `"CLOSED"` |
| `The_lunch_break_exchanges_keep_their_afternoon_session` | six-key record | fed JPX's eight-key row |
| `An_early_close_is_not_a_closure` | `bool IsClosed` | fed the `isClosed: null` + `isFullyClosed: false` row |
| `dateAdded_and_date_are_read_separately` | deriving one from the other | fed a row where they differ by a day |
| `The_dow_jones_feed_spells_absence_with_an_empty_string` | dropping the sentinel converter | fed `""` |
| `A_long_form_date_binds_under_any_culture` | culture-sensitive parsing | run under a non-English culture |
| `A_negative_offset_hour_parses` | an offset-blind pattern | fed `"09:30 AM -05:00"` |
| `The_holiday_range_excludes_its_own_from_date` | assuming an inclusive `from` | asserting a single-day range is empty |

The `Founded` test is the one most likely to be written unfalsifiably: fed only `"1998-09-04"` it passes
against a `LocalDate?` binding too. It must be fed a bare year.

Smoke tests follow the existing `LiveApi`/`Probe` pattern, one per path, skipped without a key.

**`Probe.Argument` already has an `exchange` arm**, returning `LiveApi.Exchange` (`"NASDAQ"`), and NASDAQ
answers 200 on both market-hours paths — so no new string arm is needed. What *is* needed is a **named date
range for `GetHolidaysAsync`**, following the `LiveApi.IndicatorRangeStart` precedent. The generic `LocalDate`
arm would give `RangeStart`..`SettledWeekday`, a ninety-day trailing window; measured against the corpus, that
window (2026-05-23 .. 2026-08-21) holds **3** NASDAQ holidays, and a quiet quarter would take it to zero — the
silent-green failure `RangeStart`'s own doc was written about. A fixed `2024-01-01`..`2026-12-31` returns
**38**. Add `LiveApi.HolidayRangeStart`/`HolidayRangeEnd` and two arms narrowed by
`parameter.Member.DeclaringType == typeof(Endpoints.MarketHoursEndpoints)`.

`FMPDOTNET_SMOKE_BULK` is not involved and must not be set.

`SweepCoverageTests` and `baseline-ordinary.txt` gain nine blocks, expected `outcome rows`.

## Documentation deliverables

- XML documentation on every public member, carrying the measurement and its date, per the house rule that
  documentation is a deliverable.
- README coverage table regenerated by `FMPDOTNET_UPDATE_README=1 dotnet test` — nine rows, **207 → 216**.
- Issue #38 closed with a house-style comment; epic #25 edited to move #38 into Shipped and correct the
  remainder arithmetic (three open children, 27 paths, 20 actionable).

## Files

**Create (8 + fixtures)**

| file | holds |
|---|---|
| `src/FmpDotNet/Models/IndexConstituent.cs` | 8 properties |
| `src/FmpDotNet/Models/IndexConstituentChange.cs` | 7 properties |
| `src/FmpDotNet/Models/ExchangeMarketHours.cs` | 8 bound + 5 computed |
| `src/FmpDotNet/Models/ExchangeHoliday.cs` | 7 bound + 1 computed |
| `src/FmpDotNet/Endpoints/IndexesEndpoints.cs` | 6 methods |
| `src/FmpDotNet/Endpoints/MarketHoursEndpoints.cs` | 3 methods + 1 guard |
| `tests/FmpDotNet.Tests/IndexesTests.cs`, `MarketHoursTests.cs` | the tests above |

**Modify (10)**

`NodaConverters.cs` (two new converters), `FmpJsonContext.cs` (four entries), `FmpClient.cs` (two facades),
`DependencyInjection/FmpServiceCollectionExtensions.cs` (two registrations), `AddFmpTests.cs` (count 20 → 22,
two assertions), `LiveApi.cs` (the holiday range constants), `Probe.cs` (two `LocalDate` arms),
`SweepCoverageTests.cs`, `baseline-ordinary.txt`, `README.md` (regenerated).

## What this design does not do

- **It does not validate exchange codes.** The vocabulary is upstream's and will change.
- **It does not resolve `Timezone` to a `DateTimeZone`.** Which tzdb to trust is the application's decision.
- **It does not reconstruct index membership at a date.** The change feed cannot support it — 24 current
  constituents have no addition row — and offering a method that appeared to would be the worst outcome here.
- **It does not model `founded` as structured data.** Three values are multi-valued company history; there is
  nothing to parse.
- **It does not add a `Sector` enum binding on the response side.** Measured agreement on 635 rows is not a
  guarantee about the 636th.
