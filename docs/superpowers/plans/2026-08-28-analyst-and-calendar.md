# Analyst and Calendar Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model the fourteen documented `stable/` paths FMP files under Analyst and Calendar, taking SDK coverage from 126 of 243 paths to 140.

**Architecture:** No new facade. Seven paths join `fmp.Analyst` (1 → 8 methods) and seven join `fmp.Calendar` (2 → 9); both files are small today and neither approaches the size at which `CompanyEndpoints` still reads well. Every one of the fourteen is an ordinary `GET` returning a JSON array that `FmpTransport.GetListAsync` already serves: no new transport primitive, no streaming, no CSV. Eleven new records, one generic result type, one new converter, eleven `FmpJsonContext` entries. Every type is built from the 2026-08-28 measurement pass rather than from FMP's documentation, and each measured trap gets a test that fails when the trap is reintroduced.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` source generation via `FmpJsonContext`, NodaTime (`LocalDate`), xUnit v2 (2.9.3).

**Spec:** `docs/superpowers/specs/2026-08-28-analyst-and-calendar-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-28-analyst-and-calendar-measurements.md`

## Global Constraints

- `TreatWarningsAsErrors=true` (`Directory.Build.props`) covers `CS*` and `NU*`. `IsAotCompatible` turns IL2026/IL3050 into build errors — **never** call a reflection-based `JsonSerializer.Deserialize`; every model and every nested parse goes through `FmpJsonContext`.
- **Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as `[JsonSerializable(typeof(List<X>))]` or it fails at runtime, not at compile time.** Eleven entries are added across this plan: `Dividend`, `StockSplit`, `CompanyRating`, `StockGrade`, `GradeConsensus`, `GradeHistory`, `PriceTargetConsensus`, `PriceTargetSummary`, `IpoCalendarEntry`, `IpoDisclosure`, `IpoProspectus`. `List<string>` is **already** registered (it serves `BulkPriceTargetSummary`) and must not be added twice.
- **`CS1574` is a build error here.** An unresolved `<see cref>` fails the build under `TreatWarningsAsErrors`. Never write a `cref` to a member a *later* task creates. This bit the previous slice twice.
- Models are `public sealed record` with `init` properties and an explicit `[JsonPropertyName]` on every member. **No `required` members and no non-nullable properties** — an absent JSON key binds an `init` member to `default` rather than honouring a field initialiser.
- `cik` is **`string?`, never an integer type.** Measured 2026-08-28 it arrives zero-padded to ten characters (`"0001610590"`), and parsing it loses the padding EDGAR uses.
- **Money, market caps and share counts are `decimal?`.** Measured maxima in this slice: `ipos-calendar.marketCap` 74,999,999,925 and `ipos-prospectus.pricePublicTotal` 74,999,999,925 — about thirty-five times `int.MaxValue` (2,147,483,647). An `int?` property does **not** read an out-of-range value as null: `System.Text.Json` throws, and `FmpTransport` does not wrap `DeserializeAsync`, so one row costs the whole response. This matches `MarketCapitalization.MarketCap` and `SharesFloat.OutstandingShares`, which are `decimal?` for the same reason.
- Dates carrying no time of day are `LocalDate?` via the existing `NullableLocalDateJsonConverter`. It reads a blank string as null without throwing — `LocalDatePattern.Iso.Parse("")` fails, `parsed.Success` is false, and the converter answers null. That is what makes the 2,232-of-4,000 blank `declarationDate` rows safe.
- **`NullableEasternInstantJsonConverter` must not be used anywhere in this slice.** `acceptedDate` on `ipos-disclosure` and `ipos-prospectus` is a plain 10-character date, measured 10 characters on 8,856 and 165 rows respectively — not the 19-character `uuuu-MM-dd HH:mm:ss` stamp `SecFiling.AcceptedDate` carries. The same field name means a different thing in a different endpoint family, and pointing the Eastern converter at it answers null for every row without erroring.
- **No enums.** `frequency`, `action`, `newGrade`, `splitType`, `actions`, `exchange`, `consensus`, `rating` and `form` all stay `string?`. Each observed value set is a sample from one path and one symbol, not a domain — `frequency` alone shows 2 distinct values on `dividends` and 8 on `dividends-calendar`.
- A signature must not accept a parameter the endpoint ignores. Measured ignored in this slice: `from`/`to` on all five per-symbol paths, and `limit` *and* `page` on `grades`.
- Every public member carries XML documentation in house style: it records **what was measured and on what date** (every measurement in this slice is 2026-08-28 against an Ultimate key), and states plainly anything a caller would otherwise get wrong. Where a value is a trap, the documentation is the deliverable, not decoration.
- Public list-returning methods return `IReadOnlyList<T>`, never null. Single-row lookups return `T?`, because an unknown-but-well-formed symbol answers an empty array with HTTP 200 rather than a 404.
- Tests are xUnit `[Fact]`/`[Theory]` with sentence-style method names using underscores, matching `CalendarEndpointsTests`. Two conventions in that suite are load-bearing rather than stylistic, and both are build or test failures if ignored:
  - **`Assert.ThrowsAsync<T>` matches the exception type exactly.** `ArgumentException.ThrowIfNullOrWhiteSpace(null)` throws `ArgumentNullException`, which does **not** satisfy `ThrowsAsync<ArgumentException>`. Follow `AnalystEndpointsTests`: a `[Theory]` over `""` and `"  "` asserting `ArgumentException`, and a separate `[Fact]` for `null` asserting `ArgumentNullException`. Never put `[InlineData(null)]` in the blank-symbol theory.
  - **An `async` test method with no `await` is `CS1998`, and therefore a build error here.** Reflection-only assertions (checking that a signature does *not* declare a parameter, for instance) must be plain `void` `[Fact]`s.
- **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the response after reading the body, so the second call fails with `ObjectDisposedException`. A test driving N calls builds N responses.
- Fixtures are verbatim captures from the 2026-08-28 pass and **must not contain the API key**. The key travels in the query string, so never write a built URL into a fixture or a log line. The `Fixtures\*.json` glob in `FmpDotNet.Tests.csproj` copies them automatically — **no csproj change is needed.**
- Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore. **Restore with `cp`, never `mv`** — `mv` gives the restored file an mtime older than the built DLL, so `dotnet build` skips recompiling and the next test run reads a stale assembly that still contains the mutation. Verify with `diff` and rebuild with `--no-incremental`.
- **`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` goes red at Task 2 and stays red until Task 10.** It drives every endpoint method against a stub and compares the paths against the README's generated table, so it fails the moment the first new endpoint ships and cannot pass again until the table is regenerated. Every per-task run below is filtered to the tests that task owns; a full-suite run between Task 2 and Task 10 is expected to show exactly that one failure and no other.
- **`EndpointCoverageTests.Argument` needs no new cases**, and that is worth checking rather than assuming: its `string` arm ends `_ => "AAPL"`, its `int` arm returns 5 for `limit`, and it supplies `new LocalDate(2026, 1, 2)` for every `LocalDate` — so `from` equals `to`, which is not backwards and passes the guard. That harness only records which path went out, so a meaningless-but-valid value is harmless there. The **live sweep** is the harness where a meaningless value does harm, and Task 9 is where that is fixed.
- **The keyless `SweepCoverageTests` stay green throughout Tasks 2–8**, and that was checked rather than assumed. `Probe.Argument` already has cases for every parameter name and type the fourteen new methods introduce — `symbol`, `limit` (an `int?` unwraps to `int` before dispatch), `from`, `to` — and `Probe.ElementType` already handles both `Task<IReadOnlyList<T>>` and the single-row `Task<T?>` shape, which shipped methods like `CompanyEndpoints.GetProfileAsync` already use. So a mid-plan run of `dotnet test tests/FmpDotNet.SmokeTests` should be green with no key. The *quality* of the arguments is the thing Task 9 fixes, and no keyless test can see that.
- **`AddFmpTests` needs no change** — this slice adds no facade, and that test asserts facade presence, not method counts. Verified 2026-08-28.
- Work happens on a branch off `master`. `master` carries a ruleset requiring a pull request and the `.NET — build + test` check, so the path is branch → PR → green → merge. Suggested branch name: `feat/analyst-and-calendar-coverage`.

## File Structure

**Create:**
- `src/FmpDotNet/Models/CalendarResult.cs` — `CalendarResult<T>`, the generic truncation-reporting list (Task 1)
- `src/FmpDotNet/Models/Dividend.cs` — `Dividend`, serving `dividends` and `dividends-calendar`
- `src/FmpDotNet/Models/StockSplit.cs` — `StockSplit`, serving `splits` and `splits-calendar`
- `src/FmpDotNet/Models/Ipo.cs` — `IpoCalendarEntry`, `IpoDisclosure`, `IpoProspectus`
- `src/FmpDotNet/Models/StockGrade.cs` — `StockGrade`, `GradeConsensus`, `GradeHistory`
- `src/FmpDotNet/Models/PriceTarget.cs` — `PriceTargetConsensus`, `PriceTargetSummary`
- `src/FmpDotNet/Models/CompanyRating.cs` — `CompanyRating`, serving `ratings-snapshot` and `ratings-historical`
- `tests/FmpDotNet.Tests/CalendarResultTests.cs` — the generic result type, on its own
- `tests/FmpDotNet.Tests/DividendTests.cs` — the dividend record and both dividend methods
- `tests/FmpDotNet.Tests/StockSplitTests.cs` — the split record and both split methods
- `tests/FmpDotNet.Tests/IpoTests.cs` — the three IPO records and their three methods
- `tests/FmpDotNet.Tests/StockGradeTests.cs` — the three grade records and their three methods
- `tests/FmpDotNet.Tests/PriceTargetTests.cs` — the converter, both price-target records, both methods
- `tests/FmpDotNet.Tests/CompanyRatingTests.cs` — the shared rating record and both rating methods
- 16 fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modify:**
- `src/FmpDotNet/Serialization/NodaConverters.cs` — +`PublisherListJsonConverter` (Task 7)
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +11 entries, spread across Tasks 2–8
- `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` — +7 methods (Tasks 2–5)
- `src/FmpDotNet/Endpoints/AnalystEndpoints.cs` — +7 methods (Tasks 6–8)
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — +1 constant (Task 9)
- `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()`'s `from` arm dispatches per method (Task 9)
- `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` — +2 keyless guards (Task 9)
- `README.md` — regenerated coverage table and the remaining-work paragraph (Task 10)
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded (Task 10)

**Not modified, and each was checked rather than assumed:** `FmpClient.cs` and `FmpServiceCollectionExtensions.cs` (no new facade), `AddFmpTests.cs` (asserts facade presence only), `FmpDotNet.Tests.csproj` (the fixture glob is a wildcard), `EndpointCoverageTests.cs` (its `Argument` covers every parameter name and type this slice introduces), `EarningsCalendarResult.cs` and `CalendarEndpoints.GetEarningsCalendarAsync` (explicitly out of scope).

---

## Corrections and rulings made against the spec while planning

Six. Four are measurement corrections and were written back into the spec and the measurements document in commit `f374021`, because a measurements file with a false measurement in it is cited as evidence and is worse than a plan deviation. The fifth is a design consequence of the fourth, and the sixth is a ruling on a signature default. Everything not listed here follows the spec as written.

**1. `splitType` never carries the literal string `"None"`.** The spec's trap #8 read that `splitType` is "null *and* the literal `"None"`", citing "the same `"None"` sentinel measured on the SEC filing paths in the previous slice". Re-measured field by field over all 961 `splits-calendar` rows and all five of their fields: `stock-split` ×934, JSON-null ×16, `stock-dividend` ×10, `spin-off` ×1, and the string `"None"` appears **zero times anywhere in the response**. The sentinel is real and it belongs to the previous slice's classification paths, where `symbol` reads `"None"`; it was conflated in the write-up. Trap #8 shrinks to "JSON-null on 16 of 961 rows", which is still worth a test, and the trap fixture carries nulls rather than a string that does not exist. Cost if wrong: a caller comparing against `"None"` finds nothing — which is correct, since there is nothing to find.

**2. `ipos-calendar.priceRange` is a formatted string, not a number.** The spec's null survey lists it as "null ×441" and its JSON-types table omits it from `ipos-calendar`'s string-typed fields, so the natural reading is `decimal?`. The nine populated values are strings in all nine: `"5.00 - 7.00"`, `"10.00"`, `"15 - 17"`, `"8.00 - 10.00"`, `"11.25 - 13.25"`, `"16.00 - 18.00"`, `"15.00 - 17.00"` — six ranges and three single prices. Typed `decimal?` the property would read **null on all 450 rows**: null where FMP sent null, and null where FMP sent a price, with nothing in the data to tell them apart. It is the same shape as `SecProfile.FiftyTwoWeekRange` from the previous slice. Ruling: `string?`. Cost if wrong: a caller wanting a number parses the string at their own call site, where they can see the two forms.

**3. Four numeric fields exceed `int` and must be `decimal?`.** A magnitude sweep the first pass did not run: `ipos-calendar.marketCap` reaches **74,999,999,925**, `ipos-prospectus.pricePublicTotal` reaches **74,999,999,925**, and `proceedsBeforeExpensesTotal` reaches **74,499,999,925** — each about thirty-five times `int.MaxValue`. Three prospectus fields also arrive fractional on some rows and integral on others. `shares` fits `int` at a measured maximum of 555,555,555, but it is a share count and takes `decimal?` with the rest, matching `SharesFloat.OutstandingShares`. `numerator`/`denominator` on the split paths were checked against the same limit and **fit** — 1,011,977 and 1,000,000 — so they stay `int?`, which is the opposite ruling from the same evidence and is why both were measured rather than assumed. Cost if wrong: none observed; `decimal?` reads every measured value exactly.

**4. `splits-calendar` and `ipos-calendar` truncate too — by a 90-day window, not a row cap.** The spec says "the other three calendar-shaped paths do not hit the cap", which is true and was inferred to mean they do not truncate, which is false. Measured against four `to` values spanning twenty months, with `from` fixed at 2015-01-01 in every call, the earliest row returned is **exactly 90 days before `to`** every time:

| `to` | `splits-calendar` | earliest | `ipos-calendar` | earliest | gap |
|---|---|---|---|---|---|
| 2024-12-31 | 737 rows | 2024-10-02 | 358 rows | 2024-10-02 | 90 days |
| 2025-06-30 | 620 rows | 2025-04-01 | 446 rows | 2025-04-01 | 90 days |
| 2026-03-31 | 632 rows | 2025-12-31 | 449 rows | 2025-12-31 | 90 days |
| 2026-08-28 | 947 rows | 2026-05-31 | 443 rows | 2026-06-01 | 89 / 88 days |

**A request for the whole of 2024 answers Q4 of 2024.** Nine months are silently absent and the caller is told nothing. Walking `from` back against a fixed `to` shows the edge: -88 days is honoured exactly, -90 saturates, and -100, -120 and -180 all return the identical 947 rows with the identical earliest date. `ipos-disclosure` and `ipos-prospectus` do **not** do this — both answered a full 2024 with the full year, 25,689 and 1,048 rows. So three of the five date-ranged paths truncate, by two mechanisms. Cost if wrong: the SDK reports a truncation that did not happen, which costs a caller one narrowed retry; the untaken alternative costs them nine months of data they believe they have.

**5. One generic `CalendarResult<T>` replaces the spec's single `DividendCalendarResult`.** This follows from #4. Three paths now need the signal, and the two mechanisms need different tells: 737 rows is nowhere near any cap, so `AtRowCap` is blind to the window clamp, and only `MissesStartOfRange` — the earliest returned row is later than the requested `from` — sees both. Three hand-mirrored copies of one result type would be verbatim duplication of a logic block, which the review rubric treats as a defect, and copies two and three would differ only in which tell can fire. `RowCap` and `LookbackLimitDays` therefore become nullable per-instance values rather than a `const`, each null where nothing was measured. **Every public signature in the spec is unchanged**, because `CalendarResult<T>` implements `IReadOnlyList<T>`. `EarningsCalendarResult` is left exactly as shipped — retrofitting it onto the generic is public API surgery on a shipped path with its own tests, and is a deliberate follow-up rather than a rider on a coverage slice. Cost if wrong: one generic class instead of three near-identical ones; if a fourth mechanism ever appears it gets a third nullable tell rather than a fourth file.

**6. `limit` defaults to `null`, not to 100, on three of the four methods that take it.** The spec's signature block writes `int limit = 100` on `GetDividendsAsync`, `GetSplitsAsync`, `GetGradeHistoryAsync` and `GetRatingHistoryAsync`. Its stated reason applies to exactly one of them: `ratings-historical` answers **one row** when `limit` is absent, from an endpoint whose name promises a series, so a default is the only usable choice there. The other three return the **whole series** when `limit` is absent — measured for AAPL at 92 dividends, 5 splits and 92 grade-history rows, each unchanged by `limit=10000`. Defaulting those to 100 would make the SDK silently truncate a series FMP was willing to hand over in full, which is the same class of defect as everything else this slice is guarding against, arriving through a default argument. The shipped precedent agrees: `CalendarEndpoints.GetEarningsAsync` takes `int? limit = null` and documents "without it you get everything". Ruling:

| method | signature | why |
|---|---|---|
| `GetDividendsAsync` | `int? limit = null` | absent → all 92 |
| `GetSplitsAsync` | `int? limit = null` | absent → all 5 |
| `GetGradeHistoryAsync` | `int? limit = null` | absent → all 92 |
| `GetRatingHistoryAsync` | **`int limit = 100`** | absent → **1** |

Cost if wrong: a caller who wanted a hundred rows and did not say so gets the whole series — visible in the count they hold, and one `limit:` argument to fix. The untaken alternative loses rows and says nothing.

## A count the spec got wrong

The spec's testing section says "the **two** new date-ranged Calendar methods must be checked against that dispatch". There are **five**: `GetDividendsCalendarAsync`, `GetSplitsCalendarAsync`, `GetIpoCalendarAsync`, `GetIpoDisclosuresAsync` and `GetIpoProspectusesAsync`. All five land on `Probe.Argument`'s `from` arm, whose `CalendarEndpoints` case currently answers `LiveApi.SettledWeekday` for both ends — a **one-day** window. Measured at that width on 2026-08-21:

| method | rows at 1 day | rows at 7 days |
|---|---|---|
| `GetDividendsCalendarAsync` | 331 | 1,652 |
| `GetSplitsCalendarAsync` | 12 | 40 |
| `GetIpoCalendarAsync` | 5 | 34 |
| `GetIpoDisclosuresAsync` | 116 | 764 |
| `GetIpoProspectusesAsync` | **1** | 8 |

Nothing records zero *today*, so this is not yet the silent-green failure the previous slice fixed — it is one quiet week away from being it, on a suite that runs unattended. Task 9 widens the four sparse paths to a week and pins the widths with a keyless test. `GetEarningsCalendarAsync` keeps its one-day window: its own documentation measures a 7-day peak-season window at 3,676 rows, 92% of the 4000 cap, which is exactly what the previous slice narrowed it away from.

---

### Task 1: `CalendarResult<T>`, the truncation-reporting list

Three of this slice's five date-ranged methods return it. It is built first and alone, because Tasks 2, 3 and 4 all consume it and none of them should be the place its arithmetic is debugged.

**Files:**
- Create: `src/FmpDotNet/Models/CalendarResult.cs`
- Test: `tests/FmpDotNet.Tests/CalendarResultTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal CalendarResult(IReadOnlyList<T> rows, int rowsReturned, LocalDate requestedFrom, LocalDate requestedTo, LocalDate? earliestReturnedDate, int? rowCap, int? lookbackLimitDays)` — the seven-argument constructor Tasks 2, 3 and 4 call, in that order. Public surface: `Count`, `this[int]`, `GetEnumerator`, `RowsReturned`, `RequestedFrom`, `RequestedTo`, `EarliestReturnedDate`, `RowCap`, `LookbackLimitDays`, `AtRowCap`, `ExceedsLookbackLimit`, `MissesStartOfRange`, `LikelyTruncated`, and `static bool IsLikelyTruncated(IReadOnlyList<T>)`.

**One ruling made here, on a member the spec named but did not specify.** `EarningsCalendarResult.IsLikelyTruncated` falls back to `rows.Count >= RowCap` when handed a list it did not produce, which is defensible only because that type has a single known `const RowCap = 4000`. `CalendarResult<T>` has no such constant — its cap is per-instance and null on two of the three paths — so there is no honest fallback. The static helper therefore answers **`false`** for a foreign list, and its documentation says so in as many words rather than leaving a caller to assume the check was exact. A test pins that behaviour. The alternative — inventing a fallback threshold — would be a fact nobody measured, answering "complete" about a list it cannot see the evidence for.

- [ ] **Step 1: Write the failing tests**

`tests/FmpDotNet.Tests/CalendarResultTests.cs`:

```csharp
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The generic truncation signal, exercised on its own.
///
/// <para><c>T</c> is <see cref="string"/> throughout, deliberately. This class does arithmetic on four values
/// the endpoint hands it — a raw row count, the two requested bounds and the earliest date anywhere in the raw
/// response — and none of that arithmetic touches the row type. Using a real model here would only invite a
/// reader to think the type matters.</para>
///
/// <para>The three paths that return one, and what each was measured to do on 2026-08-28:</para>
/// <list type="bullet">
/// <item><description><c>dividends-calendar</c> — a 4000-row cap. A full year answers its last three days.</description></item>
/// <item><description><c>splits-calendar</c> and <c>ipos-calendar</c> — a 90-day window measured from
/// <c>to</c>. A full year answers Q4, at 737 and 358 rows: nowhere near any cap, so a row count cannot see
/// it.</description></item>
/// </list></summary>
public class CalendarResultTests
{
    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    private static CalendarResult<string> Result(
        int rowsReturned, LocalDate from, LocalDate to, LocalDate? earliest,
        int? rowCap = null, int? lookback = null, IReadOnlyList<string>? rows = null) =>
        new(rows ?? [], rowsReturned, from, to, earliest, rowCap, lookback);

    // ---- it is a list first -------------------------------------------------------------------------------

    [Fact]
    public void It_is_the_list_it_was_given()
    {
        var result = Result(3, Day(2026, 1, 1), Day(2026, 1, 31), Day(2026, 1, 1),
                            rows: ["a", "b", "c"]);

        Assert.Equal(3, result.Count);
        Assert.Equal("b", result[1]);
        Assert.Equal(["a", "b", "c"], result);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result);
    }

    [Fact]
    public void Count_is_what_the_caller_holds_and_RowsReturned_is_what_FMP_sent()
    {
        // The distinction the whole type exists for: a row dropped by the SDK must not be able to move the
        // signal. Two rows kept out of five returned.
        var result = Result(5, Day(2026, 1, 1), Day(2026, 1, 31), Day(2026, 1, 1), rows: ["a", "b"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result.RowsReturned);
    }

    // ---- AtRowCap: the dividends-calendar mechanism -------------------------------------------------------

    [Theory]
    [InlineData(3999, false)]
    [InlineData(4000, true)]
    [InlineData(4001, true)]
    public void AtRowCap_fires_at_and_above_the_cap(int rowsReturned, bool expected)
    {
        var result = Result(rowsReturned, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 1, 1), rowCap: 4000);

        Assert.Equal(expected, result.AtRowCap);
    }

    [Fact]
    public void AtRowCap_never_fires_where_no_cap_was_measured()
    {
        // splits-calendar and ipos-calendar pass rowCap: null, because no row cap was measured on them and an
        // invented one would be a fact nobody checked. 100000 rows must still read false here.
        var result = Result(100_000, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 1, 1), rowCap: null);

        Assert.False(result.AtRowCap);
    }

    // ---- ExceedsLookbackLimit: the splits/ipos mechanism --------------------------------------------------

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, false)]
    [InlineData(91, true)]
    [InlineData(364, true)]
    public void ExceedsLookbackLimit_fires_on_a_range_wider_than_the_window(int spanDays, bool expected)
    {
        var from = Day(2026, 1, 1);
        var result = Result(500, from, from.PlusDays(spanDays), from, lookback: 90);

        Assert.Equal(expected, result.ExceedsLookbackLimit);
    }

    [Fact]
    public void ExceedsLookbackLimit_never_fires_where_no_window_was_measured()
    {
        // dividends-calendar passes lookback: null. Its row cap always fires first, so no window limit is
        // observable on it, and asserting one would be inventing evidence.
        var result = Result(4000, Day(2020, 1, 1), Day(2026, 12, 31), Day(2026, 12, 29), lookback: null);

        Assert.False(result.ExceedsLookbackLimit);
    }

    // ---- MissesStartOfRange: the only tell that sees both mechanisms --------------------------------------

    [Fact]
    public void MissesStartOfRange_fires_when_the_earliest_row_is_later_than_the_requested_from()
    {
        // The measured splits-calendar case: from=2024-01-01 to=2024-12-31 answered 737 rows whose earliest
        // date was 2024-10-02. Nine months absent, at a row count nowhere near any cap.
        var result = Result(737, Day(2024, 1, 1), Day(2024, 12, 31), Day(2024, 10, 2), rowCap: null, lookback: 90);

        Assert.True(result.MissesStartOfRange);
        Assert.False(result.AtRowCap);          // 737 is not near a cap, and there is no cap here anyway
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void MissesStartOfRange_is_the_tell_that_catches_the_ninety_day_boundary()
    {
        // Measured 2026-08-28: from = to - 90 days is one day short — the response's earliest row was
        // 2026-05-31 against a requested 2026-05-30 — while from = to - 88 was honoured exactly. A span of
        // exactly 90 therefore does NOT trip ExceedsLookbackLimit, and this tell is what covers it.
        var result = Result(947, Day(2026, 5, 30), Day(2026, 8, 28), Day(2026, 5, 31), lookback: 90);

        Assert.False(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void MissesStartOfRange_does_not_fire_when_the_range_starts_where_it_was_asked_to()
    {
        var result = Result(946, Day(2026, 6, 1), Day(2026, 8, 28), Day(2026, 6, 1), lookback: 90);

        Assert.False(result.MissesStartOfRange);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public void An_empty_response_carries_no_earliest_date_and_reads_as_untruncated()
    {
        // Known and accepted: nothing came back at all, so there is no evidence of truncation and none of
        // completeness either. False is the answer that does not invent a signal.
        var result = Result(0, Day(2026, 1, 1), Day(2026, 1, 31), earliest: null, rowCap: 4000, lookback: null);

        Assert.Null(result.EarliestReturnedDate);
        Assert.False(result.MissesStartOfRange);
        Assert.False(result.LikelyTruncated);
    }

    // ---- the static helper --------------------------------------------------------------------------------

    [Fact]
    public void The_static_helper_reads_a_result_this_sdk_produced()
    {
        IReadOnlyList<string> rows = Result(4000, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 12, 29),
                                            rowCap: 4000);

        Assert.True(CalendarResult<string>.IsLikelyTruncated(rows));
    }

    [Fact]
    public void The_static_helper_answers_false_for_a_list_it_did_not_produce()
    {
        // Documented rather than hidden. EarningsCalendarResult can fall back on `Count >= 4000` because it has
        // one known const cap; this type's cap is per-instance and null on two of its three paths, so there is
        // no honest fallback. A concatenation of chunks has thrown away the per-response evidence, and false
        // here means "no evidence", not "complete".
        IReadOnlyList<string> plain = new string[10_000];

        Assert.False(CalendarResult<string>.IsLikelyTruncated(plain));
    }

    [Fact]
    public void The_static_helper_refuses_null()
    {
        Assert.Throws<ArgumentNullException>(() => CalendarResult<string>.IsLikelyTruncated(null!));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CalendarResultTests"`
