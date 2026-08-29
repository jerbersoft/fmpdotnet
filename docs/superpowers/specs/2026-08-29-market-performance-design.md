# Market Performance — design

What issue [#32](https://github.com/jerbersoft/fmpdotnet/issues/32) builds: a new `fmp.MarketPerformance`
facade covering all eleven documented Market Performance paths.

Every fact this document argues from was measured on 2026-08-29 and is recorded, with its date, in
[the measurements](2026-08-29-market-performance-measurements.md) (committed `199d973`). Where this document
states a number, that file is where it came from. Nothing here was read from FMP's documentation.

## The shape of the problem

Eleven paths, three row shapes, and **eight paths that can answer a question the caller did not ask** — at
HTTP 200, with a well-formed body and no marker anywhere in it.

| trap | measured behaviour | affected paths |
|---|---|---|
| stale default window | omitting `from`/`to` returns 2024-02-01 … 2024-03-01 | the 4 historical |
| single-exchange default | omitting `exchange` returns NASDAQ alone | all 8 sector/industry |
| ragged snapshot | a date past the data returns rows bearing three different dates | the 4 snapshot |

The design's central claim is that **the SDK's job here is to make the caller state what FMP would otherwise
assume.** Two of the three traps disappear entirely if the parameters that trigger them cannot be omitted.
The third cannot be fixed by a signature and is documented and pinned by a test instead.

## The public surface

One facade, eleven methods, one per path. No indirection: `TechnicalIndicators` collapsed nine paths into one
method because the nine differed only by a path segment and shared a row shape. That is not this group. The
movers take no parameters at all, the snapshots take a day, and the historical paths take a range — three
genuinely different call shapes, and an enum selecting between them would add a concept without removing one.

```csharp
public sealed class MarketPerformanceEndpoints(FmpTransport transport)
{
    // Movers — market-wide, fixed at 50 rows, no parameters accepted.
    public Task<IReadOnlyList<MarketMover>> GetBiggestGainersAsync(CancellationToken ct = default);
    public Task<IReadOnlyList<MarketMover>> GetBiggestLosersAsync(CancellationToken ct = default);
    public Task<IReadOnlyList<MarketMover>> GetMostActivesAsync(CancellationToken ct = default);

    // Snapshots — one day, one exchange, optionally one sector or industry.
    public Task<IReadOnlyList<SectorPerformance>> GetSectorPerformanceSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default);
    public Task<IReadOnlyList<SectorPe>> GetSectorPeSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default);
    public Task<IReadOnlyList<IndustryPerformance>> GetIndustryPerformanceSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default);
    public Task<IReadOnlyList<IndustryPe>> GetIndustryPeSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default);

    // Historical — subject, scope, then range. Every argument required.
    public Task<IReadOnlyList<SectorPerformance>> GetHistoricalSectorPerformanceAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default);
    public Task<IReadOnlyList<SectorPe>> GetHistoricalSectorPeAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default);
    public Task<IReadOnlyList<IndustryPerformance>> GetHistoricalIndustryPerformanceAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default);
    public Task<IReadOnlyList<IndustryPe>> GetHistoricalIndustryPeAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default);
}
```

Parameter order on the historical four is subject, scope, range — what, where, when. It groups the two
arguments that identify a series before the two that bound it.

### There is no market-wide sector view, and the signature says so

`exchange` selects one exchange. No "all exchanges" value appeared among those measured, and an unrecognised
value answers `[]` rather than widening. A caller who wants the whole market loops over
`Directory.GetExchangesAsync`. The three movers lists are the only market-wide thing in this group.

This is worth stating plainly because the alternative — an optional `exchange` defaulting to NASDAQ — reads
like a market-wide answer and is not one. Measured on the same day and sector, the default and `exchange=NYSE`
disagreed on **all 20 shared dates**: Technology on 2026-08-28 is `−0.6192` on NASDAQ and `−1.7398` on NYSE.

## The models

Five records, one per wire shape. Every property is named for the exact key it binds, and the binding is pure
source-generated `System.Text.Json` — no custom converter anywhere in this slice.

```csharp
// src/FmpDotNet/Models/MarketMover.cs
public sealed record MarketMover
{
    [JsonPropertyName("symbol")]            public string?  Symbol { get; init; }
    [JsonPropertyName("name")]              public string?  Name { get; init; }
    [JsonPropertyName("price")]             public decimal? Price { get; init; }
    [JsonPropertyName("change")]            public decimal? Change { get; init; }
    [JsonPropertyName("changesPercentage")] public decimal? ChangePercentage { get; init; }
    [JsonPropertyName("exchange")]          public string?  Exchange { get; init; }
}

// src/FmpDotNet/Models/SectorIndustryMetrics.cs — all four in one file
public sealed record SectorPerformance
{
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")]          public LocalDate? Date { get; init; }
    [JsonPropertyName("sector")]        public string?    Sector { get; init; }
    [JsonPropertyName("exchange")]      public string?    Exchange { get; init; }
    [JsonPropertyName("averageChange")] public decimal?   AverageChange { get; init; }
}

public sealed record IndustryPerformance   // `industry` in place of `sector`
public sealed record SectorPe              // `pe` in place of `averageChange`
public sealed record IndustryPe            // both swaps
```

### Why one file for the four

The cost of five literal records is four types that differ by one word and must be kept in step. Four files
hide that; one file makes drift visible to anyone editing any of them. `DirectoryNames.cs` already holds four
related row types for the same reason.

### Why every property is nullable

Because the deserialiser cannot promise a key is present — **not** because a null was observed. Zero nulls
appeared across 9,855 rows and 39,523 field slots. The doc comments must say that explicitly, in the wording
`SectorName` already uses, so a reader does not infer that FMP omits these keys.

### Why `decimal` and not `double`

The metrics arrive as unrounded float64 expansions, not as the two- and four-decimal figures the price
endpoints return. The longest plain fractional part measured was **22 digits** —
`-0.0000026524148173594842`, 17 significant. A unit test pins the exact literal, and it is the test that fails
first if anyone retypes these properties: it stops compiling if the property becomes `double`.

**Ten values arrive in scientific notation, and the SDK still needs no converter.** Every value whose absolute
magnitude is below `1e-6` is written in exponent form — `5.735079118365113e-7` and nine others, all in the
4,025-row deep-history capture. Verified on .NET 10 with the source generator and this SDK's own
`[JsonSourceGenerationOptions]`: `System.Text.Json` binds exponent form to `decimal?` unaided, and
`-2.6524148173594842e-06` and `-0.0000026524148173594842` deserialise to equal values. This was verified
rather than assumed, because the whole no-converter decision rests on it. A fixture carries one of the ten so
the next person to touch the numeric typing cannot break it silently.

### `ChangePercentage` is the third spelling of one concept

FMP spells this fact three different ways in three endpoint groups, and this SDK already normalises two of
them:

| endpoint group | wire key | C# property |
|---|---|---|
| `quote` | `changePercentage` | `Quote.ChangePercentage` |
| end-of-day | `changePercent` | `EndOfDayBar.ChangePercent` |
| **movers** | **`changesPercentage`** | **`MarketMover.ChangePercentage`** |

`EndOfDayBar` already documents its divergence and keeps its own wire spelling. Here the wire's spelling would
read as a typo in C#, so the property takes `Quote`'s spelling instead, under the same rule that binds
`senateID` to `SenateId`. The attribute carries the wire verbatim. **Do not "fix" the attribute** — the property
would bind nothing, silently.

The divergence was found by cross-check, not by reading: `biggest-gainers` row `FNGR` read `price 0.398,
change 0.2246, changesPercentage 129.5271`, and `stable/quote?symbol=FNGR` returned those three values
identically.

### The movers carry no date

Neither `date` nor `timestamp` appears in the movers shape. The lists describe a session and never name it.
The same cross-check dated it: `quote` returned `timestamp 1787947201` — `2026-08-28 20:00:01Z`, the close of
the last completed session before the measurement. The method docs say the lists are the latest session and
that the SDK cannot tell the caller which one; `Quote` is where that answer lives.

## The `Sector` enum

A new top-level enum beside `ChartInterval`, `FiscalPeriod`, `EconomicIndicator` and `TechnicalIndicator`.

```csharp
public enum Sector
{
    BasicMaterials, CommunicationServices, ConsumerCyclical, ConsumerDefensive, Energy,
    FinancialServices, Healthcare, Industrials, RealEstate, Technology, Utilities,
}

public static class SectorExtensions
{
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this Sector sector) => sector switch
    {
        Sector.BasicMaterials => "Basic Materials",
        // … the wire label for each, verbatim
        _ => throw new ArgumentOutOfRangeException(nameof(sector), sector, "Not a known sector."),
    };
}
```

**Why sector is an enum and industry is not.** The asymmetry is the measurement, not a preference.
`available-sectors` returned 11 names, and every unfiltered sector snapshot taken — eight of them, across five
dates and three exchanges — carried exactly those 11, no more and no fewer. `available-industries` returned
159, of which only **139** appear in any snapshot on either NASDAQ or NYSE. Twenty documented industries —
`Banks`, `Asset Management`, `Environmental Services`, `Silver`, `Media & Entertainment` among them — return
`[]` everywhere. An enum whose members are one-in-eight measured to fail silently is worse than a string: it
would promise a validity it cannot deliver.

**What the enum buys, and what it does not.** It buys typo-safety: an unrecognised `sector` answers `[]` at
HTTP 200, so `sector=Technlogy` is otherwise indistinguishable from a quiet day. It does **not** buy
casing-safety, and the docs must not imply it does — `sector=technology` returned a response byte-identical to
`sector=Technology`. This is the opposite of `EconomicIndicator`, where `GDP` works and `gdp` does not, and
the contrast belongs in the doc comment so the two enums are not read as carrying the same guarantee.

**The response property stays `string?`.** Binding the wire label onto the enum would need a converter, and an
unknown label would then throw where it currently binds. The enum is an argument type only — the split
`TechnicalIndicator` already uses for path selection.

## The three traps, and what the design does about each

### 1. The stale default window — fixed by the signature

Omitting `from`/`to` returns 21 rows spanning 2024-02-01 … 2024-03-01, thirty months before the measurement.
Both bounds were measured independently rather than inferred: `to=2026-08-28` alone backfills `from` to
2024-02-01 (665 rows), and `from=2024-02-20` alone returns 9 rows ending at 2024-03-01. `limit=100` does not
move it.

**`from` and `to` are required, non-nullable `LocalDate`.** Fifteen existing public methods already require a
range — six on `CalendarEndpoints`, five on `ChartEndpoints`, three on `SecFilingsEndpoints`, one on
`EconomicsEndpoints`. Offering an optional
parameter whose omission is measured to produce a two-and-a-half-year-old answer is offering a trap with a
default value.

*Cost if wrong:* a caller who genuinely wanted FMP's default window must now name it. That is two arguments
against a silently stale answer, and the stale answer is not recoverable after the fact — nothing in the body
says which window it is.

### 2. The single-exchange default — fixed by the signature

**`exchange` is required and non-nullable, typed `string`.** Not an enum: only `NASDAQ`, `NYSE` and `AMEX`
were verified to return rows, `Directory.GetExchangesAsync` already ships FMP's real list, and an enum built
from three verified values would be a guess wearing a type. `QuoteEndpoints.GetExchangeQuotesAsync(string
exchange, …)` is the precedent.

`exchange` is case-insensitive upstream — `exchange=nasdaq` returned a response byte-identical to the default.
An unrecognised value answers `[]` at HTTP 200 rather than an error, which is why the parameter being required
matters more than it being validated: the SDK cannot check it, so the caller must at least have chosen it.

### 3. The ragged snapshot — documented and pinned, not guarded

Asked for a date past the end of the data, the snapshots return a full row set whose rows **do not share a
date**. `date=2026-09-01`, measured 2026-08-29:

| sector | date on the row |
|---|---|
| Basic Materials, Communication Services, Consumer Defensive, Energy, Financial Services, Healthcare, Technology, Utilities | 2026-08-28 |
| Consumer Cyclical | 2026-08-27 |
| Industrials, Real Estate | **2026-08-25** |

`date=2027-01-04` produced that split sector for sector, identically, and `sector-pe-snapshot` did too. Three
requests, two future dates, two metrics, one identical assignment — systematic, not a one-off.

**The fallback is not "each sector's latest row."** Asked for 2026-08-28 directly, Industrials and Real Estate
return rows dated 2026-08-28. The future-date response gave those two their 2026-08-25 values instead, matching
`historical-sector-performance` for that date exactly. The values are real and the dates are honest; the row
set is simply not a coherent day.

**The SDK returns the rows exactly as measured.** Three alternatives were considered and rejected:

- *A transparent result wrapper*, as `EarningsCalendarResult` does for the earnings calendar's silent 4000-row
  cap. Rejected on scope: that trap fires on ordinary, correct requests and cannot be avoided, whereas this one
  fires only on a date past the data. Four wrapper types for an avoidable case is the wrong trade.
- *An opt-in clamp*, as `clampToRange` does. Rejected because it is lossy in the same way that flag's own
  documentation warns about: it would delete real rows and return an 8-row "snapshot".
- *A future-date guard.* Rejected on dependency cost: the SDK has no clock — no `IClock`, no `SystemClock`, no
  `TimeProvider` anywhere in `src/` — and adding one threads a new dependency through DI and every facade
  registration (eighteen today, nineteen after this slice) to catch a case the payload already reports.

`date` is on every row. The method doc states the trap with the measured example above and tells the caller to
compare. **A unit test pins it**: a fixture of the eleven ragged rows, asserting the SDK hands back all eleven
unmodified with dates that differ from the one requested. That test is what makes this an engineering decision
rather than an omission — a future change to filter or clamp has to break it deliberately.

## Two more measured behaviours the docs must carry

**`pe: 0` is returned as `0`, never translated to null.** Twelve of 254 industry-PE snapshot rows read exactly `0`,
emitted as JSON `0` rather than `0.0` — eight on NASDAQ (`Agricultural Inputs`, `Business Equipment &
Supplies`, `Financial - Mortgages`, `Industrial Materials`, `Manufacturing - Textiles`, `Medical - Equipment &
Services`, `Oil & Gas Integrated`, `REIT - Industrial`) and four on NYSE (`Biotechnology`, `Construction`,
`Electronic Gaming & Multimedia`, `Solar`). Across 359 measured values `pe` was never negative and never null,
so zero is carrying "no meaningful aggregate PE" in-band. Biotechnology on the NYSE is not a zero-multiple
industry. The SDK does not have the evidence to say which zeros are real, and translating them would invent
information; the property doc records the twelve and says what the SDK does not know.

**The movers accept no parameters.** `limit=10`, `exchange=NYSE` and `page=1` each returned a response
byte-identical to the bare request. The three lists are fixed at 50 rows and span every exchange at once —
the exact opposite of the eight sector/industry paths. The methods therefore take only a
`CancellationToken`, and offering `limit` would let a caller believe a filter happened.

## Guards

| guard | where | why it is load-bearing |
|---|---|---|
| `ArgumentException.ThrowIfNullOrWhiteSpace(exchange)` | all 8 sector/industry methods | a blank exchange reaches FMP as an omitted one and silently selects NASDAQ |
| `ArgumentException.ThrowIfNullOrWhiteSpace(industry)` | the **2** historical industry methods, where `industry` is required | a blank industry answers `[]` at 200 |
| the same check, applied only when `industry` is not null | the **2** industry snapshots, where `industry` is optional | omitting it is valid and means "every industry"; supplying `" "` is a mistake, and unguarded it would silently mean the same thing |
| `DateRange.ThrowIfBackwards(from, to)` | the 4 historical methods | a backwards range answers `[]` at HTTP 200 — a spent call that says nothing happened |
| `Sector.ToQueryValue()` throws on an undeclared member | every method taking a `Sector` | an unrecognised label answers `[]` at 200 |

**Deliberately not guarded:** an unrecognised exchange, sector label or industry name, all of which answer `[]`
at HTTP 200. The SDK cannot distinguish those from a genuinely empty day, and for industry it would have to
encode a per-exchange coverage table the measurement shows is not derivable — 139 of 159 names work, and which
139 depends on the exchange. Documented on the method and on the parameter instead.

## Serialisation and wiring

No converter. Five `[JsonSerializable(typeof(List<…>))]` entries in `FmpJsonContext`, and the existing
`NullableLocalDateJsonConverter` applied per-property, which is the form the source generator understands.

**Adding the facade is five edits, not four.** The last slice's plan specified four and the implementer found
the fifth by accident:

1. `MarketPerformanceEndpoints` parameter on the `FmpClient` constructor
2. `public MarketPerformanceEndpoints MarketPerformance { get; }` property
3. DI registration in `FmpServiceCollectionExtensions`
4. the hard-coded facade count in `AddFmpTests` — **18 → 19**
5. the `Assert.NotNull(client.MarketPerformance)` line in that same test, which the count does not imply

## Testing

**Unit tests over fixtures**, captured from the measured corpus. Response bodies only — no URL, no host, no
`apikey`. One test per trap, each failing if the trap is reintroduced:

| test | pins |
|---|---|
| `pe: 0` binds to `0m` | that zero is not silently turned into null |
| the 22-digit value round-trips | that the metrics are `decimal` and not `double` |
| an exponent-form value (`5.735079118365113e-7`) binds | that the deep-history serialisation still needs no converter |
| `changesPercentage` reaches `ChangePercentage` | the third-spelling attribute |
| all 11 `Sector` members map; `(Sector)999` throws | the enum's contract |
| the ragged 11-row fixture returns 11 rows with mixed dates | the documented snapshot behaviour |
| each method issues the expected path and query | the request shapes |
| `DateRange.ThrowIfBackwards` fires on each historical method | the backwards-range guard |

**`EndpointCoverageTests` needs no new argument arm.** It handles enums generically
(`Enum.GetValues(type).GetValue(0)`) and its strings fall through to `AAPL`, which is harmless for a harness
that records only which path went out. It gains 11 rows in the generated README block.

**The smoke sweep needs two new arms in `Probe.Argument`**, and both prevent the specific failure that file's
comments were written about:

```csharp
"industry" => LiveApi.Industry,                        // new constant
if (type == typeof(Sector)) return Sector.Technology;  // no generic enum fallback exists in Probe
```

Without the first, `industry` falls through to `_ => LiveApi.Symbol`, probes with `AAPL`, gets `[]`, and
records `outcome empty` as the healthy baseline for four endpoints — matching itself green every week after.
That is the silent green `LiveApi.Exchange` and `LiveApi.Cik` were each named for. Without the second, `Sector`
passes every explicit type check in `Probe.Argument` and reaches `throw Unknown(parameter)`.

`date` and `from`/`to` need nothing. The `LocalDate` default is `LiveApi.SettledWeekday`, and only weekends
were measured to answer `[]` — a market holiday did not (2026-01-01 returned 11 rows). `from` defaults to
`LiveApi.RangeStart`, ninety days wide, and a 28-day window already returns 20 rows.

Baseline grows **167 → 178** outcome blocks. Regenerating re-runs the whole sweep — roughly 178 live calls,
not eleven.

## Documentation deliverables

The generated README block gains 11 rows; regenerate with
`FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests` (no key needed — stub handler). The prose outside
the generated markers is not machine-checked and must be edited by hand:

| | before | after |
|---|---|---|
| modelled | 187 | **198** |
| remaining | 56 | **45** |
| actionable | 49 | **38** |
| open child issues | six | **five** |
| actionable children | five | **four** |

`243 − 198 = 45`, and `45 − 7` blocked TipRanks paths `= 38`. Issue #25's body needs the same arithmetic and a
row moved from its remainder table into Shipped.

## Files

**Created**

- `src/FmpDotNet/Sector.cs` — the enum and `SectorExtensions.ToQueryValue`
- `src/FmpDotNet/Models/MarketMover.cs`
- `src/FmpDotNet/Models/SectorIndustryMetrics.cs` — `SectorPerformance`, `IndustryPerformance`, `SectorPe`, `IndustryPe`
- `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`
- `tests/FmpDotNet.Tests/MarketPerformanceTests.cs` and `SectorTests.cs`
- fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modified**

- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — five entries
- `src/FmpDotNet/FmpClient.cs` — constructor parameter and property
- `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — registration
- `tests/FmpDotNet.Tests/AddFmpTests.cs` — count 18 → 19, **and** the `Assert.NotNull` line
- `tests/FmpDotNet.SmokeTests/Probe.cs` — the `industry` and `Sector` arms
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — `Industry` constant
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — regenerated, 167 → 178
- `README.md` — generated block regenerated, prose arithmetic by hand
