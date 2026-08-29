# Technical Indicators Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `fmp.TechnicalIndicators` facade covering all nine `stable/technical-indicators/*` paths through one method, taking coverage from 178 to 187 of 243.

**Architecture:** The nine paths are one measured shape — OHLCV plus a single column named after the path segment — so this adds one record, not nine. A `TechnicalIndicator` enum selects the path segment; a separate `TechnicalIndicatorTimeframe` enum supplies the `timeframe` query value. A custom `JsonConverter` binds the six known keys and resolves the indicator from whichever ninth key arrived, which is how one record serves nine differently-named value columns.

**Tech Stack:** .NET 10, System.Text.Json with source generation (`FmpJsonContext`), NodaTime, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-technical-indicators-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-29-technical-indicators-measurements.md`

## Global Constraints

- **`TreatWarningsAsErrors=true` with `GenerateDocumentationFile=true` makes CS1574 — an unresolvable `<see cref>` — a BUILD ERROR.** Never write a cref to a type or member that does not exist yet. Where a doc wants to name something a later task creates, write `<c>Name</c>` and Task 6 promotes it. This is the single most common way these tasks fail to build.
- **CS1591 is not suppressed project-wide.** Every public type and member needs an XML doc comment.
- `[JsonPropertyName]` carries FMP's spelling exactly, including any misspelling.
- `decimal?` for measured floats; `int?` only for counts. Never widen or narrow a measured type to look tidy.
- NodaTime types appear in public signatures only.
- **Never log a built URL and never write an API key into a fixture or a test.** The key travels in the query string.
- **Adding a facade to `FmpClient` is FOUR edits**, and the build or a test fails if any is missed: constructor parameter, property, DI registration in `FmpServiceCollectionExtensions.cs`, and the hard-coded property count in `tests/FmpDotNet.Tests/AddFmpTests.cs:55` (**17 → 18**).
- The assembly declares `IsAotCompatible`, so **all deserialisation goes through `FmpJsonContext`** source generation. A reflection-based `Deserialize` fails the build on IL2026/IL3050.
- **Every measurement quoted in a doc comment carries its date** — every measurement in this plan was taken **2026-08-29**.
- Run `dotnet build -warnaserror` before every commit. Run `dotnet test tests/FmpDotNet.Tests` for unit tests. The smoke project needs a key and is only touched in Task 5.

---

### Task 1: `TechnicalIndicatorTimeframe`

**Files:**
- Create: `src/FmpDotNet/TechnicalIndicatorTimeframe.cs`
- Test: `tests/FmpDotNet.Tests/TechnicalIndicatorTimeframeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum TechnicalIndicatorTimeframe { OneMinute, FiveMinutes, FifteenMinutes, ThirtyMinutes, OneHour, FourHours, OneDay }` and `public static string ToQueryValue(this TechnicalIndicatorTimeframe timeframe)` in `TechnicalIndicatorTimeframeExtensions`, both in namespace `FmpDotNet`.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/TechnicalIndicatorTimeframeTests.cs`:

```csharp
namespace FmpDotNet.Tests;

/// <summary>The timeframe enum, against the seven values measured valid on 2026-08-29.</summary>
public class TechnicalIndicatorTimeframeTests
{
    [Theory]
    [InlineData(TechnicalIndicatorTimeframe.OneMinute, "1min")]
    [InlineData(TechnicalIndicatorTimeframe.FiveMinutes, "5min")]
    [InlineData(TechnicalIndicatorTimeframe.FifteenMinutes, "15min")]
    [InlineData(TechnicalIndicatorTimeframe.ThirtyMinutes, "30min")]
    [InlineData(TechnicalIndicatorTimeframe.OneHour, "1hour")]
    [InlineData(TechnicalIndicatorTimeframe.FourHours, "4hour")]
    [InlineData(TechnicalIndicatorTimeframe.OneDay, "1day")]
    public void Each_member_maps_to_the_value_FMP_accepts(TechnicalIndicatorTimeframe timeframe, string expected) =>
        Assert.Equal(expected, timeframe.ToQueryValue());

    [Fact]
    public void Every_declared_member_has_a_mapping()
    {
        // Guards the reverse direction of the theory above: a member added without a switch arm would otherwise
        // only be caught when a caller happened to pass it.
        foreach (var member in Enum.GetValues<TechnicalIndicatorTimeframe>())
            Assert.False(string.IsNullOrEmpty(member.ToQueryValue()));
    }

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_FMP()
    {
        // Measured 2026-08-29: `1week`, `1month` and `2hour` all answer HTTP 400 with the body
        // `Invalid timeframe provided.` Throwing here spends no call from the key's quota to learn that.
        var undeclared = (TechnicalIndicatorTimeframe)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToQueryValue());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorTimeframeTests`
Expected: FAIL — the build cannot find `TechnicalIndicatorTimeframe`.

- [ ] **Step 3: Write the implementation**

Create `src/FmpDotNet/TechnicalIndicatorTimeframe.cs`:

```csharp
namespace FmpDotNet;

/// <summary>The bar size asked of <c>GetAsync</c> on the technical-indicator paths.
///
/// <para><b>Deliberately not <see cref="ChartInterval"/>, and the reason is measured.</b>
/// <see cref="OneDay"/> is valid here, while <c>stable/historical-chart/1day</c> answered HTTP 404 with the
/// body <c>[]</c> when measured on 2026-08-27. Sharing one enum would either drop the timeframe most callers
/// want, or hand <see cref="Endpoints.ChartEndpoints.GetIntradayAsync"/> a member that breaks it. The six
/// near-identical members are the price of a type whose validity does not depend on which method receives
/// it.</para>
///
/// <para>The two enums also fail differently. There the value is a path segment, so a wrong one is a 404
/// carrying <c>[]</c>. Here it is a <b>query value</b>, so a wrong one is <b>HTTP 400</b> with the body
/// <c>Invalid timeframe provided.</c> — 27 bytes of bare text under a <c>content-type: application/json</c>
/// that is a lie. Measured 2026-08-29 on <c>1week</c>, <c>1month</c> and <c>2hour</c>.</para>
///
/// <para><b>The reachable window depends on the timeframe and is not monotonic in the bar size.</b> Measured
/// 2026-08-29 with a bare call on AAPL at <c>periodLength=10</c>, each member's own summary records what came
/// back. <see cref="FifteenMinutes"/> reached back 51 days while <see cref="ThirtyMinutes"/> reached 28 — an
/// inversion that independently reproduces the one recorded on <see cref="ChartInterval"/> on 2026-08-27 (45
/// days against 30), two days earlier on a different endpoint. No explanation is offered because none was
/// established.</para></summary>
public enum TechnicalIndicatorTimeframe
{
    /// <summary>One-minute bars — wire <c>1min</c>. Measured 2026-08-29: 1170 rows spanning about
    /// <b>2 days</b>.</summary>
    OneMinute,

    /// <summary>Five-minute bars — wire <c>5min</c>. Measured 2026-08-29: 702 rows spanning about
    /// <b>10 days</b>.</summary>
    FiveMinutes,

    /// <summary>Fifteen-minute bars — wire <c>15min</c>. Measured 2026-08-29: 988 rows spanning about
    /// <b>51 days</b> — a wider window than <see cref="ThirtyMinutes"/>. See the note on
    /// <see cref="TechnicalIndicatorTimeframe"/>.</summary>
    FifteenMinutes,

    /// <summary>Thirty-minute bars — wire <c>30min</c>. Measured 2026-08-29: 273 rows spanning about
    /// <b>28 days</b> — narrower than <see cref="FifteenMinutes"/>.</summary>
    ThirtyMinutes,

    /// <summary>Hourly bars — wire <c>1hour</c>. Measured 2026-08-29: 441 rows spanning about
    /// <b>88 days</b>.</summary>
    OneHour,

    /// <summary>Four-hour bars — wire <c>4hour</c>. Measured 2026-08-29: 249 rows spanning about
    /// <b>178 days</b>.</summary>
    FourHours,

    /// <summary>Daily bars — wire <c>1day</c>. Measured 2026-08-29: 1254 rows spanning about <b>5 years</b>.
    ///
    /// <para>The one member with no counterpart on <see cref="ChartInterval"/>, and the reason these are two
    /// types. Daily rows carry <c>00:00:00</c> as their time — see the timestamp note on
    /// <c>TechnicalIndicatorBar</c>.</para></summary>
    OneDay,
}

/// <summary>Conversions for <see cref="TechnicalIndicatorTimeframe"/>.</summary>
public static class TechnicalIndicatorTimeframeExtensions
{
    /// <summary>The value FMP expects in the <c>timeframe</c> query parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this TechnicalIndicatorTimeframe timeframe) => timeframe switch
    {
        TechnicalIndicatorTimeframe.OneMinute => "1min",
        TechnicalIndicatorTimeframe.FiveMinutes => "5min",
        TechnicalIndicatorTimeframe.FifteenMinutes => "15min",
        TechnicalIndicatorTimeframe.ThirtyMinutes => "30min",
        TechnicalIndicatorTimeframe.OneHour => "1hour",
        TechnicalIndicatorTimeframe.FourHours => "4hour",
        TechnicalIndicatorTimeframe.OneDay => "1day",
        _ => throw new ArgumentOutOfRangeException(
            nameof(timeframe), timeframe, "Not a known technical-indicator timeframe."),
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build -warnaserror && dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorTimeframeTests`
Expected: PASS, 9 tests, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/TechnicalIndicatorTimeframe.cs tests/FmpDotNet.Tests/TechnicalIndicatorTimeframeTests.cs
git commit -m "feat: add TechnicalIndicatorTimeframe (#35)"
```

---

### Task 2: `TechnicalIndicator`

**Files:**
- Create: `src/FmpDotNet/TechnicalIndicator.cs`
- Test: `tests/FmpDotNet.Tests/TechnicalIndicatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces, all in namespace `FmpDotNet`: `enum TechnicalIndicator { Adx, Dema, Ema, Rsi, Sma, StandardDeviation, Tema, WilliamsR, Wma }`; and on `TechnicalIndicatorExtensions` — `public static string ToPathSegment(this TechnicalIndicator)`, `public static bool NeedsWarmUp(this TechnicalIndicator)`, `public static int SuggestedWarmUpBars(this TechnicalIndicator, int periodLength)`, `internal static string ToJsonField(this TechnicalIndicator)`, and `internal static bool TryFromJsonField(string field, out TechnicalIndicator indicator)`. Task 3's converter uses the last two.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/TechnicalIndicatorTests.cs`:

