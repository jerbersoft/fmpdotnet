# Economics, Earnings Transcripts, ESG and COT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cover issue #40's twelve paths — three onto the existing `fmp.Economics`, and three new facades
`fmp.Transcripts`, `fmp.Esg` and `fmp.Cot`.

**Architecture:** Twelve records over 203 properties, one closed `enum` for a case-sensitive query parameter,
and one change to shared transport. No new JSON converter — the first coverage slice of this size to need
none. `CotReport` is 128 properties, the widest record in the SDK against `FinancialRatios` at 66.

**Tech Stack:** .NET 10, System.Text.Json source generation, NodaTime, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-economics-transcripts-esg-and-cot-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-29-economics-transcripts-esg-and-cot-measurements.md`

## Global Constraints

- **Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as
  `[JsonSerializable(typeof(List<X>))]`, or it fails at runtime rather than at compile time.** Twelve entries
  are added across this plan: `EconomicObservation`, `MarketRiskPremium`, `TreasuryRate`,
  `EarningsTranscript`, `TranscriptDate`, `LatestTranscript`, `EsgDisclosure`, `EsgRating`, `EsgBenchmark`,
  `CotReport`, `CotAnalysis`, `CotSymbol`.
- **Adding a facade to `FmpClient` is four edits, not three.** The constructor parameter, the property, the DI
  registration in `FmpServiceCollectionExtensions` — and `tests/FmpDotNet.Tests/AddFmpTests.cs`, whose
  `Resolves_the_client_and_every_endpoint_group` asserts `typeof(FmpClient).GetProperties(...).Length` against a
  hard-coded count and goes red the moment a property appears. That assertion is deliberate: its own comment
  records that the list "was three short when SecFilings was added", so a forgotten facade fails loudly instead
  of going untested. Each of Tasks 4, 5 and 7 must add its `Assert.NotNull(client.X)` line and bump the count
  by one — 14 → 15 → 16 → 17. `EndpointCoverageTests` and `Probe` also reflect over `FmpClient`, but both
  enumerate rather than count and need no edit.
- **`TreatWarningsAsErrors` is on and covers XML-doc warnings.** Every public member needs a doc comment, and
  every `<see cref="..."/>` must resolve or the build fails (CS1574). **Tasks are ordered so that no task
  references a type a later task creates.** Do not reorder them.
- **A model's docs may point at the facade that serves it, and the model is always written first**, so those
  crefs are unresolvable until the facade exists. Every task where this happens says so at the step, and the
  rule is uniform: **write the cref as plain `<c>GetSomethingAsync</c>` when you write the model, then promote
  it to `<see cref="Endpoints.SomethingEndpoints.GetSomethingAsync"/>` at the step that creates the facade.**
  Skipping the promotion is not a build failure, only a weaker doc — so it is easy to lose. Each affected task
  has an explicit promotion step; do not treat it as optional. There are **ten** of these across the plan
  (counted in the code blocks themselves, not from prose): two in Task 2 and one in Task 3, promoted together
  in **Task 3 Step 12**; three in Task 4, promoted in
  **Task 4 Step 6b**; one in Task 5, promoted in **Task 5 Step 6b**; and three in Task 6, promoted in
  **Task 7 Step 4**.
- **CS1591 is not suppressed project-wide.** `CotReport` gets a file-scoped `#pragma warning disable CS1591`,
  becoming the **eighth** exemption. `src/FmpDotNet/FmpDotNet.csproj:19` currently says "Each of the seven
  models now carries a file-scoped `#pragma warning disable CS1591` instead" — that sentence is load-bearing
  and Task 6 Step 7 updates it. Quote it from the file rather than from here: `:14`'s "all in the seven
  period-shaped fundamentals models from #4" is a historical statement about #21's 262 warnings and stays as
  it is.
- **No reflection in `src/`.** `IsAotCompatible` is declared; `IL2026` and `IL3050` are build errors.
- **NodaTime only in public signatures** — no `DateTime`, `DateOnly`, `DateTimeOffset`, `TimeSpan`.
- **`[JsonPropertyName]` carries FMP's spelling exactly; the C# property carries correct English.** The
  attribute string is never "fixed". 27 of `CotReport`'s 128 properties diverge under this rule, in two kinds
  that are marked differently and deliberately so — **counted from the code block, not from this sentence**:
  - **26** are the suffix `Ol` where the block is `Old`. They are a family, documented once in the type
    summary; twenty-six identical `// sic` comments would bury the two below.
  - **2** are the misspelling `Spead` for `Spread`, and those carry `// sic` at their declaration.
  - `tradersNoncommSpeadOl` is in both counts, which is why 26 and 2 total 27 rather than 28.
  `CotAnalysis` carries a third misspelling, `netPostion` → `NetPosition`, also marked `// sic`. It is on a
  different record and is not among `CotReport`'s 27.
- **Every numeric measured `float` on any row is `decimal?`.** `int?` only where the field counts discrete
  things — open interest, positions, traders, years, quarters.
- **Empty strings are preserved, never normalised to null.**
- **No row-count guard anywhere.** Four paths truncate silently; all four are documented and none is guarded,
  for the reason already written into `GetEconomicCalendarAsync`.
- **Every task that adds an endpoint method must regenerate the README coverage block in the same task**, or
  it leaves `EndpointCoverageTests` red:

      FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests

- **Every task that adds an endpoint method must keep `SweepCoverageTests` green in the same task.** That
  suite runs **without a key** and fails the moment `Probe.Argument` meets a parameter name or type it has
  never seen. Probe arms are not deferred to the end.
- **No fixture may contain an API key.** FMP authenticates by query string; fixtures are response bodies only.
- **Tests may index into fixtures freely; the live sweep may not.** The transcript feed churns over tens of
  minutes.

## File Structure

**Create:**
- `src/FmpDotNet/EconomicIndicator.cs` — the 23-member closed set and its `ToQueryValue()` extension
- `src/FmpDotNet/Models/EconomicIndicators.cs` — `EconomicObservation` (3), `MarketRiskPremium` (4),
  `TreasuryRate` (13)
- `src/FmpDotNet/Models/EarningsTranscript.cs` — `EarningsTranscript` (5), `TranscriptDate` (3),
  `LatestTranscript` (4)
- `src/FmpDotNet/Models/EsgData.cs` — `EsgDisclosure` (11), `EsgRating` (7), `EsgBenchmark` (7)
- `src/FmpDotNet/Models/CotReport.cs` — `CotReport` (128), the one file carrying the new pragma
- `src/FmpDotNet/Models/CotAnalysis.cs` — `CotAnalysis` (16), `CotSymbol` (2)
- `src/FmpDotNet/Endpoints/TranscriptsEndpoints.cs` — 3 methods
- `src/FmpDotNet/Endpoints/EsgEndpoints.cs` — 3 methods
- `src/FmpDotNet/Endpoints/CotEndpoints.cs` — 3 methods
- `tests/FmpDotNet.Tests/EconomicIndicatorTests.cs`
- `tests/FmpDotNet.Tests/TranscriptsTests.cs`
- `tests/FmpDotNet.Tests/EsgTests.cs`
- `tests/FmpDotNet.Tests/CotTests.cs`
- twelve fixtures under `tests/FmpDotNet.Tests/Fixtures/` — three per task in Tasks 3 to 6

**Modify:**
- `src/FmpDotNet/FmpTransport.cs` — one `catch (JsonException)` in `ReadListAsync`
- `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs` — three methods, and the type summary rewritten
- `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs` — the "not modelled" note on `GetTranscriptSymbolsAsync`
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — twelve entries
- `src/FmpDotNet/FmpClient.cs` — three constructor parameters and three properties
- `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — three registrations
- `src/FmpDotNet/FmpDotNet.csproj` — seven exemptions becomes eight
- `tests/FmpDotNet.Tests/FmpTransportTests.cs` — the non-JSON-200 test
- `tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs` — the three new methods' binding and URL tests
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — five constants
- `tests/FmpDotNet.SmokeTests/Probe.cs` — one type arm and six name arms, one of which replaces an
  existing arm rather than adding to it
- `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` — three pinned-argument tests
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — twelve new blocks, re-recorded live
- `README.md` — the generated block, and the prose counts below it

## Deviation from the spec, recorded here because a reviewer will meet it

The spec was amended in commit `1f4e6bd` before this plan was written. Two of its changes bind this plan:

1. **`EconomicIndicator` is an `enum`, not a `readonly record struct`.** The spec's original reason for the
   struct — that an enum cannot express wire names beginning with a digit — is disproved by `ChartInterval`,
   which maps `OneMinute` to `1min` through the same extension-method mechanism. See the spec's
   `EconomicIndicator` section for the full argument.
2. **Three measured figures were wrong** and are corrected in both spec files: the transcript content length
   (46,546 → **46,487**, the decoded length rather than the JSON-escaped one), the "61-row cap" on
   `economic-indicators` and `treasury-rates` (not a row cap — a **~3-month window**), and "the GDP family
   returns zero rows for any range of a year or more" (a 335-day window returns a row; a 183-day one does
   not).

Anything in this plan that disagrees with the spec is a defect in this plan. Anything in the spec that
disagrees with the measurements is a defect in the spec.

---

### Task 1: the transport stops leaking `JsonException` on a non-JSON 200

**Files:**
- Modify: `src/FmpDotNet/FmpTransport.cs:61-83` (`ReadListAsync`)
- Modify: `tests/FmpDotNet.Tests/FmpTransportTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing new in the public API. `GetListAsync` and every method built on it now raise
  `FmpApiException` instead of `JsonException` when FMP answers HTTP 200 with a body that is not JSON —
  and **only** then. A body that is well-formed JSON but does not fit the model still raises `JsonException`,
  because that is this SDK's defect rather than FMP's answer, and several models document that throw as the
  outcome they want.

**Why this is first and why it is alone.** This is shared transport on the SDK's busiest path — every
`GetListAsync` call in the SDK goes through `ReadListAsync`. It closes more than issue #40:
`stable/financial-reports-xlsx` is already documented in `GetBytesAsync` as answering a MISS with sixteen
bytes of `Error with query` under a `content-type: application/json` that is a lie. Reviewing it against a
one-file diff is the point of giving it its own task.

- [ ] **Step 1: Write the failing test**

Add to `tests/FmpDotNet.Tests/FmpTransportTests.cs`, beside the other 200-with-an-error-body tests:

```csharp
    [Fact]
    public async Task A_two_hundred_carrying_a_body_that_is_not_json_raises_FmpApiException()
    {
        // Measured 2026-08-29: stable/economic-indicators?name=gdp answers HTTP 200,
        // `content-type: application/json; charset=utf-8`, and twelve bytes of `Invalid name` — which is not
        // JSON at all. Before this guard the caller got
        //
        //   System.Text.Json.JsonException: 'I' is an invalid start of a value. Path: $ | LineNumber: 0 …
        //
        // a raw serialisation exception naming neither the request nor what FMP actually said. GetObjectAsync
        // has caught this since #21; ReadListAsync did not, and the two pipelines diverged for no reason
        // anyone chose. `stable/financial-reports-xlsx` answers a MISS the same way with `Error with query`.
        var (transport, _) = Build(StubHandler.Json("Invalid name"));

        var thrown = await Assert.ThrowsAsync<FmpApiException>(
            () => transport.GetListAsync(
                new FmpRequest("stable/economic-indicators").With("name", "gdp"),
                FmpJsonContext.Default.ListEconomicRelease));

        // FmpApiException has no Request property — FmpApiException(message, requestUri) folds the request
        // into Message as "(request: …)", and ErrorMessage keeps FMP's own text alone.
        Assert.Contains("not JSON", thrown.ErrorMessage);
        Assert.Contains("stable/economic-indicators", thrown.Message);
    }
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test tests/FmpDotNet.Tests \
    --filter "FullyQualifiedName~A_two_hundred_carrying_a_body_that_is_not_json"
```

Expected: FAIL. `Assert.ThrowsAsync<FmpApiException>` reports that a
`System.Text.Json.JsonException` was thrown instead.

If it fails any other way, stop — the premise of this task is that the exception currently escapes.

- [ ] **Step 3: Add the guard**

In `src/FmpDotNet/FmpTransport.cs`, the first meaningful byte is currently computed inline in the
error-envelope check and thrown away. Hoist it into a local, because the guard below needs it too. Replace:

```csharp
        // An error envelope is a JSON OBJECT where success is a JSON ARRAY, so the first non-space byte separates
        // them without parsing either.
        if (FirstMeaningfulByte(prefix, prefixLength) == (byte)'{')
            throw await ReadErrorAsync(body, request, ct).ConfigureAwait(false);

        return await deserialise(body, ct).ConfigureAwait(false) ?? [];
```

with:

