# Market Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `fmp.MarketPerformance` facade covering all eleven documented Market Performance paths, taking SDK coverage from 187 to 198 of 243.

**Architecture:** One facade, eleven methods, one per path — no enum indirection, because the three call shapes (no-argument movers, one-day snapshots, ranged history) genuinely differ. Five plain records bound by the source generator with **no custom converter anywhere in this slice**. Two of the group's three measured traps are closed by making `from`/`to` and `exchange` required and non-nullable; the third is documented and pinned by a test.

**Tech Stack:** .NET 10, C# 13, NodaTime `LocalDate`, source-generated `System.Text.Json` via `FmpJsonContext`, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-market-performance-design.md` (committed `6160a92`, amended `3d5e04d`)

**Measurements:** `docs/superpowers/specs/2026-08-29-market-performance-measurements.md` (committed `199d973`, amended `3d5e04d`) — every number below traces to this file.

## Global Constraints

- **`TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true`.** A `<see cref="...">` pointing at a type that does not exist yet is **CS1574, which is a build error, not a warning.** Use the **deferred-cref pattern**: write `<c>MarketPerformanceEndpoints</c>` while the target does not exist, and promote it to a real `<see cref>` in the task that creates it. Task 6 promotes every deferred cref in this plan.
- **CS1591 is not suppressed project-wide.** Every public type, member and parameter needs an XML doc comment. Do not add a `#pragma warning disable CS1591`; the eight existing file-scoped exemptions are all wide transcription records and nothing in this slice qualifies.
- **The assembly declares `IsAotCompatible`.** Every deserialisation goes through `FmpJsonContext`. A reflection-based `JsonSerializer.Deserialize` overload fails the build with IL2026/IL3050.
- **Never state a fact that was not measured.** Every number, date and behaviour in a doc comment must come from the measurements file, and must carry its measurement date — `measured 2026-08-29`.
- **Never log a built URL and never write one into a fixture.** The API key travels in the query string. Fixtures are response bodies only: no URL, no host, no `apikey`.
- **Do not set `FMPDOTNET_SMOKE_BULK`.** FMP's documented warning: "Frequent abuse on this API Endpoint may result in restrictions placed on this API Key." The bulk sweep is opt-in and no task here needs it.
- **Line length is 120 characters**, matching every file in `src/FmpDotNet/`.
- **`decimal`, never `double`, for every metric.** Measured values reach 22 fractional digits and 17 significant digits.

## File Structure

**Created**

| file | responsibility |
|---|---|
| `src/FmpDotNet/Sector.cs` | the 11-member `Sector` enum and `SectorExtensions.ToQueryValue` |
| `src/FmpDotNet/Models/MarketMover.cs` | the movers row shape |
| `src/FmpDotNet/Models/SectorIndustryMetrics.cs` | all four sector/industry row shapes, in one file deliberately |
| `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs` | the facade, eleven methods |
| `tests/FmpDotNet.Tests/SectorTests.cs` | the enum's contract |
| `tests/FmpDotNet.Tests/MarketPerformanceTests.cs` | binding, traps, request shapes, guards |
| `tests/FmpDotNet.Tests/Fixtures/market-performance-*.json` | seven captures, given verbatim in the tasks below |

**Modified**

| file | change |
|---|---|
| `src/FmpDotNet/Serialization/FmpJsonContext.cs` | five `[JsonSerializable]` entries |
| `src/FmpDotNet/FmpClient.cs` | constructor parameter and property |
| `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` | one `TryAddTransient` |
| `tests/FmpDotNet.Tests/AddFmpTests.cs` | count 18 → 19 **and** the `Assert.NotNull` line |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | the `industry` arm and the `Sector` arm |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | the `Industry` constant |
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | regenerated, 167 → 178 outcome blocks |
| `README.md` | generated block regenerated; prose arithmetic by hand |

## Expected red suite between Tasks 4 and 8

**From the moment Task 4 lands until Task 8 regenerates the README, `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` fails.** That is correct and expected: the generated block still describes 187 endpoints while the code now calls more. Tasks 5, 6 and 7 inherit a suite failing for that **one known reason**.

**Any other failing test is a real failure.** Before assuming a red suite is "the known one", run the suite and confirm the failure count is exactly one and its name is the test above.

**`EndpointCoverageTests.Argument` needs no new arm and must not be given one.** It already handles enums
generically (`if (type.IsEnum) return Enum.GetValues(type).GetValue(0)!;`, which yields
`Sector.BasicMaterials`), maps every unrecognised string to `"AAPL"`, and returns `new LocalDate(2026, 1, 2)`
for every `LocalDate` — so `from` and `to` are equal and `DateRange.ThrowIfBackwards` does not fire. Meaningless
values are harmless there: that harness runs against a stub handler and records only which path went out. This
is the opposite of `Probe.Argument` in the smoke project, which talks to the live API and does need two new arms
— see Task 7.

---

### Task 1: The `Sector` enum

**Files:**
- Create: `src/FmpDotNet/Sector.cs`
- Test: `tests/FmpDotNet.Tests/SectorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public enum FmpDotNet.Sector` with members `BasicMaterials, CommunicationServices, ConsumerCyclical, ConsumerDefensive, Energy, FinancialServices, Healthcare, Industrials, RealEstate, Technology, Utilities`; and `public static string SectorExtensions.ToQueryValue(this Sector sector)`, which throws `ArgumentOutOfRangeException` on an undeclared member. Tasks 4, 5 and 6 call `ToQueryValue()`.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/SectorTests.cs`:

```csharp
namespace FmpDotNet.Tests;

/// <summary>The sector vocabulary, checked against the capture taken live 2026-08-29.</summary>
public class SectorTests
{
    /// <summary>The eleven wire labels, exactly as `stable/available-sectors` returned them on 2026-08-29.</summary>
    public static TheoryData<Sector, string> WireLabels => new()
    {
        { Sector.BasicMaterials, "Basic Materials" },
        { Sector.CommunicationServices, "Communication Services" },
        { Sector.ConsumerCyclical, "Consumer Cyclical" },
        { Sector.ConsumerDefensive, "Consumer Defensive" },
        { Sector.Energy, "Energy" },
        { Sector.FinancialServices, "Financial Services" },
        { Sector.Healthcare, "Healthcare" },
        { Sector.Industrials, "Industrials" },
        { Sector.RealEstate, "Real Estate" },
        { Sector.Technology, "Technology" },
        { Sector.Utilities, "Utilities" },
    };

    [Theory]
    [MemberData(nameof(WireLabels))]
    public void Every_member_maps_to_its_wire_label(Sector sector, string expected)
        => Assert.Equal(expected, sector.ToQueryValue());

    [Fact]
    public void The_enum_covers_the_measured_vocabulary_and_nothing_else()
    {
        // `stable/available-sectors` returned exactly 11 rows on 2026-08-29, and every unfiltered sector
        // snapshot taken — eight of them, across five dates and three exchanges — carried exactly those 11
        // names. A twelfth member here would be a name FMP was never measured to accept.
        Assert.Equal(11, Enum.GetValues<Sector>().Length);
        Assert.Equal(11, WireLabels.Count);
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_the_wire()
    {
        // An unrecognised sector answers HTTP 200 with `[]`, measured 2026-08-29 with `sector=Technlogy`. A
        // value that escaped this method would surface as "a quiet day" rather than as an argument error.
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Sector)999).ToQueryValue());
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SectorTests`
Expected: build failure, `CS0246: The type or namespace name 'Sector' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `src/FmpDotNet/Sector.cs`:

```csharp
namespace FmpDotNet;

/// <summary>The sector asked of the Market Performance sector paths.
///
/// <para><b>An enum because a wrong name is not reported.</b> Measured 2026-08-29,
/// <c>stable/historical-sector-performance?sector=Technlogy</c> answers <b>HTTP 200</b> with <c>[]</c> — a typo
/// and a genuinely quiet day are the same response. The same is true of the snapshot paths.</para>
///
/// <para><b>Eleven members, and the set is complete as measured rather than as documented.</b>
/// <c>stable/available-sectors</c> returned 11 rows on 2026-08-29, and every unfiltered sector snapshot taken
/// that day — eight of them, across five dates and three exchanges — carried exactly those 11 names, no more
/// and no fewer.</para>
///
/// <para><b>This buys typo-safety, not casing-safety, and the difference matters.</b>
/// <see cref="EconomicIndicator"/> is case-<i>sensitive</i> upstream: <c>GDP</c> works and <c>gdp</c> does not.
/// Sector is not — measured 2026-08-29, <c>sector=technology</c> answered a response <b>byte-identical</b> to
/// <c>sector=Technology</c>. Do not read the two enums as carrying the same guarantee.</para>
///
/// <para><b>There is deliberately no equivalent for industry.</b> <c>stable/available-industries</c> lists 159
/// names, of which only <b>139</b> appear in any snapshot on either NASDAQ or NYSE. Twenty documented
/// industries — <c>Banks</c>, <c>Asset Management</c>, <c>Environmental Services</c>, <c>Silver</c> and
/// <c>Media &amp; Entertainment</c> among them — answer <c>[]</c> on every exchange. An enum whose members are
/// one-in-eight measured to fail silently would promise a validity it cannot deliver, so industry is a
/// <see langword="string"/> on <c>MarketPerformanceEndpoints</c> and the caller reads the live vocabulary from
/// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/>.</para></summary>
public enum Sector
{
    /// <summary>Wire <c>Basic Materials</c>.</summary>
    BasicMaterials,

    /// <summary>Wire <c>Communication Services</c>.</summary>
    CommunicationServices,

    /// <summary>Wire <c>Consumer Cyclical</c>.</summary>
    ConsumerCyclical,

    /// <summary>Wire <c>Consumer Defensive</c>.</summary>
    ConsumerDefensive,

    /// <summary>Wire <c>Energy</c>.</summary>
    Energy,

    /// <summary>Wire <c>Financial Services</c>.</summary>
    FinancialServices,

    /// <summary>Wire <c>Healthcare</c>.</summary>
    Healthcare,

    /// <summary>Wire <c>Industrials</c>.</summary>
    Industrials,

    /// <summary>Wire <c>Real Estate</c>.</summary>
    RealEstate,

    /// <summary>Wire <c>Technology</c>.</summary>
    Technology,

    /// <summary>Wire <c>Utilities</c>.</summary>
    Utilities,
}

/// <summary>Conversions for <see cref="Sector"/>.</summary>
public static class SectorExtensions
{
    /// <summary>The value FMP expects in the <c>sector=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible: an unrecognised sector is
    /// answered with <b>HTTP 200 and <c>[]</c></b>, measured 2026-08-29, so a value that escaped this method
    /// would reach the caller as an empty result rather than as an error.</para></summary>
    /// <param name="sector">The sector to convert.</param>
    /// <returns>FMP's own label for the sector.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this Sector sector) => sector switch
    {
        Sector.BasicMaterials => "Basic Materials",
        Sector.CommunicationServices => "Communication Services",
        Sector.ConsumerCyclical => "Consumer Cyclical",
        Sector.ConsumerDefensive => "Consumer Defensive",
        Sector.Energy => "Energy",
        Sector.FinancialServices => "Financial Services",
        Sector.Healthcare => "Healthcare",
        Sector.Industrials => "Industrials",
        Sector.RealEstate => "Real Estate",
        Sector.Technology => "Technology",
        Sector.Utilities => "Utilities",
        _ => throw new ArgumentOutOfRangeException(nameof(sector), sector, "Not a known sector."),
    };
}
```

Note the deferred cref: `<c>MarketPerformanceEndpoints</c>` is plain `<c>`, not `<see cref>`, because that type does not exist until Task 4. Task 6 promotes it.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SectorTests`
Expected: PASS, 13 tests (11 theory cases + 2 facts).

- [ ] **Step 5: Run the whole suite to confirm nothing else moved**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: all green. Task 1 adds no endpoint, so the coverage test is unaffected.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Sector.cs tests/FmpDotNet.Tests/SectorTests.cs
git commit -m "feat: add the Sector enum and its wire labels (#32)"
```

---

### Task 2: The `MarketMover` record