```csharp
namespace FmpDotNet.Tests;

/// <summary>The indicator enum, against the nine paths measured on 2026-08-29.</summary>
public class TechnicalIndicatorTests
{
    [Theory]
    [InlineData(TechnicalIndicator.Adx, "adx")]
    [InlineData(TechnicalIndicator.Dema, "dema")]
    [InlineData(TechnicalIndicator.Ema, "ema")]
    [InlineData(TechnicalIndicator.Rsi, "rsi")]
    [InlineData(TechnicalIndicator.Sma, "sma")]
    [InlineData(TechnicalIndicator.StandardDeviation, "standarddeviation")]
    [InlineData(TechnicalIndicator.Tema, "tema")]
    [InlineData(TechnicalIndicator.WilliamsR, "williams")]
    [InlineData(TechnicalIndicator.Wma, "wma")]
    public void Each_member_maps_to_its_path_segment(TechnicalIndicator indicator, string expected) =>
        Assert.Equal(expected, indicator.ToPathSegment());

    [Fact]
    public void The_standard_deviation_segment_and_field_differ_in_case()
    {
        // The one case in nine where the path segment is not the JSON field name. Measured 2026-08-29: the path
        // is all-lowercase `standarddeviation` and the field is camelCase `standardDeviation`. A binder that
        // derives one from the other gets eight right and this one wrong, silently.
        Assert.Equal("standarddeviation", TechnicalIndicator.StandardDeviation.ToPathSegment());
        Assert.Equal("standardDeviation", TechnicalIndicator.StandardDeviation.ToJsonField());
    }

    [Fact]
    public void Every_json_field_round_trips_back_to_its_member()
    {
        foreach (var member in Enum.GetValues<TechnicalIndicator>())
        {
            Assert.True(TechnicalIndicatorExtensions.TryFromJsonField(member.ToJsonField(), out var found));
            Assert.Equal(member, found);
        }
    }

    [Theory]
    [InlineData("date")]
    [InlineData("open")]
    [InlineData("volume")]
    [InlineData("macd")]
    [InlineData("SMA")]
    public void A_field_that_is_not_an_indicator_column_is_rejected(string field)
    {
        // `SMA` is here deliberately: the PATH segment is case-insensitive (measured 2026-08-29, `SMA` returned
        // a byte-identical response to `sma`) but the JSON FIELD is not, and this map reads fields.
        Assert.False(TechnicalIndicatorExtensions.TryFromJsonField(field, out _));
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, true)]
    [InlineData(TechnicalIndicator.Dema, true)]
    [InlineData(TechnicalIndicator.Ema, true)]
    [InlineData(TechnicalIndicator.Tema, true)]
    [InlineData(TechnicalIndicator.Rsi, false)]
    [InlineData(TechnicalIndicator.Sma, false)]
    [InlineData(TechnicalIndicator.StandardDeviation, false)]
    [InlineData(TechnicalIndicator.WilliamsR, false)]
    [InlineData(TechnicalIndicator.Wma, false)]
    public void Warm_up_is_classified_by_measurement_not_by_theory(TechnicalIndicator indicator, bool expected)
    {
        // Rsi is the row that matters. It is recursive by construction — Wilder smoothing — and measured
        // 2026-08-29 it returned values identical to the full series on every row of a 10-row window. Anything
        // that "corrects" this to true is reasoning from a textbook against a measurement.
        Assert.Equal(expected, indicator.NeedsWarmUp());
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, 10, 270)]
    [InlineData(TechnicalIndicator.Adx, 20, 540)]
    [InlineData(TechnicalIndicator.Ema, 10, 40)]
    [InlineData(TechnicalIndicator.Dema, 10, 40)]
    [InlineData(TechnicalIndicator.Tema, 10, 40)]
    [InlineData(TechnicalIndicator.Rsi, 10, 0)]
    [InlineData(TechnicalIndicator.Sma, 10, 0)]
    [InlineData(TechnicalIndicator.StandardDeviation, 10, 0)]
    [InlineData(TechnicalIndicator.WilliamsR, 10, 0)]
    [InlineData(TechnicalIndicator.Wma, 10, 0)]
    public void Suggested_warm_up_follows_the_measured_convergence(
        TechnicalIndicator indicator, int periodLength, int expected) =>
        Assert.Equal(expected, indicator.SuggestedWarmUpBars(periodLength));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Suggested_warm_up_rejects_a_period_below_one(int periodLength) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TechnicalIndicator.Adx.SuggestedWarmUpBars(periodLength));

    [Fact]
    public void An_undeclared_member_throws_rather_than_reaching_FMP()
    {
        // Measured 2026-08-29: an unknown segment such as `macd` answers HTTP 404 with the body `[]` — the
        // success shape on a failure status, which surfaces as an exception naming neither the mistake nor the
        // fix.
        var undeclared = (TechnicalIndicator)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToPathSegment());
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToJsonField());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorTests`
Expected: FAIL — the build cannot find `TechnicalIndicator`.

- [ ] **Step 3: Write the enum**

Create `src/FmpDotNet/TechnicalIndicator.cs`. Note every doc says `<c>GetAsync</c>`, never a cref — the facade does not exist until Task 4, and a cref to it is a build error.