```csharp
        // An error envelope is a JSON OBJECT where success is a JSON ARRAY, so the first non-space byte separates
        // them without parsing either.
        var first = FirstMeaningfulByte(prefix, prefixLength);
        if (first == (byte)'{')
            throw await ReadErrorAsync(body, request, ct).ConfigureAwait(false);

        // A 200 whose body is not JSON at all. Measured 2026-08-29, `stable/economic-indicators` answers an
        // unrecognised `name` with HTTP 200, `content-type: application/json; charset=utf-8`, and twelve
        // bytes of `Invalid name`. The check above cannot catch it: the first meaningful byte is `I`, neither
        // `{` nor the start of an array. Without this the caller gets a raw JsonException naming the byte
        // offset and nothing else — not the request, not what FMP said. GetObjectAsync has had this guard
        // since #21 and this pipeline had not; they were divergent by accident rather than by decision.
        // `stable/financial-reports-xlsx` answers a MISS the same way, with `Error with query`.
        //
        // The filter is what keeps the guard honest, and it is not optional. `deserialise` both PARSES and
        // BINDS, so an unfiltered catch would also swallow a well-formed array whose field is the wrong type
        // — and report it as "not JSON", which is false, and blame FMP for what is a defect in THIS SDK's
        // model. Several models document that throw as the outcome they want: "a non-numeric segment revenue
        // would be a defect worth hearing about, so the decimal dictionary is the right type and this throw
        // is the right outcome" (AsReportedTests), and CompanyMarketCap, PriceTarget and the directory lists
        // all record the same. FmpApiException has no inner-exception constructor, so wrapping would lose the
        // distinction outright. GetObjectAsync's guard makes the same cut for the same reason — it wraps
        // JsonDocument.ParseAsync and leaves RootElement.Deserialize alone. Here the peeked prefix draws the
        // line for free: a body that begins `[` is JSON, and a JsonException out of one is ours, not FMP's.
        try
        {
            return await deserialise(body, ct).ConfigureAwait(false) ?? [];
        }
        catch (JsonException ex) when (first != (byte)'[')
        {
            throw new FmpApiException(
                $"FMP answered a body that is not JSON: {ex.Message}", request.ToString());
        }
```

`System.Text.Json` is already imported at the top of the file; no using directive changes.

`FirstMeaningfulByte` returns `0` when the prefix holds nothing but whitespace or a BOM, so an empty body
takes the guard too — an empty body is not JSON either.

- [ ] **Step 3b: Write the test for the half that must keep throwing**

The filter is exactly the kind of thing a later "simplification" deletes, and nothing in the suite would
notice. Add this beside the test from Step 1:

```csharp
    [Fact]
    public async Task A_well_formed_array_with_a_field_of_the_wrong_type_still_raises_JsonException()
    {
        // The other side of the guard above, and the reason it carries a filter. This body IS JSON — it just
        // does not match the model, which makes it a defect in THIS SDK rather than a bad answer from FMP.
        // Several models say so in as many words: "a non-numeric segment revenue would be a defect worth
        // hearing about, so the decimal dictionary is the right type and this throw is the right outcome."
        // Wrapping it in FmpApiException would report "FMP answered a body that is not JSON" about a body
        // that is, blame FMP for our own modelling, and — FmpApiException taking no inner exception — throw
        // the JSON path and byte offset away with it. GetObjectAsync draws the same line: its guard wraps the
        // parse and leaves the bind alone.
        var (transport, _) = Build(StubHandler.Json("""[{"impact":7}]"""));

        await Assert.ThrowsAsync<JsonException>(
            () => transport.GetListAsync(
                new FmpRequest("stable/economic-calendar"),
                FmpJsonContext.Default.ListEconomicRelease));
    }
```

`EconomicRelease.Impact` is `string?` (`src/FmpDotNet/Models/EconomicRelease.cs:138`), so a bare `7` cannot
bind to it — and the body's first meaningful byte is `[`, so the filter declines and `System.Text.Json`'s own
exception reaches the caller unchanged. The body carries that one field and nothing else, so no other
property's converter can throw first and make the test pass for the wrong reason. Confirm the property is
still typed `string?` before running: if a later slice retyped it, pick another field on the same record whose
JSON type this fixture can violate, and name it in the comment.

- [ ] **Step 4: Run the test and the whole unit suite**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: PASS, with no other test regressing. Pay attention to `FmpTransportTests` as a whole — the error
classification tests around it assert on exception *type*, and a `catch` in the wrong place would convert an
`OperationCanceledException` from a cancelled read into an `FmpApiException`. It will not: `JsonException`
does not derive from `OperationCanceledException`, and `Assert` a cancellation test still passes.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/FmpTransport.cs tests/FmpDotNet.Tests/FmpTransportTests.cs
git commit -m "fix: raise FmpApiException when a 200 carries a body that is not JSON (#40)"
```

---

### Task 2: `EconomicIndicator`, a closed set over a case-sensitive name

**Files:**
- Create: `src/FmpDotNet/EconomicIndicator.cs`
- Create: `tests/FmpDotNet.Tests/EconomicIndicatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public enum EconomicIndicator` with 23 members in the order below, and
  `public static string ToQueryValue(this EconomicIndicator indicator)` on
  `public static class EconomicIndicatorExtensions`. Task 3 calls `ToQueryValue()` and nothing else.

**Why a type and not a `string`.** Measured 2026-08-29, `stable/economic-indicators` answers an unrecognised
`name` with **HTTP 200**, `content-type: application/json; charset=utf-8`, and twelve bytes of `Invalid name`
that are not JSON. The name is case-sensitive: `GDP` works, `gdp` does not. Without a closed type a caller who
lower-cases an indicator gets Task 1's `FmpApiException` out of a success response — better than a raw
`JsonException`, but still a runtime failure for a typo the compiler could have caught.

**Why an `enum` and not a `readonly record struct`.** See "Deviation from the spec" above. Follow
`src/FmpDotNet/ChartInterval.cs` exactly: an enum whose members are renamed, plus an extension method holding
the wire strings and a `_ => throw` arm.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/EconomicIndicatorTests.cs`:

```csharp
namespace FmpDotNet.Tests;

/// <summary>The 23 wire strings <c>stable/economic-indicators</c> accepts, pinned verbatim.
///
/// <para>The whole value of this type is that the caller cannot mistype the name, so a test that restated the
/// names loosely would guard nothing. Each of the 23 below was probed individually on 2026-08-29 and each
/// answered HTTP 200 with a well-formed array. An unrecognised name answers 200 with twelve bytes of
/// <c>Invalid name</c>, so a wrong string here does not fail loudly — it produces
/// <see cref="FmpApiException"/> at runtime for a value the compiler accepted.</para></summary>
public class EconomicIndicatorTests
{
    [Theory]
    [InlineData(EconomicIndicator.Gdp, "GDP")]
    [InlineData(EconomicIndicator.RealGdp, "realGDP")]
    [InlineData(EconomicIndicator.NominalPotentialGdp, "nominalPotentialGDP")]
    [InlineData(EconomicIndicator.RealGdpPerCapita, "realGDPPerCapita")]
    [InlineData(EconomicIndicator.FederalFunds, "federalFunds")]
    [InlineData(EconomicIndicator.ConsumerPriceIndex, "CPI")]
    [InlineData(EconomicIndicator.InflationRate, "inflationRate")]
    [InlineData(EconomicIndicator.Inflation, "inflation")]
    [InlineData(EconomicIndicator.RetailSales, "retailSales")]
    [InlineData(EconomicIndicator.ConsumerSentiment, "consumerSentiment")]
    [InlineData(EconomicIndicator.DurableGoods, "durableGoods")]
    [InlineData(EconomicIndicator.UnemploymentRate, "unemploymentRate")]
    [InlineData(EconomicIndicator.TotalNonfarmPayroll, "totalNonfarmPayroll")]
    [InlineData(EconomicIndicator.InitialClaims, "initialClaims")]
    [InlineData(EconomicIndicator.IndustrialProductionTotalIndex, "industrialProductionTotalIndex")]
    [InlineData(EconomicIndicator.NewPrivatelyOwnedHousingUnitsStartedTotalUnits,
        "newPrivatelyOwnedHousingUnitsStartedTotalUnits")]
    [InlineData(EconomicIndicator.TotalVehicleSales, "totalVehicleSales")]
    [InlineData(EconomicIndicator.RetailMoneyFunds, "retailMoneyFunds")]
    [InlineData(EconomicIndicator.SmoothedUsRecessionProbabilities, "smoothedUSRecessionProbabilities")]
    [InlineData(EconomicIndicator.ThreeMonthCertificateOfDepositRate,
        "3MonthOr90DayRatesAndYieldsCertificatesOfDeposit")]
    [InlineData(EconomicIndicator.CreditCardInterestRate,
        "commercialBankInterestRateOnCreditCardPlansAllAccounts")]
    [InlineData(EconomicIndicator.Mortgage30Year, "30YearFixedRateMortgageAverage")]
    [InlineData(EconomicIndicator.Mortgage15Year, "15YearFixedRateMortgageAverage")]
    public void Every_member_sends_the_wire_string_FMP_accepts(EconomicIndicator indicator, string wire) =>
        Assert.Equal(wire, indicator.ToQueryValue());

    [Fact]
    public void All_twenty_three_documented_names_are_covered_and_none_was_added_without_a_test()
    {
        // The Theory above is the guard; this is the guard on the Theory. A member added to the enum without
        // an InlineData row would otherwise ship untested, and the failure mode of an untested member is a
        // 200 carrying `Invalid name`.
        Assert.Equal(23, Enum.GetValues<EconomicIndicator>().Length);
    }

    [Fact]
    public void The_default_value_is_a_valid_indicator()
    {
        // default(EconomicIndicator) is ordinal 0. On an enum that is Gdp, a name measured valid; this is one
        // of the reasons the type is an enum rather than a struct wrapping the wire string, whose default
        // would be a null name.
        Assert.Equal(EconomicIndicator.Gdp, default);
        Assert.Equal("GDP", default(EconomicIndicator).ToQueryValue());
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_sending_something_plausible()
    {
        // The same guard FiscalPeriod.ToQueryValue documents, and it matters more here: an unrecognised name
        // is not rejected by FMP with a 400, it is answered with HTTP 200 and a body that is not JSON.
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => ((EconomicIndicator)999).ToQueryValue());

        Assert.Equal("indicator", thrown.ParamName);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EconomicIndicatorTests"
```

Expected: FAIL — the build breaks with `CS0246: The type or namespace name 'EconomicIndicator' could not be
found`. That is the correct failure for this step.

- [ ] **Step 3: Write the type**

Create `src/FmpDotNet/EconomicIndicator.cs`:

```csharp
namespace FmpDotNet;

/// <summary>The series asked of <see cref="Endpoints.EconomicsEndpoints.GetIndicatorAsync"/>.
///
/// <para><b>Deliberately not a string, and the reason is that the endpoint does not reject a wrong one.</b>
/// Measured 2026-08-29, <c>stable/economic-indicators?name=gdp</c> answers <b>HTTP 200</b>,
/// <c>content-type: application/json; charset=utf-8</c>, and twelve bytes of <c>Invalid name</c> that are not
/// JSON at all. The name is <b>case-sensitive</b> — <c>GDP</c> works and <c>gdp</c> does not — so the
/// difference between a working call and a failing one is one keystroke that no status code reports.</para>
///
/// <para>All 23 names FMP documents were probed individually on 2026-08-29 and all 23 answered a well-formed
/// array, so the set below is complete as measured rather than merely as documented. The members are renamed
/// from the wire — two wire names begin with a digit, and the rest carry FMP's own inconsistent casing — and
/// <see cref="EconomicIndicatorExtensions.ToQueryValue"/> holds the mapping.</para>
///
/// <para><b>Two members return an empty array rather than rows</b>, measured the same day:
/// <see cref="Inflation"/> and <see cref="ThreeMonthCertificateOfDepositRate"/>. They are valid names carrying
/// no data, not invalid names, and they are kept for that reason — dropping them would leave a caller unable
/// to ask, and unable to tell "FMP has no data" from "this SDK omitted it".</para>
///
/// <para><b>The whole endpoint is stale.</b> Measured 2026-08-29, the newest row on every one of the 21
/// non-empty series is dated between 2025-10-01 and 2025-11-26 — nine months earlier. A caller asking for a
/// window computed from today gets an empty array with HTTP 200. See
/// <see cref="Endpoints.EconomicsEndpoints.GetIndicatorAsync"/>.</para></summary>
public enum EconomicIndicator
{
    /// <summary>Nominal gross domestic product, quarterly — wire <c>GDP</c>. Newest row measured 2026-08-29:
    /// 2025-10-01.</summary>
    Gdp,

    /// <summary>Inflation-adjusted gross domestic product, quarterly — wire <c>realGDP</c>.</summary>
    RealGdp,

    /// <summary>Nominal potential gross domestic product, quarterly — wire
    /// <c>nominalPotentialGDP</c>.</summary>
    NominalPotentialGdp,

    /// <summary>Real gross domestic product per head, quarterly — wire <c>realGDPPerCapita</c>.</summary>
    RealGdpPerCapita,

    /// <summary>The effective federal funds rate, monthly — wire <c>federalFunds</c>.</summary>
    FederalFunds,

    /// <summary>The consumer price index, monthly — wire <c>CPI</c>, which is the one uppercase name FMP does
    /// not decorate further.</summary>
    ConsumerPriceIndex,

    /// <summary>The rate of change in consumer prices — wire <c>inflationRate</c>. Not the same series as
    /// <see cref="Inflation"/>, which carries no rows at all.</summary>
    InflationRate,

    /// <summary>Wire <c>inflation</c>. <b>Answers a well-formed empty array</b>, measured 2026-08-29 — a
    /// valid name with no data behind it. <see cref="InflationRate"/> is the series a caller almost certainly
    /// wants.</summary>
    Inflation,

    /// <summary>Retail and food-services sales, monthly — wire <c>retailSales</c>.</summary>
    RetailSales,

    /// <summary>The University of Michigan consumer sentiment index, monthly — wire
    /// <c>consumerSentiment</c>.</summary>
    ConsumerSentiment,

    /// <summary>New orders for durable goods, monthly — wire <c>durableGoods</c>.</summary>
    DurableGoods,

    /// <summary>The headline unemployment rate, monthly — wire <c>unemploymentRate</c>.</summary>
    UnemploymentRate,

    /// <summary>Total non-farm payroll employment, monthly — wire <c>totalNonfarmPayroll</c>.</summary>
    TotalNonfarmPayroll,

    /// <summary>Initial jobless claims, weekly — wire <c>initialClaims</c>.</summary>
    InitialClaims,

    /// <summary>Industrial production, total index, monthly — wire
    /// <c>industrialProductionTotalIndex</c>.</summary>
    IndustrialProductionTotalIndex,

    /// <summary>Housing starts, total units, monthly — wire
    /// <c>newPrivatelyOwnedHousingUnitsStartedTotalUnits</c>.</summary>
    NewPrivatelyOwnedHousingUnitsStartedTotalUnits,

    /// <summary>Total vehicle sales, monthly — wire <c>totalVehicleSales</c>.</summary>
    TotalVehicleSales,

    /// <summary>Retail money funds, monthly — wire <c>retailMoneyFunds</c>.</summary>
    RetailMoneyFunds,

    /// <summary>Smoothed US recession probabilities, monthly — wire
    /// <c>smoothedUSRecessionProbabilities</c>.</summary>
    SmoothedUsRecessionProbabilities,

    /// <summary>Three-month certificate-of-deposit rates — wire
    /// <c>3MonthOr90DayRatesAndYieldsCertificatesOfDeposit</c>, which begins with a digit and so cannot be a
    /// C# identifier.
    ///
    /// <para><b>Answers a well-formed empty array</b>, measured 2026-08-29 — like
    /// <see cref="Inflation"/>, a valid name with no data behind it.</para></summary>
    ThreeMonthCertificateOfDepositRate,

    /// <summary>The average interest rate on credit card plans at commercial banks — wire
    /// <c>commercialBankInterestRateOnCreditCardPlansAllAccounts</c>.</summary>
    CreditCardInterestRate,

    /// <summary>The 30-year fixed-rate mortgage average, weekly — wire
    /// <c>30YearFixedRateMortgageAverage</c>, which begins with a digit.</summary>
    Mortgage30Year,

    /// <summary>The 15-year fixed-rate mortgage average, weekly — wire
    /// <c>15YearFixedRateMortgageAverage</c>, which begins with a digit.</summary>
    Mortgage15Year,
}

/// <summary>Conversions for <see cref="EconomicIndicator"/>.</summary>
public static class EconomicIndicatorExtensions
{
    /// <summary>The value FMP expects in the <c>name=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible, and the reason is
    /// sharper here than on <see cref="FiscalPeriod"/>: an unrecognised <c>name</c> is not answered with a
    /// 400. Measured 2026-08-29 it answers <b>HTTP 200</b> and twelve bytes of <c>Invalid name</c>, which is
    /// not JSON — so a value that escaped this method would surface as a deserialisation failure in the
    /// transport rather than as an argument error at the call site.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this EconomicIndicator indicator) => indicator switch
    {
        EconomicIndicator.Gdp => "GDP",
        EconomicIndicator.RealGdp => "realGDP",
        EconomicIndicator.NominalPotentialGdp => "nominalPotentialGDP",
        EconomicIndicator.RealGdpPerCapita => "realGDPPerCapita",
        EconomicIndicator.FederalFunds => "federalFunds",
        EconomicIndicator.ConsumerPriceIndex => "CPI",
        EconomicIndicator.InflationRate => "inflationRate",
        EconomicIndicator.Inflation => "inflation",
        EconomicIndicator.RetailSales => "retailSales",
        EconomicIndicator.ConsumerSentiment => "consumerSentiment",
        EconomicIndicator.DurableGoods => "durableGoods",
        EconomicIndicator.UnemploymentRate => "unemploymentRate",
        EconomicIndicator.TotalNonfarmPayroll => "totalNonfarmPayroll",
        EconomicIndicator.InitialClaims => "initialClaims",
        EconomicIndicator.IndustrialProductionTotalIndex => "industrialProductionTotalIndex",
        EconomicIndicator.NewPrivatelyOwnedHousingUnitsStartedTotalUnits
            => "newPrivatelyOwnedHousingUnitsStartedTotalUnits",
        EconomicIndicator.TotalVehicleSales => "totalVehicleSales",
        EconomicIndicator.RetailMoneyFunds => "retailMoneyFunds",
        EconomicIndicator.SmoothedUsRecessionProbabilities => "smoothedUSRecessionProbabilities",
        EconomicIndicator.ThreeMonthCertificateOfDepositRate
            => "3MonthOr90DayRatesAndYieldsCertificatesOfDeposit",
        EconomicIndicator.CreditCardInterestRate
            => "commercialBankInterestRateOnCreditCardPlansAllAccounts",
        EconomicIndicator.Mortgage30Year => "30YearFixedRateMortgageAverage",
        EconomicIndicator.Mortgage15Year => "15YearFixedRateMortgageAverage",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known economic indicator."),
    };
}
```

**Note on the `<see cref="Endpoints.EconomicsEndpoints.GetIndicatorAsync"/>` references above.** They point at
a method Task 3 creates, and `TreatWarningsAsErrors` turns an unresolvable cref into CS1574 — a **build
error**. Write the file with both of those crefs rendered as plain `<c>GetIndicatorAsync</c>` in this task, and
promote them to real crefs in Task 3 Step 12, which is where the method exists. Do not skip that step: the
cross-reference is what makes the staleness note reachable from the method a caller is actually looking at.

- [ ] **Step 4: Run the tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EconomicIndicatorTests"
```

Expected: PASS, 26 tests (23 Theory rows plus 3 Facts).

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/EconomicIndicator.cs tests/FmpDotNet.Tests/EconomicIndicatorTests.cs
git commit -m "feat: add EconomicIndicator, the 23 names economic-indicators accepts (#40)"
```

---

### Task 3: the three Economics records and the three methods that answer them

**Files:**
- Create: `src/FmpDotNet/Models/EconomicIndicators.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/economic-indicators.federalFunds.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/market-risk-premium.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/treasury-rates.head.json`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs`
- Modify: `src/FmpDotNet/EconomicIndicator.cs` (promote three crefs)
- Modify: `tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs`
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `README.md` (generated block only)

**Interfaces:**
- Consumes: `EconomicIndicator` and `EconomicIndicatorExtensions.ToQueryValue()` from Task 2.
- Produces: `EconomicObservation`, `MarketRiskPremium`, `TreasuryRate`, their three
  `FmpJsonContext.Default.ListX` entries, and on `EconomicsEndpoints`:
  - `Task<IReadOnlyList<EconomicObservation>> GetIndicatorAsync(EconomicIndicator indicator, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)`
  - `Task<IReadOnlyList<MarketRiskPremium>> GetMarketRiskPremiumsAsync(CancellationToken ct = default)`
  - `Task<IReadOnlyList<TreasuryRate>> GetTreasuryRatesAsync(LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)`

  Also `LiveApi.IndicatorRangeStart` and `LiveApi.IndicatorRangeEnd` (`LocalDate`), used only by `Probe`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/economic-indicators.federalFunds.json` — captured 2026-08-29 from
`stable/economic-indicators?name=federalFunds`. Three rows rather than GDP's one, so the test proves the
converter on more than a single date:

```json
[
  {
    "name": "federalFunds",
    "date": "2025-11-01",
    "value": 3.88
  },
  {
    "name": "federalFunds",
    "date": "2025-10-01",
    "value": 4.09
  },
  {
    "name": "federalFunds",
    "date": "2025-09-01",
    "value": 4.22
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/market-risk-premium.head.json` — the first three rows of the 192 the bare call
returned on 2026-08-29. They come back reverse-alphabetically, which is worth capturing rather than
smoothing:

```json
[
  {
    "country": "Zimbabwe",
    "continent": "Africa",
    "countryRiskPremium": 11.66,
    "totalEquityRiskPremium": 15.89
  },
  {
    "country": "Zambia",
    "continent": "Africa",
    "countryRiskPremium": 11.66,
    "totalEquityRiskPremium": 15.89
  },
  {
    "country": "Yemen, Republic",
    "continent": "Asia",
    "countryRiskPremium": 15.54,
    "totalEquityRiskPremium": 19.77
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/treasury-rates.head.json` — the first three rows of the bare call on
2026-08-29, newest first. All twelve tenors carry a value on every row:

```json
[
  {
    "date": "2026-08-27",
    "month1": 3.81,
    "month2": 3.81,
    "month3": 3.84,
    "month6": 3.94,
    "year1": 4.04,
    "year2": 4.2,
    "year3": 4.3,
    "year5": 4.38,
    "year7": 4.52,
    "year10": 4.67,
    "year20": 5.18,
    "year30": 5.19
  },
  {
    "date": "2026-08-26",
    "month1": 3.8,
    "month2": 3.8,
    "month3": 3.85,
    "month6": 3.94,
    "year1": 4.02,
    "year2": 4.19,
    "year3": 4.29,
    "year5": 4.37,
    "year7": 4.51,
    "year10": 4.66,
    "year20": 5.17,
    "year30": 5.18
  },
  {
    "date": "2026-08-25",
    "month1": 3.79,
    "month2": 3.8,
    "month3": 3.86,
    "month6": 3.95,
    "year1": 4.01,
    "year2": 4.17,
    "year3": 4.25,
    "year5": 4.35,
    "year7": 4.48,
    "year10": 4.64,
    "year20": 5.16,
    "year30": 5.17
  }
]
```

The `Fixtures\*.json` glob in `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj` already copies these to the
output directory; no csproj change.

- [ ] **Step 2: Write the failing binding test**

Append to `tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs`, inside the existing class:

```csharp
    // ---- the three paths added in #40 --------------------------------------------------------------------

    [Fact]
    public void An_indicator_observation_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Fixture("economic-indicators.federalFunds.json"),
            FmpJsonContext.Default.ListEconomicObservation)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("federalFunds", rows[0].Name);
        Assert.Equal(new LocalDate(2025, 11, 1), rows[0].Date);
        Assert.Equal(3.88m, rows[0].Value);
        Assert.Equal(new LocalDate(2025, 9, 1), rows[2].Date);
    }

    [Fact]
    public void A_market_risk_premium_binds_all_four_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Fixture("market-risk-premium.head.json"),
            FmpJsonContext.Default.ListMarketRiskPremium)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Zimbabwe", rows[0].Country);
        Assert.Equal("Africa", rows[0].Continent);
        Assert.Equal(11.66m, rows[0].CountryRiskPremium);
        Assert.Equal(15.89m, rows[0].TotalEquityRiskPremium);

        // A country name carrying a comma. Nothing splits on one, and this is the row that proves it.
        Assert.Equal("Yemen, Republic", rows[2].Country);
    }

    [Fact]
    public void A_treasury_row_binds_the_date_and_all_twelve_tenors()
    {
        // Twelve tenors and all of them decimal?. Asserting the whole set rather than a spot-check, because
        // every one is a bare number under a name that differs from the C# property only in casing — the
        // exact shape in which a dropped [JsonPropertyName] costs nothing that throws.
        var rows = JsonSerializer.Deserialize(
            Fixture("treasury-rates.head.json"),
            FmpJsonContext.Default.ListTreasuryRate)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].Date);
        Assert.Equal(3.81m, rows[0].Month1);
        Assert.Equal(3.81m, rows[0].Month2);
        Assert.Equal(3.84m, rows[0].Month3);
        Assert.Equal(3.94m, rows[0].Month6);
        Assert.Equal(4.04m, rows[0].Year1);
        Assert.Equal(4.2m, rows[0].Year2);
        Assert.Equal(4.3m, rows[0].Year3);
        Assert.Equal(4.38m, rows[0].Year5);
        Assert.Equal(4.52m, rows[0].Year7);
        Assert.Equal(4.67m, rows[0].Year10);
        Assert.Equal(5.18m, rows[0].Year20);
        Assert.Equal(5.19m, rows[0].Year30);
    }
```

- [ ] **Step 3: Run it and watch it fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EconomicsEndpointsTests"
```

Expected: FAIL — `CS0246` on `EconomicObservation`, `MarketRiskPremium` and `TreasuryRate`.

- [ ] **Step 4: Write the three records**

**One deferred cref.** `TreasuryRate`'s type summary below ends with
`<see cref="Endpoints.EconomicsEndpoints.GetTreasuryRatesAsync"/>`, and that method does not exist until
Step 11. Write it as plain `<c>GetTreasuryRatesAsync</c>` now — Step 12 promotes it along with Task 2's
three. Leaving it as a live cref makes Step 6's build fail with CS1574.

Create `src/FmpDotNet/Models/EconomicIndicators.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One observation of one macroeconomic series. From <c>stable/economic-indicators</c>.
///
/// <para>The narrowest record in the SDK, and deliberately so: the endpoint answers a name, a date and a
/// number, and nothing about which series a row belongs to is carried anywhere except
/// <see cref="Name"/>.</para>
///
/// <para><see cref="Name"/> is the wire spelling of the
/// <see cref="EconomicIndicator"/> that was asked for — <c>federalFunds</c>, <c>CPI</c>,
/// <c>30YearFixedRateMortgageAverage</c>. It is not mapped back to the enum, because a value FMP invented
/// after this SDK shipped has no member to map to and would have to be discarded or guessed
/// at.</para></summary>
public sealed record EconomicObservation
{
    /// <summary>The series this row belongs to, spelled as FMP spells it — the same string
    /// <see cref="EconomicIndicatorExtensions.ToQueryValue"/> sent.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The observation date. Monthly series are dated to the first of the month, quarterly series to
    /// the first day of the quarter — measured 2026-08-29, <c>GDP</c> answers <c>2025-10-01</c> for Q4.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The observation. Units are the series' own and are not carried on the row — <c>GDP</c> is
    /// billions of dollars, <c>federalFunds</c> is a percentage, <c>CPI</c> is an index.</summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }
}

/// <summary>One country's equity risk premium. From <c>stable/market-risk-premium</c>.
///
/// <para>The whole response is 192 rows, measured 2026-08-29, returned reverse-alphabetically by country.
/// There is no query surface at all — no country parameter, no date parameter — so this is a full download or
/// nothing.</para></summary>
public sealed record MarketRiskPremium
{
    /// <summary>The country, as FMP names it. Non-empty on all 192 rows measured 2026-08-29; nullable
    /// because every string on every model in this SDK is.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>The continent, as FMP groups it. Non-empty on all 192 rows measured 2026-08-29.</summary>
    [JsonPropertyName("continent")] public string? Continent { get; init; }

    /// <summary>The premium attributable to country risk alone, as a percentage.</summary>
    [JsonPropertyName("countryRiskPremium")] public decimal? CountryRiskPremium { get; init; }

    /// <summary>The total equity risk premium — the mature-market premium plus
    /// <see cref="CountryRiskPremium"/>, as a percentage.</summary>
    [JsonPropertyName("totalEquityRiskPremium")] public decimal? TotalEquityRiskPremium { get; init; }
}

/// <summary>One day's US Treasury yield curve, twelve tenors wide. From <c>stable/treasury-rates</c>.
///
/// <para>Every tenor is a percentage and every one is <see langword="decimal"/>. The property names are the
/// tenors: <see cref="Month1"/> through <see cref="Month6"/>, then <see cref="Year1"/> through
/// <see cref="Year30"/>. All twelve carried a value on every row measured 2026-08-29.</para>
///
/// <para><b>This is the one path in issue #40's group whose data is current.</b> Measured 2026-08-29 the bare
/// call answered 2026-05-29 through 2026-08-27; the indicator, ESG-benchmark and COT paths beside it are all
/// months or years stale. See <see cref="Endpoints.EconomicsEndpoints.GetTreasuryRatesAsync"/>.</para></summary>
public sealed record TreasuryRate
{
    /// <summary>The trading day this curve was observed on. Weekends and holidays are absent rather than
    /// repeated.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>One-month yield, as a percentage.</summary>
    [JsonPropertyName("month1")] public decimal? Month1 { get; init; }

    /// <summary>Two-month yield, as a percentage.</summary>
    [JsonPropertyName("month2")] public decimal? Month2 { get; init; }

    /// <summary>Three-month yield, as a percentage.</summary>
    [JsonPropertyName("month3")] public decimal? Month3 { get; init; }

    /// <summary>Six-month yield, as a percentage.</summary>
    [JsonPropertyName("month6")] public decimal? Month6 { get; init; }

    /// <summary>One-year yield, as a percentage.</summary>
    [JsonPropertyName("year1")] public decimal? Year1 { get; init; }

    /// <summary>Two-year yield, as a percentage.</summary>
    [JsonPropertyName("year2")] public decimal? Year2 { get; init; }

    /// <summary>Three-year yield, as a percentage.</summary>
    [JsonPropertyName("year3")] public decimal? Year3 { get; init; }

    /// <summary>Five-year yield, as a percentage.</summary>
    [JsonPropertyName("year5")] public decimal? Year5 { get; init; }

    /// <summary>Seven-year yield, as a percentage.</summary>
    [JsonPropertyName("year7")] public decimal? Year7 { get; init; }

    /// <summary>Ten-year yield, as a percentage.</summary>
    [JsonPropertyName("year10")] public decimal? Year10 { get; init; }

    /// <summary>Twenty-year yield, as a percentage.</summary>
    [JsonPropertyName("year20")] public decimal? Year20 { get; init; }

    /// <summary>Thirty-year yield, as a percentage.</summary>
    [JsonPropertyName("year30")] public decimal? Year30 { get; init; }
}
```

- [ ] **Step 5: Register the three records**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, immediately before the
`// Congressional disclosures (#31).` comment block, add:

```csharp
// Economics, transcripts, ESG and COT (#40). Twelve records across twelve paths and four unrelated groups;
// see docs/superpowers/specs/2026-08-29-economics-transcripts-esg-and-cot-design.md.
[JsonSerializable(typeof(List<EconomicObservation>))]
[JsonSerializable(typeof(List<MarketRiskPremium>))]
[JsonSerializable(typeof(List<TreasuryRate>))]
```

Tasks 4, 5 and 6 append their entries under this same comment.

- [ ] **Step 6: Run the binding tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EconomicsEndpointsTests"
```

Expected: PASS.

- [ ] **Step 7: Commit the records**

```bash
git add src/FmpDotNet/Models/EconomicIndicators.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/Fixtures/economic-indicators.federalFunds.json \
        tests/FmpDotNet.Tests/Fixtures/market-risk-premium.head.json \
        tests/FmpDotNet.Tests/Fixtures/treasury-rates.head.json \
        tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs
git commit -m "feat: add EconomicObservation, MarketRiskPremium and TreasuryRate (#40)"
```

- [ ] **Step 8: Write the failing request-surface tests**

Append to `tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs`:

```csharp
    [Fact]
    public async Task The_indicator_name_goes_out_as_the_wire_string_and_never_as_the_member_name()
    {
        // The trap this whole enum exists for. Measured 2026-08-29, `name=gdp` answers HTTP 200 with twelve
        // bytes of `Invalid name` rather than an error status — so a member name reaching the wire is a
        // failure that looks like a success until the transport tries to parse it.
        var (endpoints, handler) = Build();

        await endpoints.GetIndicatorAsync(EconomicIndicator.SmoothedUsRecessionProbabilities);

        Assert.Equal("/stable/economic-indicators", handler.Requests[0].AbsolutePath);
        Assert.Contains("name=smoothedUSRecessionProbabilities", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_indicator_range_is_optional_at_both_ends()
    {
        // Both ends optional and both omitted from the query when null — not sent as empty. FmpRequest.With
        // drops a null, and this pins that the method relies on it rather than formatting "".
        var (endpoints, handler) = Build();

        await endpoints.GetIndicatorAsync(EconomicIndicator.Gdp);

        Assert.DoesNotContain("from=", handler.Requests[0].Query);
        Assert.DoesNotContain("to=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_indicator_range_is_sent_in_FMPs_date_form_when_supplied()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIndicatorAsync(
            EconomicIndicator.Gdp, new LocalDate(2025, 9, 1), new LocalDate(2025, 11, 30));

        Assert.Contains("from=2025-09-01", handler.Requests[0].Query);
        Assert.Contains("to=2025-11-30", handler.Requests[0].Query);
    }

    [Fact]
    public async Task No_limit_parameter_is_ever_sent_to_the_indicator_path()
    {
        // Measured 2026-08-29: `name=CPI&limit=100` returns the same 2 rows as `name=CPI`, byte-identical.
        // The parameter is accepted and discarded, so offering it would promise filtering FMP does not do —
        // the same class of defect as the `-by-id` trap closed in #31.
        var (endpoints, handler) = Build();

        await endpoints.GetIndicatorAsync(EconomicIndicator.ConsumerPriceIndex);

        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_backwards_range_is_refused_before_the_request_goes_out(bool treasury)
    {
        // FMP answers a backwards range rather than reporting one. Both new date-ranged methods take the same
        // house guard the calendar already takes.
        var (endpoints, handler) = Build();
        var from = new LocalDate(2025, 11, 30);
        var to = new LocalDate(2025, 9, 1);

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => treasury
                ? endpoints.GetTreasuryRatesAsync(from, to)
                : endpoints.GetIndicatorAsync(EconomicIndicator.Gdp, from, to));

        Assert.Equal("to", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_two_parameterless_paths_are_requested_where_they_live()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetMarketRiskPremiumsAsync();

        var (treasury, treasuryHandler) = Build();
        await treasury.GetTreasuryRatesAsync();

        Assert.Equal("/stable/market-risk-premium", handler.Requests[0].AbsolutePath);
        Assert.Equal("", handler.Requests[0].Query.Replace("?apikey=k", ""));
        Assert.Equal("/stable/treasury-rates", treasuryHandler.Requests[0].AbsolutePath);
        // Asserted on both, not just the first: `treasury-rates` takes an optional range, so this is also the
        // guard that a null `from`/`to` stays off the wire rather than going out empty.
        Assert.Equal("", treasuryHandler.Requests[0].Query.Replace("?apikey=k", ""));
    }
```

`Build()` in this file already defaults its body to `"[]"`, and `StubHandler` replays its last response for
every further call, so `Build()` with no argument serves each of these single-call tests.

- [ ] **Step 9: Run them and watch them fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EconomicsEndpointsTests"
```

Expected: FAIL — `CS1061`, `EconomicsEndpoints` does not contain a definition for `GetIndicatorAsync`.

- [ ] **Step 10: Rewrite the facade's type summary**

In `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs`, replace the type-level doc comment (lines 7–11) — it
currently promises only a calendar, and that stops being true here:

```csharp
/// <summary>FMP's macroeconomic surface — the release calendar, indicator series, the Treasury yield curve and
/// country equity risk premia.
///
/// <para>Unlike the company endpoints, nothing here is keyed on a symbol. Every path is <b>global</b>: a
/// request is a date range, an indicator name, or nothing at all, and the answer covers every country FMP
/// tracks. Narrowing that is the caller's job, not the SDK's.</para>
///
/// <para><b>Three of the four paths silently return less than they were asked for, in three different
/// ways.</b> <see cref="GetEconomicCalendarAsync"/> truncates a wide window to fewer rows than the narrow
/// window inside it. <see cref="GetTreasuryRatesAsync"/> truncates to about three months, keeping the newest.
/// <see cref="GetIndicatorAsync"/> answers an empty array for windows the data does not cover — and, measured
/// 2026-08-29, the data covers nothing after 2025-11-26. Each method documents its own case; none of them is
/// guarded by a row count, for the reason <see cref="GetEconomicCalendarAsync"/> sets out.</para>
///
/// <para>Only <see cref="GetTreasuryRatesAsync"/> answered current data on 2026-08-29.
/// <see cref="GetMarketRiskPremiumsAsync"/> carries no dates at all, so its currency cannot be
/// checked.</para></summary>
```

- [ ] **Step 11: Add the three methods**

Append to the body of `EconomicsEndpoints`, after `GetEconomicCalendarAsync`:

```csharp
    /// <summary>One macroeconomic series, oldest observation last — <c>stable/economic-indicators</c>.
    ///
    /// <para><b>Read this before choosing a range, because the obvious range returns nothing.</b> Measured
    /// 2026-08-29, every one of the 21 series that carries data stops between 2025-10-01 and 2025-11-26 —
    /// about nine months before that date. A window computed from today therefore answers a well-formed
    /// <b>empty array</b> with HTTP 200: <c>name=GDP&amp;from=2026-05-23&amp;to=2026-08-21</c> returned no
    /// rows, while <c>from=2025-09-01&amp;to=2025-11-30</c> returned one. Nothing in the response says the
    /// window was outside the data.</para>
    ///
    /// <para><b>Widening the window can return fewer rows, and no width rule predicts it.</b> Measured
    /// 2026-08-29 on <c>name=GDP</c>: a 90-day window answered 1 row, the 183-day window containing it
    /// answered <b>0</b>, and a 335-day window answered 1. A ~90-day range over a span the data actually
    /// covers is the only shape measured to work every time; anything wider is worth checking rather than
    /// trusting.</para>
    ///
    /// <para><b>The check is positional, not a row count</b>, for the reason
    /// <see cref="GetEconomicCalendarAsync"/> sets out at length: these series are legitimately sparse —
    /// <see cref="EconomicIndicator.Gdp"/> is quarterly and a correct answer for a quarter is one row — so a
    /// threshold rejects real answers while accepting truncated ones. Compare
    /// <see cref="EconomicObservation.Date"/> against the range you asked for.</para>
    ///
    /// <para><b>Omitting the range is a different query, not a wider one.</b> Measured 2026-08-29 the bare
    /// call answered the newest ~3 months of the series — 61 rows on <c>inflationRate</c>, 1 on
    /// <c>GDP</c> — which is usually what a caller wants and is what the live smoke sweep would use if it
    /// could.</para>
    ///
    /// <para><b>No <c>limit</c> parameter, because FMP ignores it.</b> Measured 2026-08-29,
    /// <c>name=CPI&amp;limit=100</c> answered the same 2 rows as <c>name=CPI</c>, byte-identical.</para>
    ///
    /// <para>Two indicators answer an empty array on every call — see <see cref="EconomicIndicator.Inflation"/>
    /// and <see cref="EconomicIndicator.ThreeMonthCertificateOfDepositRate"/>. They are valid names with no
    /// data behind them.</para></summary>
    /// <param name="indicator">The series. An <see cref="EconomicIndicator"/> rather than a string because
    /// the name is case-sensitive and a wrong one answers HTTP 200 with a body that is not JSON.</param>
    /// <param name="from">First day of the range, inclusive. Omit for the newest ~3 months.</param>
    /// <param name="to">Last day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The observations in the range, or an empty list — never null. An empty list means the window
    /// falls outside the data at least as often as it means the series is quiet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="indicator"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered 200 with a body that is not JSON, which is how it
    /// reports an unrecognised name.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EconomicObservation>> GetIndicatorAsync(
        EconomicIndicator indicator, LocalDate? from = null, LocalDate? to = null,
        CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/economic-indicators")
                .With("name", indicator.ToQueryValue()).With("from", from).With("to", to),
            FmpJsonContext.Default.ListEconomicObservation, ct);
    }

    /// <summary>Every country's equity risk premium — <c>stable/market-risk-premium</c>.
    ///
    /// <para>A full download with no query surface: no country parameter, no date parameter, no paging.
    /// Measured 2026-08-29 it answered <b>192 rows</b>, reverse-alphabetically by country, with all four
    /// fields populated on every one.</para>
    ///
    /// <para><b>The rows carry no date.</b> There is no way to tell from a response when these premia were
    /// computed, and no historical series is offered, so this cannot be checked for staleness the way the
    /// dated paths on this facade can.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every country FMP publishes a premium for. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketRiskPremium>> GetMarketRiskPremiumsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/market-risk-premium"),
            FmpJsonContext.Default.ListMarketRiskPremium, ct);

    /// <summary>The US Treasury yield curve day by day, newest first — <c>stable/treasury-rates</c>.
    ///
    /// <para><b>Truncates to about three months, keeping the newest, and reports nothing.</b> Measured
    /// 2024 data on 2026-08-29: a one-month range answered 21 rows complete, a three-month range answered 61
    /// complete, and a <b>two-year</b> range answered 61 rows spanning only 2024-10-02 to 2024-12-31 — 21
    /// months silently missing under HTTP 200 and a well-formed array. A 90-day range measured the same day
    /// answered 62 rows, complete, which is how the limit is known to be a window rather than a row count: 61
    /// is simply the number of trading days in those two spans.</para>
    ///
    /// <para><b>Chunk by quarter and the SDK will not do it for you</b>, for the reason
    /// <see cref="GetEconomicCalendarAsync"/> sets out: this endpoint is dense and regular, so a row-count
    /// guard would work here and would still be the wrong shape to teach, since the sibling paths on this
    /// facade are sparse and it would be wrong on those. The honest check is the same one everywhere — did
    /// <see cref="TreasuryRate.Date"/> reach both ends of the range you asked for?</para>
    ///
    /// <para><b>The one current path in issue #40's group.</b> Measured 2026-08-29 the bare call answered
    /// 2026-05-29 through 2026-08-27.</para></summary>
    /// <param name="from">First day of the range, inclusive. Omit for the newest ~3 months.</param>
    /// <param name="to">Last day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, truncated to about three months. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<TreasuryRate>> GetTreasuryRatesAsync(
        LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/treasury-rates").With("from", from).With("to", to),
            FmpJsonContext.Default.ListTreasuryRate, ct);
    }
```

- [ ] **Step 12: Promote the three deferred crefs**

All three methods now exist, so the placeholders left by Task 2 Step 3 and Step 4 above become real
cross-references. Grep for the placeholder rather than counting by eye —
`grep -rn '<c>GetIndicatorAsync</c>\|<c>GetTreasuryRatesAsync</c>' src/` — and promote every hit:

- `src/FmpDotNet/EconomicIndicator.cs` — two occurrences of `<c>GetIndicatorAsync</c>` (the type-level summary
  and the staleness paragraph) → `<see cref="Endpoints.EconomicsEndpoints.GetIndicatorAsync"/>`
- `src/FmpDotNet/Models/EconomicIndicators.cs`, `TreasuryRate`'s type summary — one occurrence of
  `<c>GetTreasuryRatesAsync</c>` → `<see cref="Endpoints.EconomicsEndpoints.GetTreasuryRatesAsync"/>`

A missed one is not a silent defect — it is just a weaker doc — but a *wrong* one is CS1574 and fails the
build, so verify with `dotnet build src/FmpDotNet -warnaserror` rather than by eye.

- [ ] **Step 13: Run the unit suite**

```bash
dotnet test tests/FmpDotNet.Tests
```

Expected: every `EconomicsEndpointsTests` case passes, and
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`
**FAILS** with three new rows in its "expected" block. That failure is correct and Step 15 clears it.

- [ ] **Step 14: Teach the live sweep to ask a question worth answering**

`SweepCoverageTests` runs **without a key** and will now fail: `Probe.Argument` has never seen an
`EconomicIndicator`. Two edits.

First, in `tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `CalendarWeekStart`:

```csharp
    /// <summary>The window <c>GetIndicatorAsync</c> is probed over — <b>fixed dates, deliberately</b>, and
    /// the only fixed date range in this file.
    ///
    /// <para>Every other range here is relative, because <see cref="SettledWeekday"/> records that "a
    /// hard-coded date is a smoke suite with an expiry". <b>On this endpoint the reasoning inverts: the data
    /// is what is frozen.</b> Measured 2026-08-29, every one of the 21 <see cref="EconomicIndicator"/> series
    /// that carries data stops between 2025-10-01 and 2025-11-26, and
    /// <c>name=GDP&amp;from=2026-05-23&amp;to=2026-08-21</c> — precisely the window
    /// <see cref="RangeStart"/> and <see cref="SettledWeekday"/> produce — answered a well-formed
    /// <b>empty array</b> at HTTP 200. A relative window records <c>outcome empty</c> on the day it is
    /// written and matches that baseline green forever, which is the failure
    /// <see cref="Exchange"/>, <see cref="Cik"/> and <see cref="FilerCik"/> were each named for.</para>
    ///
    /// <para>2025-09-01 … 2025-11-30 is ninety days and answered 1 row for <c>GDP</c>, 2 for <c>CPI</c> and 3
    /// for <c>federalFunds</c>. Ninety days rather than wider because width does not behave monotonically
    /// here — the 183-day window containing this one answered nothing, measured the same day.</para>
    ///
    /// <para><b>If this probe starts recording <c>outcome empty</c>, FMP has moved its data</b>, and the fix
    /// is to re-measure the series' extent and move this window — not to widen it.</para></summary>
    public static readonly LocalDate IndicatorRangeStart = new(2025, 9, 1);

    /// <summary>The end of <see cref="IndicatorRangeStart"/>'s window.</summary>
    public static readonly LocalDate IndicatorRangeEnd = new(2025, 11, 30);
```

Second, in `tests/FmpDotNet.SmokeTests/Probe.cs`, inside `Argument`. Add the enum arm beside the
`ChartInterval` one:

```csharp
        // Gdp and not Inflation or ThreeMonthCertificateOfDepositRate: those two are valid names that answer
        // a well-formed EMPTY array (measured 2026-08-29), so probing with either would record `outcome
        // empty` as this endpoint's healthy baseline.
        if (type == typeof(EconomicIndicator)) return EconomicIndicator.Gdp;
```

Then, in the `LocalDate` switch, **replace** the existing economics arm

```csharp
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    => LiveApi.SettledWeekday,
```

with four arms. The order matters: the two `GetIndicatorAsync` arms must precede the calendar arm, and all
three must precede the general `"from"` fallback.

```csharp
                // The indicator series are frozen in late 2025, so this is the one range in the sweep that is
                // FIXED rather than relative — see LiveApi.IndicatorRangeStart. The relative window every
                // other date-ranged probe uses answers an empty array here.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    && parameter.Member.Name == nameof(Endpoints.EconomicsEndpoints.GetIndicatorAsync)
                    => LiveApi.IndicatorRangeStart,
                "to" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    && parameter.Member.Name == nameof(Endpoints.EconomicsEndpoints.GetIndicatorAsync)
                    => LiveApi.IndicatorRangeEnd,

                // The economic calendar's own doc: "the widest range verified intact here is one week", after
                // a 6-month window returned FEWER rows than the 3-month window it contains and a
                // -3-to-+12-month window returned 0. A week sits exactly on that boundary with no margin, so
                // it keeps the day. NARROWED to this one method in #40: GetTreasuryRatesAsync and
                // GetIndicatorAsync joined this facade, and a single-day window answers 1 row on the first
                // and none at all on the second.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.EconomicsEndpoints)
                    && parameter.Member.Name == nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync)
                    => LiveApi.SettledWeekday,
```

`GetTreasuryRatesAsync` then falls through to the general `"from" => LiveApi.RangeStart` and
`_ => LiveApi.SettledWeekday`, which is correct: that 90-day window was measured on 2026-08-29 to answer 62
complete rows.

- [ ] **Step 15: Regenerate the README block and run everything**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
dotnet test
```

Expected: the whole suite green, and `git diff README.md` showing exactly three new rows under
`fmp.Economics` plus the count moving from 166 to 169. The prose below the generated block still says 166 and
77 remaining — **leave it**; Task 8 updates the prose once, against the final number.

- [ ] **Step 16: Commit**

```bash
# EconomicIndicators.cs is here because Step 12 promoted TreasuryRate's cref in it, several steps after
# Step 8 committed the file. A promotion step always dirties a file an earlier commit already took.
git add src/FmpDotNet/Endpoints/EconomicsEndpoints.cs src/FmpDotNet/EconomicIndicator.cs \
        src/FmpDotNet/Models/EconomicIndicators.cs \
        tests/FmpDotNet.Tests/EconomicsEndpointsTests.cs \
        tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs README.md
git commit -m "feat: add the three remaining Economics paths to fmp.Economics (#40)"
```

---

### Task 4: `fmp.Transcripts`

**Files:**
- Create: `src/FmpDotNet/Models/EarningsTranscript.cs`
- Create: `src/FmpDotNet/Endpoints/TranscriptsEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/TranscriptsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/earning-call-transcript.AAPL.2025.Q3.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/earning-call-transcript-dates.AAPL.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/earning-call-transcript-latest.head.json`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs` (the property-count assertion, 14 → 15)
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` (a doc comment only)
- Modify: `README.md` (generated block only)

**Interfaces:**
- Consumes: nothing from Tasks 1–3.
- Produces: `EarningsTranscript`, `TranscriptDate`, `LatestTranscript`, their three `FmpJsonContext` entries,
  `public sealed class TranscriptsEndpoints(FmpTransport transport)` with
  - `Task<EarningsTranscript?> GetTranscriptAsync(string symbol, int year, int quarter, CancellationToken ct = default)`
  - `Task<IReadOnlyList<TranscriptDate>> GetDatesAsync(string symbol, CancellationToken ct = default)`
  - `Task<IReadOnlyList<LatestTranscript>> GetLatestAsync(int? limit = null, int? page = null, CancellationToken ct = default)`

  and `FmpClient.Transcripts`.

**The one thing to get right in this task.** The three records **deliberately disagree with each other**,
because the wire does. The same quarter is `period: "Q3"` on two paths and `quarter: 3` on the third; the same
year is `year` on one and `fiscalYear` on two. Harmonising them means inventing values FMP did not send. Do
not "fix" this; there is a test whose only job is to fail when someone does.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/earning-call-transcript.AAPL.2025.Q3.json` — captured 2026-08-29 from
`stable/earning-call-transcript?symbol=AAPL&year=2025&quarter=3`. **`content` is truncated to its first 300
characters here**; the live field is 46,487 characters and a fixture carrying it whole would be a 46 KB test
asset proving nothing the first 300 do not:

```json
[
  {
    "symbol": "AAPL",
    "period": "Q3",
    "year": 2025,
    "date": "2025-07-31",
    "content": "Suhasini Chandramouli: Good afternoon, and welcome to the Apple Q3 Fiscal Year 2025 Earnings Conference Call. My name is Suhasini Chandramouli, Director of Investor Relations. Today's call is being recorded. Speaking first today is Apple's CEO, Tim Cook, and he'll be followed by CFO, Kevan Parekh. A"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/earning-call-transcript-dates.AAPL.head.json` — the first three of the 84 rows
`?symbol=AAPL` returned on 2026-08-29, newest first. Note `quarter` is an **integer** here and `fiscalYear` is
the year field:

```json
[
  {
    "quarter": 3,
    "fiscalYear": 2026,
    "date": "2026-07-30"
  },
  {
    "quarter": 2,
    "fiscalYear": 2026,
    "date": "2026-04-30"
  },
  {
    "quarter": 1,
    "fiscalYear": 2026,
    "date": "2026-01-29"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/earning-call-transcript-latest.head.json` — the first three rows of
`?limit=10` on 2026-08-29. Note `period` is the **string** `"Q2"` here while the dates fixture used an
integer, and that the first row is dated in the future relative to the second:

```json
[
  {
    "symbol": "7011.T",
    "period": "Q2",
    "fiscalYear": 2025,
    "date": "2026-11-07"
  },
  {
    "symbol": "601939.SS",
    "period": "Q2",
    "fiscalYear": 2026,
    "date": "2026-08-28"
  },
  {
    "symbol": "PRS.OL",
    "period": "Q2",
    "fiscalYear": 2026,
    "date": "2026-08-28"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/TranscriptsTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three earnings-transcript paths, checked against captures taken live 2026-08-29.</summary>
public class TranscriptsTests
{
    private static (TranscriptsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new TranscriptsEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_transcript_binds_all_five_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript.AAPL.2025.Q3.json"),
            FmpJsonContext.Default.ListEarningsTranscript)!;

        Assert.Single(rows);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("Q3", rows[0].Period);
        Assert.Equal(2025, rows[0].Year);
        Assert.Equal(new LocalDate(2025, 7, 31), rows[0].Date);
        Assert.StartsWith("Suhasini Chandramouli: Good afternoon", rows[0].Content);
    }

    [Fact]
    public void The_three_transcript_records_each_keep_their_own_field_names()
    {
        // THE trap of this slice. FMP spells the same two facts three different ways across three paths:
        //
        //   earning-call-transcript          period: "Q3"   year: 2025
        //   earning-call-transcript-dates    quarter: 3     fiscalYear: 2026
        //   earning-call-transcript-latest   period: "Q2"   fiscalYear: 2025
        //
        // Harmonising the records would mean inventing values FMP did not send — an int where it sent a
        // string, or a `year` where it sent `fiscalYear`. This test fails the moment one record is
        // "corrected" to match its siblings.
        var transcript = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript.AAPL.2025.Q3.json"),
            FmpJsonContext.Default.ListEarningsTranscript)![0];
        var dates = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-dates.AAPL.head.json"),
            FmpJsonContext.Default.ListTranscriptDate)![0];
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-latest.head.json"),
            FmpJsonContext.Default.ListLatestTranscript)![0];

        Assert.Equal("Q3", transcript.Period);   // string, from `period`
        Assert.Equal(2025, transcript.Year);     // from `year`
        Assert.Equal(3, dates.Quarter);          // int, from `quarter`
        Assert.Equal(2026, dates.FiscalYear);    // from `fiscalYear`
        Assert.Equal("Q2", latest.Period);       // string, from `period`
        Assert.Equal(2025, latest.FiscalYear);   // from `fiscalYear`
    }

    [Fact]
    public void A_transcript_date_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-dates.AAPL.head.json"),
            FmpJsonContext.Default.ListTranscriptDate)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 7, 30), rows[0].Date);
        Assert.Equal(1, rows[2].Quarter);
    }

    [Fact]
    public void A_latest_row_binds_all_four_of_its_fields_including_a_non_US_ticker()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-latest.head.json"),
            FmpJsonContext.Default.ListLatestTranscript)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));

        // The feed is global and the tickers carry exchange suffixes. Nothing splits on the dot.
        Assert.Equal("7011.T", rows[0].Symbol);
        Assert.Equal("601939.SS", rows[1].Symbol);
        Assert.Equal("PRS.OL", rows[2].Symbol);

        // Not sorted by date: row 0 is dated after row 1. Measured 2026-08-29 and captured deliberately, so
        // nothing downstream assumes an ordering the feed does not promise.
        Assert.True(rows[0].Date > rows[1].Date);
    }

    [Fact]
    public async Task A_miss_is_null_rather_than_an_empty_list()
    {
        // Single-row endpoints on this SDK return T?, following CompanyEndpoints.GetProfileAsync.
        var (endpoints, _) = Build();

        Assert.Null(await endpoints.GetTranscriptAsync("NOSUCH", 2025, 3));
    }

    [Fact]
    public async Task The_transcript_is_queried_with_quarter_even_though_it_answers_period()
    {
        // The request parameter and the response field disagree on this one endpoint: it is QUERIED with
        // `quarter=3` and ANSWERS `period: "Q3"`. A future reader who renames the parameter to match the
        // response gets HTTP 400.
        var (endpoints, handler) = Build();

        await endpoints.GetTranscriptAsync("AAPL", 2025, 3);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/earning-call-transcript", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", query);
        Assert.Contains("year=2025", query);
        Assert.Contains("quarter=3", query);
        Assert.DoesNotContain("period=", query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_refused_before_the_request_goes_out(string? symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => endpoints.GetDatesAsync(symbol!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Latest_sends_paging_only_when_it_is_given_some()
    {
        var (bare, bareHandler) = Build();
        await bare.GetLatestAsync();

        var (paged, pagedHandler) = Build();
        await paged.GetLatestAsync(limit: 50, page: 1);

        // The bare call is its own query, NOT a synonym for page=0. Measured 2026-08-29 they were issued at
        // the same instant and shared 71 of 100 rows.
        Assert.Equal("/stable/earning-call-transcript-latest", bareHandler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("page=", bareHandler.Requests[0].Query);
        Assert.DoesNotContain("limit=", bareHandler.Requests[0].Query);
        Assert.Contains("limit=50", pagedHandler.Requests[0].Query);
        Assert.Contains("page=1", pagedHandler.Requests[0].Query);
    }
}
```

- [ ] **Step 3: Run them and watch them fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~TranscriptsTests"
```

Expected: FAIL — `CS0246` on `TranscriptsEndpoints`, `EarningsTranscript`, `TranscriptDate` and
`LatestTranscript`.

- [ ] **Step 4: Write the three records**

**Three deferred crefs.** The block below points at `Endpoints.TranscriptsEndpoints.GetTranscriptAsync` three
times — once in `EarningsTranscript`'s summary, and twice in `TranscriptDate` (its type summary and its
`Quarter` property). That facade does not exist until Step 6. Write all three as plain
`<c>GetTranscriptAsync</c>` now; Step 6b is the promotion.

Create `src/FmpDotNet/Models/EarningsTranscript.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One earnings call, transcribed. From <c>stable/earning-call-transcript</c>.
///
/// <para><b>This record and its two siblings deliberately disagree with each other, because the wire
/// does.</b> The same quarter is <see cref="Period"/> — the string <c>"Q3"</c> — here and on
/// <see cref="LatestTranscript"/>, but <see cref="TranscriptDate.Quarter"/> — the integer <c>3</c> — on
/// <see cref="TranscriptDate"/>. The same year is <see cref="Year"/> here and
/// <c>fiscalYear</c> on both siblings. Normalising the three would mean inventing values FMP did not send, so
/// each record transcribes its own endpoint and the divergence is documented on all three.</para>
///
/// <para><b>The request and the response disagree too.</b> The endpoint is queried with
/// <c>quarter=3</c> and answers <c>period: "Q3"</c>. See
/// <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/>.</para></summary>
public sealed record EarningsTranscript
{
    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fiscal quarter as a string — <c>Q3</c>. The <b>request</b> takes the integer
    /// <c>3</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> here and <c>fiscalYear</c> on both sibling
    /// records.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The date the call was held.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The whole transcript as one string, speaker names inline —
    /// <c>Suhasini Chandramouli: Good afternoon, and welcome…</c>.
    ///
    /// <para><b>Measured at 46,487 characters</b> for AAPL 2025 Q3 on 2026-08-29. It is not chunked, not
    /// parsed into speaker turns, and not offered as a stream: FMP sends one JSON string field and this SDK
    /// transcribes it. A caller who wants turns splits on <c>": "</c> at a line start and owns the result;
    /// there is no delimiter FMP guarantees.</para></summary>
    [JsonPropertyName("content")] public string? Content { get; init; }
}

/// <summary>One quarter for which a transcript exists. From
/// <c>stable/earning-call-transcript-dates</c>.
///
/// <para>The index into <see cref="EarningsTranscript"/>: these three fields are exactly what
/// <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/> needs, except that the year is spelled
/// <see cref="FiscalYear"/> here and <c>year</c> there. Measured 2026-08-29, <c>?symbol=AAPL</c> answered 84
/// rows spanning 2026-07-30 back to 2005-10-13 — full history, newest first, no cap observed.</para>
///
/// <para><b><see cref="Quarter"/> is an integer here and a string on both sibling records.</b> See
/// <see cref="EarningsTranscript"/>.</para></summary>
public sealed record TranscriptDate
{
    /// <summary>The fiscal quarter as an integer — <c>3</c>. Spelled <c>period: "Q3"</c> on both sibling
    /// records, and this is the form <see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/>
    /// takes.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> on <see cref="EarningsTranscript"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The date the call was held.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}

/// <summary>One entry in the whole-market feed of newly published transcripts. From
/// <c>stable/earning-call-transcript-latest</c>.
///
/// <para><b>Global, not US-only.</b> Measured 2026-08-29 the first page carried <c>7011.T</c>,
/// <c>601939.SS</c> and <c>PRS.OL</c> — Tokyo, Shanghai and Oslo — so
/// <see cref="Symbol"/> carries exchange suffixes and nothing should split on the dot.</para>
///
/// <para><b>Not sorted by date.</b> The same measurement had row 0 dated 2026-11-07 and row 1 dated
/// 2026-08-28. Nothing here promises an ordering.</para>
///
/// <para>This record carries <see cref="Period"/> as a string like <see cref="EarningsTranscript"/> and
/// <see cref="FiscalYear"/> like <see cref="TranscriptDate"/> — one field from each sibling's vocabulary.
/// See <see cref="EarningsTranscript"/> for why none of the three is normalised.</para></summary>
public sealed record LatestTranscript
{
    /// <summary>The ticker, as FMP spells it — including an exchange suffix for non-US listings.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fiscal quarter as a string — <c>Q2</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The fiscal year. Spelled <c>year</c> on <see cref="EarningsTranscript"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The date the call was held. Measured 2026-08-29 this can be in the future relative to other
    /// rows on the same page.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}
```

- [ ] **Step 5: Register the three records**

Append under the `// Economics, transcripts, ESG and COT (#40).` comment in
`src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<EarningsTranscript>))]
[JsonSerializable(typeof(List<TranscriptDate>))]
[JsonSerializable(typeof(List<LatestTranscript>))]
```

- [ ] **Step 6: Write the facade**

Create `src/FmpDotNet/Endpoints/TranscriptsEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Earnings call transcripts — one call in full, the index of calls for a symbol, and the
/// whole-market feed of what was just published.
///
/// <para><b>Which symbols have transcripts at all is answered elsewhere.</b>
/// <see cref="DirectoryEndpoints.GetTranscriptSymbolsAsync"/> serves
/// <c>stable/earnings-transcript-list</c> — 11,178 symbols measured 2026-08-27 — and stays on
/// <see cref="DirectoryEndpoints"/> because it is a universe list rather than a transcript.</para>
///
/// <para><b>The three paths spell the same two facts three different ways</b>, and this SDK reproduces each
/// exactly rather than normalising. See <see cref="EarningsTranscript"/>.</para></summary>
public sealed class TranscriptsEndpoints(FmpTransport transport)
{
    /// <summary>The largest page <see cref="GetLatestAsync"/> will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-29, <c>?limit=500</c> answered exactly 100 rows
    /// at HTTP 200 with nothing in the body saying the request had been trimmed — byte-identical to the bare
    /// call. <c>?limit=10</c> answered 10, so the parameter works below the cap.</para></summary>
    public const int MaxLatestTranscriptPageSize = 100;

    /// <summary>One earnings call in full — <c>stable/earning-call-transcript</c>.
    ///
    /// <para><b>Queried with <c>quarter=3</c>, answers <c>period: "Q3"</c>.</b> The request vocabulary and
    /// the response vocabulary disagree on this one endpoint. Renaming
    /// <paramref name="quarter"/> to match what comes back gets HTTP 400.</para>
    ///
    /// <para>All three parameters are required — measured 2026-08-29 by removing them one at a time, each
    /// omission answering HTTP 400 naming the missing one. The quarters a symbol actually has are listed by
    /// <see cref="GetDatesAsync"/>.</para>
    ///
    /// <para><see cref="EarningsTranscript.Content"/> is the whole transcript as one string — 46,487
    /// characters for AAPL 2025 Q3, measured 2026-08-29. This is not a small response.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The fiscal year, as <see cref="TranscriptDate.FiscalYear"/> reports it.</param>
    /// <param name="quarter">The fiscal quarter as an integer, 1 to 4 — as
    /// <see cref="TranscriptDate.Quarter"/> reports it, and <b>not</b> the <c>Q3</c> form the response
    /// carries.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The transcript, or <see langword="null"/> when FMP has none for that symbol and period.
    /// A miss is an empty array rather than an error, so it arrives here as null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<EarningsTranscript?> GetTranscriptAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/earning-call-transcript")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListEarningsTranscript, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Every quarter one symbol has a transcript for, newest first —
    /// <c>stable/earning-call-transcript-dates</c>.
    ///
    /// <para>The index into <see cref="GetTranscriptAsync"/>. Measured 2026-08-29, <c>?symbol=AAPL</c>
    /// answered <b>84 rows</b> spanning 2026-07-30 back to 2005-10-13 — full history, with no cap
    /// observed and no paging parameter offered.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter with a transcript, newest first. Never <see langword="null"/>; empty for a
    /// symbol with none, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<TranscriptDate>> GetDatesAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/earning-call-transcript-dates").With("symbol", symbol),
            FmpJsonContext.Default.ListTranscriptDate, ct);
    }

    /// <summary>Transcripts as they are published, across every market —
    /// <c>stable/earning-call-transcript-latest</c>.
    ///
    /// <para><b><paramref name="page"/> works, but it does not mean what its name implies.</b> Measured
    /// 2026-08-29: two <c>page=0</c> calls in one burst returned identical sets, and pages two apart are
    /// disjoint — so paging is real. But <b>adjacent pages overlap</b>: page 0 against page 1 shared
    /// <b>28 of 100</b> rows and page 1 against page 2 shared <b>21</b>. The stride is roughly 72–79 rows
    /// against a page size of 100, and the union of pages 0, 1 and 2 was <b>251 distinct rows of
    /// 300</b>.</para>
    ///
    /// <para>So a caller enumerating this feed must <b>de-duplicate</b>, on
    /// <c>(Symbol, FiscalYear, Period, Date)</c> — the tuple measured unique within all four pages taken. The
    /// SDK does not do it: hiding the overlap would mean buffering pages and guessing when to stop.</para>
    ///
    /// <para><b>The bare call is not <c>page=0</c>.</b> Issued at the same instant on 2026-08-29 they shared
    /// 71 of 100 rows. Omitting <paramref name="page"/> is its own query rather than a synonym for
    /// zero.</para>
    ///
    /// <para><b>The feed churns on a timescale of tens of minutes</b> — two bare calls twenty minutes apart
    /// shared 90 of 100 rows. That, and not the page overlap, is why nothing may be asserted by
    /// index against this endpoint.</para>
    ///
    /// <para>The response is global: measured 2026-08-29 the first page carried Tokyo, Shanghai and Oslo
    /// tickers, and was not sorted by date.</para></summary>
    /// <param name="limit">Rows per page. Omit for FMP's own default of 100. Values above
    /// <see cref="MaxLatestTranscriptPageSize"/> are clamped by FMP without saying so, which is why this
    /// method rejects them instead.</param>
    /// <param name="page">Zero-based page index — with the overlap described above. A page past the end
    /// answers an empty list, not an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's transcripts, unsorted and possibly overlapping the adjacent page. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxLatestTranscriptPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<LatestTranscript>> GetLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        if (page is { } p) ArgumentOutOfRangeException.ThrowIfNegative(p, nameof(page));
        if (limit is { } l)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(l, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(l, MaxLatestTranscriptPageSize, nameof(limit));
        }

        return transport.GetListAsync(
            new FmpRequest("stable/earning-call-transcript-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListLatestTranscript, ct);
    }
}
```

- [ ] **Step 6b: Promote the three deferred crefs**

`TranscriptsEndpoints` now exists. In `src/FmpDotNet/Models/EarningsTranscript.cs`, replace all three
`<c>GetTranscriptAsync</c>` placeholders with
`<see cref="Endpoints.TranscriptsEndpoints.GetTranscriptAsync"/>`.

- [ ] **Step 7: Add the paging-guard tests the facade now promises**

Append to `tests/FmpDotNet.Tests/TranscriptsTests.cs`:

```csharp
    [Fact]
    public async Task Latest_refuses_a_limit_above_the_measured_cap()
    {
        // Measured 2026-08-29: limit=500 answered exactly 100 rows at HTTP 200, byte-identical to the bare
        // call, with nothing saying the request was trimmed. A caller who asks for 500 and pages by 500
        // reads a fifth of the feed and is never told.
        var (endpoints, handler) = Build();

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(
                limit: TranscriptsEndpoints.MaxLatestTranscriptPageSize + 1));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Latest_refuses_a_negative_page()
    {
        var (endpoints, handler) = Build();

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(page: -1));

        Assert.Equal("page", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 8: Wire the facade**

`src/FmpDotNet/FmpClient.cs` — add `TranscriptsEndpoints transcripts` to the primary constructor after
`congress`, and the property after `Congress`:

```csharp
    /// <summary>Earnings call transcripts — one call in full, a symbol's index of calls, and the
    /// whole-market feed of what was just published.
    ///
    /// <para>Sits beside <see cref="Calendar"/> rather than on it because a transcript is the record of a
    /// call rather than a scheduled event, and because the three paths take a symbol-and-period key that
    /// nothing on <see cref="Calendar"/> takes. Which symbols have transcripts at all is on
    /// <see cref="Directory"/>. See <see cref="TranscriptsEndpoints"/>.</para></summary>
    public TranscriptsEndpoints Transcripts { get; } = transcripts;
```

`src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — one line after
`services.TryAddTransient<CongressEndpoints>();`:

```csharp
        services.TryAddTransient<TranscriptsEndpoints>();
```

- [ ] **Step 9: Update the two doc comments that now point at the wrong place**

`src/FmpDotNet/Endpoints/DirectoryEndpoints.cs` — `GetTranscriptSymbolsAsync`'s summary says the transcripts
themselves "are not modelled — three further paths in issue #25's long tail". They are modelled now. Replace
that clause with:

```csharp
    /// <para>The transcripts themselves are on <see cref="TranscriptsEndpoints"/>: this answers which symbols
    /// have any, <see cref="TranscriptsEndpoints.GetDatesAsync"/> answers which quarters one symbol has, and
    /// <see cref="TranscriptsEndpoints.GetTranscriptAsync"/> answers a call in full.</para>
```

Read the surrounding paragraph before editing — the exact sentence must be located rather than guessed at.
**There are two copies of this claim, not one**, and the second is on a *model* rather than an endpoint:
`src/FmpDotNet/Models/DirectoryListings.cs`, on `TranscriptSymbol`'s type summary, says "The transcripts
themselves are not modelled — that is three further paths in the long tail of issue #25." Rewrite both. From
inside `FmpDotNet.Models` the cref needs the same qualification the surrounding
`<see cref="Endpoints.DirectoryEndpoints"/>` uses, or it is CS1574. Find every copy rather than trusting this
list:

    grep -rn "three further paths\|themselves are not modelled" src/

`tests/FmpDotNet.SmokeTests/LiveApi.cs` — `SettledQuarter`'s summary begins "The fiscal quarter the five 13F
probes ask for". A sixth probe now uses it. Change the opening to:

```csharp
    /// <summary>The fiscal quarter the five 13F probes and the transcript probe ask for, paired with
    /// <see cref="SettledYear"/>.
```

and append one paragraph:

```csharp
    /// <para><b>It suits the transcript probe for an unrelated reason, and that is worth stating rather than
    /// relying on.</b> <c>GetTranscriptAsync</c> takes a year and a quarter, and measured 2026-08-29,
    /// <c>symbol=AAPL&amp;year=2025&amp;quarter=3</c> answered one row carrying a 46,487-character
    /// transcript. Q3 of <see cref="SettledYear"/> is held eight to ten months before this probe can run, so
    /// it is settled on every day of the year the way a 13F quarter is — but a symbol with no call that
    /// quarter would answer an empty array, so the pairing depends on AAPL as much as on the quarter.</para>
```

- [ ] **Step 10: Run everything, regenerate the README, commit**

```bash
dotnet test tests/FmpDotNet.Tests
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
dotnet test
```

Expected: green after the regeneration, with `git diff README.md` showing a new `fmp.Transcripts` block of
three rows and the count moving 169 → 172. `SweepCoverageTests` needs no new arm here — `symbol` falls to
`LiveApi.Symbol`, `year` to `SettledYear`, `quarter` to `SettledQuarter`, and `limit`/`page` to the existing
`int` cases — but run it and confirm rather than assuming.

```bash
git add -A && git commit -m "feat: add fmp.Transcripts over the three earnings-transcript paths (#40)"
```

---

### Task 5: `fmp.Esg`

**Files:**
- Create: `src/FmpDotNet/Models/EsgData.cs`
- Create: `src/FmpDotNet/Endpoints/EsgEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/EsgTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/esg-disclosures.AAPL.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/esg-ratings.AAPL.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/esg-benchmark.2023.head.json`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs` (the property-count assertion, 15 → 16)
- Modify: `README.md` (generated block only)

**Interfaces:**
- Consumes: nothing from Tasks 1–4.
- Produces: `EsgDisclosure`, `EsgRating`, `EsgBenchmark`, their three `FmpJsonContext` entries,
  `public sealed class EsgEndpoints(FmpTransport transport)` with
  - `Task<IReadOnlyList<EsgDisclosure>> GetDisclosuresAsync(string symbol, CancellationToken ct = default)`
  - `Task<IReadOnlyList<EsgRating>> GetRatingsAsync(string symbol, CancellationToken ct = default)`
  - `Task<IReadOnlyList<EsgBenchmark>> GetBenchmarkAsync(int? year = null, CancellationToken ct = default)`

  and `FmpClient.Esg`.

**Two traps in this task.** `industryRank` reads like an integer and is the sentence `"3 out of 9"` — typing
it `int?` throws on every row. And `esg-benchmark` accepts a `sector` parameter and **discards it**: measured
2026-08-29, `?sector=APPAREL RETAIL` was byte-identical to the bare call, all 1003 rows across 291 sectors. It
is therefore not on the signature, though `sector` is on the record because it is returned.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/esg-disclosures.AAPL.head.json` — the first two rows of `?symbol=AAPL` on
2026-08-29:

```json
[
  {
    "date": "2026-06-27",
    "acceptedDate": "2026-07-31",
    "symbol": "AAPL",
    "cik": "0000320193",
    "companyName": "Apple Inc.",
    "formType": "10-Q",
    "environmentalScore": 68.41,
    "socialScore": 47.36,
    "governanceScore": 61.32,
    "ESGScore": 59.03,
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019326000020/0000320193-26-000020-index.htm"
  },
  {
    "date": "2026-03-28",
    "acceptedDate": "2026-05-01",
    "symbol": "AAPL",
    "cik": "0000320193",
    "companyName": "Apple Inc.",
    "formType": "10-Q",
    "environmentalScore": 61.24,
    "socialScore": 47.64,
    "governanceScore": 59.71,
    "ESGScore": 56.2,
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019326000013/0000320193-26-000013-index.htm"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/esg-ratings.AAPL.head.json` — the first three rows of `?symbol=AAPL` on
2026-08-29. Chosen as they came: `fiscalYear` runs 1998, 2025, 1994, which is what makes the "not sorted"
assertion honest, and `industryRank` carries three different sentences:

```json
[
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "companyName": "Apple Inc.",
    "industry": "CONSUMER ELECTRONICS",
    "fiscalYear": 1998,
    "ESGRiskRating": "B",
    "industryRank": "3 out of 9"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "companyName": "Apple Inc.",
    "industry": "CONSUMER ELECTRONICS",
    "fiscalYear": 2025,
    "ESGRiskRating": "B",
    "industryRank": "19 out of 21"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "companyName": "Apple Inc.",
    "industry": "CONSUMER ELECTRONICS",
    "fiscalYear": 1994,
    "ESGRiskRating": "B",
    "industryRank": "1 out of 2"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/esg-benchmark.2023.head.json` — the first three rows of the bare call on
2026-08-29, which is byte-identical to `?year=2023`. `period` carries both `Q2` and `FY`:

```json
[
  {
    "fiscalYear": 2023,
    "period": "Q2",
    "sector": "APPAREL RETAIL",
    "environmentalScore": 61.36,
    "socialScore": 67.44,
    "governanceScore": 68.1,
    "ESGScore": 65.63
  },
  {
    "fiscalYear": 2023,
    "period": "Q2",
    "sector": "MEDICAL - CARE FACILITIES",
    "environmentalScore": 64.87,
    "socialScore": 67.26,
    "governanceScore": 67.59,
    "ESGScore": 66.57
  },
  {
    "fiscalYear": 2023,
    "period": "FY",
    "sector": "PROPERTY/CASUALTY INSURANCE",
    "environmentalScore": 55.41,
    "socialScore": 51.5,
    "governanceScore": 57.98,
    "ESGScore": 54.96
  }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/EsgTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three ESG paths, checked against captures taken live 2026-08-29.</summary>
public class EsgTests
{
    private static (EsgEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new EsgEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_disclosure_binds_all_eleven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-disclosures.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgDisclosure)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 27), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 7, 31), rows[0].AcceptedDate);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("Apple Inc.", rows[0].CompanyName);
        Assert.Equal("10-Q", rows[0].FormType);
        Assert.Equal(68.41m, rows[0].EnvironmentalScore);
        Assert.Equal(47.36m, rows[0].SocialScore);
        Assert.Equal(61.32m, rows[0].GovernanceScore);
        Assert.Equal(59.03m, rows[0].EsgScore);
        Assert.StartsWith("https://www.sec.gov/Archives/edgar/", rows[0].Url);
    }

    [Fact]
    public void Cik_keeps_its_leading_zeros()
    {
        // "0000320193" is ten characters and only 320193 as a number. Typing this int? or long? drops four
        // significant characters and breaks every join against another cik-keyed path in this SDK.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-disclosures.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgDisclosure)!;

        Assert.Equal("0000320193", rows[0].Cik);
    }

    [Fact]
    public void The_uppercase_ESG_wire_names_bind_to_house_cased_properties()
    {
        // FMP spells these `ESGScore` and `ESGRiskRating`. The properties are EsgScore and EsgRiskRating,
        // following `cik -> Cik` and `growthEPS -> GrowthEps`. The attribute carries the wire spelling and
        // this test fails if either is "tidied" in the wrong direction.
        var disclosure = JsonSerializer.Deserialize(
            """[{"ESGScore":59.03}]""", FmpJsonContext.Default.ListEsgDisclosure)![0];
        var rating = JsonSerializer.Deserialize(
            """[{"ESGRiskRating":"B"}]""", FmpJsonContext.Default.ListEsgRating)![0];

        Assert.Equal(59.03m, disclosure.EsgScore);
        Assert.Equal("B", rating.EsgRiskRating);
    }

    [Fact]
    public void Industry_rank_is_a_sentence_and_not_a_number()
    {
        // The natural guess is int?, and it would throw on every row: the measured value is "3 out of 9".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-ratings.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgRating)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("3 out of 9", rows[0].IndustryRank);
        Assert.Equal("19 out of 21", rows[1].IndustryRank);
        Assert.Equal("1 out of 2", rows[2].IndustryRank);
    }

    [Fact]
    public void Ratings_are_not_returned_in_year_order()
    {
        // 1998, then 2025, then 1994. Captured as they arrived so nothing downstream assumes an ordering FMP
        // does not promise.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-ratings.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgRating)!;

        Assert.Equal(new int?[] { 1998, 2025, 1994 }, rows.Select(r => r.FiscalYear));
    }

    [Fact]
    public void A_benchmark_row_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-benchmark.2023.head.json"),
            FmpJsonContext.Default.ListEsgBenchmark)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(2023, rows[0].FiscalYear);
        Assert.Equal("Q2", rows[0].Period);
        Assert.Equal("APPAREL RETAIL", rows[0].Sector);
        Assert.Equal(65.63m, rows[0].EsgScore);

        // `period` is not always a quarter — row 2 is FY. A closed enum over it would be wrong twice over.
        Assert.Equal("FY", rows[2].Period);
    }

    [Fact]
    public async Task The_benchmark_never_sends_a_sector_parameter()
    {
        // THE trap of this facade's request surface. Measured 2026-08-29, `?sector=APPAREL RETAIL` came back
        // BYTE-IDENTICAL to the bare call — 1003 rows across 291 sectors. FMP accepts the parameter and
        // discards it, so offering one would promise filtering that never happens. The caller filters the
        // list on EsgBenchmark.Sector, which is on the record precisely because the field IS returned.
        var (endpoints, handler) = Build();

        await endpoints.GetBenchmarkAsync(2020);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/esg-benchmark", handler.Requests[0].AbsolutePath);
        Assert.Contains("year=2020", query);
        Assert.DoesNotContain("sector", query);
    }

    [Fact]
    public async Task The_benchmark_year_is_optional_and_omitted_rather_than_sent_empty()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetBenchmarkAsync();

        Assert.DoesNotContain("year=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (disclosures, disclosuresHandler) = Build();
        await disclosures.GetDisclosuresAsync("AAPL");

        var (ratings, ratingsHandler) = Build();
        await ratings.GetRatingsAsync("AAPL");

        Assert.Equal("/stable/esg-disclosures", disclosuresHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", disclosuresHandler.Requests[0].Query);
        Assert.Equal("/stable/esg-ratings", ratingsHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", ratingsHandler.Requests[0].Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_refused_before_the_request_goes_out(string? symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => endpoints.GetRatingsAsync(symbol!));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Run them and watch them fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EsgTests"
```

Expected: FAIL — `CS0246` on `EsgEndpoints`, `EsgDisclosure`, `EsgRating` and `EsgBenchmark`.

- [ ] **Step 4: Write the three records**

**One deferred cref.** `EsgBenchmark`'s type summary ends with
`<see cref="Endpoints.EsgEndpoints.GetBenchmarkAsync"/>`, and that facade does not exist until Step 6. Write
it as plain `<c>GetBenchmarkAsync</c>` now; Step 6b is the promotion.
`<see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/>` on `EsgRating.Industry` is **not** deferred —
that method already exists.

Create `src/FmpDotNet/Models/EsgData.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One filing's environmental, social and governance scores. From
/// <c>stable/esg-disclosures</c>.
///
/// <para>One row per SEC filing rather than per period: measured 2026-08-29 on AAPL the rows are 10-Q and
/// 10-K filings, each carrying the four scores as of that filing. <see cref="Date"/> is the period end and
/// <see cref="AcceptedDate"/> is when EDGAR accepted it, which is why the two differ by about a
/// month.</para></summary>
public sealed record EsgDisclosure
{
    /// <summary>The period end the filing reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date EDGAR accepted the filing — later than <see cref="Date"/> on every row measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, <b>zero-padded to ten characters</b> — <c>0000320193</c>. A
    /// string, not a number: the leading zeros are significant and every other <c>cik</c> in this SDK is a
    /// string for the same reason.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as EDGAR carries it.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The EDGAR form type the scores were taken from — <c>10-Q</c>, <c>10-K</c>.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The environmental score, 0 to 100.</summary>
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }

    /// <summary>The social score, 0 to 100.</summary>
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }

    /// <summary>The governance score, 0 to 100.</summary>
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }

    /// <summary>The composite score, 0 to 100. <b>Bound from the wire name <c>ESGScore</c></b>; the property
    /// is house-cased, as <c>cik</c> binds to <c>Cik</c>.</summary>
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }

    /// <summary>The EDGAR index page for the filing.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>One company's ESG risk rating for one fiscal year. From <c>stable/esg-ratings</c>.
///
/// <para><b>Not returned in year order.</b> Measured 2026-08-29 on AAPL the first three rows were 1998, 2025
/// and 1994. Sort before presenting.</para></summary>
public sealed record EsgRating
{
    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters. See
    /// <see cref="EsgDisclosure.Cik"/>.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as EDGAR carries it.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The industry the rank below is against — <c>CONSUMER ELECTRONICS</c>. FMP's own
    /// vocabulary, uppercased, and <b>not</b> the list
    /// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/> serves — that one is title-cased
    /// (<c>Consumer Electronics</c>), so the two do not join without normalising.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The fiscal year the rating is for.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The letter rating — <c>B</c>. Bound from the wire name <c>ESGRiskRating</c>. A string and
    /// not an enum: the full set of grades was not enumerated, and a closed C# enum over an open server-side
    /// vocabulary is a breaking change waiting for a Tuesday.</summary>
    [JsonPropertyName("ESGRiskRating")] public string? EsgRiskRating { get; init; }

    /// <summary><b>A sentence, not a number</b> — <c>"3 out of 9"</c>, <c>"19 out of 21"</c>, measured
    /// 2026-08-29. Typing this <see langword="int"/> is the obvious guess and it throws on every row. A
    /// caller who wants the two numbers parses them and owns the result; FMP does not send them
    /// separately.</summary>
    [JsonPropertyName("industryRank")] public string? IndustryRank { get; init; }
}

/// <summary>One sector's average ESG scores for one fiscal period. From <c>stable/esg-benchmark</c>.
///
/// <para><b><see cref="Sector"/> is on this record and not on the method that fetches it</b>, and the
/// asymmetry is deliberate: FMP <i>returns</i> the field and <i>ignores</i> the query parameter of the same
/// name. Measured 2026-08-29, <c>?sector=APPAREL RETAIL</c> was byte-identical to the bare call — 1003 rows
/// across 291 sectors. See <see cref="Endpoints.EsgEndpoints.GetBenchmarkAsync"/>.</para></summary>
public sealed record EsgBenchmark
{
    /// <summary>The fiscal year.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The fiscal period — <c>Q1</c> through <c>Q4</c> or <c>FY</c>, both measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The sector, in FMP's own uppercase vocabulary — <c>APPAREL RETAIL</c>,
    /// <c>MEDICAL - CARE FACILITIES</c>. 291 distinct values measured 2026-08-29. <b>Filter on this
    /// client-side</b>; the endpoint's <c>sector</c> parameter does nothing.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The sector's average environmental score, 0 to 100.</summary>
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }

    /// <summary>The sector's average social score, 0 to 100.</summary>
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }

    /// <summary>The sector's average governance score, 0 to 100.</summary>
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }

    /// <summary>The sector's average composite score, 0 to 100. Bound from the wire name
    /// <c>ESGScore</c>.</summary>
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }
}
```

- [ ] **Step 5: Register the three records**

Append under the `// Economics, transcripts, ESG and COT (#40).` comment in `FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<EsgDisclosure>))]
[JsonSerializable(typeof(List<EsgRating>))]
[JsonSerializable(typeof(List<EsgBenchmark>))]
```

- [ ] **Step 6: Write the facade**

Create `src/FmpDotNet/Endpoints/EsgEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Environmental, social and governance data — per-filing scores, a company's risk rating history,
/// and sector averages to read either against.
///
/// <para><b>The sector benchmark is three years stale and says nothing about it.</b> Measured 2026-08-29,
/// the bare call answered fiscal year <b>2023</b> only. See <see cref="GetBenchmarkAsync"/>.</para>
///
/// <para><b>One parameter here is accepted and discarded</b>, which is why this facade has fewer parameters
/// than FMP's documentation implies. See <see cref="GetBenchmarkAsync"/>.</para></summary>
public sealed class EsgEndpoints(FmpTransport transport)
{
    /// <summary>One company's ESG scores, filing by filing — <c>stable/esg-disclosures</c>.
    ///
    /// <para>One row per SEC filing, newest first. Measured 2026-08-29, <c>?symbol=AAPL</c> answered rows
    /// from 10-Q and 10-K filings with all eleven fields populated on each.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That company's scored filings. Never <see langword="null"/>; empty for a symbol FMP has not
    /// scored, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgDisclosure>> GetDisclosuresAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/esg-disclosures").With("symbol", symbol),
            FmpJsonContext.Default.ListEsgDisclosure, ct);
    }

    /// <summary>One company's ESG risk rating by fiscal year — <c>stable/esg-ratings</c>.
    ///
    /// <para><b>Not returned in year order.</b> Measured 2026-08-29 on AAPL the first three rows were 1998,
    /// 2025 and 1994. Sort on <see cref="EsgRating.FiscalYear"/> before presenting.</para>
    ///
    /// <para><see cref="EsgRating.IndustryRank"/> is the sentence <c>"3 out of 9"</c> rather than a
    /// number.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That company's ratings, unsorted. Never <see langword="null"/>; empty for a symbol FMP has
    /// not rated, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgRating>> GetRatingsAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/esg-ratings").With("symbol", symbol),
            FmpJsonContext.Default.ListEsgRating, ct);
    }

    /// <summary>Sector-average ESG scores for one fiscal year — <c>stable/esg-benchmark</c>.
    ///
    /// <para><b>There is no <c>sector</c> parameter here, and that is not an omission.</b> FMP documents one
    /// and ignores it: measured 2026-08-29, <c>?sector=APPAREL RETAIL</c> answered a response
    /// <b>byte-identical</b> to the bare call — 1003 rows across 291 sectors. Exposing it would promise
    /// filtering the API does not perform, which is the same class of defect as the <c>-by-id</c> trap closed
    /// in #31. Filter the returned list on <see cref="EsgBenchmark.Sector"/> instead; a method parameter that
    /// looked like a query parameter but was applied locally would misrepresent what the request did.</para>
    ///
    /// <para><b>The default year is 2023</b>, three years before the measurement date. The bare call and
    /// <c>?year=2023</c> were byte-identical on 2026-08-29, and <c>?year=2025</c> answered 622 rows — fewer
    /// than 2023's 1003, but not empty. An unrecognised year answers <c>[]</c> with HTTP 200 rather than an
    /// error, so a typo reads as "no data for that year".</para></summary>
    /// <param name="year">The fiscal year. Omit for FMP's default, measured as 2023.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector per period in that year. Never <see langword="null"/>; empty for a year
    /// FMP has no benchmark for, not an error.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgBenchmark>> GetBenchmarkAsync(
        int? year = null, CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/esg-benchmark").With("year", year),
            FmpJsonContext.Default.ListEsgBenchmark, ct);
}
```

- [ ] **Step 6b: Promote the deferred cref**

`EsgEndpoints` now exists. In `src/FmpDotNet/Models/EsgData.cs`, replace `<c>GetBenchmarkAsync</c>` in
`EsgBenchmark`'s type summary with `<see cref="Endpoints.EsgEndpoints.GetBenchmarkAsync"/>`.

- [ ] **Step 7: Wire the facade**

`src/FmpDotNet/FmpClient.cs` — add `EsgEndpoints esg` to the primary constructor after `transcripts`, and:

```csharp
    /// <summary>Environmental, social and governance data — per-filing scores, rating history, and the
    /// sector averages to read either against.
    ///
    /// <para>Its own facade rather than a corner of <see cref="Company"/> because two of its three paths take
    /// no symbol at all, and the benchmark is a whole-market reference table. See
    /// <see cref="EsgEndpoints"/>.</para></summary>
    public EsgEndpoints Esg { get; } = esg;
```

`FmpServiceCollectionExtensions.cs`:

```csharp
        services.TryAddTransient<EsgEndpoints>();
```

- [ ] **Step 8: Run everything, regenerate the README, commit**

```bash
dotnet test tests/FmpDotNet.Tests
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
dotnet test
```

Expected: green, with `README.md` gaining an `fmp.Esg` block and the count moving 172 → 175.
`Probe.Argument` needs no new arm: `symbol` falls to `LiveApi.Symbol` and `year` to `LiveApi.SettledYear`,
which is 2025 — measured 2026-08-29 to answer **622 rows**, not empty. Confirm `SweepCoverageTests` is green
rather than assuming it.

```bash
git add -A && git commit -m "feat: add fmp.Esg over the three ESG paths (#40)"
```

---

### Task 6: the three COT records — 128 properties, 27 of them renamed

**Files:**
- Create: `src/FmpDotNet/Models/CotReport.cs`
- Create: `src/FmpDotNet/Models/CotAnalysis.cs`
- Create: `tests/FmpDotNet.Tests/CotTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-report.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-analysis.NG.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-list.head.json`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpDotNet.csproj`

**Interfaces:**
- Consumes: nothing from Tasks 1–5.
- Produces: `CotReport` (128 properties), `CotAnalysis` (16), `CotSymbol` (2), and their three
  `FmpJsonContext` entries. Task 7 builds the facade on them.

**Read this before writing a line of it.**

`CotReport` is **the widest record in the SDK**, against `FinancialRatios` at 66. Twenty-seven of its 128
properties deliberately do not match their `[JsonPropertyName]`, because FMP's spelling is wrong and the house
rule is that the attribute carries the wire verbatim while the property carries correct English — the same
rule under which `senateID` binds to `SenateId` and `growthEBITDA` to `GrowthEbitda`.

| wire | property | why |
|---|---|---|
| `netPostion` | `NetPosition` | missing `i`; on `CotAnalysis`, whose siblings `previousNetPosition` and `changeInNetPosition` are spelled correctly |
| `changeInNoncommSpeadAll` | `ChangeInNoncommSpreadAll` | missing `r`; `noncommPositionsSpreadAll` on the same record is correct |
| `tradersNoncommSpeadOl` | `TradersNoncommSpreadOld` | **both** defects on one field |
| `…Ol` (26 fields) | `…Old` | the positions block spells the suffix `Old`; the pct, traders and concentration blocks do not |

**The property block below is generated from the captured responses, not retyped**, and it has been checked:
128 properties, 128 distinct names, no collisions, 27 carrying a `// sic` or `// wire suffix` comment. Copy it
verbatim. Retyping 128 property names by hand is the single largest transcription risk in issue #40, and
`Assert.Empty(Binding.Unbound(rows[0]))` in Step 3 is what catches a slip: a mistyped attribute leaves that
property null and names it in the failure.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-list.head.json` — the first three of the 65 rows the
bare call returned on 2026-08-29:

```json
[
  {
    "symbol": "NG",
    "name": "Natural Gas (NG)"
  },
  {
    "symbol": "TN",
    "name": "Ultra 10-Year T-Note (TN)"
  },
  {
    "symbol": "A6",
    "name": "Australian Dollar (A6)"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-analysis.NG.head.json` — the two newest of the nine rows
`?symbol=NG` returned on 2026-08-29. **Both rows are needed**: between them they prove
`changeInNetPosition` is a percentage rather than a delta, in both signs, and row 1 carries the leading space
in `marketSentiment`:

```json
[
  {
    "symbol": "NG",
    "date": "2024-02-27 00:00:00",
    "name": "Natural Gas (NG)",
    "sector": "ENERGIES",
    "exchange": "NAT GAS NYME - NEW YORK MERCANTILE EXCHANGE",
    "currentLongMarketSituation": 41.09,
    "currentShortMarketSituation": 58.91,
    "marketSituation": "Bearish",
    "previousLongMarketSituation": 41.12,
    "previousShortMarketSituation": 58.88,
    "previousMarketSituation": "Bearish",
    "netPostion": -141553,
    "previousNetPosition": -153872,
    "changeInNetPosition": 8.01,
    "marketSentiment": "Increasing Bullish",
    "reversalTrend": true
  },
  {
    "symbol": "NG",
    "date": "2024-02-20 00:00:00",
    "name": "Natural Gas (NG)",
    "sector": "ENERGIES",
    "exchange": "NAT GAS NYME - NEW YORK MERCANTILE EXCHANGE",
    "currentLongMarketSituation": 41.12,
    "currentShortMarketSituation": 58.88,
    "marketSituation": "Bearish",
    "previousLongMarketSituation": 41.52,
    "previousShortMarketSituation": 58.48,
    "previousMarketSituation": "Bearish",
    "netPostion": -153872,
    "previousNetPosition": -136557,
    "changeInNetPosition": -12.68,
    "marketSentiment": " Increasing Bearish",
    "reversalTrend": false
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-report.head.json` — one NG row and one ZC row, both
dated 2024-02-27, captured 2026-08-29. **Both rows are needed and the second is the point**: NG's entire
`Other` block is zero, and a test asserting against NG alone could not tell a bound zero from an unbound
property. ZC is one of the 14 symbols whose `Other` block carries real data — 118 of 545 rows across those 14,
measured — so `tradersTotOther` is 458 there and 0 on NG.

```json
[
  {
    "symbol": "NG",
    "date": "2024-02-27 00:00:00",
    "name": "Natural Gas (NG)",
    "sector": "ENERGIES",
    "marketAndExchangeNames": "NAT GAS NYME - NEW YORK MERCANTILE EXCHANGE",
    "cftcContractMarketCode": "023651",
    "cftcMarketCode": "NYME",
    "cftcRegionCode": "1",
    "cftcCommodityCode": "23",
    "openInterestAll": 1500882,
    "noncommPositionsLongAll": 326328,
    "noncommPositionsShortAll": 467881,
    "noncommPositionsSpreadAll": 558237,
    "commPositionsLongAll": 545380,
    "commPositionsShortAll": 433185,
    "totReptPositionsLongAll": 1429945,
    "totReptPositionsShortAll": 1459303,
    "nonreptPositionsLongAll": 70937,
    "nonreptPositionsShortAll": 41579,
    "openInterestOld": 1500882,
    "noncommPositionsLongOld": 326328,
    "noncommPositionsShortOld": 467881,
    "noncommPositionsSpreadOld": 558237,
    "commPositionsLongOld": 545380,
    "commPositionsShortOld": 433185,
    "totReptPositionsLongOld": 1429945,
    "totReptPositionsShortOld": 1459303,
    "nonreptPositionsLongOld": 70937,
    "nonreptPositionsShortOld": 41579,
    "openInterestOther": 0,
    "noncommPositionsLongOther": 0,
    "noncommPositionsShortOther": 0,
    "noncommPositionsSpreadOther": 0,
    "commPositionsLongOther": 0,
    "commPositionsShortOther": 0,
    "totReptPositionsLongOther": 0,
    "totReptPositionsShortOther": 0,
    "nonreptPositionsLongOther": 0,
    "nonreptPositionsShortOther": 0,
    "changeInOpenInterestAll": -91578,
    "changeInNoncommLongAll": -30006,
    "changeInNoncommShortAll": -42325,
    "changeInNoncommSpeadAll": -28330,
    "changeInCommLongAll": -22411,
    "changeInCommShortAll": -19062,
    "changeInTotReptLongAll": -80747,
    "changeInTotReptShortAll": -89717,
    "changeInNonreptLongAll": -10831,
    "changeInNonreptShortAll": -1861,
    "pctOfOpenInterestAll": 100,
    "pctOfOiNoncommLongAll": 21.7,
    "pctOfOiNoncommShortAll": 31.2,
    "pctOfOiNoncommSpreadAll": 37.2,
    "pctOfOiCommLongAll": 36.3,
    "pctOfOiCommShortAll": 28.9,
    "pctOfOiTotReptLongAll": 95.3,
    "pctOfOiTotReptShortAll": 97.2,
    "pctOfOiNonreptLongAll": 4.7,
    "pctOfOiNonreptShortAll": 2.8,
    "pctOfOpenInterestOl": 100,
    "pctOfOiNoncommLongOl": 21.7,
    "pctOfOiNoncommShortOl": 31.2,
    "pctOfOiNoncommSpreadOl": 37.2,
    "pctOfOiCommLongOl": 36.3,
    "pctOfOiCommShortOl": 28.9,
    "pctOfOiTotReptLongOl": 95.3,
    "pctOfOiTotReptShortOl": 97.2,
    "pctOfOiNonreptLongOl": 4.7,
    "pctOfOiNonreptShortOl": 2.8,
    "pctOfOpenInterestOther": 0,
    "pctOfOiNoncommLongOther": 0,
    "pctOfOiNoncommShortOther": 0,
    "pctOfOiNoncommSpreadOther": 0,
    "pctOfOiCommLongOther": 0,
    "pctOfOiCommShortOther": 0,
    "pctOfOiTotReptLongOther": 0,
    "pctOfOiTotReptShortOther": 0,
    "pctOfOiNonreptLongOther": 0,
    "pctOfOiNonreptShortOther": 0,
    "tradersTotAll": 343,
    "tradersNoncommLongAll": 122,
    "tradersNoncommShortAll": 130,
    "tradersNoncommSpreadAll": 155,
    "tradersCommLongAll": 80,
    "tradersCommShortAll": 68,
    "tradersTotReptLongAll": 298,
    "tradersTotReptShortAll": 266,
    "tradersTotOl": 343,
    "tradersNoncommLongOl": 122,
    "tradersNoncommShortOl": 130,
    "tradersNoncommSpeadOl": 155,
    "tradersCommLongOl": 80,
    "tradersCommShortOl": 68,
    "tradersTotReptLongOl": 298,
    "tradersTotReptShortOl": 266,
    "tradersTotOther": 0,
    "tradersNoncommLongOther": 0,
    "tradersNoncommShortOther": 0,
    "tradersNoncommSpreadOther": 0,
    "tradersCommLongOther": 0,
    "tradersCommShortOther": 0,
    "tradersTotReptLongOther": 0,
    "tradersTotReptShortOther": 0,
    "concGrossLe4TdrLongAll": 16.7,
    "concGrossLe4TdrShortAll": 23.8,
    "concGrossLe8TdrLongAll": 28.3,
    "concGrossLe8TdrShortAll": 35.1,
    "concNetLe4TdrLongAll": 10.1,
    "concNetLe4TdrShortAll": 12.1,
    "concNetLe8TdrLongAll": 15.5,
    "concNetLe8TdrShortAll": 17.7,
    "concGrossLe4TdrLongOl": 16.7,
    "concGrossLe4TdrShortOl": 23.8,
    "concGrossLe8TdrLongOl": 28.3,
    "concGrossLe8TdrShortOl": 35.1,
    "concNetLe4TdrLongOl": 10.1,
    "concNetLe4TdrShortOl": 12.1,
    "concNetLe8TdrLongOl": 15.5,
    "concNetLe8TdrShortOl": 17.7,
    "concGrossLe4TdrLongOther": 0,
    "concGrossLe4TdrShortOther": 0,
    "concGrossLe8TdrLongOther": 0,
    "concGrossLe8TdrShortOther": 0,
    "concNetLe4TdrLongOther": 0,
    "concNetLe4TdrShortOther": 0,
    "concNetLe8TdrLongOther": 0,
    "concNetLe8TdrShortOther": 0,
    "contractUnits": "(Contracts of 10,000 MMBTU'S)"
  },
  {
    "symbol": "ZC",
    "date": "2024-02-27 00:00:00",
    "name": "Corn (ZC)",
    "sector": "GRAINS",
    "marketAndExchangeNames": "CORN - CHICAGO BOARD OF TRADE",
    "cftcContractMarketCode": "002602",
    "cftcMarketCode": "CBT",
    "cftcRegionCode": "0",
    "cftcCommodityCode": "2",
    "openInterestAll": 1504488,
    "noncommPositionsLongAll": 295676,
    "noncommPositionsShortAll": 528280,
    "noncommPositionsSpreadAll": 386935,
    "commPositionsLongAll": 670777,
    "commPositionsShortAll": 420149,
    "totReptPositionsLongAll": 1353388,
    "totReptPositionsShortAll": 1335364,
    "nonreptPositionsLongAll": 151100,
    "nonreptPositionsShortAll": 169124,
    "openInterestOld": 1178930,
    "noncommPositionsLongOld": 355008,
    "noncommPositionsShortOld": 562552,
    "noncommPositionsSpreadOld": 176354,
    "commPositionsLongOld": 518875,
    "commPositionsShortOld": 323260,
    "totReptPositionsLongOld": 1050237,
    "totReptPositionsShortOld": 1062166,
    "nonreptPositionsLongOld": 128693,
    "nonreptPositionsShortOld": 116764,
    "openInterestOther": 325558,
    "noncommPositionsLongOther": 135489,
    "noncommPositionsShortOther": 160549,
    "noncommPositionsSpreadOther": 15760,
    "commPositionsLongOther": 151902,
    "commPositionsShortOther": 96889,
    "totReptPositionsLongOther": 303151,
    "totReptPositionsShortOther": 273198,
    "nonreptPositionsLongOther": 22407,
    "nonreptPositionsShortOther": 52360,
    "changeInOpenInterestAll": -96492,
    "changeInNoncommLongAll": 13127,
    "changeInNoncommShortAll": -20336,
    "changeInNoncommSpeadAll": -30940,
    "changeInCommLongAll": -67479,
    "changeInCommShortAll": -35639,
    "changeInTotReptLongAll": -85292,
    "changeInTotReptShortAll": -86915,
    "changeInNonreptLongAll": -11200,
    "changeInNonreptShortAll": -9577,
    "pctOfOpenInterestAll": 100,
    "pctOfOiNoncommLongAll": 19.7,
    "pctOfOiNoncommShortAll": 35.1,
    "pctOfOiNoncommSpreadAll": 25.7,
    "pctOfOiCommLongAll": 44.6,
    "pctOfOiCommShortAll": 27.9,
    "pctOfOiTotReptLongAll": 90,
    "pctOfOiTotReptShortAll": 88.8,
    "pctOfOiNonreptLongAll": 10,
    "pctOfOiNonreptShortAll": 11.2,
    "pctOfOpenInterestOl": 100,
    "pctOfOiNoncommLongOl": 30.1,
    "pctOfOiNoncommShortOl": 47.7,
    "pctOfOiNoncommSpreadOl": 15,
    "pctOfOiCommLongOl": 44,
    "pctOfOiCommShortOl": 27.4,
    "pctOfOiTotReptLongOl": 89.1,
    "pctOfOiTotReptShortOl": 90.1,
    "pctOfOiNonreptLongOl": 10.9,
    "pctOfOiNonreptShortOl": 9.9,
    "pctOfOpenInterestOther": 100,
    "pctOfOiNoncommLongOther": 41.6,
    "pctOfOiNoncommShortOther": 49.3,
    "pctOfOiNoncommSpreadOther": 4.8,
    "pctOfOiCommLongOther": 46.7,
    "pctOfOiCommShortOther": 29.8,
    "pctOfOiTotReptLongOther": 93.1,
    "pctOfOiTotReptShortOther": 83.9,
    "pctOfOiNonreptLongOther": 6.9,
    "pctOfOiNonreptShortOther": 16.1,
    "tradersTotAll": 695,
    "tradersNoncommLongAll": 133,
    "tradersNoncommShortAll": 158,
    "tradersNoncommSpreadAll": 153,
    "tradersCommLongAll": 308,
    "tradersCommShortAll": 306,
    "tradersTotReptLongAll": 524,
    "tradersTotReptShortAll": 549,
    "tradersTotOl": 669,
    "tradersNoncommLongOl": 133,
    "tradersNoncommShortOl": 152,
    "tradersNoncommSpeadOl": 119,
    "tradersCommLongOl": 288,
    "tradersCommShortOl": 261,
    "tradersTotReptLongOl": 485,
    "tradersTotReptShortOl": 472,
    "tradersTotOther": 458,
    "tradersNoncommLongOther": 68,
    "tradersNoncommShortOther": 85,
    "tradersNoncommSpreadOther": 41,
    "tradersCommLongOther": 141,
    "tradersCommShortOther": 228,
    "tradersTotReptLongOther": 234,
    "tradersTotReptShortOther": 330,
    "concGrossLe4TdrLongAll": 10.9,
    "concGrossLe4TdrShortAll": 12.7,
    "concGrossLe8TdrLongAll": 19.2,
    "concGrossLe8TdrShortAll": 21.6,
    "concNetLe4TdrLongAll": 9.1,
    "concNetLe4TdrShortAll": 10.4,
    "concNetLe8TdrLongAll": 14.8,
    "concNetLe8TdrShortAll": 16,
    "concGrossLe4TdrLongOl": 12.7,
    "concGrossLe4TdrShortOl": 15.4,
    "concGrossLe8TdrLongOl": 20.9,
    "concGrossLe8TdrShortOl": 25.3,
    "concNetLe4TdrLongOl": 12.1,
    "concNetLe4TdrShortOl": 14.2,
    "concNetLe8TdrLongOl": 19.1,
    "concNetLe8TdrShortOl": 21.9,
    "concGrossLe4TdrLongOther": 26.9,
    "concGrossLe4TdrShortOther": 21.9,
    "concGrossLe8TdrLongOther": 37,
    "concGrossLe8TdrShortOther": 31.8,
    "concNetLe4TdrLongOther": 26.4,
    "concNetLe4TdrShortOther": 21.9,
    "concNetLe8TdrLongOther": 35.6,
    "concNetLe8TdrShortOther": 31.7,
    "contractUnits": "(CONTRACTS OF 5,000 BUSHELS)"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/CotTests.cs`. The facade does not exist until Task 7, so this file starts as
binding tests only and Task 7 appends the request-surface ones:

```csharp
using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The Commitment of Traders records, checked against captures taken live 2026-08-29.
///
/// <para><c>CotReport</c> is 128 properties and these tests do not assert all 128 individually. The guard
/// against a transcription error is <see cref="Binding.Unbound{T}"/> over a fixture in which every field is
/// populated: a mistyped <c>[JsonPropertyName]</c> leaves its property null and the assertion names it. The
/// explicit assertions below are the four blocks' representatives plus every field the naming rule
/// touches.</para></summary>
public class CotTests
{
    [Fact]
    public void A_captured_report_row_binds_all_one_hundred_and_twenty_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)!;

        Assert.Equal(2, rows.Count);

        // Row 1 is ZC, whose `Other` block carries real values — the only row of the two on which every one
        // of the 128 is non-zero and non-null, and therefore the only one this assertion can be made against.
        Assert.Empty(Binding.Unbound(rows[1]));
    }

    [Fact]
    public void One_representative_from_each_of_the_four_blocks_binds()
    {
        // positions / pct / traders / change — the four blocks CotReport is built from. Asserting all 128
        // would restate the generated property list without adding a check; asserting one per block catches a
        // whole block bound to the wrong suffix.
        var ng = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal("NG", ng.Symbol);
        Assert.Equal(1500882, ng.OpenInterestAll);              // positions
        Assert.Equal(21.7m, ng.PctOfOiNoncommLongOld);          // pct, and an `Ol` -> `Old` rename
        Assert.Equal(155, ng.TradersNoncommSpreadOld);          // traders, and the double-defect field
        Assert.Equal(-28330, ng.ChangeInNoncommSpreadAll);      // change, and a `Spead` -> `Spread` rename
        Assert.Equal("(Contracts of 10,000 MMBTU'S)", ng.ContractUnits);
    }

    [Fact]
    public void The_three_misspellings_bind_from_the_wire_spelling_and_not_the_english_one()
    {
        // If any [JsonPropertyName] is "corrected" to the English spelling, these land null. That is a silent
        // failure — System.Text.Json answers a field it cannot find with null and no error — so it needs a
        // test that names it.
        var report = JsonSerializer.Deserialize(
            """[{"changeInNoncommSpeadAll":-1,"tradersNoncommSpeadOl":-2}]""",
            FmpJsonContext.Default.ListCotReport)![0];
        var analysis = JsonSerializer.Deserialize(
            """[{"netPostion":-3}]""",
            FmpJsonContext.Default.ListCotAnalysis)![0];

        Assert.Equal(-1, report.ChangeInNoncommSpreadAll);
        Assert.Equal(-2, report.TradersNoncommSpreadOld);
        Assert.Equal(-3, analysis.NetPosition);

        // And the correctly-spelled siblings still bind, so the fix is not "spell everything Spead".
        var correct = JsonSerializer.Deserialize(
            """[{"noncommPositionsSpreadAll":4,"tradersNoncommSpreadAll":5}]""",
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal(4, correct.NoncommPositionsSpreadAll);
        Assert.Equal(5, correct.TradersNoncommSpreadAll);
    }

    [Fact]
    public void A_row_carrying_both_the_Ol_and_the_Old_suffix_binds_both()
    {
        // The suffix is `Old` in the positions block and `Ol` in the other three, on the same row. Normalising
        // the ATTRIBUTE to one or the other silently empties 26 properties; normalising the PROPERTY is what
        // this SDK does instead, and this is the test that pins the direction.
        var row = JsonSerializer.Deserialize(
            """[{"openInterestOld":1,"pctOfOpenInterestOl":2,"tradersTotOl":3,"concNetLe8TdrShortOl":4}]""",
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal(1, row.OpenInterestOld);
        Assert.Equal(2, row.PctOfOpenInterestOld);
        Assert.Equal(3, row.TradersTotOld);
        Assert.Equal(4m, row.ConcNetLe8TdrShortOld);
    }

    [Fact]
    public void The_Other_block_carries_real_data_on_the_symbols_that_use_it()
    {
        // 36 of the 128 properties are the `Other` block, and dropping it to save width would silently lose
        // real data: 118 of 545 rows measured 2026-08-29 carry a non-zero value in at least one Other field,
        // across 14 distinct symbols. NG is not one of them and ZC is.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)!;

        Assert.Equal(0, rows[0].TradersTotOther);            // NG — genuinely zero
        Assert.Equal(458, rows[1].TradersTotOther);          // ZC — genuinely populated
        Assert.Equal(325558, rows[1].OpenInterestOther);
        Assert.Equal(26.9m, rows[1].ConcGrossLe4TdrLongOther);
    }

    [Fact]
    public void The_COT_date_carries_a_midnight_time_and_still_parses_to_a_date()
    {
        // "2024-02-27 00:00:00" — 19 characters with a ` 00:00:00` tail, on EVERY row of both COT paths.
        // NullableDateAtMidnightJsonConverter already parses exactly this; the plain-date converter used
        // everywhere else in this slice throws on it. No new converter was written for #40 because of this.
        var report = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)![0];
        var analysis = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)![0];

        Assert.Equal(new LocalDate(2024, 2, 27), report.Date);
        Assert.Equal(new LocalDate(2024, 2, 27), analysis.Date);
    }

    [Fact]
    public void An_analysis_row_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("NG", rows[0].Symbol);
        Assert.Equal("Natural Gas (NG)", rows[0].Name);
        Assert.Equal("ENERGIES", rows[0].Sector);
        Assert.Equal("NAT GAS NYME - NEW YORK MERCANTILE EXCHANGE", rows[0].Exchange);
        Assert.Equal(41.09m, rows[0].CurrentLongMarketSituation);
        Assert.Equal(58.91m, rows[0].CurrentShortMarketSituation);
        Assert.Equal("Bearish", rows[0].MarketSituation);
        Assert.Equal(-141553, rows[0].NetPosition);
        Assert.Equal(-153872, rows[0].PreviousNetPosition);
    }

    [Fact]
    public void ChangeInNetPosition_is_a_percentage_and_the_arithmetic_proves_it()
    {
        // The field sits between two int? position counts and is NOT their difference. Measured across all
        // 545 rows on 2026-08-29, 545 match a percent reading and 4 match an absolute one. A caller who adds
        // it to a position count is wrong by three orders of magnitude and gets no signal — which is why the
        // property is decimal? while its two neighbours are int?, and why this test does the arithmetic.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        foreach (var row in rows)
        {
            var absolute = row.NetPosition!.Value - row.PreviousNetPosition!.Value;
            var percent = 100m * absolute / Math.Abs(row.PreviousNetPosition!.Value);

            Assert.Equal(percent, row.ChangeInNetPosition!.Value, precision: 1);
            Assert.NotEqual(absolute, (int)row.ChangeInNetPosition!.Value);
        }

        // Spelled out on the newest row, so the numbers are readable rather than derived:
        //   -141553 - -153872 = 12319 absolute; 12319 / 153872 = 8.01%; the field says 8.01.
        Assert.Equal(8.01m, rows[0].ChangeInNetPosition);
        Assert.Equal(-12.68m, rows[1].ChangeInNetPosition);
    }

    [Fact]
    public void ReversalTrend_binds_a_real_JSON_boolean()
    {
        // Worth its own test because #31 met the opposite case: `capitalGainsOver200USD` arrives as the
        // STRING "False", which bool? will not bind. Measured 2026-08-29, this one is a real JSON boolean on
        // all 545 rows. The two look identical in documentation and differ on the wire, so each is typed from
        // its own measurement rather than from the other's precedent.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.True(rows[0].ReversalTrend);
        Assert.False(rows[1].ReversalTrend);
    }

    [Fact]
    public void Market_sentiment_keeps_the_leading_space_FMP_sends()
    {
        // " Increasing Bearish" — with the space. Captured rather than trimmed, because trimming here would
        // be the SDK silently disagreeing with the upstream about what the value is, and a caller matching on
        // the string needs to know.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.Equal("Increasing Bullish", rows[0].MarketSentiment);
        Assert.Equal(" Increasing Bearish", rows[1].MarketSentiment);
    }

    [Fact]
    public void A_symbol_row_binds_both_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-list.head.json"),
            FmpJsonContext.Default.ListCotSymbol)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("NG", rows[0].Symbol);
        Assert.Equal("Natural Gas (NG)", rows[0].Name);
    }
}
```

**On `Assert.Empty(Binding.Unbound(rows[0]))` for `CotAnalysis`.** `Binding.Unbound` counts `false` as
*populated* (it is not null, not a blank string, not an empty collection) but counts `0` the same way — so row
0, whose `reversalTrend` is `true`, is the safe row to assert the full set against. Row 1 has
`reversalTrend: false`, which still reads as populated; both would pass. The assertion is on row 0 for
readability, not because row 1 would fail.

- [ ] **Step 3: Run them and watch them fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CotTests"
```

Expected: FAIL — `CS0246` on `CotReport`, `CotAnalysis` and `CotSymbol`.

- [ ] **Step 4: Write `CotReport` — the 128-property record and the eighth CS1591 exemption**

Create `src/FmpDotNet/Models/CotReport.cs`. The property block is reproduced verbatim from the design spec,
which generated it from the captured responses; **copy it, do not retype it**.

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 128 properties below are a flat transcription of the CFTC's own column names as FMP
// relays them: the property name carries the same information a generated one-line summary would, and 128 of
// those would bury the type-level documentation above — which is where this response's real quirks are
// recorded, including the 27 properties whose spelling deliberately differs from the wire.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this project used to, also meant a
// NEW undocumented public member anywhere in the SDK compiled silently. This is the EIGHTH exemption: the
// seven period-shaped fundamentals models from #4, and this one. The zero-warning bar holds everywhere else.
#pragma warning disable CS1591

/// <summary>One week's Commitment of Traders report for one futures contract. From
/// <c>stable/commitment-of-traders-report</c>.
///
/// <para><b>The widest record in this SDK — 128 properties</b>, against <c>FinancialRatios</c> at 66. It is
/// the CFTC's own weekly report relayed field for field: four blocks of positions, percentages, trader counts
/// and week-on-week changes, each split three ways into <c>All</c>, <c>Old</c> and <c>Other</c>.</para>
///
/// <para><b>Twenty-seven property names deliberately differ from their <c>[JsonPropertyName]</c>, because
/// FMP's spelling is wrong.</b> The attribute carries the wire verbatim and the property carries correct
/// English — the same rule under which <c>senateID</c> binds to <c>SenateId</c>. They come in two kinds.
/// <b>Twenty-six</b> are the suffix <c>Ol</c> where the block it belongs to is <c>Old</c>; they are a family
/// rather than accidents, and this paragraph is their comment — repeating it twenty-six times at the
/// declarations would bury the two that are not. <b>Two</b> are the misspelling <c>Spead</c> for
/// <c>Spread</c>, and those carry <c>// sic</c> where they are declared.
/// <c>tradersNoncommSpeadOl</c> is in both counts at once, which is why 26 and 2 total 27 rather than 28.
/// (<c>netPostion</c> on <see cref="CotAnalysis"/> is a third misspelling, on a different record, and is not
/// in this record's twenty-seven.) Do not "fix" an attribute: the property would then bind nothing,
/// silently.</para>
///
/// <para><b>The <c>Other</c> block is 36 of the 128 and is not dead weight.</b> Measured 2026-08-29, 118 of
/// 545 rows carry a non-zero value in at least one <c>Other</c> field, across 14 distinct symbols — the
/// grains and softs, where the CFTC splits old-crop from other-crop. Dropping the block to save width would
/// silently lose real data for those contracts.</para>
///
/// <para><b>The data is about two and a half years stale.</b> Measured 2026-08-29, every COT response on this
/// key — bare, by symbol, and by range — covered 2024-01-02 to 2024-02-27 and nothing later. A caller asking
/// for a recent range gets an empty array with HTTP 200. See
/// <see cref="Endpoints.CotEndpoints.GetReportAsync"/>.</para>
///
/// <para><see cref="Date"/> arrives as <c>"2024-02-27 00:00:00"</c> on every row of both COT paths, which is
/// why it takes <see cref="NullableDateAtMidnightJsonConverter"/> rather than the plain-date converter the
/// rest of this slice uses.</para></summary>
public sealed record CotReport
{
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("sector")] public string? Sector { get; init; }
    [JsonPropertyName("marketAndExchangeNames")] public string? MarketAndExchangeNames { get; init; }
    [JsonPropertyName("cftcContractMarketCode")] public string? CftcContractMarketCode { get; init; }
    [JsonPropertyName("cftcMarketCode")] public string? CftcMarketCode { get; init; }
    [JsonPropertyName("cftcRegionCode")] public string? CftcRegionCode { get; init; }
    [JsonPropertyName("cftcCommodityCode")] public string? CftcCommodityCode { get; init; }
    [JsonPropertyName("openInterestAll")] public int? OpenInterestAll { get; init; }
    [JsonPropertyName("noncommPositionsLongAll")] public int? NoncommPositionsLongAll { get; init; }
    [JsonPropertyName("noncommPositionsShortAll")] public int? NoncommPositionsShortAll { get; init; }
    [JsonPropertyName("noncommPositionsSpreadAll")] public int? NoncommPositionsSpreadAll { get; init; }
    [JsonPropertyName("commPositionsLongAll")] public int? CommPositionsLongAll { get; init; }
    [JsonPropertyName("commPositionsShortAll")] public int? CommPositionsShortAll { get; init; }
    [JsonPropertyName("totReptPositionsLongAll")] public int? TotReptPositionsLongAll { get; init; }
    [JsonPropertyName("totReptPositionsShortAll")] public int? TotReptPositionsShortAll { get; init; }
    [JsonPropertyName("nonreptPositionsLongAll")] public int? NonreptPositionsLongAll { get; init; }
    [JsonPropertyName("nonreptPositionsShortAll")] public int? NonreptPositionsShortAll { get; init; }
    [JsonPropertyName("openInterestOld")] public int? OpenInterestOld { get; init; }
    [JsonPropertyName("noncommPositionsLongOld")] public int? NoncommPositionsLongOld { get; init; }
    [JsonPropertyName("noncommPositionsShortOld")] public int? NoncommPositionsShortOld { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOld")] public int? NoncommPositionsSpreadOld { get; init; }
    [JsonPropertyName("commPositionsLongOld")] public int? CommPositionsLongOld { get; init; }
    [JsonPropertyName("commPositionsShortOld")] public int? CommPositionsShortOld { get; init; }
    [JsonPropertyName("totReptPositionsLongOld")] public int? TotReptPositionsLongOld { get; init; }
    [JsonPropertyName("totReptPositionsShortOld")] public int? TotReptPositionsShortOld { get; init; }
    [JsonPropertyName("nonreptPositionsLongOld")] public int? NonreptPositionsLongOld { get; init; }
    [JsonPropertyName("nonreptPositionsShortOld")] public int? NonreptPositionsShortOld { get; init; }
    [JsonPropertyName("openInterestOther")] public int? OpenInterestOther { get; init; }
    [JsonPropertyName("noncommPositionsLongOther")] public int? NoncommPositionsLongOther { get; init; }
    [JsonPropertyName("noncommPositionsShortOther")] public int? NoncommPositionsShortOther { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOther")] public int? NoncommPositionsSpreadOther { get; init; }
    [JsonPropertyName("commPositionsLongOther")] public int? CommPositionsLongOther { get; init; }
    [JsonPropertyName("commPositionsShortOther")] public int? CommPositionsShortOther { get; init; }
    [JsonPropertyName("totReptPositionsLongOther")] public int? TotReptPositionsLongOther { get; init; }
    [JsonPropertyName("totReptPositionsShortOther")] public int? TotReptPositionsShortOther { get; init; }
    [JsonPropertyName("nonreptPositionsLongOther")] public int? NonreptPositionsLongOther { get; init; }
    [JsonPropertyName("nonreptPositionsShortOther")] public int? NonreptPositionsShortOther { get; init; }
    [JsonPropertyName("changeInOpenInterestAll")] public int? ChangeInOpenInterestAll { get; init; }
    [JsonPropertyName("changeInNoncommLongAll")] public int? ChangeInNoncommLongAll { get; init; }
    [JsonPropertyName("changeInNoncommShortAll")] public int? ChangeInNoncommShortAll { get; init; }
    [JsonPropertyName("changeInNoncommSpeadAll")] public int? ChangeInNoncommSpreadAll { get; init; }  // sic: wire spells it "Spead"
    [JsonPropertyName("changeInCommLongAll")] public int? ChangeInCommLongAll { get; init; }
    [JsonPropertyName("changeInCommShortAll")] public int? ChangeInCommShortAll { get; init; }
    [JsonPropertyName("changeInTotReptLongAll")] public int? ChangeInTotReptLongAll { get; init; }
    [JsonPropertyName("changeInTotReptShortAll")] public int? ChangeInTotReptShortAll { get; init; }
    [JsonPropertyName("changeInNonreptLongAll")] public int? ChangeInNonreptLongAll { get; init; }
    [JsonPropertyName("changeInNonreptShortAll")] public int? ChangeInNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestAll")] public int? PctOfOpenInterestAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongAll")] public decimal? PctOfOiNoncommLongAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortAll")] public decimal? PctOfOiNoncommShortAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadAll")] public decimal? PctOfOiNoncommSpreadAll { get; init; }
    [JsonPropertyName("pctOfOiCommLongAll")] public decimal? PctOfOiCommLongAll { get; init; }
    [JsonPropertyName("pctOfOiCommShortAll")] public decimal? PctOfOiCommShortAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongAll")] public decimal? PctOfOiTotReptLongAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortAll")] public decimal? PctOfOiTotReptShortAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongAll")] public decimal? PctOfOiNonreptLongAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortAll")] public decimal? PctOfOiNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestOl")] public int? PctOfOpenInterestOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommLongOl")] public decimal? PctOfOiNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommShortOl")] public decimal? PctOfOiNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommSpreadOl")] public decimal? PctOfOiNoncommSpreadOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommLongOl")] public decimal? PctOfOiCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommShortOl")] public decimal? PctOfOiCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptLongOl")] public decimal? PctOfOiTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptShortOl")] public decimal? PctOfOiTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptLongOl")] public decimal? PctOfOiNonreptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptShortOl")] public decimal? PctOfOiNonreptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOpenInterestOther")] public int? PctOfOpenInterestOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongOther")] public decimal? PctOfOiNoncommLongOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortOther")] public decimal? PctOfOiNoncommShortOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadOther")] public decimal? PctOfOiNoncommSpreadOther { get; init; }
    [JsonPropertyName("pctOfOiCommLongOther")] public decimal? PctOfOiCommLongOther { get; init; }
    [JsonPropertyName("pctOfOiCommShortOther")] public decimal? PctOfOiCommShortOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongOther")] public decimal? PctOfOiTotReptLongOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortOther")] public decimal? PctOfOiTotReptShortOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongOther")] public decimal? PctOfOiNonreptLongOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortOther")] public decimal? PctOfOiNonreptShortOther { get; init; }
    [JsonPropertyName("tradersTotAll")] public int? TradersTotAll { get; init; }
    [JsonPropertyName("tradersNoncommLongAll")] public int? TradersNoncommLongAll { get; init; }
    [JsonPropertyName("tradersNoncommShortAll")] public int? TradersNoncommShortAll { get; init; }
    [JsonPropertyName("tradersNoncommSpreadAll")] public int? TradersNoncommSpreadAll { get; init; }
    [JsonPropertyName("tradersCommLongAll")] public int? TradersCommLongAll { get; init; }
    [JsonPropertyName("tradersCommShortAll")] public int? TradersCommShortAll { get; init; }
    [JsonPropertyName("tradersTotReptLongAll")] public int? TradersTotReptLongAll { get; init; }
    [JsonPropertyName("tradersTotReptShortAll")] public int? TradersTotReptShortAll { get; init; }
    [JsonPropertyName("tradersTotOl")] public int? TradersTotOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommLongOl")] public int? TradersNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommShortOl")] public int? TradersNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommSpeadOl")] public int? TradersNoncommSpreadOld { get; init; }  // sic: BOTH defects — "Spead" and "Ol"
    [JsonPropertyName("tradersCommLongOl")] public int? TradersCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersCommShortOl")] public int? TradersCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptLongOl")] public int? TradersTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptShortOl")] public int? TradersTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotOther")] public int? TradersTotOther { get; init; }
    [JsonPropertyName("tradersNoncommLongOther")] public int? TradersNoncommLongOther { get; init; }
    [JsonPropertyName("tradersNoncommShortOther")] public int? TradersNoncommShortOther { get; init; }
    [JsonPropertyName("tradersNoncommSpreadOther")] public int? TradersNoncommSpreadOther { get; init; }
    [JsonPropertyName("tradersCommLongOther")] public int? TradersCommLongOther { get; init; }
    [JsonPropertyName("tradersCommShortOther")] public int? TradersCommShortOther { get; init; }
    [JsonPropertyName("tradersTotReptLongOther")] public int? TradersTotReptLongOther { get; init; }
    [JsonPropertyName("tradersTotReptShortOther")] public int? TradersTotReptShortOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongAll")] public decimal? ConcGrossLe4TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortAll")] public decimal? ConcGrossLe4TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongAll")] public decimal? ConcGrossLe8TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortAll")] public decimal? ConcGrossLe8TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe4TdrLongAll")] public decimal? ConcNetLe4TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe4TdrShortAll")] public decimal? ConcNetLe4TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe8TdrLongAll")] public decimal? ConcNetLe8TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe8TdrShortAll")] public decimal? ConcNetLe8TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongOl")] public decimal? ConcGrossLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrShortOl")] public decimal? ConcGrossLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrLongOl")] public decimal? ConcGrossLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrShortOl")] public decimal? ConcGrossLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrLongOl")] public decimal? ConcNetLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrShortOl")] public decimal? ConcNetLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrLongOl")] public decimal? ConcNetLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrShortOl")] public decimal? ConcNetLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrLongOther")] public decimal? ConcGrossLe4TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortOther")] public decimal? ConcGrossLe4TdrShortOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongOther")] public decimal? ConcGrossLe8TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortOther")] public decimal? ConcGrossLe8TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe4TdrLongOther")] public decimal? ConcNetLe4TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe4TdrShortOther")] public decimal? ConcNetLe4TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe8TdrLongOther")] public decimal? ConcNetLe8TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe8TdrShortOther")] public decimal? ConcNetLe8TdrShortOther { get; init; }
    [JsonPropertyName("contractUnits")] public string? ContractUnits { get; init; }
}
```

**The type-level `<see cref="Endpoints.CotEndpoints.GetReportAsync"/>` points at a type Task 7 creates**, and
an unresolvable cref is CS1574, a build error. Write it as plain <c>GetReportAsync</c> in this task and
promote it in Task 7 Step 4.

- [ ] **Step 5: Write `CotAnalysis` and `CotSymbol`**

Create `src/FmpDotNet/Models/CotAnalysis.cs`. These two are documented property by property — no pragma, and
the file must not acquire one:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's reading of one week's Commitment of Traders report for one contract. From
/// <c>stable/commitment-of-traders-analysis</c>.
///
/// <para>Sixteen fields against <see cref="CotReport"/>'s 128: this is the derived view — long/short balance,
/// a sentiment label, and the week-on-week change — where <see cref="CotReport"/> is the raw filing. The two
/// paths answer the same symbols and the same dates.</para>
///
/// <para><b>They do not answer the same amount of history.</b> Measured 2026-08-29 with one symbol and one
/// two-year range, this path answered <b>13 rows</b> and <see cref="CotReport"/> answered <b>105</b> — and
/// both looked equally healthy. See <see cref="Endpoints.CotEndpoints.GetAnalysisAsync"/>.</para></summary>
public sealed record CotAnalysis
{
    /// <summary>The contract symbol — <c>NG</c>, <c>ZC</c>. FMP's own codes, listed by
    /// <see cref="Endpoints.CotEndpoints.GetSymbolsAsync"/>, and not exchange tickers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The report date — the Tuesday the CFTC's positions were taken. Arrives as
    /// <c>"2024-02-27 00:00:00"</c>, hence the midnight converter.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The contract's name — <c>Natural Gas (NG)</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The sector, in FMP's own vocabulary — <c>ENERGIES</c>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange, as the CFTC names it — <c>NAT GAS NYME - NEW YORK MERCANTILE
    /// EXCHANGE</c>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The long side as a percentage of the market this week. Pairs with
    /// <see cref="CurrentShortMarketSituation"/> to 100.</summary>
    [JsonPropertyName("currentLongMarketSituation")] public decimal? CurrentLongMarketSituation { get; init; }

    /// <summary>The short side as a percentage of the market this week.</summary>
    [JsonPropertyName("currentShortMarketSituation")] public decimal? CurrentShortMarketSituation { get; init; }

    /// <summary>FMP's label for this week — <c>Bearish</c>, <c>Bullish</c>. A string and not an enum: the
    /// vocabulary was not enumerated.</summary>
    [JsonPropertyName("marketSituation")] public string? MarketSituation { get; init; }

    /// <summary>The long side as a percentage of the market the previous week.</summary>
    [JsonPropertyName("previousLongMarketSituation")] public decimal? PreviousLongMarketSituation { get; init; }

    /// <summary>The short side as a percentage of the market the previous week.</summary>
    [JsonPropertyName("previousShortMarketSituation")] public decimal? PreviousShortMarketSituation { get; init; }

    /// <summary>FMP's label for the previous week.</summary>
    [JsonPropertyName("previousMarketSituation")] public string? PreviousMarketSituation { get; init; }

    /// <summary>Net non-commercial position this week, in contracts. <b>Bound from the wire name
    /// <c>netPostion</c></b>, which is missing an <c>i</c> — its two siblings below are spelled
    /// correctly.</summary>
    [JsonPropertyName("netPostion")] public int? NetPosition { get; init; }  // sic: wire drops the "i"

    /// <summary>Net non-commercial position the previous week, in contracts.</summary>
    [JsonPropertyName("previousNetPosition")] public int? PreviousNetPosition { get; init; }

    /// <summary><b>A percent change, not a delta — this is the one field on this record that will silently
    /// cost a caller three orders of magnitude.</b>
    ///
    /// <para>It sits between two contract counts and is not their difference. Measured across all 545 rows on
    /// 2026-08-29, <b>545 match a percent reading and 4 match an absolute one</b>. On the newest NG row,
    /// <see cref="NetPosition"/> is −141,553 and <see cref="PreviousNetPosition"/> is −153,872: the
    /// difference is 12,319 and this field reads <c>8.01</c>.</para>
    ///
    /// <para>That is why this property is <see langword="decimal"/> while both its neighbours are
    /// <see langword="int"/>. Adding it to a position count compiles and is wrong.</para></summary>
    [JsonPropertyName("changeInNetPosition")] public decimal? ChangeInNetPosition { get; init; }

    /// <summary>FMP's label for the direction of travel — <c>Increasing Bullish</c>. <b>Sometimes carries a
    /// leading space</b> — <c>" Increasing Bearish"</c>, measured 2026-08-29 — which is kept rather than
    /// trimmed, because trimming would be this SDK disagreeing with the upstream about the value. Trim before
    /// matching.</summary>
    [JsonPropertyName("marketSentiment")] public string? MarketSentiment { get; init; }

    /// <summary>Whether FMP reads the week as a reversal.
    ///
    /// <para><b>A real JSON boolean</b> on all 545 rows measured 2026-08-29, which is worth stating because
    /// #31 met the opposite: <c>CongressionalTrade.CapitalGainsOver200Usd</c> arrives as the <i>string</i>
    /// <c>"False"</c> and is typed <see langword="string"/> for that reason. The two look identical in
    /// documentation and differ on the wire.</para></summary>
    [JsonPropertyName("reversalTrend")] public bool? ReversalTrend { get; init; }
}

/// <summary>One futures contract FMP publishes COT data for. From
/// <c>stable/commitment-of-traders-list</c>.
///
/// <para>The whole universe in one call — <b>65 rows</b> measured 2026-08-29, with no paging and no
/// parameters. This is where a <see cref="CotAnalysis.Symbol"/> comes from, and the codes are FMP's own
/// (<c>NG</c>, <c>ZC</c>, <c>EURGBP</c>) rather than exchange tickers.</para></summary>
public sealed record CotSymbol
{
    /// <summary>The contract code — <c>NG</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The contract's name, with the code repeated in parentheses — <c>Natural Gas
    /// (NG)</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}
```

