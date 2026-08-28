# Form 13F and Insider Trades Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model the fourteen documented `stable/` paths FMP files under Form 13F and Insider Trades, taking SDK coverage from 140 of 243 paths to 154.

**Architecture:** Two new facades — `fmp.InstitutionalOwnership` (9 paths) and `fmp.InsiderTrades` (5) — bringing the client to thirteen groups. `stable/acquisition-of-beneficial-ownership` is deliberately filed with the institutional nine rather than the insider five: it is an SC 13D/G stake disclosure keyed by symbol, not a Form 4 transaction. Every one of the fourteen is an ordinary `GET` returning a JSON array that `FmpTransport.GetListAsync` already serves — no new transport primitive, no streaming, no CSV, and **no new converter**. Thirteen new records, 195 fields, thirteen `FmpJsonContext` entries. Every type is built from the 2026-08-28 measurement pass rather than from FMP's documentation, and each measured trap gets a test that fails when the trap is reintroduced.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` source generation via `FmpJsonContext`, NodaTime (`LocalDate`, `LocalDateTime`), xUnit v2 (2.9.3).

**Spec:** `docs/superpowers/specs/2026-08-28-form-13f-and-insider-trades-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-28-form-13f-and-insider-trades-measurements.md`

## Five departures from the spec — three of them measured corrections

The spec is the authority everywhere it is not listed here. The first three claims below are wrong in it, were
re-measured on 2026-08-28, and **this plan's text wins where they disagree.** Each is carried into the task that
implements it.

1. **`MaxOwnershipPageSize = 1000` is not one cap — it is three, and one of them is 100.** The spec names a
   single constant for all five paged methods. The measurements doc records `limit=1000` honoured and
   explicitly lists "the upper bound of `limit`" as unmeasured. Measured 2026-08-28 by asking for more:

   | path | `limit=200` | `limit=1000` | `limit=2000` | `limit=5000` |
   |---|---|---|---|---|
   | `insider-trading/latest` | — | 1000 | 1000 | 1000 |
   | `insider-trading/search` | — | 1000 | 1000 | — |
   | `institutional-ownership/latest` | — | 1000 | 1000 | — |
   | `institutional-ownership/extract-analytics/holder` | **100** | **100** | **100** | — |

   Every over-cap response was HTTP 200 with nothing in the body to say it had been trimmed, and the
   `insider-trading/latest` bodies at 2000 and 5000 were byte-identical. `extract-analytics/holder` honours
   `limit=5` (5 rows) and clamps everything from 200 up to exactly 100. So the plan ships **three** constants:
   `MaxOwnershipPageSize = 1000`, `MaxHolderAnalyticsPageSize = 100`, `MaxInsiderTradePageSize = 1000`.

2. **`acquisition-of-beneficial-ownership`'s cap is unmeasured and must be documented as such.** `limit=2000`
   returned 99 rows for AAPL — the whole result set, not a clamp. The widest set found on this path was 180 rows
   (GME). No query large enough to provoke a cap was found, so `GetBeneficialOwnershipAsync` guards at
   `MaxOwnershipPageSize` as a **sibling-derived bound**, and says so in its doc comment rather than claiming a
   measurement it does not have.

3. **`FmpJsonContext` gains thirteen entries, not "thirteen plus `SymbolPositions`".** The spec hedges that
   `SymbolPositions` "needs both `List<SymbolPositions>` … and `SymbolPositions` if unwrapped through
   `GetObjectAsync`". It is not unwrapped that way — `GetSymbolPositionsAsync` calls `GetListAsync` and takes
   `rows[0]`, exactly as `SecFilingsEndpoints.GetProfileAsync` does, and `FmpJsonContext` carries only
   `List<SecProfile>` for that precedent. Thirteen `List<T>` entries, no bare-type entry.

Two further departures are decisions rather than corrections, and each is argued where it lands:

4. **Two sweep-argument cases cannot wait for Task 10.** The spec defers every argument-synthesiser change to
   the late sweep task. Two of them redden a keyless test the moment the first quarter-taking method ships, so
   they land in **Task 2**. See the Global Constraints below.
5. **`insider-trading/reporting-name` gets its own `LiveApi` constant.** The spec says it "works by luck" and
   should be left aliased to `LiveApi.AcquirerNameQuery` with a comment. `LiveApi.CompanyNameQuery` already
   settled this exact question in the other direction, and says why: two endpoints spelling the same word must
   not share one constant. The honesty goes in the doc comment, not in the coupling. See **Task 10**.

## Global Constraints

- `TreatWarningsAsErrors=true` (`Directory.Build.props`) covers `CS*` and `NU*`. `IsAotCompatible` turns IL2026/IL3050 into build errors — never call a reflection-based `JsonSerializer.Deserialize`; every model goes through `FmpJsonContext`.
- **Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as `[JsonSerializable(typeof(List<X>))]` or it fails at runtime, not at compile time.** Thirteen entries are added across this plan: `FilingQuarter`, `InstitutionalHolding`, `HolderAnalytics`, `HolderIndustryBreakdown`, `HolderPerformance`, `IndustryOwnershipSummary`, `InstitutionalFiling`, `SymbolPositions`, `BeneficialOwnership`, `InsiderTrade`, `InsiderTradeStatistics`, `InsiderReportingName`, `InsiderTransactionType`.
- Models are `public sealed record` with `init` properties and an explicit `[JsonPropertyName]` on every member. **No `required` members and no non-nullable properties** — an absent JSON key binds an `init` member to `default` rather than honouring a field initialiser.
- **Every money, share and percentage field is `decimal?`.** This is deliberately against the local evidence: `marketValue`, `value`, `performance` and their siblings were integral on all 7,946 rows sampled, and `long?` is the obvious read. `industryValue` on `industry-summary` is the same kind of quantity and is fractional on 53 of 394 rows (`523604028974.8208`), `securitiesOwned` is fractional on 5.9% of insider rows and `securitiesTransacted` on 4.0%. `System.Text.Json` **throws** on a fractional value bound to an integer property and `FmpTransport` does not wrap `DeserializeAsync`, so one such field costs the caller the whole response rather than the one field. This is the `CompanyProfile.Volume` defect, corrected 2026-08-28 after it broke a live sweep; it is not being repeated.
- **Genuine counts stay `int?`, and the list is closed:** `year`, `quarter`, `portfolioSize`, `securitiesAdded`, `securitiesRemoved`, `holdingPeriod`, `averageHoldingPeriod`, `averageHoldingPeriodTop10`, `averageHoldingPeriodTop20`, `investorsHolding`, `lastInvestorsHolding`, `investorsHoldingChange`, `newPositions`, `lastNewPositions`, `newPositionsChange`, `increasedPositions`, `lastIncreasedPositions`, `increasedPositionsChange`, `closedPositions`, `lastClosedPositions`, `closedPositionsChange`, `reducedPositions`, `lastReducedPositions`, `reducedPositionsChange`, `acquiredTransactions`, `disposedTransactions`, `totalPurchases`, `totalSales`. Nothing else. `totalCalls`, `totalPuts` and their variants are option **contract** counts that `int?` would hold, but they take `decimal?` for consistency with every other quantity on `SymbolPositions`.
- `cik`, `reportingCik`, `companyCik` and `securityCusip` are **`string?`, never an integer type.** Measured 2026-08-28, every one arrives zero-padded to ten characters (`"0000320193"`); the padding is the value. This follows `IndustryClassification.Cik` and `SecFiling.Cik`.
- **Dates: one converter for twelve records, two for the thirteenth.** Every date in this slice is bare ISO (`"2026-08-14"`) and takes `NullableLocalDateJsonConverter` — **except** `InstitutionalFiling`, where `filingDate` arrives as `"2026-08-28 00:00:00"` (midnight on 1000 of 1000 rows) and takes `NullableDateAtMidnightJsonConverter`, and `acceptedDate` arrives as `"2026-08-28 15:47:03"` (midnight on 0 of 1000) and takes `NullableLocalDateTimeJsonConverter` as a `LocalDateTime?`. **Getting this wrong is silent:** `NullableLocalDateJsonConverter` parses with `LocalDatePattern.Iso` and returns null on a parse failure rather than throwing (`NodaConverters.cs:35-48`), so pointing it at `institutional-ownership/latest` nulls every date with no exception and nothing in a diff. Task 6 owns the test that makes it loud.
- **No new converter.** The shipped set covers every shape in this slice. `TolerantDecimalJsonConverter` reads `BeneficialOwnership`'s six JSON-string numerics as shipped.
- Every public member carries XML documentation in house style: it records **what was measured and on what date** (every measurement in this slice is 2026-08-28 against an Ultimate key), and states plainly anything a caller would otherwise get wrong. Where a value is a trap, the documentation is the deliverable, not decoration.
- Public list-returning methods return `IReadOnlyList<T>`, never null. `GetSymbolPositionsAsync` returns `SymbolPositions?` and is the one single-record lookup, unwrapped as `SecFilingsEndpoints.GetProfileAsync` does it (`SecFilingsEndpoints.cs:49-56`): `GetListAsync`, then `rows.Count > 0 ? rows[0] : null`. **Not `GetObjectAsync`** — the wire shape really is an array.
- **A signature must not accept a parameter the endpoint ignores.** Nine of the fourteen paths accept `limit` and ignore it (`extract` returns all 4,177 rows for `limit=5`, byte-identical to no limit at all), and `acquisition-of-beneficial-ownership` honours `limit` while ignoring `page` (`page=0` and `page=1` returned byte-identical bodies). `limit` therefore appears on five methods and `page` on four.
- Guards: `ArgumentException.ThrowIfNullOrWhiteSpace` on every required `string`; `ArgumentOutOfRangeException.ThrowIfNegative(page)`, `ThrowIfNegativeOrZero(limit)`, `ThrowIfGreaterThan(limit, <the cap for that path>)` on the paged methods; `quarter` validated to 1–4. **`year` is not range-checked** — an out-of-range year answers `[]` with HTTP 200, a legitimate "no data", and inventing a floor would invent a fact.
- **`ArgumentNullException` for a null string, `ArgumentException` for a blank one.** `ArgumentException.ThrowIfNullOrWhiteSpace(null)` throws the former; `Assert.ThrowsAsync<T>` matches the exception type exactly, so a null case and a blank case need two separate `[Fact]`s.
- Tests are xUnit `[Fact]`/`[Theory]` with sentence-style method names using underscores, matching `SecFilingsTests`.
- **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the response after reading the body. A test driving N calls builds N responses.
- Fixtures are captures from the 2026-08-28 measurement pass, two or three rows each, and **must not contain the API key**. The key travels in the query string, so never write a built URL into a fixture, a test, or a log line. The `Fixtures\*.json` glob in `FmpDotNet.Tests.csproj` copies them automatically — no csproj change is needed.
- Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore. A mutation that fails to compile is a stronger result than a failing test — record it as such.
- **`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` goes red at Task 1 and stays red until Task 11.** It compares the README's generated table against the paths the code actually requests, so it fails the moment the first new endpoint ships and cannot pass again until the table is regenerated. Every per-task run below is filtered to the tests that task owns; a full-suite run between Task 1 and Task 11 is expected to show exactly that one failure and no other.
- **Two argument-synthesiser cases cannot be deferred to Task 10, and the spec is wrong to imply they can.** Both harnesses dispatch `int` parameters by name and neither knows `quarter`: `Probe.Argument`'s `int` arm ends `_ => throw Unknown(parameter)` (`Probe.cs:404`), which reddens the keyless `SweepCoverageTests.The_sweep_can_supply_arguments_for_every_endpoint_method`; and `EndpointCoverageTests.Argument`'s `int` arm ends `_ => 0`, which the 1–4 guard rejects, so the method requests nothing, drops out of the coverage table, and reddens `Every_public_endpoint_method_reaches_the_api`. Both cases land in **Task 2**, with the first quarter-taking method. The `string` cases (`reportingCik`, `companyCik`, `transactionType`) and the filer-CIK dispatch stay in Task 10, because those fall through to a default that is silently wrong rather than loudly absent — which is exactly what makes them Task 10's subject.
- **A `<see cref>` that does not resolve is a build error, so no doc comment may reference a type or member a later task introduces.** `FmpDotNet.csproj` sets `GenerateDocumentationFile` and `Directory.Build.props` sets `TreatWarningsAsErrors`, which turns CS1574 into an error — and the records in this slice genuinely cross-reference each other in both directions, so ordering alone cannot resolve it. Fourteen references are therefore written as plain `<c>Name</c>` at first write and **promoted to `<see cref>` in Task 9, Step 7**, once every symbol exists. The code blocks below are written that way already; do not "fix" a `<c>` into a `cref` before Task 9.
- Work happens on a branch off `master`. `master` carries a ruleset requiring a pull request and the `.NET — build + test` check, so the path is branch → PR → green → merge. Suggested branch name: `feat/form-13f-and-insider-trades`.

## File Structure

**Create:**
- `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — the first new facade, 9 methods over 9 paths
- `src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs` — the second new facade, 5 methods over 5 paths
- `src/FmpDotNet/Models/FilingQuarter.cs` — `FilingQuarter` (3 fields)
- `src/FmpDotNet/Models/InstitutionalHolding.cs` — `InstitutionalHolding` (14)
- `src/FmpDotNet/Models/HolderAnalytics.cs` — `HolderAnalytics` (39)
- `src/FmpDotNet/Models/HolderSummaries.cs` — `HolderIndustryBreakdown` (12) and `HolderPerformance` (33)
- `src/FmpDotNet/Models/SymbolPositions.cs` — `SymbolPositions` (36)
- `src/FmpDotNet/Models/InstitutionalFiling.cs` — `IndustryOwnershipSummary` (3) and `InstitutionalFiling` (8)
- `src/FmpDotNet/Models/BeneficialOwnership.cs` — `BeneficialOwnership` (15)
- `src/FmpDotNet/Models/InsiderTrade.cs` — `InsiderTrade` (16)
- `src/FmpDotNet/Models/InsiderTradeStatistics.cs` — `InsiderTradeStatistics` (13), `InsiderReportingName` (2), `InsiderTransactionType` (1)
- `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs` — the facade, the quarter guard, and the six institutional records that are not the market-wide pair
- `tests/FmpDotNet.Tests/InstitutionalFilingTests.cs` — the market-wide pair and the date trap
- `tests/FmpDotNet.Tests/BeneficialOwnershipTests.cs` — the string numerics and the `limit`-without-`page` path
- `tests/FmpDotNet.Tests/InsiderTradesTests.cs` — all five insider paths
- 14 fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modify:**
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +13 entries
- `src/FmpDotNet/FmpClient.cs` — +`InstitutionalOwnership` and +`InsiderTrades` properties
- `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — +2 registrations
- `tests/FmpDotNet.Tests/AddFmpTests.cs` — +2 assertions, group count 11 → 13
- `tests/FmpDotNet.Tests/EndpointCoverageTests.cs` — `Argument()` gains a `quarter` case (Task 2)
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — +4 constants (Tasks 2 and 10)
- `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()` gains `quarter` (Task 2), then `reportingCik`, `companyCik`, `transactionType` and the filer-CIK dispatch (Task 10)
- `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` — +1 keyless guard (Task 10)
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — +14 blocks (Task 11). **`baseline-bulk.txt` is not touched.**
- `README.md` — regenerated coverage table, and the prose counts 140 → 154, 103 → 89, 96 → 82 (Task 11)

---

### Task 1: `FilingQuarter`, and the `fmp.InstitutionalOwnership` facade it arrives on

**Files:**
- Create: `src/FmpDotNet/Models/FilingQuarter.cs`
- Create: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-dates.BRK.json`
- Create: `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`

**Interfaces:**
- Consumes: `FmpTransport.GetListAsync`, `FmpRequest`, `NullableLocalDateJsonConverter`, `Binding.Fixture`, `Binding.Unbound<T>`, `StubHandler`.
- Produces: `public sealed record FilingQuarter` with `LocalDate? Date`, `int? Year`, `int? Quarter`;
  `public sealed class InstitutionalOwnershipEndpoints(FmpTransport transport)` with
  `Task<IReadOnlyList<FilingQuarter>> GetFilingDatesAsync(string cik, CancellationToken ct = default)`;
  `FmpClient.InstitutionalOwnership`. Tasks 2–7 add methods to this same class; Task 2 adds the private
  `ThrowIfQuarterOutOfRange` guard.

This is the facade's first method and the one a caller reaches for first: it is the only path that enumerates
which `year`/`quarter` pairs a filer actually has, and the other four quarter-keyed methods require both.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-dates.BRK.json` — the first three rows of
`stable/institutional-ownership/dates?cik=0001067983` (Berkshire Hathaway), captured 2026-08-28, verbatim. The
full response was 53 rows:

```json
[
  {
    "date": "2026-06-30",
    "year": 2026,
    "quarter": 2
  },
  {
    "date": "2026-03-31",
    "year": 2026,
    "quarter": 1
  },
  {
    "date": "2025-12-31",
    "year": 2025,
    "quarter": 4
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`. This file grows over Tasks 1–5; this step writes the
header and the filing-dates section only.

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The 13F records and the facade that serves them, checked against captures taken live 2026-08-28.
///
/// <para><b>The type choices here are deliberately made against the local evidence, and that is what these tests
/// pin.</b> Every money and share field is <c>decimal?</c> although all 7,946 rows sampled from <c>extract</c>
/// and <c>extract-analytics/holder</c> carried integral values — because <c>industryValue</c> on the sibling
/// <c>industry-summary</c> path is fractional on 53 of 394 rows, and because binding a fractional value to an
/// integer property makes <c>System.Text.Json</c> throw, costing the caller the whole response rather than the
/// one field.</para></summary>
public class InstitutionalOwnershipTests
{
    private static (InstitutionalOwnershipEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InstitutionalOwnershipEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- institutional-ownership/dates --------------------------------------------------------------------------

    [Fact]
    public void A_captured_filing_quarter_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-dates.BRK.json"),
            FmpJsonContext.Default.ListFilingQuarter)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(2, rows[0].Quarter);
    }

    [Fact]
    public void A_filing_quarters_date_is_the_quarter_end_not_the_filing_date()
    {
        // Measured 2026-08-28 over Berkshire's 53 quarters: every `date` is a calendar quarter end, and the
        // year/quarter pair always agrees with it. That is what makes this endpoint the index for the other
        // four — a caller reads `Year` and `Quarter` off a row here and passes them straight back.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-dates.BRK.json"),
            FmpJsonContext.Default.ListFilingQuarter)!;

        Assert.All(rows, r =>
        {
            var quarterEnd = new LocalDate(r.Year!.Value, r.Quarter!.Value * 3, 1)
                .With(DateAdjusters.EndOfMonth);
            Assert.Equal(quarterEnd, r.Date);
        });
    }

    [Fact]
    public async Task The_filing_dates_call_sends_only_the_cik()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetFilingDatesAsync("0001067983");

        Assert.Equal("/stable/institutional-ownership/dates", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        // No limit and no page: measured 2026-08-28, this path ignores both.
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_blank_cik_is_refused_before_a_request_goes_out()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetFilingDatesAsync("   "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_cik_is_refused_with_the_other_exception_type()
    {
        // Two facts, two [Fact]s: ArgumentException.ThrowIfNullOrWhiteSpace(null) throws ArgumentNullException,
        // and Assert.ThrowsAsync<T> matches the type exactly rather than by assignability. Folding this into the
        // test above would pass for the blank case and silently stop checking the null one.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetFilingDatesAsync(null!));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: FAIL to compile — `FilingQuarter`, `InstitutionalOwnershipEndpoints` and
`FmpJsonContext.Default.ListFilingQuarter` do not exist.

- [ ] **Step 4: Write `FilingQuarter`**

`src/FmpDotNet/Models/FilingQuarter.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One quarter a 13F filer has reported, from <c>stable/institutional-ownership/dates</c>.
///
/// <para><b>The index for the rest of the group.</b> Four of the nine paths on
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints"/> require a <c>year</c> and a <c>quarter</c>, and FMP
/// answers an unfiled pair with an empty array and HTTP 200 rather than an error. This is the only path that
/// says which pairs exist, which is why a caller starts here: read <see cref="Year"/> and <see cref="Quarter"/>
/// off a row and pass them straight back.</para>
///
/// <para>Measured 2026-08-28 for Berkshire Hathaway (CIK <c>0001067983</c>): 53 rows, newest first, every one a
/// calendar quarter end agreeing with its own year and quarter.</para></summary>
public sealed record FilingQuarter
{
    /// <summary>The quarter end the filing covers — <c>2026-06-30</c>, not the date it was filed. Bare ISO on
    /// this path; see <c>InstitutionalFiling.FilingDate</c> for the one path in this group that spells
    /// dates differently.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The calendar year of <see cref="Date"/>. A genuine count of nothing — it is a label — and
    /// <see cref="int"/> rather than <c>decimal</c> for that reason.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter of <see cref="Date"/>, 1 to 4.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }
}
```

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs` — add above the `// The five below were built for the *-bulk`
comment block, keeping the new entries together:

```csharp
[JsonSerializable(typeof(List<FilingQuarter>))]
```

- [ ] **Step 6: Write the facade**

`src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Form 13F</c> group — who owns what, as institutions report it quarterly to the SEC.
///
/// <para><b>Nine paths: the eight FMP files under Form 13F, plus <c>acquisition-of-beneficial-ownership</c>,
/// which FMP files under Insider Trades.</b> That one is an SC 13D/G filing — the disclosure an investor makes
/// on crossing 5% of a class. Its subject is an institutional stake, its fields are voting and dispositive
/// power, and its reporting person is an entity (<c>"The Vanguard Group"</c>). It shares nothing with a Form 4
/// transaction but the word "ownership", so it is here rather than on
/// <c>InsiderTradesEndpoints</c>. <see cref="SecFilingsEndpoints"/> set that precedent, sending three of
/// its twelve documented paths to <see cref="DirectoryEndpoints"/> and <see cref="SearchEndpoints"/>: this SDK
/// files a path by what it returns.</para>
///
/// <para><b>Start at <see cref="GetFilingDatesAsync"/>.</b> Five of the nine take a <c>year</c> and a
/// <c>quarter</c>, all five reject a call that omits <c>quarter</c> with
/// <c>400 … missing query parameter - quarter</c>, and an unfiled pair answers <c>[]</c> with HTTP 200 rather
/// than an error. That path is the only one that enumerates the pairs that exist.</para>
///
/// <para><b>Two kinds of CIK reach this class and they are not interchangeable.</b> The four <c>cik</c>-keyed
/// methods want an institutional <i>filer's</i> CIK — Berkshire's <c>0001067983</c>. An <i>issuer's</i> CIK,
/// which is what <see cref="SecFilingsEndpoints.GetProfileByCikAsync"/> takes, answers <c>[]</c> on all four:
/// measured 2026-08-28, Apple's <c>320193</c> returned zero rows from every one of them.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class InstitutionalOwnershipEndpoints(FmpTransport transport)
{
    /// <summary>Every quarter one 13F filer has reported, newest first —
    /// <c>stable/institutional-ownership/dates</c>.
    ///
    /// <para><b>Call this before the four quarter-keyed methods.</b> They answer an unfiled <c>year</c>/
    /// <c>quarter</c> pair with an empty list and HTTP 200, so a caller who guesses a pair cannot tell "this
    /// filer reported nothing that quarter" from "this filer has not filed yet". This path answers that
    /// question directly.</para>
    ///
    /// <para><b>No <c>limit</c> and no <c>page</c>, because the endpoint honours neither.</b> Measured
    /// 2026-08-28, Berkshire answered all 53 quarters with and without <c>limit=5</c>.</para></summary>
    /// <param name="cik">The institutional filer's SEC Central Index Key, padded or unpadded — both work,
    /// measured 2026-08-28. <b>Not an issuer's CIK</b>; see the note on this class.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The filer's quarters, newest first. Never <see langword="null"/>; empty for a CIK that has
    /// filed no 13F, which includes every issuer CIK.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FilingQuarter>> GetFilingDatesAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/dates").With("cik", cik),
            FmpJsonContext.Default.ListFilingQuarter, ct);
    }
}
```

- [ ] **Step 7: Wire the facade into the client and DI**

`src/FmpDotNet/FmpClient.cs` — add `InstitutionalOwnershipEndpoints institutionalOwnership` to the primary
constructor's parameter list (after `secFilings`), and the property after `SecFilings`:

```csharp
    /// <summary>Who owns what, as institutions report it quarterly on Form 13F — holdings, holder analytics,
    /// performance and industry breakdowns, plus SC 13D/G beneficial-ownership disclosures.
    ///
    /// <para>The 5% stake disclosures FMP files under Insider Trades are here rather than on
    /// <c>InsiderTrades</c>, because an SC 13D/G is an institutional stake filing and not a Form 4
    /// transaction. See <see cref="InstitutionalOwnershipEndpoints"/>.</para></summary>
    public InstitutionalOwnershipEndpoints InstitutionalOwnership { get; } = institutionalOwnership;
```

`src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — after the `SecFilingsEndpoints` line:

```csharp
        services.TryAddTransient<InstitutionalOwnershipEndpoints>();
```

- [ ] **Step 8: Update `AddFmpTests`**

`tests/FmpDotNet.Tests/AddFmpTests.cs` — add the assertion after `Assert.NotNull(client.SecFilings);`, and bump
the group count. The count assertion is the one that matters: it is what caught three groups going unasserted
when `SecFilings` was added.

```csharp
        Assert.NotNull(client.InstitutionalOwnership);
```

and change `Assert.Equal(11, typeof(FmpClient)` to `Assert.Equal(12, typeof(FmpClient)`. Task 8 takes it to 13.

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~InstitutionalOwnershipTests|FullyQualifiedName~AddFmpTests"`
Expected: PASS.

- [ ] **Step 10: Mutation-check the two guards**

Three mutations, each restored immediately:

1. Delete `ArgumentException.ThrowIfNullOrWhiteSpace(cik);` from `GetFilingDatesAsync` →
   `A_blank_cik_is_refused_before_a_request_goes_out` and `A_null_cik_is_refused_with_the_other_exception_type`
   both fail. Restore.
2. Change `Assert.ThrowsAsync<ArgumentNullException>` to `Assert.ThrowsAsync<ArgumentException>` in the null
   test → it still passes, **and that is the point**: `ArgumentNullException` derives from `ArgumentException`
   but `ThrowsAsync` matches exactly, so the loose assertion would let a null-check regression through. Restore
   the strict form. Record this as a mutation that documents why the two tests are separate.
3. Change `.With("cik", cik)` to `.With("symbol", cik)` → `The_filing_dates_call_sends_only_the_cik` fails on
   the `Assert.Contains`. Restore.

- [ ] **Step 11: Commit**

```bash
git add src/FmpDotNet/Models/FilingQuarter.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs \
        tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-dates.BRK.json
git commit -m "feat: add fmp.InstitutionalOwnership with the 13F filing-quarter index

The twelfth facade, and the path a caller starts at: four of its nine methods
require a year and a quarter, FMP answers an unfiled pair with [] and HTTP 200
rather than an error, and this is the only path that enumerates which pairs a
filer actually has."
```

### Task 2: `InstitutionalHolding` — the null symbol, the blank field, and the `limit` that does nothing

**Files:**
- Create: `src/FmpDotNet/Models/InstitutionalHolding.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract.head.json`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+GetHoldingsAsync`, `+ThrowIfQuarterOutOfRange`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`
- Modify: `tests/FmpDotNet.Tests/EndpointCoverageTests.cs` — `Argument()` gains `quarter`
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` — `+SettledQuarter`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()` gains `quarter`

**Interfaces:**
- Consumes: `InstitutionalOwnershipEndpoints` (Task 1), `NullableLocalDateJsonConverter`.
- Produces: `public sealed record InstitutionalHolding` (14 fields);
  `Task<IReadOnlyList<InstitutionalHolding>> GetHoldingsAsync(string cik, int year, int quarter, CancellationToken ct = default)`;
  `private static void ThrowIfQuarterOutOfRange(int quarter)` — used by Tasks 3, 4 and 5;
  `LiveApi.SettledQuarter` — used by Task 10.

**This task is where both argument synthesisers stop compiling clean, and both fixes belong here.**
`GetHoldingsAsync` is the first method in the slice to take a `quarter`. `Probe.Argument`'s `int` arm ends
`_ => throw Unknown(parameter)` (`Probe.cs:404`), so the keyless
`SweepCoverageTests.The_sweep_can_supply_arguments_for_every_endpoint_method` goes red on this commit.
`EndpointCoverageTests.Argument`'s `int` arm ends `_ => 0`, which the 1–4 guard rejects — the call requests
nothing, drops out of the coverage table, and reddens `Every_public_endpoint_method_reaches_the_api`. Neither is
deferrable to Task 10.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract.head.json` — three rows of
`stable/institutional-ownership/extract?cik=0000093751&year=2026&quarter=2` (State Street), captured 2026-08-28.
The full response was 4,177 rows. **Rows 1 and 2 are the capture's first two rows and both carry a null
`symbol`; row 3 is the capture's row 16, the first that has one.** That selection is the trap: a 13F holding
need not have a ticker, and `symbol` was null on 2,209 of 7,346 rows measured — 30.1%. `putCallShare` is `""` on
all three, and was `""` on all 7,346:

```json
[
  {
    "date": "2026-06-30",
    "filingDate": "2026-08-07",
    "acceptedDate": "2026-08-07",
    "cik": "0000093751",
    "securityCusip": "10170A100",
    "symbol": null,
    "nameOfIssuer": "BOUNDLESS BIO INC",
    "shares": 15962,
    "titleOfClass": "COM",
    "sharesType": "SH",
    "putCallShare": "",
    "value": 39905,
    "link": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/0000093751-26-000507-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/XML_Infotable.xml"
  },
  {
    "date": "2026-06-30",
    "filingDate": "2026-08-07",
    "acceptedDate": "2026-08-07",
    "cik": "0000093751",
    "securityCusip": "29103W104",
    "symbol": null,
    "nameOfIssuer": "EMERALD HOLDING INC",
    "shares": 244374,
    "titleOfClass": "COM",
    "sharesType": "SH",
    "putCallShare": "",
    "value": 1231645,
    "link": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/0000093751-26-000507-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/XML_Infotable.xml"
  },
  {
    "date": "2026-06-30",
    "filingDate": "2026-08-07",
    "acceptedDate": "2026-08-07",
    "cik": "0000093751",
    "securityCusip": "100557107",
    "symbol": "SAM",
    "nameOfIssuer": "BOSTON BEER INC",
    "shares": 314732,
    "titleOfClass": "CL A",
    "sharesType": "SH",
    "putCallShare": "",
    "value": 55807630,
    "link": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/0000093751-26-000507-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/93751/000009375126000507/XML_Infotable.xml"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`:

```csharp
    // ---- institutional-ownership/extract ------------------------------------------------------------------------

    [Fact]
    public void A_captured_holding_binds_twelve_of_its_fourteen_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.Equal(3, rows.Count);
        // The two absences are both measured, and neither is a defect: `symbol` is null on 30.1% of rows and
        // `putCallShare` was blank on all 7,346 rows of this path.
        Assert.Equal(["PutCallShare", "Symbol"], Binding.Unbound(rows[0]));
        Assert.Equal("0000093751", rows[0].Cik);
        Assert.Equal("10170A100", rows[0].SecurityCusip);
        Assert.Equal("BOUNDLESS BIO INC", rows[0].NameOfIssuer);
        Assert.Equal("COM", rows[0].TitleOfClass);
        Assert.Equal("SH", rows[0].SharesType);
        Assert.Equal(15962m, rows[0].Shares);
        Assert.Equal(39905m, rows[0].Value);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].AcceptedDate);
    }

    [Fact]
    public void A_holding_without_a_ticker_keeps_every_other_field()
    {
        // The trap. Measured 2026-08-28, `symbol` was null on 2,209 of 7,346 rows — 30.1%. Bonds, warrants and
        // private placements are 13F-reportable and have no ticker. A consumer keying holdings by symbol drops
        // three rows in ten and is told nothing, so the property is `string?` and this pins that the rest of the
        // row still arrives.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.Null(rows[0].Symbol);
        Assert.Null(rows[1].Symbol);
        Assert.Equal("SAM", rows[2].Symbol);
        Assert.Equal("BOSTON BEER INC", rows[2].NameOfIssuer);
        Assert.Equal(314732m, rows[2].Shares);
    }

    [Fact]
    public void A_blank_put_call_share_stays_blank_rather_than_becoming_null()
    {
        // Modelled although it was `""` on all 7,346 rows of this path and never once populated. The same field
        // on extract-analytics/holder IS populated ("Share"), so omitting it here would leave a consumer no way
        // to reach it if FMP starts sending it. This asserts the measured emptiness rather than assuming it.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.All(rows, r => Assert.Equal("", r.PutCallShare));
    }

    [Fact]
    public async Task The_holdings_call_sends_cik_year_and_quarter_and_no_limit()
    {
        // The guard for the ignored parameter. Measured 2026-08-28, `extract` returns all 4,177 rows for
        // `limit=5` — byte-identical to no limit at all. A `limit` parameter here would be accepted, ignored,
        // and invisible in the response, which is worse than not offering one. This test fails the moment
        // somebody adds it back.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHoldingsAsync("0001067983", 2025, 3);

        Assert.Equal("/stable/institutional-ownership/extract", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_refused(int quarter)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHoldingsAsync("0001067983", 2025, quarter));

        Assert.Equal("quarter", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(2099)]
    public async Task A_year_far_outside_the_filed_range_is_sent_rather_than_refused(int year)
    {
        // Deliberate. Measured 2026-08-28, an out-of-range year answers `[]` with HTTP 200 — a legitimate
        // "no data", not an error. Guessing a floor would invent a fact the measurements do not have, and would
        // break the day FMP backfills. The endpoint is the authority on which years exist; GetFilingDatesAsync
        // is how a caller asks it.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var rows = await endpoints.GetHoldingsAsync("0001067983", year, 3);

        Assert.Empty(rows);
        Assert.Contains($"year={year}", handler.Requests[0].Query);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: FAIL to compile — `InstitutionalHolding`, `GetHoldingsAsync` and
`FmpJsonContext.Default.ListInstitutionalHolding` do not exist.

- [ ] **Step 4: Write `InstitutionalHolding`**

`src/FmpDotNet/Models/InstitutionalHolding.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One line of one 13F filing — a single position a filer reported holding at a quarter end, from
/// <c>stable/institutional-ownership/extract</c>.
///
/// <para><b>This is the raw infotable, one row per security.</b> A large filer's quarter runs to thousands of
/// rows: State Street's 2026 Q2 answered 4,177. The endpoint accepts <c>limit</c> and ignores it — measured
/// 2026-08-28, <c>limit=5</c> returned all 4,177, byte-identical to no limit at all — so
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetHoldingsAsync"/> offers neither <c>limit</c> nor
/// <c>page</c>. Take what comes back.</para>
///
/// <para><b>Three in ten rows have no ticker.</b> See <see cref="Symbol"/>.</para></summary>
public sealed record InstitutionalHolding
{
    /// <summary>The quarter end the filing reports on — <c>2026-06-30</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date the filing was submitted. Bare ISO on this path — <c>"2026-08-07"</c> — unlike
    /// <c>InstitutionalFiling.FilingDate</c>, which carries a dummy midnight and needs a different
    /// converter.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date EDGAR accepted the submission. <b>A date, not a timestamp, on this path</b> — measured
    /// 2026-08-28 it carries no time component at all, and was equal to <see cref="FilingDate"/> on every row
    /// sampled. <c>InstitutionalFiling.AcceptedDate</c> is the one place in this group where it is a real
    /// clock.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters. An institutional filer, not
    /// an issuer. <see cref="string"/> for the reason on <see cref="IndustryClassification.Cik"/>: the padding
    /// is the value.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The held security's CUSIP. Populated on every row measured, including the rows with no
    /// <see cref="Symbol"/> — which makes it the identifier to key on if you need one that is always
    /// there.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>The ticker, <b>or <see langword="null"/> — which happened on 2,209 of 7,346 rows measured
    /// 2026-08-28, 30.1%.</b>
    ///
    /// <para>A 13F holding need not have a ticker: bonds, warrants and private placements are reportable and do
    /// not have one. A consumer keying holdings by symbol silently drops three rows in ten. Use
    /// <see cref="SecurityCusip"/> or <see cref="NameOfIssuer"/> when you need every row.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name as the filer typed it — <c>"BOSTON BEER INC"</c>. Upper case, unnormalised,
    /// and populated on every row measured.</summary>
    [JsonPropertyName("nameOfIssuer")] public string? NameOfIssuer { get; init; }

    /// <summary>How many shares were held.
    ///
    /// <para><b><see cref="decimal"/> although every one of the 7,346 rows measured was integral.</b> The whole
    /// family of share and money fields in this group takes <c>decimal?</c> on one piece of evidence:
    /// <c>industryValue</c> on <c>institutional-ownership/industry-summary</c> is the same kind of quantity and
    /// is fractional on 53 of 394 rows. Binding a fractional value to an integer property makes
    /// <c>System.Text.Json</c> throw, and <c>FmpTransport</c> does not wrap the deserialiser — so one such value
    /// costs the caller the entire response, not the field. See <see cref="CompanyProfile.Volume"/> for the
    /// time this SDK learned that the expensive way.</para></summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The class of security — <c>"COM"</c>, <c>"CL A"</c>. The filer's own spelling.</summary>
    [JsonPropertyName("titleOfClass")] public string? TitleOfClass { get; init; }

    /// <summary>What <see cref="Shares"/> counts — <c>"SH"</c> for shares, <c>"PRN"</c> for principal
    /// amount.</summary>
    [JsonPropertyName("sharesType")] public string? SharesType { get; init; }

    /// <summary>Whether the position is a put, a call, or the underlying — <b>and it was blank on all 7,346
    /// rows measured 2026-08-28, across three filers.</b> Never null, never populated.
    ///
    /// <para>Modelled anyway. The same field on
    /// <c>institutional-ownership/extract-analytics/holder</c> <i>is</i> populated (<c>"Share"</c>), so this is
    /// a field FMP sends and could start filling. Omitting it would leave a consumer no way to reach it;
    /// modelling a constant costs one property. The emptiness is recorded here as a measurement rather than
    /// discovered as a bug.</para></summary>
    [JsonPropertyName("putCallShare")] public string? PutCallShare { get; init; }

    /// <summary>The position's reported market value in dollars. <c>decimal?</c> for the reason on
    /// <see cref="Shares"/>.</summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }

    /// <summary>The EDGAR filing-index page for the accession. Identical across every row of one
    /// filing.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The infotable XML itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
```

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<InstitutionalHolding>))]
```

- [ ] **Step 6: Add the method and the shared quarter guard**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>Every position one filer reported for one quarter — <c>stable/institutional-ownership/extract</c>.
    ///
    /// <para><b>Wide, and unpageable.</b> State Street's 2026 Q2 answered 4,177 rows. The endpoint accepts
    /// <c>limit</c> and ignores it — measured 2026-08-28, <c>limit=5</c> returned all 4,177 with a
    /// byte-identical body — so no <c>limit</c> and no <c>page</c> are offered here rather than shipping a
    /// control that silently does nothing.</para>
    ///
    /// <para><b>An unfiled quarter answers an empty list, not an error</b>, and so does an issuer's CIK. Use
    /// <see cref="GetFilingDatesAsync"/> to find out which quarters exist.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="year">The calendar year of the quarter end. <b>Not range-checked</b> — an out-of-range year
    /// answers an empty list with HTTP 200, which is a legitimate "no data", and inventing a floor would invent
    /// a fact.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP: omitting it answers
    /// <c>400 … missing query parameter - quarter</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every reported position, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InstitutionalHolding>> GetHoldingsAsync(
        string cik, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/extract")
                .With("cik", cik).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListInstitutionalHolding, ct);
    }

    /// <summary>Rejects a quarter FMP could only answer with an error.
    ///
    /// <para>Five methods on this class take a quarter and all five require it: measured 2026-08-28, omitting it
    /// answers <c>400 … missing query parameter - quarter</c> on every one. The range is the calendar's, not a
    /// measured cap — there is no fifth quarter to measure.</para>
    ///
    /// <para>The parameter is named <c>quarter</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentOutOfRangeException.ParamName"/>.</para></summary>
    private static void ThrowIfQuarterOutOfRange(int quarter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quarter, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quarter, 4);
    }
```

- [ ] **Step 7: Teach both argument synthesisers about `quarter`**

Neither is optional; both harnesses go red on this commit without them.

`tests/FmpDotNet.Tests/EndpointCoverageTests.cs`, in `Argument()`'s `int` arm, above the `_ => 0` default:

```csharp
                // 1 to 4, because the endpoints validate it. The `_ => 0` default below would be rejected
                // before a request went out, and the method would silently vanish from the coverage table.
                "quarter" => 3,
```

`tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `SettledYear`:

```csharp
    /// <summary>The fiscal quarter the five 13F probes ask for, paired with <see cref="SettledYear"/>.
    ///
    /// <para><b>Q3 and not Q4, and the reason is the filing deadline rather than the data.</b> A 13F is due 45
    /// days after the quarter ends, so <see cref="SettledYear"/>'s Q4 is not filed until mid-February of the
    /// following year — and <see cref="SettledYear"/> is <c>Today.Year - 1</c>, which means a run in January
    /// would ask for a quarter nobody has filed yet and record <c>rows 0</c> as the baseline for all five
    /// paths. Q3 of <see cref="SettledYear"/> was due by 14 November of that year, so it is settled on every
    /// day this suite can run.</para>
    ///
    /// <para>Measured 2026-08-28 with <c>year=2025&amp;quarter=3</c>: <c>extract</c> answered 41 rows,
    /// <c>holder-industry-breakdown</c> 33, <c>extract-analytics/holder</c> 5 (the probe's <c>limit</c>),
    /// <c>symbol-positions-summary</c> 1 and <c>industry-summary</c> 394. The same five with
    /// <c>quarter=4</c> answered 42, 34, 5, 1 and 394 — Q4 is equally good in August and unsafe in
    /// January.</para></summary>
    public const int SettledQuarter = 3;
```

`tests/FmpDotNet.SmokeTests/Probe.cs`, in `Argument()`'s `int` arm, above `_ => throw Unknown(parameter)`:

```csharp
                "quarter" => LiveApi.SettledQuarter,
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: PASS.

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: PASS — keyless, and this is the run that proves the `Probe.Argument` case landed.

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EndpointCoverageTests`
Expected: **one** failure —
`The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`, which stays red until Task 11.
`Every_public_endpoint_method_reaches_the_api` must PASS; if it fails, the `quarter` case in
`EndpointCoverageTests.Argument` is missing or wrong.

- [ ] **Step 9: Mutation-check**

1. Retype `Shares` and `Value` as `long?` → the build still succeeds and
   `A_captured_holding_binds_twelve_of_its_fourteen_fields` still passes, because this fixture's values are
   integral. **That is the point, and it is why the guard for this decision lives in Task 3 and Task 6 instead,
   on the records whose captured values really are fractional.** Restore, and record that the mutation did not
   fail here.
2. Add `.With("limit", 100)` to `GetHoldingsAsync` → `The_holdings_call_sends_cik_year_and_quarter_and_no_limit`
   fails on `Assert.DoesNotContain`. Restore.
3. Change `ThrowIfGreaterThan(quarter, 4)` to `(quarter, 5)` → `A_quarter_outside_one_to_four_is_refused(5)`
   fails. Restore.
4. Delete the `"quarter" => 3` case from `EndpointCoverageTests.Argument` →
   `Every_public_endpoint_method_reaches_the_api` fails naming `InstitutionalOwnership.GetHoldingsAsync`.
   Restore.
5. Delete the `"quarter" => LiveApi.SettledQuarter` case from `Probe.Argument` →
   `The_sweep_can_supply_arguments_for_every_endpoint_method` fails. Restore.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Models/InstitutionalHolding.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs \
        tests/FmpDotNet.Tests/EndpointCoverageTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract.head.json \
        tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs
git commit -m "feat: add the 13F holdings extract, with no limit and a nullable symbol

Three in ten holdings have no ticker (2,209 of 7,346 rows measured), so Symbol
is nullable and CUSIP is the identifier that is always there. No limit and no
page parameters: the endpoint accepts limit and ignores it, returning all 4,177
rows for limit=5.

Both argument synthesisers learn 'quarter' here rather than in the sweep task —
Probe.Argument throws on an unknown int and EndpointCoverageTests.Argument
defaults to 0, so this is the commit where each would otherwise go red."
```

### Task 3: `HolderAnalytics` — thirty-nine fields, and a page cap that is 100 rather than 1000

**Files:**
- Create: `src/FmpDotNet/Models/HolderAnalytics.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract-analytics.AAPL.json`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+MaxHolderAnalyticsPageSize`, `+GetHolderAnalyticsAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`

**Interfaces:**
- Consumes: `ThrowIfQuarterOutOfRange` (Task 2), `NullableLocalDateJsonConverter`.
- Produces: `public sealed record HolderAnalytics` (39 fields);
  `public const int MaxHolderAnalyticsPageSize = 100`;
  `Task<IReadOnlyList<HolderAnalytics>> GetHolderAnalyticsAsync(string symbol, int year, int quarter, int page = 0, int limit = 100, CancellationToken ct = default)`.

**The spec says this method's cap is `MaxOwnershipPageSize = 1000`. It is 100, measured.** Re-measured
2026-08-28: `limit=5` answered 5 rows; `limit=200`, `limit=1000`, `limit=1001` and `limit=2000` each answered
exactly **100**, HTTP 200, all four bodies byte-identical, with nothing in the response to say it had been
trimmed. This path genuinely paginates — `page=0` and `page=1` return different bodies — so a caller who asks
for 1,000 and steps `page` by 1,000 reads a tenth of the holder list and is never told. The method therefore
rejects a larger `limit` rather than passing it on to be clamped, and it gets a constant of its own.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract-analytics.AAPL.json` — the first two rows of
`stable/institutional-ownership/extract-analytics/holder?symbol=AAPL&year=2026&quarter=2&page=0&limit=5`,
captured 2026-08-28, verbatim. Two rows and not five: 39 fields each, and the second row carries a
`lastPerformance` of `0` and a `holdingPeriod` of `2` where the first carries a negative and an `8`, which is
everything the assertions need:

```json
[
  {
    "date": "2026-06-30",
    "cik": "0002012383",
    "filingDate": "2026-08-07",
    "investorName": "BLACKROCK, INC.",
    "symbol": "AAPL",
    "securityName": "APPLE INC",
    "typeOfSecurity": "COM",
    "securityCusip": "037833100",
    "sharesType": "SH",
    "putCallShare": "Share",
    "investmentDiscretion": "SOLE",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "weight": 5.0007,
    "lastWeight": 5.0758,
    "changeInWeight": -0.075,
    "changeInWeightPercentage": -1.4784,
    "marketValue": 336524794350,
    "lastMarketValue": 290512251859,
    "changeInMarketValue": 46012542491,
    "changeInMarketValuePercentage": 15.8384,
    "sharesNumber": 1162996939,
    "lastSharesNumber": 1144695425,
    "changeInSharesNumber": 18301514,
    "changeInSharesNumberPercentage": 1.5988,
    "quarterEndPrice": 289.36,
    "avgPricePaid": 234.24,
    "isNew": false,
    "isSoldOut": false,
    "ownership": 7.9058,
    "lastOwnership": 7.7814,
    "changeInOwnership": 0.1244,
    "changeInOwnershipPercentage": 1.5988,
    "holdingPeriod": 8,
    "firstAdded": "2024-09-30",
    "performance": 40716816267,
    "performancePercentage": 14.0155,
    "lastPerformance": -20864809759,
    "changeInPerformance": 61581626026,
    "isCountedForPerformance": true
  },
  {
    "date": "2026-06-30",
    "cik": "0002100119",
    "filingDate": "2026-08-13",
    "investorName": "VANGUARD CAPITAL MANAGEMENT LLC",
    "symbol": "AAPL",
    "securityName": "APPLE INC",
    "typeOfSecurity": "COM",
    "securityCusip": "037833100",
    "sharesType": "SH",
    "putCallShare": "Share",
    "investmentDiscretion": "DFND",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "weight": 5.9299,
    "lastWeight": 5.9877,
    "changeInWeight": -0.0578,
    "changeInWeightPercentage": -0.9653,
    "marketValue": 277527465127,
    "lastMarketValue": 242076924860,
    "changeInMarketValue": 35450540267,
    "changeInMarketValuePercentage": 14.6443,
    "sharesNumber": 959107911,
    "lastSharesNumber": 953847648,
    "changeInSharesNumber": 5260263,
    "changeInSharesNumberPercentage": 0.5515,
    "quarterEndPrice": 289.36,
    "avgPricePaid": 253.99,
    "isNew": false,
    "isSoldOut": false,
    "ownership": 6.5198,
    "lastOwnership": 6.484,
    "changeInOwnership": 0.0358,
    "changeInOwnershipPercentage": 0.5515,
    "holdingPeriod": 2,
    "firstAdded": "2026-03-31",
    "performance": 33928360839,
    "performancePercentage": 14.0155,
    "lastPerformance": 0,
    "changeInPerformance": 33928360839,
    "isCountedForPerformance": true
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`:

```csharp
    // ---- institutional-ownership/extract-analytics/holder --------------------------------------------------------

    [Fact]
    public void A_captured_holder_analytics_row_binds_all_thirty_nine_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(2, rows.Count);
        // Nothing unbound: this is the widest record in the slice and the one most likely to lose a field to a
        // typo'd [JsonPropertyName], which binds null rather than failing.
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BLACKROCK, INC.", rows[0].InvestorName);
        Assert.Equal("0002012383", rows[0].Cik);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("APPLE INC", rows[0].SecurityName);
        Assert.Equal("COM", rows[0].TypeOfSecurity);
        Assert.Equal("Share", rows[0].PutCallShare);
        Assert.Equal("SOLE", rows[0].InvestmentDiscretion);
        Assert.Equal("ELECTRONIC COMPUTERS", rows[0].IndustryTitle);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2024, 9, 30), rows[0].FirstAdded);
        Assert.False(rows[0].IsNew);
        Assert.False(rows[0].IsSoldOut);
        Assert.True(rows[0].IsCountedForPerformance);
        Assert.Equal(8, rows[0].HoldingPeriod);
    }

    [Fact]
    public void A_market_value_past_two_billion_binds_rather_than_throwing()
    {
        // The overflow guard. int.MaxValue is 2,147,483,647; BlackRock's AAPL position is 336,524,794,350 —
        // 157 times that. Typing MarketValue as int? makes System.Text.Json throw, and FmpTransport does not
        // wrap DeserializeAsync, so the caller loses the whole response rather than the field. Retyping it as
        // int? fails this test; retyping it as long? does not, which is why the fractional-value guard in
        // Task 6 exists as well.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(336524794350m, rows[0].MarketValue);
        Assert.Equal(290512251859m, rows[0].LastMarketValue);
        Assert.Equal(40716816267m, rows[0].Performance);
        Assert.Equal(-20864809759m, rows[0].LastPerformance);
        // 1,162,996,939 — 54% of int's ceiling and rising. `sharesNumber` is the field that gets retyped by
        // somebody who checks one row and concludes it fits.
        Assert.Equal(1162996939m, rows[0].SharesNumber);
    }

    [Fact]
    public void A_zero_performance_is_a_measured_value_and_not_a_missing_one()
    {
        // Vanguard's row carries lastPerformance: 0 because it first held AAPL in the previous quarter. Zero is
        // FMP's answer, not an absence — Binding.Unbound counts zero as populated for exactly this reason
        // (see its doc), and a caller must not read it as "not reported".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(0m, rows[1].LastPerformance);
        Assert.Empty(Binding.Unbound(rows[1]));
        Assert.Equal(2, rows[1].HoldingPeriod);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[1].FirstAdded);
    }

    [Fact]
    public async Task The_holder_analytics_call_sends_page_and_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, page: 2, limit: 50);

        Assert.Equal(
            "/stable/institutional-ownership/extract-analytics/holder", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task A_holder_analytics_limit_above_one_hundred_is_refused(int limit)
    {
        // Measured 2026-08-28: limit=200, 1000, 1001 and 2000 each answered exactly 100 rows with HTTP 200 and
        // byte-identical bodies. The path DOES paginate, so a caller who asked for 1,000 and stepped `page` by
        // 1,000 would read a tenth of the holder list and be told nothing at all. This is the one path in the
        // slice whose cap is 100 rather than 1,000.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_holder_analytics_limit_exactly_at_the_cap_is_accepted()
    {
        // The boundary the last slice's review had to add three times, for the same reason each time:
        // ThrowIfGreaterThan and ThrowIfGreaterThanOrEqual differ by one value, the whole suite stays green
        // when they are swapped, and the documented maximum starts throwing.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderAnalyticsAsync(
            "AAPL", 2025, 3, limit: InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);

        Assert.Contains("limit=100", handler.Requests[0].Query);
    }

    [Fact]
    public void The_holder_analytics_page_cap_is_the_measured_one()
    {
        Assert.Equal(100, InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task A_negative_page_or_a_non_positive_limit_is_refused_on_holder_analytics(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, page: page, limit: limit));

        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: FAIL to compile — `HolderAnalytics`, `MaxHolderAnalyticsPageSize` and `GetHolderAnalyticsAsync` do not
exist.

- [ ] **Step 4: Write `HolderAnalytics`**

`src/FmpDotNet/Models/HolderAnalytics.cs`. Thirty-nine properties; the XML doc on the record carries the
reasoning, and the individual members are documented where they can mislead rather than uniformly.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One institution's position in one symbol for one quarter, with FMP's quarter-over-quarter analytics
/// attached — <c>stable/institutional-ownership/extract-analytics/holder</c>.
///
/// <para><b>The same position <see cref="InstitutionalHolding"/> describes, read from the other end.</b> That
/// path answers "what does this filer hold"; this one answers "who holds this symbol", and adds the
/// derived fields FMP computes: weights, changes, ownership percentages, holding periods and performance. Thirty-nine
/// fields, all thirty-nine populated on every row measured 2026-08-28.</para>
///
/// <para><b>Paged, and the cap is 100.</b> See
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize"/> — this is the only path
/// in the group that clamps at 100 rather than 1,000, and it does it silently.</para>
///
/// <para><b>Every money, share and percentage field is <see cref="decimal"/>.</b> All 7,946 rows sampled from
/// this path and <c>extract</c> carried integral money values, so <c>long?</c> is the obvious read and it is
/// the wrong one: <c>industryValue</c> on <c>industry-summary</c> is the same kind of quantity and is
/// fractional on 53 of 394 rows. A fractional value bound to an integer property makes
/// <c>System.Text.Json</c> throw and costs the caller the whole response. Only
/// <see cref="HoldingPeriod"/> is an integer here, because it counts quarters.</para></summary>
public sealed record HolderAnalytics
{
    /// <summary>The quarter end — <c>2026-06-30</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The <b>filer's</b> Central Index Key, zero-padded. Not the issuer's — the issuer is identified
    /// by <see cref="Symbol"/> and <see cref="SecurityCusip"/> on this path.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The date the filer submitted. Bare ISO here.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The institution's name — <c>"BLACKROCK, INC."</c>.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>The ticker asked for. Always populated here, unlike
    /// <see cref="InstitutionalHolding.Symbol"/> — this path is keyed by symbol, so a row without one cannot
    /// exist.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name — <c>"APPLE INC"</c>.</summary>
    [JsonPropertyName("securityName")] public string? SecurityName { get; init; }

    /// <summary>The class of security — <c>"COM"</c>.</summary>
    [JsonPropertyName("typeOfSecurity")] public string? TypeOfSecurity { get; init; }

    /// <summary>The security's CUSIP.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>What <see cref="SharesNumber"/> counts — <c>"SH"</c>.</summary>
    [JsonPropertyName("sharesType")] public string? SharesType { get; init; }

    /// <summary>Put, call or underlying — <c>"Share"</c> on every row measured.
    ///
    /// <para><b>Populated here, blank on the sibling path.</b> The identically-named
    /// <see cref="InstitutionalHolding.PutCallShare"/> was <c>""</c> on all 7,346 rows of <c>extract</c>. Same
    /// field name, two different behaviours, measured 2026-08-28.</para></summary>
    [JsonPropertyName("putCallShare")] public string? PutCallShare { get; init; }

    /// <summary>The filer's voting discretion — <c>"SOLE"</c>, <c>"DFND"</c>, <c>"OTR"</c>.</summary>
    [JsonPropertyName("investmentDiscretion")] public string? InvestmentDiscretion { get; init; }

    /// <summary>The issuer's SIC industry label — <c>"ELECTRONIC COMPUTERS"</c>. Upper case, the same
    /// vocabulary <see cref="IndustryClassification.IndustryTitle"/> uses.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The position's share of the filer's whole 13F portfolio, as a percentage.</summary>
    [JsonPropertyName("weight")] public decimal? Weight { get; init; }

    /// <summary>The same weight one quarter earlier.</summary>
    [JsonPropertyName("lastWeight")] public decimal? LastWeight { get; init; }

    /// <summary><see cref="Weight"/> minus <see cref="LastWeight"/>, in percentage points.</summary>
    [JsonPropertyName("changeInWeight")] public decimal? ChangeInWeight { get; init; }

    /// <summary>That change expressed as a percentage of <see cref="LastWeight"/> — a percentage of a
    /// percentage, not a second percentage-point figure.</summary>
    [JsonPropertyName("changeInWeightPercentage")] public decimal? ChangeInWeightPercentage { get; init; }

    /// <summary>The position's value in dollars at the quarter end. <b>336,524,794,350 on the measured
    /// BlackRock row</b> — 157 times <see cref="int"/>'s ceiling.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>The same value one quarter earlier.</summary>
    [JsonPropertyName("lastMarketValue")] public decimal? LastMarketValue { get; init; }

    /// <summary>The dollar change in market value.</summary>
    [JsonPropertyName("changeInMarketValue")] public decimal? ChangeInMarketValue { get; init; }

    /// <summary>That change as a percentage of <see cref="LastMarketValue"/>.</summary>
    [JsonPropertyName("changeInMarketValuePercentage")]
    public decimal? ChangeInMarketValuePercentage { get; init; }

    /// <summary>Shares held at the quarter end. <b>1,162,996,939 on the measured BlackRock row — 54% of
    /// <see cref="int"/>'s ceiling</b>, which is close enough that a reader who checks one row concludes it
    /// fits.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>Shares held one quarter earlier.</summary>
    [JsonPropertyName("lastSharesNumber")] public decimal? LastSharesNumber { get; init; }

    /// <summary>The change in share count. Negative when the filer sold.</summary>
    [JsonPropertyName("changeInSharesNumber")] public decimal? ChangeInSharesNumber { get; init; }

    /// <summary>That change as a percentage of <see cref="LastSharesNumber"/>.</summary>
    [JsonPropertyName("changeInSharesNumberPercentage")]
    public decimal? ChangeInSharesNumberPercentage { get; init; }

    /// <summary>The security's price at the quarter end, in dollars.</summary>
    [JsonPropertyName("quarterEndPrice")] public decimal? QuarterEndPrice { get; init; }

    /// <summary>FMP's estimate of the filer's average cost. Derived, not reported: a 13F carries no cost
    /// basis.</summary>
    [JsonPropertyName("avgPricePaid")] public decimal? AvgPricePaid { get; init; }

    /// <summary>Whether this is the filer's first quarter holding the security.</summary>
    [JsonPropertyName("isNew")] public bool? IsNew { get; init; }

    /// <summary>Whether the filer exited the position this quarter.</summary>
    [JsonPropertyName("isSoldOut")] public bool? IsSoldOut { get; init; }

    /// <summary>The filer's share of the issuer's outstanding stock, as a percentage.</summary>
    [JsonPropertyName("ownership")] public decimal? Ownership { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastOwnership")] public decimal? LastOwnership { get; init; }

    /// <summary>The change in ownership, in percentage points.</summary>
    [JsonPropertyName("changeInOwnership")] public decimal? ChangeInOwnership { get; init; }

    /// <summary>That change as a percentage of <see cref="LastOwnership"/>.</summary>
    [JsonPropertyName("changeInOwnershipPercentage")] public decimal? ChangeInOwnershipPercentage { get; init; }

    /// <summary>How many consecutive quarters the filer has held the security.
    ///
    /// <para><b>One of the few genuine counts in this record, and therefore <see cref="int"/>.</b> It counts
    /// quarters; 8 and 2 on the two measured rows. Typing it <c>decimal?</c> to match its neighbours would make
    /// the API worse to read for no measured reason.</para></summary>
    [JsonPropertyName("holdingPeriod")] public int? HoldingPeriod { get; init; }

    /// <summary>The quarter end at which the filer first reported the security.</summary>
    [JsonPropertyName("firstAdded")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FirstAdded { get; init; }

    /// <summary>FMP's estimate of the position's dollar gain or loss this quarter. Negative values occur — the
    /// measured BlackRock row's <see cref="LastPerformance"/> is −20,864,809,759.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>That gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The same figure one quarter earlier. <b><c>0</c> is a measured value, not an absence</b> — it
    /// is what a filer in its first quarter gets, as on the Vanguard row captured 2026-08-28.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }

    /// <summary>Whether FMP includes this position in the filer's aggregate performance figures on
    /// <c>HolderPerformance</c>.</summary>
    [JsonPropertyName("isCountedForPerformance")] public bool? IsCountedForPerformance { get; init; }
}
```

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<HolderAnalytics>))]
```

- [ ] **Step 6: Add the constant and the method**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>The largest page <see cref="GetHolderAnalyticsAsync"/> will serve — <b>100, not 1,000</b>, and
    /// measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, and the odd one out in this group. Measured 2026-08-28,
    /// <c>limit=5</c> answered 5 rows while <c>limit=200</c>, <c>limit=1000</c>, <c>limit=1001</c> and
    /// <c>limit=2000</c> each answered exactly 100 with HTTP 200 and byte-identical bodies — nothing in the
    /// response says the request was trimmed. The path genuinely paginates, so a caller who asks for 1,000 and
    /// advances <c>page</c> by 1,000 reads a tenth of the holder list and is never told. A larger
    /// <c>limit</c> is therefore refused here rather than passed on to be clamped.</para>
    ///
    /// <para>Every other paged path in this slice caps at 1,000; see <c>MaxOwnershipPageSize</c> and
    /// <c>InsiderTradesEndpoints.MaxInsiderTradePageSize</c>.</para></summary>
    public const int MaxHolderAnalyticsPageSize = 100;

    /// <summary>Every institution reporting a position in one symbol for one quarter, with FMP's
    /// quarter-over-quarter analytics —
    /// <c>stable/institutional-ownership/extract-analytics/holder</c>.
    ///
    /// <para><b>The mirror of <see cref="GetHoldingsAsync"/>.</b> That asks a filer what it holds; this asks a
    /// symbol who holds it, and adds weights, ownership percentages, holding periods and performance that a
    /// 13F does not itself report.</para>
    ///
    /// <para><b>Paged, and the cap is 100</b> — see <see cref="MaxHolderAnalyticsPageSize"/>. A widely-held
    /// symbol runs to thousands of holders, so this is a path you page rather than one you drain in a
    /// call.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked; see
    /// <see cref="GetHoldingsAsync"/>.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxHolderAnalyticsPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's holders. Never <see langword="null"/>; empty for an unknown symbol or an unfiled
    /// quarter, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4,
    /// <paramref name="page"/> is negative, or <paramref name="limit"/> is outside 1 to
    /// <see cref="MaxHolderAnalyticsPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderAnalytics>> GetHolderAnalyticsAsync(
        string symbol, int year, int quarter, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfQuarterOutOfRange(quarter);
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxHolderAnalyticsPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/extract-analytics/holder")
                .With("symbol", symbol).With("year", year).With("quarter", quarter)
                .With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListHolderAnalytics, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: PASS.

- [ ] **Step 8: Mutation-check**

1. Retype `MarketValue` as `int?` → `A_market_value_past_two_billion_binds_rather_than_throwing` fails with a
   `System.Text.Json` exception rather than an assertion failure, which is the failure mode a caller would see.
   Restore.
2. Change `MaxHolderAnalyticsPageSize` to `1000` → `The_holder_analytics_page_cap_is_the_measured_one` and
   three of the four `A_holder_analytics_limit_above_one_hundred_is_refused` cases fail. Restore. **This is the
   mutation that pins the spec correction**; without the constant test, retyping the cap back to the spec's
   1,000 would pass everything else.
3. Delete `[JsonPropertyName("changeInWeightPercentage")]` → `Assert.Empty(Binding.Unbound(rows[0]))` fails
   naming `ChangeInWeightPercentage`. **This is the mutation that justifies `Binding.Unbound` over
   field-by-field assertions on a 39-field record**: `PropertyNameCaseInsensitive` is set, so most attributes
   here are not load-bearing and only a whole-record check finds the one that is. Restore.
4. Retype `HoldingPeriod` as `decimal?` → nothing fails. Record it: the `int?` choice on the genuine counts is
   an API-readability decision, not a correctness one, and it is not defended by a test.
5. Change `ThrowIfGreaterThan(limit, MaxHolderAnalyticsPageSize)` to `ThrowIfGreaterThanOrEqual` →
   `A_holder_analytics_limit_exactly_at_the_cap_is_accepted` fails and nothing else does. **The last slice's
   review had to add this boundary three times**, because the whole suite stays green under that swap while the
   documented maximum starts throwing. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/HolderAnalytics.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-extract-analytics.AAPL.json
git commit -m "feat: add per-symbol holder analytics, capped at 100 rather than 1000

The design spec gives this path the group's MaxOwnershipPageSize of 1000. It is
100: measured 2026-08-28, limit=200, 1000, 1001 and 2000 each answered exactly
100 rows with byte-identical bodies and HTTP 200. The path paginates, so a
caller stepping page by 1000 would read a tenth of the holder list in silence.
It gets MaxHolderAnalyticsPageSize of its own."
```

### Task 4: The two holder summaries — one industry breakdown, one performance record

**Files:**
- Create: `src/FmpDotNet/Models/HolderSummaries.cs` — `HolderIndustryBreakdown` (12) and `HolderPerformance` (33)
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-industry-breakdown.BRK.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-performance-summary.BRK.json`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+GetHolderIndustryBreakdownAsync`, `+GetHolderPerformanceAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`

**Interfaces:**
- Consumes: `ThrowIfQuarterOutOfRange` (Task 2).
- Produces: `public sealed record HolderIndustryBreakdown`, `public sealed record HolderPerformance`;
  `Task<IReadOnlyList<HolderIndustryBreakdown>> GetHolderIndustryBreakdownAsync(string cik, int year, int quarter, CancellationToken ct = default)`;
  `Task<IReadOnlyList<HolderPerformance>> GetHolderPerformanceAsync(string cik, CancellationToken ct = default)`.

Two records in one file because they answer the same question about the same subject at two resolutions: one
filer's quarter, cut by industry, and one filer's whole history, quarter by quarter. Both are keyed on a filer
CIK, both are unpaged, and neither honours `limit`. The signatures differ in one place and it is measured:
`holder-industry-breakdown` requires `year` and `quarter`; `holder-performance-summary` takes neither and
returns every quarter the filer has reported — 53 rows for Berkshire, 2026-08-28.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-industry-breakdown.BRK.json` — the first three
rows of `stable/institutional-ownership/holder-industry-breakdown?cik=0001067983&year=2026&quarter=2`, captured
2026-08-28. The full response was 24 rows. **All three carry a negative `performancePercentage` beside a
positive `performance`**, which is not a capture error — see the assertion in Step 2:

```json
[
  {
    "date": "2026-06-30",
    "cik": "0001067983",
    "investorName": "BERKSHIRE HATHAWAY INC",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "weight": 22.0383,
    "lastWeight": 21.9856,
    "changeInWeight": 0.0526,
    "changeInWeightPercentage": 0.2394,
    "performance": 8107036430,
    "performancePercentage": -296.8456,
    "lastPerformance": -4118474790,
    "changeInPerformance": 12225511220
  },
  {
    "date": "2026-06-30",
    "cik": "0001067983",
    "investorName": "BERKSHIRE HATHAWAY INC",
    "industryTitle": "FINANCE SERVICES",
    "weight": 17.1367,
    "lastWeight": 17.4306,
    "changeInWeight": -0.2939,
    "changeInWeightPercentage": -1.686,
    "performance": 5423114738,
    "performancePercentage": -153.0162,
    "lastPerformance": -10229173928,
    "changeInPerformance": 15652288666
  },
  {
    "date": "2026-06-30",
    "cik": "0001067983",
    "investorName": "BERKSHIRE HATHAWAY INC",
    "industryTitle": "SERVICES-COMPUTER PROGRAMMING, DATA PROCESSING, ETC.",
    "weight": 15.8296,
    "lastWeight": 6.7112,
    "changeInWeight": 9.1183,
    "changeInWeightPercentage": 135.8667,
    "performance": 4263796880,
    "performancePercentage": -1039.1502,
    "lastPerformance": -454005852,
    "changeInPerformance": 4717802732
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-performance-summary.BRK.json` — the first two
rows of `stable/institutional-ownership/holder-performance-summary?cik=0001067983`, captured 2026-08-28. The
full response was 53 rows, newest first. Two rows and not five: 33 fields each, and the pair spans a quarter
where `performance` flips sign:

```json
[
  {
    "date": "2026-06-30",
    "cik": "0001067983",
    "investorName": "BERKSHIRE HATHAWAY INC",
    "portfolioSize": 29,
    "securitiesAdded": 1,
    "securitiesRemoved": 1,
    "marketValue": 299253556246,
    "previousMarketValue": 263095703570,
    "changeInMarketValue": 36157852676,
    "changeInMarketValuePercentage": 13.7432,
    "averageHoldingPeriod": 20,
    "averageHoldingPeriodTop10": 29,
    "averageHoldingPeriodTop20": 25,
    "turnover": 0.069,
    "turnoverAlternateSell": 1.3636,
    "turnoverAlternateBuy": 6.4055,
    "performance": 21069772689,
    "performancePercentage": 8.0084,
    "lastPerformance": -2243708176,
    "changeInPerformance": 23313480865,
    "performance1year": 49169544640,
    "performancePercentage1year": 19.9052,
    "performance3year": 111161092907,
    "performancePercentage3year": 45.067,
    "performance5year": 143085981389,
    "performancePercentage5year": 61.4192,
    "performanceSinceInception": 288653953205,
    "performanceSinceInceptionPercentage": 228.2497,
    "performanceRelativeToSP500Percentage": -6.8622,
    "performance1yearRelativeToSP500Percentage": -0.9559,
    "performance3yearRelativeToSP500Percentage": -23.4436,
    "performance5yearRelativeToSP500Percentage": -13.0859,
    "performanceSinceInceptionRelativeToSP500Percentage": -151.8108
  },
  {
    "date": "2026-03-31",
    "cik": "0001067983",
    "investorName": "BERKSHIRE HATHAWAY INC",
    "portfolioSize": 29,
    "securitiesAdded": 3,
    "securitiesRemoved": 16,
    "marketValue": 263095703570,
    "previousMarketValue": 274160086701,
    "changeInMarketValue": -11064383131,
    "changeInMarketValuePercentage": -4.0357,
    "averageHoldingPeriod": 19,
    "averageHoldingPeriodTop10": 32,
    "averageHoldingPeriodTop20": 25,
    "turnover": 0.6552,
    "turnoverAlternateSell": 9.1702,
    "turnoverAlternateBuy": 5.8198,
    "performance": -2243708176,
    "performancePercentage": -0.8184,
    "lastPerformance": 12155036983,
    "changeInPerformance": -14398745159,
    "performance1year": 28972527543,
    "performancePercentage1year": 11.3877,
    "performance3year": 118145912143,
    "performancePercentage3year": 45.9009,
    "performance5year": 146867544096,
    "performancePercentage5year": 63.1842,
    "performanceSinceInception": 267584180516,
    "performanceSinceInceptionPercentage": 203.9112,
    "performanceRelativeToSP500Percentage": 3.8118,
    "performance1yearRelativeToSP500Percentage": -4.9473,
    "performance3yearRelativeToSP500Percentage": -12.9708,
    "performance5yearRelativeToSP500Percentage": -1.1428,
    "performanceSinceInceptionRelativeToSP500Percentage": -114.003
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`:

```csharp
    // ---- institutional-ownership/holder-industry-breakdown -------------------------------------------------------

    [Fact]
    public void A_captured_industry_breakdown_row_binds_all_twelve_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-industry-breakdown.BRK.json"),
            FmpJsonContext.Default.ListHolderIndustryBreakdown)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0001067983", rows[0].Cik);
        Assert.Equal("BERKSHIRE HATHAWAY INC", rows[0].InvestorName);
        Assert.Equal("ELECTRONIC COMPUTERS", rows[0].IndustryTitle);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(22.0383m, rows[0].Weight);
        Assert.Equal(8107036430m, rows[0].Performance);
    }

    [Fact]
    public void An_industry_performance_percentage_can_contradict_its_own_dollar_figure()
    {
        // Not a capture error, and not something to normalise. All three measured rows carry a positive
        // `performance` beside a negative `performancePercentage` — 8,107,036,430 against −296.8456. FMP's
        // percentage is computed against a base this endpoint does not publish, and the two figures are not
        // reconcilable from the response. The SDK reports both as sent; a consumer that assumes they agree in
        // sign is wrong on every row measured, which is what this test records.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-industry-breakdown.BRK.json"),
            FmpJsonContext.Default.ListHolderIndustryBreakdown)!;

        Assert.All(rows, r =>
        {
            Assert.True(r.Performance > 0);
            Assert.True(r.PerformancePercentage < 0);
        });
        Assert.Equal(-296.8456m, rows[0].PerformancePercentage);
        Assert.Equal(-4118474790m, rows[0].LastPerformance);
    }

    [Fact]
    public async Task The_industry_breakdown_call_sends_cik_year_and_quarter()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderIndustryBreakdownAsync("0001067983", 2025, 3);

        Assert.Equal(
            "/stable/institutional-ownership/holder-industry-breakdown", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    // ---- institutional-ownership/holder-performance-summary ------------------------------------------------------

    [Fact]
    public void A_captured_holder_performance_row_binds_all_thirty_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-performance-summary.BRK.json"),
            FmpJsonContext.Default.ListHolderPerformance)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BERKSHIRE HATHAWAY INC", rows[0].InvestorName);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(29, rows[0].PortfolioSize);
        Assert.Equal(1, rows[0].SecuritiesAdded);
        Assert.Equal(1, rows[0].SecuritiesRemoved);
        Assert.Equal(20, rows[0].AverageHoldingPeriod);
        Assert.Equal(29, rows[0].AverageHoldingPeriodTop10);
        Assert.Equal(25, rows[0].AverageHoldingPeriodTop20);
        Assert.Equal(299253556246m, rows[0].MarketValue);
        Assert.Equal(288653953205m, rows[0].PerformanceSinceInception);
        Assert.Equal(-151.8108m, rows[0].PerformanceSinceInceptionRelativeToSP500Percentage);
    }

    [Fact]
    public void The_performance_summary_answers_every_quarter_not_just_the_latest()
    {
        // Measured 2026-08-28: 53 rows for Berkshire, newest first, one per quarter reported — the same 53
        // quarters GetFilingDatesAsync enumerates. That is why this method takes no year and no quarter: it is
        // the filer's whole history, and asking for one quarter of it is not something the endpoint offers.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-performance-summary.BRK.json"),
            FmpJsonContext.Default.ListHolderPerformance)!;

        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[1].Date);
        // The quarter that flips sign, which is why these two rows were chosen.
        Assert.Equal(21069772689m, rows[0].Performance);
        Assert.Equal(-2243708176m, rows[1].Performance);
        // And each row's LastPerformance is the next row's Performance — the series is self-consistent.
        Assert.Equal(rows[1].Performance, rows[0].LastPerformance);
    }

    [Fact]
    public async Task The_performance_summary_call_sends_only_the_cik()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderPerformanceAsync("0001067983");

        Assert.Equal(
            "/stable/institutional-ownership/holder-performance-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        Assert.DoesNotContain("year=", handler.Requests[0].Query);
        Assert.DoesNotContain("quarter=", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: FAIL to compile — neither record nor either method exists.

- [ ] **Step 4: Write both records**

`src/FmpDotNet/Models/HolderSummaries.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>How one 13F filer's portfolio was spread across industries in one quarter, from
/// <c>stable/institutional-ownership/holder-industry-breakdown</c>.
///
/// <para>One row per industry the filer held, sorted by weight. Berkshire's 2026 Q2 answered 24 rows,
/// measured 2026-08-28, all twelve fields populated on every one.</para>
///
/// <para><b><see cref="Performance"/> and <see cref="PerformancePercentage"/> can disagree in sign, and that is
/// FMP's answer rather than a fault.</b> See <see cref="PerformancePercentage"/>.</para></summary>
public sealed record HolderIndustryBreakdown
{
    /// <summary>The quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name — <c>"BERKSHIRE HATHAWAY INC"</c>.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>The SIC industry label — <c>"ELECTRONIC COMPUTERS"</c>. The same vocabulary
    /// <see cref="HolderAnalytics.IndustryTitle"/> and <c>IndustryOwnershipSummary.IndustryTitle</c>
    /// use.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The industry's share of the filer's portfolio, as a percentage.</summary>
    [JsonPropertyName("weight")] public decimal? Weight { get; init; }

    /// <summary>The same weight one quarter earlier.</summary>
    [JsonPropertyName("lastWeight")] public decimal? LastWeight { get; init; }

    /// <summary>The change in weight, in percentage points.</summary>
    [JsonPropertyName("changeInWeight")] public decimal? ChangeInWeight { get; init; }

    /// <summary>That change as a percentage of <see cref="LastWeight"/>.</summary>
    [JsonPropertyName("changeInWeightPercentage")] public decimal? ChangeInWeightPercentage { get; init; }

    /// <summary>The industry slice's dollar gain or loss this quarter.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>The same gain as a percentage — <b>and it can contradict <see cref="Performance"/>'s
    /// sign.</b>
    ///
    /// <para>Measured 2026-08-28: all three of the captured Berkshire rows carry a positive
    /// <see cref="Performance"/> beside a negative percentage, the largest being <c>8,107,036,430</c> against
    /// <c>−296.8456</c>. FMP computes the percentage against a base this endpoint does not publish, so the two
    /// cannot be reconciled from the response. Both are reported exactly as sent; neither is derived here, and
    /// a consumer that assumes they agree in sign is wrong on every row measured.</para></summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The same dollar figure one quarter earlier.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }
}

/// <summary>One quarter of one 13F filer's aggregate portfolio performance, from
/// <c>stable/institutional-ownership/holder-performance-summary</c>.
///
/// <para><b>The filer's whole history, one row per quarter, newest first</b> — 53 rows for Berkshire, measured
/// 2026-08-28, matching the 53 quarters <c>institutional-ownership/dates</c> enumerates. The endpoint takes no
/// year and no quarter, which is why
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetHolderPerformanceAsync"/> takes only a CIK.</para>
///
/// <para><b>The series is self-consistent across rows:</b> each row's <see cref="LastPerformance"/> equals the
/// next row's <see cref="Performance"/>, verified on the captured pair.</para>
///
/// <para><b>Six fields here are genuine counts and are <see cref="int"/>:</b>
/// <see cref="PortfolioSize"/>, <see cref="SecuritiesAdded"/>, <see cref="SecuritiesRemoved"/> and the three
/// average holding periods, which count securities and quarters. Everything else is money or a percentage and
/// is <see cref="decimal"/> — see <see cref="HolderAnalytics"/> for why.</para></summary>
public sealed record HolderPerformance
{
    /// <summary>The quarter end this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>How many distinct securities the filer reported. A count, hence <see cref="int"/>.</summary>
    [JsonPropertyName("portfolioSize")] public int? PortfolioSize { get; init; }

    /// <summary>How many securities were new this quarter. A count.</summary>
    [JsonPropertyName("securitiesAdded")] public int? SecuritiesAdded { get; init; }

    /// <summary>How many were exited this quarter. A count.</summary>
    [JsonPropertyName("securitiesRemoved")] public int? SecuritiesRemoved { get; init; }

    /// <summary>The portfolio's total reported value in dollars.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>The same, one quarter earlier. <b>Spelled <c>previousMarketValue</c> on the wire</b>, not
    /// <c>lastMarketValue</c> — the only place in this group where FMP uses "previous" rather than "last", and
    /// the attribute is load-bearing because of it.</summary>
    [JsonPropertyName("previousMarketValue")] public decimal? PreviousMarketValue { get; init; }

    /// <summary>The dollar change in portfolio value.</summary>
    [JsonPropertyName("changeInMarketValue")] public decimal? ChangeInMarketValue { get; init; }

    /// <summary>That change as a percentage.</summary>
    [JsonPropertyName("changeInMarketValuePercentage")]
    public decimal? ChangeInMarketValuePercentage { get; init; }

    /// <summary>The mean number of quarters the filer has held its positions. A count of quarters, hence
    /// <see cref="int"/>.</summary>
    [JsonPropertyName("averageHoldingPeriod")] public int? AverageHoldingPeriod { get; init; }

    /// <summary>The same, over the ten largest positions.</summary>
    [JsonPropertyName("averageHoldingPeriodTop10")] public int? AverageHoldingPeriodTop10 { get; init; }

    /// <summary>The same, over the twenty largest.</summary>
    [JsonPropertyName("averageHoldingPeriodTop20")] public int? AverageHoldingPeriodTop20 { get; init; }

    /// <summary>Portfolio turnover for the quarter, as a fraction.</summary>
    [JsonPropertyName("turnover")] public decimal? Turnover { get; init; }

    /// <summary>FMP's alternative turnover measure computed from sales.</summary>
    [JsonPropertyName("turnoverAlternateSell")] public decimal? TurnoverAlternateSell { get; init; }

    /// <summary>The same computed from purchases.</summary>
    [JsonPropertyName("turnoverAlternateBuy")] public decimal? TurnoverAlternateBuy { get; init; }

    /// <summary>The portfolio's dollar gain or loss this quarter. Negative quarters occur.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>That gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The previous quarter's <see cref="Performance"/>. Equal to the next row's
    /// <see cref="Performance"/> — the rows chain.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }

    /// <summary>Trailing one-year dollar gain.</summary>
    [JsonPropertyName("performance1year")] public decimal? Performance1Year { get; init; }

    /// <summary>Trailing one-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage1year")] public decimal? PerformancePercentage1Year { get; init; }

    /// <summary>Trailing three-year dollar gain.</summary>
    [JsonPropertyName("performance3year")] public decimal? Performance3Year { get; init; }

    /// <summary>Trailing three-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage3year")] public decimal? PerformancePercentage3Year { get; init; }

    /// <summary>Trailing five-year dollar gain.</summary>
    [JsonPropertyName("performance5year")] public decimal? Performance5Year { get; init; }

    /// <summary>Trailing five-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage5year")] public decimal? PerformancePercentage5Year { get; init; }

    /// <summary>Dollar gain since the filer's first reported quarter.</summary>
    [JsonPropertyName("performanceSinceInception")] public decimal? PerformanceSinceInception { get; init; }

    /// <summary>The same as a percentage.</summary>
    [JsonPropertyName("performanceSinceInceptionPercentage")]
    public decimal? PerformanceSinceInceptionPercentage { get; init; }

    /// <summary>This quarter's percentage gain less the S&amp;P 500's. Negative means the filer
    /// trailed.</summary>
    [JsonPropertyName("performanceRelativeToSP500Percentage")]
    public decimal? PerformanceRelativeToSP500Percentage { get; init; }

    /// <summary>The same over one year.</summary>
    [JsonPropertyName("performance1yearRelativeToSP500Percentage")]
    public decimal? Performance1YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same over three years.</summary>
    [JsonPropertyName("performance3yearRelativeToSP500Percentage")]
    public decimal? Performance3YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same over five years.</summary>
    [JsonPropertyName("performance5yearRelativeToSP500Percentage")]
    public decimal? Performance5YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same since inception.</summary>
    [JsonPropertyName("performanceSinceInceptionRelativeToSP500Percentage")]
    public decimal? PerformanceSinceInceptionRelativeToSP500Percentage { get; init; }
}
```

**The `[JsonPropertyName]` attributes on the eight `*1year`/`*3year`/`*5year` properties are load-bearing.** FMP
spells them `performance1year` and `performancePercentage1year` in lower case; the C# names capitalise `Year`.
`PropertyNameCaseInsensitive` is set on `FmpJsonContext`, so those would in fact still bind — but
`performanceSinceInceptionRelativeToSP500Percentage` and its siblings would not survive a rename, and
`previousMarketValue` would not survive being renamed to match its `last*` neighbours. The whole-record
`Binding.Unbound` assertion in Step 2 is what catches any of them.

- [ ] **Step 5: Register both**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<HolderIndustryBreakdown>))]
[JsonSerializable(typeof(List<HolderPerformance>))]
```

- [ ] **Step 6: Add both methods**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>How one filer's quarter was spread across industries —
    /// <c>stable/institutional-ownership/holder-industry-breakdown</c>.
    ///
    /// <para>One row per industry, sorted by weight; Berkshire's 2026 Q2 answered 24. No <c>limit</c> and no
    /// <c>page</c> — the endpoint honours neither, and the result set is small enough that it does not
    /// matter.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The industry breakdown, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderIndustryBreakdown>> GetHolderIndustryBreakdownAsync(
        string cik, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/holder-industry-breakdown")
                .With("cik", cik).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListHolderIndustryBreakdown, ct);
    }

    /// <summary>One filer's aggregate portfolio performance for every quarter it has reported —
    /// <c>stable/institutional-ownership/holder-performance-summary</c>.
    ///
    /// <para><b>No <c>year</c> and no <c>quarter</c>, and that is the endpoint's shape rather than a choice
    /// made here.</b> It answers the filer's whole history, newest first — 53 rows for Berkshire, measured
    /// 2026-08-28, one per quarter in <see cref="GetFilingDatesAsync"/>. There is no per-quarter variant to
    /// offer.</para>
    ///
    /// <para>No <c>limit</c> either: the endpoint ignores it.</para></summary>
    /// <param name="cik">The institutional filer's Central Index Key, padded or unpadded.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter the filer has reported, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cik"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<HolderPerformance>> GetHolderPerformanceAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/holder-performance-summary").With("cik", cik),
            FmpJsonContext.Default.ListHolderPerformance, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: PASS.

- [ ] **Step 8: Mutation-check**

1. Rename `[JsonPropertyName("previousMarketValue")]` to `("lastMarketValue")` →
   `A_captured_holder_performance_row_binds_all_thirty_three_of_its_fields` fails on `Binding.Unbound` naming
   `PreviousMarketValue`. Restore. This is the attribute most likely to be "tidied" into matching its
   neighbours.
2. Rename `[JsonPropertyName("performanceSinceInceptionRelativeToSP500Percentage")]` to drop `SP500` → the same
   assertion fails naming that property. Restore.
3. Add `.With("year", year).With("quarter", quarter)` to `GetHolderPerformanceAsync` (which has no such
   parameters — it will not compile). Record it as a compile-level mutation: the signature itself is the guard.
4. Change `Assert.True(r.PerformancePercentage < 0)` to `> 0` in
   `An_industry_performance_percentage_can_contradict_its_own_dollar_figure` → it fails, confirming the
   assertion is really reading the fixture rather than passing vacuously. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/HolderSummaries.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-industry-breakdown.BRK.json \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-holder-performance-summary.BRK.json
git commit -m "feat: add the two per-filer summaries, by industry and by quarter

holder-industry-breakdown needs a year and a quarter; holder-performance-summary
takes neither and answers the filer's whole history — 53 quarters for Berkshire,
matching what institutional-ownership/dates enumerates.

Performance and performancePercentage disagree in sign on every industry row
measured. FMP computes the percentage against a base it does not publish, so
both are reported as sent and neither is derived here."
```

### Task 5: `SymbolPositions` — one row, unwrapped, with an ownership percentage over 100

**Files:**
- Create: `src/FmpDotNet/Models/SymbolPositions.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-symbol-positions-summary.AAPL.json`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+GetSymbolPositionsAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`

**Interfaces:**
- Consumes: `ThrowIfQuarterOutOfRange` (Task 2).
- Produces: `public sealed record SymbolPositions` (36 fields);
  `Task<SymbolPositions?> GetSymbolPositionsAsync(string symbol, int year, int quarter, CancellationToken ct = default)`
  — **the only nullable-single return in the slice.**

Two things here are unlike everything else in this facade, and both are measured.

**The return type.** The path answers a JSON array, but it carried exactly one row for every symbol measured,
and its 36 fields are whole-market aggregates for that symbol and quarter rather than per-filer rows. So the
method unwraps. It follows the shipped precedent exactly: `SecFilingsEndpoints.GetProfileAsync` calls
`GetListAsync` and then `rows.Count > 0 ? rows[0] : null` (`SecFilingsEndpoints.cs:49-56`) — **not
`GetObjectAsync`**, because the wire shape really is a list. An unknown symbol answers `[]`, which surfaces as
`null`.

**`ownershipPercent` exceeds 100.** AAPL measured 110.1329 and MSFT 128.2744 — two of six symbols. A 13F
double-counts shares held through multiple reporting managers, so a sum over filers legitimately passes shares
outstanding. **No clamp, no range check, no percentage wrapper type**, and the doc comment says why so that the
next reader does not "fix" it.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-symbol-positions-summary.AAPL.json` — the complete
response to `stable/institutional-ownership/symbol-positions-summary?symbol=AAPL&year=2026&quarter=2`, captured
2026-08-28. One row, which is the whole point:

```json
[
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "date": "2026-06-30",
    "investorsHolding": 6435,
    "lastInvestorsHolding": 6392,
    "investorsHoldingChange": 43,
    "numberOf13Fshares": 16201347267,
    "lastNumberOf13Fshares": 9404036028,
    "numberOf13FsharesChange": 6797311239,
    "totalInvested": 2840158192185,
    "lastTotalInvested": 2377140034982,
    "totalInvestedChange": 463018157203,
    "ownershipPercent": 110.1329,
    "lastOwnershipPercent": 63.9264,
    "ownershipPercentChange": 46.2065,
    "newPositions": 206,
    "lastNewPositions": 203,
    "newPositionsChange": 3,
    "increasedPositions": 2781,
    "lastIncreasedPositions": 2599,
    "increasedPositionsChange": 182,
    "closedPositions": 199,
    "lastClosedPositions": 217,
    "closedPositionsChange": -18,
    "reducedPositions": 2941,
    "lastReducedPositions": 3106,
    "reducedPositionsChange": -165,
    "totalCalls": 188086543,
    "lastTotalCalls": 165833284,
    "totalCallsChange": 22253259,
    "totalPuts": 157767138,
    "lastTotalPuts": 134025688,
    "totalPutsChange": 23741450,
    "putCallRatio": 0.8388,
    "lastPutCallRatio": 0.8082,
    "putCallRatioChange": 3.0605
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs`:

```csharp
    // ---- institutional-ownership/symbol-positions-summary --------------------------------------------------------

    [Fact]
    public async Task The_symbol_positions_summary_is_unwrapped_from_its_one_element_array()
    {
        // The wire shape is an array; the answer is one row of whole-market aggregates. GetProfileAsync set
        // this precedent — GetListAsync, then rows[0] — rather than GetObjectAsync, because the response really
        // is a list and pretending otherwise would fail to deserialise.
        var (endpoints, _) = Build(StubHandler.Json(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json")));

        var row = await endpoints.GetSymbolPositionsAsync("AAPL", 2026, 2);

        Assert.NotNull(row);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal("0000320193", row.Cik);
        Assert.Equal(new LocalDate(2026, 6, 30), row.Date);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_rather_than_throwing()
    {
        // Measured 2026-08-28: an unrecognised symbol answers `[]` with HTTP 200, not a 404. Null is this SDK's
        // spelling of that, matching GetProfileAsync.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetSymbolPositionsAsync("NOSUCHTICKER", 2026, 2));
    }

    [Fact]
    public void An_ownership_percentage_over_one_hundred_is_kept_exactly_as_sent()
    {
        // Not a defect and not something to clamp. A 13F double-counts shares held through multiple reporting
        // managers, so summing filers legitimately passes shares outstanding. Measured 2026-08-28, this was
        // over 100 on two of six symbols: AAPL 110.1329 and MSFT 128.2744. A clamp, a range check or a
        // percentage wrapper type would each turn a real measurement into a lie.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(110.1329m, rows[0].OwnershipPercent);
        Assert.Equal(63.9264m, rows[0].LastOwnershipPercent);
        Assert.Equal(46.2065m, rows[0].OwnershipPercentChange);
    }

    [Fact]
    public void A_total_invested_past_two_trillion_binds_rather_than_throwing()
    {
        // 2,840,158,192,185 — 1,322 times int.MaxValue, and past long's ceiling is not the risk here; the risk
        // is somebody typing it int? because "positions" sounds like a count. numberOf13Fshares is the sharper
        // case at 16,201,347,267: seven times int's ceiling on a field whose name says "shares".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(2840158192185m, rows[0].TotalInvested);
        Assert.Equal(16201347267m, rows[0].NumberOf13FShares);
        Assert.Equal(463018157203m, rows[0].TotalInvestedChange);
    }

    [Fact]
    public void The_position_counts_are_ints_and_the_negative_changes_survive_it()
    {
        // These six really are counts of filers, so they stay int? rather than being swept into decimal? for
        // safety — the largest measured is 6,435 and none was ever fractional. The changes go negative, which
        // is what this pins: closedPositionsChange is −18 and reducedPositionsChange is −165 on the captured
        // row, so an unsigned type would be wrong here even though the counts themselves never are.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(6435, rows[0].InvestorsHolding);
        Assert.Equal(43, rows[0].InvestorsHoldingChange);
        Assert.Equal(-18, rows[0].ClosedPositionsChange);
        Assert.Equal(-165, rows[0].ReducedPositionsChange);
    }

    [Fact]
    public async Task The_symbol_positions_call_sends_symbol_year_and_quarter()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetSymbolPositionsAsync("AAPL", 2025, 3);

        Assert.Equal(
            "/stable/institutional-ownership/symbol-positions-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: FAIL to compile — `SymbolPositions` and `GetSymbolPositionsAsync` do not exist.

- [ ] **Step 4: Write `SymbolPositions`**

`src/FmpDotNet/Models/SymbolPositions.cs`. Thirty-six properties in twelve triples — a current figure, the
previous quarter's, and the change — plus `symbol`, `cik` and `date`.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>What every 13F filer together reported about one symbol in one quarter, from
/// <c>stable/institutional-ownership/symbol-positions-summary</c>.
///
/// <para><b>One row, not a list.</b> The path answers a JSON array and it carried exactly one element for every
/// symbol measured 2026-08-28 — these are whole-market aggregates for the symbol and quarter, not per-filer
/// rows. <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetSymbolPositionsAsync"/> therefore returns
/// <c>SymbolPositions?</c>, unwrapping as <see cref="Endpoints.SecFilingsEndpoints.GetProfileAsync"/> does.</para>
///
/// <para><b>Twelve figures, each as a triple:</b> the quarter's value, the previous quarter's (<c>last*</c>),
/// and the change between them. The changes go negative — <see cref="ClosedPositionsChange"/> is −18 on the
/// captured row.</para>
///
/// <para><b><see cref="OwnershipPercent"/> exceeds 100, legitimately.</b> See its own documentation.</para>
///
/// <para><b>Nine fields are genuine counts and are <see cref="int"/>; everything else is
/// <see cref="decimal"/>.</b> The counts are the investors-holding and four position-count triples. The option
/// contract counts are the deliberate exception — see <see cref="TotalCalls"/>.</para></summary>
public sealed record SymbolPositions
{
    /// <summary>The ticker asked for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded — the one place in this facade where the CIK
    /// is an issuer's rather than a filer's, because the row is about the security rather than about a
    /// holder.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>How many institutions reported holding the security. A count of filers — 6,435 for AAPL in
    /// 2026 Q2 — hence <see cref="int"/>.</summary>
    [JsonPropertyName("investorsHolding")] public int? InvestorsHolding { get; init; }

    /// <summary>The same count one quarter earlier.</summary>
    [JsonPropertyName("lastInvestorsHolding")] public int? LastInvestorsHolding { get; init; }

    /// <summary>The change in that count. Goes negative.</summary>
    [JsonPropertyName("investorsHoldingChange")] public int? InvestorsHoldingChange { get; init; }

    /// <summary>Total shares reported across all 13F filers. <b>16,201,347,267 on the captured row — seven
    /// times <see cref="int"/>'s ceiling</b>, on a field whose name says "shares", which is the combination
    /// most likely to be retyped by somebody being helpful.</summary>
    [JsonPropertyName("numberOf13Fshares")] public decimal? NumberOf13FShares { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastNumberOf13Fshares")] public decimal? LastNumberOf13FShares { get; init; }

    /// <summary>The change in reported shares.</summary>
    [JsonPropertyName("numberOf13FsharesChange")] public decimal? NumberOf13FSharesChange { get; init; }

    /// <summary>Total dollars invested across all filers — 2,840,158,192,185 on the captured row.</summary>
    [JsonPropertyName("totalInvested")] public decimal? TotalInvested { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalInvested")] public decimal? LastTotalInvested { get; init; }

    /// <summary>The change in dollars invested.</summary>
    [JsonPropertyName("totalInvestedChange")] public decimal? TotalInvestedChange { get; init; }

    /// <summary>Reported 13F shares as a percentage of shares outstanding — <b>and it exceeds 100.</b>
    ///
    /// <para>Measured 2026-08-28 across six symbols, two were over: AAPL at <c>110.1329</c> and MSFT at
    /// <c>128.2744</c>. This is not a data fault. A 13F is filed by each reporting manager with investment
    /// discretion, so shares held through a chain of managers are reported more than once, and a sum over
    /// filers legitimately passes the shares that exist.</para>
    ///
    /// <para><b>Deliberately unvalidated.</b> No clamp, no range check and no percentage wrapper type: every
    /// one of those would turn a measured value into a wrong one. Treat it as a crowding indicator, not as a
    /// float.</para></summary>
    [JsonPropertyName("ownershipPercent")] public decimal? OwnershipPercent { get; init; }

    /// <summary>The same percentage one quarter earlier.</summary>
    [JsonPropertyName("lastOwnershipPercent")] public decimal? LastOwnershipPercent { get; init; }

    /// <summary>The change, in percentage points.</summary>
    [JsonPropertyName("ownershipPercentChange")] public decimal? OwnershipPercentChange { get; init; }

    /// <summary>How many filers opened a position this quarter. A count.</summary>
    [JsonPropertyName("newPositions")] public int? NewPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastNewPositions")] public int? LastNewPositions { get; init; }

    /// <summary>The change in that count.</summary>
    [JsonPropertyName("newPositionsChange")] public int? NewPositionsChange { get; init; }

    /// <summary>How many filers added to an existing position. A count.</summary>
    [JsonPropertyName("increasedPositions")] public int? IncreasedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastIncreasedPositions")] public int? LastIncreasedPositions { get; init; }

    /// <summary>The change in that count.</summary>
    [JsonPropertyName("increasedPositionsChange")] public int? IncreasedPositionsChange { get; init; }

    /// <summary>How many filers exited entirely. A count.</summary>
    [JsonPropertyName("closedPositions")] public int? ClosedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastClosedPositions")] public int? LastClosedPositions { get; init; }

    /// <summary>The change in that count — <c>−18</c> on the captured row.</summary>
    [JsonPropertyName("closedPositionsChange")] public int? ClosedPositionsChange { get; init; }

    /// <summary>How many filers trimmed a position. A count.</summary>
    [JsonPropertyName("reducedPositions")] public int? ReducedPositions { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastReducedPositions")] public int? LastReducedPositions { get; init; }

    /// <summary>The change in that count — <c>−165</c> on the captured row.</summary>
    [JsonPropertyName("reducedPositionsChange")] public int? ReducedPositionsChange { get; init; }

    /// <summary>Call contracts reported across all filers — 188,086,543 on the captured row.
    ///
    /// <para><b>A count that is deliberately <see cref="decimal"/> anyway</b>, and the exception to this
    /// record's own rule. <see cref="int"/> holds the largest value measured with room to spare, but this is a
    /// share-adjacent quantity sitting in a block of six where every sibling is <c>decimal?</c>, and splitting
    /// the block would read as an accident rather than a decision. The genuine counts on this record are the
    /// investor and position tallies, which count filers rather than instruments.</para></summary>
    [JsonPropertyName("totalCalls")] public decimal? TotalCalls { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalCalls")] public decimal? LastTotalCalls { get; init; }

    /// <summary>The change in call contracts.</summary>
    [JsonPropertyName("totalCallsChange")] public decimal? TotalCallsChange { get; init; }

    /// <summary>Put contracts reported across all filers. <see cref="decimal"/> for the reason on
    /// <see cref="TotalCalls"/>.</summary>
    [JsonPropertyName("totalPuts")] public decimal? TotalPuts { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastTotalPuts")] public decimal? LastTotalPuts { get; init; }

    /// <summary>The change in put contracts.</summary>
    [JsonPropertyName("totalPutsChange")] public decimal? TotalPutsChange { get; init; }

    /// <summary><see cref="TotalPuts"/> over <see cref="TotalCalls"/>.</summary>
    [JsonPropertyName("putCallRatio")] public decimal? PutCallRatio { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastPutCallRatio")] public decimal? LastPutCallRatio { get; init; }

    /// <summary>The change in the ratio. <b>Expressed as a percentage change, not in ratio points</b> —
    /// <c>3.0605</c> on the captured row against a ratio that moved from <c>0.8082</c> to <c>0.8388</c>, a
    /// difference of <c>0.0306</c>. The two other <c>*Change</c> conventions in this record are plain
    /// differences; this one is not, and subtracting the two ratios will not reproduce it.</summary>
    [JsonPropertyName("putCallRatioChange")] public decimal? PutCallRatioChange { get; init; }
}
```

**`numberOf13Fshares` is spelled with a lower-case `s` in `shares` on the wire**, against the C# `NumberOf13FShares`.
`PropertyNameCaseInsensitive` covers that, but the attribute states it rather than relying on the option
staying set — the same reasoning `StatementReuseBindingTests` records for the CSV-derived models.

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<SymbolPositions>))]
```

**One entry, not two.** The spec hedges that a bare `SymbolPositions` entry may also be needed "if unwrapped
through `GetObjectAsync`". It is not unwrapped that way — see Step 6 — and `FmpJsonContext` carries only
`List<SecProfile>` for the identical precedent.

- [ ] **Step 6: Add the method**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>What every 13F filer together reported about one symbol in one quarter, or
    /// <see langword="null"/> when FMP has nothing —
    /// <c>stable/institutional-ownership/symbol-positions-summary</c>.
    ///
    /// <para><b>One row, unwrapped from the array FMP sends.</b> The path answers a JSON array that carried
    /// exactly one element for every symbol measured 2026-08-28; its 36 fields are whole-market aggregates for
    /// the symbol and quarter rather than per-filer rows, so a list return would make every caller write
    /// <c>[0]</c>. Unwrapped the way <see cref="SecFilingsEndpoints.GetProfileAsync"/> does it.</para>
    ///
    /// <para><b><see cref="SymbolPositions.OwnershipPercent"/> can exceed 100</b>, legitimately — read its
    /// documentation before treating it as a fraction.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The quarter's aggregates, or <see langword="null"/> when FMP has none — which is what an
    /// unknown symbol or an unfiled quarter answers, with HTTP 200 rather than a 404.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<SymbolPositions?> GetSymbolPositionsAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ThrowIfQuarterOutOfRange(quarter);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/symbol-positions-summary")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListSymbolPositions, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalOwnershipTests`
Expected: PASS.

- [ ] **Step 8: Mutation-check**

1. Retype `NumberOf13FShares` as `int?` → `A_total_invested_past_two_trillion_binds_rather_than_throwing`
   fails with a `System.Text.Json` overflow rather than an assertion. Restore.
2. Retype `ClosedPositionsChange` as `uint?` → the build fails, because `System.Text.Json` will read it but the
   fixture's `−18` cannot round-trip. Record the compile/parse failure; this is the guard for the negative
   changes.
3. Change `rows.Count > 0 ? rows[0] : null` to `rows[0]` → `An_unknown_symbol_answers_null_rather_than_throwing`
   fails with `ArgumentOutOfRangeException` from the list rather than returning null. Restore.
4. Add a clamp — `Math.Min(value, 100)` — anywhere on `OwnershipPercent` →
   `An_ownership_percentage_over_one_hundred_is_kept_exactly_as_sent` fails. Restore. This is the mutation that
   protects the decision from a future "fix".

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/SymbolPositions.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/InstitutionalOwnershipTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-symbol-positions-summary.AAPL.json
git commit -m "feat: add the whole-market position summary for one symbol and quarter

Returns SymbolPositions? rather than a list: the path answers an array that
carried exactly one row for every symbol measured, and its 36 fields are
market-wide aggregates. Unwrapped the way GetProfileAsync does it — GetListAsync
then rows[0], not GetObjectAsync, because the wire shape really is a list.

ownershipPercent is over 100 on two of six symbols measured and is kept exactly
as sent. A 13F double-counts shares held through multiple reporting managers, so
summing filers legitimately passes shares outstanding."
```

### Task 6: The market-wide pair — the fractional aggregate, and the date trap that fails silently

**Files:**
- Create: `src/FmpDotNet/Models/InstitutionalFiling.cs` — `IndustryOwnershipSummary` (3) and `InstitutionalFiling` (8)
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-industry-summary.2025Q4.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/institutional-ownership-latest.head.json`
- Create: `tests/FmpDotNet.Tests/InstitutionalFilingTests.cs`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+MaxOwnershipPageSize`, `+GetIndustrySummaryAsync`, `+GetLatestFilingsAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: `ThrowIfQuarterOutOfRange` (Task 2), `NullableDateAtMidnightJsonConverter`,
  `NullableLocalDateTimeJsonConverter`.
- Produces: `public sealed record IndustryOwnershipSummary`, `public sealed record InstitutionalFiling`;
  `public const int MaxOwnershipPageSize = 1000` — **consumed by Task 7**;
  `Task<IReadOnlyList<IndustryOwnershipSummary>> GetIndustrySummaryAsync(int year, int quarter, CancellationToken ct = default)`;
  `Task<IReadOnlyList<InstitutionalFiling>> GetLatestFilingsAsync(int page = 0, int limit = 100, CancellationToken ct = default)`.

These two are the only paths in the facade that take no filer and no symbol, and each carries one of the
slice's two sharpest traps.

**`industry-summary` is the evidence the whole `decimal?` ruling rests on.** Every other money field in this
slice was integral on all 7,946 rows sampled. `industryValue` is the same kind of quantity — an aggregate dollar
value — and is fractional on **53 of 394 rows** in 2025 Q4. That is why `long?` was refused everywhere. This
task is where a `long?` retyping finally fails a test.

**`institutional-ownership/latest` spells its two dates differently from every other path in the slice, and
getting it wrong is silent.** `filingDate` arrives as `"2026-08-28 00:00:00"` — midnight on 1000 of 1000 rows —
and `acceptedDate` as `"2026-08-28 15:47:03"` — midnight on 0 of 1000. `NullableLocalDateJsonConverter` parses
with `LocalDatePattern.Iso` and **returns null on a parse failure rather than throwing**
(`NodaConverters.cs:35-48`), so pointing it at either field nulls every value with no exception, no failing
assertion, and nothing in a diff. A fixture-backed test is the only thing that makes it loud.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-industry-summary.2025Q4.json` — three rows of
`stable/institutional-ownership/industry-summary?year=2025&quarter=4`, captured 2026-08-28. The full response
was 394 rows, of which 53 carried a fractional `industryValue`. **Row 1 is the capture's first row and is
integral; rows 2 and 3 are two of the 53 that are not** — the second is the largest fractional value in the
response:

```json
[
  {
    "industryTitle": "ABRASIVE, ASBESTOS & MISC NONMETALLIC MINERAL PRODS",
    "industryValue": 8775759887,
    "date": "2025-12-31"
  },
  {
    "industryTitle": "BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)",
    "industryValue": 523604028974.8208,
    "date": "2025-12-31"
  },
  {
    "industryTitle": "AGRICULTURAL SERVICES",
    "industryValue": 1769618150.15,
    "date": "2025-12-31"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/institutional-ownership-latest.head.json` — the first three rows of
`stable/institutional-ownership/latest?page=0&limit=100`, captured 2026-08-28, verbatim. All three share a
`filingDate` of midnight and carry three different real `acceptedDate` clocks, which is the trap in one
response:

```json
[
  {
    "cik": "0002110329",
    "name": "CORNERSTONE FINANCIAL MANAGEMENT LLC",
    "date": "2026-06-30",
    "filingDate": "2026-08-28 00:00:00",
    "acceptedDate": "2026-08-28 15:47:03",
    "formType": "13F-HR/A",
    "link": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000015/0002110329-26-000015-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000015/xslForm13F_X02/primary_doc.xml"
  },
  {
    "cik": "0002110329",
    "name": "CORNERSTONE FINANCIAL MANAGEMENT LLC",
    "date": "2026-03-31",
    "filingDate": "2026-08-28 00:00:00",
    "acceptedDate": "2026-08-28 15:30:34",
    "formType": "13F-HR/A",
    "link": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000014/0002110329-26-000014-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000014/cornerstone13fq12026.xml"
  },
  {
    "cik": "0002110329",
    "name": "CORNERSTONE FINANCIAL MANAGEMENT LLC",
    "date": "2025-12-31",
    "filingDate": "2026-08-28 00:00:00",
    "acceptedDate": "2026-08-28 15:19:01",
    "formType": "13F-HR/A",
    "link": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000013/0002110329-26-000013-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/2110329/000211032926000013/cornerstone13fq42025.xml"
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/InstitutionalFilingTests.cs` — a file of its own, because what it guards is the two
converters rather than the facade:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The two market-wide 13F paths, and the two traps they carry.
///
/// <para><b>The date trap fails silently, which is why it is a test rather than a comment.</b>
/// <c>institutional-ownership/latest</c> sends <c>filingDate</c> as <c>"2026-08-28 00:00:00"</c> — midnight on
/// 1000 of 1000 rows measured 2026-08-28 — and <c>acceptedDate</c> as <c>"2026-08-28 15:47:03"</c>, midnight on
/// 0 of 1000. Every other path in this slice sends bare ISO dates and uses
/// <see cref="NullableLocalDateJsonConverter"/>, which parses with <c>LocalDatePattern.Iso</c> and returns null
/// on failure rather than throwing. Point it at either field here and every date reads null: no exception, no
/// failing assertion elsewhere, nothing in a diff.</para>
///
/// <para><b>The fractional trap is the evidence behind every <c>decimal?</c> in this slice.</b>
/// <c>industryValue</c> is fractional on 53 of 394 rows, while every money field on every other path measured
/// was integral. <c>System.Text.Json</c> throws on a fractional value bound to an integer property and
/// <c>FmpTransport</c> does not wrap the deserialiser, so one such value costs the caller the response.</para></summary>
public class InstitutionalFilingTests
{
    private static (InstitutionalOwnershipEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InstitutionalOwnershipEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- institutional-ownership/industry-summary ----------------------------------------------------------------

    [Fact]
    public void A_fractional_industry_value_binds_rather_than_throwing()
    {
        // THE test for the decimal? ruling. 523,604,028,974.8208 is a dollar aggregate with four decimal places,
        // and 53 of 394 rows in this quarter carry one. Retype IndustryValue as long? or int? and
        // System.Text.Json throws — costing the caller all 394 rows, not the one field. Every money and share
        // field in this slice is decimal? because of these 53 rows, even though the other 7,946 rows measured
        // were integral and would have justified long?.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-industry-summary.2025Q4.json"),
            FmpJsonContext.Default.ListIndustryOwnershipSummary)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(8775759887m, rows[0].IndustryValue);
        Assert.Equal(523604028974.8208m, rows[1].IndustryValue);
        Assert.Equal(1769618150.15m, rows[2].IndustryValue);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
    }

    [Fact]
    public void An_industry_summary_row_carries_its_quarter_end_and_its_label()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-industry-summary.2025Q4.json"),
            FmpJsonContext.Default.ListIndustryOwnershipSummary)!;

        Assert.Equal("BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)", rows[1].IndustryTitle);
        Assert.All(rows, r => Assert.Equal(new LocalDate(2025, 12, 31), r.Date));
    }

    [Fact]
    public async Task The_industry_summary_call_sends_year_and_quarter_and_nothing_else()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetIndustrySummaryAsync(2025, 4);

        Assert.Equal("/stable/institutional-ownership/industry-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=4", handler.Requests[0].Query);
        Assert.DoesNotContain("cik=", handler.Requests[0].Query);
        Assert.DoesNotContain("symbol=", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task An_industry_summary_quarter_outside_one_to_four_is_refused(int quarter)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustrySummaryAsync(2025, quarter));

        Assert.Empty(handler.Requests);
    }

    // ---- institutional-ownership/latest --------------------------------------------------------------------------

    [Fact]
    public void The_filing_feeds_two_dates_use_two_different_converters()
    {
        // The silent one, and the reason this file exists. filingDate is "2026-08-28 00:00:00" — a date wearing
        // a datetime's clothes, midnight on 1000 of 1000 rows — and reads as a LocalDate through
        // NullableDateAtMidnightJsonConverter. acceptedDate is "2026-08-28 15:47:03" — a real clock, midnight on
        // 0 of 1000 — and keeps its time as a LocalDateTime.
        //
        // Point NullableLocalDateJsonConverter at either and LocalDatePattern.Iso rejects the trailing time,
        // and the converter returns null rather than throwing (NodaConverters.cs:35-48). Every date in every row
        // would read null and nothing would say so.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].FilingDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 47, 3), rows[0].AcceptedDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 30, 34), rows[1].AcceptedDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 19, 1), rows[2].AcceptedDate);
    }

    [Fact]
    public void The_accepted_time_is_information_and_the_filing_time_is_not()
    {
        // Why they are two types rather than one. All three rows share a filingDate to the second — the dummy
        // midnight — while their acceptedDate values are 16 and 11 minutes apart. Reading acceptedDate as a
        // LocalDate would discard the only field that orders three filings made on the same day; reading
        // filingDate as a LocalDateTime would leak a meaningless 00:00:00 into every comparison a caller writes.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.All(rows, r => Assert.Equal(new LocalDate(2026, 8, 28), r.FilingDate));
        Assert.True(rows[0].AcceptedDate > rows[1].AcceptedDate);
        Assert.True(rows[1].AcceptedDate > rows[2].AcceptedDate);
    }

    [Fact]
    public void A_captured_latest_filing_binds_all_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0002110329", rows[0].Cik);
        Assert.Equal("CORNERSTONE FINANCIAL MANAGEMENT LLC", rows[0].Name);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal("13F-HR/A", rows[0].FormType);
        Assert.EndsWith("-index.htm", rows[0].Link);
        Assert.EndsWith("primary_doc.xml", rows[0].FinalLink);
    }

    [Fact]
    public void A_date_that_is_null_or_in_the_wrong_shape_costs_one_field_not_the_row()
    {
        // House rule for every date converter: one bad stamp must not abort the response and take the other
        // seven fields with it. The bare-ISO case is NOT a measured wire form on this path — 1000 of 1000 rows
        // carried the time — it is here to pin that an unexpected shape reads null rather than throwing.
        var rows = JsonSerializer.Deserialize(
            """
            [{"cik":"A","filingDate":null,"acceptedDate":null},
             {"cik":"B","filingDate":"","acceptedDate":""},
             {"cik":"C","filingDate":"2026-08-28","acceptedDate":"2026-08-28"}]
            """, FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Null(r.FilingDate));
        Assert.All(rows, r => Assert.Null(r.AcceptedDate));
        Assert.Equal("C", rows[2].Cik);
    }

    [Fact]
    public async Task The_latest_filings_call_sends_page_and_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestFilingsAsync(page: 3, limit: 250);

        Assert.Equal("/stable/institutional-ownership/latest", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=3", handler.Requests[0].Query);
        Assert.Contains("limit=250", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    public async Task A_latest_filings_limit_above_the_measured_cap_is_refused(int limit)
    {
        // Measured 2026-08-28: limit=2000 answered exactly 1,000 rows with HTTP 200 and nothing in the body to
        // say it had been trimmed. The feed paginates, so a caller stepping `page` by 2,000 reads half the
        // archive and is never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestFilingsAsync(limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_latest_filings_limit_exactly_at_the_cap_is_accepted()
    {
        // The off-by-one boundary. See the note on the holder-analytics twin in InstitutionalOwnershipTests.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestFilingsAsync(
            limit: InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
    }

    [Fact]
    public void The_ownership_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);
        // And it is NOT the same as the holder-analytics cap, which is 100. One constant for both would have
        // let a caller ask extract-analytics/holder for 1,000 and silently receive 100.
        Assert.NotEqual(
            InstitutionalOwnershipEndpoints.MaxOwnershipPageSize,
            InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalFilingTests`
Expected: FAIL to compile — neither record, neither method, and `MaxOwnershipPageSize` do not exist.

- [ ] **Step 4: Write both records**

`src/FmpDotNet/Models/InstitutionalFiling.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One industry's total reported 13F value for one quarter, across every filer, from
/// <c>stable/institutional-ownership/industry-summary</c>.
///
/// <para>Three fields and 394 rows per quarter — one per SIC industry, in the same vocabulary
/// <see cref="HolderIndustryBreakdown.IndustryTitle"/> uses.</para>
///
/// <para><b>This is the record that decided the numeric typing for the whole group.</b> See
/// <see cref="IndustryValue"/>.</para></summary>
public sealed record IndustryOwnershipSummary
{
    /// <summary>The SIC industry label — <c>"BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)"</c>.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>Total dollars 13F filers reported in the industry that quarter.
    ///
    /// <para><b>Fractional on 53 of 394 rows measured 2026-08-28</b> — <c>523604028974.8208</c> among them —
    /// while every money field on every other path in this group was integral across 7,946 rows. That
    /// asymmetry is why <i>every</i> money and share field in this slice is <see cref="decimal"/> rather than
    /// <c>long</c>: the family clearly goes fractional, and which member does it in which quarter is not
    /// stable.</para>
    ///
    /// <para><b>The cost of getting it wrong is the whole response.</b> <c>System.Text.Json</c> throws on a
    /// fractional value bound to an integer property, and <c>FmpTransport</c> does not wrap
    /// <c>DeserializeAsync</c> — so a single such value would cost the caller all 394 rows rather than the one
    /// field. See <see cref="CompanyProfile.Volume"/>.</para></summary>
    [JsonPropertyName("industryValue")] public decimal? IndustryValue { get; init; }

    /// <summary>The quarter end — <c>2025-12-31</c>. Bare ISO on this path.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}

/// <summary>One 13F filing as it arrives, from <c>stable/institutional-ownership/latest</c> — the whole-market
/// feed of new and amended 13F submissions, newest first.
///
/// <para><b>The two dates on this record use two different converters, and no other record in this group
/// does.</b> Measured 2026-08-28 over 1,000 rows: <see cref="FilingDate"/>'s time component was
/// <c>00:00:00</c> on 1,000 of 1,000 — a date wearing a datetime's clothes — while
/// <see cref="AcceptedDate"/> was at exactly midnight on 0 of 1,000 and is a real clock. Reading either with
/// the other's converter compiles and binds; reading either with the bare-ISO
/// <see cref="NullableLocalDateJsonConverter"/> that the rest of this group uses returns <see langword="null"/>
/// on every row without throwing.</para>
///
/// <para><b><see cref="FormType"/> here is 13F vocabulary, not Form 4 vocabulary.</b> See its
/// documentation.</para></summary>
public sealed record InstitutionalFiling
{
    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name — <c>"CORNERSTONE FINANCIAL MANAGEMENT LLC"</c>. Spelled <c>name</c> on this
    /// path, not <c>investorName</c> as on <see cref="HolderPerformance.InvestorName"/> and
    /// <see cref="HolderAnalytics.InvestorName"/>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The quarter end the filing reports on. Bare ISO — this is the one date on the record that is
    /// spelled the way the rest of the group spells its dates.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date the filing was submitted.
    ///
    /// <para><b>A date, not a timestamp.</b> The wire sends <c>"2026-08-28 00:00:00"</c> and the time was
    /// <c>00:00:00</c> on 1,000 of 1,000 rows measured 2026-08-28, so it is discarded — see
    /// <see cref="NullableDateAtMidnightJsonConverter"/>. All three rows of the captured page share this field
    /// to the second while their <see cref="AcceptedDate"/> values differ by minutes, which is what the
    /// midnight actually means: it carries no time.</para></summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The moment EDGAR accepted the submission — <c>"2026-08-28 15:47:03"</c>.
    ///
    /// <para><b>A <see cref="LocalDateTime"/>, deliberately, rather than an <see cref="Instant"/>.</b>
    /// <see cref="SecFiling.AcceptedDate"/> is an <c>Instant</c> because a DST measurement established EDGAR's
    /// wall clock as US Eastern on that path. No such measurement was taken here, and inventing a zone would
    /// invent a fact: this SDK reports the wall clock FMP sent and leaves the zone to a caller who knows
    /// it.</para>
    ///
    /// <para>The time is real information: the three captured rows are 16 and 11 minutes apart on the same
    /// filing date, and it is the only field that orders them.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateTimeJsonConverter))]
    public LocalDateTime? AcceptedDate { get; init; }

    /// <summary>The 13F form type — <c>"13F-HR"</c>, <c>"13F-HR/A"</c>, <c>"13F-NT"</c>, <c>"13F-NT/A"</c>.
    ///
    /// <para><b>Not the same vocabulary as <c>InsiderTrade.FormType</c></b>, which carries <c>"3"</c>,
    /// <c>"4"</c> and <c>"4/A"</c>. Two field names spelled alike over two disjoint sets of values, which is
    /// why the two records are not unified: doing so would model a coincidence.</para>
    ///
    /// <para>A raw <see cref="string"/> rather than an enum, for the reason on
    /// <see cref="SecFiling.FormType"/>.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The EDGAR filing-index page for the accession.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The primary document itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
```

- [ ] **Step 5: Register both**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<IndustryOwnershipSummary>))]
[JsonSerializable(typeof(List<InstitutionalFiling>))]
```

- [ ] **Step 6: Add the constant and both methods**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>The largest page <see cref="GetLatestFilingsAsync"/> and
    /// <c>GetBeneficialOwnershipAsync</c> will ask for, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="SecFilingsEndpoints.MaxSecFilingPageSize"/>: measured 2026-08-28,
    /// <c>institutional-ownership/latest?limit=2000</c> answered exactly 1,000 rows with HTTP 200 and nothing
    /// in the body to say the request had been trimmed. The feed paginates, so a caller who asks for 2,000 and
    /// advances <c>page</c> by 2,000 reads half the archive and is never told.</para>
    ///
    /// <para><b>Not the cap for <see cref="GetHolderAnalyticsAsync"/></b>, which clamps at 100 — see
    /// <see cref="MaxHolderAnalyticsPageSize"/>. One constant for the whole group would have let a caller ask
    /// that path for 1,000 rows and receive 100 in silence.</para>
    ///
    /// <para><b>On <c>GetBeneficialOwnershipAsync</c> this is a sibling-derived bound rather than a
    /// measured one.</b> No query on that path produced a result set large enough to provoke a clamp — the
    /// widest found was 180 rows, and <c>limit=2000</c> for AAPL answered its whole 99-row set. The guard is
    /// applied there because an unbounded <c>limit</c> is worse than a conservative one, not because 1,000 was
    /// observed to be its ceiling.</para></summary>
    public const int MaxOwnershipPageSize = 1000;

    /// <summary>Total 13F-reported value by industry for one quarter, across the whole market —
    /// <c>stable/institutional-ownership/industry-summary</c>.
    ///
    /// <para>394 rows per quarter, measured 2026-08-28, one per SIC industry. Takes no filer and no symbol: it
    /// is the market's whole 13F universe cut one way.</para>
    ///
    /// <para><b>This is the path whose values are fractional</b> — 53 of those 394 rows — which is why every
    /// money field in this group is <c>decimal?</c>. See
    /// <see cref="IndustryOwnershipSummary.IndustryValue"/>.</para></summary>
    /// <param name="year">The calendar year of the quarter end. Not range-checked.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4. Required by FMP.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is outside 1 to 4.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryOwnershipSummary>> GetIndustrySummaryAsync(
        int year, int quarter, CancellationToken ct = default)
    {
        ThrowIfQuarterOutOfRange(quarter);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/industry-summary")
                .With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListIndustryOwnershipSummary, ct);
    }

    /// <summary>The whole-market feed of 13F filings as they arrive, newest first —
    /// <c>stable/institutional-ownership/latest</c>.
    ///
    /// <para>Every filer, every quarter, new submissions and amendments alike:
    /// <see cref="InstitutionalFiling.FormType"/> carried <c>13F-HR</c>, <c>13F-HR/A</c>, <c>13F-NT</c> and
    /// <c>13F-NT/A</c> in the measured page. Use it to notice that a filer has reported; use
    /// <see cref="GetHoldingsAsync"/> to read what they reported.</para>
    ///
    /// <para><b>The two dates on the row are spelled differently from the rest of this group and mean different
    /// things.</b> See <see cref="InstitutionalFiling.FilingDate"/> and
    /// <see cref="InstitutionalFiling.AcceptedDate"/>.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxOwnershipPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxOwnershipPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InstitutionalFiling>> GetLatestFilingsAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxOwnershipPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/institutional-ownership/latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInstitutionalFiling, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InstitutionalFilingTests`
Expected: PASS.

- [ ] **Step 8: Mutation-check — this is the task with the most valuable mutations in the slice**

1. Retype `IndustryValue` as `long?` → `A_fractional_industry_value_binds_rather_than_throwing` fails with a
   `System.Text.Json` exception. Restore. **This is the mutation that defends every `decimal?` in the slice**;
   nothing else in the codebase fails when the money fields are retyped, because their captured values are
   integral.
2. Change `InstitutionalFiling.FilingDate`'s converter to `NullableLocalDateJsonConverter` →
   `The_filing_feeds_two_dates_use_two_different_converters` fails with `FilingDate` null. Restore. **The
   mutation compiles and throws nothing**, which is precisely the failure this test exists for.
3. Change `AcceptedDate`'s converter to `NullableDateAtMidnightJsonConverter` and its type to `LocalDate?` →
   `The_accepted_time_is_information_and_the_filing_time_is_not` fails, because the three rows collapse to one
   value and the ordering assertions stop discriminating. Restore.
4. Change `MaxOwnershipPageSize` to `100` → `The_ownership_page_cap_is_the_measured_one` fails on both
   assertions, including the `NotEqual` that pins the two caps apart. Restore.
5. Swap `.With("year", year)` and `.With("quarter", quarter)` values in `GetIndustrySummaryAsync` →
   `The_industry_summary_call_sends_year_and_quarter_and_nothing_else` fails. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/InstitutionalFiling.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/InstitutionalFilingTests.cs \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-industry-summary.2025Q4.json \
        tests/FmpDotNet.Tests/Fixtures/institutional-ownership-latest.head.json
git commit -m "feat: add the two market-wide 13F paths, and pin both of their traps

industry-summary is the evidence behind every decimal? in this slice:
industryValue is fractional on 53 of 394 rows while every other money field
measured across 7,946 rows was integral. Retyping it long? throws and costs the
caller all 394 rows.

institutional-ownership/latest is the one path whose dates are not bare ISO —
filingDate is midnight on 1000 of 1000 rows, acceptedDate on 0 of 1000, so they
take two different converters. Pointing the group's ISO converter at either
returns null on every row without throwing, which is why this is a test."
```

### Task 7: `BeneficialOwnership` — six string numerics, and a `limit` with no `page`

**Files:**
- Create: `src/FmpDotNet/Models/BeneficialOwnership.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/acquisition-of-beneficial-ownership.AAPL.json`
- Create: `tests/FmpDotNet.Tests/BeneficialOwnershipTests.cs`
- Modify: `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs` — `+GetBeneficialOwnershipAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: `MaxOwnershipPageSize` (Task 6), `TolerantDecimalJsonConverter`,
  `NullableLocalDateJsonConverter`.
- Produces: `public sealed record BeneficialOwnership` (15 fields);
  `Task<IReadOnlyList<BeneficialOwnership>> GetBeneficialOwnershipAsync(string symbol, int limit = 100, CancellationToken ct = default)`.

**This is the path the spec moved.** FMP files `stable/acquisition-of-beneficial-ownership` under Insider
Trades; it lands on `fmp.InstitutionalOwnership` instead, because an SC 13D/G is the disclosure an investor
makes on crossing 5% of a class — its subject is an institutional stake, its fields are voting and dispositive
power, and its reporting person is an entity (`"The Vanguard Group"`). It shares nothing with a Form 4
transaction but the word "ownership".

Two measured shapes make it unlike its new neighbours:

**Six numerics arrive as JSON strings** — `{"soleVotingPower": "0", "percentOfClass": "7.48"}`. Across 422 rows
every non-null value parsed as a number: no `"N/A"`, no thousands separators. `TolerantDecimalJsonConverter`
reads a `String` token through `decimal.TryParse` with `NumberStyles.Float`, invariant, returning null on
failure and never throwing. Used as shipped; no new converter.

**It honours `limit` and ignores `page`.** `page=0` and `page=1` returned byte-identical bodies. Honouring one
does not predict honouring the other, which is why each was measured separately — so the method takes `limit`
and no `page`.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/acquisition-of-beneficial-ownership.AAPL.json` — three rows of
`stable/acquisition-of-beneficial-ownership?symbol=AAPL`, captured 2026-08-28. The full response was 99 rows.
**Rows 1 and 2 are the capture's first two; row 3 is its row 55, the only one carrying a null
`sharedVotingPower`** — the string-numeric null case, which the head of the response did not contain. Row 3 also
carries a two-code `typeOfReportingPerson` (`"EP, IN"`) and a lower-cased state, both of which are FMP's values
rather than typos:

```json
[
  {
    "cik": "0000320193",
    "symbol": "AAPL",
    "filingDate": "2026-04-29",
    "acceptedDate": "2026-04-29",
    "cusip": "037833100",
    "nameOfReportingPerson": "Vanguard Capital Management",
    "citizenshipOrPlaceOfOrganization": "PENNSYLVANIA",
    "soleVotingPower": "0",
    "sharedVotingPower": "0",
    "soleDispositivePower": "0",
    "sharedDispositivePower": "0",
    "amountBeneficiallyOwned": "1099168953",
    "percentOfClass": "7.48",
    "typeOfReportingPerson": "IA",
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000210011926000139/xslSCHEDULE_13G_X02/primary_doc.xml"
  },
  {
    "cik": "0000320193",
    "symbol": "AAPL",
    "filingDate": "2026-03-26",
    "acceptedDate": "2026-03-26",
    "cusip": "037833100",
    "nameOfReportingPerson": "The Vanguard Group",
    "citizenshipOrPlaceOfOrganization": "PENNSYLVANIA",
    "soleVotingPower": "0",
    "sharedVotingPower": "0",
    "soleDispositivePower": "0",
    "sharedDispositivePower": "0",
    "amountBeneficiallyOwned": "0",
    "percentOfClass": "0",
    "typeOfReportingPerson": "IA",
    "url": "https://www.sec.gov/Archives/edgar/data/102909/000010290926000630/xslSCHEDULE_13G_X02/primary_doc.xml"
  },
  {
    "cik": "0000320193",
    "symbol": "AAPL",
    "filingDate": "2015-02-10",
    "acceptedDate": "2015-02-10",
    "cusip": "037833100",
    "nameOfReportingPerson": "Vanguard Group - 23-1945930",
    "citizenshipOrPlaceOfOrganization": "Pennsylvania",
    "soleVotingPower": "10208579",
    "sharedVotingPower": null,
    "soleDispositivePower": "322573028",
    "sharedDispositivePower": "9666535",
    "amountBeneficiallyOwned": "332239563",
    "percentOfClass": "5.66",
    "typeOfReportingPerson": "EP, IN",
    "url": "https://www.sec.gov/Archives/edgar/data/102909/000093247115003679/appleinc.htm"
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/BeneficialOwnershipTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>SC 13D/G beneficial-ownership disclosures — the one path in this slice whose numbers arrive as
/// JSON strings.
///
/// <para><b>Six of the fifteen fields are quoted numbers</b> — <c>"soleVotingPower": "0"</c>,
/// <c>"percentOfClass": "7.48"</c> — and across 422 rows measured 2026-08-28 every non-null value parsed
/// cleanly: no <c>"N/A"</c>, no separators, no currency symbols. <see cref="TolerantDecimalJsonConverter"/>
/// already reads a String token and returns null rather than throwing on anything it cannot parse, so it is
/// used exactly as shipped.</para>
///
/// <para><b>The path honours <c>limit</c> and ignores <c>page</c></b>, measured separately: <c>page=0</c> and
/// <c>page=1</c> returned byte-identical bodies. That asymmetry is why the method has one and not the
/// other.</para></summary>
public class BeneficialOwnershipTests
{
    private static (InstitutionalOwnershipEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InstitutionalOwnershipEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_quoted_number_binds_as_a_decimal()
    {
        // The wire sends "7.48", not 7.48. Without TolerantDecimalJsonConverter these six properties would
        // need the context's AllowReadingFromString to carry them, which it would — but a value the parser
        // rejects would then throw and cost the response, rather than binding null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(7.48m, rows[0].PercentOfClass);
        Assert.Equal(1099168953m, rows[0].AmountBeneficiallyOwned);
        Assert.Equal(0m, rows[0].SoleVotingPower);
        Assert.Equal(5.66m, rows[2].PercentOfClass);
        Assert.Equal(10208579m, rows[2].SoleVotingPower);
        Assert.Equal(322573028m, rows[2].SoleDispositivePower);
    }

    [Fact]
    public void A_null_quoted_number_binds_null_rather_than_throwing()
    {
        // Row 3 is the capture's row 55 — the only one of 99 with a null sharedVotingPower. The head of the
        // response had none, which is why it was pulled forward: a converter that throws on null would pass
        // every test written against the first three rows and fail in production.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Null(rows[2].SharedVotingPower);
        // And the rest of that row still arrives.
        Assert.Equal(9666535m, rows[2].SharedDispositivePower);
        Assert.Equal(332239563m, rows[2].AmountBeneficiallyOwned);
        Assert.Equal("Vanguard Group - 23-1945930", rows[2].NameOfReportingPerson);
    }

    [Fact]
    public void An_unparseable_quoted_number_costs_one_field_not_the_row()
    {
        // Not a measured wire form — all 422 values parsed. This pins the converter's contract: a value it
        // cannot read must bind null rather than abort the response and take the other fourteen fields with it.
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","percentOfClass":"N/A","amountBeneficiallyOwned":"1,234"}]""",
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Single(rows);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Null(rows[0].PercentOfClass);
        Assert.Null(rows[0].AmountBeneficiallyOwned);
    }

    [Fact]
    public void A_captured_disclosure_binds_all_fifteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Empty(Binding.Unbound(rows[2]));
        Assert.Equal("0000320193", rows[2].Cik);
        Assert.Equal("AAPL", rows[2].Symbol);
        Assert.Equal("037833100", rows[2].Cusip);
        Assert.Equal("Pennsylvania", rows[2].CitizenshipOrPlaceOfOrganization);
        // Two SEC reporting-person codes in one field, comma-joined. FMP's value, not a parse target.
        Assert.Equal("EP, IN", rows[2].TypeOfReportingPerson);
        Assert.Equal(new LocalDate(2015, 2, 10), rows[2].FilingDate);
        Assert.Equal(new LocalDate(2015, 2, 10), rows[2].AcceptedDate);
    }

    [Fact]
    public void The_reporting_person_is_an_entity_which_is_why_this_path_is_not_on_the_insider_facade()
    {
        // The one assertion that pins the regrouping decision. An SC 13D/G reporting person is an institution —
        // "Vanguard Capital Management", "The Vanguard Group" — filing about a stake, not an officer filing
        // about a transaction. Every row in the capture names an entity, and none carries a transaction type,
        // a transaction date, a price or a securities-transacted count. Filed next to insider-trading/* it
        // would be the only path in that facade that is not an insider transaction.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.All(rows, r => Assert.Contains("Vanguard", r.NameOfReportingPerson));
        Assert.All(rows, r => Assert.NotNull(r.SoleDispositivePower));
    }

    [Fact]
    public async Task The_beneficial_ownership_call_sends_a_limit_and_no_page()
    {
        // Measured 2026-08-28 and separately from `limit`: page=0 and page=1 returned byte-identical bodies.
        // A `page` parameter here would be accepted, ignored, and invisible in the response — so it is not
        // offered, and this test fails if somebody adds it back by symmetry with the group's other paged paths.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetBeneficialOwnershipAsync("AAPL", limit: 50);

        Assert.Equal("/stable/acquisition-of-beneficial-ownership", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    public async Task A_beneficial_ownership_limit_above_the_sibling_cap_is_refused(int limit)
    {
        // 1,000 is a sibling-derived bound rather than a measured one on this path: the widest result set found
        // was 180 rows and limit=2000 for AAPL answered its whole 99-row set, so no query provoked a clamp. The
        // guard is applied because an unbounded limit is worse than a conservative one — see
        // MaxOwnershipPageSize, which says so.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetBeneficialOwnershipAsync("AAPL", limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_beneficial_ownership_limit_exactly_at_the_cap_is_accepted()
    {
        // The off-by-one boundary, on the third of the three guards that share this shape.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetBeneficialOwnershipAsync(
            "AAPL", limit: InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_with_ArgumentNullException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => endpoints.GetBeneficialOwnershipAsync(null!));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~BeneficialOwnershipTests`
Expected: FAIL to compile — `BeneficialOwnership` and `GetBeneficialOwnershipAsync` do not exist.

- [ ] **Step 4: Write `BeneficialOwnership`**

`src/FmpDotNet/Models/BeneficialOwnership.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One SC 13D/G beneficial-ownership disclosure — the filing an investor makes on crossing 5% of a
/// class — from <c>stable/acquisition-of-beneficial-ownership</c>.
///
/// <para><b>FMP files this path under Insider Trades; this SDK files it under institutional ownership.</b> The
/// reporting person is an entity — <c>"The Vanguard Group"</c>, <c>"General Star National Insurance
/// Company"</c> — the subject is a stake rather than a transaction, and the fields are voting and dispositive
/// power. It shares nothing with a Form 4 but the word "ownership". See
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints"/>.</para>
///
/// <para><b>Six of the fifteen fields arrive as JSON strings</b> — <c>"soleVotingPower": "0"</c>,
/// <c>"percentOfClass": "7.48"</c>. Across 422 rows measured 2026-08-28, every non-null value parsed as a
/// number: no <c>"N/A"</c>, no thousands separators. They are read with
/// <see cref="TolerantDecimalJsonConverter"/>, which binds null rather than throwing on anything it cannot
/// parse.</para></summary>
public sealed record BeneficialOwnership
{
    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded — the company whose stock the stake is in,
    /// not the reporting person's.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the disclosure was filed. Bare ISO on this path.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date EDGAR accepted it. <b>A date, not a timestamp, on this path</b> — no time component
    /// arrives, and it was equal to <see cref="FilingDate"/> on every row measured.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The security's CUSIP. Spelled <c>cusip</c> here, not <c>securityCusip</c> as on
    /// <see cref="InstitutionalHolding.SecurityCusip"/> — the attribute is load-bearing.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The filer — an institution, unnormalised, and the same institution appears under several
    /// spellings across years (<c>"The Vanguard Group"</c>, <c>"Vanguard Group - 23-1945930"</c>). Do not key
    /// on it.</summary>
    [JsonPropertyName("nameOfReportingPerson")] public string? NameOfReportingPerson { get; init; }

    /// <summary>Where the reporting person is organised — <c>"PENNSYLVANIA"</c>, and <c>"Pennsylvania"</c> on
    /// an older row. Case is not normalised.</summary>
    [JsonPropertyName("citizenshipOrPlaceOfOrganization")]
    public string? CitizenshipOrPlaceOfOrganization { get; init; }

    /// <summary>Shares the filer votes alone. <b>Arrives as a JSON string</b>; see the record's
    /// documentation.</summary>
    [JsonPropertyName("soleVotingPower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SoleVotingPower { get; init; }

    /// <summary>Shares the filer votes jointly. <b>Null on 1 of the 99 rows captured for AAPL</b> — the one
    /// place in this record where a quoted numeric is absent rather than <c>"0"</c>, which is why the
    /// converter's null handling is tested rather than assumed.</summary>
    [JsonPropertyName("sharedVotingPower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SharedVotingPower { get; init; }

    /// <summary>Shares the filer can dispose of alone.</summary>
    [JsonPropertyName("soleDispositivePower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SoleDispositivePower { get; init; }

    /// <summary>Shares the filer can dispose of jointly.</summary>
    [JsonPropertyName("sharedDispositivePower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SharedDispositivePower { get; init; }

    /// <summary>Total shares beneficially owned. <b>Not necessarily the sum of the four powers above</b> — the
    /// captured 2015 row reports 332,239,563 against a sole-dispositive 322,573,028 and a shared-dispositive
    /// 9,666,535, which do sum, while the two 2026 rows report a total beside four zeroes. Nothing is derived
    /// here; all five are reported as sent.</summary>
    [JsonPropertyName("amountBeneficiallyOwned")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? AmountBeneficiallyOwned { get; init; }

    /// <summary>The stake as a percentage of the class — <c>7.48</c>. <c>"0"</c> occurs on rows where the filer
    /// reported the amount without the percentage.</summary>
    [JsonPropertyName("percentOfClass")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? PercentOfClass { get; init; }

    /// <summary>The SEC's reporting-person code — <c>"IA"</c> (investment adviser), <c>"IN"</c> (individual),
    /// <c>"EP"</c> (employee benefit plan). <b>Can carry more than one, comma-joined</b> — <c>"EP, IN"</c> on
    /// the captured 2015 row. Left as the string FMP sent rather than split, because the join is FMP's and
    /// splitting it would be a second unmeasured transform.</summary>
    [JsonPropertyName("typeOfReportingPerson")] public string? TypeOfReportingPerson { get; init; }

    /// <summary>The filing on EDGAR.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
```

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<BeneficialOwnership>))]
```

- [ ] **Step 6: Add the method**

Append to `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, inside the class:

```csharp
    /// <summary>SC 13D/G disclosures for one issuer — who has crossed 5% of a class, and with what voting and
    /// dispositive power — <c>stable/acquisition-of-beneficial-ownership</c>.
    ///
    /// <para><b>FMP documents this under Insider Trades. It is here because it is not an insider
    /// transaction</b> — the reporting person is an institution and the subject is a stake. See
    /// <see cref="BeneficialOwnership"/>.</para>
    ///
    /// <para><b><paramref name="limit"/> and no <c>page</c>, and both halves are measured.</b> The endpoint
    /// honours <c>limit</c>; it ignores <c>page</c> — <c>page=0</c> and <c>page=1</c> returned byte-identical
    /// bodies on 2026-08-28. Honouring one does not predict honouring the other, so each was measured
    /// separately and only the one that works is offered.</para>
    ///
    /// <para>Historical as well as current: the captured AAPL response spans 2015 to 2026 in 99 rows.</para></summary>
    /// <param name="symbol">The issuer's ticker, as FMP spells it.</param>
    /// <param name="limit">Rows to return, 1 to <see cref="MaxOwnershipPageSize"/>. <b>The upper bound is
    /// derived from this path's siblings rather than measured on it</b> — see
    /// <see cref="MaxOwnershipPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The disclosures, newest first. Never <see langword="null"/>; empty for an unknown symbol, not
    /// an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxOwnershipPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BeneficialOwnership>> GetBeneficialOwnershipAsync(
        string symbol, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxOwnershipPageSize);

        return transport.GetListAsync(
            new FmpRequest("stable/acquisition-of-beneficial-ownership")
                .With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListBeneficialOwnership, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~BeneficialOwnershipTests`
Expected: PASS.

- [ ] **Step 8: Mutation-check**

1. Remove `[JsonConverter(typeof(TolerantDecimalJsonConverter))]` from `PercentOfClass` →
   `A_quoted_number_binds_as_a_decimal` still passes, because `FmpJsonContext` sets
   `NumberHandling = AllowReadingFromString`. But `An_unparseable_quoted_number_costs_one_field_not_the_row`
   fails with a `System.Text.Json` exception on `"N/A"`. **That is the whole reason the converter is on these
   six properties** rather than left to the context option — record it. Restore.
2. Add `.With("page", 0)` to `GetBeneficialOwnershipAsync` →
   `The_beneficial_ownership_call_sends_a_limit_and_no_page` fails on `Assert.DoesNotContain`. Restore.
3. Rename `[JsonPropertyName("cusip")]` to `("securityCusip")` →
   `A_captured_disclosure_binds_all_fifteen_of_its_fields` fails on `Binding.Unbound` naming `Cusip`. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/BeneficialOwnership.cs \
        src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/BeneficialOwnershipTests.cs \
        tests/FmpDotNet.Tests/Fixtures/acquisition-of-beneficial-ownership.AAPL.json
git commit -m "feat: add SC 13D/G disclosures, on the institutional facade rather than the insider one

FMP files acquisition-of-beneficial-ownership under Insider Trades. An SC 13D/G
is an institutional stake disclosure — the reporting person is an entity, the
fields are voting and dispositive power — so it lands next to the 13F paths.

Six of its fifteen fields arrive as JSON strings and use the shipped
TolerantDecimalJsonConverter, which binds null on an unparseable value rather
than throwing away the response. It honours limit and ignores page, measured
separately, so the method offers one and not the other."
```

### Task 8: `InsiderTrade`, the two paths that share it, and the `fmp.InsiderTrades` facade

**Files:**
- Create: `src/FmpDotNet/Models/InsiderTrade.cs`
- Create: `src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/insider-trading-latest.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/insider-trading-search.AAPL.json`
- Create: `tests/FmpDotNet.Tests/InsiderTradesTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`

**Interfaces:**
- Consumes: `FmpTransport.GetListAsync`, `NullableLocalDateJsonConverter`.
- Produces: `public sealed record InsiderTrade` (16 fields);
  `public sealed class InsiderTradesEndpoints(FmpTransport transport)` with
  `public const int MaxInsiderTradePageSize = 1000`,
  `Task<IReadOnlyList<InsiderTrade>> GetLatestAsync(int page = 0, int limit = 100, CancellationToken ct = default)` and
  `Task<IReadOnlyList<InsiderTrade>> SearchAsync(string? symbol = null, string? reportingCik = null, string? companyCik = null, string? transactionType = null, int page = 0, int limit = 100, CancellationToken ct = default)`;
  `FmpClient.InsiderTrades`. Task 9 adds three more methods to this class.

**One record for two paths, verified rather than assumed.** `insider-trading/latest` and
`insider-trading/search` return the same sixteen keys **in the same order**. `latest` stays mapped to its own
path rather than being expressed as `SearchAsync()` with no arguments: they are two documented paths, and the
coverage table counts paths.

**The fractional fields live here.** Measured 2026-08-28 over 1,000 rows of `insider-trading/latest`:
`securitiesOwned` was fractional on 59 (5.9%) and `securitiesTransacted` on 40 (4.0%). IBM's Arvind Krishna row
carries `28447.467` and `8375.5601`. These are the values that make `long?` throw on a real response, so the
fixture carries that row deliberately.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/insider-trading-latest.head.json` — three rows of
`stable/insider-trading/latest?page=0&limit=100`, captured 2026-08-28, from a 100-row response. **The three
rows are chosen, not consecutive**: row 1 is the capture's row 61, the fractional one; row 2 is its row 18,
carrying a blank `transactionType` and a blank `acquisitionOrDisposition`; row 3 is its row 79, carrying a null
`directOrIndirect` and a blank `securityName`. Blank counts across the 100-row capture were: `transactionType`
8, `acquisitionOrDisposition` 8, `securityName` 3, and `directOrIndirect` was null (not blank) on 3:

```json
[
  {
    "symbol": "IBM",
    "filingDate": "2026-08-28",
    "transactionDate": "2026-08-27",
    "reportingCik": "0001629898",
    "companyCik": "0000051143",
    "transactionType": "I-Discretionary",
    "securitiesOwned": 28447.467,
    "reportingName": "KRISHNA ARVIND",
    "typeOfOwner": "director, officer: Chairman, President & CEO",
    "acquisitionOrDisposition": "A",
    "directOrIndirect": "D",
    "formType": "4",
    "securitiesTransacted": 8375.5601,
    "price": 0,
    "securityName": "Phantom Stock",
    "url": "https://www.sec.gov/Archives/edgar/data/51143/000162989826000005/0001629898-26-000005-index.htm"
  },
  {
    "symbol": "POLA",
    "filingDate": "2026-08-28",
    "transactionDate": "2026-06-30",
    "reportingCik": "0002030245",
    "companyCik": "0001622345",
    "transactionType": "",
    "securitiesOwned": 0,
    "reportingName": "Shalom Menachem",
    "typeOfOwner": "director",
    "acquisitionOrDisposition": "",
    "directOrIndirect": "I",
    "formType": "3",
    "securitiesTransacted": 763889,
    "price": 0,
    "securityName": "Convertible Note",
    "url": "https://www.sec.gov/Archives/edgar/data/1622345/000149315226040605/0001493152-26-040605-index.htm"
  },
  {
    "symbol": "TREX",
    "filingDate": "2026-08-28",
    "transactionDate": "2026-08-24",
    "reportingCik": "0002152353",
    "companyCik": "0001069878",
    "transactionType": "",
    "securitiesOwned": 0,
    "reportingName": "Taylor Brian J.",
    "typeOfOwner": "officer",
    "acquisitionOrDisposition": "",
    "directOrIndirect": null,
    "formType": "3",
    "securitiesTransacted": 0,
    "price": 0,
    "securityName": "",
    "url": "https://www.sec.gov/Archives/edgar/data/1069878/000106987826000077/0001069878-26-000077-index.htm"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/insider-trading-search.AAPL.json` — the complete response to
`stable/insider-trading/search?symbol=AAPL&reportingCik=1780525&companyCik=320193&transactionType=S-Sale&page=0&limit=5`,
captured 2026-08-28. Three rows, and all four discriminators are satisfied by every one — which is the
assertion Step 2 makes about how they combine:

```json
[
  {
    "symbol": "AAPL",
    "filingDate": "2026-08-27",
    "transactionDate": "2026-08-25",
    "reportingCik": "0001780525",
    "companyCik": "0000320193",
    "transactionType": "S-Sale",
    "securitiesOwned": 37229,
    "reportingName": "Newstead Jennifer",
    "typeOfOwner": "officer: SVP, GC and Secretary",
    "acquisitionOrDisposition": "D",
    "directOrIndirect": "D",
    "formType": "4",
    "securitiesTransacted": 1439,
    "price": 310.95,
    "securityName": "Common Stock",
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000114036126034741/0001140361-26-034741-index.htm"
  },
  {
    "symbol": "AAPL",
    "filingDate": "2026-08-20",
    "transactionDate": "2026-08-18",
    "reportingCik": "0001780525",
    "companyCik": "0000320193",
    "transactionType": "S-Sale",
    "securitiesOwned": 38668,
    "reportingName": "Newstead Jennifer",
    "typeOfOwner": "officer: SVP, GC and Secretary",
    "acquisitionOrDisposition": "D",
    "directOrIndirect": "D",
    "formType": "4",
    "securitiesTransacted": 1439,
    "price": 307.49,
    "securityName": "Common Stock",
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000114036126033928/0001140361-26-033928-index.htm"
  },
  {
    "symbol": "AAPL",
    "filingDate": "2026-08-13",
    "transactionDate": "2026-08-11",
    "reportingCik": "0001780525",
    "companyCik": "0000320193",
    "transactionType": "S-Sale",
    "securitiesOwned": 40107,
    "reportingName": "Newstead Jennifer",
    "typeOfOwner": "officer: SVP, GC and Secretary",
    "acquisitionOrDisposition": "D",
    "directOrIndirect": "D",
    "formType": "4",
    "securitiesTransacted": 1439,
    "price": 307.75,
    "securityName": "Common Stock",
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000114036126032884/0001140361-26-032884-index.htm"
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/InsiderTradesTests.cs`. This file grows over Tasks 8 and 9; this step writes the header,
the record section and the two path sections.

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>Form 3, 4 and 5 insider transactions, checked against captures taken live 2026-08-28.
///
/// <para><b>Share counts here are fractional, and that is what forced <c>decimal?</c> across the whole
/// slice.</b> Measured over 1,000 rows of <c>insider-trading/latest</c>: <c>securitiesOwned</c> was fractional
/// on 59 (5.9%) and <c>securitiesTransacted</c> on 40 (4.0%). Phantom stock, deferred units and dividend
/// reinvestment all produce fractions. Typing either as <c>long?</c> makes <c>System.Text.Json</c> throw, and
/// <c>FmpTransport</c> does not wrap the deserialiser — so one such row costs the caller all 1,000.</para>
///
/// <para><b>Blank and null are both wire values here and mean different things.</b> <c>transactionType</c> is
/// <c>""</c> on 8 rows of 100 — Form 3 initial statements have no transaction — while
/// <c>directOrIndirect</c> is explicitly <c>null</c> on 3. Neither is normalised to the other.</para></summary>
public class InsiderTradesTests
{
    private static (InsiderTradesEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InsiderTradesEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- the record --------------------------------------------------------------------------------------------

    [Fact]
    public void A_fractional_share_count_binds_rather_than_throwing()
    {
        // THE test for the insider half of the decimal? ruling, and the one whose values are real rather than
        // constructed: IBM's Arvind Krishna holds 28,447.467 phantom shares and transacted 8,375.5601 of them.
        // Retype either property as long? or int? and System.Text.Json throws, costing the caller every row in
        // the response rather than the one field.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(28447.467m, rows[0].SecuritiesOwned);
        Assert.Equal(8375.5601m, rows[0].SecuritiesTransacted);
        Assert.Equal("Phantom Stock", rows[0].SecurityName);
    }

    [Fact]
    public void A_blank_transaction_type_stays_blank_and_a_null_direct_flag_stays_null()
    {
        // Two different absences on two different fields, both measured, neither normalised. transactionType
        // was "" on 8 of 100 rows — a Form 3 initial statement reports a holding, not a transaction — while
        // directOrIndirect was explicitly null on 3. Mapping "" to null would erase the distinction between
        // "FMP sent an empty value" and "FMP sent nothing", and an enum over transactionType would have no
        // member for the blank at all.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal("", rows[1].TransactionType);
        Assert.Equal("", rows[1].AcquisitionOrDisposition);
        Assert.Equal("I", rows[1].DirectOrIndirect);

        Assert.Equal("", rows[2].TransactionType);
        Assert.Null(rows[2].DirectOrIndirect);
        Assert.Equal("", rows[2].SecurityName);
        // And the rest of the row still arrives.
        Assert.Equal("TREX", rows[2].Symbol);
        Assert.Equal("Taylor Brian J.", rows[2].ReportingName);
        Assert.Equal("3", rows[2].FormType);
    }

    [Fact]
    public void A_captured_trade_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("IBM", rows[0].Symbol);
        Assert.Equal("0001629898", rows[0].ReportingCik);
        Assert.Equal("0000051143", rows[0].CompanyCik);
        Assert.Equal("I-Discretionary", rows[0].TransactionType);
        Assert.Equal("KRISHNA ARVIND", rows[0].ReportingName);
        Assert.Equal("director, officer: Chairman, President & CEO", rows[0].TypeOfOwner);
        Assert.Equal("A", rows[0].AcquisitionOrDisposition);
        Assert.Equal("D", rows[0].DirectOrIndirect);
        Assert.Equal("4", rows[0].FormType);
        Assert.Equal(0m, rows[0].Price);
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].TransactionDate);
    }

    [Fact]
    public void The_transaction_date_is_not_the_filing_date()
    {
        // Two distinct dates and the gap is real: two days on the IBM row and 59 on the POLA row. A consumer
        // that reads either as the other misdates the transaction, and neither is derivable from the other.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.All(rows, r => Assert.Equal(new LocalDate(2026, 8, 28), r.FilingDate));
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].TransactionDate);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[1].TransactionDate);
        Assert.Equal(new LocalDate(2026, 8, 24), rows[2].TransactionDate);
    }

    // ---- insider-trading/latest --------------------------------------------------------------------------------

    [Fact]
    public async Task The_latest_call_sends_page_and_limit_and_no_filters()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestAsync(page: 2, limit: 50);

        Assert.Equal("/stable/insider-trading/latest", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("symbol=", handler.Requests[0].Query);
        Assert.DoesNotContain("transactionType=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    [InlineData(5000)]
    public async Task An_insider_limit_above_the_measured_cap_is_refused_on_both_paths(int limit)
    {
        // Measured 2026-08-28: insider-trading/latest at limit=2000 and limit=5000 each answered exactly 1,000
        // rows with HTTP 200 and byte-identical bodies; insider-trading/search at limit=2000 answered 1,000 as
        // well. Both feeds paginate, so a caller stepping `page` by 5,000 reads a fifth of the archive and is
        // never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        var first = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(limit: limit));
        var second = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchAsync(symbol: "AAPL", limit: limit));

        Assert.Equal("limit", first.ParamName);
        Assert.Equal("limit", second.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_insider_limit_exactly_at_the_cap_is_accepted_on_both_paths()
    {
        // The off-by-one boundary, on the shared guard — so one swapped comparison would break both feeds and
        // this is the only test that would say so.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetLatestAsync(limit: InsiderTradesEndpoints.MaxInsiderTradePageSize);
        await endpoints.SearchAsync(symbol: "AAPL", limit: InsiderTradesEndpoints.MaxInsiderTradePageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
        Assert.Contains("limit=1000", handler.Requests[1].Query);
    }

    [Fact]
    public void The_insider_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, InsiderTradesEndpoints.MaxInsiderTradePageSize);
    }

    // ---- insider-trading/search --------------------------------------------------------------------------------

    [Fact]
    public async Task Every_search_discriminator_that_is_supplied_reaches_the_query()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync(
            symbol: "AAPL", reportingCik: "1780525", companyCik: "320193", transactionType: "S-Sale",
            page: 0, limit: 5);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/insider-trading/search", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", query);
        Assert.Contains("reportingCik=1780525", query);
        Assert.Contains("companyCik=320193", query);
        Assert.Contains("transactionType=S-Sale", query);
    }

    [Fact]
    public async Task A_search_with_no_criteria_is_a_valid_call()
    {
        // Deliberate. With nothing supplied the endpoint degenerates to the same feed GetLatestAsync answers,
        // which is a legitimate thing to ask for and not a caller error. FmpRequest drops null and blank
        // values, so nothing reaches FMP but page and limit.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync();

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/insider-trading/search", handler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("symbol=", query);
        Assert.DoesNotContain("reportingCik=", query);
        Assert.DoesNotContain("companyCik=", query);
        Assert.DoesNotContain("transactionType=", query);
        Assert.Contains("page=0", query);
    }

    [Fact]
    public async Task A_blank_discriminator_is_treated_as_absent_rather_than_refused()
    {
        // The four are optional, so blank means "not filtering on this" rather than "the caller made a
        // mistake". FmpRequest drops it either way; this pins that the method does not throw on it, which
        // would make `SearchAsync(symbol: userInput)` unusable against an empty form field.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync(symbol: "AAPL", transactionType: "   ");

        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.DoesNotContain("transactionType=", handler.Requests[0].Query);
    }

    [Fact]
    public void The_four_search_discriminators_narrow_together_rather_than_widen()
    {
        // Measured 2026-08-28, and worth recording because a first reading of the row counts suggests
        // otherwise. `reportingCik=1780525` alone answers a default page of 100 rows whose head is all AAPL —
        // which looks as though adding `symbol=AAPL` should change nothing, yet it drops the count to 10.
        //
        // Asking for the whole set explains it: `reportingCik=1780525&limit=1000` answers 553 rows across five
        // symbols (META 518, FB 20, AAPL 10, RJET 3, EMR 2) — the reporting person moved employers. Exactly 10
        // are AAPL, and `symbol=AAPL&reportingCik=1780525` answers exactly those 10. The filters intersect
        // correctly; the 100-row default page was the misleading part.
        //
        // This fixture is the four-way intersection: every row satisfies all four discriminators.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-search.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("AAPL", r.Symbol);
            Assert.Equal("0001780525", r.ReportingCik);
            Assert.Equal("0000320193", r.CompanyCik);
            Assert.Equal("S-Sale", r.TransactionType);
        });
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
    }

    [Fact]
    public void The_search_and_latest_paths_return_the_same_sixteen_fields()
    {
        // Verified rather than assumed: the two paths send the same keys in the same order, which is why one
        // record serves both. If they diverge, this fails on the record that stopped binding.
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;
        var search = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-search.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Empty(Binding.Unbound(latest[0]));
        Assert.Empty(Binding.Unbound(search[0]));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InsiderTradesTests`
Expected: FAIL to compile — `InsiderTrade` and `InsiderTradesEndpoints` do not exist.

- [ ] **Step 4: Write `InsiderTrade`**

`src/FmpDotNet/Models/InsiderTrade.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One insider transaction from a Form 3, 4 or 5 — served by both
/// <c>stable/insider-trading/latest</c> and <c>stable/insider-trading/search</c>.
///
/// <para><b>One record for two paths, verified rather than assumed.</b> Measured 2026-08-28, the two paths
/// return the same sixteen keys in the same order. They differ in what they select, not in what they
/// send.</para>
///
/// <para><b>Share counts are fractional.</b> Over 1,000 rows of the <c>latest</c> feed,
/// <see cref="SecuritiesOwned"/> was fractional on 59 (5.9%) and <see cref="SecuritiesTransacted"/> on 40
/// (4.0%) — phantom stock, deferred units and dividend reinvestment all produce them. Both are
/// <see cref="decimal"/>; an integer type would make <c>System.Text.Json</c> throw on those rows and cost the
/// caller the whole response.</para>
///
/// <para><b>Blank and null are both wire values and mean different things.</b> See
/// <see cref="TransactionType"/> and <see cref="DirectOrIndirect"/>.</para></summary>
public sealed record InsiderTrade
{
    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the form was filed. <b>Not the transaction date</b> — see
    /// <see cref="TransactionDate"/>, which was 59 days earlier on one of the three captured rows.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date the transaction took place. Neither date is derivable from the other; a Form 4 is due
    /// within two business days but a Form 3 can report a holding from months earlier.</summary>
    [JsonPropertyName("transactionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? TransactionDate { get; init; }

    /// <summary>The <b>insider's</b> Central Index Key, zero-padded — a person or an entity that files about
    /// the issuer. Distinct from <see cref="CompanyCik"/>, and the two are not interchangeable in
    /// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/>.</summary>
    [JsonPropertyName("reportingCik")] public string? ReportingCik { get; init; }

    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded.</summary>
    [JsonPropertyName("companyCik")] public string? CompanyCik { get; init; }

    /// <summary>The SEC transaction code — <c>"S-Sale"</c>, <c>"P-Purchase"</c>, <c>"A-Award"</c>. Eighteen
    /// exist and <c>Endpoints.InsiderTradesEndpoints.GetTransactionTypesAsync</c> serves the list.
    ///
    /// <para><b><c>""</c> on 40 of 1,000 rows measured 2026-08-28</b>, which is FMP's value and not an absence:
    /// a Form 3 initial statement reports a holding rather than a transaction, so there is no code to send.
    /// That blank is also why this is a <see cref="string"/> rather than an enum — a closed C# enum over a
    /// server-served list would have no member for it, and no member for a code FMP adds next
    /// Tuesday.</para></summary>
    [JsonPropertyName("transactionType")] public string? TransactionType { get; init; }

    /// <summary>Shares the insider holds after the transaction. <b>Fractional on 5.9% of rows measured</b>,
    /// with a maximum of 61,721,535 — see the record's documentation.</summary>
    [JsonPropertyName("securitiesOwned")] public decimal? SecuritiesOwned { get; init; }

    /// <summary>The insider's name as EDGAR spells it — <c>"KRISHNA ARVIND"</c>, <c>"Newstead Jennifer"</c>.
    /// Surname first, case unnormalised.</summary>
    [JsonPropertyName("reportingName")] public string? ReportingName { get; init; }

    /// <summary>The insider's relationship to the issuer — <c>"director"</c>,
    /// <c>"officer: SVP, GC and Secretary"</c>, <c>"director, officer: Chairman, President &amp; CEO"</c>.
    /// Free text carrying several roles comma-joined; not a code, and not parsed here.</summary>
    [JsonPropertyName("typeOfOwner")] public string? TypeOfOwner { get; init; }

    /// <summary><c>"A"</c> for an acquisition, <c>"D"</c> for a disposition. <b><c>""</c> on the same rows
    /// where <see cref="TransactionType"/> is blank</b> — 8 of the 100-row capture.</summary>
    [JsonPropertyName("acquisitionOrDisposition")] public string? AcquisitionOrDisposition { get; init; }

    /// <summary><c>"D"</c> for directly held, <c>"I"</c> for indirectly.
    ///
    /// <para><b>Explicitly <see langword="null"/> on 3 of the 100-row capture</b>, where
    /// <see cref="TransactionType"/> is blank rather than null on its own 8. Two different absences on one
    /// record, neither normalised into the other: <c>""</c> is a value FMP sent, <c>null</c> is a value it did
    /// not.</para></summary>
    [JsonPropertyName("directOrIndirect")] public string? DirectOrIndirect { get; init; }

    /// <summary>The SEC form — <c>"3"</c>, <c>"4"</c>, <c>"4/A"</c>, <c>"5"</c>.
    ///
    /// <para><b>Not the same vocabulary as <see cref="InstitutionalFiling.FormType"/></b>, which carries
    /// <c>"13F-HR"</c> and its variants. One field name, two disjoint value sets — which is why the two records
    /// are separate.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>Shares moved by the transaction. <b>Fractional on 4.0% of rows measured</b>, maximum
    /// 33,586,045.</summary>
    [JsonPropertyName("securitiesTransacted")] public decimal? SecuritiesTransacted { get; init; }

    /// <summary>The price per share.
    ///
    /// <para><b><c>0</c> on 41.4% of rows measured</b> — 414 of 1,000 — and that is a real value rather than a
    /// missing one: an award, a gift and a phantom-stock accrual all move shares at no price. Do not read a
    /// zero here as "unknown".</para></summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>What was transacted — <c>"Common Stock"</c>, <c>"Phantom Stock"</c>,
    /// <c>"Convertible Note"</c>, <c>"Restricted Stock Unit"</c>. Blank on 3 of the 100-row capture.</summary>
    [JsonPropertyName("securityName")] public string? SecurityName { get; init; }

    /// <summary>The filing on EDGAR.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
```

- [ ] **Step 5: Register it**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<InsiderTrade>))]
```

- [ ] **Step 6: Write the facade**

`src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Insider Trades</c> group — what officers, directors and 10% owners file on Forms 3, 4
/// and 5.
///
/// <para><b>Five of the six paths FMP files under this heading.</b> The sixth,
/// <c>acquisition-of-beneficial-ownership</c>, is an SC 13D/G stake disclosure rather than an insider
/// transaction and lives on <see cref="InstitutionalOwnershipEndpoints"/>; see that class for why. This SDK
/// files a path by what it returns.</para>
///
/// <para><b>Two of the five answer the same row shape.</b>
/// <see cref="GetLatestAsync"/> and <see cref="SearchAsync"/> both return
/// <see cref="InsiderTrade"/> — the same sixteen keys in the same order, verified 2026-08-28 — and differ only
/// in what they select. The other three answer shapes of their own.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class InsiderTradesEndpoints(FmpTransport transport)
{
    /// <summary>The largest page either insider feed will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-28, <c>insider-trading/latest?limit=2000</c> and
    /// <c>?limit=5000</c> each answered exactly 1,000 rows with HTTP 200 and byte-identical bodies, and
    /// <c>insider-trading/search?limit=2000</c> answered 1,000 as well — nothing in the response says the
    /// request was trimmed. Both feeds paginate, so a caller who asks for 5,000 and advances <c>page</c> by
    /// 5,000 reads a fifth of the archive and is never told.</para></summary>
    public const int MaxInsiderTradePageSize = 1000;

    /// <summary>The whole-market feed of insider filings as they arrive, newest first —
    /// <c>stable/insider-trading/latest</c>.
    ///
    /// <para>The 100 rows a bare call returns is a default rather than a cap: measured 2026-08-28,
    /// <c>limit=200</c> answered 200 and <c>limit=1000</c> answered 1,000. See
    /// <see cref="MaxInsiderTradePageSize"/> for where that stops.</para>
    ///
    /// <para><b>A distinct path from <see cref="SearchAsync"/>, not a special case of it.</b> An unfiltered
    /// search answers the same rows, but the two are separate endpoints and each is modelled as
    /// itself.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxInsiderTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxInsiderTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit.</exception>
    public Task<IReadOnlyList<InsiderTrade>> GetLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);

        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInsiderTrade, ct);
    }

    /// <summary>Insider filings narrowed by any combination of four criteria —
    /// <c>stable/insider-trading/search</c>.
    ///
    /// <para><b>All four discriminators are optional and they intersect.</b> Measured 2026-08-28:
    /// <c>reportingCik=1780525</c> alone answers 553 rows across five symbols — the reporting person changed
    /// employers — of which exactly 10 are AAPL, and <c>symbol=AAPL&amp;reportingCik=1780525</c> answers
    /// exactly those 10. Adding a criterion narrows; it never widens.</para>
    ///
    /// <para><b>A row count that drops sharply when you add a criterion is usually the default page, not the
    /// filter.</b> A bare call returns 100 rows, so <c>reportingCik</c> alone looked like "100 rows, all AAPL"
    /// until the whole 553-row set was asked for. Raise <paramref name="limit"/> before concluding a filter has
    /// lost rows.</para>
    ///
    /// <para><b>With nothing supplied this answers the same feed as <see cref="GetLatestAsync"/>.</b> That is a
    /// valid call rather than a caller error: <c>FmpRequest</c> drops null and blank values, so an unset
    /// criterion simply does not reach FMP.</para></summary>
    /// <param name="symbol">The issuer's ticker. Optional.</param>
    /// <param name="reportingCik">The <b>insider's</b> Central Index Key, padded or unpadded — both work.
    /// Optional.</param>
    /// <param name="companyCik">The <b>issuer's</b> Central Index Key, padded or unpadded. Optional, and not
    /// interchangeable with <paramref name="reportingCik"/>.</param>
    /// <param name="transactionType">An SEC transaction code — <c>"S-Sale"</c>, <c>"P-Purchase"</c>. The
    /// eighteen valid values come from <c>GetTransactionTypesAsync</c>. Optional, and not validated
    /// here: an unrecognised code answers an empty list rather than an error, and a code FMP adds must not cost
    /// the caller the call.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxInsiderTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching filings, newest first. Never <see langword="null"/>; empty when nothing matches,
    /// not an error.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxInsiderTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTrade>> SearchAsync(
        string? symbol = null, string? reportingCik = null, string? companyCik = null,
        string? transactionType = null, int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);

        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/search")
                .With("symbol", symbol).With("reportingCik", reportingCik)
                .With("companyCik", companyCik).With("transactionType", transactionType)
                .With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListInsiderTrade, ct);
    }

    /// <summary>The paging guard the two feeds share. Extracted at two call sites for the reason
    /// <see cref="SecFilingsEndpoints"/> records: the three-line body is the thing that must not drift between
    /// them.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxInsiderTradePageSize);
    }
}
```

**`ThrowIfPagingOutOfRange`'s `ParamName` is correct because its parameters are named `page` and `limit`** —
`[CallerArgumentExpression]` reads the argument expression at the call site inside the helper, which is the
helper's own parameter name. The tests assert `ParamName == "limit"`, which is what pins this.

- [ ] **Step 7: Wire the facade into the client and DI**

`src/FmpDotNet/FmpClient.cs` — add `InsiderTradesEndpoints insiderTrades` to the primary constructor after
`institutionalOwnership`, and the property after `InstitutionalOwnership`:

```csharp
    /// <summary>What company insiders file on Forms 3, 4 and 5 — the whole-market feed, a four-way search,
    /// per-symbol statistics, and the two reference lists behind them.
    ///
    /// <para>SC 13D/G beneficial-ownership disclosures are <b>not</b> here: FMP documents them under this
    /// heading, but they are institutional stake filings rather than insider transactions and live on
    /// <see cref="InstitutionalOwnership"/>.</para></summary>
    public InsiderTradesEndpoints InsiderTrades { get; } = insiderTrades;
```

`src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`:

```csharp
        services.TryAddTransient<InsiderTradesEndpoints>();
```

- [ ] **Step 8: Update `AddFmpTests`**

```csharp
        Assert.NotNull(client.InsiderTrades);
```

and change `Assert.Equal(12, typeof(FmpClient)` to `Assert.Equal(13, typeof(FmpClient)`.

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~InsiderTradesTests|FullyQualifiedName~AddFmpTests"`
Expected: PASS.

- [ ] **Step 10: Mutation-check**

1. Retype `SecuritiesOwned` as `long?` → `A_fractional_share_count_binds_rather_than_throwing` fails with a
   `System.Text.Json` exception. Restore.
2. Add `?? ""` normalisation on `DirectOrIndirect` (or map `""` to null on `TransactionType`) →
   `A_blank_transaction_type_stays_blank_and_a_null_direct_flag_stays_null` fails. Restore.
3. Change `SearchAsync` to call `ArgumentException.ThrowIfNullOrWhiteSpace(symbol)` →
   `A_search_with_no_criteria_is_a_valid_call` and `A_blank_discriminator_is_treated_as_absent_rather_than_refused`
   both fail. Restore. This is the mutation somebody makes by symmetry with the rest of the SDK, where required
   strings are guarded.
4. Change `MaxInsiderTradePageSize` to `5000` → `The_insider_page_cap_is_the_measured_one` and all three
   `An_insider_limit_above_the_measured_cap_is_refused_on_both_paths` cases fail. Restore.
5. Change `ThrowIfGreaterThan` to `ThrowIfGreaterThanOrEqual` in `ThrowIfPagingOutOfRange` →
   `An_insider_limit_exactly_at_the_cap_is_accepted_on_both_paths` fails and nothing else does. Restore. The
   guard is shared, so one swapped comparison breaks both feeds and this is the only test that says so.

- [ ] **Step 11: Commit**

```bash
git add src/FmpDotNet/Models/InsiderTrade.cs \
        src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs \
        tests/FmpDotNet.Tests/InsiderTradesTests.cs \
        tests/FmpDotNet.Tests/Fixtures/insider-trading-latest.head.json \
        tests/FmpDotNet.Tests/Fixtures/insider-trading-search.AAPL.json
git commit -m "feat: add fmp.InsiderTrades with the feed and the four-way search

The thirteenth facade. One record serves both paths — verified, not assumed:
insider-trading/latest and insider-trading/search send the same sixteen keys in
the same order.

Share counts are fractional on 4-6% of rows (IBM's phantom stock: 28447.467
owned, 8375.5601 transacted), which is what forced decimal? across the slice.
Blank transactionType and null directOrIndirect are separate wire values and
neither is normalised into the other."
```

### Task 9: The three small insider paths — statistics, the name lookup, and the code list

**Files:**
- Create: `src/FmpDotNet/Models/InsiderTradeStatistics.cs` — `InsiderTradeStatistics` (13), `InsiderReportingName` (2), `InsiderTransactionType` (1)
- Create: `tests/FmpDotNet.Tests/Fixtures/insider-trading-statistics.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/insider-trading-reporting-name.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/insider-trading-transaction-type.json`
- Modify: `src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs` — `+GetStatisticsAsync`, `+SearchReportingNameAsync`, `+GetTransactionTypesAsync`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `tests/FmpDotNet.Tests/InsiderTradesTests.cs`
- Modify, in Step 7 only: `src/FmpDotNet/Models/{FilingQuarter,InstitutionalHolding,HolderAnalytics,HolderSummaries,InstitutionalFiling,InsiderTrade}.cs`, `src/FmpDotNet/Endpoints/InstitutionalOwnershipEndpoints.cs`, `src/FmpDotNet/FmpClient.cs` — the fourteen deferred cross-references become `<see cref>` now that every symbol exists

**Interfaces:**
- Consumes: `InsiderTradesEndpoints` (Task 8).
- Produces: three records and three methods —
  `Task<IReadOnlyList<InsiderTradeStatistics>> GetStatisticsAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<InsiderReportingName>> SearchReportingNameAsync(string name, CancellationToken ct = default)`,
  `Task<IReadOnlyList<InsiderTransactionType>> GetTransactionTypesAsync(CancellationToken ct = default)`.

Three paths in one task because each is one small record with one required parameter or none, and no two of
them share a decision a reviewer could weigh separately. Their measured content is not small, though:

- **`statistics` is the third place fractional values appear**, and unlike the others it is fractional
  *usually*: `acquiredDisposedRatio` on 87 of 94 rows, `averageDisposed` on 85, `averageAcquired` on 76.
- **`reporting-name` matches on a prefix**, measured: `name=Cook` answers 133 names all beginning "Cook";
  `name=Apple` answers 20 beginning "Apple", including `Applebach` and `Applebaum`.
- **`insider-trading-transaction-type` is the list `SearchAsync`'s `transactionType` draws from** — 18 codes,
  one field per row, and modelled as a record rather than projected to `IReadOnlyList<string>`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/insider-trading-statistics.AAPL.json` — the first three rows of
`stable/insider-trading/statistics?symbol=AAPL`, captured 2026-08-28, verbatim. The full response was 94 rows,
newest quarter first. **Row 1 is a quarter with no acquisitions at all — every acquired figure is `0` — and
rows 2 and 3 carry the fractional ratios and averages** that make this record's typing load-bearing:

```json
[
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "year": 2026,
    "quarter": 3,
    "acquiredTransactions": 0,
    "disposedTransactions": 3,
    "acquiredDisposedRatio": 0,
    "totalAcquired": 0,
    "totalDisposed": 4317,
    "averageAcquired": 0,
    "averageDisposed": 1439,
    "totalPurchases": 0,
    "totalSales": 3
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "year": 2026,
    "quarter": 2,
    "acquiredTransactions": 7,
    "disposedTransactions": 40,
    "acquiredDisposedRatio": 0.175,
    "totalAcquired": 303199,
    "totalDisposed": 927380,
    "averageAcquired": 43314.1429,
    "averageDisposed": 23184.5,
    "totalPurchases": 0,
    "totalSales": 14
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "year": 2026,
    "quarter": 1,
    "acquiredTransactions": 15,
    "disposedTransactions": 10,
    "acquiredDisposedRatio": 1.5,
    "totalAcquired": 76696,
    "totalDisposed": 102492,
    "averageAcquired": 5113.0667,
    "averageDisposed": 10249.2,
    "totalPurchases": 0,
    "totalSales": 0
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/insider-trading-reporting-name.head.json` — the first three rows of
`stable/insider-trading/reporting-name?name=Cook`, captured 2026-08-28. The full response was 133 rows:

```json
[
  {
    "reportingCik": "0001706288",
    "reportingName": "Cook Adam T"
  },
  {
    "reportingCik": "0001320559",
    "reportingName": "Cook Anne Marie"
  },
  {
    "reportingCik": "0001531469",
    "reportingName": "Cook Benton Lowell"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/insider-trading-transaction-type.json` — the **complete** response to
`stable/insider-trading-transaction-type`, captured 2026-08-28. All 18 rows, because the list is the answer and
a truncated copy of it would be a worse fixture than none:

```json
[
  {
    "transactionType": "A-Award"
  },
  {
    "transactionType": "C-Conversion"
  },
  {
    "transactionType": "D-Return"
  },
  {
    "transactionType": "E-ExpireShort"
  },
  {
    "transactionType": "F-InKind"
  },
  {
    "transactionType": "G-Gift"
  },
  {
    "transactionType": "H-ExpireLong"
  },
  {
    "transactionType": "I-Discretionary"
  },
  {
    "transactionType": "J-Other"
  },
  {
    "transactionType": "L-Small"
  },
  {
    "transactionType": "M-Exempt"
  },
  {
    "transactionType": "O-OutOfTheMoney"
  },
  {
    "transactionType": "P-Purchase"
  },
  {
    "transactionType": "S-Sale"
  },
  {
    "transactionType": "U-Tender"
  },
  {
    "transactionType": "W-Will"
  },
  {
    "transactionType": "X-InTheMoney"
  },
  {
    "transactionType": "Z-Trust"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/InsiderTradesTests.cs`:

```csharp
    // ---- insider-trading/statistics -----------------------------------------------------------------------------

    [Fact]
    public void The_statistics_ratios_and_averages_are_usually_fractional()
    {
        // The third place in this slice where a long? would throw, and the only one where fractional is the
        // normal case rather than the exception. Measured 2026-08-28 over AAPL's 94 quarters:
        // acquiredDisposedRatio fractional on 87, averageDisposed on 85, averageAcquired on 76. The totals and
        // the transaction counts were fractional on 0 — which is why those stay int?.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(0.175m, rows[1].AcquiredDisposedRatio);
        Assert.Equal(43314.1429m, rows[1].AverageAcquired);
        Assert.Equal(23184.5m, rows[1].AverageDisposed);
        Assert.Equal(1.5m, rows[2].AcquiredDisposedRatio);
        Assert.Equal(5113.0667m, rows[2].AverageAcquired);
    }

    [Fact]
    public void A_quarter_with_no_acquisitions_reports_zeroes_rather_than_nulls()
    {
        // Row 1 is 2026 Q3: no acquisitions, three disposals. Every acquired figure is 0 and the ratio is 0 —
        // all of them FMP's answers, not absences. Binding.Unbound counts zero as populated for this reason,
        // so the whole-record check still holds.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(0, rows[0].AcquiredTransactions);
        Assert.Equal(3, rows[0].DisposedTransactions);
        Assert.Equal(0m, rows[0].TotalAcquired);
        Assert.Equal(4317m, rows[0].TotalDisposed);
        Assert.Equal(0m, rows[0].AcquiredDisposedRatio);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(3, rows[0].Quarter);
    }

    [Fact]
    public void Total_sales_counts_filings_and_total_disposed_counts_shares()
    {
        // Two fields whose names read alike and whose units are not. On 2026 Q2: totalSales is 14 and
        // totalDisposed is 927,380. One counts transactions, the other counts shares — which is why the first
        // is int? and the second decimal?, and why the doc comments say which is which.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Equal(14, rows[1].TotalSales);
        Assert.Equal(927380m, rows[1].TotalDisposed);
        Assert.Equal(40, rows[1].DisposedTransactions);
    }

    [Fact]
    public async Task The_statistics_call_sends_only_the_symbol()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetStatisticsAsync("AAPL");

        Assert.Equal("/stable/insider-trading/statistics", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("year=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_blank_statistics_symbol_is_refused()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetStatisticsAsync(" "));

        Assert.Empty(handler.Requests);
    }

    // ---- insider-trading/reporting-name -------------------------------------------------------------------------

    [Fact]
    public void A_reporting_name_row_is_a_cik_and_a_name_and_nothing_else()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-reporting-name.head.json"),
            FmpJsonContext.Default.ListInsiderReportingName)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
        Assert.Equal("0001706288", rows[0].ReportingCik);
        Assert.Equal("Cook Adam T", rows[0].ReportingName);
    }

    [Fact]
    public void The_name_lookup_matches_a_prefix_of_a_surname_first_name()
    {
        // Measured 2026-08-28 on two queries: name=Cook answered 133 rows, every one beginning "Cook";
        // name=Apple answered 20, including "Applebach Richard Jr" and "Applebaum Michelle Galanter". So it is
        // a prefix match against a name EDGAR spells surname-first, not a substring match and not a match on a
        // company. Searching for a given name finds nothing.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-reporting-name.head.json"),
            FmpJsonContext.Default.ListInsiderReportingName)!;

        Assert.All(rows, r => Assert.StartsWith("Cook", r.ReportingName));
    }

    [Fact]
    public async Task The_reporting_name_call_sends_the_name_as_typed()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchReportingNameAsync("Cook");

        Assert.Equal("/stable/insider-trading/reporting-name", handler.Requests[0].AbsolutePath);
        Assert.Contains("name=Cook", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_null_name_is_refused_with_ArgumentNullException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.SearchReportingNameAsync(null!));

        Assert.Empty(handler.Requests);
    }

    // ---- insider-trading-transaction-type -----------------------------------------------------------------------

    [Fact]
    public void The_transaction_type_list_is_the_eighteen_codes_search_accepts()
    {
        // The whole response, not a head: the list IS the answer. These eighteen are what
        // SearchAsync(transactionType:) draws from, and they are served by an endpoint rather than fixed in the
        // SDK — which is exactly why InsiderTrade.TransactionType is a string and not an enum. FMP can add a
        // nineteenth without an SDK release, and a closed enum would also have no member for the blank that
        // appears on 40 of 1,000 rows.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-transaction-type.json"),
            FmpJsonContext.Default.ListInsiderTransactionType)!;

        Assert.Equal(18, rows.Count);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
        Assert.Equal("A-Award", rows[0].TransactionType);
        Assert.Equal("Z-Trust", rows[^1].TransactionType);
        Assert.Contains(rows, r => r.TransactionType == "S-Sale");
        Assert.Contains(rows, r => r.TransactionType == "P-Purchase");
    }

    [Fact]
    public void Every_measured_transaction_type_on_a_trade_row_is_in_the_list_or_blank()
    {
        // The two fixtures agree, which is the point of modelling the list at all. Measured over 1,000 rows of
        // insider-trading/latest, every transactionType was drawn from these eighteen or was the empty string.
        var codes = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-transaction-type.json"),
            FmpJsonContext.Default.ListInsiderTransactionType)!
            .Select(r => r.TransactionType ?? "").ToHashSet(StringComparer.Ordinal);
        var trades = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        // `?? ""` on both sides rather than `!`: TransactionType is string?, and ToHashSet/Contains over a
        // nullable element type would warn under TreatWarningsAsErrors. The blank is a legal value here anyway,
        // which is what the assertion allows for.
        Assert.All(trades, t => Assert.True(
            t.TransactionType == "" || codes.Contains(t.TransactionType ?? ""),
            $"'{t.TransactionType}' is not one of the eighteen codes and is not blank."));
    }

    [Fact]
    public async Task The_transaction_type_call_sends_no_parameters_at_all()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetTransactionTypesAsync();

        Assert.Equal("/stable/insider-trading-transaction-type", handler.Requests[0].AbsolutePath);
        // The path is NOT under insider-trading/ — it is a sibling. Getting that wrong answers 404, which
        // FmpTransport surfaces as an exception rather than an empty list, so it would be loud; the assertion
        // above is here so it is loud at build time instead.
        Assert.DoesNotContain("insider-trading/transaction-type", handler.Requests[0].AbsolutePath);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InsiderTradesTests`
Expected: FAIL to compile — none of the three records or methods exists.

- [ ] **Step 4: Write the three records**

`src/FmpDotNet/Models/InsiderTradeStatistics.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One quarter of aggregated insider activity for one issuer, from
/// <c>stable/insider-trading/statistics</c>.
///
/// <para>One row per quarter the issuer has any, newest first — 94 rows for AAPL, measured 2026-08-28, going
/// back to 2003.</para>
///
/// <para><b>Fractional values are the normal case here, not the exception.</b> Over those 94 rows
/// <see cref="AcquiredDisposedRatio"/> was fractional on 87, <see cref="AverageDisposed"/> on 85 and
/// <see cref="AverageAcquired"/> on 76 — while the totals and the transaction counts were fractional on none.
/// That split is the reason four fields here are <see cref="decimal"/> and four are <see cref="int"/>.</para>
///
/// <para><b>Two pairs of fields read alike and count different things.</b>
/// <see cref="DisposedTransactions"/> and <see cref="TotalSales"/> both count filings;
/// <see cref="TotalDisposed"/> counts shares. On the measured 2026 Q2 row those are 40, 14 and 927,380.</para></summary>
public sealed record InsiderTradeStatistics
{
    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The calendar year.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter, 1 to 4.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }

    /// <summary>How many acquiring transactions were filed. A count of filings — never fractional across 94
    /// rows measured.</summary>
    [JsonPropertyName("acquiredTransactions")] public int? AcquiredTransactions { get; init; }

    /// <summary>How many disposing transactions were filed. A count.</summary>
    [JsonPropertyName("disposedTransactions")] public int? DisposedTransactions { get; init; }

    /// <summary><see cref="AcquiredTransactions"/> over <see cref="DisposedTransactions"/>. <b>Fractional on
    /// 87 of 94 rows measured</b> — <c>0.175</c> and <c>1.5</c> on the captured rows — and <c>0</c> in a
    /// quarter with no acquisitions, which is a value rather than an absence.</summary>
    [JsonPropertyName("acquiredDisposedRatio")] public decimal? AcquiredDisposedRatio { get; init; }

    /// <summary>Total <b>shares</b> acquired across the quarter, not a count of filings.</summary>
    [JsonPropertyName("totalAcquired")] public decimal? TotalAcquired { get; init; }

    /// <summary>Total shares disposed across the quarter — 927,380 on the measured 2026 Q2 row, against a
    /// <see cref="TotalSales"/> of 14.</summary>
    [JsonPropertyName("totalDisposed")] public decimal? TotalDisposed { get; init; }

    /// <summary>Mean shares per acquiring transaction. <b>Fractional on 76 of 94 rows.</b></summary>
    [JsonPropertyName("averageAcquired")] public decimal? AverageAcquired { get; init; }

    /// <summary>Mean shares per disposing transaction. <b>Fractional on 85 of 94 rows.</b></summary>
    [JsonPropertyName("averageDisposed")] public decimal? AverageDisposed { get; init; }

    /// <summary>How many open-market purchases were filed — a narrower count than
    /// <see cref="AcquiredTransactions"/>, which includes awards and exercises. <c>0</c> on all three captured
    /// AAPL quarters.</summary>
    [JsonPropertyName("totalPurchases")] public int? TotalPurchases { get; init; }

    /// <summary>How many open-market sales were filed. A count of filings, <b>not shares</b> — 14 on the
    /// measured 2026 Q2 row.</summary>
    [JsonPropertyName("totalSales")] public int? TotalSales { get; init; }
}

/// <summary>One insider FMP knows by name, from <c>stable/insider-trading/reporting-name</c> — a lookup that
/// turns a name into the <c>reportingCik</c>
/// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/> takes.
///
/// <para><b>Matching is on a prefix of a surname-first name.</b> Measured 2026-08-28: <c>name=Cook</c> answered
/// 133 rows all beginning "Cook"; <c>name=Apple</c> answered 20 including <c>"Applebach Richard Jr"</c> and
/// <c>"Applebaum Michelle Galanter"</c>. Searching a given name finds nothing, and this is not a company
/// search.</para></summary>
public sealed record InsiderReportingName
{
    /// <summary>The insider's Central Index Key, zero-padded — the value to pass as
    /// <c>reportingCik</c>.</summary>
    [JsonPropertyName("reportingCik")] public string? ReportingCik { get; init; }

    /// <summary>The name as EDGAR spells it, surname first — <c>"Cook Adam T"</c>.</summary>
    [JsonPropertyName("reportingName")] public string? ReportingName { get; init; }
}

/// <summary>One SEC transaction code, from <c>stable/insider-trading-transaction-type</c> — the eighteen values
/// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/> accepts and
/// <see cref="InsiderTrade.TransactionType"/> carries.
///
/// <para><b>A one-field record rather than an enum, and rather than a bare string.</b></para>
///
/// <para><i>Not an enum:</i> the list is served by an endpoint, so FMP can extend it without an SDK release,
/// and the empty string that appears on 40 of 1,000 measured trade rows would have no member to map to. A
/// closed C# enum over an open server-side list is a breaking change waiting for a Tuesday.</para>
///
/// <para><i>Not <c>IReadOnlyList&lt;string&gt;</c>:</i> the wire shape is
/// <c>[{"transactionType": "A-Award"}, …]</c>, and projecting it to bare strings would need a converter whose
/// only job is to discard a key. If FMP adds a description field, this record absorbs it and the projection
/// would have to be unpicked.</para></summary>
public sealed record InsiderTransactionType
{
    /// <summary>The code — <c>"A-Award"</c> through <c>"Z-Trust"</c>. The letter is the SEC's Table I/II code
    /// and the word is FMP's gloss on it.</summary>
    [JsonPropertyName("transactionType")] public string? TransactionType { get; init; }
}
```

- [ ] **Step 5: Register all three**

`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<InsiderTradeStatistics>))]
[JsonSerializable(typeof(List<InsiderReportingName>))]
[JsonSerializable(typeof(List<InsiderTransactionType>))]
```

That is the thirteenth entry, and the count is worth checking against the plan's Global Constraints list before
moving on: `FilingQuarter`, `InstitutionalHolding`, `HolderAnalytics`, `HolderIndustryBreakdown`,
`HolderPerformance`, `IndustryOwnershipSummary`, `InstitutionalFiling`, `SymbolPositions`,
`BeneficialOwnership`, `InsiderTrade`, `InsiderTradeStatistics`, `InsiderReportingName`,
`InsiderTransactionType`. **A missing one fails at runtime, not at compile time**, which is why this is checked
rather than trusted.

- [ ] **Step 6: Add the three methods**

Append to `src/FmpDotNet/Endpoints/InsiderTradesEndpoints.cs`, inside the class:

```csharp
    /// <summary>Insider activity for one issuer, aggregated by quarter —
    /// <c>stable/insider-trading/statistics</c>.
    ///
    /// <para>One row per quarter with any activity, newest first — 94 for AAPL, measured 2026-08-28, back to
    /// 2003. No <c>limit</c>, no <c>page</c>, and no year or quarter filter: the endpoint honours none of
    /// them.</para>
    ///
    /// <para><b>Read <see cref="InsiderTradeStatistics"/> before comparing its fields.</b> Two pairs of them
    /// read alike and count different things — transactions against shares.</para></summary>
    /// <param name="symbol">The issuer's ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter with activity, newest first. Never <see langword="null"/>; empty for an unknown
    /// symbol, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTradeStatistics>> GetStatisticsAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/statistics").With("symbol", symbol),
            FmpJsonContext.Default.ListInsiderTradeStatistics, ct);
    }

    /// <summary>Insiders whose name starts with what you typed —
    /// <c>stable/insider-trading/reporting-name</c>.
    ///
    /// <para><b>The way to get a <c>reportingCik</c> for <see cref="SearchAsync"/>.</b> That method takes a
    /// CIK, not a name; this turns one into the other.</para>
    ///
    /// <para><b>A prefix match against a surname-first name.</b> Measured 2026-08-28, <c>Cook</c> answered 133
    /// rows all beginning "Cook", and <c>Apple</c> answered 20 including <c>Applebach</c> and
    /// <c>Applebaum</c> — so it is neither a substring match nor a company search, and a given name finds
    /// nothing.</para>
    ///
    /// <para>No <c>limit</c>: the endpoint ignores it.</para></summary>
    /// <param name="name">The start of the insider's name, surname first.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching insiders. Never <see langword="null"/>; empty for an unmatched prefix, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderReportingName>> SearchReportingNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/insider-trading/reporting-name").With("name", name),
            FmpJsonContext.Default.ListInsiderReportingName, ct);
    }

    /// <summary>The eighteen SEC transaction codes <see cref="SearchAsync"/> accepts —
    /// <c>stable/insider-trading-transaction-type</c>.
    ///
    /// <para><b>Note the path: a sibling of <c>insider-trading/*</c>, not a child of it.</b> FMP spells this
    /// one with a hyphen where the rest of the group uses a slash.</para>
    ///
    /// <para>Takes no parameters and answers the whole list. Measured 2026-08-28: 18 rows, <c>A-Award</c>
    /// through <c>Z-Trust</c>, and every <c>transactionType</c> on 1,000 sampled trade rows was drawn from it
    /// or was the empty string.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The eighteen codes. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<InsiderTransactionType>> GetTransactionTypesAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/insider-trading-transaction-type"),
            FmpJsonContext.Default.ListInsiderTransactionType, ct);
```

- [ ] **Step 7: Promote the fourteen deferred cross-references**

Every symbol in the slice now exists, so the placeholders can become real links. Until this step they had to
stay `<c>` spans: `GenerateDocumentationFile` plus `TreatWarningsAsErrors` makes an unresolvable `<see cref>` a
**CS1574 build error**, and these records reference each other in both directions — `InstitutionalHolding`
points at `InstitutionalFiling`'s dates and `InstitutionalFiling` points back at `InsiderTrade.FormType` — so
no task ordering could have avoided it.

Replace `<c>X</c>` with `<see cref="X"/>` at each of these fourteen sites, in five files:

| file | member | placeholder |
|---|---|---|
| `Models/FilingQuarter.cs` | `Date` | `InstitutionalFiling.FilingDate` |
| `Models/InstitutionalHolding.cs` | `FilingDate` | `InstitutionalFiling.FilingDate` |
| `Models/InstitutionalHolding.cs` | `AcceptedDate` | `InstitutionalFiling.AcceptedDate` |
| `Models/HolderAnalytics.cs` | `IsCountedForPerformance` | `HolderPerformance` |
| `Models/HolderSummaries.cs` | `HolderIndustryBreakdown.IndustryTitle` | `IndustryOwnershipSummary.IndustryTitle` |
| `Models/InstitutionalFiling.cs` | `InstitutionalFiling.FormType` | `InsiderTrade.FormType` |
| `Models/InsiderTrade.cs` | `TransactionType` | `Endpoints.InsiderTradesEndpoints.GetTransactionTypesAsync` |
| `Endpoints/InstitutionalOwnershipEndpoints.cs` | class doc | `InsiderTradesEndpoints` |
| `Endpoints/InstitutionalOwnershipEndpoints.cs` | `MaxHolderAnalyticsPageSize` | `MaxOwnershipPageSize` |
| `Endpoints/InstitutionalOwnershipEndpoints.cs` | `MaxHolderAnalyticsPageSize` | `InsiderTradesEndpoints.MaxInsiderTradePageSize` |
| `Endpoints/InstitutionalOwnershipEndpoints.cs` | `MaxOwnershipPageSize` | `GetBeneficialOwnershipAsync` (twice) |
| `Endpoints/InsiderTradesEndpoints.cs` | `SearchAsync`'s `transactionType` param | `GetTransactionTypesAsync` |
| `FmpClient.cs` | `InstitutionalOwnership` | `InsiderTrades` |

Then confirm none was missed, and none was invented:

```bash
grep -rn '<c>InstitutionalFiling\.\|<c>InsiderTrade\.\|<c>InsiderTrades\|<c>HolderPerformance<\|<c>IndustryOwnershipSummary\.\|<c>MaxOwnershipPageSize<\|<c>GetBeneficialOwnershipAsync<\|<c>GetTransactionTypesAsync<\|<c>Endpoints\.InsiderTrades' src/FmpDotNet
dotnet build src/FmpDotNet
```

Expected: the grep prints **nothing**, and the build succeeds. A CS1574 here means a cref names something that
does not exist — read the name rather than deleting the reference; it is more likely a typo than a missing
type. **Do not suppress CS1574** to get past this.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~InsiderTradesTests`
Expected: PASS.

Then run the whole unit suite: `dotnet test tests/FmpDotNet.Tests`
Expected: **one** failure —
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`, still
red until Task 11. Anything else failing here is a regression from this task or an earlier one.

- [ ] **Step 9: Mutation-check**

1. Retype `AcquiredDisposedRatio` as `int?` → `The_statistics_ratios_and_averages_are_usually_fractional` fails
   with a `System.Text.Json` exception on `0.175`. Restore.
2. Retype `TotalSales` as `decimal?` → nothing fails. Record it: the `int?`/`decimal?` split on this record is
   documented and measured but only half-defended by tests, because widening a type is never a parse failure.
3. Change the transaction-type path to `stable/insider-trading/transaction-type` →
   `The_transaction_type_call_sends_no_parameters_at_all` fails on the `AbsolutePath` assertion. Restore. This
   is the typo the rest of the group's spelling invites.
4. Delete `[JsonSerializable(typeof(List<InsiderTransactionType>))]` → the build fails on
   `FmpJsonContext.Default.ListInsiderTransactionType`. **Record it as a compile-level mutation** — and note
   that it is compile-level only because the code names the generated property. A model registered nowhere and
   reached through a different route would fail at runtime instead, which is the case the Global Constraints
   warn about.

- [ ] **Step 10: Commit**

```bash
# The promotion in Step 7 touches five more files; add everything rather than listing it.
git add src/FmpDotNet tests/FmpDotNet.Tests/InsiderTradesTests.cs \
        tests/FmpDotNet.Tests/Fixtures/insider-trading-statistics.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/insider-trading-reporting-name.head.json \
        tests/FmpDotNet.Tests/Fixtures/insider-trading-transaction-type.json
git commit -m "feat: add insider statistics, the name lookup and the transaction-type list

Statistics is the third place fractional values appear and the only one where
fractional is the normal case: acquiredDisposedRatio on 87 of 94 rows measured,
averageDisposed on 85, averageAcquired on 76 — while the totals and transaction
counts were fractional on none, which is where the int?/decimal? split comes
from.

reporting-name matches a prefix of a surname-first name: name=Apple answers
Applebach and Applebaum. transaction-type is a sibling path, not a child —
insider-trading-transaction-type, hyphen not slash."
```

### Task 10: Teach the live sweep to ask the fourteen new paths something worth answering

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` — `+FilerCik`, `+InsiderReportingCik`, `+InsiderTransactionCode`, `+InsiderNameQuery`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()`'s `string` arm
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` — `+1` keyless guard, and the class doc's counts

**Interfaces:**
- Consumes: `LiveApi.Cik`, `LiveApi.Symbol`, `LiveApi.SettledQuarter` (Task 2), the fourteen new endpoint
  methods.
- Produces: nothing the SDK consumes. This task's output is that the scheduled live run actually exercises the
  fourteen paths instead of recording empty baselines for them.

**Everything in this task is a silent failure, not a loud one.** `Probe.Argument`'s `string` arm ends
`_ => LiveApi.Symbol`, so an unrecognised parameter name becomes `"AAPL"` and the call succeeds against a
question nobody meant to ask. Every one of these paths answers a wrong-but-well-formed argument with `[]` and
HTTP 200, which the probe records as `outcome empty` — a baseline that agrees with itself every week
thereafter. Task 2 fixed the two cases that go red on their own; these are the ones that do not.

**Hazard 1 — `cik` means the wrong thing on this facade.** `Probe.Argument` maps `cik` to `LiveApi.Cik`, which
is `"320193"`: Apple's CIK, an **issuer**. The four `cik`-keyed methods on
`InstitutionalOwnershipEndpoints` want an institutional **filer's** CIK. Measured 2026-08-28, all four answer
zero rows for Apple's:

| method, with `cik=320193` | rows | with Berkshire's `0001067983` |
|---|---|---|
| `GetFilingDatesAsync` | 0 | 53 |
| `GetHoldingsAsync` | 0 | 41 |
| `GetHolderIndustryBreakdownAsync` | 0 | 33 |
| `GetHolderPerformanceAsync` | 0 | 53 |

**Hazard 2 — `SearchAsync`'s optional discriminators all collapse to `"AAPL"`.** Nothing in the smoke suite
inspects `IsOptional`, so `Probe` supplies *every* parameter, optional ones included. `reportingCik`,
`companyCik` and `transactionType` are all unknown to the `string` arm and all fall through to
`LiveApi.Symbol`. Measured 2026-08-28,
`insider-trading/search?symbol=AAPL&reportingCik=AAPL&companyCik=AAPL&transactionType=AAPL` returns `[]` — and
so does the form with only `transactionType` bogus. A method that works perfectly would sweep green while
returning nothing.

**The four values must be mutually consistent, because `SearchAsync` receives all four at once.** They
intersect (Task 8), so one inconsistent value empties the result. Measured 2026-08-28,
`symbol=AAPL&reportingCik=1780525&companyCik=320193&transactionType=S-Sale` answers **3 rows with all sixteen
fields populated** — thin, but a complete baseline rather than an empty one, and permanent: EDGAR history does
not disappear.

**One deviation from the spec, and it is a naming call rather than a measurement.** The spec says
`insider-trading/reporting-name` "works by luck" through `name => LiveApi.AcquirerNameQuery` and should be
"left alone, with a comment saying so". It gets its own constant instead. `LiveApi.CompanyNameQuery` already
sets that precedent in this exact situation and says why: two endpoints spelling the same word must not share
one constant, because *"a future change to one must not silently move the other"*. Aliasing is the coupling,
not the honesty — the honesty goes in the constant's doc comment, which records that `"Apple"` matches by
prefix accident.

- [ ] **Step 1: Add the four `LiveApi` constants**

`tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `Cik`:

```csharp
    /// <summary>An institutional <b>filer's</b> Central Index Key, for the four <c>cik</c>-keyed 13F probes —
    /// Berkshire Hathaway, <c>0001067983</c>.
    ///
    /// <para><b>Distinct from <see cref="Cik"/>, and the distinction is the whole point.</b> That is Apple's
    /// CIK — an <i>issuer</i>. The 13F paths want the CIK of an institution that <i>files</i>. Measured
    /// 2026-08-28, Apple's <c>320193</c> answers <b>zero rows</b> on all four of
    /// <c>institutional-ownership/dates</c>, <c>/extract</c>, <c>/holder-industry-breakdown</c> and
    /// <c>/holder-performance-summary</c>, each with HTTP 200 rather than an error — so the sweep would have
    /// recorded <c>rows 0</c> as the baseline for four endpoints and matched it every week thereafter. The same
    /// silent green <see cref="Exchange"/>, <see cref="Cik"/> and <see cref="AcquirerNameQuery"/> were named
    /// for.</para>
    ///
    /// <para>Berkshire's <c>0001067983</c> answers 53, 41, 33 and 53 rows against those four, paired with
    /// <see cref="SettledYear"/> and <see cref="SettledQuarter"/>. Given padded, because that is the form FMP
    /// returns and the endpoint accepts either.</para></summary>
    public const string FilerCik = "0001067983";

    /// <summary>An insider's Central Index Key, for <c>insider-trading/search</c>'s <c>reportingCik</c> —
    /// <c>1780525</c>, Apple's SVP and General Counsel.
    ///
    /// <para><b>Chosen to agree with <see cref="Symbol"/>, <see cref="Cik"/> and
    /// <see cref="InsiderTransactionCode"/>.</b> <c>Probe</c> supplies every parameter including the optional
    /// ones, and the four discriminators intersect — so one value that contradicts the others empties the
    /// result. Measured 2026-08-28, the four together answer 3 rows with all sixteen fields populated.</para>
    ///
    /// <para><b>Without this case the parameter falls through to <see cref="Symbol"/></b>, and
    /// <c>reportingCik=AAPL</c> answers <c>[]</c> with HTTP 200 — the silent green this suite exists to
    /// catch.</para>
    ///
    /// <para>Given unpadded deliberately, for the reason on <see cref="Cik"/>: both forms work, measured
    /// 2026-08-28 with byte-identical responses, so this also exercises the normalisation.</para></summary>
    public const string InsiderReportingCik = "1780525";

    /// <summary>The SEC transaction code the insider search is probed with — <c>"S-Sale"</c>.
    ///
    /// <para>Named for the reason on <see cref="Exchange"/>: unrecognised, <c>transactionType</c> would become
    /// <c>"AAPL"</c>, and measured 2026-08-28 that alone empties the response even when the other three
    /// discriminators are right.</para>
    ///
    /// <para><c>"S-Sale"</c> rather than any of the other seventeen because it is one
    /// <see cref="InsiderReportingCik"/> actually filed against <see cref="Symbol"/> — the four have to
    /// intersect. A code from <c>insider-trading-transaction-type</c>, so the sweep asks with a value that
    /// endpoint vouches for.</para></summary>
    public const string InsiderTransactionCode = "S-Sale";

    /// <summary>The name <c>insider-trading/reporting-name</c> is probed with — <c>"Apple"</c>.
    ///
    /// <para><b>It works, and it works by accident.</b> That endpoint matches a prefix of a surname-first
    /// person's name, so <c>"Apple"</c> hits <c>"Apple Allan Victor"</c>, <c>"Applebach Richard Jr"</c> and
    /// <c>"Applebaum Michelle Galanter"</c> — 20 rows measured 2026-08-28. It has nothing to do with the
    /// company, and a reader who assumes otherwise will assume the endpoint searches issuers.</para>
    ///
    /// <para><b>Its own constant rather than an alias of <see cref="AcquirerNameQuery"/>, for the reason
    /// <see cref="CompanyNameQuery"/> gives:</b> two endpoints spelling the same word must not share one
    /// constant, because a future change to one would silently move the other. Three constants now hold
    /// <c>"Apple"</c> for three different endpoints, and that repetition is the point.</para></summary>
    public const string InsiderNameQuery = "Apple";
```

- [ ] **Step 2: Teach `Probe.Argument` the four names**

`tests/FmpDotNet.SmokeTests/Probe.cs`. Replace the `string` arm's body so that `cik` dispatches on the
declaring type — the mechanism the `from` arm already uses to separate calendar from economics semantics — and
add the three insider cases:

```csharp
        if (type == typeof(string))
            return parameter.Name switch
            {
                // `cik` means two different things depending on who is asking, and both are well-formed. The
                // 13F paths want an institutional FILER's CIK; everything else wants an issuer's. Measured
                // 2026-08-28, Apple's issuer CIK answers zero rows on all four cik-keyed 13F paths with HTTP
                // 200 — so without this arm the sweep records `rows 0` for four endpoints and agrees with
                // itself forever. Dispatched on the declaring type, the same way `from` separates the calendar
                // and economics windows below.
                "cik" when parameter.Member.DeclaringType == typeof(Endpoints.InstitutionalOwnershipEndpoints)
                    => LiveApi.FilerCik,
                "cik" => LiveApi.Cik,

                // insider-trading/search takes four optional discriminators and Probe supplies ALL of them —
                // nothing here inspects IsOptional. They intersect, so one wrong value empties the result, and
                // all three of these would otherwise fall through to the AAPL default below. The four values
                // are chosen to agree: an Apple officer, Apple's issuer CIK, and a code that officer filed.
                "reportingCik" => LiveApi.InsiderReportingCik,
                // The issuer's CIK, which is exactly what LiveApi.Cik is — but named, because a reader
                // otherwise cannot tell this case from the filer case above.
                "companyCik" => LiveApi.Cik,
                "transactionType" => LiveApi.InsiderTransactionCode,

                // insider-trading/reporting-name matches a person's name, not a company's. Its own constant
                // rather than AcquirerNameQuery's, so a change to the M&A probe cannot silently move this one.
                "name" when parameter.Member.DeclaringType == typeof(Endpoints.InsiderTradesEndpoints)
                    => LiveApi.InsiderNameQuery,

                "exchange" => LiveApi.Exchange,
                "cusip" => LiveApi.Cusip,
                "isin" => LiveApi.Isin,
                "query" => LiveApi.SearchQuery,
                "name" => LiveApi.AcquirerNameQuery,
                "company" => LiveApi.CompanyNameQuery,
                "formType" => LiveApi.FormType,
                "sicCode" => LiveApi.SicCode,
                _ => LiveApi.Symbol,
            };
```

**The two `"name"` arms must stay in this order** — C# switch expressions match top to bottom, and the
unguarded `"name" => LiveApi.AcquirerNameQuery` would shadow the guarded one if it came first. The same is true
of the two `"cik"` arms. The compiler does not warn about this; it only warns about arms that are wholly
unreachable.

- [ ] **Step 3: Write the failing guard**

`tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`. First update the class doc, which counts its own facts:
change *"All nine checks below"* to *"All ten checks below"* and *"The remaining six pin the literal
argument"* to *"The remaining seven pin the literal argument"*, and extend that sentence's list of examples
with **"an issuer's CIK where four 13F paths need an institutional filer's"**.

Then add the fact:

```csharp
    [Fact]
    public void The_sweep_asks_the_thirteen_f_paths_for_a_filer_cik_rather_than_an_issuer_cik()
    {
        // The synthesiser produces a well-formed CIK for every one of these, so the generic argument check
        // above passes either way — this is the check that the CIK means the right thing. Measured 2026-08-28,
        // Apple's issuer CIK (LiveApi.Cik) answers ZERO rows on all four of these paths with HTTP 200, so the
        // sweep would record `rows 0` as their baseline and match it every week after. Berkshire's filer CIK
        // answers 53, 41, 33 and 53.
        // Probe.EndpointMethods rather than raw BindingFlags, so this walks exactly the methods the sweep
        // walks — and so the file needs no `using System.Reflection`.
        var filerKeyed = Probe.EndpointMethods(typeof(Endpoints.InstitutionalOwnershipEndpoints))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.Name == "cik")
            .ToList();

        // Four of them, and if that number changes this test should be revisited rather than adjusted.
        Assert.Equal(4, filerKeyed.Count);
        Assert.All(filerKeyed, p => Assert.Equal(LiveApi.FilerCik, Probe.Argument(p)));
        Assert.NotEqual(LiveApi.Cik, LiveApi.FilerCik);

        // And the issuer meaning survives elsewhere: SecFilings still gets an issuer's CIK.
        var issuerKeyed = Probe.EndpointMethods(typeof(Endpoints.SecFilingsEndpoints))
            .SelectMany(m => m.GetParameters())
            .First(p => p.Name == "cik");
        Assert.Equal(LiveApi.Cik, Probe.Argument(issuerKeyed));
    }
```

- [ ] **Step 4: Run the guard to verify it fails**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: FAIL to compile if `LiveApi.FilerCik` is not yet added, or FAIL on
`Assert.All(cikKeyed, …)` if the `Probe.Argument` dispatch is not yet in place. Both are the guard doing its
job; write Steps 1 and 2 in either order but confirm you have seen this red before it goes green.

- [ ] **Step 5: Run the keyless smoke tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.SmokeTests`
Expected: PASS. Every test in this project is either keyless or gated behind `LiveFactAttribute`, so a run
without `FMP_API_KEY` exercises exactly the reflection guards and skips the live sweep. That is the run that
matters here.

- [ ] **Step 6: Mutation-check**

1. Delete the `"cik" when … InstitutionalOwnershipEndpoints` arm →
   `The_sweep_asks_the_thirteen_f_paths_for_a_filer_cik_rather_than_an_issuer_cik` fails. Restore.
2. Move the unguarded `"cik" => LiveApi.Cik` arm above the guarded one → the same test fails, and **the
   compiler says nothing**. Record it: switch-arm order here is load-bearing and unchecked by the language.
3. Delete the `"transactionType"` arm → **nothing fails**, because the value still synthesises. Record it as
   the mutation that shows the limit of what a keyless suite can guard: the wrongness is only visible in the
   live baseline, which is why Task 11 re-records it and reads the diff rather than trusting it.
4. Change `LiveApi.InsiderTransactionCode` to `"P-Purchase"` → nothing fails keylessly, and the next live run
   records `outcome empty` for `SearchAsync`, because Apple's General Counsel filed no open-market purchases.
   Restore. Same lesson as 3.

- [ ] **Step 7: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs
git commit -m "test: stop the live sweep asking the new paths a meaningless question

Two measured blind spots, both silent. Probe.Argument maps cik to Apple's issuer
CIK, and all four cik-keyed 13F paths answer zero rows for it with HTTP 200 —
the sweep would have recorded rows 0 as their baseline and agreed with itself
every week. And nothing in the suite inspects IsOptional, so SearchAsync's three
optional discriminators all fell through to AAPL, which empties the response.

cik now dispatches on the declaring type, the way `from` already separates the
calendar and economics windows. The four insider values are chosen to intersect,
because Probe supplies all of them at once."
```

### Task 11: Regenerate the README, re-record the baseline, reconcile the epic

Two generated artifacts, one block of hand-written prose that no test reads, and one tracking issue that has to
be re-verified rather than adjusted.

**Files:**
- Modify: `README.md`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`
- GitHub: issue #25 (the epic) and issue #36 (this slice)

**Interfaces:** none — nothing downstream depends on this task.

- [ ] **Step 1: Regenerate the coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

Then check the result rather than trusting it:

```bash
git diff --stat README.md
grep -n "of FMP.s 243 endpoint paths are modelled" README.md
sed -n '/BEGIN GENERATED/,/END GENERATED/p' README.md | grep -c '^| `stable/'
```

Expected: the headline reads **154 of FMP's 243 endpoint paths are modelled**, the table has **154** rows (up
from 140), and two new sections appear — `` `fmp.InsiderTrades` `` with five paths and
`` `fmp.InstitutionalOwnership` `` with nine, the latter including
`` `stable/acquisition-of-beneficial-ownership` ``. If the headline is not 154, an endpoint is not being
discovered, and `EndpointCoverageTests.Every_public_endpoint_method_reaches_the_api` names which.

- [ ] **Step 2: Update the three prose counts under "Reaching an endpoint that is not modelled"**

`EndpointCoverageTests` regenerates the table above this prose but does not read the prose, so it rots
silently — it had drifted a whole slice behind by the time #30 shipped. Three numbers and one list change.

Replace the paragraph beginning "The rest is unbuilt rather than blocked" with:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **89 paths
remain**, of which **82 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are Senate & House (12) and
Economics/Transcripts/ESG/COT (12), then Market Performance (11), News (10) and Fundraisers & DCF (10); ETF &
Mutual Funds, Technical Indicators and Indexes & Market Hours carry 9 apiece.
```

and replace the paragraph beginning "That remainder is tracked as ten issues" with:

```markdown
That remainder is tracked as nine issues under the epic, eight of them actionable, each 9 to 12 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 154 modelled plus 89 remaining, with no path counted twice and none missing.
```

Note the "each 9 to 14 paths" becomes "each 9 to 12 paths" — #36 was the 14, and it is leaving the remainder.
Leave the two paragraphs after those unchanged; both are still true.

- [ ] **Step 3: Verify the arithmetic against the issues rather than trusting it**

```bash
for n in 31 32 33 34 35 38 39 40 41; do
  gh issue view $n --json body --jq .body | grep -coE 'stable/[a-z0-9-]'
done | paste -sd+ | bc
```

Expected: `89`. That is `243 − 154`, so the partition holds with no gap and no double count. If it prints
anything else, the prose is wrong — fix the prose, not this check. (#36 is deliberately absent from the loop; it
is shipping.)

- [ ] **Step 4: Run the unit suite green**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS, all of it, including both `EndpointCoverageTests` facts. **This is the first point in the plan
where the whole unit suite is green** — `The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`
has been red since Task 1 by design.

- [ ] **Step 5: Re-record the live baseline**

The baseline is a measurement, not a specification — never hand-edit it. Record it in one run so its header
date is true of every line:

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY\s*=\s*\"?([^\"\s]+)\"?', open('.env').read(), re.M).group(1))") \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

Do not `source` the `.env` — it has clobbered `PATH` for a whole shell before; extract the one variable into
the one command's environment, as above. Do not set `FMPDOTNET_SMOKE_BULK`: `baseline-bulk.txt` is untouched by
this slice, and re-recording it would spend the key's standing on twenty of FMP's most restricted endpoints for
nothing. **Never print a built URL** — the key travels in the query string.

`ShapeAssertions.Updated` refuses to write a baseline from a run in which any endpoint errored, so a transport
fault or a throttled key fails loudly here instead of writing `outcome error` in as an endpoint's recorded
truth. If it refuses, wait and re-run rather than working around it.

- [ ] **Step 6: Read the baseline diff before committing it**

```bash
git diff --stat tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
grep -c '^\[' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | grep -E '^\+\[|outcome' | head -60
```

Expected, and each item is a thing to check rather than assume:

1. The entry count goes from **128 to 142** — fourteen new endpoint methods, one per new path.
2. Every one of the fourteen reads `outcome rows`. **Not one may read `empty`.** Task 10 exists precisely
   because four of them would otherwise read empty for a well-formed argument and one more would read empty for
   a well-formed search. An `empty` here means Task 10's fix did not take, or a measured value has gone stale —
   investigate rather than record.
3. `InsiderTrades.SearchAsync` will read **3 rows**, which is thin but complete: all sixteen properties were
   measured populated across those three on 2026-08-28. If it reads fewer properties than
   `InsiderTrades.GetLatestAsync`, the four discriminators have stopped intersecting.
4. `InstitutionalOwnership.GetSymbolPositionsAsync` will read **1 row** — it is a single-record path, and
   `Probe.Flatten` turns one record into one row.
5. The header date is today's.
6. Nothing else changed. Any `now always null, was populated` line on an endpoint this slice did not touch is a
   real finding — stop and investigate rather than committing it.

- [ ] **Step 7: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "docs: regenerate the coverage table and re-record the live baseline

154 of 243 paths modelled, up from 140. The prose counts under 'Reaching an
endpoint that is not modelled' are hand-written and no test reads them, so they
move by hand: 103 remaining to 89, 96 actionable to 82, ten open issues to nine.

baseline-ordinary.txt gains fourteen blocks, all reading 'rows'. baseline-bulk.txt
is untouched."
```

- [ ] **Step 8: Reconcile the epic, and re-verify rather than assume**

Issue #25's body carries a Shipped table, a remainder table, two subtotals and a partition sentence. Four edits,
and the last one is a check rather than an edit:

1. Add to the **Shipped** table, after the #37 row:
   `| #36 | Form 13F and Insider Trades | 14 | 2026-08-28 — new fmp.InstitutionalOwnership and fmp.InsiderTrades facades |`
2. Delete the `| #36 | Form 13F and Insider Trades | 14 | new |` row from **The remainder**.
3. Change the subtotals: **actionable subtotal 96 → 82**, **total remaining 103 → 89**. Change the TipRanks
   paragraph's closing sentence to *"That is why the actionable number is 82 rather than 89."*
4. Change the partition sentence to read *"Re-verified 2026-08-28 after #36: the paths listed across the nine
   open children total 89, which is `243 − 154`, with no gap and no path counted twice."* — and re-run Step 3's
   loop to confirm it before writing it down. The sentence claims a verification; make the claim true.

Also update the sentence *"and the table below is that enumeration minus Statements, Company, SEC Filings, and
Analyst and Calendar"* to add *"and Form 13F and Insider Trades"*, and *"#28, #29, #30 and #37 have all shipped
since"* to include #36.

Leave the collapsed "What this issue used to say, and why it was wrong" section untouched — it is a record of a
past error, not a live count.

- [ ] **Step 9: Close #36 with what it actually shipped**

```bash
gh issue close 36 --comment "Shipped 2026-08-28. Two new facades — \`fmp.InstitutionalOwnership\` (9 paths) and \`fmp.InsiderTrades\` (5) — taking coverage from 140 of 243 to 154.

\`stable/acquisition-of-beneficial-ownership\` is on the institutional facade rather than the insider one: an SC 13D/G is a stake disclosure by an entity, not a Form 4 transaction. That follows #30's precedent of redistributing 3 of its 12 paths by what they return rather than by FMP's own grouping.

Thirteen records, 195 fields, no new converter. Three measurements corrected the design spec during implementation: \`extract-analytics/holder\` caps at 100 rather than the group's 1,000; \`acquisition-of-beneficial-ownership\`'s cap could not be provoked and is documented as sibling-derived; and \`FmpJsonContext\` needed thirteen entries rather than fourteen.

Design: docs/superpowers/specs/2026-08-28-form-13f-and-insider-trades-design.md
Measurements: docs/superpowers/specs/2026-08-28-form-13f-and-insider-trades-measurements.md
Plan: docs/superpowers/plans/2026-08-28-form-13f-and-insider-trades.md"
```

- [ ] **Step 10: Finish the branch**

Use `superpowers:finishing-a-development-branch`. `master` carries a ruleset requiring a pull request and the
`.NET — build + test` check, so the path is branch → PR → green → merge.

---

## Totals

**78 new test methods** across five test files (four created, one modified), 14 fixtures, 14 paths, 14 public
methods, 13 records, 195 fields, 13 `FmpJsonContext` entries, 3 page-size constants, 2 new facades, **0 new
converters**.

Composition, counted from this document rather than estimated: 31 in `InstitutionalOwnershipTests` (Tasks 1–5),
12 in `InstitutionalFilingTests` (Task 6), 9 in `BeneficialOwnershipTests` (Task 7), 25 in `InsiderTradesTests`
(Tasks 8–9), and 1 added to `SweepCoverageTests` (9 → 10). 31 + 12 + 9 + 25 + 1 = 78.

**Count these again after execution rather than trusting the figure.** The last two slices both ended above
their planned totals, and in each case the extra tests were the ones the reviews demanded because nothing else
covered the hole. Four of this plan's 78 exist only because of that history — the "limit exactly at the cap is
accepted" boundaries in Tasks 3, 6, 7 and 8, which the previous branch's review had to add three separate times
after `ThrowIfGreaterThan`/`ThrowIfGreaterThanOrEqual` proved to be a swap the whole suite tolerates.

### What this plan changed about the spec

Three measured corrections and two decisions, all argued at the top of this document and carried into the tasks
that implement them:

| # | the spec says | measured / decided |
|---|---|---|
| 1 | one `MaxOwnershipPageSize` of 1000 | three constants; `extract-analytics/holder` caps at **100** |
| 2 | (silent) | `acquisition-of-beneficial-ownership`'s cap could not be provoked — documented as sibling-derived |
| 3 | 13 entries "plus `SymbolPositions` if unwrapped" | 13 entries; it is not unwrapped that way |
| 4 | every sweep-argument fix in the late task | two of them redden a keyless test at Task 2 |
| 5 | leave `reporting-name` aliased to `AcquirerNameQuery` | its own constant, per `CompanyNameQuery`'s precedent |

A sixth item is neither: the fourteen deferred `<see cref>` promotions in Task 9, Step 7 exist because
`GenerateDocumentationFile` plus `TreatWarningsAsErrors` makes an unresolvable cref a build error, and these
records reference each other in both directions. No task ordering avoids it.
