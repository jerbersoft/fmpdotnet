# Statements Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model the 19 unmodelled paths of FMP's Statements section onto `fmp.Statements`, taking it from 8 of 27 paths to 27 of 27 and the SDK from 82 of 243 to 101.

**Architecture:** One existing endpoint class gains 20 methods; no new facade, no `FmpClient` change, no DI change. Only six new record types are needed — eight of the nineteen paths answer field sets the SDK already models, which is the single most important fact in this slice and the reason it is not nineteen new records. `FiscalPeriod` widens from two members to six, and the transport gains its third and fourth read shapes (one JSON object, one binary body). Seven measured traps drive the design and each gets a test that fails when the trap is reintroduced.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` source generation via `FmpJsonContext`, NodaTime (`LocalDate`, `LocalDateTime`), xUnit v2 (2.9.3).

**Spec:** `docs/superpowers/specs/2026-08-27-statements-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-27-statements-measurements.md`

## Global Constraints

- `TreatWarningsAsErrors=true` covers `CS*` and `NU*`. `IsAotCompatible` turns IL2026/IL3050 into build errors — never call a reflection-based `JsonSerializer.Deserialize`; every model goes through `FmpJsonContext`.
- `GenerateDocumentationFile=true` on `src/FmpDotNet/FmpDotNet.csproj` makes an unresolved `<see cref="…"/>` a **CS1574 build error**. Test projects do not generate docs, so crefs there are safe. Check every cref you write in `src/`.
- Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as `[JsonSerializable(typeof(List<X>))]` — or `typeof(X)` for the one object-shaped response — or it will not deserialise.
- Every public member carries XML documentation in house style: it records **what was measured, and on what date** (every measurement in this slice is 2026-08-27), and states plainly anything a caller would otherwise get wrong. Where a value is a trap, the documentation is the deliverable, not decoration.
- Public list-returning methods return `IReadOnlyList<T>`, never null. Single-row lookups return `T?`.
- Every numeric field is `decimal?`. Money is not a `double` in this SDK.
- **The default `limit` is `100000`, never absent.** `StatementEndpoints.FullHistoryLimit` is the single constant and every per-symbol paged path sends it when the caller passes none.
- **`FiscalPeriod` gains `Q1`–`Q4` appended AFTER `Annual` and `Quarter`.** Ordinals 0 and 1 must not move; a shipped caller that persisted the ordinal must keep reading the same value.
- Tests are xUnit `[Fact]`/`[Theory]` with sentence-style method names using underscores, matching `StatementEndpointsTests`.
- **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the response after reading. A test driving N calls builds N responses.
- Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore. A mutation that fails to compile is a stronger result than a failing test — record it as such.
- Fixtures are already committed at `833e89d` and were captured live on 2026-08-27. **Do not hand-write a fixture that a captured one covers**, and do not "tidy" a captured value — the typos and the odd types are the evidence.
- Branch is `feat/statements-coverage`, already created. `master` carries a ruleset requiring the check `.NET — build + test` and a pull request, so the path is branch → PR → green → merge.

## File Structure

**Create:**
- `src/FmpDotNet/Models/AsReportedStatements.cs` — `AsReportedStatement`, `RevenueSegmentation` (one envelope, two value domains; they change together)
- `src/FmpDotNet/Models/OwnerEarnings.cs`
- `src/FmpDotNet/Models/FinancialReports.cs` — `FinancialReportLink`, `FinancialReport`, `FinancialReportJsonConverter`
- `src/FmpDotNet/Models/LatestFinancialStatement.cs`
- `tests/FmpDotNet.Tests/Binding.cs` — the reflection helper every binding test in this slice uses
- `tests/FmpDotNet.Tests/StatementTtmTests.cs`
- `tests/FmpDotNet.Tests/StatementReuseBindingTests.cs`
- `tests/FmpDotNet.Tests/AsReportedTests.cs`
- `tests/FmpDotNet.Tests/FinancialReportTests.cs`
- `tests/FmpDotNet.Tests/LatestStatementsTests.cs`

**Modify:**
- `src/FmpDotNet/FiscalPeriod.cs` — +4 members, rewritten summary, widened `ToQueryValue`
- `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +20 methods, +4 constants, +1 private helper, `Periodic()` fixed (174 lines now; lands near `DirectoryEndpoints.cs`'s size, inside the codebase's range, so no split)
- `src/FmpDotNet/FmpTransport.cs` — +`GetObjectAsync<T>`, +`GetBytesAsync`
- `src/FmpDotNet/Serialization/NodaConverters.cs` — +`NullableLocalDateTimeJsonConverter`
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +11 entries
- `src/FmpDotNet/Models/RatiosTtm.cs`, `KeyMetricsTtm.cs`, `IncomeStatementGrowth.cs`, `BalanceSheetGrowth.cs`, `CashFlowGrowth.cs` — +237 `[JsonPropertyName]` attributes, +3 `[JsonConverter]`
- `tests/FmpDotNet.Tests/StatementEndpointsTests.cs` — the default-limit assertion inverts; +period coverage
- `tests/FmpDotNet.SmokeTests/Probe.cs` — `byte[]` is one answer, not a million rows
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded live
- `README.md` — regenerated table, corrected prose

**Fixtures already committed** (`833e89d`, captured live 2026-08-27) — read them, do not recreate them:

```
income-statement-ttm.AAPL.json                        balance-sheet-statement-ttm.AAPL.json
cash-flow-statement-ttm.AAPL.json                     income-statement-growth.AAPL.json
balance-sheet-statement-growth.AAPL.json              cash-flow-statement-growth.AAPL.json
key-metrics-ttm.AAPL.json                             ratios-ttm.AAPL.json
owner-earnings.AAPL.json                              income-statement-as-reported.AAPL.json
financial-statement-full-as-reported.AAPL.mixed.json  revenue-product-segmentation.AAPL.json
revenue-geographic-segmentation.AAPL.json             financial-reports-dates.AAPL.json
financial-reports-json.AAPL.2025.FY.json              latest-financial-statements.p0.json
```

Two fixtures are trimmed rather than whole, and the trimming is recorded here because a reader must
not mistake them for complete responses. `financial-statement-full-as-reported.AAPL.mixed.json`
keeps 14 of AAPL's 300 `data` keys, chosen to carry every measured value *kind* — 6 ints, 5 strings,
3 floats including `1e-05`. `financial-reports-json.AAPL.2025.FY.json` keeps 5 of 73 top-level keys.
Everything else is the head of the live response with every field of each row intact.

**No xlsx fixture exists and none should.** The workbook is 1,399,564 bytes and nothing about the
zip's interior is under test — only its first four bytes. The tests construct `PK\x03\x04` and the
measured 16-byte miss body inline.

---

### Task 1: `FiscalPeriod` widens, and the shipped five-row truncation is fixed

The only task that changes what already-shipped code does, so it goes first and alone. Everything after it consumes the widened enum and the fixed `Periodic()`.

**Files:**
- Modify: `src/FmpDotNet/FiscalPeriod.cs` (whole file)
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — add `FullHistoryLimit`, fix `Periodic()`, add the `<param>` tags the seven shipped methods never had
- Modify: `tests/FmpDotNet.Tests/StatementEndpointsTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `FiscalPeriod.Q1`/`Q2`/`Q3`/`Q4` (ordinals 2–5), `FiscalPeriodExtensions.ToQueryValue` returning `"Q1"`–`"Q4"`, and `public const int StatementEndpoints.FullHistoryLimit = 100_000`. Every later task uses both.

- [ ] **Step 1: Rewrite `src/FmpDotNet/FiscalPeriod.cs`**

The existing summary argues the enum exists to stop a caller "posting a response value back as a request value". That argument inverts once `Q1` is legal in both directions, so it is replaced rather than left to mislead. Replace the whole file:

```csharp
namespace FmpDotNet;

/// <summary>The reporting cadence asked of the period-shaped endpoints.
///
/// <para><b>Six values, not two.</b> Beyond <c>annual</c> and <c>quarter</c>, FMP accepts each fiscal quarter as
/// a filter ACROSS years, which is a different question from "give me quarters". Measured on AAPL 2026-08-27:</para>
///
/// <code>
/// period=annual   ->  FY2025, FY2024, FY2023, FY2022 …
/// period=quarter  ->  Q32026, Q22026, Q12026, Q42025 …
/// period=Q1       ->  Q12026, Q12025, Q12024, Q12023 …
/// </code>
///
/// <para><b>Deliberately not a string, and the reason has changed.</b> An earlier version of this type said the
/// enum stopped a caller posting a response value back as a request value — FMP labels rows <c>FY</c>/<c>Q1</c>
/// while the request took <c>annual</c>/<c>quarter</c>. That is no longer true: <c>Q1</c> is legal in both
/// directions and <c>FY</c> is accepted as a synonym for <c>annual</c>. What the enum earns now is different and
/// still worth having — it makes all six legal values discoverable, and it rejects everything else. Measured
/// 2026-08-27, <b>an unrecognised period silently falls back to annual</b> on the statement paths:
/// <c>period=bogus</c> answers FY rows at HTTP 200 with no warning, so a typo costs you the wrong series and
/// nothing says so. On the two report-document paths the same typo is an HTTP 400 instead. One query parameter,
/// two behaviours, neither documented.</para>
///
/// <para><b>The order of these members is load-bearing.</b> <see cref="Annual"/> and <see cref="Quarter"/> keep
/// ordinals 0 and 1; the quarters were appended. A caller who persisted the underlying integer keeps reading the
/// value they stored.</para></summary>
public enum FiscalPeriod
{
    /// <summary>Full fiscal years. Rows come back labelled <c>FY</c>.</summary>
    Annual,

    /// <summary>Fiscal quarters, most recent first, across every quarter. Rows come back labelled <c>Q1</c>
    /// through <c>Q4</c>.
    ///
    /// <para><b>Not accepted on the two report-document paths</b> —
    /// <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/> and
    /// <see cref="Endpoints.StatementEndpoints.GetFinancialReportWorkbookAsync"/> reject it. A filed report is one
    /// fiscal period, and "the 2025 quarterly report" is not a document that exists. See those methods for what
    /// FMP does instead when you ask.</para></summary>
    Quarter,

    /// <summary>First fiscal quarter of each year, across years — Q1 2026, Q1 2025, Q1 2024 …</summary>
    Q1,

    /// <summary>Second fiscal quarter of each year, across years.</summary>
    Q2,

    /// <summary>Third fiscal quarter of each year, across years.</summary>
    Q3,

    /// <summary>Fourth fiscal quarter of each year, across years. <b>Not the same series as
    /// <see cref="Annual"/></b> even where the period ends on the same day: measured on AAPL, the Q4 end and the
    /// fiscal year end are both 2025-09-27, and the two series carry different figures.</summary>
    Q4,
}

/// <summary>Conversions for <see cref="FiscalPeriod"/>.</summary>
public static class FiscalPeriodExtensions
{
    /// <summary>The value FMP expects in the <c>period=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible. That is not defensive
    /// tidiness: an unrecognised <c>period</c> is silently reinterpreted as annual by the statement paths, so a
    /// value that escaped this method would return a well-formed answer to a question nobody asked.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this FiscalPeriod period) => period switch
    {
        FiscalPeriod.Annual => "annual",
        FiscalPeriod.Quarter => "quarter",
        FiscalPeriod.Q1 => "Q1",
        FiscalPeriod.Q2 => "Q2",
        FiscalPeriod.Q3 => "Q3",
        FiscalPeriod.Q4 => "Q4",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Not a known fiscal period."),
    };
}
```

The two `<see cref>`s pointing at `StatementEndpoints.GetFinancialReportAsync` do not exist until Task 8, and CS1574 is a build error. **Write them as `<c>GetFinancialReportAsync</c>` in this task and convert them to crefs in Task 8, Step 6.** That conversion is a named step there; do not skip it.

- [ ] **Step 2: Run the suite to see what the widened enum broke**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS. Nothing asserts on the member count. If `EndpointCoverageTests` fails, read the failure before changing anything — it drives each periodic method once per enum value, and six values instead of two should still deduplicate to the same paths.

- [ ] **Step 3: Write the failing tests for the default limit and the four new periods**

In `tests/FmpDotNet.Tests/StatementEndpointsTests.cs`, **replace** the last assertion of `Each_of_the_seven_hits_its_own_path_with_the_shared_query`:

```csharp
        Assert.DoesNotContain("limit=", uri.Query);    // omitted rather than guessed at when the caller gives none
```

with:

```csharp
        // NOT omitted. FMP's undocumented default is 5, so sending nothing returned 5 rows of a 41-row history
        // — measured 2026-08-27 on all seven of these paths. See StatementEndpoints.FullHistoryLimit.
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
```

This is an inverted assertion, not a weakened one: it asserts the opposite of what it did, because the behaviour it described was a defect. The old line is what let the defect ship.

Then append these three tests to the class:

```csharp
    [Theory]
    [InlineData(FiscalPeriod.Annual, "period=annual")]
    [InlineData(FiscalPeriod.Quarter, "period=quarter")]
    [InlineData(FiscalPeriod.Q1, "period=Q1")]
    [InlineData(FiscalPeriod.Q2, "period=Q2")]
    [InlineData(FiscalPeriod.Q3, "period=Q3")]
    [InlineData(FiscalPeriod.Q4, "period=Q4")]
    public async Task All_six_period_values_reach_the_wire(FiscalPeriod period, string expected)
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementAsync("AAPL", period);

        Assert.Contains(expected, handler.Requests.Single().Query);
    }

    [Fact]
    public void An_undeclared_period_throws_rather_than_reaching_the_wire()
    {
        // The throw is the point. An unrecognised period is silently read as annual by FMP (measured 2026-08-27,
        // `period=bogus` answered FY rows at HTTP 200), so a value that got past this would produce a well-formed
        // answer to a question nobody asked.
        Assert.Throws<ArgumentOutOfRangeException>(() => ((FiscalPeriod)99).ToQueryValue());
    }

    [Fact]
    public void The_two_original_period_ordinals_did_not_move()
    {
        // Q1-Q4 were appended, not inserted. A caller who persisted the underlying int keeps reading what they
        // stored — which is the whole reason the enum was widened at the end rather than in fiscal order.
        Assert.Equal(0, (int)FiscalPeriod.Annual);
        Assert.Equal(1, (int)FiscalPeriod.Quarter);
    }
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementEndpointsTests"`
Expected: FAIL — seven failures from the theory (`limit=100000` not in the query) and none from the period tests, which Step 1 already satisfied.

- [ ] **Step 5: Add `FullHistoryLimit` and fix `Periodic()`**

In `src/FmpDotNet/Endpoints/StatementEndpoints.cs`, add the constant immediately above the private `Periodic` helper:

```csharp
    /// <summary>The <c>limit</c> the SDK sends when the caller asks for no limit, and the reason it sends one.
    ///
    /// <para><b>Without it FMP returns five rows.</b> Measured 2026-08-27, every per-symbol paged path in this
    /// group has an undocumented default of 5: <c>stable/income-statement</c> for AAPL answered 5 rows of a
    /// 41-row annual history, <c>cash-flow-statement</c> 5 of 37, and so on across all seven of the paths this
    /// SDK shipped before that measurement. A well-formed HTTP 200 array of five rows is indistinguishable from a
    /// complete one, so a caller asking for a company's history got 12% of it and nothing said so.</para>
    ///
    /// <para>100,000 rather than the deepest history found is headroom rather than a guess. The deepest series
    /// measured was <c>income-statement-ttm</c> at 164 rows back to 1985-09-30, and the ceiling was probed:
    /// <c>limit=1000</c>, <c>limit=10000</c> and <c>limit=100000</c> all returned the same true total, so there is
    /// no server-side cap between them and asking for more costs nothing. The precedent is
    /// <see cref="DirectoryEndpoints.SymbolChangeRequestLimit"/>, which exists for exactly this failure.</para>
    ///
    /// <para><b>One endpoint in this group caps below any limit you send.</b> <c>owner-earnings</c> stops at 50
    /// rows regardless — see <see cref="MaxOwnerEarningsRows"/>.</para></summary>
    public const int FullHistoryLimit = 100_000;
```