The two `<see cref="Endpoints.CotEndpoints…"/>` references in this file also point at Task 7's type. Write
them as plain `<c>…</c>` here and promote them in Task 7 Step 4.

- [ ] **Step 6: Register the three records**

Append under the `// Economics, transcripts, ESG and COT (#40).` comment in `FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<CotReport>))]
[JsonSerializable(typeof(List<CotAnalysis>))]
[JsonSerializable(typeof(List<CotSymbol>))]
```

- [ ] **Step 7: Update the csproj comment — seven exemptions becomes eight**

`src/FmpDotNet/FmpDotNet.csproj` currently states, inside the `GenerateDocumentationFile` comment:

> Each of the seven models now carries a file-scoped `#pragma warning disable CS1591` instead, with the count
> and the reasoning at the top of the file. The exemption is visible where it applies and the zero-warning bar
> holds everywhere else

That sentence records *why* CS1591 is not suppressed project-wide, so it is load-bearing rather than
decorative, and it is now wrong by one. Replace "Each of the seven models" with:

```
      Each of those seven models now carries a file-scoped `#pragma warning disable CS1591` instead, with the
      count and the reasoning at the top of the file, and #40 added an eighth — CotReport, 128 properties of
      CFTC column names. The exemption is visible where it applies and the zero-warning bar holds everywhere
      else