Expected: the build fails — `CS0246: The type or namespace name 'CalendarResult<>' could not be found`. A compile failure is the correct first result here; there is nothing yet to run.

- [ ] **Step 3: Write the implementation**

`src/FmpDotNet/Models/CalendarResult.cs`:

```csharp
using System.Collections;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>The rows one date-ranged calendar call returned, plus the evidence needed to judge whether they are
/// all of them.
///
/// <para>This is the list — it implements <see cref="IReadOnlyList{T}"/> and every method returning one declares
/// that as its return type, so nothing about the ordinary path changes. It exists because three of FMP's
/// calendar paths truncate silently, and a bare list of rows cannot tell a caller whether it is looking at an
/// answer or at the tail of one.</para>
///
/// <code>
/// var rows = await fmp.Calendar.GetSplitsCalendarAsync(from, to);
/// if (rows is CalendarResult&lt;StockSplit&gt; { LikelyTruncated: true }) { /* narrow the range and retry */ }
/// </code>
///
/// <para><b>Two different mechanisms, measured 2026-08-28, and they need different tells.</b></para>
///
/// <list type="bullet">
/// <item><description><c>dividends-calendar</c> caps at <b>4000 rows</b>. A request for the whole of 2025
/// answered 4000 rows whose earliest date was 2025-12-29 — the last three days of the year. <c>limit=10000</c>
/// was accepted and ignored. <see cref="RowCap"/> is 4000 and <see cref="LookbackLimitDays"/> is null: the cap
/// always fires first at 340–876 rows a day, so no window limit is observable on this path and asserting one
/// would be inventing evidence.</description></item>
/// <item><description><c>splits-calendar</c> and <c>ipos-calendar</c> clamp to a <b>90-day window measured from
/// <c>to</c></b>. Across four <c>to</c> values spanning twenty months, each with <c>from</c> fixed at
/// 2015-01-01, the earliest row returned was exactly 90 days before <c>to</c> every time. A request for the
/// whole of 2024 answered Q4 of 2024 — <b>737 and 358 rows</b>, nowhere near any cap, which is why
/// <see cref="AtRowCap"/> is blind to it. <see cref="LookbackLimitDays"/> is 90 and <see cref="RowCap"/> is
/// null.</description></item>
/// </list>
///
/// <para><b>Everything here is measured on the raw response, before the SDK clamps or drops anything.</b> That
/// ordering is the whole point and it is not a detail: clamp first and a genuinely truncated response whose
/// overshoot rows the clamp removed arrives at its detector already reduced, and is judged complete.
/// <see cref="Count"/> is what the caller was handed; <see cref="RowsReturned"/> is what FMP sent, and only the
/// second can answer the question.</para>
///
/// <para><c>stable/earnings-calendar</c> has the same defect and its own type,
/// <see cref="EarningsCalendarResult"/>, which shipped first and is deliberately left alone. Folding it into
/// this generic is public API surgery on a shipped path and is a separate decision.</para></summary>
/// <typeparam name="T">The row type the calendar path returns.</typeparam>
public sealed class CalendarResult<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _rows;

    internal CalendarResult(
        IReadOnlyList<T> rows,
        int rowsReturned,
        LocalDate requestedFrom,
        LocalDate requestedTo,
        LocalDate? earliestReturnedDate,
        int? rowCap,
        int? lookbackLimitDays)
    {
        _rows = rows;
        RowsReturned = rowsReturned;
        RequestedFrom = requestedFrom;
        RequestedTo = requestedTo;
        EarliestReturnedDate = earliestReturnedDate;
        RowCap = rowCap;
        LookbackLimitDays = lookbackLimitDays;
    }

    /// <summary>How many rows the caller is holding, after any rows with no usable date were dropped. Compare
    /// against <see cref="RowsReturned"/> to see what the SDK removed.</summary>
    public int Count => _rows.Count;

    /// <inheritdoc/>
    public T this[int index] => _rows[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>How many rows FMP's response actually carried, counted before the SDK dropped anything. This,
    /// and not <see cref="Count"/>, is what the truncation tells are computed from.</summary>
    public int RowsReturned { get; }

    /// <summary>The <c>from</c> that was asked for.</summary>
    public LocalDate RequestedFrom { get; }

    /// <summary>The <c>to</c> that was asked for.</summary>
    public LocalDate RequestedTo { get; }

    /// <summary>The earliest date anywhere in the raw response, or <see langword="null"/> if it carried no
    /// dated row. Raw, so nothing the SDK does can move it.</summary>
    public LocalDate? EarliestReturnedDate { get; }

    /// <summary>FMP's undocumented hard cap on this path's response, or <see langword="null"/> where no cap was
    /// measured. 4000 on <c>dividends-calendar</c>; null on the two window-clamped paths.</summary>
    public int? RowCap { get; }

    /// <summary>How far back this path will reach from <see cref="RequestedTo"/>, or <see langword="null"/>
    /// where no window limit was measured. 90 on <c>splits-calendar</c> and <c>ipos-calendar</c>; null on
    /// <c>dividends-calendar</c>, whose row cap always fires first.</summary>
    public int? LookbackLimitDays { get; }

    /// <summary>The response came back at or above <see cref="RowCap"/>, so it is almost certainly cut short.
    ///
    /// <para>Always <see langword="false"/> where <see cref="RowCap"/> is null. Exact at the cap and blind just
    /// under it, so a false reading here is "complete" and never "truncated";
    /// <see cref="MissesStartOfRange"/> is the tell that covers the near-cap case.</para></summary>
    public bool AtRowCap => RowCap is { } cap && RowsReturned >= cap;

    /// <summary>The requested range is wider than this path will serve, so its front was dropped.
    ///
    /// <para>Always <see langword="false"/> where <see cref="LookbackLimitDays"/> is null. Note that a span of
    /// <i>exactly</i> the limit reads <see langword="false"/> here and still loses a day: measured 2026-08-28,
    /// <c>from = to - 90</c> answered an earliest row of 2026-05-31 against a requested 2026-05-30, while
    /// <c>from = to - 88</c> was honoured exactly. <see cref="MissesStartOfRange"/> catches that
    /// boundary.</para></summary>
    public bool ExceedsLookbackLimit =>
        LookbackLimitDays is { } limit && Period.DaysBetween(RequestedFrom, RequestedTo) > limit;

    /// <summary>The earliest row returned is later than the first day asked for, although something came back.
    ///
    /// <para><b>The only tell that sees both mechanisms.</b> Both of them drop rows from the <i>front</i> of the
    /// range, and this compares what arrived against what was asked for, so it does not care which one did it.
    /// A row cap of 4000 is invisible to it only when the cap is not reached, and a 90-day clamp is invisible to
    /// a row count entirely.</para>
    ///
    /// <para><b>Known false positive:</b> a range whose first days are a weekend, a holiday or simply quiet
    /// legitimately has nothing on them, and this reads <see langword="true"/> anyway. That is the deliberate
    /// direction to be wrong in — a caller re-requesting a narrower range that was fine loses a request,
    /// whereas the opposite loses rows.</para></summary>
    public bool MissesStartOfRange => EarliestReturnedDate is { } earliest && earliest > RequestedFrom;

    /// <summary>Any tell fired, so treat these rows as incomplete and narrow the range.
    ///
    /// <para><b>Safe widths, measured 2026-08-28.</b> <c>dividends-calendar</c> ran 340–876 rows a day, so the
    /// cap falls somewhere between five and eleven days depending on the season — a six-day window returned 2147
    /// and was complete, a thirty-day window was not. <c>splits-calendar</c> and <c>ipos-calendar</c> are flat
    /// 90 days regardless of season. The SDK reports rather than chunks: see the remarks on the methods that
    /// return this type.</para></summary>
    public bool LikelyTruncated => AtRowCap || ExceedsLookbackLimit || MissesStartOfRange;

    /// <summary>Whether a calendar result should be treated as cut short, for callers holding it as a plain
    /// <see cref="IReadOnlyList{T}"/>.
    ///
    /// <para><b>Answers <see langword="false"/> for any list this SDK did not produce, and that means "no
    /// evidence" rather than "complete".</b> The per-response evidence — <see cref="RowsReturned"/> and
    /// <see cref="EarliestReturnedDate"/>, both taken raw — lives on the instance, and a test double, a
    /// concatenation of several chunks or a list a caller has already filtered has thrown it away. There is no
    /// fallback to offer: this type's cap is per-instance and null on two of the three paths that return it, so
    /// a row-count threshold here would be a number nobody measured. (<see cref="EarningsCalendarResult"/> can
    /// fall back on <c>Count &gt;= 4000</c> only because it has one known cap.) Test each chunk as it arrives,
    /// not the concatenation.</para></summary>
    /// <param name="rows">The rows to judge.</param>
    public static bool IsLikelyTruncated(IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows is CalendarResult<T> { LikelyTruncated: true };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CalendarResultTests"`
Expected: PASS — 13 test methods, **18 test cases** (11 `[Fact]`, plus 3 and 4 `[InlineData]` rows on the two `[Theory]` methods). Zero warnings.

- [ ] **Step 5: Mutation-check the two tells that are easy to get subtly wrong**

**Mutation A — `AtRowCap` uses `>` instead of `>=`.** Edit `AtRowCap` to `RowsReturned > cap`.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CalendarResultTests"`
Expected: exactly **1** failure — `AtRowCap_fires_at_and_above_the_cap(rowsReturned: 4000, expected: True)`. The 3999 and 4001 rows still pass, which is the point of the three-row theory: an off-by-one at the cap is invisible to any single-value test. Restore with `cp` from git, verify with `diff`, and rebuild with `--no-incremental`.

**Mutation B — `MissesStartOfRange` compares against `RequestedTo`.** Edit it to `earliest > RequestedTo`.

Run the same filter.
Expected: exactly **2** failures — `MissesStartOfRange_fires_when_the_earliest_row_is_later_than_the_requested_from` and `MissesStartOfRange_is_the_tell_that_catches_the_ninety_day_boundary`. Work through why the other three survive, because each one is informative: `MissesStartOfRange_does_not_fire_when_the_range_starts_where_it_was_asked_to` asserts `false` and still gets `false`; `An_empty_response_carries_no_earliest_date_and_reads_as_untruncated` has a null earliest date, which short-circuits before the comparison; and `The_static_helper_reads_a_result_this_sdk_produced` passes `rowCap: 4000` at 4000 rows, so `AtRowCap` carries `LikelyTruncated` on its own. That last one is the reason the two dedicated `MissesStartOfRange` tests exist at all — a tell that is only ever asserted through `LikelyTruncated` can be broken without any test noticing. Restore as above.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Models/CalendarResult.cs tests/FmpDotNet.Tests/CalendarResultTests.cs
git commit -m "feat: CalendarResult<T>, the truncation signal three calendar paths need (#37)"
```

---

### Task 2: `Dividend`, and the two paths that return it

One record, two paths, measured byte-identical field sets. This is also the task that first ships an endpoint, so `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` goes red here and stays red until Task 10 regenerates the table. Expect exactly that one failure on any full-suite run in between, and no other.

**Files:**
- Create: `src/FmpDotNet/Models/Dividend.cs`, `tests/FmpDotNet.Tests/DividendTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/dividends.AAPL.json`, `tests/FmpDotNet.Tests/Fixtures/dividends-calendar.head.json`
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (+2 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+1 entry)

**Interfaces:**
- Consumes: `CalendarResult<T>`'s seven-argument internal constructor from Task 1, in the order `(rows, rowsReturned, requestedFrom, requestedTo, earliestReturnedDate, rowCap, lookbackLimitDays)`. Also the shipped `DateRange.ThrowIfBackwards(LocalDate?, LocalDate?)` and `NullableLocalDateJsonConverter`.
- Produces: `public sealed record Dividend` with `Symbol`, `Date`, `RecordDate`, `PaymentDate`, `DeclarationDate` (`LocalDate?`), `AdjDividend`, `DividendAmount`, `Yield` (`decimal?`), `Frequency` (`string?`). `CalendarEndpoints.GetDividendsAsync(string symbol, int? limit = null, CancellationToken ct = default)` and `GetDividendsCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default)`, both returning `Task<IReadOnlyList<Dividend>>`.

**The property is `DividendAmount`, not `Dividend`.** C# forbids a member sharing its enclosing type's name (CS0542), exactly as `EmployeeCount.Employees` already documents. The wire name `dividend` is preserved by an explicit `[JsonPropertyName("dividend")]`.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/dividends.AAPL.json` — the first five rows of `stable/dividends?symbol=AAPL&limit=5`, captured 2026-08-28, verbatim. Every date field is populated on all five, which is what makes it the counterpart to the calendar fixture below:

```json
[
 {
  "symbol": "AAPL",
  "date": "2026-08-10",
  "recordDate": "2026-08-10",
  "paymentDate": "2026-08-13",
  "declarationDate": "2026-07-30",
  "adjDividend": 0.27,
  "dividend": 0.27,
  "yield": 0.3438655680269902,
  "frequency": "Quarterly"
 },
 {
  "symbol": "AAPL",
  "date": "2026-05-11",
  "recordDate": "2026-05-11",
  "paymentDate": "2026-05-14",
  "declarationDate": "2026-04-30",
  "adjDividend": 0.27,
  "dividend": 0.27,
  "yield": 0.3587535875358754,
  "frequency": "Quarterly"
 },
 {
  "symbol": "AAPL",
  "date": "2026-02-09",
  "recordDate": "2026-02-09",
  "paymentDate": "2026-02-12",
  "declarationDate": "2026-01-29",
  "adjDividend": 0.26,
  "dividend": 0.26,
  "yield": 0.3787051198019081,
  "frequency": "Quarterly"
 },
 {
  "symbol": "AAPL",
  "date": "2025-11-10",
  "recordDate": "2025-11-10",
  "paymentDate": "2025-11-13",
  "declarationDate": "2025-10-30",
  "adjDividend": 0.26,
  "dividend": 0.26,
  "yield": 0.38228853505548754,
  "frequency": "Quarterly"
 },
 {
  "symbol": "AAPL",
  "date": "2025-08-11",
  "recordDate": "2025-08-11",
  "paymentDate": "2025-08-14",
  "declarationDate": "2025-07-31",
  "adjDividend": 0.26,
  "dividend": 0.26,
  "yield": 0.44898318513953694,
  "frequency": "Quarterly"
 }
]
```

`tests/FmpDotNet.Tests/Fixtures/dividends-calendar.head.json` — the first five rows of `stable/dividends-calendar?from=2026-08-24&to=2026-08-25`, captured 2026-08-28, verbatim. **All five carry a blank `declarationDate`, and that is not an edit** — the field was blank on 325 of the 622 rows that request returned, and on 2,232 of 4,000 in the earlier pass. Between this fixture and the one above, both states of that field are covered by real captures and no hand-built third file is needed:

```json
[
 {
  "symbol": "001231.SZ",
  "date": "2026-08-25",
  "recordDate": "2026-08-24",
  "paymentDate": "2026-08-25",
  "declarationDate": "",
  "adjDividend": 0.15,
  "dividend": 0.15,
  "yield": 0.7173601147776184,
  "frequency": "Annual"
 },
 {
  "symbol": "0018.HK",
  "date": "2026-08-25",
  "recordDate": "2026-08-27",
  "paymentDate": "2026-09-10",
  "declarationDate": "",
  "adjDividend": 0.01,
  "dividend": 0.01,
  "yield": 4.651162790697675,
  "frequency": "Annual"
 },
 {
  "symbol": "0027.HK",
  "date": "2026-08-25",
  "recordDate": "2026-08-26",
  "paymentDate": "2026-09-15",
  "declarationDate": "",
  "adjDividend": 0.9,
  "dividend": 0.9,
  "yield": 4.605641911341393,
  "frequency": "Semi-Annual"
 },
 {
  "symbol": "0150.HK",
  "date": "2026-08-25",
  "recordDate": "2026-08-26",
  "paymentDate": "2026-09-04",
  "declarationDate": "",
  "adjDividend": 0.0018,
  "dividend": 0.0018,
  "yield": 5.42483660130719,
  "frequency": "Annual"
 },
 {
  "symbol": "0237.HK",
  "date": "2026-08-25",
  "recordDate": "2026-08-27",
  "paymentDate": "2026-09-16",
  "declarationDate": "",
  "adjDividend": 0.05,
  "dividend": 0.05,
  "yield": 3.652968036529681,
  "frequency": "Semi-Annual"
 }
]
```