**Files:**
- Create: `src/FmpDotNet/Models/MarketMover.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/market-performance-biggest-gainers.head.json`
- Create: `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `public sealed record FmpDotNet.Models.MarketMover` with `string? Symbol`, `string? Name`, `decimal? Price`, `decimal? Change`, `decimal? ChangePercentage`, `string? Exchange`; and `FmpJsonContext.Default.ListMarketMover`. Task 4 returns `IReadOnlyList<MarketMover>` from three methods.

- [ ] **Step 1: Create the fixture**

Create `tests/FmpDotNet.Tests/Fixtures/market-performance-biggest-gainers.head.json` with exactly this content — the first three rows of `stable/biggest-gainers` as captured 2026-08-29:

```json
[
  {
    "symbol": "FNGR",
    "price": 0.398,
    "name": "FingerMotion, Inc.",
    "change": 0.2246,
    "changesPercentage": 129.5271,
    "exchange": "NASDAQ"
  },
  {
    "symbol": "CHAI",
    "price": 0.4,
    "name": "Core AI Holdings Inc",
    "change": 0.1454,
    "changesPercentage": 57.10919,
    "exchange": "NASDAQ"
  },
  {
    "symbol": "WCT",
    "price": 1.09,
    "name": "Wellchange Holdings Company Limited",
    "change": 0.341,
    "changesPercentage": 45.52737,
    "exchange": "NASDAQ"
  }
]
```

The `Fixtures/*.json` glob in `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj` already copies this to the output directory. No csproj edit is needed.

- [ ] **Step 2: Write the failing test**

Create `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The eleven Market Performance paths, checked against captures taken live 2026-08-29.</summary>
public class MarketPerformanceTests
{
    [Fact]
    public void A_mover_binds_all_six_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-biggest-gainers.head.json"),
            FmpJsonContext.Default.ListMarketMover)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("FNGR", rows[0].Symbol);
        Assert.Equal("FingerMotion, Inc.", rows[0].Name);
        Assert.Equal(0.398m, rows[0].Price);
        Assert.Equal(0.2246m, rows[0].Change);
        Assert.Equal(129.5271m, rows[0].ChangePercentage);
        Assert.Equal("NASDAQ", rows[0].Exchange);
    }

    [Fact]
    public void The_movers_third_spelling_of_change_percentage_binds_to_the_house_name()
    {
        // FMP spells this fact three ways: `changePercentage` on quote, `changePercent` on end-of-day, and
        // `changesPercentage` — with the S — here. EndOfDayBar already documents its divergence and normalises
        // the C# name; this follows the same rule. Do NOT "fix" the attribute: the property would then bind
        // nothing, silently, and Binding.Unbound above is the only other thing that would notice.
        var row = JsonSerializer.Deserialize(
            """[{"changesPercentage":129.5271}]""", FmpJsonContext.Default.ListMarketMover)![0];

        Assert.Equal(129.5271m, row.ChangePercentage);
    }

    [Fact]
    public void A_mover_carries_no_date_of_its_own()
    {
        // Measured 2026-08-29: the movers shape is exactly six keys and none of them is a date or a timestamp.
        // The lists describe a session and never name it — cross-checked against `stable/quote?symbol=FNGR`,
        // which returned the identical price, change and percentage with `timestamp 1787947201`
        // (2026-08-28 20:00:01Z). This test fails if a future capture grows a date field, which would mean the
        // model can now answer a question its own doc says it cannot.
        using var wire = JsonDocument.Parse(
            Binding.Fixture("market-performance-biggest-gainers.head.json"));

        var keys = wire.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            ["symbol", "price", "name", "change", "changesPercentage", "exchange"], keys);
    }
}
```

- [ ] **Step 3: Run the test and confirm it fails to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: build failure, `CS1061` on `ListMarketMover` / `CS0246` on `MarketMover`.

- [ ] **Step 4: Write the record**

Create `src/FmpDotNet/Models/MarketMover.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of the three movers lists — <c>stable/biggest-gainers</c>,
/// <c>stable/biggest-losers</c> and <c>stable/most-actives</c>.
///
/// <para>The three share one shape exactly. Measured 2026-08-29, each answered <b>50 rows</b> carrying the
/// same six keys, and the lists overlap: 8 symbols were in both gainers and most-actives, 1 in both losers and
/// most-actives, 0 in both gainers and losers.</para>
///
/// <para><b>No row carries a date.</b> The lists describe a session and never name it. Cross-checked
/// 2026-08-29 (a Saturday), <c>FNGR</c> read <c>price 0.398, change 0.2246, changesPercentage 129.5271</c>
/// here, and <c>stable/quote?symbol=FNGR</c> returned those three values <b>identically</b> with
/// <c>timestamp 1787947201</c> — <c>2026-08-28 20:00:01Z</c>, Friday's close. So the lists are the last
/// completed session, and <see cref="Quote"/> is where a caller learns which one that was.</para>
///
/// <para><b><c>most-actives</c> carries no volume</b>, measured the same day — the quantity that defines the
/// ranking is not in the response. <see cref="Quote.Volume"/> has it.</para></summary>
public sealed record MarketMover
{
    /// <summary>The ticker. Nullable because the deserialiser cannot promise a key is present, not because any
    /// measured row omitted it — no null appeared in any field across 9,855 rows measured 2026-08-29.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>name</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The last price. See <see cref="Symbol"/> for why it is nullable.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The absolute change over the session.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>The percentage change over the session.
    ///
    /// <para><b>The wire spells this <c>changesPercentage</c> — with an S — which is a third spelling of one
    /// concept in this API.</b> <see cref="Quote.ChangePercentage"/> binds <c>changePercentage</c> and
    /// <see cref="EndOfDayBar.ChangePercentage"/> binds <c>changePercent</c>. The property carries the house
    /// name so the three read alike in C#; the attribute carries the wire verbatim, under the same rule that
    /// binds <c>senateID</c> to <c>SenateId</c>. <b>Do not "fix" the attribute</b> — the property would then
    /// bind nothing, silently.</para></summary>
    [JsonPropertyName("changesPercentage")] public decimal? ChangePercentage { get; init; }

    /// <summary>The exchange the symbol trades on. Present on every measured row; the movers lists span all
    /// exchanges at once, unlike the sector and industry paths, which answer for one exchange at a
    /// time.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
}
```

- [ ] **Step 5: Register the model in the JSON context**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add this line to the `[JsonSerializable]` list, after the last existing entry:

```csharp
[JsonSerializable(typeof(List<MarketMover>))]
```

- [ ] **Step 6: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: PASS, 3 tests.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: all green — still no new endpoint, so the coverage test is unaffected.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/MarketMover.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/MarketPerformanceTests.cs \
        tests/FmpDotNet.Tests/Fixtures/market-performance-biggest-gainers.head.json
git commit -m "feat: add the MarketMover record (#32)"
```

---

### Task 3: The four sector and industry records

**Files:**
- Create: `src/FmpDotNet/Models/SectorIndustryMetrics.cs`
- Create: six fixtures under `tests/FmpDotNet.Tests/Fixtures/`
- Modify: `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: nothing from Tasks 1-2.
- Produces: `SectorPerformance`, `IndustryPerformance`, `SectorPe`, `IndustryPe` — all `public sealed record` in `FmpDotNet.Models` — plus `FmpJsonContext.Default.ListSectorPerformance`, `ListIndustryPerformance`, `ListSectorPe`, `ListIndustryPe`. Tasks 5 and 6 return `IReadOnlyList<>` of each.

Each record has four properties. `SectorPerformance`: `LocalDate? Date`, `string? Sector`, `string? Exchange`, `decimal? AverageChange`. `IndustryPerformance` swaps `Sector` for `string? Industry`. `SectorPe` swaps `AverageChange` for `decimal? Pe`. `IndustryPe` does both.

- [ ] **Step 1: Create the six fixtures**

`tests/FmpDotNet.Tests/Fixtures/market-performance-sector-pe-snapshot.head.json` — the first two rows of `stable/sector-pe-snapshot?date=2026-08-28`:

```json
[
  {
    "date": "2026-08-28",
    "sector": "Basic Materials",
    "exchange": "NASDAQ",
    "pe": 25.792527521262276
  },
  {
    "date": "2026-08-28",
    "sector": "Communication Services",
    "exchange": "NASDAQ",
    "pe": 20.413145893353047
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-performance-industry-performance-snapshot.head.json` — the first three rows of `stable/industry-performance-snapshot?date=2026-08-28`:

```json
[
  {
    "date": "2026-08-28",
    "industry": "Advertising Agencies",
    "exchange": "NASDAQ",
    "averageChange": 0.5507225355896539
  },
  {
    "date": "2026-08-28",
    "industry": "Aerospace & Defense",
    "exchange": "NASDAQ",
    "averageChange": 0.35005461750317046
  },
  {
    "date": "2026-08-28",
    "industry": "Agricultural Farm Products",
    "exchange": "NASDAQ",
    "averageChange": 0.34359138098742914
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-performance-industry-pe-snapshot.head.json` — two ordinary rows and one carrying the measured `pe: 0`:

```json
[
  {
    "date": "2026-08-28",
    "industry": "Advertising Agencies",
    "exchange": "NASDAQ",
    "pe": 19.993844800802336
  },
  {
    "date": "2026-08-28",
    "industry": "Aerospace & Defense",
    "exchange": "NASDAQ",
    "pe": 11.458311799603079
  },
  {
    "date": "2026-08-28",
    "industry": "Agricultural Inputs",
    "exchange": "NASDAQ",
    "pe": 0
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-performance-historical-sector-performance.head.json` — **copy this byte for byte.** The first value is in exponent form exactly as FMP wrote it, single-digit exponent and all; the second is the longest plain fraction in the corpus. Do not reformat either.

```json
[
  {
    "date": "2005-09-02",
    "sector": "Technology",
    "exchange": "NASDAQ",
    "averageChange": 5.735079118365113e-7
  },
  {
    "date": "2006-10-05",
    "sector": "Technology",
    "exchange": "NASDAQ",
    "averageChange": -0.0000026524148173594842
  },
  {
    "date": "2015-12-31",
    "sector": "Technology",
    "exchange": "NASDAQ",
    "averageChange": -1.171486877582397
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-performance-sector-performance-snapshot.json` — all eleven rows of `stable/sector-performance-snapshot?date=2026-08-28`, every row sharing one date:

```json
[
  { "date": "2026-08-28", "sector": "Basic Materials", "exchange": "NASDAQ", "averageChange": 0.17296837188471859 },
  { "date": "2026-08-28", "sector": "Communication Services", "exchange": "NASDAQ", "averageChange": 1.453460578158583 },
  { "date": "2026-08-28", "sector": "Consumer Cyclical", "exchange": "NASDAQ", "averageChange": 0.8782469218209465 },
  { "date": "2026-08-28", "sector": "Consumer Defensive", "exchange": "NASDAQ", "averageChange": 0.42516588987551174 },
  { "date": "2026-08-28", "sector": "Energy", "exchange": "NASDAQ", "averageChange": -1.4123286236367827 },
  { "date": "2026-08-28", "sector": "Financial Services", "exchange": "NASDAQ", "averageChange": 0.009992534471425785 },
  { "date": "2026-08-28", "sector": "Healthcare", "exchange": "NASDAQ", "averageChange": -1.304865799217958 },
  { "date": "2026-08-28", "sector": "Industrials", "exchange": "NASDAQ", "averageChange": -0.4894123836384434 },
  { "date": "2026-08-28", "sector": "Real Estate", "exchange": "NASDAQ", "averageChange": -3.1167941746989336 },
  { "date": "2026-08-28", "sector": "Technology", "exchange": "NASDAQ", "averageChange": -0.6192144246915721 },
  { "date": "2026-08-28", "sector": "Utilities", "exchange": "NASDAQ", "averageChange": -1.6348147744152053 }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-performance-sector-performance-ragged.json` — the eleven rows returned for `date=2026-09-01`, a date past the end of the data. **Three distinct dates. This is the trap fixture; do not tidy it.**

```json
[
  { "date": "2026-08-28", "sector": "Basic Materials", "exchange": "NASDAQ", "averageChange": 0.17296837188471859 },
  { "date": "2026-08-28", "sector": "Communication Services", "exchange": "NASDAQ", "averageChange": 1.453460578158583 },
  { "date": "2026-08-27", "sector": "Consumer Cyclical", "exchange": "NASDAQ", "averageChange": -0.09404972724083027 },
  { "date": "2026-08-28", "sector": "Consumer Defensive", "exchange": "NASDAQ", "averageChange": 0.42516588987551174 },
  { "date": "2026-08-28", "sector": "Energy", "exchange": "NASDAQ", "averageChange": -1.4123286236367827 },
  { "date": "2026-08-28", "sector": "Financial Services", "exchange": "NASDAQ", "averageChange": 0.009992534471425785 },
  { "date": "2026-08-28", "sector": "Healthcare", "exchange": "NASDAQ", "averageChange": -1.304865799217958 },
  { "date": "2026-08-25", "sector": "Industrials", "exchange": "NASDAQ", "averageChange": -0.18558688760651476 },
  { "date": "2026-08-25", "sector": "Real Estate", "exchange": "NASDAQ", "averageChange": 1.6557323270546709 },
  { "date": "2026-08-28", "sector": "Technology", "exchange": "NASDAQ", "averageChange": -0.6192144246915721 },
  { "date": "2026-08-28", "sector": "Utilities", "exchange": "NASDAQ", "averageChange": -1.6348147744152053 }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/MarketPerformanceTests.cs` (the file already has `using System.Text.Json;` and `using FmpDotNet.Serialization;`; add `using NodaTime;`):

```csharp
    [Fact]
    public void A_sector_performance_row_binds_all_four_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-performance-snapshot.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(11, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].Date);
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal("NASDAQ", rows[0].Exchange);
        Assert.Equal(0.17296837188471859m, rows[0].AverageChange);
    }

    [Fact]
    public void An_industry_performance_row_binds_the_industry_key()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-industry-performance-snapshot.head.json"),
            FmpJsonContext.Default.ListIndustryPerformance)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Advertising Agencies", rows[0].Industry);
        // An ampersand survives the round trip; it is URL-encoded on the way out, not on the way back.
        Assert.Equal("Aerospace & Defense", rows[1].Industry);
        Assert.Equal(0.5507225355896539m, rows[0].AverageChange);
    }

    [Fact]
    public void A_sector_pe_row_binds_the_pe_key()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-pe-snapshot.head.json"),
            FmpJsonContext.Default.ListSectorPe)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal(25.792527521262276m, rows[0].Pe);
    }

    [Fact]
    public void A_pe_of_zero_stays_zero_and_is_not_turned_into_null()
    {
        // Measured 2026-08-29: 12 of 254 industry-PE rows read exactly 0, emitted as JSON `0` rather than
        // `0.0` — eight on NASDAQ and four on NYSE. Across 359 measured values `pe` was never negative and
        // never null, so zero is carrying "no meaningful aggregate PE" in band. Biotechnology on the NYSE is
        // not a zero-multiple industry. The SDK does not have the evidence to say which zeros are real, so it
        // reports what FMP sent; translating them would invent information.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-industry-pe-snapshot.head.json"),
            FmpJsonContext.Default.ListIndustryPe)!;

        Assert.Equal("Agricultural Inputs", rows[2].Industry);
        Assert.Equal(0m, rows[2].Pe);
        Assert.NotNull(rows[2].Pe);
    }

    [Fact]
    public void The_deep_history_number_formats_both_bind_to_the_same_decimal()
    {
        // Two things at once, and both are load-bearing for the decision to ship no custom converter here.
        //
        // 1. FMP writes values below 1e-6 in EXPONENT form. Measured 2026-08-29, exactly ten values in the
        //    corpus do so, all of them in a deep-history request and all below that threshold — every value at
        //    or above it, including the 22-digit one below, is written out in full.
        // 2. The metrics reach 22 fractional digits and 17 significant digits, which is why they are `decimal`.
        //    This test stops compiling if anyone retypes these properties as `double`.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-historical-sector-performance.head.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(0.0000005735079118365113m, rows[0].AverageChange);
        Assert.Equal(-0.0000026524148173594842m, rows[1].AverageChange);
        Assert.Equal(-1.171486877582397m, rows[2].AverageChange);
    }

    [Fact]
    public void A_snapshot_past_the_end_of_the_data_returns_rows_that_do_not_share_a_date()
    {
        // The trap this SDK documents rather than guards. Measured 2026-08-29, `date=2026-09-01` returned 11
        // rows bearing THREE dates — and it is not "each sector's latest row": asked for 2026-08-28 directly,
        // Industrials and Real Estate both return rows dated 2026-08-28. `date=2027-01-04` produced the same
        // split sector for sector, and sector-pe-snapshot produced it too.
        //
        // This test pins the DOCUMENTED behaviour: the SDK hands back all eleven rows unmodified, with their
        // dates intact, so a caller can compare. A future change to filter or clamp has to break this
        // deliberately.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-performance-ragged.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(11, rows.Count);
        Assert.Equal(3, rows.Select(r => r.Date).Distinct().Count());
        Assert.Equal(new LocalDate(2026, 8, 25), rows.Single(r => r.Sector == "Industrials").Date);
        Assert.Equal(new LocalDate(2026, 8, 27), rows.Single(r => r.Sector == "Consumer Cyclical").Date);
        Assert.Equal(new LocalDate(2026, 8, 28), rows.Single(r => r.Sector == "Technology").Date);
    }
```

- [ ] **Step 3: Run the tests and confirm they fail to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: build failure, `CS1061` on `ListSectorPerformance` and the three siblings.

- [ ] **Step 4: Write the four records**

Create `src/FmpDotNet/Models/SectorIndustryMetrics.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// The four records below differ from one another by exactly one word each, and they live in one file for that
// reason. Split across four files they drift; together, a reader editing any one of them sees all four.
//
// They cover eight paths: sector or industry, performance or PE, snapshot or historical. Measured 2026-08-29,
// those eight carry exactly four distinct key tuples, and `snapshot` and `historical` return the SAME rows
// selected differently rather than different rows — which is why there are four types here and not eight.

/// <summary>One sector's average price change on one day and one exchange. From
/// <c>stable/sector-performance-snapshot</c> and <c>stable/historical-sector-performance</c>.
///
/// <para><b>The exchange is part of the fact, not a filter on it.</b> Measured 2026-08-29, Technology on
/// 2026-08-28 read <c>-0.6192</c> on NASDAQ and <c>-1.7398</c> on NYSE, and across 20 shared dates in one
/// window not a single value matched. A row is meaningless without its <see cref="Exchange"/>.</para>
///
/// <para><b><see cref="Date"/> is not necessarily the date you asked for.</b> See the snapshot methods on
/// <c>MarketPerformanceEndpoints</c> for the measurement — a snapshot for a date past the end of the data
/// returns rows bearing three different dates.</para></summary>
public sealed record SectorPerformance
{
    /// <summary>The trading day the row describes. Nullable because the deserialiser cannot promise a key is
    /// present, not because any measured row omitted it — no null appeared in any field across 9,855 rows
    /// measured 2026-08-29.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own sector label — <c>Basic Materials</c>, <c>Technology</c>. A
    /// <see langword="string"/> and not <see cref="FmpDotNet.Sector"/>: binding the label onto the enum would
    /// need a converter, and an unrecognised label would then throw where it currently binds. The enum is an
    /// argument type.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange this average was taken over. See the type summary — this is part of the fact.
    /// Never <c>ALL</c> or an aggregate; no market-wide value appeared among those measured.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The mean price change across the sector's constituents on that exchange, as a percentage.
    ///
    /// <para><b><see cref="decimal"/> rather than <see cref="double"/>, and that is load-bearing.</b> The value
    /// arrives as an unrounded float64 expansion: measured 2026-08-29 the longest plain fractional part was 22
    /// digits and the greatest number of significant digits was 17. Values below <c>1e-6</c> in magnitude
    /// arrive in <b>exponent form</b> — ten of them in the measured corpus, all in deep history — which
    /// <c>System.Text.Json</c> binds to <see cref="decimal"/> unaided; verified 2026-08-29 on .NET 10 with this
    /// SDK's own source-generation options. Range measured across 9,016 values: <c>-74.8932</c> to
    /// <c>+73.6983</c>.</para></summary>
    [JsonPropertyName("averageChange")] public decimal? AverageChange { get; init; }
}

/// <summary>One industry's average price change on one day and one exchange. From
/// <c>stable/industry-performance-snapshot</c> and <c>stable/historical-industry-performance</c>.
///
/// <para>The same shape as <see cref="SectorPerformance"/> under a different key. Everything on that type
/// applies here — the exchange is part of the fact, and <see cref="Date"/> is not necessarily the date you
/// asked for.</para>
///
/// <para><b>The industry vocabulary is wider than these paths answer for.</b>
/// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/> returned 159 names on 2026-08-29 and only 139
/// appear in any snapshot on either NASDAQ or NYSE; the other 20 answer <c>[]</c> everywhere.</para></summary>
public sealed record IndustryPerformance
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own industry label — <c>Advertising Agencies</c>, <c>Oil &amp; Gas Midstream</c>. Labels
    /// carrying <c>&amp;</c> and <c>,</c> were measured to work when URL-encoded on the way out.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The exchange this average was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The mean price change across the industry's constituents on that exchange, as a percentage. See
    /// <see cref="SectorPerformance.AverageChange"/> for why this is <see cref="decimal"/>.</summary>
    [JsonPropertyName("averageChange")] public decimal? AverageChange { get; init; }
}

/// <summary>One sector's aggregate price-to-earnings ratio on one day and one exchange. From
/// <c>stable/sector-pe-snapshot</c> and <c>stable/historical-sector-pe</c>.
///
/// <para><see cref="SectorPerformance"/> with <see cref="Pe"/> in place of
/// <see cref="SectorPerformance.AverageChange"/>; everything documented there applies here too.</para></summary>
public sealed record SectorPe
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own sector label. See <see cref="SectorPerformance.Sector"/>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange this ratio was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The aggregate price-to-earnings ratio.
    ///
    /// <para><b>Zero is an in-band sentinel and this SDK does not translate it.</b> Measured 2026-08-29, 12 of
    /// 254 industry-PE rows read exactly <c>0</c>, emitted as JSON <c>0</c> rather than <c>0.0</c>. Across 359
    /// measured values <c>pe</c> was never negative and never null, so zero is carrying "no meaningful
    /// aggregate PE" rather than a measurement — Biotechnology on the NYSE is not a zero-multiple industry.
    /// The SDK has no way to tell which zeros are real, so it reports what FMP sent. Treat <c>0</c> as "no
    /// answer", not as a ratio.</para></summary>
    [JsonPropertyName("pe")] public decimal? Pe { get; init; }
}

/// <summary>One industry's aggregate price-to-earnings ratio on one day and one exchange. From
/// <c>stable/industry-pe-snapshot</c> and <c>stable/historical-industry-pe</c>.
///
/// <para><see cref="IndustryPerformance"/> with <see cref="Pe"/> in place of
/// <see cref="IndustryPerformance.AverageChange"/>. The <c>pe: 0</c> sentinel documented on
/// <see cref="SectorPe.Pe"/> was measured on this shape specifically — all 12 of the zeros are industry
/// rows.</para></summary>
public sealed record IndustryPe
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own industry label. See <see cref="IndustryPerformance.Industry"/>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The exchange this ratio was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The aggregate price-to-earnings ratio. <b>Zero is an in-band sentinel</b> — see
    /// <see cref="SectorPe.Pe"/>, where the measurement is recorded.</summary>
    [JsonPropertyName("pe")] public decimal? Pe { get; init; }
}
```

- [ ] **Step 5: Register the four models**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add these four lines after the `List<MarketMover>` entry from Task 2:

```csharp
[JsonSerializable(typeof(List<SectorPerformance>))]
[JsonSerializable(typeof(List<IndustryPerformance>))]
[JsonSerializable(typeof(List<SectorPe>))]
[JsonSerializable(typeof(List<IndustryPe>))]
```

- [ ] **Step 6: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: PASS, 9 tests.

If `The_deep_history_number_formats_both_bind_to_the_same_decimal` fails, the fixture was reformatted — check that `5.735079118365113e-7` is still written with a single-digit exponent and that the 22-digit value was not shortened.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: all green.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/SectorIndustryMetrics.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/MarketPerformanceTests.cs tests/FmpDotNet.Tests/Fixtures/
git commit -m "feat: add the four sector and industry metric records (#32)"
```

---

### Task 4: The facade, the three movers methods, and the wiring

**Files:**
- Create: `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs:139` (after the `TechnicalIndicatorsEndpoints` line)
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`
- Modify: `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`

**Interfaces:**
- Consumes: `MarketMover` and `FmpJsonContext.Default.ListMarketMover` from Task 2.
- Produces: `public sealed class FmpDotNet.Endpoints.MarketPerformanceEndpoints(FmpTransport transport)` with `GetBiggestGainersAsync(CancellationToken)`, `GetBiggestLosersAsync(CancellationToken)`, `GetMostActivesAsync(CancellationToken)`, each returning `Task<IReadOnlyList<MarketMover>>`; and `FmpClient.MarketPerformance`. Tasks 5 and 6 add methods to this same class.

**⚠ This task turns the suite red for one known reason.** See "Expected red suite" above.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/MarketPerformanceTests.cs` (add `using FmpDotNet.Endpoints;`, `using Microsoft.Extensions.Options;`):

```csharp
    private static (MarketPerformanceEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new MarketPerformanceEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Theory]
    [InlineData("gainers", "/stable/biggest-gainers")]
    [InlineData("losers", "/stable/biggest-losers")]
    [InlineData("actives", "/stable/most-actives")]
    public async Task Each_movers_list_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        _ = which switch
        {
            "gainers" => await endpoints.GetBiggestGainersAsync(),
            "losers" => await endpoints.GetBiggestLosersAsync(),
            _ => await endpoints.GetMostActivesAsync(),
        };

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task The_movers_send_nothing_but_the_key()
    {
        // Measured 2026-08-29: `limit=10`, `exchange=NYSE` and `page=1` each returned a response BYTE-IDENTICAL
        // to the bare request. The three lists are fixed at 50 rows and span every exchange at once. Offering
        // any of those parameters would let a caller believe a filter happened, so the methods take only a
        // cancellation token — and this test fails if one is ever added.
        var (endpoints, handler) = Build();

        await endpoints.GetBiggestGainersAsync();

        var query = handler.Requests[0].Query;
        Assert.DoesNotContain("limit=", query);
        Assert.DoesNotContain("exchange=", query);
        Assert.DoesNotContain("page=", query);
    }

    [Fact]
    public async Task A_movers_list_binds_through_the_facade()
    {
        var (endpoints, _) = Build(Binding.Fixture("market-performance-biggest-gainers.head.json"));

        var rows = await endpoints.GetBiggestGainersAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal("FNGR", rows[0].Symbol);
        Assert.Equal(129.5271m, rows[0].ChangePercentage);
    }
```

- [ ] **Step 2: Run the tests and confirm they fail to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: build failure, `CS0246: The type or namespace name 'MarketPerformanceEndpoints' could not be found`.

- [ ] **Step 3: Create the facade with its three movers methods**

Create `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>How the market moved — the movers lists, and sector and industry performance and valuation.
///
/// <para><b>Eleven paths in three call shapes.</b> The movers take no parameters at all; the snapshots take a
/// day; the historical paths take a range. That is why this facade has eleven methods rather than one
/// parameterised method — unlike <see cref="TechnicalIndicatorsEndpoints"/>, where nine paths shared one
/// shape.</para>
///
/// <para><b>There is no market-wide sector view, and these signatures say so.</b> Every sector and industry
/// path answers for <b>one exchange</b>: <c>exchange</c> is required here because omitting it upstream
/// silently selects NASDAQ alone, and measured 2026-08-29 that is a materially different answer — Technology on
/// 2026-08-28 read <c>-0.6192</c> on NASDAQ and <c>-1.7398</c> on NYSE, with not one of 20 shared dates
/// matching. No "all exchanges" value appeared among those measured, so a caller who wants the whole market
/// iterates <see cref="DirectoryEndpoints.GetExchangesAsync"/>. The three movers lists are the only
/// market-wide thing in this group.</para></summary>
public sealed class MarketPerformanceEndpoints(FmpTransport transport)
{
    /// <summary>The fifty biggest percentage risers of the last completed session, from
    /// <c>stable/biggest-gainers</c>.
    ///
    /// <para><b>Fifty rows, every exchange, and no parameters are accepted.</b> Measured 2026-08-29,
    /// <c>limit=10</c>, <c>exchange=NYSE</c> and <c>page=1</c> each returned a response <b>byte-identical</b>
    /// to the bare request. The list cannot be narrowed, paged or extended.</para>
    ///
    /// <para><b>The rows carry no date.</b> See <see cref="Models.MarketMover"/> — the list describes a session
    /// and never names it. <see cref="QuoteEndpoints.GetQuoteAsync"/> is where a caller learns which
    /// one.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order, which is by descending percentage change. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<MarketMover>> GetBiggestGainersAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/biggest-gainers"), FmpJsonContext.Default.ListMarketMover, ct);

    /// <summary>The fifty biggest percentage fallers of the last completed session, from
    /// <c>stable/biggest-losers</c>.
    ///
    /// <para>Fifty rows, every exchange, no parameters accepted — see
    /// <see cref="GetBiggestGainersAsync"/>, where the measurement is recorded. Measured 2026-08-29 this list
    /// shared <b>no</b> symbol with the gainers and one with the most-actives.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketMover>> GetBiggestLosersAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/biggest-losers"), FmpJsonContext.Default.ListMarketMover, ct);

    /// <summary>The fifty most active symbols of the last completed session, from
    /// <c>stable/most-actives</c>.
    ///
    /// <para><b>The response carries no volume</b>, measured 2026-08-29 — the quantity that defines the ranking
    /// is not in the body. <see cref="Models.Quote.Volume"/> has it, per symbol.</para>
    ///
    /// <para>Fifty rows, every exchange, no parameters accepted — see
    /// <see cref="GetBiggestGainersAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketMover>> GetMostActivesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/most-actives"), FmpJsonContext.Default.ListMarketMover, ct);
}
```

- [ ] **Step 4: Wire the facade — all five edits**

**This is five edits, not four.** The previous slice's plan specified four and the fifth was found by accident.

**4a.** In `src/FmpDotNet/FmpClient.cs`, add the constructor parameter after `TechnicalIndicatorsEndpoints technicalIndicators`:

```csharp
    TechnicalIndicatorsEndpoints technicalIndicators, MarketPerformanceEndpoints marketPerformance)
```

**4b.** In the same file, add the property after the `TechnicalIndicators` property:

```csharp
    /// <summary>How the market moved — the gainers, losers and most-actives lists, and sector and industry
    /// performance and valuation, by day or over a range.
    ///
    /// <para>Every sector and industry method answers for <b>one exchange</b> and requires it. See
    /// <see cref="MarketPerformanceEndpoints"/>.</para></summary>
    public MarketPerformanceEndpoints MarketPerformance { get; } = marketPerformance;
```

**4c.** In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, after line 139:

```csharp
        services.TryAddTransient<MarketPerformanceEndpoints>();
```

**4d.** In `tests/FmpDotNet.Tests/AddFmpTests.cs`, add this line after `Assert.NotNull(client.TechnicalIndicators);`:

```csharp
        Assert.NotNull(client.MarketPerformance);
```

**4e.** In the same test, change the count from 18 to 19:

```csharp
        Assert.Equal(19, typeof(FmpClient)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length);
```

Edits 4d and 4e are separate on purpose. The comment above the count in that test records that the list was three short when `SecFilings` was added; bumping the number without adding the line reproduces exactly that bug, and the count still passes.

- [ ] **Step 5: Run the new tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: PASS, 14 tests.

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~AddFmpTests`
Expected: PASS.

- [ ] **Step 6: Run the whole suite and confirm exactly one expected failure**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: **exactly one failure**, named
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`.
The README's generated block still describes the pre-#32 endpoint set. Task 8 regenerates it.

**If the failure count is not exactly one, or the name differs, stop and investigate — that is a real failure, not this one.**

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Tests/MarketPerformanceTests.cs
git commit -m "feat: add the MarketPerformance facade and its three movers lists (#32)"
```

---

### Task 5: The four snapshot methods

**Files:**
- Modify: `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`

**Interfaces:**
- Consumes: `MarketPerformanceEndpoints` from Task 4; `Sector` and `SectorExtensions.ToQueryValue()` from Task 1; the four records and their `FmpJsonContext` entries from Task 3.
- Produces: `GetSectorPerformanceSnapshotAsync(LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default)` and its three siblings `GetSectorPeSnapshotAsync`, `GetIndustryPerformanceSnapshotAsync(LocalDate date, string exchange, string? industry = null, …)`, `GetIndustryPeSnapshotAsync`.

**Suite state:** inherits the one known `EndpointCoverageTests` failure from Task 4. Any second failure is real.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`:

```csharp
    [Fact]
    public async Task The_sector_performance_snapshot_sends_the_date_the_exchange_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), "NASDAQ");

        Assert.Equal("/stable/sector-performance-snapshot", handler.Requests[0].AbsolutePath);
        var query = handler.Requests[0].Query;
        Assert.Contains("date=2026-08-28", query);
        Assert.Contains("exchange=NASDAQ", query);
        // The optional filter is omitted entirely when null rather than sent empty — an empty `sector=`
        // is not a request that was ever measured.
        Assert.DoesNotContain("sector=", query);
    }

    [Fact]
    public async Task The_sector_filter_goes_out_as_FMPs_own_label()
    {
        // Measured 2026-08-29, `date=2026-08-28&sector=Technology` returned exactly one row — real server-side
        // filtering, which is why it is offered. The enum member is FinancialServices; the wire wants
        // "Financial Services", with the space.
        var (endpoints, handler) = Build();

        await endpoints.GetSectorPerformanceSnapshotAsync(
            new LocalDate(2026, 8, 28), "NASDAQ", Sector.FinancialServices);

        Assert.Contains("sector=Financial%20Services", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_industry_filter_url_encodes_an_ampersand()
    {
        // Measured 2026-08-29: `industry=Aerospace & Defense` returns rows when encoded. An unencoded
        // ampersand would split the query string and silently drop everything after it, including the key.
        var (endpoints, handler) = Build();

        await endpoints.GetIndustryPerformanceSnapshotAsync(
            new LocalDate(2026, 8, 28), "NASDAQ", "Aerospace & Defense");

        Assert.Contains("industry=Aerospace%20%26%20Defense", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData("sector-pe", "/stable/sector-pe-snapshot")]
    [InlineData("industry-performance", "/stable/industry-performance-snapshot")]
    [InlineData("industry-pe", "/stable/industry-pe-snapshot")]
    public async Task Each_remaining_snapshot_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();
        var date = new LocalDate(2026, 8, 28);

        switch (which)
        {
            case "sector-pe": await endpoints.GetSectorPeSnapshotAsync(date, "NASDAQ"); break;
            case "industry-performance":
                await endpoints.GetIndustryPerformanceSnapshotAsync(date, "NASDAQ"); break;
            default: await endpoints.GetIndustryPeSnapshotAsync(date, "NASDAQ"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_exchange_is_rejected_before_the_request_goes_out(string exchange)
    {
        // A blank exchange reaches FMP as an OMITTED one, which silently selects NASDAQ alone. Rejecting here
        // is the only place the two can be told apart.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), exchange));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_supplied_but_blank_industry_filter_is_rejected()
    {
        // Omitting `industry` is valid and means "every industry". Supplying "   " is a mistake, and unguarded
        // it would reach FMP meaning exactly the same thing — the caller would believe a filter happened.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetIndustryPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), "NASDAQ", "   "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_snapshot_returns_the_ragged_rows_through_the_facade_unmodified()
    {
        // The end-to-end half of the trap test in Task 3: the facade must not filter, clamp or reorder.
        var (endpoints, _) = Build(Binding.Fixture("market-performance-sector-performance-ragged.json"));

        var rows = await endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 9, 1), "NASDAQ");

        Assert.Equal(11, rows.Count);
        Assert.Equal(3, rows.Select(r => r.Date).Distinct().Count());
    }
```

- [ ] **Step 2: Run the tests and confirm they fail to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: build failure, `CS1061` — `MarketPerformanceEndpoints` has no `GetSectorPerformanceSnapshotAsync`.

- [ ] **Step 3: Add the four methods**

Add to `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`, and add `using NodaTime;` to the top of the file:

```csharp
    /// <summary>Every sector's average price change on one day and one exchange, from
    /// <c>stable/sector-performance-snapshot</c>.
    ///
    /// <para><b>A date past the end of the data does not answer empty — it answers a row set whose rows do not
    /// share a date.</b> Measured 2026-08-29, <c>date=2026-09-01</c> returned 11 rows bearing <b>three</b>
    /// dates: Industrials and Real Estate at 2026-08-25, Consumer Cyclical at 2026-08-27, the other eight at
    /// 2026-08-28. <c>date=2027-01-04</c> produced that split sector for sector, identically, and
    /// <see cref="GetSectorPeSnapshotAsync"/> produced it too. It is <b>not</b> "each sector's latest row":
    /// asked for 2026-08-28 directly, Industrials and Real Estate both return rows dated 2026-08-28. The values
    /// are real and the dates are honest; the row set is simply not a coherent day.</para>
    ///
    /// <para><b>Not guarded, deliberately.</b> <see cref="Models.SectorPerformance.Date"/> is on every row, so
    /// the check is one comparison at the call site. Guarding would need a clock this library does not have,
    /// and clamping would delete real rows.</para>
    ///
    /// <para>A weekend answers <c>[]</c> with HTTP 200 — measured 2026-08-22 and 2026-08-29, both Saturdays. A
    /// market holiday does <b>not</b>: 2026-01-01 returned 11 rows dated 2026-01-01.</para>
    ///
    /// <para>An unrecognised <paramref name="exchange"/> or <paramref name="sector"/> answers <c>[]</c> with
    /// HTTP 200 rather than an error, which is why <paramref name="exchange"/> is required and
    /// <paramref name="sector"/> is an enum.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for — <c>NASDAQ</c>, <c>NYSE</c> and <c>AMEX</c> were each
    /// verified 2026-08-29. Case-insensitive. Required: omitting it upstream silently selects NASDAQ alone.
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/> lists what FMP knows.</param>
    /// <param name="sector">Narrows the answer to one sector, server-side. Omit for all eleven.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPerformance>> GetSectorPerformanceSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        return transport.GetListAsync(
            new FmpRequest("stable/sector-performance-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("sector", sector?.ToQueryValue()),
            FmpJsonContext.Default.ListSectorPerformance, ct);
    }

    /// <summary>Every sector's aggregate price-to-earnings ratio on one day and one exchange, from
    /// <c>stable/sector-pe-snapshot</c>.
    ///
    /// <para><b>The out-of-range date behaviour documented on
    /// <see cref="GetSectorPerformanceSnapshotAsync"/> was measured on this path too</b>, producing the same
    /// three-date split sector for sector. Read that method's summary; it applies here unchanged.</para>
    ///
    /// <para><b>A <c>pe</c> of exactly <c>0</c> means "no meaningful aggregate", not a ratio of zero</b> — see
    /// <see cref="Models.SectorPe.Pe"/>.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="sector">Narrows the answer to one sector, server-side. Omit for all eleven.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPe>> GetSectorPeSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        return transport.GetListAsync(
            new FmpRequest("stable/sector-pe-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("sector", sector?.ToQueryValue()),
            FmpJsonContext.Default.ListSectorPe, ct);
    }

    /// <summary>Every industry's average price change on one day and one exchange, from
    /// <c>stable/industry-performance-snapshot</c>.
    ///
    /// <para><b>Fewer industries come back than <see cref="DirectoryEndpoints.GetIndustriesAsync"/> lists.</b>
    /// Measured 2026-08-29 on 2026-08-28: 126 industries on NASDAQ and 128 on NYSE, against 159 documented —
    /// a union of 139. Twenty documented names answer <c>[]</c> on every exchange, so passing that list
    /// through unfiltered produces an empty result for one name in eight, indistinguishable from a
    /// typo.</para>
    ///
    /// <para><b>The out-of-range date behaviour documented on
    /// <see cref="GetSectorPerformanceSnapshotAsync"/> applies here.</b></para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="industry">Narrows the answer to one industry, server-side, using FMP's own label. Omit for
    /// all of them. Labels carrying <c>&amp;</c> and <c>,</c> are URL-encoded for you.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace; or
    /// <paramref name="industry"/> was supplied and is empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPerformance>> GetIndustryPerformanceSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (industry is not null) ArgumentException.ThrowIfNullOrWhiteSpace(industry);

        return transport.GetListAsync(
            new FmpRequest("stable/industry-performance-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("industry", industry),
            FmpJsonContext.Default.ListIndustryPerformance, ct);
    }

    /// <summary>Every industry's aggregate price-to-earnings ratio on one day and one exchange, from
    /// <c>stable/industry-pe-snapshot</c>.
    ///
    /// <para><b>Twelve of 254 measured rows read <c>pe: 0</c></b>, which means "no meaningful aggregate" rather
    /// than a ratio of zero — see <see cref="Models.SectorPe.Pe"/>. Every one of the twelve was an industry
    /// row; no sector row carried a zero.</para>
    ///
    /// <para>The vocabulary gap documented on <see cref="GetIndustryPerformanceSnapshotAsync"/> and the
    /// out-of-range date behaviour documented on <see cref="GetSectorPerformanceSnapshotAsync"/> both apply
    /// here.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="industry">Narrows the answer to one industry, server-side. Omit for all of them.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace; or
    /// <paramref name="industry"/> was supplied and is empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPe>> GetIndustryPeSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (industry is not null) ArgumentException.ThrowIfNullOrWhiteSpace(industry);

        return transport.GetListAsync(
            new FmpRequest("stable/industry-pe-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("industry", industry),
            FmpJsonContext.Default.ListIndustryPe, ct);
    }
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: PASS, 24 tests.

If `The_sector_filter_goes_out_as_FMPs_own_label` fails on the encoding, check what `FmpRequest.With(string, string?)` emits for a space — the assertion expects `%20`. Adjust the assertion to whatever the existing builder actually produces for other space-carrying values (`FmpRequestTests` has precedent); do **not** change the builder.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: still exactly one failure, the known `EndpointCoverageTests` one.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs tests/FmpDotNet.Tests/MarketPerformanceTests.cs
git commit -m "feat: add the four Market Performance snapshot methods (#32)"
```

---

### Task 6: The four historical methods, and promoting the deferred crefs

**Files:**
- Modify: `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`
- Modify: `src/FmpDotNet/Sector.cs`
- Modify: `src/FmpDotNet/Models/SectorIndustryMetrics.cs`
- Modify: `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1, 3, 4, 5.
- Produces: `GetHistoricalSectorPerformanceAsync(Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)` and its three siblings `GetHistoricalSectorPeAsync`, `GetHistoricalIndustryPerformanceAsync(string industry, string exchange, LocalDate from, LocalDate to, …)`, `GetHistoricalIndustryPeAsync`. This is the last task that touches `src/`.

**Suite state:** inherits the one known `EndpointCoverageTests` failure.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/MarketPerformanceTests.cs`:

```csharp
    [Fact]
    public async Task The_historical_sector_path_always_sends_a_window()
    {
        // The point of requiring `from` and `to`: omitting them upstream returns 2024-02-01..2024-03-01,
        // measured 2026-08-29 — thirty months stale, at HTTP 200, with nothing in the body saying so.
        // `from` defaults to 2024-02-01 and `to` to 2024-03-01, both hard-coded, and `limit=100` does not move
        // them. Non-nullable parameters are how that default becomes unreachable.
        var (endpoints, handler) = Build();

        await endpoints.GetHistoricalSectorPerformanceAsync(
            Sector.Technology, "NASDAQ", new LocalDate(2026, 8, 1), new LocalDate(2026, 8, 28));

        Assert.Equal("/stable/historical-sector-performance", handler.Requests[0].AbsolutePath);
        var query = handler.Requests[0].Query;
        Assert.Contains("sector=Technology", query);
        Assert.Contains("exchange=NASDAQ", query);
        Assert.Contains("from=2026-08-01", query);
        Assert.Contains("to=2026-08-28", query);
    }

    [Theory]
    [InlineData("sector-pe", "/stable/historical-sector-pe")]
    [InlineData("industry-performance", "/stable/historical-industry-performance")]
    [InlineData("industry-pe", "/stable/historical-industry-pe")]
    public async Task Each_remaining_historical_path_is_asked_by_name(string which, string expected)
    {
        var (endpoints, handler) = Build();
        var from = new LocalDate(2026, 8, 1);
        var to = new LocalDate(2026, 8, 28);

        switch (which)
        {
            case "sector-pe":
                await endpoints.GetHistoricalSectorPeAsync(Sector.Technology, "NASDAQ", from, to); break;
            case "industry-performance":
                await endpoints.GetHistoricalIndustryPerformanceAsync("Steel", "NASDAQ", from, to); break;
            default:
                await endpoints.GetHistoricalIndustryPeAsync("Steel", "NASDAQ", from, to); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task A_backwards_range_is_rejected_before_the_request_goes_out()
    {
        // Measured 2026-08-29: `from=2026-08-28&to=2026-08-01` answers `[]` with HTTP 200 — a spent call that
        // says nothing happened. Rejecting here is the only place that reads as an error.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHistoricalSectorPerformanceAsync(
                Sector.Technology, "NASDAQ", new LocalDate(2026, 8, 28), new LocalDate(2026, 8, 1)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_industry_is_rejected_on_the_historical_path()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetHistoricalIndustryPeAsync(
                "  ", "NASDAQ", new LocalDate(2026, 8, 1), new LocalDate(2026, 8, 28)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_historical_path_binds_the_deep_history_number_formats()
    {
        var (endpoints, _) = Build(
            Binding.Fixture("market-performance-historical-sector-performance.head.json"));

        var rows = await endpoints.GetHistoricalSectorPerformanceAsync(
            Sector.Technology, "NASDAQ", new LocalDate(2000, 1, 1), new LocalDate(2016, 1, 1));

        Assert.Equal(3, rows.Count);
        Assert.Equal(0.0000005735079118365113m, rows[0].AverageChange);
        Assert.Equal(-0.0000026524148173594842m, rows[1].AverageChange);
    }
```

- [ ] **Step 2: Run the tests and confirm they fail to build**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: build failure, `CS1061` on `GetHistoricalSectorPerformanceAsync`.

- [ ] **Step 3: Add the four methods**

Add to `src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs`:

```csharp
    /// <summary>One sector's average price change over a range, on one exchange, from
    /// <c>stable/historical-sector-performance</c>.
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> are required because FMP's defaults are
    /// thirty months stale.</b> Measured 2026-08-29, omitting both returns 21 rows spanning
    /// <c>2024-02-01 … 2024-03-01</c> — HTTP 200, well-formed, and wrong for anyone who meant "recently". The
    /// two bounds were measured separately: <c>to</c> alone backfills <c>from</c> to 2024-02-01, and
    /// <c>from=2024-02-20</c> alone returns 9 rows ending at 2024-03-01. <c>limit=100</c> does not move either.
    /// Recent data is reachable and plentiful; only the defaults are stuck, so this SDK makes them
    /// unreachable.</para>
    ///
    /// <para><b>The exchange is part of the fact.</b> Measured on the same window, the NASDAQ and NYSE answers
    /// for Technology disagreed on all 20 shared dates.</para>
    ///
    /// <para>History reaches back to at least <b>2000-01-03</b>, measured 2026-08-29. No row cap was reached:
    /// a single request for 2000-01-01 to 2016-01-01 returned <b>4,025 rows</b>. Rows arrive newest
    /// first.</para>
    ///
    /// <para>An unrecognised <paramref name="exchange"/> answers <c>[]</c> with HTTP 200 rather than an
    /// error.</para></summary>
    /// <param name="sector">The sector to report on.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="sector"/> is not a declared member. Both are checked before
    /// the request is sent: FMP answers a backwards range with HTTP 200 and <c>[]</c>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPerformance>> GetHistoricalSectorPerformanceAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-sector-performance")
                .With("sector", sector.ToQueryValue())
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListSectorPerformance, ct);
    }

    /// <summary>One sector's aggregate price-to-earnings ratio over a range, on one exchange, from
    /// <c>stable/historical-sector-pe</c>.
    ///
    /// <para>The stale-default measurement on
    /// <see cref="GetHistoricalSectorPerformanceAsync"/> was taken on this path too — the same 21 rows spanning
    /// 2024-02-01 to 2024-03-01. Read that method's summary; it applies here unchanged.</para>
    ///
    /// <para><b>A <c>pe</c> of exactly <c>0</c> means "no meaningful aggregate"</b> — see
    /// <see cref="Models.SectorPe.Pe"/>.</para></summary>
    /// <param name="sector">The sector to report on.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPe>> GetHistoricalSectorPeAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-sector-pe")
                .With("sector", sector.ToQueryValue())
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListSectorPe, ct);
    }

    /// <summary>One industry's average price change over a range, on one exchange, from
    /// <c>stable/historical-industry-performance</c>.
    ///
    /// <para>The stale-default measurement on <see cref="GetHistoricalSectorPerformanceAsync"/> and the
    /// vocabulary gap on <see cref="GetIndustryPerformanceSnapshotAsync"/> both apply here. An industry FMP
    /// does not carry on the requested exchange answers <c>[]</c> with HTTP 200, indistinguishable from a
    /// typo — measured 2026-08-29 with <c>industry=Banks</c>, which is in
    /// <see cref="DirectoryEndpoints.GetIndustriesAsync"/> and returns nothing anywhere.</para></summary>
    /// <param name="industry">The industry to report on, using FMP's own label. Labels carrying <c>&amp;</c>
    /// and <c>,</c> are URL-encoded for you.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="industry"/> or <paramref name="exchange"/> is null,
    /// empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPerformance>> GetHistoricalIndustryPerformanceAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(industry);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-industry-performance")
                .With("industry", industry)
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListIndustryPerformance, ct);
    }

    /// <summary>One industry's aggregate price-to-earnings ratio over a range, on one exchange, from
    /// <c>stable/historical-industry-pe</c>.
    ///
    /// <para>Everything documented on <see cref="GetHistoricalIndustryPerformanceAsync"/> applies, and
    /// <b>a <c>pe</c> of exactly <c>0</c> means "no meaningful aggregate"</b> — see
    /// <see cref="Models.SectorPe.Pe"/>, where the twelve measured zeros are recorded.</para></summary>
    /// <param name="industry">The industry to report on, using FMP's own label.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="industry"/> or <paramref name="exchange"/> is null,
    /// empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPe>> GetHistoricalIndustryPeAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(industry);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-industry-pe")
                .With("industry", industry)
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListIndustryPe, ct);
    }
```

- [ ] **Step 4: Promote the deferred crefs**

`MarketPerformanceEndpoints` now exists, so every `<c>MarketPerformanceEndpoints</c>` written earlier can become a real cross-reference. Replace each of these:

In `src/FmpDotNet/Sector.cs`, in the `Sector` type summary:

```
so industry is a <see langword="string"/> on <c>MarketPerformanceEndpoints</c> and the caller
```
becomes
```
so industry is a <see langword="string"/> on <see cref="Endpoints.MarketPerformanceEndpoints"/> and the caller
```

In `src/FmpDotNet/Models/SectorIndustryMetrics.cs`, in the `SectorPerformance` type summary:

```
<para><b><see cref="Date"/> is not necessarily the date you asked for.</b> See the snapshot methods on
/// <c>MarketPerformanceEndpoints</c> for the measurement
```
becomes
```
<para><b><see cref="Date"/> is not necessarily the date you asked for.</b> See
/// <see cref="Endpoints.MarketPerformanceEndpoints.GetSectorPerformanceSnapshotAsync"/> for the measurement
```

Then search the whole `src/` tree for any remaining deferred cref and promote it:

```bash
grep -rn "<c>MarketPerformanceEndpoints</c>" src/FmpDotNet/
```

Expected after promotion: no matches.

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~MarketPerformanceTests`
Expected: PASS, 31 tests.

- [ ] **Step 6: Confirm the build is clean under warnings-as-errors**

Run: `dotnet build src/FmpDotNet -warnaserror`
Expected: no warnings, no errors. A CS1574 here means a promoted cref names something that does not exist — check the exact member name and namespace.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: still exactly one failure, the known `EndpointCoverageTests` one.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Endpoints/MarketPerformanceEndpoints.cs src/FmpDotNet/Sector.cs \
        src/FmpDotNet/Models/SectorIndustryMetrics.cs tests/FmpDotNet.Tests/MarketPerformanceTests.cs
git commit -m "feat: add the four historical Market Performance methods (#32)"
```

---

### Task 7: The smoke-sweep arguments

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

**Interfaces:**
- Consumes: the eleven methods from Tasks 4-6, reflected over by the sweep.
- Produces: `LiveApi.Industry`; two new dispatch arms in `Probe.Argument`; a regenerated baseline.

**Suite state:** inherits the one known `EndpointCoverageTests` failure.

**⚠ Step 4 spends roughly 178 live API calls.** Regenerating the baseline re-runs the **whole** sweep, not just the new endpoints. Run it once, deliberately, and do not retry blindly on a transient failure — a blind retry costs another full sweep.

- [ ] **Step 1: Add the `Industry` constant**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, beside the `Exchange` constant, add:

```csharp
    /// <summary>The industry the Market Performance industry paths are probed with.
    ///
    /// <para><b>Named for the reason <see cref="Exchange"/> is.</b> <c>Probe.Argument</c> maps any unrecognised
    /// string to <see cref="Symbol"/>, and <c>industry=AAPL</c> answers an empty array with HTTP 200 — so
    /// without this constant four endpoints would record <c>outcome empty</c> as their healthy baseline and
    /// agree with themselves forever.</para>
    ///
    /// <para><b>And it has to be an industry FMP actually carries.</b> Measured 2026-08-29,
    /// <c>stable/available-industries</c> lists 159 names and only 139 appear in any snapshot on either NASDAQ
    /// or NYSE; <c>Banks</c>, <c>Asset Management</c> and eighteen others answer <c>[]</c> everywhere. Picking
    /// a documented-but-empty name would reproduce exactly the silent green this constant exists to prevent.
    /// <c>Steel</c> was measured to return rows on both the snapshot and the historical paths.</para></summary>
    public const string Industry = "Steel";
```

- [ ] **Step 2: Add the two dispatch arms**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, inside the `string` switch in `Argument`, beside `"exchange" => LiveApi.Exchange,`:

```csharp
                "industry" => LiveApi.Industry,
```

And after the `TechnicalIndicatorTimeframe` arm, add:

```csharp
        // Technology and not an arbitrary member: every sector was measured present on every snapshot taken
        // 2026-08-29, so any would answer, but the design's measurement tables are keyed to Technology and a
        // sweep diff should be readable against them. There is no generic enum fallback in this method — a new
        // enum parameter with no arm here reaches `throw Unknown(parameter)`, which is the intended behaviour.
        if (type == typeof(Sector)) return Sector.Technology;
```

`date`, `from` and `to` need no new arm. The `LocalDate` default is `LiveApi.SettledWeekday`, a weekday, and only weekends were measured to answer `[]` — a market holiday did not. `from` takes `LiveApi.RangeStart`, ninety days wide, and a 28-day window already returns 20 rows.

- [ ] **Step 3: Confirm the sweep can synthesise every new argument, without calling FMP**

Run: `dotnet build tests/FmpDotNet.SmokeTests -warnaserror`
Expected: clean.

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: PASS. This test checks that every public endpoint method has synthesisable arguments and does **not** call the API. If it fails naming `industry` or `Sector`, an arm above is missing or misspelled.

- [ ] **Step 4: Regenerate the baseline — one live run**

```bash
FMP_API_KEY=$(grep '^FMP_API_KEY=' .env | cut -d= -f2-) \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
```

Do **not** `source .env` or use `set -a` — that has clobbered `PATH` for a whole shell in this repo before. Extract the one variable into the one command, as above. `FMPDOTNET_SMOKE_BULK` stays unset.

Expected: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` grows from **167** to **178** `outcome` lines.

Verify:

```bash
grep -c "^outcome" tests/FmpDotNet.SmokeTests/baseline-ordinary.txt   # expect 178
grep -c "^outcome empty" tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

- [ ] **Step 5: Read the diff before accepting it**

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | head -200
```

**`outcome empty` on any of the eleven new blocks is a failure, not a result.** Every one of the eleven paths was measured to return rows with the arguments this sweep synthesises. An empty block means an argument arm is wrong — most likely `industry`, or a `date` that landed on a non-trading day. Fix the arm and regenerate rather than accepting the baseline.

Changes to pre-existing blocks are drift and are expected; changes that flip a property from `set` to absent on an unrelated endpoint deserve a look before accepting.

- [ ] **Step 6: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "test: sweep the eleven Market Performance paths (#32)"
```

---

### Task 8: Documentation

**Files:**
- Modify: `README.md` — the generated block between the markers at lines 111 and 394, and the prose from line 396

**Interfaces:**
- Consumes: the complete facade. This task closes the expected red suite.

- [ ] **Step 1: Regenerate the coverage block**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

No API key is needed — the generator runs against a stub handler.

Expected: the block between `<!-- BEGIN GENERATED: endpoint coverage -->` and `<!-- END GENERATED: endpoint coverage -->` gains 11 rows.

- [ ] **Step 2: Confirm the coverage test is now green**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: **all green, zero failures.** The known failure from Task 4 is now resolved.

- [ ] **Step 3: Edit the prose, which is not machine-checked**

`EndpointCoverageTests` guards only the generated block. The paragraphs below it drift silently and must be edited by hand. In `README.md`, in the section `### Reaching an endpoint that is not modelled`:

Replace:

```
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **56 paths
remain**, of which **49 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest group is Market Performance (11), then News (10)
and Fundraisers & DCF (10); ETF & Mutual Funds and Indexes & Market Hours carry 9 apiece.
```

with:

```
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **45 paths
remain**, of which **38 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are News (10) and Fundraisers & DCF (10);
ETF & Mutual Funds and Indexes & Market Hours carry 9 apiece.
```

Replace:

```
That remainder is tracked as six issues under the epic, five of them actionable, each 9 to 12 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 187 modelled plus 56 remaining, with no path counted twice and none missing.
```

with:

```
That remainder is tracked as five issues under the epic, four of them actionable, each 7 to 10 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 198 modelled plus 45 remaining, with no path counted twice and none missing.
```

Two things to note about that second edit. `243 − 198 = 45` and `45 − 7 = 38`, so the arithmetic closes. And the phrase "each 9 to 12 paths" was **already wrong before this change** — the six issues it described ranged from 7 (#41) to 11 (#32). The replacement states the true range for the five that remain: #41 at 7, #34 and #38 at 9, #33 and #39 at 10.

- [ ] **Step 4: Check no other prose in the README contradicts the new numbers**

```bash
grep -nE "\b(56|49|187|six issues|five of them actionable)\b" README.md
```

Expected: no match inside the prose sections. A match inside the generated block is fine — that block is regenerated, not hand-edited. Investigate anything else the grep finds.

- [ ] **Step 5: Confirm the whole suite and a warnings-as-errors build**

```bash
dotnet build -warnaserror
dotnet test tests/FmpDotNet.Tests
```

Expected: clean build, all tests green, zero failures, zero skips.

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: record Market Performance coverage, 187 -> 198 of 243 (#32)"
```

---

## After the plan

Issue #25's body carries the same arithmetic as the README and will be stale: its headline (56/49), its "six open children" partition sentence and its `243 − 187` reconciliation, plus a row that moves from the remainder table into Shipped. Issue #32 needs closing. Both are post-merge housekeeping rather than plan tasks — they touch GitHub, not the repository.