```

Read the surrounding paragraph before editing; the wrapping must be preserved and the sentence spans lines.

- [ ] **Step 8: Run the tests**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CotTests"
```

Expected: PASS, 11 tests. If
`A_captured_report_row_binds_all_one_hundred_and_twenty_eight_of_its_fields` fails, read the property names
it lists — each one is an attribute that does not match the wire, and the fixture beside it is the authority.

- [ ] **Step 9: Build with docs on, then commit**

```bash
dotnet build src/FmpDotNet -warnaserror
dotnet test tests/FmpDotNet.Tests
```

Expected: no CS1591 outside the eight pragma'd files, and no CS1574.

```bash
git add src/FmpDotNet/Models/CotReport.cs src/FmpDotNet/Models/CotAnalysis.cs         src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/FmpDotNet.csproj         tests/FmpDotNet.Tests/CotTests.cs tests/FmpDotNet.Tests/Fixtures/commitment-of-traders-*.json
git commit -m "feat: add CotReport, CotAnalysis and CotSymbol (#40)"
```

---

### Task 7: `fmp.Cot`

**Files:**
- Create: `src/FmpDotNet/Endpoints/CotEndpoints.cs`
- Modify: `src/FmpDotNet/Models/CotReport.cs` (promote one cref)
- Modify: `src/FmpDotNet/Models/CotAnalysis.cs` (promote two crefs)
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs` (the property-count assertion, 16 → 17)
- Modify: `tests/FmpDotNet.Tests/CotTests.cs`
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `README.md` (generated block only)

**Interfaces:**
- Consumes: `CotReport`, `CotAnalysis`, `CotSymbol` from Task 6.
- Produces: `public sealed class CotEndpoints(FmpTransport transport)` with
  - `Task<IReadOnlyList<CotReport>> GetReportAsync(string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)`
  - `Task<IReadOnlyList<CotAnalysis>> GetAnalysisAsync(string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)`
  - `Task<IReadOnlyList<CotSymbol>> GetSymbolsAsync(CancellationToken ct = default)`

  `FmpClient.Cot`, and `LiveApi.CotContract`, `LiveApi.CotRangeStart`, `LiveApi.CotRangeEnd`.

**Naming note.** `commitment-of-traders-list` becomes `GetSymbolsAsync`, **not** `GetListAsync`. The wire name
would put a `GetListAsync` on a facade whose transport already has one meaning something entirely different,
and the response is a symbol directory — the same thing `Directory.GetTranscriptSymbolsAsync` returns for
transcripts.

- [ ] **Step 1: Write the failing request-surface tests**

Append to `tests/FmpDotNet.Tests/CotTests.cs`, and add the `using`s the file does not yet have
(`FmpDotNet.Endpoints`, `Microsoft.Extensions.Options`):

```csharp
    // ---- the request surface -----------------------------------------------------------------------------

    private static (CotEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CotEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Every_parameter_on_the_two_dated_paths_is_optional()
    {
        // All three optional on both, and a bare call is legal: measured 2026-08-29 it answered 545 rows on
        // each. Omitted parameters must not reach the wire as empty values.
        var (endpoints, handler) = Build();

        await endpoints.GetReportAsync();

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/commitment-of-traders-report", handler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("symbol=", query);
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (report, reportHandler) = Build();
        await report.GetReportAsync("NG", new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));

        var (analysis, analysisHandler) = Build();
        await analysis.GetAnalysisAsync("NG");

        var (symbols, symbolsHandler) = Build();
        await symbols.GetSymbolsAsync();

        Assert.Equal("/stable/commitment-of-traders-report", reportHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=NG", reportHandler.Requests[0].Query);
        Assert.Contains("from=2024-01-01", reportHandler.Requests[0].Query);
        Assert.Contains("to=2024-03-31", reportHandler.Requests[0].Query);
        Assert.Equal("/stable/commitment-of-traders-analysis", analysisHandler.Requests[0].AbsolutePath);
        Assert.Equal("/stable/commitment-of-traders-list", symbolsHandler.Requests[0].AbsolutePath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_backwards_range_is_refused_before_the_request_goes_out(bool analysis)
    {
        var (endpoints, handler) = Build();
        var from = new LocalDate(2024, 3, 31);
        var to = new LocalDate(2024, 1, 1);

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => analysis
                ? endpoints.GetAnalysisAsync("NG", from, to)
                : endpoints.GetReportAsync("NG", from, to));

        Assert.Equal("to", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 2: Run them and watch them fail**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CotTests"
```

Expected: FAIL — `CS0246` on `CotEndpoints`.

- [ ] **Step 3: Write the facade**

Create `src/FmpDotNet/Endpoints/CotEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>The CFTC's weekly Commitment of Traders report — the filing in full, FMP's reading of it, and the
/// contracts both cover.
///
/// <para><b>The data on this key stops at 2024-02-27.</b> Measured 2026-08-29, every response from both dated
/// paths — bare, by symbol, and by range — covered 2024-01-02 to 2024-02-27 and nothing later, about two and
/// a half years before the measurement date. A caller asking for a recent range gets a well-formed empty
/// array with HTTP 200 and nothing saying why. This is the first thing to check when these methods return
/// nothing.</para>
///
/// <para><b>The two dated paths do not answer the same amount of history for the same question</b>, and both
/// look equally healthy. See <see cref="GetAnalysisAsync"/>.</para>
///
/// <para>Contract codes are FMP's own — <c>NG</c>, <c>ZC</c>, <c>EURGBP</c> — not exchange tickers, and not
/// the equity symbols the rest of this SDK takes. <see cref="GetSymbolsAsync"/> lists all 65.</para></summary>
public sealed class CotEndpoints(FmpTransport transport)
{
    /// <summary>The CFTC's weekly report, field for field — <c>stable/commitment-of-traders-report</c>.
    ///
    /// <para>128 fields per row; see <see cref="CotReport"/> for what they are and for the 27 whose C#
    /// spelling differs from the wire. Measured 2026-08-29, a bare call answered <b>545 rows</b> — nine
    /// weekly dates across the 65 contracts — and one symbol over a two-year range answered <b>105</b>, the
    /// full weekly history in that range with no truncation observed.</para>
    ///
    /// <para><b>Every parameter is optional, and omitting <paramref name="symbol"/> means every
    /// contract.</b> That is a legitimate query and it is also 2.4 MB, measured.</para></summary>
    /// <param name="symbol">The contract code from <see cref="GetSymbolsAsync"/> — <c>NG</c>. Omit for every
    /// contract.</param>
    /// <param name="from">First report date in the range, inclusive. Omit for FMP's default window.</param>
    /// <param name="to">Last report date in the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per contract per weekly report date. Never <see langword="null"/>; an empty list
    /// usually means the range is outside the data rather than that the contract has no filings.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotReport>> GetReportAsync(
        string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-report")
                .With("symbol", symbol).With("from", from).With("to", to),
            FmpJsonContext.Default.ListCotReport, ct);
    }

    /// <summary>FMP's reading of the weekly report — <c>stable/commitment-of-traders-analysis</c>.
    ///
    /// <para><b>This path truncates to 13 rows and its sibling does not.</b> Measured 2026-08-29, same
    /// symbol, same range, issued together:</para>
    /// <list type="table">
    ///   <listheader><term>range (<c>symbol=NG</c>)</term><description>analysis / report</description></listheader>
    ///   <item><term>2024-01-01 … 2024-03-31</term><description>13 rows / 13 rows — identical</description></item>
    ///   <item><term>2024-01-01 … 2024-06-30</term><description><b>13</b> rows, 2024-04-02 onward /
    ///     26 rows, 2024-01-02 onward</description></item>
    ///   <item><term>2023-01-01 … 2024-12-31</term><description><b>13</b> rows, 2024-10-08 onward /
    ///     105 rows, 2023-01-03 onward</description></item>
    /// </list>
    /// <para>Thirteen is a hard cap, the newest rows survive, and the status is 200 with a well-formed array
    /// every time. A caller who asks both for two years of history and joins them on date gets thirteen rows
    /// and no indication that the other 92 were dropped on one side. <b>Ask for a quarter at a time</b>, or
    /// read <see cref="CotReport"/> and derive what you need.</para>
    ///
    /// <para>No row-count guard is added, for the reason
    /// <see cref="EconomicsEndpoints.GetEconomicCalendarAsync"/> sets out: a threshold that caught this would
    /// reject a legitimately short range. Compare <see cref="CotAnalysis.Date"/> against the range you asked
    /// for.</para>
    ///
    /// <para><see cref="CotAnalysis.ChangeInNetPosition"/> is a <b>percentage</b>, not a difference of
    /// contracts, despite sitting between two contract counts.</para></summary>
    /// <param name="symbol">The contract code from <see cref="GetSymbolsAsync"/>. Omit for every
    /// contract.</param>
    /// <param name="from">First report date in the range, inclusive.</param>
    /// <param name="to">Last report date in the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>At most 13 rows per request, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotAnalysis>> GetAnalysisAsync(
        string? symbol = null, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-analysis")
                .With("symbol", symbol).With("from", from).With("to", to),
            FmpJsonContext.Default.ListCotAnalysis, ct);
    }

    /// <summary>Every contract FMP publishes COT data for — <c>stable/commitment-of-traders-list</c>.
    ///
    /// <para>The whole universe in one call: <b>65 rows</b> measured 2026-08-29, no paging, no parameters.
    /// This is where a contract code for <see cref="GetReportAsync"/> and <see cref="GetAnalysisAsync"/>
    /// comes from.</para>
    ///
    /// <para><b>Named <c>GetSymbolsAsync</c> rather than after the path.</b> <c>GetListAsync</c> is what
    /// <see cref="FmpTransport"/> calls its own primitive, and a facade method of that name would read as the
    /// transport rather than as a directory of contracts.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every contract code and name. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CotSymbol>> GetSymbolsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/commitment-of-traders-list"),
            FmpJsonContext.Default.ListCotSymbol, ct);
}
```

- [ ] **Step 4: Promote the three deferred crefs**

`CotEndpoints` now exists. In `src/FmpDotNet/Models/CotReport.cs` and `src/FmpDotNet/Models/CotAnalysis.cs`,
turn the placeholders left by Task 6 into real cross-references:

Find them by grep rather than by eye — a count written here has been wrong before, and the grep cannot be:

```bash
grep -rn '<c>GetReportAsync</c>\|<c>GetAnalysisAsync</c>\|<c>GetSymbolsAsync</c>' src/FmpDotNet/Models/
```

**Scoped to `Models/`, and that matters.** The placeholders are Task 6's, and Task 6 wrote only models — nothing
in `Endpoints/` was ever deferred. A grep across all of `src/` also catches `CotEndpoints.cs`'s own summary,
which says "Named `<c>GetSymbolsAsync</c>` rather than after the path" and pairs it with `<c>GetListAsync</c>`.
Those two are names being discussed as names, and the parallel is the point of the sentence: promoting one of
them to a cref — a method's summary linking to itself — breaks it. Leave both as `<c>`.

Promote **every** hit to its `<see cref="Endpoints.CotEndpoints.X"/>` form — `GetReportAsync` in
`CotReport.cs`, `GetAnalysisAsync` and `GetSymbolsAsync` in `CotAnalysis.cs`. Re-run the grep afterwards and
expect nothing back.

A missed promotion is not a build failure, only a weaker doc, which is why it needs the grep. A *wrong* one is
CS1574, so `dotnet build src/FmpDotNet -warnaserror` is the other half of the check.

- [ ] **Step 5: Wire the facade**

`src/FmpDotNet/FmpClient.cs` — add `CotEndpoints cot` to the primary constructor after `esg`, and:

```csharp
    /// <summary>The CFTC's weekly Commitment of Traders report — who is positioned how in the futures
    /// markets.
    ///
    /// <para>The only group in this SDK keyed on a futures contract code rather than an equity symbol, which
    /// is why it is its own facade and not a corner of <see cref="Quote"/>. Its data is years stale on the
    /// current key — see <see cref="CotEndpoints"/> before reading an empty result as "no
    /// positions".</para></summary>
    public CotEndpoints Cot { get; } = cot;