Note `recordDate` and `paymentDate` **after** `date` on several of these rows — a record date the day before the ex-date and a payment date three weeks later. That is FMP's data, not a sorting fault, and no test should assume an ordering among the four dates.

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/DividendTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/dividends</c> and <c>stable/dividends-calendar</c>, checked against captures taken live
/// 2026-08-28.
///
/// <para>One record serves both: their field sets were measured byte-identical, nine fields in the same order.
/// What differs is everything around the record — one takes a symbol and returns a whole history, the other
/// takes a date range and returns every symbol in it, and only the second truncates.</para></summary>
public class DividendTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- binding ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_per_symbol_row_binds_all_nine_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("dividends.AAPL.json"));

        var rows = await endpoints.GetDividendsAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 10), rows[0].Date);
        Assert.Equal(Day(2026, 8, 10), rows[0].RecordDate);
        Assert.Equal(Day(2026, 8, 13), rows[0].PaymentDate);
        Assert.Equal(Day(2026, 7, 30), rows[0].DeclarationDate);
        Assert.Equal(0.27m, rows[0].AdjDividend);
        Assert.Equal(0.27m, rows[0].DividendAmount);
        Assert.Equal(0.3438655680269902m, rows[0].Yield);
        Assert.Equal("Quarterly", rows[0].Frequency);
    }

    [Fact]
    public async Task A_blank_declaration_date_reads_as_null_and_costs_nothing_else_on_the_row()
    {
        // The measured shape, not an edge case: declarationDate was blank on 325 of the 622 rows this fixture's
        // request returned, and on 2232 of 4000 in a wider one. NullableLocalDateJsonConverter reads "" as null
        // because LocalDatePattern.Iso.Parse("") fails and it answers null rather than throwing — a throw here
        // would cost the whole response, since FmpTransport does not wrap DeserializeAsync.
        var (endpoints, _) = Build(Binding.Fixture("dividends-calendar.head.json"));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Null(r.DeclarationDate));
        Assert.Equal(["DeclarationDate"], Binding.Unbound(rows[0]));
        // Everything else on the row survived the blank.
        Assert.Equal("001231.SZ", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 24), rows[0].RecordDate);
        Assert.Equal(0.15m, rows[0].DividendAmount);
        Assert.Equal("Annual", rows[0].Frequency);
    }

    [Fact]
    public void The_wire_name_dividend_binds_to_the_property_named_DividendAmount()
    {
        // C# forbids a member sharing its type's name (CS0542), so the property is renamed and the wire name is
        // pinned by an explicit attribute. Without that attribute `dividend` would not bind and AdjDividend
        // would still populate — half the row correct, which is the failure worth a test of its own.
        var row = JsonSerializer.Deserialize(
            """[{"dividend":1.25,"adjDividend":9.99}]""", FmpJsonContext.Default.ListDividend)![0];

        Assert.Equal(1.25m, row.DividendAmount);
        Assert.Equal(9.99m, row.AdjDividend);
    }

    [Fact]
    public void The_four_dates_are_read_independently_and_none_is_assumed_to_precede_another()
    {
        // In the captured calendar rows a recordDate falls before its date and a paymentDate three weeks after.
        // Nothing in the SDK sorts or validates them against each other.
        var row = JsonSerializer.Deserialize(
            """
            [{"date":"2026-08-25","recordDate":"2026-08-24","paymentDate":"2026-09-10",
              "declarationDate":"2026-07-01"}]
            """, FmpJsonContext.Default.ListDividend)![0];

        Assert.Equal(Day(2026, 8, 25), row.Date);
        Assert.Equal(Day(2026, 8, 24), row.RecordDate);
        Assert.Equal(Day(2026, 9, 10), row.PaymentDate);
        Assert.Equal(Day(2026, 7, 1), row.DeclarationDate);
    }

    [Fact]
    public void Frequency_stays_a_string_because_the_observed_set_depends_on_which_path_answered()
    {
        // Measured 2026-08-28: dividends?symbol=AAPL shows 2 distinct values (Quarterly x91, Irregular x1);
        // dividends-calendar over two days shows 7 (Monthly, Quarterly, Semi-Annual, Annual, Weekly, Irregular,
        // Special) and 8 over a wider window (adding Bi-Weekly). An enum built from either sample would be
        // wrong for the other, and would turn an unseen value into a deserialisation failure.
        var rows = JsonSerializer.Deserialize(
            """[{"frequency":"Bi-Weekly"},{"frequency":"Special"},{"frequency":"Something FMP Adds In 2027"}]""",
            FmpJsonContext.Default.ListDividend)!;

        Assert.Equal(["Bi-Weekly", "Special", "Something FMP Adds In 2027"], rows.Select(r => r.Frequency));
    }

    // ---- requests -----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_per_symbol_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        // limit is omitted rather than defaulted, because an absent limit returns the whole series: 92 rows for
        // AAPL, unchanged by limit=10000. A default of 100 would silently truncate a longer history.
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsAsync("AAPL");

        Assert.Equal("stable/dividends", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_per_symbol_path_sends_a_limit_when_one_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_per_symbol_path_offers_no_date_range_because_the_endpoint_ignores_one()
    {
        // Measured 2026-08-28: dividends?symbol=AAPL answers 92 rows, and the same call with
        // from=2024-01-01&to=2024-12-31 answers the same 92. The parameters are accepted and ignored, so the
        // signature does not offer them — a caller who could pass them would believe the filter happened.
        var method = typeof(CalendarEndpoints).GetMethod(nameof(CalendarEndpoints.GetDividendsAsync))!;

        Assert.DoesNotContain(method.GetParameters(), p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task The_calendar_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal("stable/dividends-calendar", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-08-24&to=2026-08-25&apikey=k", handler.Requests.Single().Query);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_blank_symbol_is_refused_before_a_request_is_spent(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetDividendsAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_before_a_request_is_spent()
    {
        // Separate from the theory above: ArgumentException.ThrowIfNullOrWhiteSpace throws
        // ArgumentNullException for null, and Assert.ThrowsAsync matches the type exactly.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetDividendsAsync(null!));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetDividendsAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_through_the_shared_guard()
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetDividendsCalendarAsync(Day(2026, 8, 25), Day(2026, 8, 24)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    // ---- truncation ---------------------------------------------------------------------------------------

    [Fact]
    public async Task The_calendar_returns_a_CalendarResult_carrying_the_measured_row_cap()
    {
        var (endpoints, _) = Build(Binding.Fixture("dividends-calendar.head.json"));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(4000, result.RowCap);
        // Null, and deliberately: the row cap always fires first at 340-876 rows a day, so no window limit is
        // observable on this path and asserting one would be inventing evidence.
        Assert.Null(result.LookbackLimitDays);
        Assert.Equal(Day(2026, 8, 24), result.RequestedFrom);
        Assert.Equal(Day(2026, 8, 25), result.RequestedTo);
        Assert.Equal(Day(2026, 8, 25), result.EarliestReturnedDate);
        Assert.Equal(5, result.RowsReturned);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_response_at_the_cap_reports_itself_truncated()
    {
        // The measured headline: from=2025-01-01&to=2025-12-31 answered exactly 4000 rows whose earliest date
        // was 2025-12-29 — a request for a year, answered with its last three days. Both tells fire here, and
        // they are independent: the cap is visible in the count, the missing front only in the dates.
        var (endpoints, _) = Build(SyntheticCalendar(4000, Day(2025, 12, 29)));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2025, 1, 1), Day(2025, 12, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.True(result.AtRowCap);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
        Assert.True(CalendarResult<Dividend>.IsLikelyTruncated(rows));
    }

    [Fact]
    public async Task The_truncation_signal_is_taken_from_the_raw_response_before_any_row_is_dropped()
    {
        // The ordering that makes the signal trustworthy. 4000 rows arrive, one of them undated and therefore
        // dropped, so the caller holds 3999. Count the kept rows instead of the raw ones and this response --
        // genuinely at the cap -- reports itself complete.
        var body = SyntheticCalendar(3999, Day(2025, 12, 29), undatedRows: 1);
        var (endpoints, _) = Build(body);

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2025, 1, 1), Day(2025, 12, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(4000, result.RowsReturned);
        Assert.Equal(3999, result.Count);
        Assert.True(result.AtRowCap);
    }

    [Fact]
    public async Task A_calendar_row_with_an_unparseable_date_is_dropped_rather_than_aborting_the_response()
    {
        // Same rule the earnings calendar already applies: on a calendar the date is half the row's identity,
        // so a row that cannot be placed on a timeline is dropped, and RowsReturned says how many were.
        var (endpoints, _) = Build(
            """
            [{"symbol":"BAD.X","date":"","dividend":1,"frequency":"Annual"},
             {"symbol":"0018.HK","date":"2026-08-25","dividend":0.01,"frequency":"Annual"}]
            """);

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        var row = Assert.Single(rows);
        Assert.Equal("0018.HK", row.Symbol);
        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(2, result.RowsReturned);
    }

    /// <summary>A calendar payload of a given size. Synthetic on purpose — the cap needs 4000 rows to exercise
    /// and nothing about those rows matters except how many there are and which dates they carry, so shipping a
    /// 4000-row fixture would add a megabyte of noise and prove nothing the captures do not.</summary>
    private static string SyntheticCalendar(int rowCount, LocalDate day, int undatedRows = 0)
    {
        var json = new System.Text.StringBuilder("[");
        for (var i = 0; i < rowCount + undatedRows; i++)
        {
            if (i > 0) json.Append(',');
            var date = i < undatedRows ? "" : day.ToString("uuuu-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{i}}","date":"{{date}}","dividend":1,"adjDividend":1,"yield":1,"frequency":"Annual"}""");
        }
        return json.Append(']').ToString();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DividendTests"`
Expected: the build fails — `CS0246` for `Dividend` and `CS1061` for `GetDividendsAsync`/`GetDividendsCalendarAsync` on `CalendarEndpoints`.

- [ ] **Step 4: Write the record**

`src/FmpDotNet/Models/Dividend.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One dividend event. Serves both <c>stable/dividends</c>, which answers one symbol's whole history,
/// and <c>stable/dividends-calendar</c>, which answers every symbol in a date range.
///
/// <para><b>Those two paths return the same nine fields in the same order</b>, compared on 2026-08-28, which is
/// why one record serves both — the same reasoning as <see cref="EmployeeCount"/> and
/// <see cref="SecFiling"/>.</para>
///
/// <para><b>Four dates, in no guaranteed order relative to each other.</b> <see cref="Date"/> is the ex-dividend
/// date; the other three are the record, payment and declaration dates, and the captured calendar rows include
/// a record date the day <i>before</i> its ex-date and a payment date three weeks after. Nothing here sorts or
/// cross-validates them.</para>
///
/// <para><b><see cref="DeclarationDate"/> is very often absent, and FMP spells absent as a blank string rather
/// than as null.</b> Measured 2026-08-28: blank on 15 of AAPL's 92 rows, on 325 of 622 calendar rows for a
/// two-day window, and on 2,232 of 4,000 for a wider one. <see cref="NullableLocalDateJsonConverter"/> reads
/// <c>""</c> as null rather than throwing, which is what keeps those rows from costing the whole
/// response.</para></summary>
public sealed record Dividend
{
    /// <summary>Ticker as FMP spells it. The calendar is global, so the captured rows span <c>001231.SZ</c>,
    /// <c>0018.HK</c> and <c>0237.HK</c> — suffixed exchange codes are the norm there rather than the
    /// exception, and a caller filtering to US listings must do so explicitly.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The ex-dividend date — the day the share trades without the entitlement, and the date both
    /// paths select on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The record date. Blank on 1 of 622 calendar rows measured 2026-08-28, which reads as
    /// null.</summary>
    [JsonPropertyName("recordDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? RecordDate { get; init; }

    /// <summary>The payment date. Blank on 4 of 622 calendar rows measured 2026-08-28.</summary>
    [JsonPropertyName("paymentDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? PaymentDate { get; init; }

    /// <summary>When the dividend was declared, or <see langword="null"/> where FMP has no declaration on file.
    ///
    /// <para><b>Null is the common case on the calendar, not the exception</b> — 325 of 622 rows measured
    /// 2026-08-28, and 2,232 of 4,000 over a wider window. A pipeline that treats a null here as a data fault
    /// will treat over half the calendar as faulty.</para></summary>
    [JsonPropertyName("declarationDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DeclarationDate { get; init; }

    /// <summary>The dividend adjusted for subsequent splits, in the issuer's own currency.</summary>
    [JsonPropertyName("adjDividend")] public decimal? AdjDividend { get; init; }

    /// <summary>The dividend as declared, in the issuer's own currency.
    ///
    /// <para><b>Named <c>DividendAmount</c> rather than <c>Dividend</c> because C# forbids a member sharing its
    /// enclosing type's name</b> (CS0542) — the same rename, for the same reason, as
    /// <see cref="EmployeeCount.Employees"/>. The wire name is unchanged.</para>
    ///
    /// <para><see langword="decimal"/> and not a narrower type: the field arrives as a JSON integer on some rows
    /// and a float on others (32 and 590 of 622 measured), and ranged from 0.001 to 3,383.85 in a single two-day
    /// window across global listings.</para></summary>
    [JsonPropertyName("dividend")] public decimal? DividendAmount { get; init; }

    /// <summary>FMP's computed yield for this event, as a percentage. Measured from 0.018 to 245.16 in one
    /// two-day calendar window — the high end being small-denomination listings rather than an error, and a
    /// reason not to treat this as a sanity-checked figure.</summary>
    [JsonPropertyName("yield")] public decimal? Yield { get; init; }

    /// <summary>How often the issuer pays, as FMP's own label.
    ///
    /// <para><b>A string and not an enum, because the observed set depends on which path answered.</b> Measured
    /// 2026-08-28: <c>stable/dividends</c> for AAPL shows two values (<c>Quarterly</c> ×91,
    /// <c>Irregular</c> ×1); the calendar shows eight (<c>Quarterly</c>, <c>Semi-Annual</c>, <c>Monthly</c>,
    /// <c>Annual</c>, <c>Weekly</c>, <c>Irregular</c>, <c>Special</c>, <c>Bi-Weekly</c>). Either sample would
    /// make an enum that the other contradicts, and an unlisted value would then become a deserialisation
    /// failure instead of a string a caller can read.</para></summary>
    [JsonPropertyName("frequency")] public string? Frequency { get; init; }
}
```

- [ ] **Step 5: Register the record**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add beside the other calendar entries — a missing registration here fails at runtime, not at compile time:

```csharp
[JsonSerializable(typeof(List<Dividend>))]
```

- [ ] **Step 6: Write the two methods**

Append to `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`, inside the class:

```csharp
    /// <summary>Every dividend FMP holds for one symbol, newest first, from <c>stable/dividends</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything.</b> Measured
    /// 2026-08-28, AAPL answers <b>92 rows</b> with no limit and the same 92 with <c>limit=10000</c> — the whole
    /// history, back to 1987. A default of 100 would have quietly cut a longer one.</para>
    ///
    /// <para><b>There is no date range on this method, because the endpoint ignores one.</b> Measured the same
    /// day: <c>symbol=AAPL</c> answers 92 rows, and <c>symbol=AAPL&amp;from=2024-01-01&amp;to=2024-12-31</c>
    /// answers the same 92. Offering the parameters would let a caller believe a filter happened. Use
    /// <see cref="GetDividendsCalendarAsync"/> for a date range, or filter
    /// <see cref="Dividend.Date"/> at the call site.</para>
    ///
    /// <para>An unknown symbol answers <c>[]</c> with HTTP 200 rather than a 404, which the transport surfaces
    /// as an empty list — never null.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it — hyphenated for class shares (<c>BRK-B</c>, not
    /// <c>BRK.B</c>).</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<Dividend>> GetDividendsAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/dividends").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListDividend, ct).ConfigureAwait(false);
    }

    /// <summary>Every dividend event FMP has in a date range, across all symbols, from
    /// <c>stable/dividends-calendar</c>.
    ///
    /// <para><b>The response is silently capped at 4000 rows, and the truncation eats the front of the
    /// range.</b> Measured 2026-08-28: <c>from=2025-01-01&amp;to=2025-12-31</c> answered <b>exactly 4000</b>
    /// rows whose earliest date was <b>2025-12-29</b> — a request for a year, answered with its last three days,
    /// and a caller reading <c>rows[0]</c> is handed December believing they hold January. One month behaves the
    /// same way: June 2025 answered 4000 rows starting 2025-06-26. <c>limit=10000</c> was accepted and ignored.
    /// There is no cursor, so the SDK cannot page around it and can only report it — which is what
    /// <see cref="CalendarResult{T}"/> is for, and the returned list is one.</para>
    ///
    /// <para><b>A safe width cannot be read off the calendar.</b> Density measured 340 rows on 2025-11-20, 673
    /// on 2025-03-14 and 876 on 2025-06-02, so the cap falls somewhere between five and eleven days depending on
    /// the season. A six-day window returned 2147 rows and was complete; a thirty-day window was not. That
    /// season-dependence is exactly why this method reports rather than guesses a chunk size.</para>
    ///
    /// <code>
    /// var rows = await fmp.Calendar.GetDividendsCalendarAsync(from, to);
    /// if (rows is CalendarResult&lt;Dividend&gt; { LikelyTruncated: true }) { /* narrow the range and retry */ }
    /// </code>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this class: on a
    /// calendar the date is half the row's identity. <see cref="CalendarResult{T}.RowsReturned"/> against
    /// <see cref="CalendarResult{T}.Count"/> says how many, and the truncation tells are computed on the raw
    /// response before any of that happens.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive. May equal <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="Dividend"/> — the rows in wire order, which is
    /// <b>not</b> sorted, carrying the row count FMP actually returned so the caller can tell a complete answer
    /// from a truncated one.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<Dividend>> GetDividendsCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/dividends-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListDividend, ct).ConfigureAwait(false);

        // Taken from the raw response, before the filter below can move it.
        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<Dividend>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        // rowCap 4000, lookbackLimitDays null: the cap always fires first at 340-876 rows a day, so no window
        // limit is observable on this path and asserting one would be inventing evidence.
        return new CalendarResult<Dividend>(kept, rows.Count, from, to, earliest, rowCap: 4000, lookbackLimitDays: null);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DividendTests"`
Expected: PASS — 17 test methods, **19 test cases** (15 `[Fact]`, plus 2 and 2 `[InlineData]` rows on the two `[Theory]` methods). Zero warnings.

- [ ] **Step 8: Mutation-check the raw-count ordering, which is the whole point of the type**

Edit `GetDividendsCalendarAsync` to compute the result from the kept rows instead of the raw ones — `new CalendarResult<Dividend>(kept, kept.Count, from, to, earliest, …)`.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DividendTests"`
Expected: exactly **1** failure — `The_truncation_signal_is_taken_from_the_raw_response_before_any_row_is_dropped`, on `Assert.Equal(4000, result.RowsReturned)`. Note that `A_response_at_the_cap_reports_itself_truncated` **still passes**, because no row is dropped there; that is precisely why the two tests are separate and why the dropped-row case needs one of its own. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/Dividend.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/DividendTests.cs \
        tests/FmpDotNet.Tests/Fixtures/dividends.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/dividends-calendar.head.json
git commit -m "feat: dividends and the 4000-row calendar that reports its own truncation (#37)"
```

---

### Task 3: `StockSplit`, and the calendar with a 90-day window

The second shared record, and the task that first ships the window-clamp half of `CalendarResult<T>`. Read the ruling on the 90-day lookback in "Corrections and rulings" before starting: the spec originally recorded this path as not truncating.

**Files:**
- Create: `src/FmpDotNet/Models/StockSplit.cs`, `tests/FmpDotNet.Tests/StockSplitTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/splits.AAPL.json`, `splits-calendar.head.json`, `splits-calendar.split-types.json`
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (+2 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+1 entry)

**Note the one `<c>` where a `<see cref>` belongs.** `StockSplit.Numerator`'s documentation cross-references `IpoCalendarEntry.MarketCap` — the field where the same measurement went the other way — but that type does not exist until Task 4, and an unresolved `<see cref>` is `CS1574`, a build error here. It is written as `<c>IpoCalendarEntry.MarketCap</c>` for now and **promoted to a real `cref` in Task 4, Step 4a**, once the target exists. Leave it as `<c>` in this task; do not "fix" it.

**Interfaces:**
- Consumes: `CalendarResult<T>` (Task 1), `DateRange.ThrowIfBackwards`, `NullableLocalDateJsonConverter`. `Dividend` and `GetDividendsCalendarAsync` from Task 2 exist and are the model to follow — read `CalendarEndpoints.GetDividendsCalendarAsync` before writing `GetSplitsCalendarAsync`, because the two differ in exactly two constructor arguments and nothing else.
- Produces: `public sealed record StockSplit` with `Symbol` (`string?`), `Date` (`LocalDate?`), `Numerator`, `Denominator` (`int?`), `SplitType` (`string?`). `CalendarEndpoints.GetSplitsAsync(string symbol, int? limit = null, CancellationToken ct = default)` and `GetSplitsCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default)`, both returning `Task<IReadOnlyList<StockSplit>>`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/splits.AAPL.json` — `stable/splits?symbol=AAPL`, captured 2026-08-28, verbatim. All five rows FMP holds; the whole history is five rows, so this fixture is the complete response and not a head of one:

```json
[
 {"symbol": "AAPL", "date": "2020-08-31", "numerator": 4, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "AAPL", "date": "2014-06-09", "numerator": 7, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "AAPL", "date": "2005-02-28", "numerator": 2, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "AAPL", "date": "2000-06-21", "numerator": 2, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "AAPL", "date": "1987-06-16", "numerator": 2, "denominator": 1, "splitType": "stock-split"}
]
```

`tests/FmpDotNet.Tests/Fixtures/splits-calendar.head.json` — the first five rows of `stable/splits-calendar?from=2026-01-01&to=2026-08-28`, captured 2026-08-28, verbatim. Note `CYCU` at 1-for-8, a reverse split, and that every row here is `stock-split` — which is why the third fixture exists:

```json
[
 {"symbol": "8011.T", "date": "2026-08-28", "numerator": 3, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "7649.T", "date": "2026-08-28", "numerator": 2, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "SPICEISLIN.BO", "date": "2026-08-28", "numerator": 5, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "9279.T", "date": "2026-08-28", "numerator": 2, "denominator": 1, "splitType": "stock-split"},
 {"symbol": "CYCU", "date": "2026-08-28", "numerator": 1, "denominator": 8, "splitType": "stock-split"}
]
```

`tests/FmpDotNet.Tests/Fixtures/splits-calendar.split-types.json` — five rows selected from the same 961-row `stable/splits-calendar` response by the value of `splitType`, so that every value FMP sends is present. The head fixture is `stock-split` on all five rows and cannot carry them. **Two rows have `splitType: null`** — that is 16 of 961 — and `GOODY.IS` at 5629/1000 is the one `spin-off` in the whole response:

```json
[
 {"symbol": "GAME", "date": "2026-08-24", "numerator": 1, "denominator": 8, "splitType": null},
 {"symbol": "BIAF", "date": "2026-08-24", "numerator": 1, "denominator": 15, "splitType": null},
 {"symbol": "5863.TWO", "date": "2026-08-25", "numerator": 51, "denominator": 50, "splitType": "stock-dividend"},
 {"symbol": "MAZAYA.KW", "date": "2026-08-24", "numerator": 51, "denominator": 50, "splitType": "stock-dividend"},
 {"symbol": "GOODY.IS", "date": "2026-07-02", "numerator": 5629, "denominator": 1000, "splitType": "spin-off"}
]
```

**There is no `"None"` row in this fixture, and that is the point.** The spec's trap #8 said `splitType` carries both JSON-null and the literal string `"None"`. Re-measured field by field across all 961 rows and all five of their fields, `"None"` appears nowhere in the response; the sentinel is real but belongs to the previous slice's classification paths. Do not add a `"None"` row to make the fixture match the spec — the corrected spec and measurements say what this file says.

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/StockSplitTests.cs`. `Build` and `Day` are the same two helpers as `DividendTests` — repeat them here rather than sharing, matching how every endpoint test file in this repo already stands alone:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/splits</c> and <c>stable/splits-calendar</c>, checked against captures taken live
/// 2026-08-28.
///
/// <para>One record serves both — five fields, measured identical. The calendar truncates, but not the way the
/// dividend calendar does: it clamps to a <b>90-day window measured from <c>to</c></b> and drops everything
/// earlier. A request for the whole of 2024 answered Q4 of 2024, at <b>737 rows</b> — nowhere near any cap, so
/// no row count could have seen it.</para></summary>
public class StockSplitTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- binding ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_per_symbol_row_binds_all_five_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("splits.AAPL.json"));

        var rows = await endpoints.GetSplitsAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2020, 8, 31), rows[0].Date);
        Assert.Equal(4, rows[0].Numerator);
        Assert.Equal(1, rows[0].Denominator);
        Assert.Equal("stock-split", rows[0].SplitType);
    }

    [Fact]
    public async Task A_reverse_split_is_a_numerator_below_its_denominator_and_nothing_else()
    {
        // CYCU at 1-for-8 in the captured calendar page. The SDK does not compute a ratio, flag a direction or
        // normalise the pair: it reports the two integers FMP sent, and the caller divides if they want to.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.head.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 1, 1), Day(2026, 8, 28));

        var reverse = Assert.Single(rows, r => r.Symbol == "CYCU");
        Assert.Equal(1, reverse.Numerator);
        Assert.Equal(8, reverse.Denominator);
    }

    [Fact]
    public async Task A_null_split_type_binds_as_null_and_costs_nothing_else_on_the_row()
    {
        // 16 of 961 rows measured 2026-08-28. The other four fields on those rows are fully populated, so a
        // null here is FMP declining to classify the event, not a broken row.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.split-types.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal(5, rows.Count);
        Assert.Null(rows[0].SplitType);
        Assert.Equal(["SplitType"], Binding.Unbound(rows[0]));
        Assert.Equal("GAME", rows[0].Symbol);
        Assert.Equal(1, rows[0].Numerator);
        Assert.Equal(8, rows[0].Denominator);
    }

    [Fact]
    public async Task Every_split_type_FMP_sends_is_carried_through_verbatim()
    {
        // The complete measured set across 961 rows: stock-split x934, JSON-null x16, stock-dividend x10,
        // spin-off x1. Four values counting null, and no enum, because the set is a sample from one response.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.split-types.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal(
            new string?[] { null, null, "stock-dividend", "stock-dividend", "spin-off" },
            rows.Select(r => r.SplitType));
    }

    [Fact]
    public void The_literal_string_None_is_carried_through_as_a_string_if_it_ever_arrives()
    {
        // Recorded rather than asserted as measured. FMP sends no "None" on this field — re-measured field by
        // field across all 961 rows on 2026-08-28 — and an earlier draft of the spec said it did, having
        // confused it with the sentinel on the previous slice's classification paths. Typed `string?`, the SDK
        // is right either way: a value it has never seen reaches the caller unchanged instead of throwing.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"X","splitType":"None"}]""", FmpJsonContext.Default.ListStockSplit)![0];

        Assert.Equal("None", row.SplitType);
    }

    [Fact]
    public void The_split_ratio_stays_int_because_the_measured_maxima_fit()
    {
        // 1,011,977 and 1,000,000 were the largest values across 961 rows, against an int.MaxValue of
        // 2,147,483,647, and 961 of 961 were whole. This is the opposite ruling from IpoCalendarEntry.MarketCap,
        // from the same kind of evidence: that field was measured at 74,999,999,925 and does NOT fit.
        var row = JsonSerializer.Deserialize(
            """[{"numerator":1011977,"denominator":1000000}]""", FmpJsonContext.Default.ListStockSplit)![0];

        Assert.Equal(1_011_977, row.Numerator);
        Assert.Equal(1_000_000, row.Denominator);
    }

    // ---- requests -----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_per_symbol_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSplitsAsync("AAPL");

        Assert.Equal("stable/splits", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_per_symbol_path_offers_no_date_range_because_the_endpoint_ignores_one()
    {
        // Measured 2026-08-28: splits?symbol=AAPL answers 5 rows with and without
        // from=2024-01-01&to=2024-12-31 — and AAPL had no split in 2024, so a working filter would have
        // answered zero.
        var method = typeof(CalendarEndpoints).GetMethod(nameof(CalendarEndpoints.GetSplitsAsync))!;

        Assert.DoesNotContain(method.GetParameters(), p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task The_calendar_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal("stable/splits-calendar", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-06-01&to=2026-08-28&apikey=k", handler.Requests.Single().Query);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_blank_symbol_is_refused_before_a_request_is_spent(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetSplitsAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_before_a_request_is_spent()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetSplitsAsync(null!));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_through_the_shared_guard()
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetSplitsCalendarAsync(Day(2026, 8, 28), Day(2026, 6, 1)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    // ---- the 90-day window --------------------------------------------------------------------------------

    [Fact]
    public async Task The_calendar_reports_a_ninety_day_window_and_no_row_cap()
    {
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.head.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 8, 28), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.Equal(90, result.LookbackLimitDays);
        // Null, and deliberately: no row cap was measured on this path, and an invented one would be a number
        // nobody checked. 947 rows came back for the widest range tried.
        Assert.Null(result.RowCap);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_wider_than_ninety_days_reports_itself_truncated_at_a_row_count_no_cap_would_catch()
    {
        // The measured case, and the reason this task exists as written: from=2024-01-01&to=2024-12-31 answered
        // 737 rows whose earliest date was 2024-10-02. Nine months absent. AtRowCap is structurally blind here —
        // RowCap is null — so both surviving tells have to carry it.
        var (endpoints, _) = Build(SyntheticCalendar(737, Day(2024, 10, 2)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2024, 1, 1), Day(2024, 12, 31));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.AtRowCap);
        Assert.True(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_of_exactly_ninety_days_is_caught_by_the_start_of_range_tell_alone()
    {
        // Measured 2026-08-28 against a fixed to=2026-08-28: from at -88 days was honoured exactly, from at -90
        // answered an earliest row of 2026-05-31 against a requested 2026-05-30. So a 90-day span does not trip
        // ExceedsLookbackLimit and still loses a day, which is what MissesStartOfRange is for.
        var (endpoints, _) = Build(SyntheticCalendar(947, Day(2026, 5, 31)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 5, 30), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_inside_the_window_reports_itself_complete()
    {
        // from = to - 88 days, honoured exactly when measured: 946 rows, earliest 2026-06-01.
        var (endpoints, _) = Build(SyntheticCalendar(946, Day(2026, 6, 1)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.LikelyTruncated);
        Assert.False(CalendarResult<StockSplit>.IsLikelyTruncated(rows));
    }

    /// <summary>A splits-calendar payload of a given size, every row on one date. Synthetic for the same reason
    /// as in <see cref="DividendTests"/>: what these tests exercise is a row count and an earliest date, and a
    /// 947-row capture would add noise without adding evidence.</summary>
    private static string SyntheticCalendar(int rowCount, LocalDate earliest)
    {
        var json = new System.Text.StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{i}}","date":"{{earliest:uuuu-MM-dd}}","numerator":2,"denominator":1,"splitType":"stock-split"}""");
        }
        return json.Append(']').ToString();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockSplitTests"`
Expected: the build fails — `CS0246` for `StockSplit`, `CS1061` for the two methods.

- [ ] **Step 4: Write the record**

`src/FmpDotNet/Models/StockSplit.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One share split. Serves both <c>stable/splits</c>, which answers one symbol's whole history, and
/// <c>stable/splits-calendar</c>, which answers every symbol in a date range — five fields, measured identical
/// on 2026-08-28.
///
/// <para><b>The ratio is reported as the two integers FMP sent, and nothing is computed from them.</b> A
/// forward split is <c>4/1</c>, a reverse split is <c>1/8</c>, and awkward real-world ratios are ordinary here:
/// <c>51/50</c> for a Taiwanese stock dividend and <c>5629/1000</c> for a Turkish spin-off, both in the same
/// captured response. Dividing them is the caller's business, at the precision the caller wants.</para></summary>
public sealed record StockSplit
{
    /// <summary>Ticker as FMP spells it. The calendar is global — <c>8011.T</c>, <c>SPICEISLIN.BO</c>,
    /// <c>MAZAYA.KW</c> and <c>GOODY.IS</c> all appear in one captured response — so a caller filtering to US
    /// listings must do so explicitly.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the split takes effect, and the date the calendar path selects on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Shares held after the split, per <see cref="Denominator"/> shares held before.
    ///
    /// <para><see langword="int"/> rather than a wider or fractional type, and that was measured rather than
    /// assumed: across 961 calendar rows on 2026-08-28 every value was whole, and the largest was
    /// <b>1,011,977</b> against an <see cref="int"/> ceiling of 2,147,483,647. Recording what was measured beats
    /// widening against a fractional value nobody has seen — and the same check went the other way on
    /// <c>IpoCalendarEntry.MarketCap</c>, which does not fit.</para></summary>
    [JsonPropertyName("numerator")] public int? Numerator { get; init; }

    /// <summary>Shares held before the split, per <see cref="Numerator"/> shares held after. Largest measured
    /// value 1,000,000.</summary>
    [JsonPropertyName("denominator")] public int? Denominator { get; init; }

    /// <summary>FMP's classification of the event, or <see langword="null"/> where it does not classify one.
    ///
    /// <para><b>Null on 16 of 961 rows measured 2026-08-28</b>, with every other field on those rows fully
    /// populated — so a null here is FMP declining to label the event, not a broken row. The three string values
    /// observed were <c>stock-split</c> ×934, <c>stock-dividend</c> ×10 and <c>spin-off</c> ×1.</para>
    ///
    /// <para>A string and not an enum: four values counting null, drawn from one response, is a sample rather
    /// than a domain, and an unlisted value should reach the caller unchanged rather than fail to
    /// deserialise.</para></summary>
    [JsonPropertyName("splitType")] public string? SplitType { get; init; }
}
```

- [ ] **Step 5: Register the record**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<StockSplit>))]`

- [ ] **Step 6: Write the two methods**

Append to `CalendarEndpoints.cs`. `GetSplitsAsync` mirrors `GetDividendsAsync` exactly bar the path and the type. `GetSplitsCalendarAsync` mirrors `GetDividendsCalendarAsync` bar the path, the type, and **the last two constructor arguments, which are the opposite way round**:

```csharp
    /// <summary>Every split FMP holds for one symbol, newest first, from <c>stable/splits</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything.</b> AAPL's
    /// whole history is five rows, back to 1987, measured 2026-08-28.</para>
    ///
    /// <para><b>There is no date range on this method, because the endpoint ignores one.</b> Measured the same
    /// day: <c>symbol=AAPL</c> answers 5 rows with and without <c>from=2024-01-01&amp;to=2024-12-31</c> — and
    /// AAPL had no split in 2024, so a filter that worked would have answered none. Use
    /// <see cref="GetSplitsCalendarAsync"/> for a date range.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N rows, or null for the whole history. Must be positive when given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockSplit>> GetSplitsAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/splits").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListStockSplit, ct).ConfigureAwait(false);
    }

    /// <summary>Every split FMP has in a date range, across all symbols, from <c>stable/splits-calendar</c>.
    ///
    /// <para><b>This path will not reach more than 90 days back from <paramref name="to"/>, and it drops the
    /// front of the range without saying so.</b> Measured 2026-08-28 against four different <c>to</c> values
    /// spanning twenty months, each with <c>from</c> fixed at 2015-01-01, the earliest row returned was exactly
    /// 90 days before <c>to</c> every time. <b>A request for the whole of 2024 answers Q4 of 2024</b> — 737 rows,
    /// nine months missing. Walking <c>from</c> backwards against a fixed <c>to</c> shows the edge: −88 days is
    /// honoured exactly, and −100, −120 and −180 all return the identical 947 rows with the identical earliest
    /// date.</para>
    ///
    /// <para><b>No row count can see this.</b> 737 is nowhere near a cap, and no cap was measured on this path
    /// at all — the widest range tried answered 947 rows. So
    /// <see cref="CalendarResult{T}.AtRowCap"/> is structurally blind here and
    /// <see cref="CalendarResult{T}.MissesStartOfRange"/> is what catches it, by comparing the earliest row
    /// against the <c>from</c> that was asked for. That is a different mechanism from
    /// <see cref="GetDividendsCalendarAsync"/>, which is row-capped instead, and the returned type reports which
    /// one applies.</para>
    ///
    /// <para>Note that a span of <i>exactly</i> 90 days reads
    /// <see cref="CalendarResult{T}.ExceedsLookbackLimit"/> as <see langword="false"/> and still loses a day —
    /// −90 answered an earliest row of 2026-05-31 against a requested 2026-05-30. Read
    /// <see cref="CalendarResult{T}.LikelyTruncated"/>, which is the union of the tells, rather than any one of
    /// them.</para>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this
    /// class.</para></summary>
    /// <param name="from">First day of the range, inclusive. Anything more than 90 days before
    /// <paramref name="to"/> is silently ignored — see above.</param>
    /// <param name="to">Last day of the range, inclusive, and the anchor the 90-day window is measured
    /// from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="StockSplit"/>, carrying the evidence needed to
    /// tell a complete answer from a clamped one.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockSplit>> GetSplitsCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/splits-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListStockSplit, ct).ConfigureAwait(false);

        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<StockSplit>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        // The opposite of the dividend calendar: no cap was measured here, and the clamp is a flat 90-day
        // window from `to`.
        return new CalendarResult<StockSplit>(kept, rows.Count, from, to, earliest, rowCap: null, lookbackLimitDays: 90);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockSplitTests"`
Expected: PASS — 16 test methods, **17 test cases** (15 `[Fact]`, plus 2 `[InlineData]` rows on the one `[Theory]`). Zero warnings.

- [ ] **Step 8: Mutation-check the constructor arguments, which are the only thing separating the two calendars**

Edit `GetSplitsCalendarAsync`'s last two arguments to `rowCap: 4000, lookbackLimitDays: null` — the dividend calendar's values, which is exactly the copy-paste this task is most likely to produce.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockSplitTests"`
Expected: exactly **2** failures. Worked through rather than guessed:

| test | under the mutation | result |
|---|---|---|
| `The_calendar_reports_a_ninety_day_window_and_no_row_cap` | `LookbackLimitDays` is null, not 90 | **FAILS** |
| `A_range_wider_than_ninety_days_…` | `AtRowCap` still false (737 < 4000, so that assertion survives), but `ExceedsLookbackLimit` is false | **FAILS** |
| `A_range_of_exactly_ninety_days_…` | asserts `ExceedsLookbackLimit` is *false*, which it now is for the wrong reason; `MissesStartOfRange` still carries it | passes |
| `A_range_inside_the_window_…` | 946 < 4000 and nothing else fires | passes |

That third row is the interesting one and worth reading twice: a test written around one tell being false cannot detect that tell being permanently false. It is covered because the row above it asserts the same tell true on a different range — the pair is what pins the behaviour, not either test alone. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/StockSplit.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/StockSplitTests.cs \
        tests/FmpDotNet.Tests/Fixtures/splits.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/splits-calendar.head.json \
        tests/FmpDotNet.Tests/Fixtures/splits-calendar.split-types.json
git commit -m "feat: splits, and the calendar that answers a year with a quarter (#37)"
```

---

### Task 4: `IpoCalendarEntry`, and the third path with a 90-day window

The last of the three truncating calendars, and the record carrying the two typings the spec got wrong. Read corrections **2** and **3** in "Corrections and rulings" before starting: `priceRange` is a string, and `marketCap` overflows `int`.

**Files:**
- Create: `src/FmpDotNet/Models/Ipo.cs` (this task writes `IpoCalendarEntry`; Task 5 appends the other two records to the same file), `tests/FmpDotNet.Tests/IpoTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/ipos-calendar.head.json`, `ipos-calendar.priced.json`
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (+1 method), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+1 entry)

**Interfaces:**
- Consumes: `CalendarResult<T>` (Task 1), `DateRange.ThrowIfBackwards`, `NullableLocalDateJsonConverter`. `GetSplitsCalendarAsync` (Task 3) is the exact model for this method's window handling — same `rowCap: null, lookbackLimitDays: 90`.
- Produces: `public sealed record IpoCalendarEntry` with `Symbol`, `Daa`, `Company`, `Exchange`, `Actions`, `PriceRange` (all `string?`), `Date` (`LocalDate?`), `Shares`, `MarketCap` (`decimal?`). `CalendarEndpoints.GetIpoCalendarAsync(LocalDate from, LocalDate to, CancellationToken ct = default)` returning `Task<IReadOnlyList<IpoCalendarEntry>>`.

**`Daa` is modelled and named after the wire field, and it carries no information.** Every one of 450 rows was checked on 2026-08-28: the date part of `daa` equalled `date` in **450 of 450**, and the time part took exactly **one** distinct value across the whole response, `T04:00:00.000Z` — which is midnight Eastern. It is `date` twice, in two formats, under a name that explains neither. It is kept as `string?` rather than parsed, because parsing it would manufacture a second date property that can never disagree with the first, and the documentation says outright not to use it.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/ipos-calendar.head.json` — the first five rows of `stable/ipos-calendar?from=2026-01-01&to=2026-08-28`, captured 2026-08-28, verbatim. **All three numeric fields are null on all five rows**, which is the measured norm — `shares` was null on 349 of 450, `priceRange` on 441 and `marketCap` on 354:

```json
[
 {"symbol": "XLABW", "date": "2026-08-28", "daa": "2026-08-28T04:00:00.000Z", "company": "Exascale Labs Holdings Inc. Warrant", "exchange": "NASDAQ", "actions": "Expected", "shares": null, "priceRange": null, "marketCap": null},
 {"symbol": "XLAB", "date": "2026-08-28", "daa": "2026-08-28T04:00:00.000Z", "company": "Exascale Labs Holdings Inc. Class A Common Stock", "exchange": "NASDAQ", "actions": "Expected", "shares": null, "priceRange": null, "marketCap": null},
 {"symbol": "PSQLW", "date": "2026-08-28", "daa": "2026-08-28T04:00:00.000Z", "company": "Pasqal Holding SA Warrant", "exchange": "NASDAQ", "actions": "Expected", "shares": null, "priceRange": null, "marketCap": null},
 {"symbol": "IPHXU", "date": "2026-08-28", "daa": "2026-08-28T04:00:00.000Z", "company": "Inflection Point Acquisition Corp. VIII Unit", "exchange": "NASDAQ", "actions": "Expected", "shares": null, "priceRange": null, "marketCap": null},
 {"symbol": "DHGR", "date": "2026-08-28", "daa": "2026-08-28T04:00:00.000Z", "company": "Devonian Health Group Inc.", "exchange": "NYSE", "actions": "Expected", "shares": null, "priceRange": null, "marketCap": null}
]
```

`tests/FmpDotNet.Tests/Fixtures/ipos-calendar.priced.json` — five rows selected from the same response by having at least one numeric field populated, because the head fixture has none and a model that always answered null would pass against it alone. Note **`priceRange` is a string in both its forms** — a range and a single price — and note that `SCATU` and `JTTT` carry a null `priceRange` beside a populated `shares` and `marketCap`, so the three fields are independently absent:

```json
[
 {"symbol": "MOT", "date": "2026-08-14", "daa": "2026-08-14T04:00:00.000Z", "company": "MetaOptics Ltd", "exchange": "NASDAQ", "actions": "Expected", "shares": 3000000, "priceRange": "5.00 - 7.00", "marketCap": 24150000},
 {"symbol": "OCLT", "date": "2026-08-06", "daa": "2026-08-06T04:00:00.000Z", "company": "OceanLight Acquisition Corp", "exchange": "NASDAQ", "actions": "Expected", "shares": 10000000, "priceRange": "10.00", "marketCap": 115000000},
 {"symbol": "NCOU", "date": "2026-07-21", "daa": "2026-07-21T04:00:00.000Z", "company": "Southern Cross Acquisition I Corp.", "exchange": "NASDAQ", "actions": "Expected", "shares": 10000000, "priceRange": "10.00", "marketCap": 100000000},
 {"symbol": "SCATU", "date": "2026-08-26", "daa": "2026-08-26T04:00:00.000Z", "company": "Southern Cross Acquisition II Corp.", "exchange": "NASDAQ", "actions": "Priced", "shares": 7500000, "priceRange": null, "marketCap": 75000000},
 {"symbol": "JTTT", "date": "2026-08-26", "daa": "2026-08-26T04:00:00.000Z", "company": "JATT III Acquisition Corp", "exchange": "NASDAQ", "actions": "Priced", "shares": 6000000, "priceRange": null, "marketCap": 60000000}
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/IpoTests.cs` — created here with the calendar tests; Task 5 appends the disclosure and prospectus tests to the same file. `Build` and `Day` as in the previous two tasks:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three <c>stable/ipos-*</c> paths, checked against captures taken live 2026-08-28.
///
/// <para>They are three different shapes under one heading. <c>ipos-calendar</c> is a scheduling feed, mostly
/// unpriced, clamped to a 90-day window. <c>ipos-disclosure</c> and <c>ipos-prospectus</c> are EDGAR filing
/// feeds that answer whatever range they are given — 25,689 rows for a full 2024 on the first.</para>
///
/// <para><b><c>acceptedDate</c> means something different here than on the SEC filing paths.</b> Every
/// date-shaped field on both filing feeds was 10 characters — a plain ISO date, measured across 8,856 and 165
/// rows. <see cref="SecFiling.AcceptedDate"/> reads a 19-character Eastern wall clock through a different
/// converter, and pointing that converter at these fields would answer null for every row without
/// erroring.</para></summary>
public class IpoTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- ipos-calendar: binding ---------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_calendar_row_binds_its_six_populated_fields_and_nulls_the_other_three()
    {
        var (endpoints, _) = Build(Binding.Fixture("ipos-calendar.head.json"));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2026, 1, 1), Day(2026, 8, 28));

        Assert.Equal(5, rows.Count);
        // The measured norm, not a gap in the capture: shares null on 349 of 450 rows, priceRange on 441,
        // marketCap on 354. An unpriced scheduling entry is what this feed mostly holds.
        Assert.Equal(["MarketCap", "PriceRange", "Shares"], Binding.Unbound(rows[0]));
        Assert.Equal("XLABW", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 28), rows[0].Date);
        Assert.Equal("Exascale Labs Holdings Inc. Warrant", rows[0].Company);
        Assert.Equal("NASDAQ", rows[0].Exchange);
        Assert.Equal("Expected", rows[0].Actions);
    }

    [Fact]
    public async Task A_priced_row_binds_all_nine_and_reads_priceRange_as_the_string_FMP_sent()
    {
        // The reason this second fixture exists. Typed decimal?, PriceRange would read null on all 450 rows --
        // null where FMP sent null and null where FMP sent a price -- and the head fixture alone could never
        // show the difference, because every row in it is null anyway.
        var (endpoints, _) = Build(Binding.Fixture("ipos-calendar.priced.json"));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("5.00 - 7.00", rows[0].PriceRange);   // a range
        Assert.Equal("10.00", rows[1].PriceRange);         // a single price, same field
        Assert.Equal(3_000_000m, rows[0].Shares);
        Assert.Equal(24_150_000m, rows[0].MarketCap);
    }

    [Fact]
    public async Task The_three_numeric_fields_are_absent_independently_of_each_other()
    {
        // SCATU and JTTT carry a populated shares and marketCap beside a null priceRange, so a caller cannot
        // gate all three on any one of them.
        var (endpoints, _) = Build(Binding.Fixture("ipos-calendar.priced.json"));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        var scatu = Assert.Single(rows, r => r.Symbol == "SCATU");
        Assert.Null(scatu.PriceRange);
        Assert.Equal(7_500_000m, scatu.Shares);
        Assert.Equal(75_000_000m, scatu.MarketCap);
    }

    [Fact]
    public void A_market_cap_beyond_int_binds_rather_than_throwing()
    {
        // 74,999,999,925 was the measured maximum across 450 rows -- about thirty-five times int.MaxValue
        // (2,147,483,647). An int? property does NOT read an out-of-range value as null: System.Text.Json
        // throws, and because FmpTransport does not wrap DeserializeAsync, that one row would cost the whole
        // response. decimal?, matching MarketCapitalization.MarketCap and SharesFloat.OutstandingShares.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"BIG","shares":555555555,"marketCap":74999999925}]""",
            FmpJsonContext.Default.ListIpoCalendarEntry)![0];

        Assert.Equal(74_999_999_925m, row.MarketCap);
        Assert.Equal(555_555_555m, row.Shares);
    }

    [Fact]
    public async Task Daa_is_the_date_twice_and_is_documented_as_carrying_nothing()
    {
        // All 450 rows checked on 2026-08-28: daa's date part equalled `date` in 450 of 450, and its time part
        // took exactly one distinct value across the whole response, T04:00:00.000Z -- midnight Eastern. It is
        // kept as the raw string rather than parsed, because a parsed second date property could never disagree
        // with the first and would invite a caller to think it might.
        var (endpoints, _) = Build(Binding.Fixture("ipos-calendar.head.json"));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2026, 1, 1), Day(2026, 8, 28));

        Assert.All(rows, r =>
        {
            Assert.NotNull(r.Daa);
            Assert.StartsWith(r.Date!.Value.ToString("uuuu-MM-dd", null), r.Daa);
            Assert.EndsWith("T04:00:00.000Z", r.Daa);
        });
    }

    // ---- ipos-calendar: request and window ----------------------------------------------------------------

    [Fact]
    public async Task The_calendar_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIpoCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal("stable/ipos-calendar", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-06-01&to=2026-08-28&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_through_the_shared_guard()
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIpoCalendarAsync(Day(2026, 8, 28), Day(2026, 6, 1)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_calendar_reports_the_same_ninety_day_window_as_the_splits_calendar()
    {
        // Measured 2026-08-28 against four `to` values twenty months apart, `from` fixed at 2015-01-01: the
        // earliest row returned was 90 days before `to` every time. A full 2024 answered Q4 at 358 rows.
        var (endpoints, _) = Build(Binding.Fixture("ipos-calendar.head.json"));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2026, 8, 28), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<IpoCalendarEntry>>(rows);
        Assert.Equal(90, result.LookbackLimitDays);
        Assert.Null(result.RowCap);
    }

    [Fact]
    public async Task A_full_year_request_reports_itself_truncated()
    {
        var (endpoints, _) = Build(SyntheticCalendar(358, Day(2024, 10, 2)));

        var rows = await endpoints.GetIpoCalendarAsync(Day(2024, 1, 1), Day(2024, 12, 31));

        var result = Assert.IsType<CalendarResult<IpoCalendarEntry>>(rows);
        Assert.True(result.LikelyTruncated);
        Assert.True(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.False(result.AtRowCap);
    }

    private static string SyntheticCalendar(int rowCount, LocalDate earliest)
    {
        var json = new System.Text.StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{i}}","date":"{{earliest:uuuu-MM-dd}}","daa":"{{earliest:uuuu-MM-dd}}T04:00:00.000Z","company":"C","exchange":"NASDAQ","actions":"Expected","shares":null,"priceRange":null,"marketCap":null}""");
        }
        return json.Append(']').ToString();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IpoTests"`
Expected: the build fails — `CS0246` for `IpoCalendarEntry`, `CS1061` for `GetIpoCalendarAsync`.

- [ ] **Step 4: Write the record**

`src/FmpDotNet/Models/Ipo.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One scheduled or priced offering from <c>stable/ipos-calendar</c>.
///
/// <para><b>This is mostly a scheduling feed, not a pricing one.</b> Measured across 450 rows on 2026-08-28,
/// <see cref="Shares"/> was null on 349, <see cref="PriceRange"/> on 441 and <see cref="MarketCap"/> on 354 —
/// and the three are absent independently, so a row can carry a share count and a market cap with no price
/// range beside them. <see cref="Actions"/> was <c>Expected</c> on 359 rows and <c>Priced</c> on 91, and even
/// among the 102 rows with any numeric populated, 11 were still <c>Expected</c>. Gate on the field you are
/// about to read, not on the label.</para></summary>
public sealed record IpoCalendarEntry
{
    /// <summary>The ticker the offering will trade under. Warrants and units appear as their own rows with
    /// their own tickers — <c>XLABW</c> beside <c>XLAB</c>, <c>IPHXU</c> for a unit — so one company can occupy
    /// several rows on one date.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The offering date, and the date this path selects on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary><b>The same value as <see cref="Date"/>, in a different format, and it carries nothing.</b>
    ///
    /// <para>Measured across all 450 rows on 2026-08-28: the date part of <c>daa</c> equalled <c>date</c> in
    /// <b>450 of 450</b>, and the time part took exactly <b>one</b> distinct value across the whole response —
    /// <c>T04:00:00.000Z</c>, which is midnight Eastern. So this is <see cref="Date"/> at midnight in EDT,
    /// expressed as UTC, under a name that explains neither.</para>
    ///
    /// <para>Kept as the raw string rather than parsed to a date or an instant, deliberately. Parsing it would
    /// manufacture a second temporal property that cannot disagree with <see cref="Date"/> and would invite a
    /// caller to think it might mean something else. <b>Use <see cref="Date"/>.</b></para></summary>
    [JsonPropertyName("daa")] public string? Daa { get; init; }

    /// <summary>The issuer's name as FMP writes it, including the instrument — <c>"… Warrant"</c>,
    /// <c>"… Class A Common Stock"</c>, <c>"… Unit"</c>.</summary>
    [JsonPropertyName("company")] public string? Company { get; init; }

    /// <summary>Where it lists. Two values across 450 rows measured 2026-08-28, <c>NASDAQ</c> and <c>NYSE</c> —
    /// a string rather than an enum, because two values from one response is a sample, not a domain.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>FMP's status label: <c>Expected</c> on 359 of 450 rows and <c>Priced</c> on 91, measured
    /// 2026-08-28. Note it does not partition the numeric fields — 11 of the 102 rows carrying a populated
    /// number were still labelled <c>Expected</c>.</summary>
    [JsonPropertyName("actions")] public string? Actions { get; init; }

    /// <summary>Shares offered, or <see langword="null"/> — which is the common case, 349 of 450.
    ///
    /// <para><see langword="decimal"/> rather than an integer type, matching
    /// <see cref="SharesFloat.OutstandingShares"/>. The measured maximum was 555,555,555, which does fit an
    /// <see cref="int"/>; the type follows the SDK's existing convention for share counts rather than the
    /// narrowest thing today's sample allows.</para></summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The offering price or price band, <b>as a formatted string</b>, or <see langword="null"/> —
    /// which is overwhelmingly the common case, 441 of 450.
    ///
    /// <para><b>Not a number, and this was measured rather than assumed.</b> The nine populated values on
    /// 2026-08-28 were all strings, in two shapes: six ranges (<c>"5.00 - 7.00"</c>, <c>"15 - 17"</c>,
    /// <c>"11.25 - 13.25"</c>) and three single prices (<c>"10.00"</c>). Typed <see langword="decimal"/> this
    /// property would read <b>null on all 450 rows</b> — null where FMP sent null, and null where FMP sent a
    /// price — with nothing in the data to tell the two apart. It is the same kind of field as
    /// <see cref="SecProfile.FiftyTwoWeekRange"/>.</para>
    ///
    /// <para>The SDK does not split or parse it: both shapes are real, the separator is not guaranteed, and a
    /// caller who wants numbers can see which shape they have.</para></summary>
    [JsonPropertyName("priceRange")] public string? PriceRange { get; init; }

    /// <summary>Expected market capitalisation at the offering, or <see langword="null"/> — 354 of 450.
    ///
    /// <para><b><see langword="decimal"/> and never a narrower type.</b> Measured 2026-08-28, values ran to
    /// <b>74,999,999,925</b> — about thirty-five times <see cref="int"/>'s ceiling of 2,147,483,647. An
    /// <see cref="int"/> property does not read an out-of-range value as null: <c>System.Text.Json</c> throws,
    /// and <c>FmpTransport</c> does not wrap <c>DeserializeAsync</c>, so a single such row would cost the caller
    /// the whole response. Same rule and same reason as
    /// <see cref="MarketCapitalization.MarketCap"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}
```

- [ ] **Step 4a: Promote the deferred cross-reference in `StockSplit`**

Task 3 could not write a `<see cref>` to a type Task 4 had not created yet — `CS1574` is a build error here. Now that `IpoCalendarEntry` exists, promote it. In `src/FmpDotNet/Models/StockSplit.cs`, on `Numerator`:

```diff
-    /// widening against a fractional value nobody has seen — and the same check went the other way on
--    /// <c>IpoCalendarEntry.MarketCap</c>, which does not fit.</para></summary>
+    /// widening against a fractional value nobody has seen — and the same check went the other way on
+    /// <see cref="IpoCalendarEntry.MarketCap"/>, which does not fit.</para></summary>
```

Build immediately after this edit — `dotnet build src/FmpDotNet` — because a typo here is `CS1574` and will otherwise surface as a confusing failure three steps later.

- [ ] **Step 5: Register the record**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<IpoCalendarEntry>))]`

- [ ] **Step 6: Write the method**

Append to `CalendarEndpoints.cs`:

```csharp
    /// <summary>Every offering FMP has scheduled or priced in a date range, from <c>stable/ipos-calendar</c>.
    ///
    /// <para><b>This path will not reach more than 90 days back from <paramref name="to"/>, exactly as
    /// <see cref="GetSplitsCalendarAsync"/> does, and it drops the front of the range without saying so.</b>
    /// Measured 2026-08-28 against four <c>to</c> values spanning twenty months, each with <c>from</c> fixed at
    /// 2015-01-01, the earliest row returned was 90 days before <c>to</c> every time. A request for the whole of
    /// 2024 answered Q4 of 2024, at <b>358 rows</b> — no cap was reached and none was measured on this path, so
    /// <see cref="CalendarResult{T}.MissesStartOfRange"/> is what catches it.</para>
    ///
    /// <para><b>Most rows are unpriced.</b> <see cref="IpoCalendarEntry.PriceRange"/> was null on 441 of 450
    /// rows, <see cref="IpoCalendarEntry.Shares"/> on 349 and <see cref="IpoCalendarEntry.MarketCap"/> on 354.
    /// A row per warrant and per unit is normal, so one company can occupy several rows on one date.</para>
    ///
    /// <para>Rows whose <c>date</c> cannot be parsed are dropped, for the reason recorded on this
    /// class.</para></summary>
    /// <param name="from">First day of the range, inclusive. Anything more than 90 days before
    /// <paramref name="to"/> is silently ignored.</param>
    /// <param name="to">Last day of the range, inclusive, and the anchor the 90-day window is measured
    /// from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CalendarResult{T}"/> of <see cref="IpoCalendarEntry"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoCalendarEntry>> GetIpoCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ipos-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoCalendarEntry, ct).ConfigureAwait(false);

        LocalDate? earliest = null;
        foreach (var row in rows)
            if (row?.Date is { } date && (earliest is null || date < earliest)) earliest = date;

        var kept = new List<IpoCalendarEntry>(rows.Count);
        foreach (var row in rows)
            if (row is { Date: not null }) kept.Add(row);

        return new CalendarResult<IpoCalendarEntry>(
            kept, rows.Count, from, to, earliest, rowCap: null, lookbackLimitDays: 90);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IpoTests"`
Expected: PASS — 9 test methods, 9 test cases. Zero warnings.

- [ ] **Step 8: Mutation-check the two typings the spec got wrong**

**Mutation A — `PriceRange` becomes `decimal?`.** Change the property type and delete nothing else.

Run the filter above.
Expected: a **compile failure** — `CS0019` or `CS1503` at `Assert.Equal("5.00 - 7.00", rows[0].PriceRange)`, because a `decimal?` cannot be compared to a string. A compile failure is a *stronger* result than a failing test and should be reported as such: this typing cannot be got wrong silently once the test exists. Note what would have happened without the test, though, and put it in your report — the property would have bound null on every one of the 450 measured rows and nothing would have thrown.

**Mutation B — `MarketCap` becomes `int?`.** Change the property type.

Run the filter above.
Expected: exactly **1** failure — `A_market_cap_beyond_int_binds_rather_than_throwing`, and it fails with a `System.Text.Json.JsonException` rather than an assertion mismatch. That distinction is the finding: the value does not read as null, it aborts the deserialisation of the entire response. Restore both with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/Ipo.cs src/FmpDotNet/Models/StockSplit.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/IpoTests.cs \
        tests/FmpDotNet.Tests/Fixtures/ipos-calendar.head.json \
        tests/FmpDotNet.Tests/Fixtures/ipos-calendar.priced.json
git commit -m "feat: the IPO calendar, its 90-day window and its string price range (#37)"
```

---

### Task 5: `IpoDisclosure` and `IpoProspectus`, the two feeds that answer everything you ask for

The opposite failure mode from the last three tasks. Neither path truncates — both were measured returning the full requested range — so **neither returns a `CalendarResult<T>`**, and inventing a truncation signal for them would be a fact nobody measured. What they do instead is answer a wide range with 123,678 rows, which is a payload problem and is documented as one.

**Files:**
- Modify: `src/FmpDotNet/Models/Ipo.cs` (+2 records), `tests/FmpDotNet.Tests/IpoTests.cs` (+tests), `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (+2 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+2 entries)
- Create: `tests/FmpDotNet.Tests/Fixtures/ipos-disclosure.head.json`, `ipos-prospectus.head.json`

**Interfaces:**
- Consumes: `DateRange.ThrowIfBackwards`, `NullableLocalDateJsonConverter`, and `IpoCalendarEntry` from Task 4 (same file — append below it, do not create a second file). The `Build` and `Day` helpers already exist in `IpoTests`.
- Produces: `public sealed record IpoDisclosure` with `Symbol`, `Cik`, `Form`, `Url` (`string?`) and `FilingDate`, `AcceptedDate`, `EffectivenessDate` (`LocalDate?`). `public sealed record IpoProspectus` with `Symbol`, `Cik`, `Form`, `Url` (`string?`), `AcceptedDate`, `FilingDate`, `IpoDate` (`LocalDate?`), and six `decimal?` money fields: `PricePublicPerShare`, `PricePublicTotal`, `DiscountsAndCommissionsPerShare`, `DiscountsAndCommissionsTotal`, `ProceedsBeforeExpensesPerShare`, `ProceedsBeforeExpensesTotal`. `CalendarEndpoints.GetIpoDisclosuresAsync(LocalDate from, LocalDate to, CancellationToken ct = default)` and `GetIpoProspectusesAsync(LocalDate from, LocalDate to, CancellationToken ct = default)`, both returning `Task<IReadOnlyList<T>>` — a **plain list**, not a `CalendarResult<T>`.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/ipos-disclosure.head.json` — the first five rows of `stable/ipos-disclosure?from=2026-08-01&to=2026-08-28`, captured 2026-08-28, verbatim. All five share one CIK, one form and one URL under five different tickers: a single `CERT` filing covering five share classes of one fund. That is the wire shape and not a truncated capture — do not deduplicate it:

```json
[
 {"symbol": "HLPPX", "filingDate": "2026-08-28", "acceptedDate": "2026-08-28", "effectivenessDate": "2026-08-28", "cik": "0001040674", "form": "CERT", "url": "https://www.sec.gov/Archives/edgar/data/1040674/000114336226000329/FRUT082826.pdf"},
 {"symbol": "DCRIX", "filingDate": "2026-08-28", "acceptedDate": "2026-08-28", "effectivenessDate": "2026-08-28", "cik": "0001040674", "form": "CERT", "url": "https://www.sec.gov/Archives/edgar/data/1040674/000114336226000329/FRUT082826.pdf"},
 {"symbol": "DCRAX", "filingDate": "2026-08-28", "acceptedDate": "2026-08-28", "effectivenessDate": "2026-08-28", "cik": "0001040674", "form": "CERT", "url": "https://www.sec.gov/Archives/edgar/data/1040674/000114336226000329/FRUT082826.pdf"},
 {"symbol": "EPSYX", "filingDate": "2026-08-28", "acceptedDate": "2026-08-28", "effectivenessDate": "2026-08-28", "cik": "0001040674", "form": "CERT", "url": "https://www.sec.gov/Archives/edgar/data/1040674/000114336226000329/FRUT082826.pdf"},
 {"symbol": "CSIAX", "filingDate": "2026-08-28", "acceptedDate": "2026-08-28", "effectivenessDate": "2026-08-28", "cik": "0001040674", "form": "CERT", "url": "https://www.sec.gov/Archives/edgar/data/1040674/000114336226000329/FRUT082826.pdf"}
]
```

`tests/FmpDotNet.Tests/Fixtures/ipos-prospectus.head.json` — the first five rows of `stable/ipos-prospectus?from=2026-08-01&to=2026-08-28`, captured 2026-08-28, verbatim. Three things in it are worth not tidying: `AVCO` has an `acceptedDate` a day **before** its `filingDate`; `AVCO`'s `pricePublicPerShare` of 300 against a `pricePublicTotal` of 273 is arithmetically absurd and is what FMP sent; and `QDMI` repeats the same 10,709,298 across three unrelated fields. The SDK reports all of it unchanged:

```json
[
 {"symbol": "CHEK", "acceptedDate": "2026-08-27", "filingDate": "2026-08-27", "ipoDate": "2015-02-16", "cik": "0001610590", "pricePublicPerShare": 6.5, "pricePublicTotal": 10000003, "discountsAndCommissionsPerShare": 0.39, "discountsAndCommissionsTotal": 600000.18, "proceedsBeforeExpensesPerShare": 6.11, "proceedsBeforeExpensesTotal": 9400002.82, "form": "424B4", "url": "https://www.sec.gov/Archives/edgar/data/1610590/000121390026094129/ea0303478-424b4_checkcap.htm"},
 {"symbol": "CHEKZ", "acceptedDate": "2026-08-27", "filingDate": "2026-08-27", "ipoDate": "2018-05-03", "cik": "0001610590", "pricePublicPerShare": 6.5, "pricePublicTotal": 10000003, "discountsAndCommissionsPerShare": 0.39, "discountsAndCommissionsTotal": 600000.18, "proceedsBeforeExpensesPerShare": 6.11, "proceedsBeforeExpensesTotal": 9400002.82, "form": "424B4", "url": "https://www.sec.gov/Archives/edgar/data/1610590/000121390026094129/ea0303478-424b4_checkcap.htm"},
 {"symbol": "AVCO", "acceptedDate": "2026-08-24", "filingDate": "2026-08-25", "ipoDate": "2016-02-21", "cik": "0001630212", "pricePublicPerShare": 300, "pricePublicTotal": 273, "discountsAndCommissionsPerShare": 3, "discountsAndCommissionsTotal": 320546, "proceedsBeforeExpensesPerShare": 300, "proceedsBeforeExpensesTotal": 18260976, "form": "S-1/A", "url": "https://www.sec.gov/Archives/edgar/data/1630212/000121390026093149/ea0302304-s1a1_change.htm"},
 {"symbol": "MI", "acceptedDate": "2026-08-24", "filingDate": "2026-08-24", "ipoDate": "2015-11-24", "cik": "0001958713", "pricePublicPerShare": 4.6, "pricePublicTotal": 2008267.92, "discountsAndCommissionsPerShare": 0.28, "discountsAndCommissionsTotal": 120496.08, "proceedsBeforeExpensesPerShare": 4.32, "proceedsBeforeExpensesTotal": 1887771.84, "form": "424B4", "url": "https://www.sec.gov/Archives/edgar/data/1958713/000121390026092917/ea0303127-424b4_nftlimited.htm"},
 {"symbol": "QDMI", "acceptedDate": "2026-08-24", "filingDate": "2026-08-25", "ipoDate": "2000-06-22", "cik": "0001094032", "pricePublicPerShare": 10709298, "pricePublicTotal": 10709298, "discountsAndCommissionsPerShare": 0, "discountsAndCommissionsTotal": 0, "proceedsBeforeExpensesPerShare": 10709298, "proceedsBeforeExpensesTotal": 10709298, "form": "S-1/A", "url": "https://www.sec.gov/Archives/edgar/data/1094032/000121390026093210/ea0213156-10.htm"}
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/IpoTests.cs`, above the private `SyntheticCalendar` helper:

```csharp
    // ---- ipos-disclosure ----------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_disclosure_row_binds_all_seven_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("ipos-disclosure.head.json"));

        var rows = await endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("HLPPX", rows[0].Symbol);
        Assert.Equal("0001040674", rows[0].Cik);
        Assert.Equal("CERT", rows[0].Form);
        Assert.Equal(Day(2026, 8, 28), rows[0].FilingDate);
        Assert.Equal(Day(2026, 8, 28), rows[0].AcceptedDate);
        Assert.Equal(Day(2026, 8, 28), rows[0].EffectivenessDate);
        Assert.EndsWith("FRUT082826.pdf", rows[0].Url);
    }

    [Fact]
    public async Task One_filing_appears_once_per_share_class_it_covers()
    {
        // All five captured rows share a CIK, a form and a URL under five different tickers. A caller
        // deduplicating on `url` would collapse five real rows into one.
        var (endpoints, _) = Build(Binding.Fixture("ipos-disclosure.head.json"));

        var rows = await endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.Single(rows.Select(r => r.Url).Distinct());
        Assert.Equal(5, rows.Select(r => r.Symbol).Distinct().Count());
    }

    [Fact]
    public void The_cik_keeps_its_leading_zeros_because_it_is_a_string()
    {
        var row = JsonSerializer.Deserialize(
            """[{"cik":"0001040674"}]""", FmpJsonContext.Default.ListIpoDisclosure)![0];

        Assert.Equal("0001040674", row.Cik);
    }

    [Fact]
    public async Task The_disclosure_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.Equal("stable/ipos-disclosure", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-08-01&to=2026-08-28&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_disclosure_path_returns_a_plain_list_because_no_truncation_was_measured()
    {
        // The opposite ruling from the three calendars, from the same kind of evidence: a full 2024 answered
        // 25,689 rows spanning 2024-01-02 to 2024-12-31 -- the whole year, nothing clamped. Wrapping this in a
        // CalendarResult would offer a truncation signal that has nothing to report.
        var (endpoints, _) = Build(Binding.Fixture("ipos-disclosure.head.json"));

        var rows = await endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.IsNotType<CalendarResult<IpoDisclosure>>(rows);
    }

    // ---- ipos-prospectus ----------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_prospectus_row_binds_all_thirteen_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("ipos-prospectus.head.json"));

        var rows = await endpoints.GetIpoProspectusesAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("CHEK", rows[0].Symbol);
        Assert.Equal("0001610590", rows[0].Cik);
        Assert.Equal("424B4", rows[0].Form);
        Assert.Equal(Day(2026, 8, 27), rows[0].AcceptedDate);
        Assert.Equal(Day(2026, 8, 27), rows[0].FilingDate);
        Assert.Equal(Day(2015, 2, 16), rows[0].IpoDate);
        Assert.Equal(6.5m, rows[0].PricePublicPerShare);
        Assert.Equal(10_000_003m, rows[0].PricePublicTotal);
        Assert.Equal(0.39m, rows[0].DiscountsAndCommissionsPerShare);
        Assert.Equal(600_000.18m, rows[0].DiscountsAndCommissionsTotal);
        Assert.Equal(6.11m, rows[0].ProceedsBeforeExpensesPerShare);
        Assert.Equal(9_400_002.82m, rows[0].ProceedsBeforeExpensesTotal);
    }

    [Fact]
    public async Task An_accepted_date_can_precede_its_filing_date_and_the_sdk_does_not_correct_it()
    {
        // AVCO in the captured page: accepted 2026-08-24, filed 2026-08-25. The two are independent fields and
        // nothing here orders them.
        var (endpoints, _) = Build(Binding.Fixture("ipos-prospectus.head.json"));

        var rows = await endpoints.GetIpoProspectusesAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        var avco = Assert.Single(rows, r => r.Symbol == "AVCO");
        Assert.Equal(Day(2026, 8, 24), avco.AcceptedDate);
        Assert.Equal(Day(2026, 8, 25), avco.FilingDate);
        Assert.True(avco.AcceptedDate < avco.FilingDate);
    }

    [Fact]
    public async Task The_money_fields_are_reported_exactly_as_sent_however_implausible()
    {
        // AVCO: 300 per share against a total of 273. QDMI: 10,709,298 repeated across three unrelated fields.
        // Both are what FMP sent, and neither is corrected, flagged or dropped -- the alternative would be the
        // SDK inventing a plausibility rule it cannot justify.
        var (endpoints, _) = Build(Binding.Fixture("ipos-prospectus.head.json"));

        var rows = await endpoints.GetIpoProspectusesAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        var avco = Assert.Single(rows, r => r.Symbol == "AVCO");
        Assert.Equal(300m, avco.PricePublicPerShare);
        Assert.Equal(273m, avco.PricePublicTotal);
    }

    [Fact]
    public void A_prospectus_total_beyond_int_binds_rather_than_throwing()
    {
        // Measured maxima across 165 rows on 2026-08-28: pricePublicTotal 74,999,999,925 and
        // proceedsBeforeExpensesTotal 74,499,999,925, both about thirty-five times int.MaxValue. Same rule and
        // same failure mode as IpoCalendarEntry.MarketCap: an int? would throw, not read null.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"BIG","pricePublicTotal":74999999925,"proceedsBeforeExpensesTotal":74499999925}]""",
            FmpJsonContext.Default.ListIpoProspectus)![0];

        Assert.Equal(74_999_999_925m, row.PricePublicTotal);
        Assert.Equal(74_499_999_925m, row.ProceedsBeforeExpensesTotal);
    }

    [Fact]
    public void Every_date_on_both_filing_feeds_is_a_plain_ten_character_date_not_an_eastern_timestamp()
    {
        // The trap this pair shares with the SEC filing paths, in the other direction. SecFiling.AcceptedDate
        // reads a 19-character "uuuu-MM-dd HH:mm:ss" Eastern wall clock; every date-shaped field here was 10
        // characters on all 8,856 disclosure rows and all 165 prospectus rows measured 2026-08-28. Pointing
        // NullableEasternInstantJsonConverter at these would answer null for every row and never throw.
        var disclosure = JsonSerializer.Deserialize(
            """[{"filingDate":"2026-08-26","acceptedDate":"2026-08-26","effectivenessDate":"2026-08-26"}]""",
            FmpJsonContext.Default.ListIpoDisclosure)![0];
        var prospectus = JsonSerializer.Deserialize(
            """[{"filingDate":"2026-05-29","acceptedDate":"2026-05-29","ipoDate":"1989-03-02"}]""",
            FmpJsonContext.Default.ListIpoProspectus)![0];

        Assert.Equal(Day(2026, 8, 26), disclosure.AcceptedDate);
        Assert.Equal(Day(2026, 8, 26), disclosure.EffectivenessDate);
        Assert.Equal(Day(2026, 5, 29), prospectus.AcceptedDate);
        Assert.Equal(Day(1989, 3, 2), prospectus.IpoDate);
    }

    [Fact]
    public async Task The_prospectus_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIpoProspectusesAsync(Day(2026, 8, 1), Day(2026, 8, 28));

        Assert.Equal("stable/ipos-prospectus", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-08-01&to=2026-08-28&apikey=k", handler.Requests.Single().Query);
    }

    [Theory]
    [InlineData("disclosures")]
    [InlineData("prospectuses")]
    public async Task A_backwards_range_is_refused_on_both_filing_feeds(string which)
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => which == "disclosures"
            ? endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 28), Day(2026, 8, 1))
            : endpoints.GetIpoProspectusesAsync(Day(2026, 8, 28), Day(2026, 8, 1)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }
```

Note the `[Theory]` above returns a `Task` from a ternary whose two branches have different generic types — `Task<IReadOnlyList<IpoDisclosure>>` and `Task<IReadOnlyList<IpoProspectus>>`. That does not compile as written. Write it as two statements instead:

```csharp
    [Theory]
    [InlineData("disclosures")]
    [InlineData("prospectuses")]
    public async Task A_backwards_range_is_refused_on_both_filing_feeds(string which)
    {
        var (endpoints, handler) = Build();
        Func<Task> call = which == "disclosures"
            ? () => endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 28), Day(2026, 8, 1))
            : () => endpoints.GetIpoProspectusesAsync(Day(2026, 8, 28), Day(2026, 8, 1));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(call);

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }
```

Use the second form. The first is shown only so the compile error is recognised rather than debugged.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IpoTests"`
Expected: the build fails — `CS0246` for `IpoDisclosure` and `IpoProspectus`, `CS1061` for the two methods. Task 4's nine tests are in the same file and cannot run either; that is expected and does not mean they regressed.

- [ ] **Step 4: Write the two records**

Append to `src/FmpDotNet/Models/Ipo.cs`, below `IpoCalendarEntry`:

```csharp
/// <summary>One EDGAR filing marking a registration as effective, from <c>stable/ipos-disclosure</c>.
///
/// <para><b>Every field was populated on every row measured</b> — 8,856 rows on 2026-08-28 — so this record has
/// no measured absent value, which is unusual in this SDK and worth stating rather than leaving to be
/// discovered.</para>
///
/// <para><b>One filing appears once per share class it covers.</b> All five rows of the captured page share a
/// CIK, a form and a URL under five different tickers: a single <c>CERT</c> covering five classes of one fund.
/// A caller deduplicating on <see cref="Url"/> collapses five real rows into one.</para>
///
/// <para><b>The three dates are plain dates, not timestamps.</b> All three were 10 characters on all 8,856 rows
/// — read the note on <see cref="AcceptedDate"/> before reaching for a converter.</para></summary>
public sealed record IpoDisclosure
{
    /// <summary>The ticker the filing covers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When the filing was made.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, <b>as a plain date</b>.
    ///
    /// <para><b>This is not the same kind of value as <see cref="SecFiling.AcceptedDate"/>, despite the
    /// identical field name.</b> That one is a 19-character <c>uuuu-MM-dd HH:mm:ss</c> EDGAR wall clock in US
    /// Eastern, read through <see cref="NullableEasternInstantJsonConverter"/>. This one was <b>10 characters on
    /// all 8,856 rows</b> measured 2026-08-28 — there is no time of day in it at all. Pointing the Eastern
    /// converter at this field would answer <see langword="null"/> for every row and never throw, which is the
    /// silent kind of wrong.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>When the registration became effective.</summary>
    [JsonPropertyName("effectivenessDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? EffectivenessDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters — <c>"0001040674"</c>. A
    /// string and never a number: parsing it loses the padding EDGAR uses.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The EDGAR form type — <c>CERT</c> on the captured page.</summary>
    [JsonPropertyName("form")] public string? Form { get; init; }

    /// <summary>Direct link to the filing on <c>sec.gov</c>. Shared across every row of the same filing.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>One prospectus filing and the offering economics on it, from <c>stable/ipos-prospectus</c>.
///
/// <para><b>Every field was populated on every one of the 165 rows measured</b> on 2026-08-28.</para>
///
/// <para><b>The money fields are reported exactly as FMP sent them, including where that is absurd.</b> One
/// captured row carries a price of 300 per share against a total of 273; another repeats 10,709,298 across
/// three unrelated fields. The SDK does not correct, flag or drop them — a plausibility rule here would be the
/// SDK inventing a fact, and the values are what a caller needs to see in order to judge them.</para>
///
/// <para>Every date here is a plain 10-character date, as on <see cref="IpoDisclosure"/>, and
/// <see cref="AcceptedDate"/> can fall a day <i>before</i> <see cref="FilingDate"/>.</para></summary>
public sealed record IpoProspectus
{
    /// <summary>The ticker the prospectus covers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When EDGAR accepted the filing, as a plain date. See the note on
    /// <see cref="IpoDisclosure.AcceptedDate"/>: this is not the Eastern timestamp the SEC filing paths carry,
    /// and it can precede <see cref="FilingDate"/> by a day.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>When the prospectus was filed.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The issuer's original IPO date, which can be decades before the filing — <c>1989-03-02</c> and
    /// <c>2000-06-22</c> both appear against 2026 filings. This is a follow-on prospectus feed as much as a
    /// new-issue one.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>Offering price per share to the public.</summary>
    [JsonPropertyName("pricePublicPerShare")] public decimal? PricePublicPerShare { get; init; }

    /// <summary>Total offering value to the public.
    ///
    /// <para><b><see langword="decimal"/> and never a narrower type:</b> measured to <b>74,999,999,925</b>
    /// across 165 rows on 2026-08-28, about thirty-five times <see cref="int"/>'s ceiling, and fractional on 13
    /// of those rows. An <see cref="int"/> here throws rather than reading null, costing the whole
    /// response.</para></summary>
    [JsonPropertyName("pricePublicTotal")] public decimal? PricePublicTotal { get; init; }

    /// <summary>Underwriting discounts and commissions per share.</summary>
    [JsonPropertyName("discountsAndCommissionsPerShare")]
    public decimal? DiscountsAndCommissionsPerShare { get; init; }

    /// <summary>Total underwriting discounts and commissions. Measured to 500,000,000.</summary>
    [JsonPropertyName("discountsAndCommissionsTotal")]
    public decimal? DiscountsAndCommissionsTotal { get; init; }

    /// <summary>Proceeds to the issuer per share, before expenses.</summary>
    [JsonPropertyName("proceedsBeforeExpensesPerShare")]
    public decimal? ProceedsBeforeExpensesPerShare { get; init; }

    /// <summary>Total proceeds to the issuer before expenses. Measured to <b>74,499,999,925</b> — see
    /// <see cref="PricePublicTotal"/> for why this is <see langword="decimal"/>.</summary>
    [JsonPropertyName("proceedsBeforeExpensesTotal")]
    public decimal? ProceedsBeforeExpensesTotal { get; init; }

    /// <summary>The EDGAR form type — <c>424B4</c> and <c>S-1/A</c> on the captured page.</summary>
    [JsonPropertyName("form")] public string? Form { get; init; }

    /// <summary>Direct link to the filing on <c>sec.gov</c>.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
```