```csharp
namespace FmpDotNet;

/// <summary>The indicator asked of <c>GetAsync</c>, which selects the path segment after
/// <c>stable/technical-indicators/</c>.
///
/// <para>All nine paths return the <b>same shape</b> — <c>date, open, high, low, close, volume</c> plus one
/// column named after the segment. Measured 2026-08-29 across 88 non-empty responses, there were exactly nine
/// distinct key tuples, differing in that one element. That is why this SDK models the nine with one record
/// rather than nine.</para>
///
/// <para><b>Why a closed type when the segment is case-insensitive.</b> Unlike
/// <see cref="EconomicIndicator"/>, where <c>GDP</c> works and <c>gdp</c> does not, casing is forgiving here:
/// measured 2026-08-29, <c>SMA</c> returned a response byte-identical to <c>sma</c>. The enum earns its place
/// for two other reasons. An <i>unknown</i> segment answers <b>HTTP 404 with the body <c>[]</c></b> — the
/// success shape on a failure status, which reaches a caller as an exception naming neither the mistake nor
/// the fix. And this is the only place a caller will read the warm-up behaviour below.</para>
///
/// <para><b>The value FMP returns for a given date depends on the range you asked for.</b> This is the most
/// dangerous measured behaviour on these paths and it is invisible at every layer: the status is 200, the
/// array is well formed, and the numbers are plausible. Measured 2026-08-29 on AAPL at
/// <c>periodLength=10</c>, comparing a ten-row window against the same ten dates inside the 1254-row series,
/// the worst row of each:</para>
/// <list type="table">
///   <listheader><term>indicator</term><description>worst row</description></listheader>
///   <item><term><see cref="Sma"/>, <see cref="Wma"/>, <see cref="WilliamsR"/>,
///     <see cref="StandardDeviation"/>, <see cref="Rsi"/></term><description>0.0000% — exact</description></item>
///   <item><term><see cref="Ema"/></term><description>0.1616%</description></item>
///   <item><term><see cref="Tema"/></term><description>0.1540%</description></item>
///   <item><term><see cref="Dema"/></term><description>0.4021%</description></item>
///   <item><term><see cref="Adx"/></term><description><b>276.9981%</b></description></item>
/// </list>
/// <para>Use <see cref="TechnicalIndicatorExtensions.NeedsWarmUp"/> and
/// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> to act on this. The SDK does <b>not</b> act
/// on it for you: it sends exactly the range it was given.</para></summary>
public enum TechnicalIndicator
{
    /// <summary>Average Directional Index — segment <c>adx</c>, field <c>adx</c>.
    ///
    /// <para><b>The one indicator that is unusable on a short range.</b> Measured 2026-08-29 at
    /// <c>periodLength=10</c>, the newest row of a ten-row window read 57.743123 where the full series read
    /// 15.847068 — an error of <b>264%</b>. Convergence against history depth, newest row: 10 bars 264.377%,
    /// 42 bars 10.876%, 83 bars 0.139%, 145 bars 0.001%, 271 bars exact. Repeated at
    /// <c>periodLength=20</c>: 83 bars 35.6145%, 145 bars 3.3040%, 271 bars 0.0030%, 521 bars exact. Reaching
    /// the full-series value took 271 bars at one period and 521 at the other — about <b>26–27× the
    /// period</b> in both cases.</para></summary>
    Adx,

    /// <summary>Double Exponential Moving Average — segment <c>dema</c>, field <c>dema</c>. Measured
    /// 2026-08-29, worst row of a ten-row window at <c>periodLength=10</c>: <b>0.4021%</b> from the full
    /// series, the largest of the three moving averages that drift.</summary>
    Dema,

    /// <summary>Exponential Moving Average — segment <c>ema</c>, field <c>ema</c>. Measured 2026-08-29, worst
    /// row of a ten-row window at <c>periodLength=10</c>: <b>0.1616%</b> from the full series.</summary>
    Ema,

    /// <summary>Relative Strength Index — segment <c>rsi</c>, field <c>rsi</c>.
    ///
    /// <para><b>Recursive by construction and measured exact.</b> RSI uses Wilder smoothing, so theory says it
    /// carries state from before the window — yet measured 2026-08-29, every row of a ten-row window matched
    /// the full series to every digit. Whatever history FMP buffers ahead of the requested range is enough for
    /// this one. <see cref="TechnicalIndicatorExtensions.NeedsWarmUp"/> reports <see langword="false"/> here,
    /// on the measurement rather than on the textbook.</para></summary>
    Rsi,

    /// <summary>Simple Moving Average — segment <c>sma</c>, field <c>sma</c>. Measured 2026-08-29: exact on
    /// every row of a ten-row window. A sanity check on the column's meaning — at <c>periodLength=1</c> it
    /// equalled <c>close</c> on all 1254 rows.</summary>
    Sma,

    /// <summary>Rolling standard deviation of price — segment <c>standarddeviation</c>, field
    /// <b><c>standardDeviation</c></b>.
    ///
    /// <para><b>The one member in nine whose path segment is not its JSON field name.</b> The segment is
    /// all-lowercase and the field is camelCase, measured 2026-08-29. This is why the SDK holds both mappings
    /// rather than deriving one from the other.</para>
    ///
    /// <para>Measured 2026-08-29 on AAPL, 1254 daily rows at <c>periodLength=10</c>: 0.6703 to 18.9556, and
    /// exact on every row of a ten-row window.</para></summary>
    StandardDeviation,

    /// <summary>Triple Exponential Moving Average — segment <c>tema</c>, field <c>tema</c>. Measured
    /// 2026-08-29, worst row of a ten-row window at <c>periodLength=10</c>: <b>0.1540%</b> from the full
    /// series.</summary>
    Tema,

    /// <summary>Williams %R — segment <c>williams</c>, field <c>williams</c>.
    ///
    /// <para><b>Negative.</b> Measured 2026-08-29 on AAPL, 1254 daily rows at <c>periodLength=10</c> ran from
    /// <b>−99.5844</b> to 0.0000: 1252 strictly negative, two exactly zero, none positive. A model that
    /// assumes indicator columns are non-negative is wrong on this one.</para>
    ///
    /// <para>Named for the indicator rather than the segment, following <see cref="EconomicIndicator"/>, which
    /// renames freely from the wire.</para></summary>
    WilliamsR,

    /// <summary>Weighted Moving Average — segment <c>wma</c>, field <c>wma</c>. Measured 2026-08-29: exact on
    /// every row of a ten-row window.</summary>
    Wma,
}
```

- [ ] **Step 4: Write the extensions**

**The file does not build between Step 3 and this step.** The enum's summary crefs
`TechnicalIndicatorExtensions.NeedsWarmUp`, which the class below defines — building in between raises CS1574.
Finish both steps, then build.

Append to `src/FmpDotNet/TechnicalIndicator.cs`:

```csharp
/// <summary>Conversions and measured warm-up guidance for <see cref="TechnicalIndicator"/>.</summary>
public static class TechnicalIndicatorExtensions
{
    /// <summary>The segment FMP expects after <c>stable/technical-indicators/</c>.
    ///
    /// <para>A path segment rather than a query value, so an unmapped member must throw rather than fall back:
    /// measured 2026-08-29, an unrecognised segment answers <b>HTTP 404 with the body <c>[]</c></b>, which the
    /// transport reports as "FMP answered HTTP 404 (NotFound) with no explanation in the body" — true, and
    /// unhelpful about which argument was wrong.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToPathSegment(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx => "adx",
        TechnicalIndicator.Dema => "dema",
        TechnicalIndicator.Ema => "ema",
        TechnicalIndicator.Rsi => "rsi",
        TechnicalIndicator.Sma => "sma",
        TechnicalIndicator.StandardDeviation => "standarddeviation",
        TechnicalIndicator.Tema => "tema",
        TechnicalIndicator.WilliamsR => "williams",
        TechnicalIndicator.Wma => "wma",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known technical indicator."),
    };

    /// <summary>Whether the value FMP returns for this indicator changes when the requested range narrows.
    ///
    /// <para><see langword="true"/> for <see cref="TechnicalIndicator.Adx"/>,
    /// <see cref="TechnicalIndicator.Dema"/>, <see cref="TechnicalIndicator.Ema"/> and
    /// <see cref="TechnicalIndicator.Tema"/> — the four that drifted when measured 2026-08-29.
    /// <see langword="false"/> for the five that were exact.</para>
    ///
    /// <para><b>Deliberately not called <c>IsRecursive</c>.</b> <see cref="TechnicalIndicator.Rsi"/> is
    /// recursive by construction and measured exact, so a name asserting the textbook property would
    /// contradict the measurement it encodes. This reports what was observed, which is the only thing the SDK
    /// knows.</para></summary>
    public static bool NeedsWarmUp(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx or TechnicalIndicator.Dema
            or TechnicalIndicator.Ema or TechnicalIndicator.Tema => true,
        _ => false,
    };

    /// <summary>How many extra bars to request <i>before</i> the range you actually want, then discard.
    ///
    /// <para><b>This is a recommendation derived from the measurements, not a measured constant</b>, and the
    /// distinction matters. <see cref="TechnicalIndicator.Adx"/> was swept across five window widths at two
    /// periods and reached the full-series value at 271 bars for <c>periodLength=10</c> and 521 for
    /// <c>periodLength=20</c> — about 26–27× in both, which is why 27× is used here.
    /// <see cref="TechnicalIndicator.Ema"/>, <see cref="TechnicalIndicator.Dema"/> and
    /// <see cref="TechnicalIndicator.Tema"/> were measured at two periods but only at the narrow end: worst
    /// row 0.4021% at ten bars, and 0.002% or better by 42 bars. The 4× returned for those is a round number
    /// comfortably past where the error stopped mattering, not a threshold anyone measured.</para>
    ///
    /// <para>Zero for the five measured exact at the narrowest window tested — FMP evidently buffers enough
    /// history ahead of the range for them.</para>
    ///
    /// <para>The SDK never applies this itself. Over-fetching on the caller's behalf would transfer up to 27×
    /// the requested bytes, diverge silently from the request that was made, and could not always succeed
    /// anyway because of the roughly five-year span ceiling on daily bars.</para></summary>
    /// <param name="indicator">The indicator whose warm-up is wanted.</param>
    /// <param name="periodLength">The period the call will use. Must be 1 or greater.</param>
    /// <returns>Extra bars to prepend to the requested range, or zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodLength"/> is less than 1.</exception>
    public static int SuggestedWarmUpBars(this TechnicalIndicator indicator, int periodLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(periodLength, 1);

        return indicator switch
        {
            TechnicalIndicator.Adx => 27 * periodLength,
            TechnicalIndicator.Dema or TechnicalIndicator.Ema or TechnicalIndicator.Tema => 4 * periodLength,
            _ => 0,
        };
    }

    /// <summary>The JSON field carrying this indicator's value.
    ///
    /// <para>Equal to <see cref="ToPathSegment"/> on eight of nine.
    /// <see cref="TechnicalIndicator.StandardDeviation"/> is the exception — segment
    /// <c>standarddeviation</c>, field <c>standardDeviation</c>, measured 2026-08-29.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    internal static string ToJsonField(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx => "adx",
        TechnicalIndicator.Dema => "dema",
        TechnicalIndicator.Ema => "ema",
        TechnicalIndicator.Rsi => "rsi",
        TechnicalIndicator.Sma => "sma",
        TechnicalIndicator.StandardDeviation => "standardDeviation",
        TechnicalIndicator.Tema => "tema",
        TechnicalIndicator.WilliamsR => "williams",
        TechnicalIndicator.Wma => "wma",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known technical indicator."),
    };

    /// <summary>Resolves a JSON field name back to the indicator it carries, for the converter that reads
    /// whichever ninth key arrived. Case-sensitive: the wire field is, even though the path segment is
    /// not.</summary>
    internal static bool TryFromJsonField(string field, out TechnicalIndicator indicator)
    {
        switch (field)
        {
            case "adx": indicator = TechnicalIndicator.Adx; return true;
            case "dema": indicator = TechnicalIndicator.Dema; return true;
            case "ema": indicator = TechnicalIndicator.Ema; return true;
            case "rsi": indicator = TechnicalIndicator.Rsi; return true;
            case "sma": indicator = TechnicalIndicator.Sma; return true;
            case "standardDeviation": indicator = TechnicalIndicator.StandardDeviation; return true;
            case "tema": indicator = TechnicalIndicator.Tema; return true;
            case "williams": indicator = TechnicalIndicator.WilliamsR; return true;
            case "wma": indicator = TechnicalIndicator.Wma; return true;
            default: indicator = default; return false;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build -warnaserror && dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorTests`