```

`FmpServiceCollectionExtensions.cs`:

```csharp
        services.TryAddTransient<CotEndpoints>();
```

- [ ] **Step 6: Teach the live sweep three constants it cannot guess**

Without this, `SweepCoverageTests` still passes — `symbol` synthesises to `AAPL`, `from` to `RangeStart` —
and the live sweep records `outcome empty` for both dated COT paths, then matches that baseline green
forever. This is the silent failure `LiveApi.Exchange`, `Cik` and `FilerCik` were each named for, arriving a
fourth way.

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `IndicatorRangeEnd`:

```csharp
    /// <summary>The contract the two dated COT probes ask for — <c>NG</c>, Natural Gas.
    ///
    /// <para><b>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>.</b> <c>Probe.Argument</c> maps any unrecognised string to
    /// <see cref="Symbol"/>, and the COT paths take a <b>futures contract code</b> — FMP's own <c>NG</c>,
    /// <c>ZC</c>, <c>EURGBP</c>, listed by <c>GetSymbolsAsync</c> — not an equity ticker.
    /// <c>symbol=AAPL</c> is not an error there; it is an empty array under HTTP 200.</para>
    ///
    /// <para><c>NG</c> because it answers on both paths at the same range: measured 2026-08-29,
    /// <c>?symbol=NG&amp;from=2024-01-01&amp;to=2024-03-31</c> returned 13 rows from
    /// <c>commitment-of-traders-report</c> and 13 from <c>commitment-of-traders-analysis</c>. Deliberately
    /// not one of the 14 contracts whose <c>Other</c> block is populated: those are the exception rather than
    /// the shape, and the baseline should record the common one.</para></summary>
    public const string CotContract = "NG";

    /// <summary>The start of the window the two dated COT probes ask for — <b>fixed dates</b>, like
    /// <see cref="IndicatorRangeStart"/> and for the same kind of reason.
    ///
    /// <para><b>The COT data on this key stops at 2024-02-27.</b> Measured 2026-08-29, every response from
    /// both dated paths covered 2024-01-02 to 2024-02-27 and nothing later — about two and a half years
    /// earlier. A range computed from today returns an empty array at HTTP 200, so a relative window records
    /// <c>outcome empty</c> on the day it is written and agrees with itself forever.</para>
    ///
    /// <para>2024-01-01 … 2024-03-31 is one quarter, which is the widest window that keeps the two paths
    /// <b>agreeing</b>: measured 2026-08-29 it answered 13 rows from each, while a six-month window answered
    /// 13 from <c>analysis</c> and 26 from <c>report</c>. Thirteen is <c>analysis</c>'s hard cap, so anything
    /// wider records two probes that look inconsistent for a reason that has nothing to do with
    /// drift.</para></summary>
    public static readonly LocalDate CotRangeStart = new(2024, 1, 1);

    /// <summary>The end of <see cref="CotRangeStart"/>'s window.</summary>
    public static readonly LocalDate CotRangeEnd = new(2024, 3, 31);