- [ ] **Step 5: Register both records**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<IpoDisclosure>))]` and `[JsonSerializable(typeof(List<IpoProspectus>))]`

- [ ] **Step 6: Write the two methods**

Append to `CalendarEndpoints.cs`:

```csharp
    /// <summary>Effectiveness filings for registrations in a date range, from <c>stable/ipos-disclosure</c>.
    ///
    /// <para><b>This path answers the whole range asked for, and that is the thing to plan for.</b> Measured
    /// 2026-08-28: 2024-01-01 to 2024-12-31 returned <b>25,689 rows</b> spanning 2024-01-02 to 2024-12-31, and
    /// 2020-01-01 to 2026-08-28 returned <b>123,678</b>. It is neither capped nor paginated, so a wide range is
    /// a single large response rather than a truncated one — the opposite failure mode from
    /// <see cref="GetIpoCalendarAsync"/>, and the reason this method returns a plain list with no truncation
    /// signal on it. There is nothing to report; there is a payload to budget for.</para>
    ///
    /// <para><b>One filing appears once per share class it covers</b>, sharing a CIK, form and URL across
    /// several tickers — so the row count is not a filing count.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoDisclosure>> GetIpoDisclosuresAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return await transport.GetListAsync(
            new FmpRequest("stable/ipos-disclosure").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoDisclosure, ct).ConfigureAwait(false);
    }

    /// <summary>Prospectus filings and their offering economics in a date range, from
    /// <c>stable/ipos-prospectus</c>.
    ///
    /// <para>Like <see cref="GetIpoDisclosuresAsync"/>, this answers the whole range asked for and is neither
    /// capped nor paginated — 1,048 rows for a full 2024, 15,726 for 2020 to 2026 — so it returns a plain list
    /// with no truncation signal. Smaller than its sibling by roughly twenty-five to one.</para>
    ///
    /// <para><b>It is a follow-on feed as much as a new-issue one:</b>
    /// <see cref="IpoProspectus.IpoDate"/> ran back to 1989 against 2026 filings in the measured sample. And the
    /// money fields are reported exactly as sent — read the remarks on <see cref="IpoProspectus"/> before
    /// treating them as arithmetically consistent.</para></summary>
    /// <param name="from">First day of the range, inclusive.</param>
    /// <param name="to">Last day of the range, inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<IpoProspectus>> GetIpoProspectusesAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return await transport.GetListAsync(
            new FmpRequest("stable/ipos-prospectus").With("from", from).With("to", to),
            FmpJsonContext.Default.ListIpoProspectus, ct).ConfigureAwait(false);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IpoTests"`