Expected: PASS, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/TechnicalIndicator.cs tests/FmpDotNet.Tests/TechnicalIndicatorTests.cs
git commit -m "feat: add TechnicalIndicator with measured warm-up guidance (#35)"
```

---

### Task 3: `TechnicalIndicatorBar` and its converter

**Files:**
- Create: `src/FmpDotNet/Models/TechnicalIndicatorBar.cs`
- Create: `src/FmpDotNet/Serialization/TechnicalIndicatorBarJsonConverter.cs`
- Create: twelve fixtures under `tests/FmpDotNet.Tests/Fixtures/`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Test: `tests/FmpDotNet.Tests/TechnicalIndicatorBarTests.cs`

**Interfaces:**
- Consumes: `TechnicalIndicator`, `TechnicalIndicatorExtensions.ToJsonField`, `TechnicalIndicatorExtensions.TryFromJsonField` from Task 2.
- Produces: `FmpDotNet.Models.TechnicalIndicatorBar` with `LocalDateTime? Timestamp`, `decimal? Open/High/Low/Close/Volume`, `TechnicalIndicator Indicator`, `decimal? Value`; and `FmpJsonContext.Default.ListTechnicalIndicatorBar`, which Task 4 passes to `transport.GetListAsync`.

- [ ] **Step 1: Create the fixtures**

These are trimmed verbatim from responses captured on 2026-08-29. No key appears in any of them. Run from the repository root:

```bash
cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-adx.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "adx": 15.847068140658923},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "adx": 14.899750942432235},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "adx": 16.18350524051887}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-dema.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "dema": 314.43177801929204},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "dema": 311.77475339613636},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "dema": 310.4725436061811}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-ema.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "ema": 313.0116111765074},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "ema": 311.52530254906463},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "ema": 310.84648089330125}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-rsi.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "rsi": 61.425245840267664},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "rsi": 54.45845128870961},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "rsi": 52.76389941813692}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-rsi.BTCUSD.fractional-volume.json <<'JSON'
[
  {"date": "2025-01-25 00:00:00", "open": 104866.13, "high": 105294, "low": 104104, "close": 104733.56, "volume": 32733923279.452477, "rsi": 60.32021542294661},
  {"date": "2025-01-24 00:00:00", "open": 103926.36, "high": 107200, "low": 102751.92, "close": 104850.27, "volume": 66392569619.66874, "rsi": 60.62149789181802}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-sma.AAPL.1hour.head.json <<'JSON'
[
  {"date": "2026-08-28 15:30:00", "open": 319.55, "high": 320.73, "low": 319.5, "close": 319.66, "volume": 2303138, "sma": 318.03900000000004},
  {"date": "2026-08-28 14:30:00", "open": 319.55, "high": 319.99, "low": 319.07, "close": 319.54, "volume": 3074518, "sma": 317.59900000000005},
  {"date": "2026-08-28 13:30:00", "open": 319.78, "high": 320.05, "low": 319.37, "close": 319.54, "volume": 1803407.9791699983, "sma": 317.10299999999995}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-sma.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "sma": 312.1070000000001},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "sma": 310.72999999999996},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "sma": 309.79799999999994}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-standarddeviation.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "standardDeviation": 3.884718908750028},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "standardDeviation": 3.353368455747144},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "standardDeviation": 3.447650794381587}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-tema.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "tema": 316.84426060254566},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "tema": 313.5526272243999},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "tema": 312.0221123732002}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-williams.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "williams": -13.741636644364464},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "williams": -32.87197231833908},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "williams": -37.46571585298957}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-williams.AAPL.range.json <<'JSON'
[
  {"date": "2024-07-03 00:00:00", "open": 220, "high": 221.55, "low": 219.03, "close": 221.55, "volume": 37369801, "williams": 0},
  {"date": "2026-08-12 00:00:00", "open": 305.1, "high": 305.66, "low": 300.57, "close": 302.25, "volume": 41657800, "williams": -93.5251798561151}
]
JSON