The `<see cref="MaxOwnerEarningsRows"/>` does not exist until Task 6 and CS1574 is a build error. **Write it as `<c>MaxOwnerEarningsRows</c>` here and convert it to a cref in Task 6, Step 5.**

Then change the last line of `Periodic()`:

```csharp
            .With("limit", limit);
```

to:

```csharp
            // `limit ?? FullHistoryLimit`, not `limit` — a null limit means "all of it", and FMP reads a missing
            // limit as 5. See FullHistoryLimit.
            .With("limit", limit ?? FullHistoryLimit);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS, whole suite.

- [ ] **Step 7: Document `limit` on the seven shipped methods**

None of the seven has a `<param>` tag for `limit` at all. Add this pair to each of `GetIncomeStatementAsync`, `GetBalanceSheetAsync`, `GetCashFlowAsync`, `GetRatiosAsync`, `GetKeyMetricsAsync`, `GetFinancialGrowthAsync` and `GetEnterpriseValuesAsync`, above the existing `<exception>` tag:

```csharp
    /// <param name="period">Which series to ask for. All six values work on this path, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/> rather than omitting the parameter, because FMP
    /// reads an omitted limit as 5. See that constant.</param>
```

Add a `<param name="symbol">` and `<param name="ct">` only if the method already documents its other parameters; it does not, and adding two more per method is scope this task does not own. `period` and `limit` are here because both changed behaviour in this task.

- [ ] **Step 8: Mutation-check the default limit**

Revert `Periodic()`'s last line to `.With("limit", limit)`, run
`dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementEndpointsTests"`, and confirm the theory fails with seven failures. Restore. Record the result in the task report.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/FiscalPeriod.cs src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/StatementEndpointsTests.cs
git commit -m "fix: send an explicit full-history limit, and widen FiscalPeriod to six values (#28)"
```

---

### Task 2: The three TTM statements

Three paths, zero new models. Also builds the reflection helper every later binding test uses, so it comes before the model-heavy tasks.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +3 methods, +1 private helper
- Create: `tests/FmpDotNet.Tests/Binding.cs`
- Create: `tests/FmpDotNet.Tests/StatementTtmTests.cs`

**Interfaces:**
- Consumes: `StatementEndpoints.FullHistoryLimit` (Task 1), `IncomeStatement`, `BalanceSheetStatement`, `CashFlowStatement` and their `FmpJsonContext.Default.List*` entries (all existing).
- Produces: `private static FmpRequest StatementEndpoints.Rolling(string path, string symbol, int? limit)`, used again by Task 6. `Binding.Unbound<T>(T row)` and `Binding.Fixture(string name)`, used by Tasks 3, 5, 6, 8 and 9.

- [ ] **Step 1: Write the binding helper**

Create `tests/FmpDotNet.Tests/Binding.cs`. `Unbound` is the engine of every "the attributes are still there" test in this slice: it reports which wire-bound properties came back empty, so a test can assert the whole record populated rather than spot-checking two fields.

```csharp
using System.Reflection;
using System.Text.Json.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Shared helpers for the tests that prove a captured response still binds.
///
/// <para><see cref="Unbound{T}"/> exists because the failure this slice is guarding against is silent. Five of the
/// models reused here were built for CSV and carry no <c>[JsonPropertyName]</c> attributes; without them JSON
/// binding falls back to the C# property name, which deliberately drops FMP's <c>TTM</c> suffix. Nothing throws —
/// <c>symbol</c> populates and 61 metrics land null. A test that spot-checked two fields could pass with the
/// other 59 empty, so these tests assert the whole record.</para></summary>
internal static class Binding
{
    /// <summary>A captured response, read from the test assembly's output directory.</summary>
    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>The names of every wire-bound property on <paramref name="row"/> that came back with nothing in
    /// it — null, blank, or an empty dictionary.
    ///
    /// <para>Only properties carrying <c>[JsonPropertyName]</c> are considered, which is what makes this precise:
    /// a computed or <c>[JsonIgnore]</c>d property is not something FMP sends and has no business failing a
    /// binding test. Blank counts as unbound because this SDK spells a missing string as <c>""</c>.</para></summary>
    public static IReadOnlyList<string> Unbound<T>(T row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [.. typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
            .Where(p => p.GetValue(row) switch
            {
                null => true,
                string text => text.Trim().Length == 0,
                System.Collections.IEnumerable items => !items.GetEnumerator().MoveNext(),
                _ => false,
            })
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/StatementTtmTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The three rolling-twelve-month statements, which reuse the base statement models exactly.</summary>
public class StatementTtmTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    public static TheoryData<string, Func<StatementEndpoints, Task>> Calls => new()
    {
        { "stable/income-statement-ttm", e => e.GetIncomeStatementTtmAsync("AAPL") },
        { "stable/balance-sheet-statement-ttm", e => e.GetBalanceSheetTtmAsync("AAPL") },
        { "stable/cash-flow-statement-ttm", e => e.GetCashFlowTtmAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Each_ttm_path_asks_for_the_whole_history_and_sends_no_period(
        string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
        // Measured 2026-08-27: these three accept `period` and ignore it — they are a rolling series, always
        // newest-first from the latest quarter. Sending a parameter the endpoint discards is not free.
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task An_income_statement_ttm_row_binds_every_field_the_base_model_declares()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementTtmAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal("AAPL", row.Symbol);
    }

    [Fact]
    public async Task A_cash_flow_ttm_row_binds_every_field_the_base_model_declares()
    {
        var (endpoints, _) = Build(Binding.Fixture("cash-flow-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetCashFlowTtmAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task The_balance_sheet_ttm_row_is_missing_exactly_one_field_and_it_is_the_measured_one()
    {
        // Measured 2026-08-27 across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B, KO, GE and MSFT: the TTM row carries
        // 60 keys and never `capitalLeaseObligationsNonCurrent`, while the plain balance sheet carries it for all
        // ten. That is structural, not a sparse filer — so it binds as null on every TTM row forever, and a
        // caller reading it off one is reading an absence rather than a zero.
        var (endpoints, _) = Build(Binding.Fixture("balance-sheet-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetBalanceSheetTtmAsync("AAPL"));

        Assert.Equal(["CapitalLeaseObligationsNonCurrent"], Binding.Unbound(row));
    }

    [Fact]
    public async Task The_field_the_ttm_row_omits_is_present_on_the_plain_balance_sheet()
    {
        // The other half of the claim above. Without this, "the TTM row omits it" could equally mean "the model
        // never binds it", and the two are not the same defect.
        var (endpoints, _) = Build(Binding.Fixture("balance-sheet-statement.AAPL.json"));

        var row = Assert.Single(await endpoints.GetBalanceSheetAsync("AAPL", limit: 1));

        Assert.NotNull(row.CapitalLeaseObligationsNonCurrent);
    }

    [Fact]
    public async Task A_limit_is_passed_through_when_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementTtmAsync("AAPL", limit: 8);

        Assert.Contains("limit=8", handler.Requests.Single().Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_rejected_before_a_request_goes_out(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetIncomeStatementTtmAsync(symbol));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_zero_limit_is_rejected_before_a_request_goes_out()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIncomeStatementTtmAsync("AAPL", limit: 0));

        Assert.Empty(handler.Requests);
    }
}
```

`balance-sheet-statement.AAPL.json` is an existing fixture from the first coverage slice — it is the plain balance sheet, not the TTM one. Both exist; read the filenames carefully.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementTtmTests"`
Expected: FAIL to compile — `StatementEndpoints` has no `GetIncomeStatementTtmAsync`.

- [ ] **Step 4: Add the helper and the three methods**

In `src/FmpDotNet/Endpoints/StatementEndpoints.cs`, add the helper next to `Periodic()`:

```csharp
    /// <summary>The query shape for the per-symbol paths that take no <c>period</c>.
    ///
    /// <para>Separate from <see cref="Periodic"/> rather than passing a nullable period through it, because the
    /// difference is a fact about the endpoints and not a formatting choice: measured 2026-08-27, these paths
    /// accept <c>period</c> and discard it. A helper that could emit it would leave the decision to a call
    /// site.</para></summary>
    private static FmpRequest Rolling(string path, string symbol, int? limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("limit", limit ?? FullHistoryLimit);
    }
```

Then the three methods, after `GetEnterpriseValuesAsync` and before `GetScoresAsync`:

```csharp
    /// <summary>Rolling-twelve-month income statements for one symbol, newest first. From
    /// <c>stable/income-statement-ttm</c>.
    ///
    /// <para><b>The same 39 fields as <see cref="GetIncomeStatementAsync"/>, on a different clock.</b> The wire
    /// field set was compared key by key on 2026-08-27 and is identical, so this reuses
    /// <see cref="IncomeStatement"/> rather than declaring a near-duplicate record. Each row covers the twelve
    /// months ending at the <c>date</c> on it, so consecutive rows OVERLAP by nine months — summing them
    /// quadruples the revenue. The plain statement is the one to sum.</para>
    ///
    /// <para><b>No <c>period</c> parameter, deliberately.</b> The endpoint accepts one and ignores it, measured
    /// 2026-08-27: the answer is always quarterly-stepped and newest-first from the latest quarter. This is the
    /// deepest series in the group — AAPL returned 164 rows back to 1985-09-30.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it. Class shares need the hyphenated form (<c>BRK-B</c>).</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> — the default — means the whole
    /// history: the SDK sends <see cref="FullHistoryLimit"/>, because FMP reads an omitted limit as 5.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol — which answers <c>[]</c> at HTTP 200 rather
    /// than a 404. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IncomeStatement>> GetIncomeStatementTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/income-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListIncomeStatement, ct);

    /// <summary>Rolling-twelve-month balance sheets for one symbol, newest first. From
    /// <c>stable/balance-sheet-statement-ttm</c>.
    ///
    /// <para><b>Sixty of <see cref="BalanceSheetStatement"/>'s 61 fields.</b>
    /// <see cref="BalanceSheetStatement.CapitalLeaseObligationsNonCurrent"/> is <b>never</b> sent on this path and
    /// therefore always binds null — measured 2026-08-27 across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B, KO, GE and
    /// MSFT, where the TTM row carried exactly 60 keys every time and the plain balance sheet carried the 61st
    /// for all ten. It is structural, not a sparse filer, and null here is an absence rather than a zero.</para>
    ///
    /// <para>A rolling balance sheet is a stranger object than a rolling income statement — a balance sheet is
    /// already a point in time — so read these as "the balance sheet as at the end of each trailing twelve-month
    /// window", which is the quarter end, not an average over the year.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BalanceSheetStatement>> GetBalanceSheetTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/balance-sheet-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListBalanceSheetStatement, ct);

    /// <summary>Rolling-twelve-month cash flow statements for one symbol, newest first. From
    /// <c>stable/cash-flow-statement-ttm</c>.
    ///
    /// <para>The same 47 fields as <see cref="GetCashFlowAsync"/>, compared key by key on 2026-08-27 and
    /// identical, so this reuses <see cref="CashFlowStatement"/>. Consecutive rows overlap by nine months; do not
    /// sum them.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CashFlowStatement>> GetCashFlowTtmAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/cash-flow-statement-ttm", symbol, limit),
            FmpJsonContext.Default.ListCashFlowStatement, ct);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS, whole suite. `EndpointCoverageTests` will now fail on the README table — that is expected and Task 10 regenerates it. If it does fail, note it and continue; if it does NOT fail, say so in the report, because it means discovery missed the three new methods.

- [ ] **Step 6: Mutation-check the one-missing-field claim**

Add `"capitalLeaseObligationsNonCurrent": 1` to the row in `tests/FmpDotNet.Tests/Fixtures/balance-sheet-statement-ttm.AAPL.json`, run the TTM filter, and confirm `The_balance_sheet_ttm_row_is_missing_exactly_one_field_and_it_is_the_measured_one` fails. **Restore the fixture with `git checkout`** — it is captured evidence and must not be left edited.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/Binding.cs tests/FmpDotNet.Tests/StatementTtmTests.cs
git commit -m "feat: add the three rolling-twelve-month statements (#28)"
```

---

### Task 3: The five CSV-built models learn to bind JSON

The highest-value task in the slice, because the failure it prevents is silent. 237 attributes across five files, every one of them mechanical — but a wrong one produces a null field, not an error.

**Files:**
- Modify: `src/FmpDotNet/Models/RatiosTtm.cs` (62 properties)
- Modify: `src/FmpDotNet/Models/KeyMetricsTtm.cs` (43)
- Modify: `src/FmpDotNet/Models/IncomeStatementGrowth.cs` (34)
- Modify: `src/FmpDotNet/Models/BalanceSheetGrowth.cs` (56)
- Modify: `src/FmpDotNet/Models/CashFlowGrowth.cs` (42)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +5 entries
- Create: `tests/FmpDotNet.Tests/StatementReuseBindingTests.cs`

**Interfaces:**
- Consumes: `Binding.Unbound<T>` and `Binding.Fixture` (Task 2).
- Produces: `FmpJsonContext.Default.ListRatiosTtm`, `.ListKeyMetricsTtm`, `.ListIncomeStatementGrowth`, `.ListBalanceSheetGrowth`, `.ListCashFlowGrowth` — Task 4 calls all five.

**The rule, and why it is exact rather than a judgement call.** Each of these five records already
contains its own wire-name table: the `FromCsv` method at the bottom of the file assigns every
property from a named CSV column. Measured 2026-08-27, the JSON and CSV forms of these five paths
carry **exactly the same field names**, FMP's typos and `TTM` suffixes included. So:

> **The `[JsonPropertyName]` for a property is the string literal already passed to
> `row.GetX("…")` for that property inside the same file's `FromCsv`.**

That mapping was verified total before this plan was written: 62/43/34/56/42 properties against
62/43/34/56/42 `FromCsv` assignments, no property unassigned and no assignment orphaned. There is
nothing to decide and nothing to look up elsewhere — do not consult FMP's documentation, and do not
"correct" a spelling. `growthNetCashProvidedByOperatingActivites` is missing its second `i` on the
wire and the attribute must be missing it too.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/StatementReuseBindingTests.cs`. These bind through `FmpJsonContext` directly, with no endpoint involved, because what is under test is the model rather than the request:

```csharp
using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Proves the five CSV-built models bind the JSON forms of their endpoints.
///
/// <para><b>These are the tests that fail when someone deletes a <c>[JsonPropertyName]</c>.</b> The five records
/// here were written for the <c>*-bulk</c> CSV surface, which maps them by an explicit wire-name lookup, and
/// their C# property names deliberately drop FMP's <c>TTM</c> suffix — <c>GrossProfitMargin</c> for
/// <c>grossProfitMarginTTM</c>. The serializer context sets <c>PropertyNameCaseInsensitive</c> and no naming
/// policy, so without the attributes JSON binding falls back to the property name, misses, and leaves the field
/// null. Nothing throws. <c>symbol</c> populates and 61 metrics do not, and every assertion that spot-checked one
/// field would still pass.</para>
///
/// <para>So each test asserts the WHOLE record populated, against a row captured live on 2026-08-27 in which
/// every field carried a value — measured, not assumed: all five captures were checked for nulls and had
/// none.</para></summary>
public class StatementReuseBindingTests
{
    [Fact]
    public void Ratios_ttm_binds_all_sixty_two_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("ratios-ttm.AAPL.json"), FmpJsonContext.Default.ListRatiosTtm)![0];

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal("AAPL", row.Symbol);
        // The suffixed name specifically. `GrossProfitMargin` would bind from a hypothetical `grossProfitMargin`
        // by case-insensitive fallback; it is the TTM suffix that needs the attribute.
        Assert.NotNull(row.GrossProfitMargin);
    }

    [Fact]
    public void Key_metrics_ttm_binds_all_forty_three_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("key-metrics-ttm.AAPL.json"), FmpJsonContext.Default.ListKeyMetricsTtm)![0];

        Assert.Empty(Binding.Unbound(row));
        // `marketCap` carries NO suffix while `enterpriseValueTTM` does, on the same response. That inconsistency
        // is FMP's and is why the attribute values are copied from FromCsv rather than derived by a rule.
        Assert.NotNull(row.MarketCap);
        Assert.NotNull(row.EnterpriseValue);
    }

    [Fact]
    public void Income_statement_growth_binds_all_thirty_four_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("income-statement-growth.AAPL.json"), FmpJsonContext.Default.ListIncomeStatementGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal(2025, row.FiscalYear);          // arrives as the STRING "2025"; see the fiscal-year test below
        Assert.Equal("FY", row.Period);
        Assert.Equal(new NodaTime.LocalDate(2025, 9, 27), row.Date);
    }

    [Fact]
    public void Balance_sheet_growth_binds_all_fifty_six_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("balance-sheet-statement-growth.AAPL.json"), FmpJsonContext.Default.ListBalanceSheetGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public void Cash_flow_growth_binds_all_forty_two_fields_including_fmps_typo()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("cash-flow-statement-growth.AAPL.json"), FmpJsonContext.Default.ListCashFlowGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
        // FMP spells this `growthNetCashProvidedByOperatingActivites` — one `i` short of `Activities`. The C#
        // name is corrected and the attribute is not, which is the whole reason the attribute exists.
        Assert.NotNull(row.GrowthNetCashProvidedByOperatingActivities);
    }

    [Fact]
    public void A_fiscal_year_binds_from_both_wire_forms()
    {
        // `fiscalYear` is an int on six of the nineteen paths and a string on seven, measured 2026-08-27. One
        // `int?` property reads both ONLY because FmpJsonContext sets JsonNumberHandling.AllowReadingFromString,
        // which makes that option load-bearing rather than incidental. This is the test that says so.
        const string quoted = """[{"symbol":"AAPL","fiscalYear":"2025"}]""";
        const string bare = """[{"symbol":"AAPL","fiscalYear":2025}]""";

        Assert.Equal(2025,
            JsonSerializer.Deserialize(quoted, FmpJsonContext.Default.ListIncomeStatementGrowth)![0].FiscalYear);
        Assert.Equal(2025,
            JsonSerializer.Deserialize(bare, FmpJsonContext.Default.ListIncomeStatementGrowth)![0].FiscalYear);
    }
}
```

Check `GrowthNetCashProvidedByOperatingActivities` against the actual property name in `CashFlowGrowth.cs` before relying on it — the C# spelling is corrected there, but confirm the exact casing rather than assuming.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementReuseBindingTests"`
Expected: FAIL to compile — `FmpJsonContext.Default` has no `ListRatiosTtm`.

- [ ] **Step 3: Add the five context registrations**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add after the existing statement entries:

```csharp
// The five below were built for the *-bulk CSV surface and are registered here because their per-symbol JSON
// twins carry the identical field set, measured 2026-08-27. They bind by [JsonPropertyName] rather than by
// property name — see StatementReuseBindingTests for why that is not optional.
[JsonSerializable(typeof(List<RatiosTtm>))]
[JsonSerializable(typeof(List<KeyMetricsTtm>))]
[JsonSerializable(typeof(List<IncomeStatementGrowth>))]
[JsonSerializable(typeof(List<BalanceSheetGrowth>))]
[JsonSerializable(typeof(List<CashFlowGrowth>))]
```

- [ ] **Step 4: Add the attributes, one file at a time**

For each of the five files, in this order — `KeyMetricsTtm.cs` (43), `IncomeStatementGrowth.cs` (34), `CashFlowGrowth.cs` (42), `BalanceSheetGrowth.cs` (56), `RatiosTtm.cs` (62):

1. Add `using System.Text.Json.Serialization;` to the top of the file.
2. For every `public … X { get; init; }`, prefix the declaration with `[JsonPropertyName("<the string from FromCsv for X>")]` on the same line, matching the existing house style in `FinancialScores.cs`:

```csharp
    /// <summary>Trailing-twelve-month <c>grossProfitMargin</c>.</summary>
    [JsonPropertyName("grossProfitMarginTTM")] public decimal? GrossProfitMargin { get; init; }
```

3. The three growth models additionally carry a `LocalDate? Date`. It needs a converter as well, on its own lines, matching `IncomeStatement.cs`:

```csharp
    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
```

`NullableLocalDateJsonConverter` is in `FmpDotNet.Serialization`, which these files already import for `CsvRow`.

4. Leave `FromCsv` untouched. It is the source the attributes were copied from and it still serves the bulk endpoints.

**Do not do all five in one edit and then build.** Build after each file — a compile error naming one file is worth more than five files of unknown state.

- [ ] **Step 5: Verify the mapping mechanically, not by eye**

237 attributes is past what review by reading catches. Run this from the repo root; it re-derives the mapping from `FromCsv` and reports every disagreement:

```bash
python3 - <<'PY'
import re, os
M = "src/FmpDotNet/Models"
bad = 0
for model in ["RatiosTtm", "KeyMetricsTtm", "IncomeStatementGrowth", "BalanceSheetGrowth", "CashFlowGrowth"]:
    src = open(os.path.join(M, model + ".cs")).read()
    csv = dict(re.findall(r'(\w+)\s*=\s*row\.Get\w+\("([^"]+)"\)', src[src.index("FromCsv"):]))
    json_attrs = dict(re.findall(r'JsonPropertyName\("([^"]+)"\)\]\s*(?:\n\s*\[[^\]]*\]\s*)*public\s+\S+\s+(\w+)', src))
    json_attrs = {v: k for k, v in json_attrs.items()}
    for prop, wire in sorted(csv.items()):
        if json_attrs.get(prop) != wire:
            print(f"  {model}.{prop}: FromCsv={wire!r} attribute={json_attrs.get(prop)!r}")
            bad += 1
    print(f"{model}: {len(csv)} properties, {len(json_attrs)} attributes")
print("MISMATCHES:", bad)
PY
```

Expected: `MISMATCHES: 0`, and the property and attribute counts equal on every line (62, 43, 34, 56, 42). Anything else is a defect — fix it before moving on, and do not adjust the script to agree with the code.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementReuseBindingTests"`
Expected: PASS, 6 tests.

Then the whole suite: `dotnet test`. The bulk CSV tests (`BulkTtmTests`, `BulkStatementFamilyTests`) exercise the same five records through `FromCsv` and must still pass — if one broke, an attribute edit damaged a `FromCsv` line.

- [ ] **Step 7: Mutation-check the trap**

Delete the `[JsonPropertyName("grossProfitMarginTTM")]` attribute from `RatiosTtm.GrossProfitMargin`, run
`dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementReuseBindingTests"`, and confirm `Ratios_ttm_binds_all_sixty_two_fields` fails naming `GrossProfitMargin`. This is the single most important mutation check in the slice: it proves the suite notices the silent failure. Restore, and record the result in the task report.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models src/FmpDotNet/Serialization/FmpJsonContext.cs tests/FmpDotNet.Tests/StatementReuseBindingTests.cs
git commit -m "feat: teach the five CSV-built statement models to bind JSON (#28)"
```

---

### Task 4: The three growth paths and the two TTM metric snapshots

Five methods over the five models Task 3 just taught to bind. Two shapes: three list-returning periodic paths, and two that answer a single row.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +5 methods
- Modify: `tests/FmpDotNet.Tests/StatementReuseBindingTests.cs` — +the endpoint-level tests

**Interfaces:**
- Consumes: `Periodic()` (Task 1), the five `FmpJsonContext.Default.List*` entries (Task 3), `Binding.Fixture` (Task 2).
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/StatementReuseBindingTests.cs`. Add the usings and the `Build` helper at the top of the class — the same shape `StatementTtmTests` uses:

```csharp
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    public static TheoryData<string, Func<StatementEndpoints, Task>> GrowthCalls => new()
    {
        { "stable/income-statement-growth", e => e.GetIncomeStatementGrowthAsync("AAPL") },
        { "stable/balance-sheet-statement-growth", e => e.GetBalanceSheetGrowthAsync("AAPL") },
        { "stable/cash-flow-statement-growth", e => e.GetCashFlowGrowthAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(GrowthCalls))]
    public async Task Each_growth_path_goes_through_the_shared_periodic_shape(
        string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("period=annual", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
    }

    [Fact]
    public async Task A_growth_row_arrives_through_the_endpoint_fully_bound()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-growth.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementGrowthAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
    }

    [Theory]
    [InlineData("stable/key-metrics-ttm")]
    [InlineData("stable/ratios-ttm")]
    public async Task The_ttm_snapshots_send_neither_period_nor_limit(string path)
    {
        // Measured 2026-08-27: each answers a single row and ignores both parameters. GetScoresAsync set the
        // precedent — an endpoint that discards a parameter should not be sent one.
        var (endpoints, handler) = Build();

        if (path.EndsWith("key-metrics-ttm", StringComparison.Ordinal))
            await endpoints.GetKeyMetricsTtmAsync("AAPL");
        else
            await endpoints.GetRatiosTtmAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Fact]
    public async Task A_ratios_ttm_snapshot_comes_back_as_one_record_not_a_list()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratios-ttm.AAPL.json"));

        var row = await endpoints.GetRatiosTtmAsync("AAPL");

        Assert.NotNull(row);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task A_key_metrics_ttm_snapshot_comes_back_as_one_record_not_a_list()
    {
        var (endpoints, _) = Build(Binding.Fixture("key-metrics-ttm.AAPL.json"));

        var row = await endpoints.GetKeyMetricsTtmAsync("AAPL");

        Assert.NotNull(row);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task An_unknown_symbol_is_null_rather_than_an_exception_on_the_ttm_snapshots()
    {
        // FMP answers `[]` at HTTP 200 for an unknown symbol on all eleven list-shaped paths in this group,
        // measured 2026-08-27 — "not found" is a shape here, not a status code. Same rule as GetScoresAsync.
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetRatiosTtmAsync("NOSUCHSYM"));
    }
```

The file needs `using Microsoft.Extensions.Options;` and `using FmpDotNet.Endpoints;` added for the `Build` helper.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementReuseBindingTests"`
Expected: FAIL to compile — no `GetIncomeStatementGrowthAsync`.

- [ ] **Step 3: Add the three growth methods**

In `src/FmpDotNet/Endpoints/StatementEndpoints.cs`, after the TTM statements from Task 2. Each goes through the existing `Periodic()` — the shared shape is the point, so do not hand-roll the request:

```csharp
    /// <summary>Period-over-period growth of one income statement, newest first. From
    /// <c>stable/income-statement-growth</c>.
    ///
    /// <para><b>Not the same fields as <see cref="GetFinancialGrowthAsync"/>.</b> That path answers FMP's own
    /// summary growth set; this one answers a growth rate for every line of the income statement, 34 fields
    /// whose names are the upstream's own — typos included. See <see cref="IncomeStatementGrowth"/>.</para>
    ///
    /// <para>Every figure is a <b>fraction, not a percentage</b>: 0.12 is twelve percent. FMP sends 0 where the
    /// prior period was zero or absent, so a zero cannot be told apart from "no prior period to grow
    /// from".</para>
    ///
    /// <para>The model is shared with <c>stable/income-statement-growth-bulk</c>: the JSON and CSV field sets
    /// were compared name by name on 2026-08-27 and are identical.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work, including
    /// <see cref="FiscalPeriod.Q1"/>–<see cref="FiscalPeriod.Q4"/> as cross-year filters.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IncomeStatementGrowth>> GetIncomeStatementGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListIncomeStatementGrowth, ct);

    /// <summary>Period-over-period growth of one balance sheet, newest first — 56 fields. From
    /// <c>stable/balance-sheet-statement-growth</c>.
    ///
    /// <para>Fractions, not percentages, with the same zero-means-two-things caveat as
    /// <see cref="GetIncomeStatementGrowthAsync"/>. Model shared with the bulk CSV form; field sets compared name
    /// by name on 2026-08-27 and identical.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<BalanceSheetGrowth>> GetBalanceSheetGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/balance-sheet-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListBalanceSheetGrowth, ct);

    /// <summary>Period-over-period growth of one cash flow statement, newest first — 42 fields. From
    /// <c>stable/cash-flow-statement-growth</c>.
    ///
    /// <para>Fractions, not percentages. Model shared with the bulk CSV form; field sets compared name by name on
    /// 2026-08-27 and identical, <b>including FMP's spelling of
    /// <c>growthNetCashProvidedByOperatingActivites</c></b> — one letter short of <c>Activities</c>. The C#
    /// property corrects it; the wire name does not.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CashFlowGrowth>> GetCashFlowGrowthAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/cash-flow-statement-growth", symbol, period, limit),
            FmpJsonContext.Default.ListCashFlowGrowth, ct);
```

- [ ] **Step 4: Add the two snapshot methods**

Same file, after the growth methods. Both follow `GetScoresAsync`'s shape — single nullable record, symbol only:

```csharp
    /// <summary>Trailing-twelve-month key metrics for one symbol, or null when FMP has none. From
    /// <c>stable/key-metrics-ttm</c>.
    ///
    /// <para><b>One row, and <paramref name="symbol"/> is the only parameter.</b> Measured 2026-08-27, this path
    /// answers a single-element array and ignores both <c>period</c> and <c>limit</c>, so neither is sent — the
    /// same reasoning <see cref="GetScoresAsync"/> follows.</para>
    ///
    /// <para><b>There is no date on this response of any kind.</b> It describes the twelve months ending whenever
    /// FMP last recomputed it, which the payload does not say. Two calls days apart are not comparable as a time
    /// series and nothing in the data will tell you they differ — whoever stores this has to stamp it at fetch
    /// time. See <see cref="KeyMetricsTtm"/>.</para>
    ///
    /// <para>Null means FMP answered <c>[]</c> at HTTP 200, which covers both "no such symbol" and "not
    /// applicable to this security" and cannot distinguish them.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The snapshot, or <see langword="null"/> when FMP answered an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<KeyMetricsTtm?> GetKeyMetricsTtmAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/key-metrics-ttm").With("symbol", symbol),
            FmpJsonContext.Default.ListKeyMetricsTtm, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Trailing-twelve-month financial ratios for one symbol, or null when FMP has none. From
    /// <c>stable/ratios-ttm</c>.
    ///
    /// <para>The twin of <see cref="GetKeyMetricsTtmAsync"/> and carries the same three caveats: one row, no
    /// <c>period</c> or <c>limit</c> (both accepted and ignored, measured 2026-08-27), and <b>no date field</b>,
    /// so two calls days apart are not a series. See <see cref="RatiosTtm"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The snapshot, or <see langword="null"/> when FMP answered an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<RatiosTtm?> GetRatiosTtmAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ratios-ttm").With("symbol", symbol),
            FmpJsonContext.Default.ListRatiosTtm, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StatementReuseBindingTests"`
Expected: PASS.

Then `dotnet test tests/FmpDotNet.Tests`. Only `EndpointCoverageTests`'s README assertion should fail; Task 10 fixes it.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/StatementReuseBindingTests.cs
git commit -m "feat: add the three growth paths and the two TTM metric snapshots (#28)"
```

---

### Task 5: As-reported and segmentation — one envelope, two value domains

Six paths, two models. The four `*-as-reported` paths and the two `revenue-*-segmentation` paths answer the identical five-field envelope around an open dictionary, and the temptation is one type for all six. They get two, and the reason is measured rather than stylistic.

**Files:**
- Create: `src/FmpDotNet/Models/AsReportedStatements.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +2 entries
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +6 methods, +1 private helper
- Create: `tests/FmpDotNet.Tests/AsReportedTests.cs`

**Interfaces:**
- Consumes: `Periodic()` (Task 1), `Binding.Fixture`/`Binding.Unbound` (Task 2), `NullableLocalDateJsonConverter` (existing).
- Produces: `AsReportedStatement`, `RevenueSegmentation`, and `private static FmpRequest StatementEndpoints.Envelope(string path, string symbol, FiscalPeriod period)` — nothing later depends on them.

**Verified before planning, so do not re-litigate:** the source generator accepts both
`IReadOnlyDictionary<string, JsonElement>` and `IReadOnlyDictionary<string, decimal>` under
`IsAotCompatible` with no SYSLIB diagnostic; segment keys containing spaces and commas
(`"Wearables, Home and Accessories"`) bind correctly; `1e-05` reads into `decimal` as `0.00001`; and
a string value in the `decimal` dictionary throws `JsonException`, which is the intended outcome.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/AsReportedTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The six paths that answer one envelope around an open dictionary — and the reason two of them get a
/// different dictionary from the other four.</summary>
public class AsReportedTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    public static TheoryData<string, Func<StatementEndpoints, Task>> AsReportedCalls => new()
    {
        { "stable/income-statement-as-reported", e => e.GetIncomeStatementAsReportedAsync("AAPL") },
        { "stable/balance-sheet-statement-as-reported", e => e.GetBalanceSheetAsReportedAsync("AAPL") },
        { "stable/cash-flow-statement-as-reported", e => e.GetCashFlowAsReportedAsync("AAPL") },
        { "stable/financial-statement-full-as-reported", e => e.GetFullStatementAsReportedAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(AsReportedCalls))]
    public async Task Each_as_reported_path_goes_through_the_shared_periodic_shape(
        string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("period=annual", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
    }

    [Fact]
    public async Task An_as_reported_row_carries_its_envelope_and_its_xbrl_dictionary()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-as-reported.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(2025, row.FiscalYear);
        Assert.Equal("FY", row.Period);
        Assert.Equal("USD", row.ReportedCurrency);
        Assert.Equal(new NodaTime.LocalDate(2025, 9, 26), row.Date);
        // The keys are lowercased, concatenated XBRL tags — not the camelCase of the modelled statements.
        Assert.Equal(416161000000m, row.Data["revenuefromcontractwithcustomerexcludingassessedtax"].GetDecimal());
    }

    [Fact]
    public async Task An_as_reported_dictionary_holds_strings_and_floats_beside_its_numbers()
    {
        // This is why Data is JsonElement rather than decimal. Measured 2026-08-27, AAPL's FY2025
        // financial-statement-full-as-reported held 234 ints, 47 strings and 19 floats in one object, and its key
        // count swings 300 -> 923 between AAPL and JPM. A Dictionary<string, decimal> throws on the 47.
        var (endpoints, _) = Build(Binding.Fixture("financial-statement-full-as-reported.AAPL.mixed.json"));

        var row = Assert.Single(await endpoints.GetFullStatementAsReportedAsync("AAPL"));

        Assert.Equal(JsonValueKind.String, row.Data["documenttype"].ValueKind);
        Assert.Equal("10-K", row.Data["documenttype"].GetString());
        Assert.Equal(JsonValueKind.Number, row.Data["grossprofit"].ValueKind);
        Assert.Equal(0.00001m, row.Data["commonstockparorstatedvaluepershare"].GetDecimal());
        // Not every number here is money. `entityaddresspostalzipcode` is a POSTAL CODE that happens to be an
        // integer, which is the other half of why this dictionary is not typed as decimal.
        Assert.Equal(95014m, row.Data["entityaddresspostalzipcode"].GetDecimal());
    }

    [Theory]
    [InlineData("stable/revenue-product-segmentation")]
    [InlineData("stable/revenue-geographic-segmentation")]
    public async Task Segmentation_sends_no_limit_because_the_endpoint_ignores_it(string path)
    {
        // Measured 2026-08-27: both segmentation paths transfer the full set regardless of `limit`, the
        // behaviour already recorded for etf-list and its siblings.
        var (endpoints, handler) = Build();

        if (path.Contains("product", StringComparison.Ordinal))
            await endpoints.GetRevenueByProductAsync("AAPL");
        else
            await endpoints.GetRevenueByGeographyAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("period=annual", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Fact]
    public async Task Segmentation_does_not_send_the_structure_parameter_fmp_documents()
    {
        // Measured 2026-08-27 on AAPL and on JPM — a filer with genuinely nested segments — `structure=flat` and
        // `structure=hierarchical` returned payloads identical to sending nothing. A parameter that does nothing
        // still costs a caller the belief that it does something.
        var (endpoints, handler) = Build();

        await endpoints.GetRevenueByProductAsync("AAPL");

        Assert.DoesNotContain("structure", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_segmentation_row_reads_its_segments_as_numbers()
    {
        var (endpoints, _) = Build(Binding.Fixture("revenue-product-segmentation.AAPL.json"));

        var rows = await endpoints.GetRevenueByProductAsync("AAPL");

        var row = rows[0];
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(209586000000m, row.Data["iPhone"]);
        // Segment names are the company's own, so they carry spaces and commas — they are not identifiers.
        Assert.Equal(35686000000m, row.Data["Wearables, Home and Accessories"]);
    }

    [Fact]
    public void A_string_segment_value_throws_rather_than_binding_as_zero()
    {
        // Deliberate. Measured across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B and KO, both segmentation endpoints and
        // both cadences — every row, not a sample — the values were 3,201 ints and 36 floats and not one string.
        // A non-numeric segment revenue would be a defect worth hearing about, so the decimal dictionary is the
        // right type and this throw is the right outcome.
        const string body = """[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","data":{"Mac":"lots"}}]""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListRevenueSegmentation));
    }

    [Fact]
    public async Task An_empty_data_object_binds_to_an_empty_dictionary_not_null()
    {
        var (endpoints, _) = Build("""[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","data":{}}]""");

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.NotNull(row.Data);
        Assert.Empty(row.Data);
    }

    [Fact]
    public async Task An_absent_data_object_binds_to_an_empty_dictionary_not_null()
    {
        // The property initialiser has to survive a missing key, not just an empty one — a null here would make
        // every caller null-check a dictionary that is documented never to be null.
        var (endpoints, _) = Build("""[{"symbol":"AAPL","fiscalYear":2025,"period":"FY"}]""");

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.NotNull(row.Data);
        Assert.Empty(row.Data);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~AsReportedTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Write the two models**

Create `src/FmpDotNet/Models/AsReportedStatements.cs`. They share a file because they share an envelope and will change together:

```csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One filing's figures exactly as the issuer tagged them, from the four <c>*-as-reported</c> paths.
///
/// <para><b>This is not a statement model with a few extra fields — it is a different kind of object.</b> The
/// modelled statements (<see cref="IncomeStatement"/> and friends) are FMP's normalisation: a fixed field set
/// that means the same thing for every filer. This is the XBRL as filed, so the keys are the issuer's own tags
/// and the set changes per company and per year. Measured 2026-08-27, <c>income-statement-as-reported</c>
/// answered 24 keys for AAPL and 39 for JPM; <c>financial-statement-full-as-reported</c> answered 300 for AAPL
/// and 923 for JPM. Nothing is missing from the smaller one — the filers tagged different things.</para>
///
/// <para><b>Which is why <see cref="Data"/> is an open dictionary of <see cref="JsonElement"/> and not a record,
/// and not a dictionary of <see cref="decimal"/>.</b> No record can express a field set that varies by filer. And
/// the values are not all numbers: AAPL's FY2025 full statement held 234 integers, 47 strings and 19 floats in
/// one object. The strings are filing metadata — <c>documenttype: "10-K"</c>,
/// <c>currentfiscalyearenddate: "--09-27"</c> — and a <c>Dictionary&lt;string, decimal&gt;</c> throws on every
/// one of them, losing the whole response. Some of the integers are not money either:
/// <c>entityaddresspostalzipcode</c> is a postal code. <see cref="JsonElement"/> is honest about what arrived and
/// costs the caller one <c>GetDecimal()</c>.</para>
///
/// <para>Keys are lowercased, concatenated XBRL tags —
/// <c>revenuefromcontractwithcustomerexcludingassessedtax</c>, <c>costofgoodsandservicessold</c>. Measured over
/// AAPL, JPM, BRK-B and TSM: no null values, no keys colliding under case-insensitive comparison, no non-ASCII
/// keys, and the largest magnitude anywhere was 7.1e12, comfortably inside <see cref="decimal"/>.</para></summary>
public sealed record AsReportedStatement
{
    /// <summary>Ticker as FMP spells it, read from the response rather than echoed back from the argument.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON <b>integer</b> on these four paths and as a <b>string</b> on seven
    /// others in the same section; one <c>int?</c> reads both only because <c>FmpJsonContext</c> sets
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>–<c>Q4</c> for quarterly.
    /// FMP's RESPONSE vocabulary, which is not the request vocabulary <see cref="FiscalPeriod"/> sends for
    /// <see cref="FiscalPeriod.Annual"/>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the figures are reported in — not necessarily USD.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filing's tagged facts, keyed by XBRL tag. Never <see langword="null"/> — an absent or empty
    /// <c>data</c> object binds to an empty dictionary, so a caller does not null-check it.
    ///
    /// <para>Read a number with <c>Data["grossprofit"].GetDecimal()</c>, and check
    /// <see cref="JsonElement.ValueKind"/> first on any key you have not measured: see the type's summary for why
    /// a third of some payloads is not numeric.</para></summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, JsonElement> Data { get; init; } =
        ReadOnlyDictionary<string, JsonElement>.Empty;
}

/// <summary>One period's revenue split by product line or by geography, from the two
/// <c>revenue-*-segmentation</c> paths.
///
/// <para><b>The same five-field envelope as <see cref="AsReportedStatement"/>, and deliberately a different
/// type</b> — because <see cref="Data"/> here is <see cref="decimal"/> rather than <see cref="JsonElement"/>.
/// That is measured, not assumed: across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B and KO, both endpoints, both
/// cadences, <b>every row rather than a sample</b>, the values were 3,201 integers and 36 floats and not one
/// string. Segmentation is genuinely segment-name-to-number where as-reported is not, and sharing a field layout
/// is not a reason to share a type when one of the two has a proven value domain. If FMP ever sends a string
/// here the binding throws, which is the correct outcome — a non-numeric segment revenue is a defect worth
/// hearing about rather than silently reading as zero.</para>
///
/// <para><b>Keys are the company's own segment names</b>, so they carry spaces, ampersands and commas —
/// <c>"Wearables, Home and Accessories"</c>, <c>"Consumer &amp; Community Banking"</c> — and they change when the
/// company reorganises. They are labels, not identifiers, and nothing guarantees the same name across
/// years.</para>
///
/// <para>Measured 2026-08-27, the segment count ranges from 1 (O) to 6 (XOM) per period.</para></summary>
public sealed record RevenueSegmentation
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON integer on both segmentation paths.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c>, or <c>Q1</c>–<c>Q4</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the figures are reported in.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Period end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Revenue by segment name. Never <see langword="null"/>; empty when FMP sent no split.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, decimal> Data { get; init; } = ReadOnlyDictionary<string, decimal>.Empty;
}
```

- [ ] **Step 4: Register both in `FmpJsonContext`**

```csharp
[JsonSerializable(typeof(List<AsReportedStatement>))]
[JsonSerializable(typeof(List<RevenueSegmentation>))]
```

- [ ] **Step 5: Add the six methods**

In `StatementEndpoints.cs`. First the helper, beside `Rolling`:

```csharp
    /// <summary>The query shape for the paths that take a <c>period</c> and ignore <c>limit</c>.
    ///
    /// <para>Measured 2026-08-27, both segmentation paths transfer the full set whatever limit is sent, so no
    /// limit is offered rather than one that does nothing.</para></summary>
    private static FmpRequest Envelope(string path, string symbol, FiscalPeriod period)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("period", period.ToQueryValue());
    }
```

Then the four as-reported methods, which go through `Periodic()`. Write all four in full — they differ only in path and summary, and a reader arriving at one should not have to find another. Use this as the pattern:

```csharp
    /// <summary>One symbol's income statements exactly as filed, newest first. From
    /// <c>stable/income-statement-as-reported</c>.
    ///
    /// <para><b>The issuer's XBRL tags, not FMP's normalised fields.</b> Use this to see what a company actually
    /// reported; use <see cref="GetIncomeStatementAsync"/> to compare companies. The two do not have the same
    /// field names and are not meant to. See <see cref="AsReportedStatement"/> for why the payload is an open
    /// dictionary whose values are not all numbers.</para>
    ///
    /// <para>Measured 2026-08-27: 24 tagged facts for AAPL and 39 for JPM on the same path and the same
    /// cadence.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AsReportedStatement>> GetIncomeStatementAsReportedAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Periodic("stable/income-statement-as-reported", symbol, period, limit),
            FmpJsonContext.Default.ListAsReportedStatement, ct);
```

The other three follow with these paths, names and distinguishing summaries:

| method | path | what its summary must add |
|---|---|---|
| `GetBalanceSheetAsReportedAsync` | `stable/balance-sheet-statement-as-reported` | the as-filed counterpart of `GetBalanceSheetAsync` |
| `GetCashFlowAsReportedAsync` | `stable/cash-flow-statement-as-reported` | the as-filed counterpart of `GetCashFlowAsync` |
| `GetFullStatementAsReportedAsync` | `stable/financial-statement-full-as-reported` | **all three statements plus the cover page in one object** — 300 keys for AAPL and 923 for JPM, measured 2026-08-27, and the payload where the 47 strings and the postal code live |

Then the two segmentation methods:

```csharp
    /// <summary>One symbol's revenue split by product line, newest period first. From
    /// <c>stable/revenue-product-segmentation</c>.
    ///
    /// <para><b>Takes no <c>limit</c>.</b> Measured 2026-08-27, the endpoint transfers the full set regardless of
    /// what is sent, so offering the parameter would be offering a lever that does nothing.</para>
    ///
    /// <para><b>The <c>structure</c> parameter FMP documents is not sent either.</b> Measured on AAPL and on JPM —
    /// a filer with genuinely nested segments — <c>structure=flat</c> and <c>structure=hierarchical</c> returned
    /// payloads identical to sending nothing at all. It is inert.</para>
    ///
    /// <para>Segment names are the company's own and change when it reorganises; see
    /// <see cref="RevenueSegmentation"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="period">Which series to ask for. All six values work.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, or empty for an unknown symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<RevenueSegmentation>> GetRevenueByProductAsync(
        string symbol, FiscalPeriod period = FiscalPeriod.Annual, CancellationToken ct = default) =>
        transport.GetListAsync(Envelope("stable/revenue-product-segmentation", symbol, period),
            FmpJsonContext.Default.ListRevenueSegmentation, ct);
```

`GetRevenueByGeographyAsync` is the same against `stable/revenue-geographic-segmentation`, with a summary noting its keys are country and region names rather than product lines.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~AsReportedTests"`
Expected: PASS, 10 tests.

- [ ] **Step 7: Mutation-check the two-dictionaries decision**

Change `RevenueSegmentation.Data` to `IReadOnlyDictionary<string, JsonElement>` and confirm `A_string_segment_value_throws_rather_than_binding_as_zero` fails — the throw is the guarantee, and one type for all six paths would silently remove it. Restore. Record it.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/AsReportedStatements.cs src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/AsReportedTests.cs
git commit -m "feat: add the four as-reported paths and both revenue segmentations (#28)"
```

---

### Task 6: Owner earnings, and the cap that cannot be seen from the payload

One path, one model, and one trap that is worth more than the model: the endpoint truncates at 50 rows and nothing in the response says so.

**Files:**
- Create: `src/FmpDotNet/Models/OwnerEarnings.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +1 entry
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +1 constant, +1 method, and the cref conversion Task 1 deferred
- Create: `tests/FmpDotNet.Tests/OwnerEarningsTests.cs`

**Interfaces:**
- Consumes: `Rolling()` (Task 2), `Binding.Fixture`/`Binding.Unbound` (Task 2).
- Produces: `public const int StatementEndpoints.MaxOwnerEarningsRows = 50` — referenced by the cref Task 1 left as `<c>`.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/OwnerEarningsTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class OwnerEarningsTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task It_asks_for_the_whole_history_and_sends_no_period()
    {
        // Measured 2026-08-27: owner-earnings accepts `period` and ignores it — the series is quarterly only.
        var (endpoints, handler) = Build();

        await endpoints.GetOwnerEarningsAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/owner-earnings", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task A_row_binds_all_ten_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("owner-earnings.AAPL.json"));

        var rows = await endpoints.GetOwnerEarningsAsync("AAPL");

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(2026, rows[0].FiscalYear);        // arrives as the STRING "2026"
        Assert.Equal("Q3", rows[0].Period);
        Assert.Equal(new NodaTime.LocalDate(2026, 6, 27), rows[0].Date);
        // Two of the ten are routinely negative — they are capital SPENDING, and reading them as positive
        // outflows double-counts the sign.
        Assert.True(rows[0].MaintenanceCapex < 0);
        Assert.True(rows[0].GrowthCapex < 0);
    }

    [Fact]
    public void The_measured_row_ceiling_is_recorded_as_a_constant()
    {
        // Not a tautology: the constant is the only place the SDK records that a full-length answer may be
        // truncated, and a caller comparing rows.Count against it is the only way to suspect it. Measured
        // 2026-08-27 — AAPL, MSFT, GE, KO, JPM, IBM and PG all returned exactly 50 at limit=100000 while
        // income-statement-ttm returned 164 for the same filers; SHOP returned 46, which is its real history.
        Assert.Equal(50, StatementEndpoints.MaxOwnerEarningsRows);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~OwnerEarningsTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Write the model**

Create `src/FmpDotNet/Models/OwnerEarnings.cs`:

```csharp
using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Buffett-style owner earnings for one fiscal quarter. From <c>stable/owner-earnings</c>.
///
/// <para><b>Quarterly only.</b> The endpoint accepts <c>period</c> and ignores it, measured 2026-08-27, so there
/// is no annual series to ask for — the rows step by quarter and the newest is the latest reported one.</para>
///
/// <para><b>Owner earnings is a derived figure, not a filed one.</b> It is net income plus depreciation and
/// amortisation less the capital spending needed to hold the business steady, and the last term is an estimate
/// FMP makes: it splits total capex into <see cref="MaintenanceCapex"/> and <see cref="GrowthCapex"/> using
/// <see cref="AveragePpe"/>. No issuer files that split. Two providers computing this from the same statements
/// will disagree, and nothing on the row records the method.</para></summary>
public sealed record OwnerEarnings
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>ISO currency the figures are reported in.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Fiscal year. Arrives as a JSON <b>string</b> on this path — <c>"2026"</c> — and as an integer on
    /// six others in the same section. One <c>int?</c> reads both only because <c>FmpJsonContext</c> sets
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal quarter as FMP labels it: <c>Q1</c>–<c>Q4</c>. Never <c>FY</c> — see the type's
    /// summary.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>Quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The ratio FMP uses to split capex into maintenance and growth — <b>a rate, not an amount</b>,
    /// despite the name reading like a balance. AAPL measured 0.13466 for Q3 2026.</summary>
    [JsonPropertyName("averagePPE")] public decimal? AveragePpe { get; init; }

    /// <summary>Estimated capital spending needed to maintain the business. <b>Negative</b> — it is an outflow as
    /// the cash flow statement signs it, measured −383,794,540 for AAPL Q3 2026. Adding it to net income is the
    /// arithmetic; subtracting its absolute value double-counts.</summary>
    [JsonPropertyName("maintenanceCapex")] public decimal? MaintenanceCapex { get; init; }

    /// <summary>Owner earnings for the quarter. Note the spelling: FMP writes <c>ownersEarnings</c>, plural
    /// possessive, where the endpoint is named <c>owner-earnings</c>.</summary>
    [JsonPropertyName("ownersEarnings")] public decimal? OwnersEarnings { get; init; }

    /// <summary>Estimated capital spending on growth — total capex less <see cref="MaintenanceCapex"/>. Also
    /// negative.</summary>
    [JsonPropertyName("growthCapex")] public decimal? GrowthCapex { get; init; }

    /// <summary>Owner earnings per share for the quarter.</summary>
    [JsonPropertyName("ownersEarningsPerShare")] public decimal? OwnersEarningsPerShare { get; init; }
}
```

- [ ] **Step 4: Register it**

```csharp
[JsonSerializable(typeof(List<OwnerEarnings>))]
```

- [ ] **Step 5: Add the constant, the method, and convert Task 1's deferred cref**

In `StatementEndpoints.cs`:

```csharp
    /// <summary>The most rows <c>stable/owner-earnings</c> will return, whatever limit is sent — and the reason a
    /// caller has to care.
    ///
    /// <para>Measured 2026-08-27 at <c>limit=100000</c>: AAPL, MSFT, GE, KO, JPM, IBM and PG each returned
    /// <b>exactly 50</b>, oldest row 2013-12-31 to 2014-05-09. <c>income-statement-ttm</c> reaches 1985 for the
    /// same filers, so 50 is this endpoint's ceiling rather than the extent of FMP's data.</para>
    ///
    /// <para><b>The payload cannot tell you which case you are in.</b> SHOP returned 46, and that is Shopify's
    /// real history. So fewer than 50 rows is data, exactly 50 rows is a truncation, and the two are
    /// indistinguishable from the response — there is no <c>hasMore</c>, no total, and no error. Comparing
    /// <c>rows.Count</c> against this constant is the only signal there is.</para></summary>
    public const int MaxOwnerEarningsRows = 50;

    /// <summary>Buffett-style owner earnings for one symbol, newest quarter first. From
    /// <c>stable/owner-earnings</c>.
    ///
    /// <para><b>Quarterly only, and capped at <see cref="MaxOwnerEarningsRows"/> rows.</b> A result of exactly 50
    /// rows is probably truncated and cannot be distinguished from a company with exactly 50 quarters of history
    /// — read that constant before treating the oldest row as the start of the series. Roughly twelve years,
    /// measured 2026-08-27.</para>
    ///
    /// <para>Takes no <c>period</c>: the endpoint accepts one and ignores it.</para>
    ///
    /// <para>The figures are FMP's estimates rather than filed values — see <see cref="OwnerEarnings"/>, which
    /// also explains why two of the ten fields are negative.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Rows to return, newest first. <see langword="null"/> means the whole history — see
    /// <see cref="FullHistoryLimit"/> — which this endpoint still caps at
    /// <see cref="MaxOwnerEarningsRows"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Rows newest first, at most <see cref="MaxOwnerEarningsRows"/> of them, or empty for an unknown
    /// symbol. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<OwnerEarnings>> GetOwnerEarningsAsync(
        string symbol, int? limit = null, CancellationToken ct = default) =>
        transport.GetListAsync(Rolling("stable/owner-earnings", symbol, limit),
            FmpJsonContext.Default.ListOwnerEarnings, ct);
```

Then, in the `FullHistoryLimit` summary written in Task 1, change `<c>MaxOwnerEarningsRows</c>` to `<see cref="MaxOwnerEarningsRows"/>`. The constant exists now, so the cref resolves.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~OwnerEarningsTests"`
Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Models/OwnerEarnings.cs src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/OwnerEarningsTests.cs
git commit -m "feat: add owner earnings, and record the 50-row ceiling it does not report (#28)"
```

---

### Task 7: The transport learns two new body shapes

`FmpTransport` today reads a JSON array or a CSV stream. `financial-reports-json` answers a JSON *object* and `financial-reports-xlsx` answers a zip, and neither fits. This task is prerequisite to Task 8 and touches nothing else.

**Files:**
- Modify: `src/FmpDotNet/FmpTransport.cs` — +2 public methods
- Modify: `tests/FmpDotNet.Tests/FmpTransportTests.cs` — +tests for both

**Interfaces:**
- Consumes: `SendAsync`, `ReadFailureAsync`, `ErrorTextFrom` (all existing `private`).
- Produces: `FmpTransport.GetObjectAsync<T>(FmpRequest, JsonTypeInfo<T>, CancellationToken) -> Task<T?>` and `FmpTransport.GetBytesAsync(FmpRequest, CancellationToken) -> Task<byte[]>`. Task 8 calls both.

**The design point a reviewer should check:** `GetListAsync` tells success from failure by the first
byte — success is a JSON array, an error envelope is a JSON object. That test is useless here,
because on `financial-reports-json` **both are objects**. So `GetObjectAsync` buffers the body into a
`JsonDocument` and asks `ErrorTextFrom` whether the root names an error, which is the existing
three-spelling check. Buffering is a real cost — the measured report is 558 KB — and it is accepted
because there is no prefix that separates the two shapes.

`GetBytesAsync` does **not** classify at all. It returns whatever arrived, and the caller decides. A
transport that sniffed for a zip would be guessing on behalf of a single endpoint.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/FmpTransportTests.cs`. Match the file's existing helper for building a transport around a `StubHandler`; read the top of that file first rather than inventing a second pattern.

```csharp
    [Fact]
    public async Task GetObjectAsync_reads_a_json_object_body()
    {
        var (transport, handler) = BuildTransport(StubHandler.Json("""{"symbol":"AAPL","period":"FY","year":"2025"}"""));

        var report = await transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport);

        Assert.NotNull(report);
        Assert.Equal("AAPL", report.Symbol);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetObjectAsync_raises_an_error_envelope_that_arrived_as_a_200()
    {
        // The reason this method cannot use GetListAsync's first-byte test: here BOTH the success shape and the
        // error shape are JSON objects. Measured 2026-08-27, a miss on financial-reports-json is HTTP 200
        // carrying {"Error Message": "No Data for this symbol or invalid API call…"}.
        var (transport, _) = BuildTransport(StubHandler.Json(
            """{"Error Message":"No Data for this symbol or invalid API call."}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(
            () => transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport));

        Assert.Contains("No Data for this symbol", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetObjectAsync_raises_a_plan_restriction_before_reading_the_body()
    {
        var (transport, _) = BuildTransport(StubHandler.Status(System.Net.HttpStatusCode.PaymentRequired));

        await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport));
    }

    [Fact]
    public async Task GetBytesAsync_returns_the_body_verbatim_without_looking_at_it()
    {
        // Deliberately not JSON and deliberately not a zip. The transport does not classify a binary body — the
        // endpoint that knows what it asked for does.
        byte[] payload = [0x50, 0x4B, 0x03, 0x04, 0xFF, 0x00, 0x41];
        var (transport, _) = BuildTransport(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            // The content type FMP actually sends for a workbook, which is a lie, and is ignored here.
            Content = new ByteArrayContent(payload)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
            },
        });

        Assert.Equal(payload, await transport.GetBytesAsync(new FmpRequest("stable/x")));
    }

    [Fact]
    public async Task GetBytesAsync_still_raises_a_failure_status()
    {
        var (transport, _) = BuildTransport(StubHandler.Status(System.Net.HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<FmpApiException>(() => transport.GetBytesAsync(new FmpRequest("stable/x")));
    }
```

`FmpJsonContext.Default.FinancialReport` does not exist until Task 8. **In this task, write these
tests against a placeholder type you add to the context here instead** — or, simpler and preferred,
move the three `GetObjectAsync` tests into Task 8 and keep only the two `GetBytesAsync` tests here.
Take the simpler path: **Task 7 ships `GetObjectAsync` covered by the two `GetBytesAsync` tests plus
a compile, and Task 8 adds the three `GetObjectAsync` tests above once `FinancialReport` exists.**
Say so in the task report so the reviewer knows the gap is deliberate and closed one task later.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~FmpTransportTests"`
Expected: FAIL to compile — no `GetBytesAsync`.

- [ ] **Step 3: Add both methods to `FmpTransport`**

After `GetListAsync` and its `ReadListAsync` helper:

```csharp
    /// <summary>GETs a JSON <b>object</b> and deserialises it through a source-generated
    /// <see cref="JsonTypeInfo{T}"/>. Null only when FMP sent a literal JSON <c>null</c>.
    ///
    /// <para><b>Separate from <see cref="GetListAsync"/> because the error test is different, not because the
    /// shape is.</b> That method tells success from failure by the first meaningful byte: success is a JSON
    /// array and an FMP error envelope is a JSON object, so one byte separates them without parsing either. Here
    /// both are objects — measured 2026-08-27, a miss on <c>stable/financial-reports-json</c> answers HTTP 200
    /// carrying <c>{"Error Message": …}</c>, and a hit answers HTTP 200 carrying a 73-key document. No prefix
    /// distinguishes them.</para>
    ///
    /// <para>So the body is buffered into a <see cref="JsonDocument"/> and its root is offered to the same
    /// error-envelope check the rest of the transport uses. <b>Buffering is a real cost</b> — the measured report
    /// is 558 KB — and it is accepted because the alternative is guessing.</para></summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error — in the body of a 200, or on a non-success
    /// status.</exception>
    public async Task<T?> GetObjectAsync<T>(
        FmpRequest request, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var _ = body.ConfigureAwait(false);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new FmpApiException(
                $"FMP answered a body that is not JSON: {ex.Message}", request.ToString());
        }

        using (document)
        {
            if (ErrorTextFrom(document.RootElement) is { } message)
                throw new FmpApiException(message, request.ToString());
            return document.RootElement.Deserialize(typeInfo);
        }
    }

    /// <summary>GETs a body and hands back its bytes, unexamined.
    ///
    /// <para><b>It must not go near a JSON reader, and it deliberately does not classify what arrived.</b>
    /// <c>stable/financial-reports-xlsx</c> answers an XLSX zip under
    /// <c>Content-Type: application/json; charset=utf-8</c> — measured 2026-08-27, 1,399,564 bytes beginning
    /// <c>PK\x03\x04</c> — and answers a MISS the same way: HTTP 200, the same content type, and 16 bytes of
    /// <c>Error with query</c>. Neither the status nor the header separates them, so the only reliable test is
    /// the magic number, and that test belongs to the endpoint that knows it asked for a workbook rather than to
    /// a transport that would be guessing on one path's behalf.</para>
    ///
    /// <para>The whole body is buffered, because bytes are what the caller asked for. Non-success statuses still
    /// raise, so a 402, a 429 or a 400 behaves as it does everywhere else.</para></summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP answered a non-success status.</exception>
    public async Task<byte[]> GetBytesAsync(FmpRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
```

`JsonElement.Deserialize(JsonTypeInfo<T>)` is the AOT-safe overload — do **not** reach for `JsonSerializer.Deserialize<T>(string)`, which is reflection-based and fails the build on IL2026.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~FmpTransportTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/FmpTransport.cs tests/FmpDotNet.Tests/FmpTransportTests.cs
git commit -m "feat: teach the transport to read a JSON object and a binary body (#28)"
```

---

### Task 8: Report access — a link list, a rendered document, and a workbook

Three paths, three different answer shapes, and the two hardest traps in the slice.

**Files:**
- Create: `src/FmpDotNet/Models/FinancialReports.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +2 entries
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +3 methods, and the cref conversion Task 1 deferred
- Create: `tests/FmpDotNet.Tests/FinancialReportTests.cs`
- Modify: `tests/FmpDotNet.Tests/FmpTransportTests.cs` — the three `GetObjectAsync` tests Task 7 deferred

**Interfaces:**
- Consumes: `FmpTransport.GetObjectAsync<T>` and `GetBytesAsync` (Task 7), `Binding.Fixture` (Task 2).
- Produces: `FinancialReportLink`, `FinancialReport`, `FinancialReportJsonConverter`, `ReportPeriodJsonConverter`.

**Two rulings this task carries, both measured 2026-08-27 and recorded in the measurements addendum.**

1. **`FiscalPeriod.Quarter` is rejected on the two document methods.** Both paths accept it and
   silently resolve it to Q1: `financial-reports-json?period=quarter` echoes `"period": "Q1"`, and
   the workbook comes back named `AAPL_2025_Q1_.xlsx` at 58,263 bytes where the Q3 one the caller
   probably wanted is 785,087. A filed report is one fiscal period; "the 2025 quarterly report" is
   not a document. `Annual` is fine — it normalises to `FY`, which is a real filing.
2. **`FinancialReportLink.Period` is typed `FiscalPeriod?`, not `string?`.** Every other model in
   this slice keeps `Period` as a string because there it is a label on a data row. Here it is an
   argument the caller feeds straight back into `GetFinancialReportAsync`, and that round trip is
   the entire purpose of the dates endpoint. Typing it as a string would make the SDK's one
   list-then-fetch workflow go through a hand-written parse.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/FinancialReportTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class FinancialReportTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses.Length > 0 ? responses : [StubHandler.Json("[]")]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    /// <summary>A body FMP would answer with the workbook's content type, which is a lie about every one of
    /// these.</summary>
    private static HttpResponseMessage Binary(byte[] payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
            },
        };

    // ---- financial-reports-dates ----------------------------------------------------------------------------

    [Fact]
    public async Task The_dates_list_sends_only_a_symbol()
    {
        // Measured 2026-08-27: it ignores `limit` and transfers all 65 rows regardless.
        var (endpoints, handler) = Build();

        await endpoints.GetFinancialReportDatesAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/financial-reports-dates", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task A_link_row_parses_fmps_response_period_back_into_the_request_enum()
    {
        // The whole point of the type. `financial-reports-dates` answers "Q3"; GetFinancialReportAsync takes a
        // FiscalPeriod. Typing this as a string would put a hand-written parse between the two calls.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("financial-reports-dates.AAPL.json")));

        var links = await endpoints.GetFinancialReportDatesAsync("AAPL");

        Assert.Equal("AAPL", links[0].Symbol);
        Assert.Equal(2026, links[0].FiscalYear);
        Assert.Equal(FiscalPeriod.Q3, links[0].Period);
        Assert.NotNull(links[0].LinkJson);
    }

    [Fact]
    public async Task An_annual_link_row_parses_fy_as_annual()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","linkJson":"https://x","linkXlsx":"https://y"}]"""));

        var link = Assert.Single(await endpoints.GetFinancialReportDatesAsync("AAPL"));

        Assert.Equal(FiscalPeriod.Annual, link.Period);
    }

    [Fact]
    public async Task An_unrecognised_period_label_binds_to_null_rather_than_throwing()
    {
        // One unreadable label must not cost the caller the other 64 rows — the rule the date converters follow.
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"symbol":"AAPL","fiscalYear":2025,"period":"H1","linkJson":"https://x","linkXlsx":"https://y"}]"""));

        var link = Assert.Single(await endpoints.GetFinancialReportDatesAsync("AAPL"));

        Assert.Null(link.Period);
        Assert.Equal("AAPL", link.Symbol);
    }

    // ---- financial-reports-json -----------------------------------------------------------------------------

    [Fact]
    public async Task A_report_keeps_its_three_scalars_apart_from_its_seventy_sections()
    {
        var (endpoints, handler) = Build(StubHandler.Json(
            Binding.Fixture("financial-reports-json.AAPL.2025.FY.json")));

        var report = await endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Annual);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/financial-reports-json", uri.AbsolutePath);
        Assert.Contains("year=2025", uri.Query);
        Assert.Contains("period=annual", uri.Query);

        Assert.NotNull(report);
        Assert.Equal("AAPL", report.Symbol);
        Assert.Equal("FY", report.Period);          // FMP normalises `annual` to `FY` in its own echo
        Assert.Equal(2025, report.Year);            // arrives as the STRING "2025"
        // symbol, period and year are NOT sections.
        Assert.DoesNotContain("symbol", report.Sections.Keys);
        Assert.Contains("Cover Page", report.Sections.Keys);
    }

    [Fact]
    public async Task A_report_section_name_is_truncated_and_the_type_does_not_pretend_otherwise()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            Binding.Fixture("financial-reports-json.AAPL.2025.FY.json")));

        var report = await endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Annual);

        // Measured 2026-08-27: section names are cut at about 30 characters and vary per filing, which is why
        // nothing typed sits over them.
        Assert.Contains("CONSOLIDATED STATEMENTS OF OPER", report!.Sections.Keys);
        Assert.Equal(JsonValueKind.Array, report.Sections["CONSOLIDATED STATEMENTS OF OPER"].ValueKind);
    }

    [Fact]
    public async Task A_report_miss_arrives_as_an_error_envelope_on_a_200_and_raises()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            """{"Error Message":"No Data for this symbol or invalid API call."}"""));

        await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetFinancialReportAsync("NOSUCHSYM", 2025, FiscalPeriod.Annual));
    }

    // ---- financial-reports-xlsx -----------------------------------------------------------------------------

    [Fact]
    public async Task A_workbook_is_recognised_by_its_magic_number()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];
        var (endpoints, _) = Build(Binary(zip));

        var bytes = await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual);

        Assert.Equal(zip, bytes);
    }

    [Fact]
    public async Task A_workbook_miss_is_null_even_though_fmp_answered_two_hundred()
    {
        // Measured 2026-08-27, for both a bad symbol and a good symbol in a year with no filing: HTTP 200,
        // Content-Type application/json, and exactly these 16 bytes. Neither the status nor the header
        // distinguishes it from the 1.4 MB zip, which is why the magic number is the test.
        var (endpoints, _) = Build(Binary(Encoding.UTF8.GetBytes("Error with query")));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("NOSUCHSYM", 2025, FiscalPeriod.Annual));
    }

    [Fact]
    public async Task An_empty_workbook_body_is_null_rather_than_an_index_out_of_range()
    {
        var (endpoints, _) = Build(Binary([]));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual));
    }

    [Fact]
    public async Task A_body_shorter_than_the_magic_number_is_null()
    {
        var (endpoints, _) = Build(Binary([0x50, 0x4B]));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual));
    }

    // ---- the quarter trap -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(FiscalPeriod.Annual)]
    [InlineData(FiscalPeriod.Q1)]
    [InlineData(FiscalPeriod.Q4)]
    public async Task A_named_period_reaches_the_wire_on_both_document_paths(FiscalPeriod period)
    {
        var (endpoints, handler) = Build(Binary([0x50, 0x4B, 0x03, 0x04]));

        await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, period);

        Assert.Contains($"period={period.ToQueryValue()}", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_bare_quarter_is_rejected_on_both_document_paths_before_a_request_goes_out()
    {
        // Measured 2026-08-27: FMP accepts period=quarter here and silently answers Q1 — the workbook comes back
        // named AAPL_2025_Q1_.xlsx at 58,263 bytes against 785,087 for the Q3 one. A report is one fiscal period,
        // so the caller has to name it rather than have FMP pick.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Quarter));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Quarter));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~FinancialReportTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Write the models and the two converters**

Create `src/FmpDotNet/Models/FinancialReports.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>Reads FMP's report-period label — <c>FY</c>, <c>Q1</c>–<c>Q4</c> — as a <see cref="FiscalPeriod"/>.
///
/// <para>Applied only to <see cref="FinancialReportLink.Period"/>, and the narrowness is the point. Everywhere
/// else in this SDK a <c>period</c> field is a LABEL on a row of data and stays a string. On a report link it is
/// an ARGUMENT: the caller passes it straight back to
/// <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>, and that list-then-fetch round trip is the
/// only reason <c>financial-reports-dates</c> exists.</para>
///
/// <para>An unrecognised label reads as null rather than throwing, following the date converters: one unreadable
/// row out of 65 must not cost the caller the other 64.</para></summary>
public sealed class ReportPeriodJsonConverter : JsonConverter<FiscalPeriod?>
{
    /// <inheritdoc/>
    public override FiscalPeriod? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "FY" or "annual" => FiscalPeriod.Annual,
                "Q1" => FiscalPeriod.Q1,
                "Q2" => FiscalPeriod.Q2,
                "Q3" => FiscalPeriod.Q3,
                "Q4" => FiscalPeriod.Q4,
                _ => null,
            }
            : null;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FiscalPeriod? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value == FiscalPeriod.Annual ? "FY" : value.Value.ToQueryValue());
    }
}

/// <summary>One filing FMP holds a rendered report for, and the two links it publishes for it. From
/// <c>stable/financial-reports-dates</c> — 65 rows for AAPL measured 2026-08-27, FY and Q1–Q4 back to 2013.
///
/// <para><b>The two links are not usable as they arrive.</b> Both carry the literal string
/// <c>apikey=YOUR_API_KEY</c> rather than a key, so fetching one as-is fails. They are documentation of the URL
/// shape, not credentials — call <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/> or
/// <see cref="Endpoints.StatementEndpoints.GetFinancialReportWorkbookAsync"/> with
/// <see cref="Symbol"/>, <see cref="FiscalYear"/> and <see cref="Period"/> instead, which is what
/// <see cref="Period"/> is typed as an enum for.</para></summary>
public sealed record FinancialReportLink
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON integer on this path.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Which report — <see cref="FiscalPeriod.Annual"/> for the <c>FY</c> filing, or the named quarter.
    ///
    /// <para><b>Typed as the request enum rather than as FMP's label string</b>, because this value's job is to
    /// be handed back to <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>. Null when FMP sent a
    /// label this SDK does not recognise, which no measured row did.</para></summary>
    [JsonPropertyName("period")]
    [JsonConverter(typeof(ReportPeriodJsonConverter))]
    public FiscalPeriod? Period { get; init; }

    /// <summary>FMP's URL for the rendered JSON report. <b>Carries <c>YOUR_API_KEY</c>, not a key.</b></summary>
    [JsonPropertyName("linkJson")] public string? LinkJson { get; init; }

    /// <summary>FMP's URL for the XLSX workbook. <b>Carries <c>YOUR_API_KEY</c>, not a key.</b></summary>
    [JsonPropertyName("linkXlsx")] public string? LinkXlsx { get; init; }
}

/// <summary>One filing rendered as report sections. From <c>stable/financial-reports-json</c>.
///
/// <para><b>This is a rendered document, not a record, and the type does not pretend otherwise.</b> The response
/// is a flat object of 73 keys measured 2026-08-27 for AAPL FY2025: <c>symbol</c>, <c>period</c>, <c>year</c>,
/// and 70 report SECTION NAMES. The section names are truncated to about 30 characters
/// (<c>"CONSOLIDATED STATEMENTS OF OPER"</c>), carry spaces, parentheses and commas, and differ per filing —
/// <c>period=Q1</c> answered 45 keys against <c>FY</c>'s 73. Anything typed over them would be a guess dressed as
/// an API, so <see cref="Sections"/> stays open.</para>
///
/// <para>Each section is a JSON array of single-key objects, the key being a full column header and the value a
/// list of cell strings:</para>
///
/// <code>
/// {"CONSOLIDATED BALANCE SHEETS - USD ($) shares in Thousands, $ in Millions": ["Sep. 27, 2025", "Sep. 28, 2024"]}
/// </code>
///
/// <para>For figures you want to compute with, use the statement endpoints. This is for showing a filing the way
/// it was laid out.</para></summary>
[JsonConverter(typeof(FinancialReportJsonConverter))]
public sealed record FinancialReport
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The period FMP says it answered, in its own label vocabulary — <c>FY</c> or <c>Q1</c>–<c>Q4</c>.
    ///
    /// <para><b>Worth reading rather than assuming.</b> FMP normalises the request: asking for <c>annual</c> gets
    /// <c>FY</c> back, and asking for <c>quarter</c> got <c>Q1</c> back — which is why the SDK refuses to send
    /// that. See <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>.</para></summary>
    public string? Period { get; init; }

    /// <summary>Fiscal year. Arrives as a JSON <b>string</b> on this path.</summary>
    public int? Year { get; init; }

    /// <summary>The report's sections, keyed by FMP's truncated section name. Never <see langword="null"/>;
    /// empty when the response carried nothing but the three scalars.</summary>
    public IReadOnlyDictionary<string, JsonElement> Sections { get; init; } =
        ReadOnlyDictionary<string, JsonElement>.Empty;
}

/// <summary>Splits <c>stable/financial-reports-json</c>'s flat object into three scalars and everything else.
///
/// <para><b>Hand-written rather than <c>[JsonExtensionData]</c>, which is the obvious tool and the wrong one
/// here.</b> That attribute requires the property to be a mutable <c>Dictionary&lt;string, JsonElement&gt;</c>
/// and public, so using it would put a mutable dictionary on the public surface of a record whose other
/// collection properties are read-only. Twenty lines buys consistency.</para></summary>
public sealed class FinancialReportJsonConverter : JsonConverter<FinancialReport>
{
    /// <inheritdoc/>
    public override FinancialReport Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A financial report must be a JSON object.");

        var symbol = "";
        string? period = null;
        int? year = null;
        var sections = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            reader.Read();
            switch (name)
            {
                case "symbol": symbol = reader.GetString() ?? ""; break;
                case "period": period = reader.GetString(); break;
                // The wire sends "2025", a string, but an int would be just as legal and this costs nothing.
                case "year":
                    year = reader.TokenType switch
                    {
                        JsonTokenType.Number => reader.GetInt32(),
                        JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
                        _ => null,
                    };
                    break;
                default: sections[name] = JsonElement.ParseValue(ref reader); break;
            }
        }

        return new FinancialReport { Symbol = symbol, Period = period, Year = year, Sections = sections };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FinancialReport value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("symbol", value.Symbol);
        writer.WriteString("period", value.Period);
        if (value.Year is { } year) writer.WriteNumber("year", year); else writer.WriteNull("year");
        foreach (var (name, section) in value.Sections)
        {
            writer.WritePropertyName(name);
            section.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
```

- [ ] **Step 4: Register both in `FmpJsonContext`**

Note the second one is **not** a list — this is the only object-shaped response in the slice:

```csharp
[JsonSerializable(typeof(List<FinancialReportLink>))]
[JsonSerializable(typeof(FinancialReport))]
```

- [ ] **Step 5: Add the three methods**

In `StatementEndpoints.cs`:

```csharp
    /// <summary>Every filing FMP holds a rendered report for, with the links it publishes for each. From
    /// <c>stable/financial-reports-dates</c> — 65 rows for AAPL measured 2026-08-27, FY and Q1–Q4 back to 2013.
    ///
    /// <para>This is the index for <see cref="GetFinancialReportAsync"/> and
    /// <see cref="GetFinancialReportWorkbookAsync"/>: it tells you which (year, period) pairs exist, so a caller
    /// does not have to probe for them and read a 200 that means "no". The links on each row are NOT usable as
    /// they arrive — see <see cref="FinancialReportLink"/>.</para>
    ///
    /// <para>Takes no <c>limit</c>: the endpoint ignores it and transfers the full set.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every available filing in FMP's order, or empty for an unknown symbol. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FinancialReportLink>> GetFinancialReportDatesAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/financial-reports-dates").With("symbol", symbol),
            FmpJsonContext.Default.ListFinancialReportLink, ct);
    }

    /// <summary>One filing rendered as report sections. From <c>stable/financial-reports-json</c>.
    ///
    /// <para><b><see cref="FiscalPeriod.Quarter"/> is rejected, and this is the one place in the SDK where a
    /// legal <see cref="FiscalPeriod"/> is refused.</b> A report is one fiscal period, and "the 2025 quarterly
    /// report" is not a document that exists. FMP accepts the value anyway and silently answers Q1 — measured
    /// 2026-08-27, <c>period=quarter</c> echoed <c>"period": "Q1"</c> and returned 45 sections rather than the
    /// 47 of the Q3 report a caller asking this way probably wanted. Name the quarter.</para>
    ///
    /// <para><b>A miss is an HTTP 200 carrying <c>{"Error Message": …}</c></b>, which surfaces as
    /// <see cref="FmpApiException"/> rather than null — unlike
    /// <see cref="GetFinancialReportWorkbookAsync"/>, whose miss carries no message to raise. Use
    /// <see cref="GetFinancialReportDatesAsync"/> to find out which filings exist rather than probing.</para>
    ///
    /// <para><b>The response is buffered whole</b> — 558 KB measured for AAPL FY2025 — because the success shape
    /// and the error shape are both JSON objects and no prefix separates them. See
    /// <see cref="FmpTransport.GetObjectAsync"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="year">Fiscal year of the filing. Required by FMP — omitting it is an HTTP 400.</param>
    /// <param name="period">Which filing: <see cref="FiscalPeriod.Annual"/> or a named quarter.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rendered report, or <see langword="null"/> only if FMP sent a literal JSON null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is
    /// <see cref="FiscalPeriod.Quarter"/> — see above.</exception>
    /// <exception cref="FmpApiException">FMP has no report for that symbol, year and period.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<FinancialReport?> GetFinancialReportAsync(
        string symbol, int year, FiscalPeriod period, CancellationToken ct = default) =>
        transport.GetObjectAsync(Report("stable/financial-reports-json", symbol, year, period),
            FmpJsonContext.Default.FinancialReport, ct);

    /// <summary>One filing as an XLSX workbook, or null when FMP has no such filing. From
    /// <c>stable/financial-reports-xlsx</c>.
    ///
    /// <para><b>Neither the status code nor the content type tells success from failure on this path.</b>
    /// Measured 2026-08-27: a hit is HTTP 200 with <c>Content-Type: application/json; charset=utf-8</c> and a
    /// 1,399,564-byte body beginning <c>PK\x03\x04</c>. A miss — unknown symbol, or a year with no filing — is
    /// <b>also HTTP 200</b>, under the same content type, carrying 16 bytes of <c>Error with query</c>. The only
    /// reliable test is the zip magic number, so that is what this uses: a body starting <c>PK\x03\x04</c> is the
    /// workbook and anything else is null.</para>
    ///
    /// <para>Null rather than an exception because those same 16 bytes cover both "no such symbol" and "no filing
    /// that year" and carry no message to raise — the same reasoning as <see cref="GetScoresAsync"/>. Use
    /// <see cref="GetFinancialReportDatesAsync"/> to learn which filings exist.</para>
    ///
    /// <para><see cref="FiscalPeriod.Quarter"/> is rejected here for the same measured reason as on
    /// <see cref="GetFinancialReportAsync"/>: it resolves to Q1 and the workbook comes back named
    /// <c>AAPL_2025_Q1_.xlsx</c>.</para>
    ///
    /// <para>The whole workbook is buffered. It is megabytes.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="year">Fiscal year of the filing.</param>
    /// <param name="period">Which filing: <see cref="FiscalPeriod.Annual"/> or a named quarter.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The workbook's bytes, or <see langword="null"/> when FMP has no such filing.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is
    /// <see cref="FiscalPeriod.Quarter"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<byte[]?> GetFinancialReportWorkbookAsync(
        string symbol, int year, FiscalPeriod period, CancellationToken ct = default)
    {
        var bytes = await transport
            .GetBytesAsync(Report("stable/financial-reports-xlsx", symbol, year, period), ct)
            .ConfigureAwait(false);
        return bytes is [0x50, 0x4B, 0x03, 0x04, ..] ? bytes : null;
    }

    /// <summary>The query shape the two report-document paths share, and the one place a legal
    /// <see cref="FiscalPeriod"/> is refused. See <see cref="GetFinancialReportAsync"/> for the
    /// measurement.</summary>
    private static FmpRequest Report(string path, string symbol, int year, FiscalPeriod period)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (period == FiscalPeriod.Quarter)
            throw new ArgumentOutOfRangeException(nameof(period), period,
                "A report is one fiscal period. Ask for Annual, or name the quarter (Q1-Q4) — FMP accepts "
                + "'quarter' here and silently answers Q1.");
        return new FmpRequest(path)
            .With("symbol", symbol)
            .With("year", year)
            .With("period", period.ToQueryValue());
    }
```

`bytes is [0x50, 0x4B, 0x03, 0x04, ..]` is a list pattern — it handles the empty and short-body cases without an explicit length check, which is why two of the tests above cover them.

- [ ] **Step 6: Convert Task 1's deferred crefs**

In `src/FmpDotNet/FiscalPeriod.cs`, on the `Quarter` member, change the two `<c>GetFinancialReportAsync</c>` / `<c>GetFinancialReportWorkbookAsync</c>` references back to
`<see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>` and
`<see cref="Endpoints.StatementEndpoints.GetFinancialReportWorkbookAsync"/>`. Both methods exist now. If the build reports CS1574, the cref path is wrong — check the namespace prefix rather than deleting the cref.

- [ ] **Step 7: Add the three `GetObjectAsync` transport tests Task 7 deferred**

Append the three tests written out in Task 7, Step 1 under the heading `GetObjectAsync_…` to `tests/FmpDotNet.Tests/FmpTransportTests.cs`. They compile now that `FmpJsonContext.Default.FinancialReport` exists.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~FinancialReportTests|FullyQualifiedName~FmpTransportTests"`
Expected: PASS.

- [ ] **Step 9: Mutation-check the magic number**

Change `bytes is [0x50, 0x4B, 0x03, 0x04, ..]` to `bytes.Length > 0` and confirm `A_workbook_miss_is_null_even_though_fmp_answered_two_hundred` fails — the length test is what a reader would write without the measurement, and it hands back `Error with query` as a workbook. Restore. Record it.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Models/FinancialReports.cs src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Endpoints/StatementEndpoints.cs src/FmpDotNet/FiscalPeriod.cs tests/FmpDotNet.Tests
git commit -m "feat: add report dates, the rendered report and the XLSX workbook (#28)"
```

---

### Task 9: The market-wide recency feed

The last path, and the only one that is not per-symbol. Two methods, one model, one new converter, and a page ceiling that has to be enforced client-side.

**Files:**
- Create: `src/FmpDotNet/Models/LatestFinancialStatement.cs`
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` — +`NullableLocalDateTimeJsonConverter`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +1 entry
- Modify: `src/FmpDotNet/Endpoints/StatementEndpoints.cs` — +2 constants, +2 methods
- Create: `tests/FmpDotNet.Tests/LatestStatementsTests.cs`

**Interfaces:**
- Consumes: `Binding.Fixture`/`Binding.Unbound` (Task 2).
- Produces: `LatestFinancialStatement`, `NullableLocalDateTimeJsonConverter`, `MaxLatestStatementsPage`, `MaxLatestStatementsPageSize`.

**Why `dateAdded` is a `LocalDateTime` and not an `Instant`.** FMP sends
`"2026-08-27 11:03:21"` — space-separated, no offset, no `T`. The SDK already has two converters for
that exact shape, and they disagree on purpose: the economic calendar's is UTC and the statements'
`acceptedDate` is Eastern, each established by measuring a DST shift. **Neither measurement was made
for this field**, and picking one would be asserting a fact nobody checked — silently putting every
timestamp four or five hours out if wrong. `LocalDateTime` is the type that says what is actually
known: a wall clock with no zone. It still sorts and still compares.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/LatestStatementsTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using NodaTime;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class LatestStatementsTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses.Length > 0 ? responses : [StubHandler.Json("[]")]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_page_is_requested_by_page_and_limit()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetLatestStatementsAsync(page: 2, limit: 250);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/latest-financial-statements", uri.AbsolutePath);
        Assert.Contains("page=2", uri.Query);
        Assert.Contains("limit=250", uri.Query);
    }

    [Fact]
    public async Task A_row_binds_and_is_keyed_on_calendar_year_not_fiscal_year()
    {
        // The only path in this group keyed on calendarYear, measured 2026-08-27. A caller joining these rows to
        // the statement endpoints on "year" is joining two different years for any filer whose fiscal year does
        // not end in December.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("latest-financial-statements.p0.json")));

        var rows = await endpoints.GetLatestStatementsAsync(0, 250);

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(2026, rows[0].CalendarYear);
        Assert.Equal("Q2", rows[0].Period);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        // Space-separated, not ISO-T — the shape that would silently fail an Instant parse expecting a `T`.
        Assert.Equal(new LocalDateTime(2026, 8, 27, 11, 3, 21), rows[0].DateAdded);
    }

    [Theory]
    [InlineData(-1, 250)]
    [InlineData(101, 250)]
    [InlineData(0, 0)]
    [InlineData(0, 251)]
    public async Task An_out_of_range_page_or_limit_throws_before_a_request_goes_out(int page, int limit)
    {
        // Measured 2026-08-27: page=101 is HTTP 400 ("Maxmium Query Parameter…", FMP's spelling) and limit=1000
        // silently answers 250. A caller who asks for 1,000 a page and advances by 1,000 skips three quarters of
        // the feed and never sees an error — which is why the limit is refused here rather than clamped upstream.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestStatementsAsync(page, limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_walk_stops_at_the_first_short_page()
    {
        var full = string.Join(",", Enumerable.Range(0, 250).Select(i => $$"""{"symbol":"S{{i}}","calendarYear":2026}"""));
        var (endpoints, handler) = Build(
            StubHandler.Json($"[{full}]"),
            StubHandler.Json("""[{"symbol":"LAST","calendarYear":2026}]"""));

        var rows = new List<Models.LatestFinancialStatement>();
        await foreach (var row in endpoints.StreamLatestStatementsAsync()) rows.Add(row);

        Assert.Equal(251, rows.Count);
        Assert.Equal(2, handler.Requests.Count);      // it did not ask for a third page
        Assert.Contains("page=0", handler.Requests[0].Query);
        Assert.Contains("page=1", handler.Requests[1].Query);
    }

    [Fact]
    public void The_measured_ceilings_are_recorded_as_constants()
    {
        Assert.Equal(100, StatementEndpoints.MaxLatestStatementsPage);
        Assert.Equal(250, StatementEndpoints.MaxLatestStatementsPageSize);
    }
}
```

`StubHandler` replays its last response forever once its queue runs out, so the two-response stub above would answer a third page identically to the second — which is exactly why the test asserts `handler.Requests.Count` rather than only the row count.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~LatestStatementsTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Add the wall-clock converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`:

```csharp
/// <summary>Reads FMP's <c>"yyyy-MM-dd HH:mm:ss"</c> timestamps as a <see cref="LocalDateTime"/> — a wall clock
/// with <b>no timezone attached</b>, which is exactly what is known about them.
///
/// <para><b>The third converter for this wire shape, and the only honest one where the zone was never
/// measured.</b> <see cref="NullableFmpInstantJsonConverter"/> reads it as UTC and
/// <see cref="NullableEasternInstantJsonConverter"/> reads it as Eastern; each of those readings was established
/// by measuring a DST shift on its own endpoint, and the two are four or five hours apart. Applying either to a
/// field whose zone nobody checked would not be a small risk — it would be a fabricated fact, wrong by hours,
/// with nothing in the data to reveal it.</para>
///
/// <para>So this converter declines to guess. A <see cref="LocalDateTime"/> still sorts, still compares and still
/// formats; what it will not do is claim to be a moment in time. If the zone is ever measured, this becomes an
/// <see cref="Instant"/> and the caller's code gets more correct rather than differently wrong.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad stamp costs one field rather than
/// the whole response.</para></summary>
public sealed class NullableLocalDateTimeJsonConverter : JsonConverter<LocalDateTime?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public override LocalDateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}
```

- [ ] **Step 4: Write the model and register it**

Create `src/FmpDotNet/Models/LatestFinancialStatement.cs`:

```csharp
using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One filing FMP has recently ingested, from <c>stable/latest-financial-statements</c> — the only
/// market-wide path in the Statements group.
///
/// <para><b>A three-week window, not the universe.</b> Measured 2026-08-27: 250 rows a page, <c>page</c> capped
/// at 100, so 25,250 rows are reachable in total — and page 100 was still returning filings dated 2026-08-05.
/// Everything older is simply unreachable through this path. Use it to learn what has landed since you last
/// looked, not to enumerate anything.</para>
///
/// <para><b>Keyed on <see cref="CalendarYear"/>, not fiscal year</b> — the only path in this section that is.
/// Joining these rows to the statement endpoints on "year" silently mismatches every filer whose fiscal year does
/// not end in December.</para></summary>
public sealed record LatestFinancialStatement
{
    /// <summary>Ticker as FMP spells it, including non-US suffixes — <c>300415.SZ</c> was the first row
    /// measured.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Calendar year, <b>not fiscal year</b>. See the type's summary.</summary>
    [JsonPropertyName("calendarYear")] public int? CalendarYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c>, or <c>Q1</c>–<c>Q4</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>Period end — the last day of the fiscal period the filing reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When FMP ingested the filing. The feed is sorted by this, descending.
    ///
    /// <para><b>A wall clock with no timezone, deliberately.</b> FMP sends
    /// <c>"2026-08-27 11:03:21"</c> — space-separated, no offset, not ISO-8601 with a <c>T</c> — and which zone
    /// that is has never been measured for this field. See
    /// <see cref="NullableLocalDateTimeJsonConverter"/>.</para></summary>
    [JsonPropertyName("dateAdded")]
    [JsonConverter(typeof(NullableLocalDateTimeJsonConverter))]
    public LocalDateTime? DateAdded { get; init; }
}
```

Register: `[JsonSerializable(typeof(List<LatestFinancialStatement>))]`

- [ ] **Step 5: Add the constants and the two methods**

`StatementEndpoints.cs` needs `using System.Runtime.CompilerServices;` at the top for `[EnumeratorCancellation]`.

```csharp
    /// <summary>The highest <c>page</c> <c>stable/latest-financial-statements</c> will serve. Measured
    /// 2026-08-27: <c>page=101</c> answers HTTP 400 <c>Maxmium Query Parameter: The maximum page number for this
    /// endpoint is '100'</c> — FMP's spelling of "maximum".
    ///
    /// <para>With <see cref="MaxLatestStatementsPageSize"/> that makes 25,250 rows reachable in total, and page
    /// 100 was still returning filings dated 2026-08-05 — so the ceiling cuts about three weeks back and
    /// everything older is unreachable through this path.</para></summary>
    public const int MaxLatestStatementsPage = 100;

    /// <summary>The largest page <c>stable/latest-financial-statements</c> will serve. A <b>cap, not a page
    /// size</b>: measured 2026-08-27, <c>limit=1000</c> answered exactly 250 rows.
    ///
    /// <para>A caller who asks for 1,000 rows a page and advances the page index by 1,000 skips three quarters of
    /// the feed and never sees an error, so <see cref="GetLatestStatementsAsync"/> rejects a larger limit rather
    /// than passing it on to be clamped — the same treatment
    /// <see cref="DirectoryEndpoints.MaxCikListPageSize"/> gives the registrant index.</para></summary>
    public const int MaxLatestStatementsPageSize = 250;

    /// <summary>One page of the market-wide feed of recently-ingested filings, newest first. From
    /// <c>stable/latest-financial-statements</c>.
    ///
    /// <para>Rows are keyed on calendar year rather than fiscal year, and carry a wall-clock ingest time with no
    /// timezone — see <see cref="LatestFinancialStatement"/> for both.</para></summary>
    /// <param name="page">Zero-based page index, 0 to <see cref="MaxLatestStatementsPage"/>.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxLatestStatementsPageSize"/>. Required rather than
    /// defaulted: the page size and the page index have to agree for a walk to be complete, and a default would
    /// let them disagree invisibly.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows, newest first. Empty past the end. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is outside 0 to
    /// <see cref="MaxLatestStatementsPage"/>, or <paramref name="limit"/> is outside 1 to
    /// <see cref="MaxLatestStatementsPageSize"/> — see those constants for why both are enforced here rather than
    /// left to be clamped or refused upstream.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<LatestFinancialStatement>> GetLatestStatementsAsync(
        int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, MaxLatestStatementsPage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxLatestStatementsPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/latest-financial-statements").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListLatestFinancialStatement, ct);
    }

    /// <summary>Walks the recency feed from page 0 and streams every filing it can reach — at most 25,250 rows
    /// over 101 requests.
    ///
    /// <para><b>Bounded by <see cref="MaxLatestStatementsPage"/> as well as by a short page</b>, and the bound is
    /// not belt-and-braces: page 101 is an HTTP 400, so a walk that only stopped on a short page would end this
    /// sequence with an exception rather than an ending. Measured 2026-08-27, page 100 was still full, so that
    /// bound is reached in practice rather than in theory.</para>
    ///
    /// <para><b>This is not "every statement FMP has."</b> It is roughly the last three weeks of ingests. See
    /// <see cref="LatestFinancialStatement"/>.</para></summary>
    /// <param name="ct">Cancels the walk between pages as well as mid-page.</param>
    /// <exception cref="FmpRateLimitedException">FMP answered 429. Possible if 101 pages are walked flat
    /// out.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async IAsyncEnumerable<LatestFinancialStatement> StreamLatestStatementsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var page = 0; page <= MaxLatestStatementsPage; page++)
        {
            var rows = await GetLatestStatementsAsync(page, MaxLatestStatementsPageSize, ct).ConfigureAwait(false);
            foreach (var row in rows) yield return row;

            // A short page is the last page, and an empty one ends it too — the same condition.
            if (rows.Count < MaxLatestStatementsPageSize) yield break;
        }
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~LatestStatementsTests"`
Expected: PASS, 8 tests.

- [ ] **Step 7: Mutation-check the page ceiling**

Change the loop bound to `for (var page = 0; ; page++)` and confirm the suite still passes — it will, because the stub never returns a full page 101. **That is the point:** the ceiling is not unit-testable against a stub, so record in the task report that it is covered by the measurement and the constant rather than by a test, and do not invent a test that pretends otherwise. Restore the bound.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/LatestFinancialStatement.cs src/FmpDotNet/Serialization src/FmpDotNet/Endpoints/StatementEndpoints.cs tests/FmpDotNet.Tests/LatestStatementsTests.cs
git commit -m "feat: add the market-wide recency feed and its page ceiling (#28)"
```

---

### Task 10: The harnesses, the README, and the live baseline

Twenty new methods change what two reflection-driven harnesses see. One of those changes is a real defect rather than bookkeeping: the smoke sweep would flatten a 1.4 MB workbook into 1.4 million boxed bytes.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` — `byte[]` handling in three places
- Modify: `README.md` — regenerated table, corrected prose
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded live

**Interfaces:**
- Consumes: every method added in Tasks 1–9.
- Produces: nothing.

**`EndpointCoverageTests` needs no change.** Its `Argument()` already handles `year`, `page`, `limit`
and any enum, and its `Drive()` awaits a plain `Task`, so `Task<byte[]?>` is fine. Confirm that by
running it rather than by reading — if it throws `NotSupportedException`, a parameter name slipped
through and the message names it.

- [ ] **Step 1: Fix `Probe` for the binary endpoint**

`GetFinancialReportWorkbookAsync` returns `Task<byte[]?>`, and `byte[]` is an `IEnumerable`. Three places in `tests/FmpDotNet.SmokeTests/Probe.cs` treat it wrongly:

In `Flatten`, add a case **above** the `IEnumerable` case — order matters, since `byte[]` matches both:

```csharp
        null => [],
        // A workbook is ONE answer, not a row set. byte[] is an IEnumerable, so without this the 1.4 MB
        // financial-reports-xlsx response flattens to 1.4 million boxed bytes — measured 2026-08-27.
        byte[] bytes => bytes.Length > 0 ? [bytes] : [],
        IEnumerable rows => …
```

In `ElementType`, inside the `Task<T>` branch, return the array type itself rather than letting it match `IReadOnlyList<byte>`:

```csharp
            var inner = returnType.GetGenericArguments()[0];
            // Before the IReadOnlyList probe: byte[] implements IReadOnlyList<byte>, and resolving this to
            // `byte` would make the baseline describe a workbook as a sequence of bytes.
            if (inner == typeof(byte[])) return typeof(byte[]);
```

In `Fields`, exclude arrays, or the baseline records `Length`, `LongLength`, `Rank` and `IsFixedSize` as if FMP sent them:

```csharp
    private static IReadOnlyList<PropertyInfo> Fields(Type row) =>
        row == typeof(string) || row.IsPrimitive || row.IsEnum || row.IsArray
            ? []
            : …
```

- [ ] **Step 2: Verify the harnesses without spending a live call**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"`
Expected: PASS. These three tests are the only ones in that project not gated on `FMP_API_KEY`, so they run with no key and no request. They prove the sweep can synthesise arguments for all 20 new methods and destructure all 20 return types.

- [ ] **Step 3: Run the whole offline suite**

Run: `dotnet test`
Expected: everything passes except `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`, which fails with a diff showing 19 new paths. Read that diff: **it must list exactly 19 new `stable/` paths, all under `fmp.Statements`.** If it shows 18, a method is not reaching the API; if 20, something is being requested that should not be.

- [ ] **Step 4: Regenerate the coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EndpointCoverageTests"
git diff --stat README.md
```

Then re-run `dotnet test` with the variable unset and confirm it passes.

The header inside the generated block should now read **101 of FMP's 243 endpoint paths are modelled.** If it says anything else, stop and report the number rather than editing the block by hand — it is generated, and a hand-edit will be overwritten and hide whatever is really wrong.

- [ ] **Step 5: Correct the README prose the generator does not own**

Three edits outside the generated block. Current text is at `README.md:250-266`.

Replace:

```
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **161 paths
remain**, of which **154 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are Statements (19), Company (13), SEC
Filings (12), Senate (12), Market Performance (11) and News (10); ETF & Mutual Funds and Technical Indicators carry
9 apiece, Form 13F 8, and Analyst and Calendar 7 each.
```

with:

```
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **142 paths
remain**, of which **135 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are Company (13), SEC Filings (12), Senate
(12), Market Performance (11) and News (10); ETF & Mutual Funds and Technical Indicators carry 9 apiece, Form 13F
8, and Analyst and Calendar 7 each.
```

Then replace:

```
That remainder is tracked as thirteen actionable issues under the epic, each 9 to 19 paths and each carrying the
measured path list for its group. Statements and Company are the largest and the two a trading consumer needs, so
they are the natural next slices rather than long-tail work.
```

with:

```
That remainder is tracked as twelve actionable issues under the epic, each 7 to 13 paths and each carrying the
measured path list for its group. Company is now the largest and is the one a trading consumer needs next, so it
is the natural next slice rather than long-tail work.
```

Check the arithmetic before committing: 161 − 19 = 142, 154 − 19 = 135, and thirteen issues less the one this
slice closes is twelve. If the numbers in the file differ from the ones quoted above, an earlier slice moved them
— recompute from what is there rather than pasting these.

- [ ] **Step 6: Re-record the live smoke baseline**

Twenty new methods means twenty new blocks in `baseline-ordinary.txt`, and the file is a live recording rather than something to hand-write. This is the one step in the plan that calls FMP.

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY=(.+)$',open('.env').read(),re.M).group(1).strip())") \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
```

**Never `source` or `set -a` the `.env` file** — it has clobbered `PATH` for the whole shell before. Extract only
the one key, into the one command, as above. **Do not set `FMPDOTNET_SMOKE_BULK`**: the bulk endpoints are
throttled at 2/min and FMP's own error text warns that frequent abuse of them can get a key restricted. Nothing in
this slice touches bulk.

Then inspect the diff before committing it:

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | head -100
```

Expected: 20 new `[Statements.*]` blocks and **no `set` → `null` flips on existing entries**. A flip means a field
the SDK models stopped arriving, which is a defect in production code rather than churn — investigate it, do not
commit it as noise. New `null` lines *inside the new blocks* are fine and expected: several of the new endpoints
answer sparse rows for AAPL.

If `Statements.GetFinancialReportWorkbookAsync` records anything other than `outcome rows` with no field lines,
the `Probe` fix in Step 1 did not take.

- [ ] **Step 7: Run everything one more time**

```bash
dotnet test
```
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add tests/FmpDotNet.SmokeTests README.md
git commit -m "docs: regenerate coverage at 101 of 243, and stop the sweep unrolling a workbook (#28)"
```

---

## Self-Review

Run against the spec after Task 10, before the final whole-branch review.

**Spec coverage.** Every section of `2026-08-27-statements-design.md` maps to a task:

| spec section | task |
|---|---|
| The seven things a shape-only reading would get wrong, #1 (5-row truncation) | 1 |
| #2 (`period` has six values) | 1 |
| #3 (`owner-earnings` caps at 50) | 6 |
| #4 (xlsx content-type lie) | 8 |
| #5 (as-reported dictionary is open and mixed) | 5 |
| #6 (segmentation shares the envelope, not the problem) | 5 |
| #7 (reusing five models needs JSON attributes) | 3 |
| Surface — TTM statements | 2 |
| Surface — growth and TTM metrics | 3, 4 |
| Surface — as-reported and segmentation | 5 |
| Surface — owner earnings | 6 |
| Surface — report access | 7, 8 |
| Surface — market-wide feed | 9 |
| Constants | 6 (`MaxOwnerEarningsRows`), 9 (both `MaxLatestStatements*`) |
| Models (six new types) | 5 (2), 6 (1), 8 (2), 9 (1) |
| Transport (`GetObjectAsync`, `GetBytesAsync`) | 7 |
| Testing (all eight named traps) | 1, 3, 5, 8, 9 |
| Elsewhere (README, coverage tests, issue bookkeeping) | 10 |

Each of the eight tests the spec names by hand has a home: the `[JsonPropertyName]` deletion (3.7), the default
limit on all 27 paths (1.3), `period=Q1` on the wire and the undeclared-enum throw (1.3), the workbook's two
bodies (8.1), `page: 101` and `limit: 251` with `Assert.Empty(handler.Requests)` (9.1), `fiscalYear` from both
wire forms (3.1), the mixed-type as-reported dictionary (5.1), and the walker's short page (9.1).

**Three deliberate departures from the spec**, each measured after it was written and recorded in the
measurements addendum at `833e89d`:

1. **`FiscalPeriod.Quarter` is rejected on the two report-document methods.** The spec's signature block admits
   any `FiscalPeriod`. Measured 2026-08-27, FMP accepts `quarter` there and silently answers Q1 — a 58 KB
   workbook where the caller expected 785 KB. Refusing is the house rule about not letting a caller ask a
   question that quietly means something else.
2. **`FinancialReportLink.Period` is `FiscalPeriod?`, not `string?`.** The spec lists the field without pinning a
   type. It is an argument fed back into `GetFinancialReportAsync`, not a row label, and that round trip is the
   dates endpoint's only purpose.
3. **`LatestFinancialStatement.DateAdded` is `LocalDateTime?`.** The spec notes only that the wire form is
   space-separated rather than ISO-T. The zone was never measured for this field, and the SDK's two existing
   converters for that shape disagree by four or five hours, so neither can be applied without asserting an
   unmeasured fact.

**Placeholder scan.** No "TBD", no "add error handling", no "similar to Task N". Two forward references are
deliberate and each has a named closing step: Task 1 writes two crefs as `<c>` and Task 8 Step 6 converts them;
Task 1 writes `<c>MaxOwnerEarningsRows</c>` and Task 6 Step 5 converts it. Task 7 defers three transport tests to
Task 8 Step 7 because the type they bind does not exist yet, and says so.

**Type consistency.** `FullHistoryLimit` (Task 1) is used by name in Tasks 2, 4, 5, 6. `Rolling()` (Task 2) is
used by Task 6. `Periodic()` (existing, fixed in Task 1) is used by Tasks 4 and 5. `Envelope()` (Task 5) and
`Report()` (Task 8) are used only where defined. `Binding.Fixture`/`Binding.Unbound` (Task 2) are used by Tasks 3,
4, 5, 6, 8, 9. `GetObjectAsync`/`GetBytesAsync` (Task 7) are used by Task 8. Every `FmpJsonContext.Default.List*`
name a task calls is registered in that same task or an earlier one.

**Verified before writing rather than assumed:** the source generator accepts both dictionary shapes under
`IsAotCompatible`; a string in the `decimal` dictionary throws `JsonException`; `1e-05` reads as `0.00001`;
`FromCsv` covers all 237 properties across the five reused models with no orphans either way; the mapping-check
script in Task 3 Step 5 reports zero mismatches against three models that already carry both forms; and the
`balance-sheet-statement-ttm` payload omits exactly `capitalLeaseObligationsNonCurrent`.

## Task Dependency Map

Only two edges are real. Everything else can be reordered or batched.

```
Task 1  (FiscalPeriod + FullHistoryLimit + Periodic fix)
   |
   +--> Task 2  (TTM statements; also builds tests/Binding.cs)
   |       |
   |       +--> Task 6  (owner earnings — uses Rolling())
   |
   +--> Task 3  (the 237 attributes) --> Task 4  (the five methods)
   |
   +--> Task 5  (as-reported + segmentation)
   |
   +--> Task 9  (latest financial statements)

Task 7  (transport: GetObjectAsync + GetBytesAsync)  --> Task 8  (report access)

Task 10 (harnesses, README, live baseline)  <-- everything
```

Task 1 must be first: it fixes `Periodic()`, and every later task's request assertions expect
`limit=100000`. Task 2 must precede Tasks 3–6, 8 and 9 only because it creates `tests/Binding.cs`.
Task 10 must be last — the README table is generated from the whole surface and the live baseline
records it.

## Notes for the executor

- **The fixtures are evidence.** Several steps ask you to mutate one to prove a test fails. Every one
  of those says to restore it with `git checkout`. A fixture left edited turns captured measurement
  into fiction, and the next reader has no way to tell.
- **`EndpointCoverageTests` fails from Task 2 until Task 10.** That is expected and each task says so.
  Do not regenerate the README early to make it green — the table would then be regenerated again at
  every task, and its diff is the only cross-check that all 19 paths actually became reachable.
- **One live call, in Task 10 Step 6, and no bulk calls at all.** Everything else runs offline against
  the committed fixtures.
- **`limit is <= 0` in `Periodic()` and `Rolling()` rejects zero and negatives but not the null
  default** — that is the existing behaviour and it stays. `null` now means `FullHistoryLimit`.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-27-statements.md`. Two execution options:

**1. Subagent-Driven (recommended)** — a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — tasks executed in this session using executing-plans, batch execution with checkpoints.