Expected: PASS — 21 test methods, **22 test cases** (Task 4's 9 `[Fact]`s, plus the 11 `[Fact]`s and one 2-row `[Theory]` added here). Zero warnings.

- [ ] **Step 8: Mutation-check the converter choice, which fails silently**

Edit `IpoDisclosure.AcceptedDate` to `[JsonConverter(typeof(NullableEasternInstantJsonConverter))]` and its type to `Instant?`.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~IpoTests"`
Expected: a **compile failure** — `CS1503` at the `Assert.Equal(Day(2026, 8, 26), disclosure.AcceptedDate)` comparisons, because an `Instant?` will not compare to a `LocalDate`. Report it as a compile failure, which is the stronger result. Then, because that mutation is caught by the type system rather than by the measurement, do the one that is not: leave the type as `LocalDate?` and change only the converter to `NullableDateAtMidnightJsonConverter` — the one that expects `uuuu-MM-dd HH:mm:ss`.

Run the filter again.
Expected: exactly **2** failures — `A_captured_disclosure_row_binds_all_seven_of_its_fields`, which now reports `AcceptedDate` among the unbound, and `Every_date_on_both_filing_feeds_is_a_plain_ten_character_date_not_an_eastern_timestamp`. The other three disclosure tests do not read that field and pass unchanged. **Nothing throws anywhere, and that silence is the finding**: a wrong date converter on this field does not error, it answers null on every row for ever, and only a test that asserts a specific value against a real capture can see it. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/Ipo.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/IpoTests.cs \
        tests/FmpDotNet.Tests/Fixtures/ipos-disclosure.head.json \
        tests/FmpDotNet.Tests/Fixtures/ipos-prospectus.head.json
git commit -m "feat: the two IPO filing feeds, which answer everything you ask for (#37)"
```

---

### Task 6: The three grade paths, and the one that looks like the others and is not

`fmp.Analyst` goes from one method to four here. The trap is that `grades-consensus` and `grades-historical` both carry five analyst-count fields and are **not** views of the same data — measured the same minute, their totals differ by more than a factor of two.

**Files:**
- Create: `src/FmpDotNet/Models/StockGrade.cs` (three records), `tests/FmpDotNet.Tests/StockGradeTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/grades.AAPL.json`, `grades-consensus.AAPL.json`, `grades-historical.AAPL.json`
- Modify: `src/FmpDotNet/Endpoints/AnalystEndpoints.cs` (+3 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+3 entries)

**Interfaces:**
- Consumes: `NullableLocalDateJsonConverter`. Nothing from Tasks 1–5.
- Produces: `public sealed record StockGrade` (`Symbol`, `GradingCompany`, `PreviousGrade`, `NewGrade`, `Action` as `string?`; `Date` as `LocalDate?`); `public sealed record GradeConsensus` (`Symbol`, `Consensus` as `string?`; `StrongBuy`, `Buy`, `Hold`, `Sell`, `StrongSell` as `int?`); `public sealed record GradeHistory` (`Symbol` as `string?`, `Date` as `LocalDate?`, and `AnalystRatingsStrongBuy`, `AnalystRatingsBuy`, `AnalystRatingsHold`, `AnalystRatingsSell`, `AnalystRatingsStrongSell` as `int?`). Methods: `AnalystEndpoints.GetGradesAsync(string symbol, CancellationToken ct = default)` → `Task<IReadOnlyList<StockGrade>>`; `GetGradeConsensusAsync(string symbol, CancellationToken ct = default)` → `Task<GradeConsensus?>`; `GetGradeHistoryAsync(string symbol, int? limit = null, CancellationToken ct = default)` → `Task<IReadOnlyList<GradeHistory>>`.

**`GetGradesAsync` takes a symbol and nothing else.** Measured 2026-08-28: `grades?symbol=AAPL` answers 1,791 rows; `limit=5` answers 1,791; `limit=10000` answers 1,791; `page=1` answers the same 1,791 with a **byte-identical first row**. Row count varies by symbol — AAPL 1,791, MSFT 967, BRK-B 93 — so this is the whole set each time, not a fixed cap. The endpoint returns everything and there is no way to ask for less, so the signature offers neither `limit` nor `page`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/grades.AAPL.json` — the first five rows of `stable/grades?symbol=AAPL`, captured 2026-08-28, verbatim. All three `action` values appear across these five, and note they are lower case while the grades themselves are title case:

```json
[
 {"symbol": "AAPL", "date": "2026-08-17", "gradingCompany": "Rothschild & Co", "previousGrade": "Neutral", "newGrade": "Buy", "action": "upgrade"},
 {"symbol": "AAPL", "date": "2026-08-10", "gradingCompany": "Jefferies", "previousGrade": "Hold", "newGrade": "Underperform", "action": "downgrade"},
 {"symbol": "AAPL", "date": "2026-08-04", "gradingCompany": "China Renaissance", "previousGrade": "Buy", "newGrade": "Hold", "action": "downgrade"},
 {"symbol": "AAPL", "date": "2026-07-31", "gradingCompany": "Goldman Sachs", "previousGrade": "Buy", "newGrade": "Buy", "action": "maintain"},
 {"symbol": "AAPL", "date": "2026-07-31", "gradingCompany": "Barclays", "previousGrade": "Underweight", "newGrade": "Underweight", "action": "maintain"}
]
```

`tests/FmpDotNet.Tests/Fixtures/grades-consensus.AAPL.json` — `stable/grades-consensus?symbol=AAPL`, captured 2026-08-28, verbatim. The complete response: one row, seven fields, **no date**:

```json
[
 {"symbol": "AAPL", "strongBuy": 1, "buy": 70, "hold": 32, "sell": 9, "strongSell": 0, "consensus": "Buy"}
]
```

`tests/FmpDotNet.Tests/Fixtures/grades-historical.AAPL.json` — the first five rows of `stable/grades-historical?symbol=AAPL&limit=5`, captured 2026-08-28 **in the same pass, minutes apart from the consensus fixture above**. That simultaneity is the evidence for the trap: row 0 here totals 47 analysts against the consensus fixture's 112:

```json
[
 {"symbol": "AAPL", "date": "2026-08-01", "analystRatingsStrongBuy": 6, "analystRatingsBuy": 22, "analystRatingsHold": 14, "analystRatingsSell": 3, "analystRatingsStrongSell": 2},
 {"symbol": "AAPL", "date": "2026-07-01", "analystRatingsStrongBuy": 6, "analystRatingsBuy": 23, "analystRatingsHold": 17, "analystRatingsSell": 2, "analystRatingsStrongSell": 2},
 {"symbol": "AAPL", "date": "2026-06-01", "analystRatingsStrongBuy": 7, "analystRatingsBuy": 23, "analystRatingsHold": 16, "analystRatingsSell": 2, "analystRatingsStrongSell": 2},
 {"symbol": "AAPL", "date": "2026-05-01", "analystRatingsStrongBuy": 7, "analystRatingsBuy": 25, "analystRatingsHold": 16, "analystRatingsSell": 1, "analystRatingsStrongSell": 2},
 {"symbol": "AAPL", "date": "2026-04-01", "analystRatingsStrongBuy": 7, "analystRatingsBuy": 25, "analystRatingsHold": 15, "analystRatingsSell": 1, "analystRatingsStrongSell": 1}
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/StockGradeTests.cs`:

```csharp
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three <c>stable/grades*</c> paths, checked against captures taken live 2026-08-28.
///
/// <para><b>Two of them look like the same data and are not.</b> <c>grades-consensus</c> and
/// <c>grades-historical</c> each carry five analyst-count fields, under different names, and a caller could
/// reasonably assume the first is the current view of the second. Measured the same minute for AAPL, the
/// newest historical row totals <b>47</b> analysts and the consensus totals <b>112</b> — different populations,
/// not a stale copy. They are separate records for that reason.</para></summary>
public class StockGradeTests
{
    private static (AnalystEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new AnalystEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- grades -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_grade_row_binds_all_six_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades.AAPL.json"));

        var rows = await endpoints.GetGradesAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 17), rows[0].Date);
        Assert.Equal("Rothschild & Co", rows[0].GradingCompany);
        Assert.Equal("Neutral", rows[0].PreviousGrade);
        Assert.Equal("Buy", rows[0].NewGrade);
        Assert.Equal("upgrade", rows[0].Action);
    }

    [Fact]
    public async Task A_maintain_carries_the_same_grade_on_both_sides()
    {
        // Two of the five captured rows are `maintain`, and on both the previous and new grades are identical.
        // A caller filtering for "the grade changed" must read `action`, not compare the two grade fields --
        // and must fold case on `action`, which is lower case while the grades are title case.
        var (endpoints, _) = Build(Binding.Fixture("grades.AAPL.json"));

        var rows = await endpoints.GetGradesAsync("AAPL");

        var maintained = rows.Where(r => r.Action == "maintain").ToList();
        Assert.Equal(2, maintained.Count);
        Assert.All(maintained, r => Assert.Equal(r.PreviousGrade, r.NewGrade));
    }

    [Fact]
    public void The_grades_method_offers_neither_a_limit_nor_a_page_because_the_endpoint_ignores_both()
    {
        // Measured 2026-08-28: grades?symbol=AAPL answers 1791 rows; limit=5 answers 1791; limit=10000 answers
        // 1791; page=1 answers 1791 with a byte-identical first row. The count varies by symbol (MSFT 967,
        // BRK-B 93), so it is the whole set each time and not a cap. A signature offering either parameter
        // would let a caller believe they had asked for less.
        var parameters = typeof(AnalystEndpoints)
            .GetMethod(nameof(AnalystEndpoints.GetGradesAsync))!
            .GetParameters();

        Assert.Equal(new[] { "symbol", "ct" }, parameters.Select(p => p.Name!));
    }

    [Fact]
    public async Task The_grades_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetGradesAsync("AAPL");

        Assert.Equal("stable/grades", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    // ---- grades-consensus ---------------------------------------------------------------------------------

    [Fact]
    public async Task The_consensus_unwraps_the_single_element_array_FMP_sends()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-consensus.AAPL.json"));

        var consensus = await endpoints.GetGradeConsensusAsync("AAPL");

        Assert.NotNull(consensus);
        Assert.Equal("AAPL", consensus.Symbol);
        Assert.Equal(1, consensus.StrongBuy);
        Assert.Equal(70, consensus.Buy);
        Assert.Equal(32, consensus.Hold);
        Assert.Equal(9, consensus.Sell);
        Assert.Equal(0, consensus.StrongSell);
        Assert.Equal("Buy", consensus.Consensus);
        // StrongSell is a real zero, not an absent field -- so it shows as unbound and must not be read as
        // "FMP does not know".
        Assert.Equal(["StrongSell"], Binding.Unbound(consensus));
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_rather_than_throwing()
    {
        // Every path in this slice answers an unknown-but-well-formed symbol with [] and HTTP 200, not a 404,
        // so "no coverage", "not found" and "misspelled class-share ticker" are one shape here.
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetGradeConsensusAsync("NOSUCHTICKER"));
    }

    [Fact]
    public async Task The_consensus_carries_no_date_at_all()
    {
        // Seven fields, and none of them temporal. There is no way to tell how old a consensus row is, which is
        // half the reason it cannot be treated as the head of the historical series.
        var (endpoints, _) = Build(Binding.Fixture("grades-consensus.AAPL.json"));

        await endpoints.GetGradeConsensusAsync("AAPL");

        Assert.DoesNotContain(
            typeof(GradeConsensus).GetProperties(),
            p => p.PropertyType == typeof(LocalDate?) || p.PropertyType == typeof(LocalDate));
    }

    // ---- grades-historical --------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_history_row_binds_all_seven_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var rows = await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(Day(2026, 8, 1), rows[0].Date);
        Assert.Equal(6, rows[0].AnalystRatingsStrongBuy);
        Assert.Equal(22, rows[0].AnalystRatingsBuy);
        Assert.Equal(14, rows[0].AnalystRatingsHold);
        Assert.Equal(3, rows[0].AnalystRatingsSell);
        Assert.Equal(2, rows[0].AnalystRatingsStrongSell);
    }

    [Fact]
    public async Task The_history_rows_are_monthly_and_newest_first()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var rows = await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal(
            [Day(2026, 8, 1), Day(2026, 7, 1), Day(2026, 6, 1), Day(2026, 5, 1), Day(2026, 4, 1)],
            rows.Select(r => r.Date));
    }

    [Fact]
    public async Task The_consensus_is_not_the_newest_history_row_and_the_two_fixtures_prove_it()
    {
        // The trap, asserted from two captures taken minutes apart in one pass. 47 analysts against 112: not a
        // stale copy, a different population. Merging them, or treating either as a refresh of the other, is
        // the mistake these two records exist as separate types to prevent.
        var (consensusEndpoints, _) = Build(Binding.Fixture("grades-consensus.AAPL.json"));
        var (historyEndpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var consensus = await consensusEndpoints.GetGradeConsensusAsync("AAPL");
        var history = await historyEndpoints.GetGradeHistoryAsync("AAPL");

        var consensusTotal = consensus!.StrongBuy + consensus.Buy + consensus.Hold
                             + consensus.Sell + consensus.StrongSell;
        var newest = history[0];
        var historyTotal = newest.AnalystRatingsStrongBuy + newest.AnalystRatingsBuy + newest.AnalystRatingsHold
                           + newest.AnalystRatingsSell + newest.AnalystRatingsStrongSell;

        Assert.Equal(112, consensusTotal);
        Assert.Equal(47, historyTotal);
        // And the shape differs, not just the scale: the consensus is Buy-heavy at 70 of 112, the history row
        // is spread across StrongBuy and Buy at 6 and 22 of 47.
        Assert.NotEqual(consensus.Buy, newest.AnalystRatingsBuy);
    }

    [Fact]
    public async Task The_history_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        // Absent limit returns the whole series: 92 rows for AAPL, unchanged by limit=10000. See the ruling in
        // the plan -- a default of 100 belongs on ratings-historical, which answers ONE row without it, and
        // nowhere else in this slice.
        var (endpoints, handler) = Build();

        await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal("stable/grades-historical", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_history_path_sends_a_limit_when_one_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetGradeHistoryAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void No_grade_method_offers_a_date_range_because_all_three_ignore_one()
    {
        // Measured 2026-08-28: grades answers 1791 rows and grades-historical 92, with and without
        // from=2024-01-01&to=2024-12-31 in each case.
        var methods = new[]
        {
            nameof(AnalystEndpoints.GetGradesAsync),
            nameof(AnalystEndpoints.GetGradeConsensusAsync),
            nameof(AnalystEndpoints.GetGradeHistoryAsync),
        };

        foreach (var name in methods)
            Assert.DoesNotContain(
                typeof(AnalystEndpoints).GetMethod(name)!.GetParameters(),
                p => p.Name is "from" or "to");
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Every_grade_method_refuses_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (grades, h1) = Build();
        var (consensus, h2) = Build();
        var (history, h3) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => grades.GetGradesAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => consensus.GetGradeConsensusAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => history.GetGradeHistoryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
        Assert.Empty(h3.Requests);
    }

    [Fact]
    public async Task Every_grade_method_refuses_a_null_symbol_before_spending_a_request()
    {
        var (grades, _) = Build();
        var (consensus, _) = Build();
        var (history, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => grades.GetGradesAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => consensus.GetGradeConsensusAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => history.GetGradeHistoryAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_history_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetGradeHistoryAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockGradeTests"`
Expected: the build fails — `CS0246` for the three records, `CS1061` for the three methods.

- [ ] **Step 4: Write the three records**

`src/FmpDotNet/Models/StockGrade.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One analyst rating action on one symbol, from <c>stable/grades</c>.
///
/// <para><b>An action is not necessarily a change.</b> <see cref="Action"/> was <c>maintain</c>,
/// <c>upgrade</c> or <c>downgrade</c> across 1,791 rows measured 2026-08-28, and on a <c>maintain</c> the
/// previous and new grades are identical — two of five rows in the captured page. A caller looking for rating
/// changes filters on <see cref="Action"/>, not by comparing the two grade fields.</para>
///
/// <para><b>The vocabulary is not one scale.</b> <see cref="NewGrade"/> took <b>20 distinct values</b> across
/// those 1,791 rows — <c>Buy</c>, <c>Outperform</c>, <c>Overweight</c>, <c>Neutral</c>, <c>Hold</c>,
/// <c>Market Perform</c>, <c>Equal Weight</c>, <c>Underweight</c> and more — because each house uses its own
/// words. Mapping them onto a common ladder is a judgement the SDK does not make for you.</para></summary>
public sealed record StockGrade
{
    /// <summary>The symbol the action was taken on.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>When the action was published. Rows arrive newest first.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The house that published it — <c>"Rothschild &amp; Co"</c>, <c>"Jefferies"</c>,
    /// <c>"Goldman Sachs"</c>. Free text, and not a stable identifier.</summary>
    [JsonPropertyName("gradingCompany")] public string? GradingCompany { get; init; }

    /// <summary>The grade before this action, in that house's own vocabulary. Equal to
    /// <see cref="NewGrade"/> whenever <see cref="Action"/> is <c>maintain</c>.</summary>
    [JsonPropertyName("previousGrade")] public string? PreviousGrade { get; init; }

    /// <summary>The grade after this action. See the type's remarks: 20 distinct values across one symbol's
    /// history, drawn from each house's own scale, which is why this is a string and not an enum.</summary>
    [JsonPropertyName("newGrade")] public string? NewGrade { get; init; }

    /// <summary>What the house did: <c>maintain</c>, <c>downgrade</c> or <c>upgrade</c> across 1,791 rows
    /// measured 2026-08-28.
    ///
    /// <para><b>Lower case, while the grades beside it are title case.</b> The token is kept exactly as sent —
    /// a caller matching on it should fold case itself, since the SDK does not normalise what it was
    /// given.</para></summary>
    [JsonPropertyName("action")] public string? Action { get; init; }
}

/// <summary>The current spread of analyst opinion on one symbol, from <c>stable/grades-consensus</c>.
///
/// <para><b>This is not the newest row of <see cref="GradeHistory"/>, although it looks like it could be.</b>
/// Both carry five analyst counts, and a caller could reasonably read one as a live view of the other. Measured
/// for AAPL the same minute on 2026-08-28:</para>
///
/// <code>
/// grades-historical row 0  2026-08-01  strongBuy 6  buy 22  hold 14  sell 3  strongSell 2   total  47
/// grades-consensus         (no date)   strongBuy 1  buy 70  hold 32  sell 9  strongSell 0   total 112
/// </code>
///
/// <para>The totals differ by more than a factor of two and the distributions are differently shaped, so these
/// are different populations rather than one being stale. They stay separate records for that reason, and
/// nothing in this SDK merges or reconciles them.</para>
///
/// <para><b>There is no date on this record</b>, because the endpoint sends none — so there is no way to tell
/// how current a consensus is.</para></summary>
public sealed record GradeConsensus
{
    /// <summary>The symbol the consensus is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Analysts at the strongest buy rating.</summary>
    [JsonPropertyName("strongBuy")] public int? StrongBuy { get; init; }

    /// <summary>Analysts at a buy rating. The largest bucket for AAPL at 70 of 112.</summary>
    [JsonPropertyName("buy")] public int? Buy { get; init; }

    /// <summary>Analysts at a hold rating.</summary>
    [JsonPropertyName("hold")] public int? Hold { get; init; }

    /// <summary>Analysts at a sell rating.</summary>
    [JsonPropertyName("sell")] public int? Sell { get; init; }

    /// <summary>Analysts at the strongest sell rating. <b>Zero is a measured value here, not an absence</b> —
    /// AAPL answered 0 on 2026-08-28.</summary>
    [JsonPropertyName("strongSell")] public int? StrongSell { get; init; }

    /// <summary>FMP's own one-word summary of the five counts — <c>"Buy"</c> for AAPL. A string, because the
    /// observed set is one value from one symbol and an enum built on it would be a guess.</summary>
    [JsonPropertyName("consensus")] public string? Consensus { get; init; }
}

/// <summary>One month's snapshot of how analysts were rating a symbol, from <c>stable/grades-historical</c>.
///
/// <para>Rows are monthly and newest first, dated the first of the month — 92 of them for AAPL measured
/// 2026-08-28, back to 2018. The five counts are named as FMP names them, <c>analystRatings*</c>, and they are
/// <b>not</b> the same five as <see cref="GradeConsensus"/>: read the remarks on that type before treating
/// either as a view of the other.</para></summary>
public sealed record GradeHistory
{
    /// <summary>The symbol the snapshot is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The month the snapshot covers, dated its first day.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Analysts at the strongest buy rating that month.</summary>
    [JsonPropertyName("analystRatingsStrongBuy")] public int? AnalystRatingsStrongBuy { get; init; }

    /// <summary>Analysts at a buy rating that month.</summary>
    [JsonPropertyName("analystRatingsBuy")] public int? AnalystRatingsBuy { get; init; }

    /// <summary>Analysts at a hold rating that month.</summary>
    [JsonPropertyName("analystRatingsHold")] public int? AnalystRatingsHold { get; init; }

    /// <summary>Analysts at a sell rating that month.</summary>
    [JsonPropertyName("analystRatingsSell")] public int? AnalystRatingsSell { get; init; }

    /// <summary>Analysts at the strongest sell rating that month.</summary>
    [JsonPropertyName("analystRatingsStrongSell")] public int? AnalystRatingsStrongSell { get; init; }
}
```

- [ ] **Step 5: Register the three records**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<StockGrade>))]`, `[JsonSerializable(typeof(List<GradeConsensus>))]`, `[JsonSerializable(typeof(List<GradeHistory>))]`

- [ ] **Step 6: Write the three methods**

Append to `src/FmpDotNet/Endpoints/AnalystEndpoints.cs`, inside the class and **after** the existing `Estimates` private helper:

```csharp
    /// <summary>Every analyst rating action on one symbol, newest first, from <c>stable/grades</c>.
    ///
    /// <para><b>This returns the whole series and there is no way to ask for less.</b> Measured 2026-08-28,
    /// <c>symbol=AAPL</c> answered <b>1,791 rows</b>; so did <c>limit=5</c>, <c>limit=10000</c> and
    /// <c>page=1</c> — the last with a byte-identical first row. The count varies by symbol (MSFT 967, BRK-B
    /// 93), so it is the whole set each time rather than a cap. Neither <c>limit</c> nor <c>page</c> is offered
    /// here, because offering a parameter FMP discards would let a caller believe they had narrowed something.
    /// Take from the head of the returned list instead.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored too</b>, measured the same day: 1,791 rows with and
    /// without <c>from=2024-01-01&amp;to=2024-12-31</c>. Filter on <see cref="StockGrade.Date"/> at the call
    /// site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<StockGrade>> GetGradesAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await transport.GetListAsync(
            new FmpRequest("stable/grades").With("symbol", symbol),
            FmpJsonContext.Default.ListStockGrade, ct).ConfigureAwait(false);
    }

    /// <summary>The current spread of analyst opinion on one symbol, from <c>stable/grades-consensus</c>.
    /// Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para><b>This is not the newest row of <see cref="GetGradeHistoryAsync"/>.</b> Measured for AAPL the same
    /// minute on 2026-08-28, this endpoint's counts total 112 analysts and the newest historical row totals 47,
    /// with differently shaped distributions. See <see cref="GradeConsensus"/> for the numbers. They are
    /// different populations, and joining or reconciling them is not something this SDK does for you.</para>
    ///
    /// <para>FMP sends one row in an array; this unwraps it, as
    /// <see cref="CompanyEndpoints.GetProfileAsync"/> does. An unknown-but-well-formed symbol answers an empty
    /// array with HTTP 200 rather than a 404, which surfaces here as <see langword="null"/>.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<GradeConsensus?> GetGradeConsensusAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/grades-consensus").With("symbol", symbol),
            FmpJsonContext.Default.ListGradeConsensus, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Monthly snapshots of analyst ratings for one symbol, newest first, from
    /// <c>stable/grades-historical</c>.
    ///
    /// <para><b><paramref name="limit"/> is omitted by default, and without it you get everything</b> — 92 rows
    /// for AAPL measured 2026-08-28, unchanged by <c>limit=10000</c>, back to 2018. Rows are dated the first of
    /// each month.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored</b>, measured the same day: 92 rows with and without a
    /// 2024 range. Filter on <see cref="GradeHistory.Date"/> at the call site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N months, or null for the whole history. Must be positive when
    /// given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<GradeHistory>> GetGradeHistoryAsync(
        string symbol, int? limit = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit, when given, must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/grades-historical").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListGradeHistory, ct).ConfigureAwait(false);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockGradeTests"`
Expected: PASS — 16 test methods, **18 test cases** (14 `[Fact]`, plus 2 and 2 `[InlineData]` rows on the two `[Theory]` methods). Zero warnings.

- [ ] **Step 8: Mutation-check the two records staying separate**

Point `GetGradeConsensusAsync` at `FmpJsonContext.Default.ListGradeHistory` and change its return type to `GradeHistory?` — the shape of the "these are the same data" mistake.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~StockGradeTests"`
Expected: a **compile failure** across `The_consensus_unwraps_the_single_element_array_FMP_sends`, `The_consensus_carries_no_date_at_all` and `The_consensus_is_not_the_newest_history_row_and_the_two_fixtures_prove_it` — `CS1061`, since `GradeHistory` has no `StrongBuy`, `Buy`, `Hold`, `Sell`, `StrongSell` or `Consensus`. Report it as a compile failure. **The finding worth writing down is what the type system does not catch**: had the two records shared field names, this mutation would have compiled and bound the consensus row to zeros across the board, because `strongBuy` does not match `analystRatingsStrongBuy` under any casing rule. The differing wire names are what make the mistake loud. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/StockGrade.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/AnalystEndpoints.cs tests/FmpDotNet.Tests/StockGradeTests.cs \
        tests/FmpDotNet.Tests/Fixtures/grades.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/grades-consensus.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/grades-historical.AAPL.json
git commit -m "feat: the three grade paths, two of which are not the same data (#37)"
```

---

### Task 7: `PublisherListJsonConverter` and the two price-target paths

The only new converter in this slice. `price-target-summary` sends `publishers` as a **string whose content is a JSON array**, and the already-shipped `BulkPriceTargetSummary.Publishers` is `IReadOnlyList<string>` — so today the bulk path and the ordinary path disagree about the type of one field, and this task ends that.

**Files:**
- Create: `src/FmpDotNet/Models/PriceTarget.cs` (two records), `tests/FmpDotNet.Tests/PriceTargetTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/price-target-consensus.AAPL.json`, `price-target-summary.AAPL.json`
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (+1 converter), `src/FmpDotNet/Endpoints/AnalystEndpoints.cs` (+2 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+2 entries)

**Interfaces:**
- Consumes: `FmpJsonContext.Default.ListString`, which is **already registered** — it was added for `BulkPriceTargetSummary.ParsePublishers` and must not be added a second time.
- Produces: `public sealed class PublisherListJsonConverter : JsonConverter<IReadOnlyList<string>>`. `public sealed record PriceTargetConsensus` (`Symbol` as `string?`; `TargetHigh`, `TargetLow`, `TargetConsensus`, `TargetMedian` as `decimal?`). `public sealed record PriceTargetSummary` (`Symbol` as `string?`; `LastMonthCount`, `LastQuarterCount`, `LastYearCount`, `AllTimeCount` as `int?`; `LastMonthAvgPriceTarget`, `LastQuarterAvgPriceTarget`, `LastYearAvgPriceTarget`, `AllTimeAvgPriceTarget` as `decimal?`; `Publishers` as `IReadOnlyList<string>?`). Methods: `AnalystEndpoints.GetPriceTargetConsensusAsync(string symbol, CancellationToken ct = default)` → `Task<PriceTargetConsensus?>` and `GetPriceTargetSummaryAsync(string symbol, CancellationToken ct = default)` → `Task<PriceTargetSummary?>`.

**The converter must guard `reader.TokenType` before calling `GetString()`, and it must call `reader.Skip()` in that guard.** This is the exact defect the previous slice found and fixed on `BusinessAddressJsonConverter`, and it is subtle: an early `return null` alone is not enough. For `StartArray` and `StartObject` the reader is positioned only at the *opening* token, and `System.Text.Json`'s `VerifyRead` demands the converter leave it past the matching close — otherwise it throws its own `JsonException` ("read too much or not enough") in place of the one the guard exists to avoid. `Skip()` is a correct no-op on the scalar tokens too. Copy the shape from `BusinessAddressJsonConverter.Read`, comment and all.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/price-target-consensus.AAPL.json` — `stable/price-target-consensus?symbol=AAPL`, captured 2026-08-28, verbatim. The complete response, one row. Note that three of the four numbers arrive as JSON integers and one as a float — all four are `decimal?`:

```json
[
 {"symbol": "AAPL", "targetHigh": 400, "targetLow": 245, "targetConsensus": 340.72, "targetMedian": 360}
]
```

`tests/FmpDotNet.Tests/Fixtures/price-target-summary.AAPL.json` — `stable/price-target-summary?symbol=AAPL`, captured 2026-08-28, verbatim. **The `publishers` value is a string, and its escaping is exact** — copy it character for character, including `Investor's Business Daily`, whose apostrophe sits inside a double-quoted JSON string and is therefore correctly escaped. That is the difference from the `businessAddress` field of the previous slice, where a stringified Python list broke on the same character:

```json
[
 {
  "symbol": "AAPL",
  "lastMonthCount": 5,
  "lastMonthAvgPriceTarget": 323.73,
  "lastQuarterCount": 17,
  "lastQuarterAvgPriceTarget": 331.69,
  "lastYearCount": 71,
  "lastYearAvgPriceTarget": 307.39,
  "allTimeCount": 259,
  "allTimeAvgPriceTarget": 232.31,
  "publishers": "[\"StreetInsider\",\"Benzinga\",\"Pulse 2.0\",\"MarketWatch\",\"Investing\",\"Barrons\",\"Investor's Business Daily\"]"
 }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/PriceTargetTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The two <c>stable/price-target-*</c> paths and the converter one of them needs, checked against
/// captures taken live 2026-08-28.
///
/// <para><b><c>publishers</c> arrives as a string whose content is a JSON array</b> — the only nested-format
/// field in this slice. Unlike the <c>businessAddress</c> field of the previous slice, which was a stringified
/// Python list that broke on an apostrophe, this one is real JSON and survives a real parse:
/// <c>Investor's Business Daily</c> comes back intact.</para>
///
/// <para>The shipped <see cref="BulkPriceTargetSummary.Publishers"/> is already
/// <see cref="IReadOnlyList{T}"/> of <see cref="string"/>, so before this slice the bulk path and the ordinary
/// path disagreed about the type of one field. They no longer do.</para></summary>
public class PriceTargetTests
{
    private static (AnalystEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new AnalystEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    // ---- the converter, exercised directly ----------------------------------------------------------------

    [Fact]
    public void A_json_array_inside_a_string_is_parsed_into_a_list()
    {
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[\"StreetInsider\",\"Benzinga\"]"}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Equal(["StreetInsider", "Benzinga"], row.Publishers);
    }

    [Fact]
    public void An_apostrophe_inside_a_publisher_name_survives_the_parse()
    {
        // The measured value, and the reason a real parse is safe here where it was not on businessAddress:
        // the apostrophe sits inside a double-quoted JSON string and is correctly escaped, so nothing has to
        // guess where the element boundaries are.
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[\"Investor's Business Daily\",\"Barrons\"]"}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Equal(["Investor's Business Daily", "Barrons"], row.Publishers);
    }

    [Fact]
    public void An_empty_json_array_reads_as_an_empty_list_and_not_as_null()
    {
        // Empty and null mean different things, deliberately: an empty list is FMP saying there are no
        // publishers, null is this SDK saying the field could not be read. The shipped
        // BulkPriceTargetSummary.Publishers already draws that distinction and measured 874 empty arrays across
        // 5,277 bulk rows, so the empty case is common rather than theoretical.
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[]"}]""", FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.NotNull(row.Publishers);
        Assert.Empty(row.Publishers);
    }

    [Fact]
    public void A_string_that_is_not_json_costs_that_field_and_nothing_else()
    {
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","publishers":"not json at all","allTimeCount":259}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(259, row.AllTimeCount);
    }

    [Fact]
    public void A_json_null_reads_as_null()
    {
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","publishers":null}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
    }

    [Theory]
    [InlineData("""["StreetInsider","Benzinga"]""")]      // a real array, not a string containing one
    [InlineData("""{"a":1}""")]                            // an object
    [InlineData("""42""")]                                 // a number
    [InlineData("""true""")]                               // a boolean
    public void A_token_that_is_not_a_string_costs_that_field_and_never_the_response(string publishers)
    {
        // The defect the previous slice found on BusinessAddressJsonConverter, guarded against from the start
        // here. The realistic trigger is FMP fixing the double-encoding: if `publishers` ever arrives as a real
        // JSON array, an unguarded GetString() throws -- and because FmpTransport does not wrap
        // DeserializeAsync, that costs the WHOLE response rather than the one field.
        //
        // The array and object rows are the ones that matter most: for those the reader sits on the OPENING
        // token only, and returning null without calling reader.Skip() makes System.Text.Json's VerifyRead
        // throw its own JsonException ("read too much or not enough") in place of the one the guard exists to
        // avoid. A guard without Skip() passes the scalar rows here and fails these two.
        var row = JsonSerializer.Deserialize(
            $$"""[{"symbol":"AAPL","allTimeCount":259,"publishers":{{publishers}}}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(259, row.AllTimeCount);
    }

    // ---- price-target-consensus ---------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_consensus_row_binds_all_five_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-consensus.AAPL.json"));

        var consensus = await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.NotNull(consensus);
        Assert.Empty(Binding.Unbound(consensus));
        Assert.Equal("AAPL", consensus.Symbol);
        Assert.Equal(400m, consensus.TargetHigh);
        Assert.Equal(245m, consensus.TargetLow);
        Assert.Equal(340.72m, consensus.TargetConsensus);
        Assert.Equal(360m, consensus.TargetMedian);
    }

    [Fact]
    public async Task The_consensus_can_sit_outside_the_median_and_the_sdk_does_not_reconcile_them()
    {
        // Measured: consensus 340.72, median 360 -- the mean below the median, which is what a left-skewed
        // distribution of targets looks like and is not a fault. Nothing here recomputes or cross-checks.
        var (endpoints, _) = Build(Binding.Fixture("price-target-consensus.AAPL.json"));

        var consensus = await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.True(consensus!.TargetConsensus < consensus.TargetMedian);
    }

    [Fact]
    public async Task The_consensus_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.Equal("stable/price-target-consensus", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    // ---- price-target-summary -----------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_summary_row_binds_all_ten_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-summary.AAPL.json"));

        var summary = await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.NotNull(summary);
        Assert.Empty(Binding.Unbound(summary));
        Assert.Equal(5, summary.LastMonthCount);
        Assert.Equal(323.73m, summary.LastMonthAvgPriceTarget);
        Assert.Equal(17, summary.LastQuarterCount);
        Assert.Equal(331.69m, summary.LastQuarterAvgPriceTarget);
        Assert.Equal(71, summary.LastYearCount);
        Assert.Equal(307.39m, summary.LastYearAvgPriceTarget);
        Assert.Equal(259, summary.AllTimeCount);
        Assert.Equal(232.31m, summary.AllTimeAvgPriceTarget);
    }

    [Fact]
    public async Task The_captured_publishers_string_parses_into_its_seven_names()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-summary.AAPL.json"));

        var summary = await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.Equal(
            ["StreetInsider", "Benzinga", "Pulse 2.0", "MarketWatch", "Investing", "Barrons",
             "Investor's Business Daily"],
            summary!.Publishers);
    }

    [Fact]
    public void The_ordinary_and_bulk_summaries_now_agree_on_the_type_of_publishers()
    {
        // The whole point of the converter. Before this slice the bulk path parsed the nested array and the
        // ordinary path did not exist; shipping the ordinary one as a raw string would have left two types for
        // one field, and a caller moving between them would have had to know which.
        Assert.Equal(
            typeof(BulkPriceTargetSummary).GetProperty(nameof(BulkPriceTargetSummary.Publishers))!.PropertyType,
            typeof(PriceTargetSummary).GetProperty(nameof(PriceTargetSummary.Publishers))!.PropertyType);
    }

    [Fact]
    public async Task The_summary_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.Equal("stable/price-target-summary", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Both_methods_answer_null_for_an_unknown_symbol()
    {
        var (consensus, _) = Build("[]");
        var (summary, _) = Build("[]");

        Assert.Null(await consensus.GetPriceTargetConsensusAsync("NOSUCHTICKER"));
        Assert.Null(await summary.GetPriceTargetSummaryAsync("NOSUCHTICKER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Both_methods_refuse_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (consensus, h1) = Build();
        var (summary, h2) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => consensus.GetPriceTargetConsensusAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => summary.GetPriceTargetSummaryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
    }

    [Fact]
    public async Task Both_methods_refuse_a_null_symbol_before_spending_a_request()
    {
        var (consensus, _) = Build();
        var (summary, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => consensus.GetPriceTargetConsensusAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => summary.GetPriceTargetSummaryAsync(null!));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~PriceTargetTests"`