cat > tests/FmpDotNet.Tests/Fixtures/technical-indicators-wma.AAPL.head.json <<'JSON'
[
  {"date": "2026-08-28 00:00:00", "open": 316.85, "high": 322.37, "low": 315.45, "close": 319.7, "volume": 38649398, "wma": 313.3681818181818},
  {"date": "2026-08-27 00:00:00", "open": 310.55, "high": 315.4, "low": 309.4, "close": 314.58, "volume": 32419233, "wma": 311.73727272727274},
  {"date": "2026-08-26 00:00:00", "open": 310.3, "high": 315.43, "low": 308.8, "close": 313.45, "volume": 34024500, "wma": 310.86781818181817}
]
JSON
```

- [ ] **Step 2: Write the failing test**

Create `tests/FmpDotNet.Tests/TechnicalIndicatorBarTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The one record the nine technical-indicator paths share, against responses captured live on
/// 2026-08-29.</summary>
public class TechnicalIndicatorBarTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static IReadOnlyList<TechnicalIndicatorBar> Parse(string fixture) =>
        JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListTechnicalIndicatorBar)!;

    [Theory]
    [InlineData("adx", TechnicalIndicator.Adx)]
    [InlineData("dema", TechnicalIndicator.Dema)]
    [InlineData("ema", TechnicalIndicator.Ema)]
    [InlineData("rsi", TechnicalIndicator.Rsi)]
    [InlineData("sma", TechnicalIndicator.Sma)]
    [InlineData("standarddeviation", TechnicalIndicator.StandardDeviation)]
    [InlineData("tema", TechnicalIndicator.Tema)]
    [InlineData("williams", TechnicalIndicator.WilliamsR)]
    [InlineData("wma", TechnicalIndicator.Wma)]
    public void Every_path_binds_its_column_to_Value_and_names_itself(string segment, TechnicalIndicator expected)
    {
        var rows = Parse($"technical-indicators-{segment}.AAPL.head.json");

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            // The indicator is resolved from the field that ARRIVED, not stamped by the caller's argument.
            Assert.Equal(expected, row.Indicator);
            Assert.NotNull(row.Value);
            Assert.NotNull(row.Open);
            Assert.NotNull(row.High);
            Assert.NotNull(row.Low);
            Assert.NotNull(row.Close);
            Assert.NotNull(row.Volume);
            Assert.NotNull(row.Timestamp);
        });
    }

    [Fact]
    public void The_shared_OHLCV_block_is_identical_across_paths()
    {
        // The nine paths are the same price series with one column swapped — measured 2026-08-29, exactly nine
        // distinct key tuples across 88 non-empty responses. If a future change bound OHLCV differently per
        // path, this fails. It is also what justifies one record instead of nine.
        var sma = Parse("technical-indicators-sma.AAPL.head.json");
        var adx = Parse("technical-indicators-adx.AAPL.head.json");

        Assert.Equal(sma.Count, adx.Count);
        for (var i = 0; i < sma.Count; i++)
        {
            Assert.Equal(sma[i].Timestamp, adx[i].Timestamp);
            Assert.Equal(sma[i].Open, adx[i].Open);
            Assert.Equal(sma[i].High, adx[i].High);
            Assert.Equal(sma[i].Low, adx[i].Low);
            Assert.Equal(sma[i].Close, adx[i].Close);
            Assert.Equal(sma[i].Volume, adx[i].Volume);
            Assert.NotEqual(sma[i].Value, adx[i].Value);
        }
    }

    [Fact]
    public void A_daily_row_carries_midnight_and_an_intraday_row_carries_a_real_time()
    {
        // Pins the LocalDateTime decision against a future tidy-up to LocalDate. Measured 2026-08-29: all 1254
        // daily rows are `00:00:00`, and every intraday timeframe carries a real bar time. One property serves
        // both, so it cannot drop the time half.
        var daily = Parse("technical-indicators-sma.AAPL.head.json")[0];
        var hourly = Parse("technical-indicators-sma.AAPL.1hour.head.json")[0];

        Assert.Equal(new LocalDateTime(2026, 8, 28, 0, 0, 0), daily.Timestamp);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 30, 0), hourly.Timestamp);
    }

    [Fact]
    public void A_fractional_volume_survives_on_a_daily_bar()
    {
        // The measurement that forces decimal? rather than long?. EndOfDayBar.Volume is long? because daily
        // EQUITY bars showed no fractions — but measured 2026-08-29, BTCUSD carried 75 fractional volumes
        // across 1825 daily rows. One record serves daily and intraday here, so long? would truncate real data.
        var rows = Parse("technical-indicators-rsi.BTCUSD.fractional-volume.json");

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.Volume);
            Assert.NotEqual(decimal.Truncate(row.Volume!.Value), row.Volume!.Value);
        });
    }

    [Fact]
    public void A_negative_indicator_value_binds()
    {
        // Williams %R is negative by construction. Measured 2026-08-29 on 1254 AAPL daily rows: −99.5844 to
        // 0.0000, none positive. A model assuming non-negative indicator columns is wrong on one of nine.
        var rows = Parse("technical-indicators-williams.AAPL.range.json");

        Assert.Contains(rows, r => r.Value == 0m);
        Assert.Contains(rows, r => r.Value < -90m);
        Assert.All(rows, r => Assert.Equal(TechnicalIndicator.WilliamsR, r.Indicator));
    }

    [Fact]
    public void A_row_with_no_indicator_column_is_rejected()
    {
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void A_row_with_two_indicator_columns_is_rejected()
    {
        // Never observed in 88 captures. If FMP ever answers two, the row is not what this record models and
        // guessing which column the caller meant would be worse than failing.
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3,
              "sma": 1.5, "rsi": 60.0}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void An_unrecognised_column_is_rejected_rather_than_silently_dropped()
    {
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": 1, "high": 2, "low": 1, "close": 2, "volume": 3,
              "macd": 1.5}]
            """;
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar));
    }

    [Fact]
    public void A_null_value_binds_as_null_rather_than_failing()
    {
        // No null was observed in 386,617 field slots on 2026-08-29, but the properties are nullable by house
        // convention and the converter must honour that rather than throw on a shape it merely never saw.
        const string body = """
            [{"date": "2026-08-28 00:00:00", "open": null, "high": null, "low": null, "close": null,
              "volume": null, "sma": null}]
            """;
        var rows = JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListTechnicalIndicatorBar)!;

        Assert.Single(rows);
        Assert.Equal(TechnicalIndicator.Sma, rows[0].Indicator);
        Assert.Null(rows[0].Value);
        Assert.Null(rows[0].Open);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorBarTests`
Expected: FAIL — the build cannot find `TechnicalIndicatorBar`.

- [ ] **Step 4: Write the record**

Create `src/FmpDotNet/Models/TechnicalIndicatorBar.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One bar from <c>stable/technical-indicators/{indicator}</c>, at any of the nine indicators and
/// seven timeframes.
///
/// <para>All nine paths return the same six price fields plus <b>one</b> column named after the path segment,
/// measured 2026-08-29 across 88 non-empty responses carrying exactly nine distinct key tuples. This record
/// holds that column in <see cref="Value"/> and names it in <see cref="Indicator"/>, so one type serves all
/// nine rather than nine types duplicating the price block.</para>
///
/// <para>Rows arrive <b>newest first</b> — strictly descending, no duplicate dates across 1254 daily rows —
/// and the SDK does not re-sort them.</para>
///
/// <para><b>The row does not carry its symbol.</b> No response includes one. A caller fanning out across
/// symbols and concatenating the results cannot tell them apart afterwards, and this SDK does not stamp a
/// field FMP did not send.</para></summary>
[JsonConverter(typeof(TechnicalIndicatorBarJsonConverter))]
public sealed record TechnicalIndicatorBar
{
    /// <summary>When the bar opened, as wall clock with <b>no zone asserted</b>.
    ///
    /// <para>The wire form is <c>"2026-08-28 15:59:00"</c> — space-separated, no offset. On the six intraday
    /// timeframes this is <b>Eastern</b> wall clock, established the same way as
    /// <see cref="IntradayBar.Timestamp"/> and re-measured here on 2026-08-29: bars run 09:30 to 15:59 and
    /// stop, which is the US regular session in New York local time. Read as UTC they would place the market
    /// open at 05:30 ET. Convert through tzdb — never arithmetic on an offset.</para>
    ///
    /// <para><b>On <see cref="TechnicalIndicatorTimeframe.OneDay"/> the time half is padding, not data.</b> All 1254
    /// daily rows measured 2026-08-29 carried <c>00:00:00</c>. That is why this is a
    /// <see cref="LocalDateTime"/> and not the <see cref="Instant"/> that
    /// <see cref="IntradayBar.Timestamp"/> uses: binding a daily row through the Eastern converter would
    /// assert that the bar opened at midnight in New York, which is false, and a daily bar is not an instant
    /// at all. One property honestly serving seven timeframes has to decline to name a zone.</para></summary>
    public LocalDateTime? Timestamp { get; init; }

    /// <summary>The bar's opening price.</summary>
    public decimal? Open { get; init; }

    /// <summary>The bar's highest price.</summary>
    public decimal? High { get; init; }

    /// <summary>The bar's lowest price.</summary>
    public decimal? Low { get; init; }

    /// <summary>The bar's closing price.</summary>
    public decimal? Close { get; init; }

    /// <summary>Shares or contracts traded in the bar.
    ///
    /// <para><see cref="decimal"/>, not <see cref="long"/>, and BTCUSD is why. This SDK types volume both ways
    /// deliberately — <see cref="EndOfDayBar.Volume"/> is <see cref="long"/> because daily equity bars showed
    /// no fractions, while <see cref="IntradayBar.Volume"/> is <see cref="decimal"/> because intraday bars
    /// did. This endpoint serves both from one shape, and the daily case is not safe either: measured
    /// 2026-08-29, BTCUSD carried <b>75 fractional volumes across 1825 daily rows</b>. Rounding to
    /// <see cref="long"/> would invent precision FMP did not send.</para></summary>
    public decimal? Volume { get; init; }

    /// <summary>Which indicator <see cref="Value"/> holds.
    ///
    /// <para><b>Resolved from the column that arrived</b>, not stamped from the argument that was sent. If FMP
    /// ever answers a column other than the one requested, this reports what came back rather than
    /// mislabelling it.</para>
    ///
    /// <para>Not nullable: the column must be present for the row to parse at all, so its absence is a parse
    /// failure rather than a missing value.</para></summary>
    public TechnicalIndicator Indicator { get; init; }

    /// <summary>The indicator's value for this bar.
    ///
    /// <para><b>What this means depends on the range that was requested</b>, for four of the nine indicators.
    /// See <see cref="TechnicalIndicator"/> for the measured error at each, and
    /// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> for how much history to prepend.</para>
    ///
    /// <para>Negative for <see cref="TechnicalIndicator.WilliamsR"/> — measured 2026-08-29 from −99.5844 to
    /// 0.0000.</para></summary>
    public decimal? Value { get; init; }
}
```

- [ ] **Step 5: Write the converter**

Create `src/FmpDotNet/Serialization/TechnicalIndicatorBarJsonConverter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Serialization;

/// <summary>Binds a technical-indicator row, whose value column has a different name on each of the nine
/// paths.
///
/// <para><b>Why a converter rather than nine properties.</b> The column is named after the indicator —
/// <c>sma</c>, <c>adx</c>, <c>standardDeviation</c> and so on — so no single
/// <see cref="JsonPropertyNameAttribute"/> binds it. Declaring all nine as properties would leave eight null
/// on every row and make the caller work out which to read. This reads the six known keys by name and treats
/// <b>the single remaining key</b> as the value, resolving
/// <see cref="TechnicalIndicatorBar.Indicator"/> from that key's name.</para>
///
/// <para>Resolving from the wire is the point: the SDK reports the column that arrived rather than the one
/// that was asked for.</para>
///
/// <para>Throws <see cref="JsonException"/> when a row carries no unrecognised key, more than one, or one that
/// is not an indicator column. None of those was observed across 88 captures on 2026-08-29, and each means the
/// row is not what <see cref="TechnicalIndicatorBar"/> models — guessing would be worse than
/// failing.</para></summary>
public sealed class TechnicalIndicatorBarJsonConverter : JsonConverter<TechnicalIndicatorBar>
{
    // Reused rather than reimplemented, so the measured parsing of FMP's space-separated stamp lives in one
    // place.
    private static readonly NullableLocalDateTimeJsonConverter Timestamps = new();

    /// <inheritdoc/>
    public override TechnicalIndicatorBar Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A technical-indicator row must be a JSON object.");

        LocalDateTime? timestamp = null;
        decimal? open = null, high = null, low = null, close = null, volume = null, value = null;
        TechnicalIndicator? indicator = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a property name in a technical-indicator row.");

            var name = reader.GetString()!;
            reader.Read();

            switch (name)
            {
                case "date":
                    timestamp = Timestamps.Read(ref reader, typeof(LocalDateTime?), options);
                    break;
                case "open": open = ReadDecimal(ref reader); break;
                case "high": high = ReadDecimal(ref reader); break;
                case "low": low = ReadDecimal(ref reader); break;
                case "close": close = ReadDecimal(ref reader); break;
                case "volume": volume = ReadDecimal(ref reader); break;
                default:
                    if (!TechnicalIndicatorExtensions.TryFromJsonField(name, out var found))
                        throw new JsonException(
                            $"'{name}' is not a price field or a known indicator column.");
                    if (indicator is not null)
                        throw new JsonException(
                            $"A technical-indicator row carried two indicator columns: "
                            + $"'{indicator.Value.ToJsonField()}' and '{name}'.");
                    indicator = found;
                    value = ReadDecimal(ref reader);
                    break;
            }
        }

        if (indicator is null)
            throw new JsonException("A technical-indicator row carried no indicator column.");

        return new TechnicalIndicatorBar
        {
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Indicator = indicator.Value,
            Value = value,
        };
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, TechnicalIndicatorBar value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("date");
        Timestamps.Write(writer, value.Timestamp, options);
        WriteDecimal(writer, "open", value.Open);
        WriteDecimal(writer, "high", value.High);
        WriteDecimal(writer, "low", value.Low);
        WriteDecimal(writer, "close", value.Close);
        WriteDecimal(writer, "volume", value.Volume);
        WriteDecimal(writer, value.Indicator.ToJsonField(), value.Value);
        writer.WriteEndObject();
    }

    private static decimal? ReadDecimal(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : reader.GetDecimal();

    private static void WriteDecimal(Utf8JsonWriter writer, string name, decimal? value)
    {
        writer.WritePropertyName(name);
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}
```

- [ ] **Step 6: Register the list type**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add alongside the other `[JsonSerializable]` attributes:

```csharp
[JsonSerializable(typeof(List<TechnicalIndicatorBar>))]
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet build -warnaserror && dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorBarTests`
Expected: PASS, 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/TechnicalIndicatorBar.cs \
        src/FmpDotNet/Serialization/TechnicalIndicatorBarJsonConverter.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/TechnicalIndicatorBarTests.cs \
        tests/FmpDotNet.Tests/Fixtures/technical-indicators-*.json
git commit -m "feat: add TechnicalIndicatorBar and its column-resolving converter (#35)"
```

---

### Task 4: The facade and its wiring

**Files:**
- Create: `src/FmpDotNet/Endpoints/TechnicalIndicatorsEndpoints.cs`
- Modify: `src/FmpDotNet/FmpClient.cs` — constructor parameter and property
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — one `TryAddTransient`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs:55` — `Assert.Equal(17, …)` becomes `18`
- Test: `tests/FmpDotNet.Tests/TechnicalIndicatorsEndpointsTests.cs`

**Interfaces:**
- Consumes: `TechnicalIndicator.ToPathSegment()`, `TechnicalIndicatorTimeframe.ToQueryValue()`, `FmpJsonContext.Default.ListTechnicalIndicatorBar`.
- Produces: `FmpClient.TechnicalIndicators` of type `TechnicalIndicatorsEndpoints`, with the single method
  `Task<IReadOnlyList<TechnicalIndicatorBar>> GetAsync(string symbol, TechnicalIndicator indicator, int periodLength, TechnicalIndicatorTimeframe timeframe, LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)`.

**All four wiring edits are required.** Missing the DI registration breaks `AddFmp`; missing the count in `AddFmpTests` fails that test; missing either `FmpClient` edit fails the build.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/TechnicalIndicatorsEndpointsTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The <c>TechnicalIndicators</c> group, against responses captured live on 2026-08-29.</summary>
public class TechnicalIndicatorsEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (TechnicalIndicatorsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, "adx")]
    [InlineData(TechnicalIndicator.Dema, "dema")]
    [InlineData(TechnicalIndicator.Ema, "ema")]
    [InlineData(TechnicalIndicator.Rsi, "rsi")]
    [InlineData(TechnicalIndicator.Sma, "sma")]
    [InlineData(TechnicalIndicator.StandardDeviation, "standarddeviation")]
    [InlineData(TechnicalIndicator.Tema, "tema")]
    [InlineData(TechnicalIndicator.WilliamsR, "williams")]
    [InlineData(TechnicalIndicator.Wma, "wma")]
    public async Task Each_indicator_reaches_its_own_path(TechnicalIndicator indicator, string segment)
    {
        // One method over nine paths, so the path is the only thing distinguishing the calls. Without this,
        // wiring every indicator to `sma` would pass every other test in this file.
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", indicator, 10, TechnicalIndicatorTimeframe.OneDay);

        Assert.Contains($"stable/technical-indicators/{segment}?", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task The_three_required_parameters_are_always_sent()
    {
        // Measured 2026-08-29: omitting any one answers HTTP 400 with
        // `Query Error: Invalid or missing query parameter - <name>`. There are no server-side defaults.
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", TechnicalIndicator.Rsi, 14, TechnicalIndicatorTimeframe.OneHour);

        var request = handler.Requests[0].ToString();
        Assert.Contains("symbol=AAPL", request);
        Assert.Contains("periodLength=14", request);
        Assert.Contains("timeframe=1hour", request);
    }

    [Fact]
    public async Task An_omitted_range_sends_no_range_parameters()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay);

        var request = handler.Requests[0].ToString();
        Assert.DoesNotContain("from=", request);
        Assert.DoesNotContain("to=", request);
    }

    [Fact]
    public async Task A_supplied_range_is_sent_in_FMPs_date_form()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetAsync(
            "AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay,
            new LocalDate(2026, 8, 17), new LocalDate(2026, 8, 28));

        var request = handler.Requests[0].ToString();
        Assert.Contains("from=2026-08-17", request);
        Assert.Contains("to=2026-08-28", request);
    }

    [Fact]
    public async Task The_response_binds_through_the_shared_record()
    {
        var (endpoints, _) = Build(Fixture("technical-indicators-sma.AAPL.head.json"));
        var rows = await endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay);

        Assert.Equal(3, rows.Count);
        Assert.Equal(TechnicalIndicator.Sma, rows[0].Indicator);
        Assert.Equal(319.7m, rows[0].Close);
        Assert.NotNull(rows[0].Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_period_below_one_throws_before_any_call_is_made(int periodLength)
    {
        // Measured 2026-08-29: FMP answers periodLength=0 and periodLength=-5 with HTTP 200 and `[]`. A caller
        // whose computed period lands on zero would read that as "this symbol has no data" — a plausible,
        // wrong answer bought with a call from their quota.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, periodLength,
                                     TechnicalIndicatorTimeframe.OneDay));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_throws_before_any_call_is_made()
    {
        // Measured 2026-08-29: `from` after `to` answers HTTP 200 with 1254 rows — `to` honoured, `from`
        // silently discarded. A plainly wrong argument would otherwise return a plausible result.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAsync(
                "AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay,
                new LocalDate(2026, 8, 28), new LocalDate(2026, 8, 1)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_unknown_segment_answering_404_with_an_empty_array_still_throws()
    {
        // Measured 2026-08-29: `stable/technical-indicators/macd` answers HTTP 404 with the body `[]` — the
        // SUCCESS shape on a failure status. Passing that through would surface as "no data" instead of
        // "no such indicator". Guards FmpTransport.ReadFailureAsync's array branch for this endpoint.
        var handler = new StubHandler(StubHandler.Json("[]", System.Net.HttpStatusCode.NotFound));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        var error = await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay));
        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task An_invalid_timeframe_answering_400_with_bare_text_keeps_the_sentence()
    {
        // Measured 2026-08-29 on `1week`, `1month` and `2hour`: HTTP 400 with the body
        // `Invalid timeframe provided.` — 27 bytes of bare text under a `content-type: application/json` that
        // is a lie. EnsureSuccessStatusCode would throw that sentence away and report only the status.
        var handler = new StubHandler(
            StubHandler.Json("Invalid timeframe provided.", System.Net.HttpStatusCode.BadRequest));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        var error = await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay));
        Assert.Contains("Invalid timeframe provided.", error.Message);
    }
}
```

`StubHandler.Json(string body, HttpStatusCode status = HttpStatusCode.OK)` already exists at
`tests/FmpDotNet.Tests/StubHandler.cs:20`, and `handler.Requests` is a `List<Uri>` — verified 2026-08-29. No
new test helper is needed.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter TechnicalIndicatorsEndpointsTests`
Expected: FAIL — the build cannot find `TechnicalIndicatorsEndpoints`.

- [ ] **Step 3: Write the facade**

Create `src/FmpDotNet/Endpoints/TechnicalIndicatorsEndpoints.cs`:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's technical-indicator surface — nine indicators over one price series.
///
/// <para><b>Nine paths, one method.</b> Every path returns the same six price fields plus one column named
/// after the path segment, measured 2026-08-29 across 88 non-empty responses carrying exactly nine distinct
/// key tuples. <see cref="TechnicalIndicator"/> selects the path;
/// <see cref="Models.TechnicalIndicatorBar"/> is the shape they share.</para>
///
/// <para><b>This facade computes nothing.</b> It reports what FMP returned, including where that is wrong —
/// see the warm-up note on <see cref="GetAsync"/>.</para></summary>
public sealed class TechnicalIndicatorsEndpoints(FmpTransport transport)
{
    /// <summary>One indicator's series for one symbol —
    /// <c>stable/technical-indicators/{indicator}</c>.
    ///
    /// <para><b>The value FMP returns for a given date depends on the range you ask for.</b> Measured
    /// 2026-08-29 on AAPL at <c>periodLength=10</c>, a ten-row window compared against the same dates in the
    /// 1254-row series: <see cref="TechnicalIndicator.Sma"/>, <see cref="TechnicalIndicator.Wma"/>,
    /// <see cref="TechnicalIndicator.WilliamsR"/>, <see cref="TechnicalIndicator.StandardDeviation"/> and
    /// <see cref="TechnicalIndicator.Rsi"/> were exact on every row, while
    /// <see cref="TechnicalIndicator.Adx"/> was out by <b>264% on the newest row and 277% at worst</b>. The
    /// four that drift warm up from the start of the returned range rather than from a buffer of prior data.
    /// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> says how much history to prepend;
    /// this method does not prepend it for you.</para>
    ///
    /// <para><b>A range wider than the timeframe's ceiling is silently truncated.</b> Each
    /// <see cref="TechnicalIndicatorTimeframe"/> member records its own measured window. On
    /// <see cref="TechnicalIndicatorTimeframe.OneDay"/> the ceiling is a span of about <b>five years anchored
    /// at <paramref name="to"/></b>: measured 2026-08-29, <c>2010-01-01 … 2020-01-01</c> answered 1257 rows
    /// covering only 2015-01-05 onward, and <c>2010-01-01 … 2026-08-28</c> answered 1255 rows covering only
    /// 2021-08-30 onward. There is <b>no history floor</b> — <c>2010-01-01 … 2015-01-01</c> returned that
    /// range in full — so it is a span limit, and the half that vanishes is the older one.</para>
    ///
    /// <para><b>Not guarded, deliberately</b>, for the reason
    /// <see cref="EconomicsEndpoints.GetEconomicCalendarAsync"/> sets out: no row count distinguishes a
    /// truncated window from a genuinely short one. The honest check is positional — did
    /// <see cref="Models.TechnicalIndicatorBar.Timestamp"/> reach both ends of the range you asked
    /// for?</para>
    ///
    /// <para><b>Two more silent answers.</b> A wholly future range returns five years of the past — measured
    /// 2026-08-29, <c>2027-01-01 … 2027-06-01</c> answered byte-identically to a bare call. And a
    /// <paramref name="periodLength"/> longer than the available history is quietly satisfied with less:
    /// <c>periodLength=100000</c> against 1254 bars answered 1254 distinct non-null values, which are
    /// expanding-window averages rather than the average that was asked for. The SDK cannot know how many
    /// bars FMP holds for a symbol, so it sets no upper bound.</para>
    ///
    /// <para>An unknown symbol answers <b>HTTP 200 with an empty array</b>, measured 2026-08-29. Equities,
    /// ETFs, indices, forex and crypto all work; the row count follows the trading calendar, so BTCUSD
    /// returned 1825 daily rows over five years where AAPL returned 1254.</para>
    ///
    /// <para>Rows arrive <b>newest first</b> and are returned exactly as FMP sent them — unsorted,
    /// unfiltered, and not clamped to the requested range.</para></summary>
    /// <param name="symbol">The ticker, futures code or pair to ask about.</param>
    /// <param name="indicator">Which indicator to compute. Selects the path segment.</param>
    /// <param name="periodLength">The indicator's period, in bars. Must be 1 or greater.</param>
    /// <param name="timeframe">The bar size. Determines how far back the data reaches.</param>
    /// <param name="from">First calendar day of the range, inclusive. Omit for the timeframe's default
    /// window.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The bars in the range, newest first, truncated to the timeframe's ceiling. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodLength"/> is less than 1;
    /// <paramref name="to"/> is earlier than <paramref name="from"/>; or <paramref name="indicator"/> or
    /// <paramref name="timeframe"/> is not a declared member. All are checked before the request is sent:
    /// FMP answers a zero or negative period, and a backwards range, with HTTP 200 and a plausible body.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status — including the HTTP 404 carrying
    /// <c>[]</c> that an unrecognised indicator segment produces, and the HTTP 400 carrying
    /// <c>Invalid timeframe provided.</c> that an unrecognised timeframe produces.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<TechnicalIndicatorBar>> GetAsync(
        string symbol,
        TechnicalIndicator indicator,
        int periodLength,
        TechnicalIndicatorTimeframe timeframe,
        LocalDate? from = null,
        LocalDate? to = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfLessThan(periodLength, 1);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest($"stable/technical-indicators/{indicator.ToPathSegment()}")
                .With("symbol", symbol)
                .With("periodLength", periodLength)
                .With("timeframe", timeframe.ToQueryValue())
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListTechnicalIndicatorBar, ct);
    }
}
```

- [ ] **Step 4: Wire it into `FmpClient` — edit 1 of 4, the constructor**

In `src/FmpDotNet/FmpClient.cs`, add `TechnicalIndicatorsEndpoints technicalIndicators` to the primary constructor's parameter list, following the existing formatting.

- [ ] **Step 5: Wire it into `FmpClient` — edit 2 of 4, the property**

In the same file, beside the other facade properties:

```csharp
    /// <summary>Nine technical indicators over one price series —
    /// <see cref="TechnicalIndicatorsEndpoints"/>.
    ///
    /// <para>One method reaches all nine paths. Read
    /// <see cref="TechnicalIndicatorsEndpoints.GetAsync"/> before trusting a value computed over a narrow
    /// range: four of the nine change with the window, and one of them by more than 200%.</para></summary>
    public TechnicalIndicatorsEndpoints TechnicalIndicators { get; } = technicalIndicators;
```

- [ ] **Step 6: Wire it into DI — edit 3 of 4**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, beside the other registrations
(around line 138):

```csharp
        services.TryAddTransient<TechnicalIndicatorsEndpoints>();
```

- [ ] **Step 7: Update the facade count — edit 4 of 4**

In `tests/FmpDotNet.Tests/AddFmpTests.cs:55`, change `Assert.Equal(17, typeof(FmpClient)` to
`Assert.Equal(18, typeof(FmpClient)`.

- [ ] **Step 8: Run the whole unit suite**

Run: `dotnet build -warnaserror && dotnet test tests/FmpDotNet.Tests`
Expected: PASS, 0 warnings. `EndpointCoverageTests` will now fail if the README is stale — that is expected
and Task 6 fixes it. If it fails for any other reason, stop and report.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Endpoints/TechnicalIndicatorsEndpoints.cs \
        src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs \
        tests/FmpDotNet.Tests/TechnicalIndicatorsEndpointsTests.cs
git commit -m "feat: add the TechnicalIndicators facade (#35)"
```

---

### Task 5: Smoke sweep support

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` (regenerated, not hand-edited)

**Interfaces:**
- Consumes: `TechnicalIndicator`, `TechnicalIndicatorTimeframe`, and `FmpClient.TechnicalIndicators` from Tasks 1, 2 and 4.
- Produces: a `[TechnicalIndicators.GetAsync]` block in the baseline.

The sweep is reflection-driven over `FmpClient`'s properties, so the facade is picked up automatically. What
it needs is arguments: `SweepCoverageTests` runs without a key and **fails when `Probe.Argument` meets a
parameter it cannot synthesise.**

- [ ] **Step 1: Add the period constant**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, beside the other constants:

```csharp
    /// <summary>The period used for the technical-indicator sweep. Ten because that is the period every
    /// measurement on 2026-08-29 used, so a baseline diff can be read against the design's tables.</summary>
    public const int IndicatorPeriodLength = 10;
```

- [ ] **Step 2: Add the two enum arms to `Probe`**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, beside the existing `ChartInterval` and `EconomicIndicator` arms:

```csharp
        // Sma rather than Adx: the sweep records shape, and Adx over the sweep's ninety-day window is one of
        // the values this SDK documents as wrong (measured 2026-08-29, 264% out at ten bars). Recording a
        // known-bad number as a healthy baseline would teach the wrong thing to every future diff.
        if (type == typeof(TechnicalIndicator)) return TechnicalIndicator.Sma;

        // OneDay rather than an intraday member: the intraday windows are days wide (measured 2026-08-29,
        // 1min reaches back 2 days), so a ninety-day sweep range would sit entirely outside them for the
        // shorter bars and record `outcome empty` as this endpoint's healthy baseline.
        if (type == typeof(TechnicalIndicatorTimeframe)) return TechnicalIndicatorTimeframe.OneDay;
```

- [ ] **Step 3: Add the `periodLength` arm to the int switch**

In the same file's `if (type == typeof(int))` switch, add:

```csharp
                "periodLength" => LiveApi.IndicatorPeriodLength,
```

No other facade declares a parameter called `periodLength` — verified 2026-08-29 — so this arm needs no
narrowing on declaring type. If a future slice adds one, narrow it then.

- [ ] **Step 4: Verify the sweep can synthesise every argument**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter SweepCoverageTests`
Expected: PASS with no key set. A failure naming an unknown parameter means an arm above is missing.

- [ ] **Step 5: Regenerate the baseline**

This makes **one** live call for the new endpoint. `FMPDOTNET_SMOKE_BULK` is not set and no `*-bulk` path is
touched. Read the key without sourcing the file — sourcing `.env` has clobbered `PATH` for a whole shell
before:

```bash
FMP_API_KEY=$(grep -E '^FMP_API_KEY=' "$(git rev-parse --show-toplevel)/.env" | head -1 | cut -d= -f2- | tr -d '"'"'"'') \
  FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests
```

If the repository is a worktree, `.env` lives in the main checkout — resolve it with
`git rev-parse --git-common-dir` instead of `--show-toplevel`.

- [ ] **Step 6: Check the new block**

Run: `git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

Expected: exactly one new block, `[TechnicalIndicators.GetAsync]`, whose first line is `outcome rows`.

**`outcome empty` is a failure, not a result.** If the block says `outcome empty`, stop: the probe arguments
are wrong, not the endpoint. Do not commit a green-looking empty baseline.

- [ ] **Step 7: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "test: sweep the TechnicalIndicators facade (#35)"
```

---

### Task 6: Documentation — promote the deferred crefs and regenerate the README

**Files:**
- Modify: `src/FmpDotNet/TechnicalIndicator.cs`
- Modify: `src/FmpDotNet/TechnicalIndicatorTimeframe.cs`
- Modify: `src/FmpDotNet/Models/TechnicalIndicatorBar.cs`
- Modify: `src/FmpDotNet/ChartInterval.cs`
- Modify: `README.md` (generated block, machine-written)

**Interfaces:**
- Consumes: `TechnicalIndicatorsEndpoints.GetAsync` and `TechnicalIndicatorBar`, which now exist.
- Produces: nothing new. This task closes the deferred references Tasks 1–3 had to leave as `<c>…</c>`.

- [ ] **Step 1: Find every deferred reference**

Run:

```bash
grep -n '<c>GetAsync</c>\|<c>TechnicalIndicatorBar</c>' \
  src/FmpDotNet/TechnicalIndicator.cs \
  src/FmpDotNet/TechnicalIndicatorTimeframe.cs \
  src/FmpDotNet/Models/TechnicalIndicatorBar.cs
```

Expect **three** hits: two `<c>GetAsync</c>` (one per enum file) and one `<c>TechnicalIndicatorBar</c>` in
`TechnicalIndicatorTimeframe.cs`. `TechnicalIndicatorBar.cs` should have none — its only forward reference,
`TechnicalIndicatorTimeframe.OneDay`, was already a live cref because Task 1 created that type before Task 3
needed it.

Every hit is a placeholder written because the target did not exist when that file was created. Promote each
one; do not promote anything the grep does not find, and do not widen the grep to `src/` — placeholders only
live in these three files, and a wider scope has previously forced a self-referential cref.

- [ ] **Step 2: Promote them**

- `<c>GetAsync</c>` becomes `<see cref="Endpoints.TechnicalIndicatorsEndpoints.GetAsync"/>` in
  `TechnicalIndicator.cs` and `TechnicalIndicatorTimeframe.cs`.
- `<c>TechnicalIndicatorBar</c>` becomes `<see cref="Models.TechnicalIndicatorBar"/>` in
  `TechnicalIndicatorTimeframe.cs`.

- [ ] **Step 3: Cross-reference `ChartInterval`**

The two enums look like duplication and are not. Add to the `ChartInterval` type summary in
`src/FmpDotNet/ChartInterval.cs`:

```csharp
/// <para><b>Not to be merged with <see cref="TechnicalIndicatorTimeframe"/>.</b> That enum carries a seventh
/// member, <see cref="TechnicalIndicatorTimeframe.OneDay"/>, which is valid on the technical-indicator paths
/// and answers HTTP 404 with the body <c>[]</c> here — measured 2026-08-27. The two also fail differently:
/// this one is a path segment, so a wrong value is a 404, while that one is a query value, so a wrong value
/// is HTTP 400 carrying <c>Invalid timeframe provided.</c></para>
```

- [ ] **Step 4: Verify the build still passes**

Run: `dotnet build -warnaserror`
Expected: 0 warnings, 0 errors. **A CS1574 here means a cref was promoted to a name that does not resolve** —
check the namespace qualification rather than reverting to `<c>`.

- [ ] **Step 5: Regenerate the README coverage block**

The block between `<!-- BEGIN GENERATED: endpoint coverage -->` and `<!-- END GENERATED -->` is machine-written.
Never edit it by hand:

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

- [ ] **Step 6: Check the generated diff**

Run: `git diff README.md`

Expected: the headline moves from `**178 of FMP's 243 endpoint paths are modelled.**` to
`**187 of FMP's 243 endpoint paths are modelled.**`, and a new `fmp.TechnicalIndicators` section lists
**nine** rows, every one mapping to `GetAsync`:

```
| `stable/technical-indicators/adx` | `GetAsync` |
| `stable/technical-indicators/dema` | `GetAsync` |
| `stable/technical-indicators/ema` | `GetAsync` |
| `stable/technical-indicators/rsi` | `GetAsync` |
| `stable/technical-indicators/sma` | `GetAsync` |
| `stable/technical-indicators/standarddeviation` | `GetAsync` |
| `stable/technical-indicators/tema` | `GetAsync` |
| `stable/technical-indicators/williams` | `GetAsync` |
| `stable/technical-indicators/wma` | `GetAsync` |
```

**Fewer than nine rows means the coverage generator drove only some enum combinations** — that is the exact
drift `EndpointCoverageTests` exists to catch, and it must be fixed rather than accepted.

- [ ] **Step 7: Run everything**

Run: `dotnet build -warnaserror && dotnet test tests/FmpDotNet.Tests`
Expected: PASS, 0 warnings, `EndpointCoverageTests` green.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/TechnicalIndicator.cs \
        src/FmpDotNet/TechnicalIndicatorTimeframe.cs \
        src/FmpDotNet/Models/TechnicalIndicatorBar.cs \
        src/FmpDotNet/ChartInterval.cs \
        README.md
git commit -m "docs: promote deferred crefs and regenerate coverage to 187 (#35)"
```