```

In `tests/FmpDotNet.SmokeTests/Probe.cs`, add to the `string` switch — **before** the `_ => LiveApi.Symbol`
default:

```csharp
                // The COT paths take a futures contract code, not an equity ticker. AAPL answers `[]` with
                // HTTP 200 there, which is the silent green this file's other named constants exist to stop.
                "symbol" when parameter.Member.DeclaringType == typeof(Endpoints.CotEndpoints)
                    => LiveApi.CotContract,
```

and to the `LocalDate` switch, before the general `"from"` fallback:

```csharp
                // The COT data stops at 2024-02-27, so this range is FIXED — see LiveApi.CotRangeStart. One
                // quarter, because it is the widest window over which `analysis` and `report` still agree.
                "from" when parameter.Member.DeclaringType == typeof(Endpoints.CotEndpoints)
                    => LiveApi.CotRangeStart,
                "to" when parameter.Member.DeclaringType == typeof(Endpoints.CotEndpoints)
                    => LiveApi.CotRangeEnd,
```

- [ ] **Step 7: Run everything, regenerate the README, commit**

```bash
dotnet test tests/FmpDotNet.Tests
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
dotnet test
```

Expected: green, with `README.md` gaining an `fmp.Cot` block and the count reaching **178 of 243**. That is
issue #40's whole target; if it reads anything else, a method is missing or one is being counted twice.

```bash
git add -A && git commit -m "feat: add fmp.Cot over the three Commitment of Traders paths (#40)"
```

---

### Task 8: the live guard, the recorded baseline, and the prose

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` (recorded, not hand-written)
- Modify: `README.md` (the hand-maintained prose below the generated block)