Expected: the build fails — `CS0246` for both records, `CS1061` for both methods.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`:

```csharp
/// <summary>Reads <c>price-target-summary</c>'s <c>publishers</c> field, which is a <b>string containing a JSON
/// array</b>, into the list it describes.
///
/// <para>Measured 2026-08-28, AAPL answered:</para>
///
/// <code>
/// "publishers": "[\"StreetInsider\",\"Benzinga\",\"Pulse 2.0\",\"MarketWatch\",\"Investing\",\"Barrons\",\"Investor's Business Daily\"]"
/// </code>
///
/// <para><b>A real parse is safe here, and that is not true of every double-encoded field in this SDK.</b>
/// <see cref="BusinessAddressJsonConverter"/> deals with a stringified <i>Python</i> list built by naive
/// formatting, where an apostrophe inside an element breaks the encoding and a parse fails on it. This one is
/// genuine JSON: the apostrophe in <c>Investor's Business Daily</c> sits inside a double-quoted JSON string and
/// is correctly escaped, so <c>JsonSerializer</c> reads it back exactly.</para>
///
/// <para>It binds to <see cref="IReadOnlyList{T}"/> of <see cref="string"/> so that the ordinary path and
/// <see cref="Models.BulkPriceTargetSummary.Publishers"/> agree about the type of this field; before this
/// converter they would not have.</para>
///
/// <para><b>Empty and null mean different things, deliberately.</b> An empty list is FMP saying there are no
/// publishers — 874 of 5,277 rows on the bulk path measured 2026-08-26 — and <see langword="null"/> is this SDK
/// saying the field could not be read. Collapsing the two would turn a format change upstream into a silent,
/// universal "no publishers".</para>
///
/// <para>Deserialisation goes through <see cref="FmpJsonContext"/> rather than a reflection-based overload,
/// because this assembly declares <c>IsAotCompatible</c> and a reflecting <c>Deserialize</c> would fail the
/// build on IL2026/IL3050.</para></summary>
public sealed class PublisherListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<string>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Guarded from the start, unlike BusinessAddressJsonConverter, which shipped without this and had it
        // added by review. Utf8JsonReader.GetString() throws on anything but String, PropertyName or Null, and
        // the realistic trigger is FMP fixing the double-encoding: if `publishers` ever arrives as a real JSON
        // array, an unguarded read costs the WHOLE response, since FmpTransport does not wrap DeserializeAsync.
        if (reader.TokenType != JsonTokenType.String)
        {
            // Skip() rather than an early return alone: for StartArray/StartObject the reader is positioned at
            // the OPENING token only, and System.Text.Json's VerifyRead demands the converter leave it past the
            // matching close token -- otherwise it throws its own JsonException ("read too much or not enough")
            // in place of the one this guard exists to avoid. Skip() is a correct no-op on the scalar tokens
            // (Number, True, False, Null) that also reach this branch.
            reader.Skip();
            return null;
        }

        var raw = reader.GetString();
        if (raw is null) return null;
        if (raw.Length == 0) return [];

        try
        {
            return JsonSerializer.Deserialize(raw, FmpJsonContext.Default.ListString);
        }
        catch (JsonException)
        {
            return null;   // unreadable, which is not the same as empty
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
        => writer.WriteStringValue(
            JsonSerializer.Serialize(new List<string>(value), FmpJsonContext.Default.ListString));
}
```

- [ ] **Step 5: Write the two records**

`src/FmpDotNet/Models/PriceTarget.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Where the analyst price targets on one symbol sit, from <c>stable/price-target-consensus</c>.
///
/// <para>One row, five fields, all populated on the symbol measured 2026-08-28. <b>The mean can fall below the
/// median</b> — AAPL answered a consensus of 340.72 against a median of 360 — which is an ordinary left-skewed
/// distribution and not a fault. Nothing here recomputes or cross-checks the four numbers.</para>
///
/// <para>The values arrive as a mix of JSON integers and floats in the same response, so all four are
/// <see langword="decimal"/>.</para></summary>
public sealed record PriceTargetConsensus
{
    /// <summary>The symbol the targets are for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The highest published target.</summary>
    [JsonPropertyName("targetHigh")] public decimal? TargetHigh { get; init; }

    /// <summary>The lowest published target.</summary>
    [JsonPropertyName("targetLow")] public decimal? TargetLow { get; init; }

    /// <summary>The mean of the published targets. Can sit below <see cref="TargetMedian"/>.</summary>
    [JsonPropertyName("targetConsensus")] public decimal? TargetConsensus { get; init; }

    /// <summary>The median of the published targets.</summary>
    [JsonPropertyName("targetMedian")] public decimal? TargetMedian { get; init; }
}

/// <summary>Analyst price-target activity on one symbol, summarised over four windows, from
/// <c>stable/price-target-summary</c>.
///
/// <para>The same ten fields as the whole-universe <see cref="BulkPriceTargetSummary"/>, and since this slice
/// the same <i>types</i> too — read that type's remarks on why a zero count and a zero average are
/// indistinguishable in the payload, and why the average is only meaningful where the matching count is above
/// zero.</para>
///
/// <para><b><see cref="Publishers"/> arrives as a string containing a JSON array</b> and is parsed by
/// <see cref="PublisherListJsonConverter"/>. It is the only nested-format field in this endpoint
/// group.</para></summary>
public sealed record PriceTargetSummary
{
    /// <summary>The symbol the summary is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Price targets published in the last month.</summary>
    [JsonPropertyName("lastMonthCount")] public int? LastMonthCount { get; init; }

    /// <summary>Average target across the last month. Meaningless unless <see cref="LastMonthCount"/> is above
    /// zero — gate on the count, never on the average.</summary>
    [JsonPropertyName("lastMonthAvgPriceTarget")] public decimal? LastMonthAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last quarter.</summary>
    [JsonPropertyName("lastQuarterCount")] public int? LastQuarterCount { get; init; }

    /// <summary>Average target across the last quarter. Gate on <see cref="LastQuarterCount"/>.</summary>
    [JsonPropertyName("lastQuarterAvgPriceTarget")] public decimal? LastQuarterAvgPriceTarget { get; init; }

    /// <summary>Price targets published in the last year.</summary>
    [JsonPropertyName("lastYearCount")] public int? LastYearCount { get; init; }

    /// <summary>Average target across the last year. Gate on <see cref="LastYearCount"/>.</summary>
    [JsonPropertyName("lastYearAvgPriceTarget")] public decimal? LastYearAvgPriceTarget { get; init; }

    /// <summary>Price targets published over the whole history FMP holds.</summary>
    [JsonPropertyName("allTimeCount")] public int? AllTimeCount { get; init; }

    /// <summary>Average target across the whole history. Gate on <see cref="AllTimeCount"/>.</summary>
    [JsonPropertyName("allTimeAvgPriceTarget")] public decimal? AllTimeAvgPriceTarget { get; init; }

    /// <summary>The publications the targets came from.
    ///
    /// <para><b>On the wire this is a string containing a JSON array</b>, not an array — measured 2026-08-28,
    /// AAPL sent seven names and MSFT six, both in that form. <see cref="PublisherListJsonConverter"/> reads it,
    /// so this property is the list, matching <see cref="BulkPriceTargetSummary.Publishers"/>.</para>
    ///
    /// <para>An empty list means FMP reported no publishers; <see langword="null"/> means the field could not be
    /// read. Those are different states and are kept apart.</para></summary>
    [JsonPropertyName("publishers")]
    [JsonConverter(typeof(PublisherListJsonConverter))]
    public IReadOnlyList<string>? Publishers { get; init; }
}
```

- [ ] **Step 6: Register both records**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<PriceTargetConsensus>))]` and `[JsonSerializable(typeof(List<PriceTargetSummary>))]`. **Do not add `List<string>` — it is already there**, registered for `BulkPriceTargetSummary`, and a duplicate `[JsonSerializable]` for the same type is a source-generator error.

- [ ] **Step 7: Write the two methods**

Append to `AnalystEndpoints.cs`:

```csharp
    /// <summary>Where analyst price targets on one symbol sit, from <c>stable/price-target-consensus</c>.
    /// Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para>One row, unwrapped as <see cref="CompanyEndpoints.GetProfileAsync"/> does. An
    /// unknown-but-well-formed symbol answers an empty array with HTTP 200, not a 404.</para>
    ///
    /// <para><c>from</c>, <c>to</c> and <c>limit</c> are not offered: this endpoint answers a single current
    /// summary and has nothing to page or filter.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<PriceTargetConsensus?> GetPriceTargetConsensusAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/price-target-consensus").With("symbol", symbol),
            FmpJsonContext.Default.ListPriceTargetConsensus, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Analyst price-target activity on one symbol over four windows, from
    /// <c>stable/price-target-summary</c>. Returns <see langword="null"/> when FMP has no coverage.
    ///
    /// <para><b>A zero count and a zero average are indistinguishable from "unknown" in this payload</b> — read
    /// the remarks on <see cref="PriceTargetSummary"/> and gate every average on its matching count.</para>
    ///
    /// <para><see cref="PriceTargetSummary.Publishers"/> arrives as a string containing a JSON array and is
    /// parsed into a list; this is the same shape and now the same type as the whole-universe
    /// <see cref="BulkEndpoints.StreamPriceTargetSummariesAsync"/> returns.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<PriceTargetSummary?> GetPriceTargetSummaryAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/price-target-summary").With("symbol", symbol),
            FmpJsonContext.Default.ListPriceTargetSummary, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
```

**The `cref` to `BulkEndpoints.StreamPriceTargetSummariesAsync` was verified while planning** — `src/FmpDotNet/Endpoints/BulkEndpoints.cs:209`, public, that exact spelling. Every other `cref` in this task points at a type this task or an earlier one creates, or at a shipped member. Build before committing anyway: an unresolved `<see cref>` is `CS1574`, a build error under `TreatWarningsAsErrors`, and the previous slice was bitten twice by exactly this.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~PriceTargetTests"`
Expected: PASS — 16 test methods, **20 test cases** (14 `[Fact]`, plus 4 and 2 `[InlineData]` rows on the two `[Theory]` methods). Zero warnings.

- [ ] **Step 9: Mutation-check the converter guard, which is the subtle one**

Replace `reader.Skip(); return null;` with a bare `return null;` — the exact form that shipped on `BusinessAddressJsonConverter` and had to be fixed.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~PriceTargetTests"`
Expected: exactly **2** failures, both rows of `A_token_that_is_not_a_string_costs_that_field_and_never_the_response` — the array case and the object case — each throwing `System.Text.Json.JsonException` with a message about reading too much or not enough. **The number and 42/true rows still pass**, because on a scalar token the reader is already positioned correctly and `Skip()` is a no-op there. That asymmetry is the whole reason the theory carries four rows rather than one: a guard tested only against scalars looks correct and is not. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Models/PriceTarget.cs src/FmpDotNet/Serialization/NodaConverters.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Endpoints/AnalystEndpoints.cs \
        tests/FmpDotNet.Tests/PriceTargetTests.cs \
        tests/FmpDotNet.Tests/Fixtures/price-target-consensus.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/price-target-summary.AAPL.json
git commit -m "feat: price targets, and the converter that ends one field having two types (#37)"
```

---

### Task 8: `CompanyRating`, and the endpoint whose default is one row

The last of the eleven records, and the one method in this slice where `limit` defaults to a number. `ratings-historical` answers **one row** when `limit` is absent, from an endpoint whose name promises a series.

**Files:**
- Create: `src/FmpDotNet/Models/CompanyRating.cs`, `tests/FmpDotNet.Tests/CompanyRatingTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/ratings-snapshot.AAPL.json`, `ratings-historical.AAPL.json`
- Modify: `src/FmpDotNet/Endpoints/AnalystEndpoints.cs` (+2 methods), `src/FmpDotNet/Serialization/FmpJsonContext.cs` (+1 entry)

**Interfaces:**
- Consumes: `NullableLocalDateJsonConverter`.
- Produces: `public sealed record CompanyRating` with `Symbol`, `Rating` (`string?`), `Date` (`LocalDate?`), and `OverallScore`, `DiscountedCashFlowScore`, `ReturnOnEquityScore`, `ReturnOnAssetsScore`, `DebtToEquityScore`, `PriceToEarningsScore`, `PriceToBookScore` (`int?`). Methods: `AnalystEndpoints.GetRatingAsync(string symbol, CancellationToken ct = default)` → `Task<CompanyRating?>` and `GetRatingHistoryAsync(string symbol, int limit = 100, CancellationToken ct = default)` → `Task<IReadOnlyList<CompanyRating>>`. Note the **non-nullable `int limit = 100`** here, unlike every other `limit` in this slice.

**One record serves both paths, and `Date` is the discriminator.** Measured 2026-08-28, `ratings-snapshot` sends nine fields and `ratings-historical` sends the same nine plus `date`. That is the `EmployeeCount` pattern: one record, the discriminating field nullable, and `GetRatingAsync` therefore always returns a row whose `Date` is null.

**The shipped `BulkCompanyRating` is not reused, and the reason is one field.** It carries `symbol, date, rating, discountedCashFlowScore, returnOnEquityScore, returnOnAssetsScore, debtToEquityScore, priceToEarningsScore, priceToBookScore` — nine fields with **no `overallScore`**, which both ordinary paths send. Reusing it would drop a measured field; adding `overallScore` to it would put a permanently-null property on the bulk shape. Two records with nine overlapping fields is the honest outcome.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/ratings-snapshot.AAPL.json` — `stable/ratings-snapshot?symbol=AAPL`, captured 2026-08-28, verbatim. The complete response: one row, **nine fields and no `date`**:

```json
[
 {"symbol": "AAPL", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1}
]
```

`tests/FmpDotNet.Tests/Fixtures/ratings-historical.AAPL.json` — the first five rows of `stable/ratings-historical?symbol=AAPL&limit=5`, captured 2026-08-28, verbatim. **Every score is identical across all five rows and only the date moves.** That is what the daily series looks like for a stable large-cap and it is not a truncated or duplicated capture — it is also why the test below asserts on the dates rather than on the scores:

```json
[
 {"symbol": "AAPL", "date": "2026-08-27", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1},
 {"symbol": "AAPL", "date": "2026-08-26", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1},
 {"symbol": "AAPL", "date": "2026-08-25", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1},
 {"symbol": "AAPL", "date": "2026-08-24", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1},
 {"symbol": "AAPL", "date": "2026-08-21", "rating": "B", "overallScore": 3, "discountedCashFlowScore": 3, "returnOnEquityScore": 5, "returnOnAssetsScore": 5, "debtToEquityScore": 1, "priceToEarningsScore": 2, "priceToBookScore": 1}
]
```

Note the gap between 2026-08-21 and 2026-08-24: 22 and 23 August were a weekend. The series is per trading day, not per calendar day.

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/CompanyRatingTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/ratings-snapshot</c> and <c>stable/ratings-historical</c>, checked against captures taken
/// live 2026-08-28.
///
/// <para>One record serves both. Their field sets differ by exactly one member: the snapshot sends nine and the
/// history sends the same nine plus <c>date</c>, so <see cref="CompanyRating.Date"/> is nullable and is null on
/// every row the snapshot returns — the same pattern as <see cref="EmployeeCount"/>.</para>
///
/// <para><b>The trap is the default.</b> <c>ratings-historical</c> with no <c>limit</c> answers <b>one row</b>,
/// from an endpoint whose name promises a series. That is the one place in this slice where a
/// <c>limit</c> is defaulted rather than omitted.</para></summary>
public class CompanyRatingTests
{
    private static (AnalystEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new AnalystEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- one record, two shapes ---------------------------------------------------------------------------

    [Fact]
    public async Task The_snapshot_binds_its_nine_fields_and_leaves_the_date_null()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratings-snapshot.AAPL.json"));

        var rating = await endpoints.GetRatingAsync("AAPL");

        Assert.NotNull(rating);
        // Date is the one member this path never sends, so it is the one member reported unbound.
        Assert.Equal(["Date"], Binding.Unbound(rating));
        Assert.Null(rating.Date);
        Assert.Equal("AAPL", rating.Symbol);
        Assert.Equal("B", rating.Rating);
        Assert.Equal(3, rating.OverallScore);
        Assert.Equal(3, rating.DiscountedCashFlowScore);
        Assert.Equal(5, rating.ReturnOnEquityScore);
        Assert.Equal(5, rating.ReturnOnAssetsScore);
        Assert.Equal(1, rating.DebtToEquityScore);
        Assert.Equal(2, rating.PriceToEarningsScore);
        Assert.Equal(1, rating.PriceToBookScore);
    }

    [Fact]
    public async Task The_history_binds_all_ten_fields_including_the_date()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratings-historical.AAPL.json"));