**Interfaces:**
- Consumes: everything Tasks 1–7 produced.
- Produces: nothing new in the public API.

**Why this is one task at the end rather than a step in each.** Tasks 3, 4, 5 and 7 each kept
`SweepCoverageTests` *passing* — that suite fails when an argument cannot be synthesised at all. What it
cannot see is an argument that synthesises fine and asks a meaningless question, and the three pins below are
the guard against that. They are written together because they are the same guard, and because two of them
assert a *fixed* date where every other pin in the file asserts a relative one — a departure that needs to be
read as a group to make sense.

The baseline is recorded last for a plain reason: it costs twelve live calls plus the whole existing sweep,
and re-recording it after each of five tasks would spend that five times.

- [ ] **Step 1: Pin the three arguments a reviewer cannot otherwise check**

Append to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`:

```csharp
    [Fact]
    public void The_sweep_asks_the_indicator_path_for_a_window_the_data_actually_covers()
    {
        // Probe.Argument dispatched `from` on the DECLARING TYPE, so every EconomicsEndpoints method got
        // LiveApi.SettledWeekday — a one-day window that is right for the economic calendar and useless for
        // the two paths #40 added beside it. Worse than useless on this one: measured 2026-08-29, every
        // economic-indicators series stops between 2025-10-01 and 2025-11-26, so
        // name=GDP&from=2026-05-23&to=2026-08-21 — the window RangeStart and SettledWeekday produce — answers
        // a well-formed EMPTY ARRAY at HTTP 200. The probe would record `outcome empty` on the day it was
        // written and match that baseline green for ever.
        //
        // This is the only FIXED date range in the sweep, and the inversion is the point: everywhere else a
        // hard-coded date is a suite with an expiry, and here the DATA is what is frozen.
        var indicator = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetIndicatorAsync))!;

        Assert.Equal(LiveApi.IndicatorRangeStart, Probe.Argument(indicator.GetParameters()[1]));
        Assert.Equal(LiveApi.IndicatorRangeEnd, Probe.Argument(indicator.GetParameters()[2]));
        Assert.NotEqual(LiveApi.SettledWeekday, Probe.Argument(indicator.GetParameters()[1]));

        // And the indicator itself must be one that carries rows. EconomicIndicator.Inflation and
        // ThreeMonthCertificateOfDepositRate are valid names that answer an empty array, measured 2026-08-29.
        Assert.Equal(EconomicIndicator.Gdp, Probe.Argument(indicator.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_still_asks_the_economic_calendar_for_a_single_day()
    {
        // The narrowing in #40 must not have cost the calendar its own window. Its doc records a 6-month
        // range returning FEWER rows than the 3-month range inside it, and "the widest range verified intact
        // here is one week" — so a day, with no margin spent.
        //
        // GetTreasuryRatesAsync deliberately does NOT keep the day: it falls through to RangeStart, and 90
        // days answered 62 complete rows on 2026-08-29 where one day answers one.
        var calendar = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync))!;
        var treasury = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetTreasuryRatesAsync))!;

        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(calendar.GetParameters()[0]));
        Assert.Equal(LiveApi.RangeStart, Probe.Argument(treasury.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_asks_the_COT_paths_for_a_contract_code_and_a_range_the_data_covers()
    {
        // Two silent-green traps on one facade. The string arm of Probe.Argument ends in `_ => AAPL`, and the
        // COT paths take a futures contract code — measured 2026-08-29, symbol=AAPL answers `[]` at HTTP 200.
        // And the COT data stops at 2024-02-27, so any relative range answers `[]` too.
        //
        // One quarter and not more: 13 rows is commitment-of-traders-analysis's hard cap, and a wider window
        // records two sibling probes disagreeing for a reason that is not drift.
        var report = typeof(Endpoints.CotEndpoints)
            .GetMethod(nameof(Endpoints.CotEndpoints.GetReportAsync))!;

        Assert.Equal(LiveApi.CotContract, Probe.Argument(report.GetParameters()[0]));
        Assert.NotEqual(LiveApi.Symbol, Probe.Argument(report.GetParameters()[0]));
        Assert.Equal(LiveApi.CotRangeStart, Probe.Argument(report.GetParameters()[1]));
        Assert.Equal(LiveApi.CotRangeEnd, Probe.Argument(report.GetParameters()[2]));
        Assert.True(NodaTime.Period.DaysBetween(LiveApi.CotRangeStart, LiveApi.CotRangeEnd) <= 92,
            "A COT window wider than a quarter makes `analysis` and `report` disagree at `analysis`'s 13-row "
            + "cap, which reads as drift and is not.");
    }
```

Add `using FmpDotNet;` at the top of the file if `EconomicIndicator` does not resolve.

- [ ] **Step 2: Run the keyless suite**

```bash
dotnet test tests/FmpDotNet.SmokeTests
```

Expected: PASS. Every test in `SweepCoverageTests` and `BaselineRecordingTests` runs without
`FMP_API_KEY`; the live probes skip. Thirteen checks now, up from ten.

- [ ] **Step 3: Record the baseline against the live API**

This is the only step in the whole plan that spends the key's quota, and it spends about twelve extra calls
on top of the existing ordinary sweep.

**Read the key from `.env` into this one command's process only.** Do not `source` it and do not `set -a`:
that file has clobbered `PATH` for a whole shell before.

**`.env` is git-ignored, so it exists only in the main checkout — a worktree does not have one.** Resolve it
from the common git dir rather than assuming the working directory holds it; this is correct whether you are
in the main checkout or in a linked worktree:

```bash
ENV_FILE="$(cd "$(git rev-parse --git-common-dir)/.." && pwd -P)/.env"
FMP_API_KEY="$(grep '^FMP_API_KEY=' "$ENV_FILE" | cut -d= -f2-)" \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
    dotnet test tests/FmpDotNet.SmokeTests
```

If `grep` finds nothing, stop rather than running the sweep with an empty key: every live probe would skip,
and `FMPDOTNET_UPDATE_SMOKE_BASELINE=1` would then record a baseline of nothing at all.

**Do not set `FMPDOTNET_SMOKE_BULK`.** None of issue #40's twelve paths is a `*-bulk` path, and FMP's own
throttle message warns that "frequent abuse on this API Endpoint may result in restrictions placed on this
API Key". The cost of that switch is the key, not the minutes.

- [ ] **Step 4: Read the baseline diff before believing it**

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | head -200
grep -c '^outcome empty' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Twelve new blocks must appear, and **every one of them must read `outcome rows`**:

```
[Cot.GetAnalysisAsync]          [Esg.GetBenchmarkAsync]        [Transcripts.GetDatesAsync]
[Cot.GetReportAsync]            [Esg.GetDisclosuresAsync]      [Transcripts.GetLatestAsync]
[Cot.GetSymbolsAsync]           [Esg.GetRatingsAsync]          [Transcripts.GetTranscriptAsync]
[Economics.GetIndicatorAsync]   [Economics.GetMarketRiskPremiumsAsync]
[Economics.GetTreasuryRatesAsync]
```

**`outcome empty` on any of the twelve is a failure of this task, not a result.** It means the probe asked a
question the endpoint cannot answer, and recording it would bake a permanently-green empty baseline into the
suite. The three likely causes, in order:

1. `Economics.GetIndicatorAsync` empty → FMP moved the indicator data. Re-measure where the series now ends
   and move `LiveApi.IndicatorRangeStart`/`End` onto it. **Do not widen the window** — measured 2026-08-29, a
   183-day window answered nothing where the 90-day window inside it answered a row.
2. Either `Cot.*` empty → the COT data moved. Re-measure and move `LiveApi.CotRangeStart`/`End`. Keep the
   span at a quarter or less.
3. `Esg.GetBenchmarkAsync` empty → `LiveApi.SettledYear` has rolled past FMP's coverage. It was 2025 and
   answered 622 rows on 2026-08-29; if 2026 answers nothing, this needs its own constant pinned to a year
   that does, the way `IndicatorRangeStart` is.

Also check the `set`/`null` lines on `[Cot.GetReportAsync]`. All 128 record `set`, including the 36 `…Other`
properties — and that is right even though `NG` is deliberately **not** one of the 14 contracts with a
populated `Other` block. **`set` means the property bound a value, and zero is a value.** FMP sends
`"openInterestOther": 0` rather than omitting the field, so an `int?` binds `0`, which is not null. Recording
`null` here would mean the field had gone *missing*, which would be a real finding.

This is why `CotTests` asserts the `Other` block against a **ZC** fixture instead: the live baseline cannot
tell "present and zero" from "present and meaningful", so proving the block carries real data is a job for a
fixture, not for the sweep.

- [ ] **Step 5: Update the README prose**

The generated block is already right — Task 7 took it to 178 of 243. The prose below it is hand-maintained
and still says 166. Three places:

`README.md:115` is inside the generated block and needs no edit. Below `<!-- END GENERATED -->`:

- "**77 paths remain**, of which **70 are actionable**" → **65 remain**, of which **58 are actionable**. The
  seven `tipranks-*` paths still need a separately-purchased add-on and are still not actionable.
- "the largest group is Economics/Transcripts/ESG/COT (12), then Market Performance (11), News (10) and
  Fundraisers & DCF (10)" → that group is done. The sentence becomes: "the largest group is Market Performance
  (11), then News (10) and Fundraisers & DCF (10); ETF & Mutual Funds, Technical Indicators and Indexes &
  Market Hours carry 9 apiece."
- "166 modelled plus 77 remaining" → "178 modelled plus 65 remaining".
- "tracked as eight issues under the epic, seven of them actionable" → **seven issues, six of them
  actionable**. Check this against the epic before writing it: the count is the number of open coverage
  issues, and #40 closing changes it.

Then confirm the arithmetic holds: 178 + 65 = 243, and 65 − 7 = 58.

- [ ] **Step 6: Full run and commit**

```bash
dotnet build -warnaserror
dotnet test tests/FmpDotNet.Tests
dotnet test tests/FmpDotNet.SmokeTests
```

```bash
git add -A
git commit -m "test: record the twelve #40 paths in the live sweep, and update the README prose"
```

---

## Out of scope

Carried from the spec so an executor does not add these as improvements:

- **Chunking around the row caps.** The SDK does not silently issue multiple requests to work around
  truncation, here or on `economic-calendar`. The caps are documented and the caller chunks.
- **De-duplicating `GetLatestAsync` across pages.** The overlap is documented and the caller de-duplicates;
  hiding it would mean buffering pages and guessing when to stop.
- **Normalising the three transcript records' field names.** Covered in Task 4.
- **A sector filter on `esg-benchmark` applied client-side.** A method parameter that looked like a query
  parameter but was applied locally would misrepresent what the request did.
- **A row-count guard anywhere.** Documented, not guarded — Tasks 3 and 7 both say why.
- **`FinancialRatios.cs`'s header comment**, which says "The 56 properties below" and has 66. Noticed while
  measuring the widest existing record; unrelated to #40 and left alone. `CotReport.cs`'s equivalent comment
  must say 128 and be right.

## Definition of done

- [ ] Twelve paths reachable from `FmpClient`: three new properties (`Transcripts`, `Esg`, `Cot`) and three
      new methods on `Economics`.
- [ ] `README.md`'s generated block reads **178 of FMP's 243 endpoint paths are modelled**, and the prose
      below it agrees: 178 + 65 = 243.
- [ ] `dotnet build -warnaserror` is clean. CS1591 is suppressed in exactly eight files, and the csproj
      comment says eight.
- [ ] Twelve `[JsonSerializable]` entries added; `FmpJsonContext` has no reflection fallback.
- [ ] `baseline-ordinary.txt` carries twelve new blocks and **every one reads `outcome rows`**.
- [ ] `SweepCoverageTests` has thirteen checks and passes without a key.
- [ ] Every trap from the spec's testing table has a test that fails when the trap is reintroduced:
      the three misspellings, the `Ol`/`Old` split, the 23 wire strings, the transport guard, `Cik`'s leading
      zeros, `IndustryRank`'s sentence, `ReversalTrend`'s real boolean, COT's midnight date, the three
      transcript records' divergent field names, `esg-benchmark`'s absent `sector`, and
      `GetTranscriptAsync`'s `quarter`-in/`period`-out mismatch.
- [ ] No fixture contains an API key.
- [ ] `ChangeInNetPosition`'s percentage nature is documented on the property **and** asserted
      arithmetically in a test.