        var rows = await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(Day(2026, 8, 27), rows[0].Date);
        Assert.Equal("B", rows[0].Rating);
        Assert.Equal(3, rows[0].OverallScore);
    }

    [Fact]
    public async Task The_history_is_per_trading_day_not_per_calendar_day()
    {
        // 2026-08-22 and 08-23 were a weekend and are simply absent. A caller stepping dates rather than
        // reading them will misalign.
        var (endpoints, _) = Build(Binding.Fixture("ratings-historical.AAPL.json"));

        var rows = await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal(
            [Day(2026, 8, 27), Day(2026, 8, 26), Day(2026, 8, 25), Day(2026, 8, 24), Day(2026, 8, 21)],
            rows.Select(r => r.Date));
    }

    [Fact]
    public void The_shipped_bulk_rating_is_not_reused_because_it_has_no_overall_score()
    {
        // The measurement that forced two records rather than one: BulkCompanyRating carries nine fields and
        // none of them is overallScore, which both ordinary paths send on every row. Reusing it would silently
        // drop a measured field; adding the property to it would put a permanently-null member on the bulk
        // shape. This test fails if someone later "deduplicates" the two.
        Assert.Null(typeof(BulkCompanyRating).GetProperty("OverallScore"));
        Assert.NotNull(typeof(CompanyRating).GetProperty(nameof(CompanyRating.OverallScore)));
    }

    // ---- the one-row default ------------------------------------------------------------------------------

    [Fact]
    public async Task The_history_sends_a_limit_of_one_hundred_when_the_caller_gives_none()
    {
        // The trap, and the one place in this slice where a limit is defaulted. Measured 2026-08-28:
        // ratings-historical?symbol=AAPL with no limit answers exactly ONE row; limit=5 answers 5; limit=100
        // answers 100; limit=10000 answers 6292, which is AAPL's whole series and not a cap. Faithfully passing
        // FMP's default through would give a caller one row from an endpoint called "historical".
        var (endpoints, handler) = Build();

        await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal("stable/ratings-historical", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&limit=100&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task An_explicit_limit_replaces_the_default()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetRatingHistoryAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_history_limit_is_not_nullable_unlike_every_other_limit_in_this_slice()
    {
        // Deliberate asymmetry, pinned so it is not "tidied" into consistency later. Dividends, splits and
        // grade history all answer the whole series with no limit, so theirs are `int?` defaulting to null.
        // This one answers one row, so a null default would be useless.
        var parameter = typeof(AnalystEndpoints)
            .GetMethod(nameof(AnalystEndpoints.GetRatingHistoryAsync))!
            .GetParameters()
            .Single(p => p.Name == "limit");

        Assert.Equal(typeof(int), parameter.ParameterType);
        Assert.Equal(100, parameter.DefaultValue);
    }

    // ---- requests and validation --------------------------------------------------------------------------

    [Fact]
    public async Task The_snapshot_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetRatingAsync("AAPL");

        Assert.Equal("stable/ratings-snapshot", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void Neither_rating_method_offers_a_date_range_because_the_history_ignores_one()
    {
        // Measured 2026-08-28: ratings-historical?symbol=AAPL&limit=1000 answers 1000 rows with and without
        // from=2024-01-01&to=2024-12-31.
        foreach (var name in new[]
                 {
                     nameof(AnalystEndpoints.GetRatingAsync),
                     nameof(AnalystEndpoints.GetRatingHistoryAsync),
                 })
            Assert.DoesNotContain(
                typeof(AnalystEndpoints).GetMethod(name)!.GetParameters(),
                p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_from_the_snapshot_and_an_empty_list_from_the_history()
    {
        var (snapshot, _) = Build("[]");
        var (history, _) = Build("[]");

        Assert.Null(await snapshot.GetRatingAsync("NOSUCHTICKER"));
        Assert.Empty(await history.GetRatingHistoryAsync("NOSUCHTICKER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Both_methods_refuse_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (snapshot, h1) = Build();
        var (history, h2) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => snapshot.GetRatingAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => history.GetRatingHistoryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
    }

    [Fact]
    public async Task Both_methods_refuse_a_null_symbol_before_spending_a_request()
    {
        var (snapshot, _) = Build();
        var (history, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => snapshot.GetRatingAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => history.GetRatingHistoryAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_history_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetRatingHistoryAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_letter_rating_is_a_string_because_the_observed_scale_is_not_A_to_F()
    {
        // The shipped BulkCompanyRating documents the measurement: across 45,008 bulk rows the values ran
        // C, B+, C+, B, A-, B-, C-, D+, A, A+, and then S- and S -- two grades ABOVE A+ -- while D- and F never
        // appeared at all. A scale inferred from any one snapshot is wrong at both ends.
        var rows = JsonSerializer.Deserialize(
            """[{"rating":"S"},{"rating":"S-"},{"rating":"A+"},{"rating":"Z"}]""",
            FmpJsonContext.Default.ListCompanyRating)!;

        Assert.Equal(["S", "S-", "A+", "Z"], rows.Select(r => r.Rating));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CompanyRatingTests"`
Expected: the build fails — `CS0246` for `CompanyRating`, `CS1061` for the two methods.

- [ ] **Step 4: Write the record**

`src/FmpDotNet/Models/CompanyRating.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's own letter rating for one company and the six component scores behind it. Serves both
/// <c>stable/ratings-snapshot</c> and <c>stable/ratings-historical</c>.
///
/// <para><b>The two paths differ by exactly one field.</b> Measured 2026-08-28, the snapshot sends nine —
/// <c>symbol</c>, <c>rating</c>, <c>overallScore</c> and the six components — and the history sends the same
/// nine plus <c>date</c>. So <see cref="Date"/> is nullable and is null on every row the snapshot returns; the
/// same pattern as <see cref="EmployeeCount"/>, where one record serves two paths and the discriminating field
/// carries the difference.</para>
///
/// <para><b>Not the same type as <see cref="BulkCompanyRating"/>, and the difference is one field.</b> That
/// type — built for <c>stable/rating-bulk</c> — carries nine fields and <b>no <c>overallScore</c></b>, which
/// both of these paths send on every row. Reusing it would drop a measured value; widening it would put a
/// permanently-null property on the bulk shape. Two records with nine overlapping fields is the honest
/// outcome.</para>
///
/// <para><b><see cref="Rating"/> is the upstream string and the scale is not the one you would guess.</b>
/// Measured across 45,008 rows on the bulk path: C, B+, C+, B, A-, B-, C-, D+, A, A+, and then <b>S-</b> and
/// <b>S</b> — two grades above A+, which no A-to-F enum would have a member for — while D- and F never appeared
/// at all.</para></summary>
public sealed record CompanyRating
{
    /// <summary>The symbol the rating is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The day FMP computed the rating, or <see langword="null"/>.
    ///
    /// <para><b>Always null from <c>ratings-snapshot</c></b>, which sends no date at all — so a null here means
    /// "this came from the snapshot", not "FMP does not know when". The history series is per <i>trading</i>
    /// day: weekends and holidays are absent rather than repeated.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The letter grade. See the type's remarks: the observed scale runs to <c>S</c>, above
    /// <c>A+</c>.</summary>
    [JsonPropertyName("rating")] public string? Rating { get; init; }

    /// <summary>FMP's overall score, 1 to 5 across the rows measured.
    ///
    /// <para><b>The one field <see cref="BulkCompanyRating"/> does not carry</b>, and therefore the reason these
    /// are two records rather than one.</para></summary>
    [JsonPropertyName("overallScore")] public int? OverallScore { get; init; }

    /// <summary>Score for the discounted-cash-flow factor.</summary>
    [JsonPropertyName("discountedCashFlowScore")] public int? DiscountedCashFlowScore { get; init; }

    /// <summary>Score for the return-on-equity factor.</summary>
    [JsonPropertyName("returnOnEquityScore")] public int? ReturnOnEquityScore { get; init; }

    /// <summary>Score for the return-on-assets factor.</summary>
    [JsonPropertyName("returnOnAssetsScore")] public int? ReturnOnAssetsScore { get; init; }

    /// <summary>Score for the debt-to-equity factor.</summary>
    [JsonPropertyName("debtToEquityScore")] public int? DebtToEquityScore { get; init; }

    /// <summary>Score for the price-to-earnings factor.</summary>
    [JsonPropertyName("priceToEarningsScore")] public int? PriceToEarningsScore { get; init; }

    /// <summary>Score for the price-to-book factor.</summary>
    [JsonPropertyName("priceToBookScore")] public int? PriceToBookScore { get; init; }
}
```

- [ ] **Step 5: Register the record**

In `FmpJsonContext.cs`: `[JsonSerializable(typeof(List<CompanyRating>))]`

- [ ] **Step 6: Write the two methods**

Append to `AnalystEndpoints.cs`:

```csharp
    /// <summary>FMP's current letter rating for one symbol, from <c>stable/ratings-snapshot</c>. Returns
    /// <see langword="null"/> when FMP has no rating.
    ///
    /// <para><b>The returned row carries no date</b> — this endpoint sends none, so
    /// <see cref="CompanyRating.Date"/> is always null here. Use <see cref="GetRatingHistoryAsync"/> if you need
    /// to know when a rating applied.</para>
    ///
    /// <para>One row, unwrapped as <see cref="CompanyEndpoints.GetProfileAsync"/> does. An
    /// unknown-but-well-formed symbol answers an empty array with HTTP 200, not a 404.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<CompanyRating?> GetRatingAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var rows = await transport.GetListAsync(
            new FmpRequest("stable/ratings-snapshot").With("symbol", symbol),
            FmpJsonContext.Default.ListCompanyRating, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>FMP's rating for one symbol over time, newest first, from <c>stable/ratings-historical</c>.
    ///
    /// <para><b><paramref name="limit"/> defaults to 100, and that is deliberately <i>not</i> what FMP does.</b>
    /// Measured 2026-08-28: with no <c>limit</c> this endpoint answers <b>exactly one row</b> — from a path
    /// named "historical". Passing FMP's default through faithfully would be useless to a caller, so this method
    /// sends 100 unless told otherwise. The measured ladder, for anyone choosing a value: <c>limit=5</c> → 5,
    /// <c>100</c> → 100, <c>1000</c> → 1000, <c>5000</c> → 5000, <c>10000</c> → <b>6292</b>, <c>50000</c> →
    /// 6292. That last figure is AAPL's whole series, not a cap — it stops growing because the data does. There
    /// is therefore no maximum page size to enforce here.</para>
    ///
    /// <para>This is the only <c>limit</c> in this endpoint group with a non-null default. The dividend, split
    /// and grade-history methods all leave theirs null, because those endpoints answer the whole series when the
    /// parameter is absent and a default would silently truncate it.</para>
    ///
    /// <para><b><c>from</c> and <c>to</c> are ignored</b>, measured the same day: 1000 rows with and without a
    /// 2024 range. The series is per trading day. Filter on <see cref="CompanyRating.Date"/> at the call
    /// site.</para></summary>
    /// <param name="symbol">Ticker as FMP spells it.</param>
    /// <param name="limit">Newest N rows. Defaults to 100 rather than to FMP's own default of one. Must be
    /// positive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<IReadOnlyList<CompanyRating>> GetRatingHistoryAsync(
        string symbol, int limit = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit must be positive.");

        return await transport.GetListAsync(
            new FmpRequest("stable/ratings-historical").With("symbol", symbol).With("limit", limit),
            FmpJsonContext.Default.ListCompanyRating, ct).ConfigureAwait(false);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CompanyRatingTests"`
Expected: PASS — 14 test methods, **16 test cases** (12 `[Fact]`, plus 2 and 2 `[InlineData]` rows on the two `[Theory]` methods). Zero warnings.

- [ ] **Step 8: Run the whole unit suite**

This is the last task that adds an endpoint, so run everything before moving to the sweep.

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: **exactly one failure**, `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`, whose diff should list all fourteen new paths — seven under `fmp.Analyst` and seven under `fmp.Calendar`. Read that diff and check the fourteen against the spec's signature block before continuing; it is the cheapest end-to-end proof that every path is wired to the method it should be. Any *other* failure is a real regression and stops the task.

- [ ] **Step 9: Mutation-check the default that exists to be non-faithful**

Change `int limit = 100` to `int? limit = null` and drop the `.With("limit", limit)` guard accordingly.

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CompanyRatingTests"`
Expected: **2** failures — `The_history_sends_a_limit_of_one_hundred_when_the_caller_gives_none` (the query is now `?symbol=AAPL&apikey=k`) and `The_history_limit_is_not_nullable_unlike_every_other_limit_in_this_slice`. The second is the one that matters: it exists because the first would keep passing if someone changed the default from 100 to some other number, and because "make all the limits consistent" is a plausible future tidy-up that would reintroduce the one-row default. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Models/CompanyRating.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/AnalystEndpoints.cs tests/FmpDotNet.Tests/CompanyRatingTests.cs \
        tests/FmpDotNet.Tests/Fixtures/ratings-snapshot.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/ratings-historical.AAPL.json
git commit -m "feat: company ratings, and the history endpoint that defaults to one row (#37)"
```

---

### Task 9: Teach the live sweep to ask the five new calendars something worth answering

The live sweep discovers endpoints by reflection, so all fourteen new methods are already in it. What it does not get right by itself is the date range: `Probe.Argument`'s `from` arm answers `LiveApi.SettledWeekday` for anything declared on `CalendarEndpoints`, and `to` answers the same, so all five new date-ranged methods would be probed over a **one-day window**.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` (+1 constant), `tests/FmpDotNet.SmokeTests/Probe.cs` (`Argument`'s `from` arm), `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` (+2 keyless guards)

**Interfaces:**
- Consumes: the five date-ranged `CalendarEndpoints` methods from Tasks 2–5, by name.
- Produces: `LiveApi.CalendarWeekStart`, a `LocalDate` seven days wide ending at `SettledWeekday`.

**The measurement this task acts on.** Row counts at each width, measured 2026-08-28 with `to = 2026-08-21`:

| method | 1 day | 7 days | 14 days | 30 days |
|---|---|---|---|---|
| `GetDividendsCalendarAsync` | 331 | 1,652 | 3,249 | **4,000 — at the cap** |
| `GetSplitsCalendarAsync` | 12 | 40 | 93 | 235 |
| `GetIpoCalendarAsync` | 5 | 34 | 74 | 151 |
| `GetIpoDisclosuresAsync` | 116 | 764 | 1,379 | 2,523 |
| `GetIpoProspectusesAsync` | **1** | 8 | 14 | 42 |

Seven days is the width that serves all five: every one answers comfortably above zero, and the dividend calendar sits at 41% of its cap with room for a heavy season. Fourteen days would put the dividend calendar at 81% of the cap, and thirty days over it — a truncated baseline would then record `LikelyTruncated` as this endpoint's normal state, which is a bad thing to normalise. One day, today, records one row for `ipos-prospectus`: not yet the silent-green failure the previous slice fixed, but one quiet week away from it on a suite that runs unattended.

**`GetEarningsCalendarAsync` keeps its one-day window and must not be widened.** Its own documentation measures a 7-day peak-season window at 3,676 rows — 92% of the 4000 cap — and a 31-day window at exactly 4000. Narrowing it was the previous slice's fix; this task must not undo it. `EconomicsEndpoints` likewise stays narrow: its documentation records "the widest range verified intact here is one week", and a 7-day window sits exactly on that boundary with no margin.

- [ ] **Step 1: Write the failing keyless guards**

Append to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`:

```csharp
    [Fact]
    public void The_sweep_gives_the_five_new_calendars_a_week_and_the_earnings_calendar_a_day()
    {
        // Probe.Argument dispatches `from` on the declaring type, and every date-ranged CalendarEndpoints method
        // used to get LiveApi.SettledWeekday for both ends -- a one-day window. Measured 2026-08-28 with
        // to=2026-08-21, one day answers: dividends 331, splits 12, ipos-calendar 5, ipos-disclosure 116, and
        // ipos-prospectus ONE. A single quiet week takes that last one to zero, which records `outcome empty`
        // as its baseline and then agrees with itself for ever.
        //
        // Seven days answers 1652 / 40 / 34 / 764 / 8 -- all comfortably non-zero, and the dividend calendar at
        // 41% of its 4000-row cap rather than the 81% a fortnight would give it.
        //
        // GetEarningsCalendarAsync is the deliberate exception and stays at one day: its own doc measures a
        // 7-day peak-season window at 3676 rows, 92% of the same cap. Narrowing it was the previous slice's
        // fix and widening it here would undo that.
        var calendar = typeof(Endpoints.CalendarEndpoints);
        var weekly = new[]
        {
            nameof(Endpoints.CalendarEndpoints.GetDividendsCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetSplitsCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoDisclosuresAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoProspectusesAsync),
        };

        foreach (var name in weekly)
        {
            var method = calendar.GetMethod(name)!;
            var from = (NodaTime.LocalDate)Probe.Argument(method.GetParameters()[0]);
            var to = (NodaTime.LocalDate)Probe.Argument(method.GetParameters()[1]);

            Assert.Equal(6, NodaTime.Period.DaysBetween(from, to));
        }

        var earnings = calendar.GetMethod(nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync))!;
        Assert.Equal(
            LiveApi.SettledWeekday,
            (NodaTime.LocalDate)Probe.Argument(earnings.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_never_widens_a_window_that_was_narrowed_because_it_truncates()
    {
        // The regression guard for the two endpoints whose own documentation measures a wide window as unsafe.
        // A future change that collapsed the `from` arm back to one rule per declaring type would widen both of
        // these, and nothing else in the suite would notice until the next scheduled live run.
        var earnings = typeof(Endpoints.CalendarEndpoints)
            .GetMethod(nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync))!.GetParameters()[0];
        var economic = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync))!.GetParameters()[0];

        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(earnings));
        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(economic));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"`
Expected: `The_sweep_gives_the_five_new_calendars_a_week_and_the_earnings_calendar_a_day` **FAILS** on the first `Assert.Equal(6, …)`, reporting 0 days — because `from` and `to` both resolve to `SettledWeekday` today. `The_sweep_never_widens_a_window_that_was_narrowed_because_it_truncates` **PASSES** already, which is correct and expected: it is a regression guard on behaviour that is currently right, and its job starts in Step 3 when the arm it guards is rewritten. No key is needed for either — every test in this class is pure reflection.

- [ ] **Step 3: Add the constant**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, beside `RangeStart`:

```csharp
    /// <summary>The start of the week-long window the five new date-ranged Calendar probes ask for.
    ///
    /// <para><b>Named rather than reusing <see cref="SettledWeekday"/> for both ends, because a one-day window
    /// is one quiet week away from an empty baseline on the sparsest of them.</b> Measured 2026-08-28 with
    /// <c>to=2026-08-21</c>: over a single day, <c>ipos-prospectus</c> answered <b>1 row</b>,
    /// <c>ipos-calendar</c> 5 and <c>splits-calendar</c> 12. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties and matches that baseline every week thereafter — the silent
    /// green this suite exists to prevent, and the same failure <see cref="Exchange"/> and <see cref="Cik"/>
    /// were named for.</para>
    ///
    /// <para>Over seven days the same five answered 1652, 40, 34, 764 and 8. Seven and not fourteen because of
    /// the other direction: <c>dividends-calendar</c> caps at 4000 rows and answered 3249 over a fortnight —
    /// 81% of the cap — against 1652 over a week. A baseline recorded from a truncated response would normalise
    /// truncation as that endpoint's healthy state.</para>
    ///
    /// <para>Not used for <c>GetEarningsCalendarAsync</c> or for the economic calendar; both measured a 7-day
    /// window as unsafe on their own endpoints and keep <see cref="SettledWeekday"/>.</para></summary>
    public static LocalDate CalendarWeekStart => SettledWeekday.PlusDays(-6);
```

- [ ] **Step 4: Rewrite the `from` arm**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, replace the `LocalDate` switch. The **order of the arms is load-bearing** — the earnings-calendar arm must precede the general `CalendarEndpoints` arm, or it never fires:

```csharp
        if (type == typeof(LocalDate))
            return parameter.Name switch
            {
                // The economic calendar's own doc: "the widest range verified intact here is one week", after a
                // 6-month window returned FEWER rows than the 3-month window it contains and a -3-to-+12-month
                // window returned 0. A week sits exactly on that boundary with no margin, so it keeps the day.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    => LiveApi.SettledWeekday,

                // The earnings calendar is the deliberate exception among the Calendar methods, and this arm
                // MUST come before the general one below. Its own doc records day-at-a-time as "the only chunk
                // width measured to be safe": a 7-day peak-season window returned 3676 rows against a 4000-row
                // cap, and a 31-day window returned exactly 4000. Narrowing it was the previous slice's fix.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.CalendarEndpoints)
                    && parameter.Member.Name == nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync)
                    => LiveApi.SettledWeekday,

                // The five other date-ranged Calendar methods are sparse enough that a single day is thin and
                // getting thinner: measured 2026-08-28, one day answered 1 row on ipos-prospectus and 5 on
                // ipos-calendar. A week answers 8 and 34, and keeps dividends-calendar at 41% of its cap rather
                // than the 81% a fortnight would give it. See LiveApi.CalendarWeekStart.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.CalendarEndpoints)
                    => LiveApi.CalendarWeekStart,

                // Everything else -- the three sec-filings-search paths this dispatch was written for, plus the
                // per-symbol chart and market-cap methods -- is unaffected by width and keeps the 90-day range.
                "from" => LiveApi.RangeStart,
                _ => LiveApi.SettledWeekday,
            };
```

Update the block comment above the `LocalDate` case to match: it currently says `from` is dispatched "on the parameter's DECLARING TYPE" and names two groups. It is now dispatched on declaring type **and, within `CalendarEndpoints`, on the method name**, and there are three outcomes rather than two.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"`
Expected: PASS — 9 test methods (the 7 that existed plus the 2 added here). Zero warnings. No key required.

- [ ] **Step 6: Mutation-check the arm ordering, which is the failure this task is most likely to ship**

Move the general `CalendarEndpoints` arm **above** the earnings-calendar arm.

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter "FullyQualifiedName~SweepCoverageTests"`
Expected: **2** failures — `The_sweep_gives_the_five_new_calendars_a_week_and_the_earnings_calendar_a_day` on its final assertion, and `The_sweep_never_widens_a_window_that_was_narrowed_because_it_truncates` on the earnings assertion. Both name the earnings calendar, and that is the point: this is a silent widening that no compiler and no unit test outside this file can see, and it would only surface as a truncated baseline on the next scheduled live run. Note also that the first three assertions of the first test still pass — the five weekly methods are unaffected — so a test that checked only the new behaviour would have missed it entirely. Restore with `cp`, verify with `diff`, rebuild with `--no-incremental`.

- [ ] **Step 7: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs
git commit -m "test: give the five new calendars a week-wide sweep window, keeping earnings at a day (#37)"
```

---

### Task 10: Regenerate the README, re-record the live baseline

The last task. It closes the one failing test that has been red since Task 2 and takes the live shape of all fourteen new endpoints on record. **This task needs `FMP_API_KEY`.**

**Files:**
- Modify: `README.md` (generated coverage block, and the remaining-work paragraph, which is hand-written)
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

**Interfaces:** consumes everything. Produces nothing new.

- [ ] **Step 1: Regenerate the coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

This drives every public endpoint method against a stub, records the path each one requests, and rewrites the block between the `BEGIN GENERATED` / `END GENERATED` markers. It needs no key — the stub answers HTTP 400 to everything.

- [ ] **Step 2: Check the regenerated table before trusting it**

```bash
git diff README.md
```

Expected, and check each of these rather than skimming:
- the headline reads **`**140 of FMP's 243 endpoint paths are modelled.**`** — 126 + 14, with no path counted twice;
- `fmp.Analyst` gains **seven** rows: `grades`, `grades-consensus`, `grades-historical`, `price-target-consensus`, `price-target-summary`, `ratings-historical`, `ratings-snapshot`;
- `fmp.Calendar` gains **seven** rows: `dividends`, `dividends-calendar`, `ipos-calendar`, `ipos-disclosure`, `ipos-prospectus`, `splits`, `splits-calendar`;
- **nothing else in the block changed.** Any other row moving means an existing endpoint's request changed, which is a regression from this branch and not a regeneration.

If the count is not 140, stop and find out why before editing anything by hand. The table is generated from the code; a wrong number in it means the code is wrong, not the table.

- [ ] **Step 3: Update the hand-written remaining-work paragraph**

The prose under **"Reaching an endpoint that is not modelled"** is *not* generated and will otherwise contradict the table directly above it. Four figures change and one list entry goes away. Current text:

> **117 paths remain**, of which **110 are actionable** … the largest groups are Form 13F & Insider Trades (14) and Analyst & Calendar (14), then Senate & House (12) and Economics/Transcripts/ESG/COT (12), Market Performance (11), News (10) and Fundraisers & DCF (10); ETF & Mutual Funds, Technical Indicators and Indexes & Market Hours carry 9 apiece.
>
> That remainder is tracked as **eleven issues** under the epic, **ten of them actionable**, each 9 to 14 paths …
>
> The counts above are the sum of those issues and reconcile exactly against the 243-path inventory: **126 modelled plus 117 remaining**, with no path counted twice and none missing.

Replace with:

> **103 paths remain**, of which **96 are actionable** … the largest group is Form 13F & Insider Trades (14), then Senate & House (12) and Economics/Transcripts/ESG/COT (12), Market Performance (11), News (10) and Fundraisers & DCF (10); ETF & Mutual Funds, Technical Indicators and Indexes & Market Hours carry 9 apiece.
>
> That remainder is tracked as **ten issues** under the epic, **nine of them actionable**, each 9 to 14 paths …
>
> The counts above are the sum of those issues and reconcile exactly against the 243-path inventory: **140 modelled plus 103 remaining**, with no path counted twice and none missing.

The arithmetic, written out so it can be checked rather than trusted: 117 − 14 = 103 remaining; 110 − 14 = 96 actionable, the seven `tipranks-*` paths still blocked on a paid add-on; 140 + 103 = 243. **"Analyst & Calendar (14)" is deleted from the largest-groups list**, and the sentence changes from "the largest groups are X and Y" to "the largest group is X" because only one 14-path group is left.

Edit the surrounding wording as needed so it reads naturally — this paragraph is prose, not a table, and a mechanical find-and-replace will leave "the largest groups are Form 13F & Insider Trades (14), then …" ungrammatical.

- [ ] **Step 4: Confirm the whole unit suite is green**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: **PASS, zero failures.** `EndpointCoverageTests` has been the one expected failure since Task 2 and this is where it goes green. `Every_sdk_member_the_readme_names_still_exists` also runs here and will catch any method name you mistyped into the prose.

- [ ] **Step 5: Re-record the live smoke baseline**

Read the key from `.env` into this one command's environment. **Never `source` the file and never `set -a`** — it carries other variables, and doing so has previously clobbered `PATH` for a whole shell:

```bash
FMP_API_KEY="$(sed -n 's/^[[:space:]]*FMP_API_KEY[[:space:]]*=[[:space:]]*["'"'"']\{0,1\}\([^"'"'"'[:space:]]*\).*/\1/p' .env)" \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

**Verify the key was actually extracted before reading the results.** A one-liner like this has silently produced an empty string before, and an empty key makes every live test skip while the run still reports success — a green suite that called nothing. Check that `baseline-ordinary.txt` actually changed, and that the run reported live tests as *run* rather than *skipped*.

`FMPDOTNET_SMOKE_BULK` is **not** set and must not be. The bulk endpoints are untouched by this slice, `baseline-bulk.txt` must not change, and FMP's own throttle message warns that frequent calls there can get a key restricted.

- [ ] **Step 6: Check the baseline diff**

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Expected:
- **fourteen new `[Group.Method]` blocks** — seven under `Analyst.`, seven under `Calendar.` — each recording `outcome rows` with a non-zero count and a `set` line per property that arrived;
- **`Calendar.GetEarningsCalendarAsync` unchanged**, because Task 9 left its window at one day. If this block moved, the arm ordering in `Probe.Argument` is wrong — go back to Task 9.
- **`Economics.GetEconomicCalendarAsync` unchanged**, for the same reason;
- the measured-on date in the file header updated.

Then check the fourteen new blocks against the measurements, since this is the first time the real API has been through the actual SDK types rather than through a probe script:
- none reads `outcome empty` — an empty baseline is the silent-green failure this whole suite exists to prevent, and Task 9's widths were chosen to make it impossible;
- `Calendar.GetIpoCalendarAsync` records `PriceRange` as **set** on at least one row. It is null on 441 of 450, so a week-wide window may legitimately catch none — if `PriceRange` is absent from that block, that is expected rather than wrong, but say so explicitly in your report rather than letting it pass unmentioned;
- `Analyst.GetPriceTargetSummaryAsync` records `Publishers` as set, which is the live proof the converter works end to end;
- `Analyst.GetRatingAsync` records `Date` as **absent**, and `Analyst.GetRatingHistoryAsync` records it as set. That asymmetry is the `EmployeeCount` pattern working as designed, and seeing it in the baseline is the cheapest confirmation the single shared record was the right call.

- [ ] **Step 7: Verify no key reached a tracked file**

Write the extraction to a script file rather than repeating the one-liner — the inline version has failed silently before, and an empty key matches every file, producing a false clean result:

```bash
cat > /tmp/checkkey.sh <<'SH'
set -eu
KEY="$(sed -n 's/^[[:space:]]*FMP_API_KEY[[:space:]]*=[[:space:]]*["'"'"']\{0,1\}\([^"'"'"'[:space:]]*\).*/\1/p' .env)"
[ -n "$KEY" ] || { echo "FATAL: key not extracted — this check would pass vacuously"; exit 1; }
if git grep -qF -- "$KEY"; then echo "FATAL: the key is in a tracked file"; git grep -lF -- "$KEY"; exit 1; fi
echo "clean: key not present in any tracked file"
SH
sh /tmp/checkkey.sh && rm /tmp/checkkey.sh
```

Expected: `clean: key not present in any tracked file`. The guard on the second line is the point — without it an empty `$KEY` matches everything and the check reports whatever it likes.

- [ ] **Step 8: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "docs: 140 of 243 paths modelled, and the live baseline for the fourteen new ones (#37)"
```

---

## Coverage of the spec

Run against the corrected spec, section by section.

**The fourteen paths.** Every one is implemented, and each appears in exactly one task:

| path | facade | method | task |
|---|---|---|---|
| `stable/dividends` | Calendar | `GetDividendsAsync` | 2 |
| `stable/dividends-calendar` | Calendar | `GetDividendsCalendarAsync` | 2 |
| `stable/splits` | Calendar | `GetSplitsAsync` | 3 |
| `stable/splits-calendar` | Calendar | `GetSplitsCalendarAsync` | 3 |
| `stable/ipos-calendar` | Calendar | `GetIpoCalendarAsync` | 4 |
| `stable/ipos-disclosure` | Calendar | `GetIpoDisclosuresAsync` | 5 |
| `stable/ipos-prospectus` | Calendar | `GetIpoProspectusesAsync` | 5 |
| `stable/grades` | Analyst | `GetGradesAsync` | 6 |
| `stable/grades-consensus` | Analyst | `GetGradeConsensusAsync` | 6 |
| `stable/grades-historical` | Analyst | `GetGradeHistoryAsync` | 6 |
| `stable/price-target-consensus` | Analyst | `GetPriceTargetConsensusAsync` | 7 |
| `stable/price-target-summary` | Analyst | `GetPriceTargetSummaryAsync` | 7 |
| `stable/ratings-snapshot` | Analyst | `GetRatingAsync` | 8 |
| `stable/ratings-historical` | Analyst | `GetRatingHistoryAsync` | 8 |

**The eleven records**, each created once: `Dividend` (2), `StockSplit` (3), `IpoCalendarEntry` (4), `IpoDisclosure` and `IpoProspectus` (5), `StockGrade`, `GradeConsensus` and `GradeHistory` (6), `PriceTargetConsensus` and `PriceTargetSummary` (7), `CompanyRating` (8).

**The eleven `FmpJsonContext` registrations**, one per record: 1 in Task 2, 1 in Task 3, 1 in Task 4, 2 in Task 5, 3 in Task 6, 2 in Task 7, 1 in Task 8. `List<string>` is **not** among them — already registered for `BulkPriceTargetSummary`, and a duplicate is a source-generator error.

**One generic result type** (`CalendarResult<T>`, Task 1) and **one converter** (`PublisherListJsonConverter`, Task 7).

**All fifteen traps**, each with a test that fails when the trap is reintroduced:

| # | trap | defended in |
|---|---|---|
| 1 | `dividends-calendar` caps at 4000 and eats the front of the range | Task 2 |
| 2 | `ratings-historical` answers one row with no `limit` | Task 8 |
| 3 | `grades` ignores `limit` and `page` | Task 6 |
| 4 | `from`/`to` ignored on all five per-symbol paths | Tasks 2, 3, 6, 8 |
| 5 | `grades-consensus` is not the newest `grades-historical` row | Task 6 |
| 6 | `ipos-calendar.daa` duplicates `date` at a constant time | Task 4 |
| 7 | `acceptedDate` here is a date, not the SEC paths' timestamp | Task 5 |
| 8 | `splitType` is JSON-null on 16 of 961 rows | Task 3 |
| 9 | `declarationDate` blank on over half the calendar's rows | Task 2 |
| 10 | `ipos-calendar` shares / priceRange / marketCap mostly null | Task 4 |
| 11 | `ipos-disclosure` returns 123,678 rows uncapped for a wide range | Task 5 |
| 12 | `frequency` shows 2 values on one path and 8 on another | Task 2 |
| 13 | `splits-calendar` and `ipos-calendar` answer a year with a quarter | Tasks 3, 4 |
| 14 | `ipos-calendar.priceRange` is a formatted string | Task 4 |
| 15 | `marketCap` and two prospectus totals exceed `int` by ~35× | Tasks 4, 5 |

**Sixteen fixtures**, not the spec's seventeen. Fourteen are one per path; two are trap captures that a path fixture cannot carry — `splits-calendar.split-types.json` (the head fixture is `stock-split` on all five rows) and `ipos-calendar.priced.json` (the head fixture is null in all three numeric fields on all five rows). The spec's third extra capture, a `dividends-calendar` row with a blank `declarationDate`, is **already the head fixture** — all five of its captured rows carry one — and `dividends.AAPL.json` carries the populated case, so both states are covered by real captures with no hand-built third file.

**The live sweep** is Task 9, and the count the spec got wrong (two date-ranged methods, not five) is corrected there with the per-method row counts that justify each width.

**Totals.** 115 new test methods and **132 test cases** across 8 test files — 7 created, plus `SweepCoverageTests` modified. 16 fixtures, 14 paths, 11 records, 11 context registrations, 1 generic result type, 1 converter, 0 new facades, 0 changes to `FmpClient` or the DI registration.

## Deliberately not done

- **`EarningsCalendarResult` is not folded into `CalendarResult<T>`.** It is shipped public API on a shipped path with its own tests, nothing in this slice needs it moved, and the spec says explicitly that it is the model being followed rather than modified. It leaves the SDK with two types doing one job, which is a real cost and is recorded here as the next natural follow-up.
- **No auto-chunking on any calendar.** Three of the five date-ranged methods now report that they truncated; none of them narrows and retries. That needs request-count limits, cancellation semantics and a decision about partial failure mid-walk, and it is a slice of its own.
- **`NullableLocalDateJsonConverter` is not given a token-type guard.** It reads `reader.GetString()` unguarded, which is the same latent defect the previous slice found and fixed on `BusinessAddressJsonConverter`: a non-string token would throw and, because `FmpTransport` does not wrap `DeserializeAsync`, cost the whole response. Every date field measured in this slice arrives as a string or is absent, so the risk here is hypothetical — and that converter is shared by many already-shipped endpoints, which makes changing it a decision of its own rather than a rider on a coverage slice. `PublisherListJsonConverter`, being new, is written with the guard from the start.
- **`limit` bounds are not enforced beyond "positive".** No `Max*PageSize` constant is added, because no cap was measured on any path here: `ratings-historical` answered 6,292 for both `limit=10000` and `limit=50000` because that is AAPL's whole series, not a ceiling.
