# ETF and Mutual Funds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `fmp.EtfAndFunds` facade covering all nine documented ETF And Mutual Funds paths, taking SDK
coverage from 198 to 207 of 243.

**Architecture:** One facade, nine methods, one per path — the `MarketPerformanceEndpoints` shape, because the
nine paths share no parameter shape worth parameterising over. Ten records for nine paths (`EtfInfoSector` is
the nested array element). **Four new converters**, which is where the work in this slice actually lives: the
group contradicts itself on three fields that share a name across sibling paths, and spells absence four ways.
Nothing here is fixable by the shape of a method except one guard — a comma in `symbol` — so the rest is
converters plus XML documentation that names what the wire does.

**Tech Stack:** .NET 10, C# 13, NodaTime `Instant` and `LocalDate`, source-generated `System.Text.Json` via
`FmpJsonContext`, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-etf-and-funds-design.md` (committed `e10c603`, corrected
`18fec44`)

**Measurements:** `docs/superpowers/specs/2026-08-30-etf-and-funds-measurements.md` (committed `0dbbaef`,
corrected `18fec44`) — every number in this plan traces to that file. **Read the corrected version**: commit
`18fec44` fixes the `funds/disclosure-holders-latest` date spread, which both documents originally recorded as
four dates inside one quarter and which is in fact 19 dates over seven years.

## Global Constraints

- **`TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true`.** A `<see cref="...">` pointing at a type
  that does not exist yet is **CS1574, which is a build error, not a warning.** Use the **deferred-cref
  pattern**: write `<c>EtfAndFundsEndpoints</c>` while the target does not exist, and promote it to a real
  `<see cref>` in the task that creates it. **Task 7 promotes every deferred cref in this plan** and lists them
  by file and line-anchor.
- **CS1591 is not suppressed project-wide.** Every public type, member and parameter needs an XML doc comment.
  Do not add `#pragma warning disable CS1591`; the existing file-scoped exemptions are all wide transcription
  records and nothing in this slice qualifies.
- **The assembly declares `IsAotCompatible`.** Every deserialisation goes through `FmpJsonContext`. A
  reflection-based `JsonSerializer.Deserialize` overload in `src/FmpDotNet` fails the build with IL2026/IL3050.
  (The test project has no trim analyser, so `JsonSerializer.Deserialize(fixture, FmpJsonContext.Default.ListX)`
  there is the same call the SDK makes and is what every existing test uses.)
- **Never state a fact that was not measured.** Every number, date and behaviour in a doc comment must come from
  the measurements file and must carry its date — `measured 2026-08-30`.
- **Never log a built URL and never write one into a fixture.** The API key travels in the query string.
  Fixtures are response bodies only: no URL, no host, no `apikey`.
- **Do not set `FMPDOTNET_SMOKE_BULK`.** FMP's documented warning: "Frequent abuse on this API Endpoint may
  result in restrictions placed on this API Key." No task here needs the bulk sweep.
- **Line length is 120 characters** in `src/` and `tests/`, matching every file already there.
- **`decimal`, never `double`, for every figure.** Measured magnitudes reach `7,434,183,997,921.512` with 17
  significant digits, and the smallest is `1.4210854715202004e-14`. `BulkEndOfDayPrice` is the SDK's single
  deliberate `double` exception and nothing in this slice qualifies.
- **Every property is nullable.** The deserialiser cannot promise a key is present. Two fields are nullable
  because FMP actually sent JSON `null` — `FundDisclosure.Symbol` and `FundShareClass.Address` — and the rest
  follow the house convention; the XML doc distinguishes the two cases the way `MarketMover.Symbol` does.
- **No client-side sorting, no range checks on percentages, no `year` bound, no enums for the categoricals.**
  All four are decisions the spec records with its reasons; re-litigating one in code is a spec violation.

## Ruling carried into this plan: `FundDisclosure.FairValLevel`

The spec says two things about this field that do not agree. Its sentinel table lists the properties that take
`SentinelStringJsonConverter` — "applied to exactly the fields measured to carry a sentinel, and no others" —
and `FairValLevel` is **not** on it. Its "Numeric-string fields stay strings" section then says `EntityOrgType`
and `FairValLevel` both stay `string?` "through `SentinelStringJsonConverter`".

**Ruling: the sentinel table wins.** `FairValLevel` was measured as `"1"` ×3,829, `"2"` ×28, `"3"` ×4 and never
as a sentinel, so it is a plain `string?` with no converter. `EntityOrgType` was measured as `"NULL"` on 1,540
rows and does take the converter. The measured rule — apply the converter where a sentinel was observed — is the
one the spec states as a rule; the other sentence is a generalisation about C# type choice that swept a second
field along with it. **Cost if wrong:** a future capture sends `"N/A"` for `fairValLevel` and a caller sees the
literal string instead of `null` — a documented value, not a silent one, and a one-attribute fix.

## File Structure

**Created (22)**

| file | responsibility |
|---|---|
| `src/FmpDotNet/Models/EtfCountryWeighting.cs` | the country row — the percent-string shape |
| `src/FmpDotNet/Models/EtfSectorWeighting.cs` | the sector row — the number shape, one letter apart in the URL |
| `src/FmpDotNet/Models/EtfHolding.cs` | what an ETF owns; UTC cache stamp; three `""` sentinels |
| `src/FmpDotNet/Models/EtfAssetExposure.cs` | which ETFs own an asset — the reversed path |
| `src/FmpDotNet/Models/EtfInfo.cs` | the fund fact sheet **and** `EtfInfoSector`, its nested element |
| `src/FmpDotNet/Models/FundDisclosure.cs` | one N-PORT holding line, 23 keys, Eastern `acceptedDate` |
| `src/FmpDotNet/Models/FundDisclosureDate.cs` | the fiscal period-ends, and the `year`/`quarter` that select them |
| `src/FmpDotNet/Models/FundHolder.cs` | who holds a security, per holder, per reporting date |
| `src/FmpDotNet/Models/FundShareClass.cs` | an SEC-registered fund share class; six `"NULL"` fields |
| `src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs` | the facade, nine methods |
| `tests/FmpDotNet.Tests/EtfAndFundsTests.cs` | binding, traps, converters, request shapes, guards |
| `tests/FmpDotNet.Tests/Fixtures/etf-*.json`, `funds-*.json` | eleven captures, given verbatim in the tasks below |

**Modified (10)**

| file | change |
|---|---|
| `src/FmpDotNet/Serialization/NodaConverters.cs` | four new converters; two doc additions to existing ones |
| `src/FmpDotNet/Serialization/FmpJsonContext.cs` | nine `[JsonSerializable]` entries |
| `src/FmpDotNet/FmpClient.cs` | constructor parameter and property |
| `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` | one `TryAddTransient` |
| `tests/FmpDotNet.Tests/AddFmpTests.cs` | count **19 → 20** *and* the `Assert.NotNull` line |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | `EtfSymbol` and `FundNameQuery` |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | two `Argument` arms |
| `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` | one pinning test, and the class doc's own counts |
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | nine new outcome blocks, from a live run |
| `README.md` | generated block regenerated; prose arithmetic by hand |

## `EndpointCoverageTests` needs no new argument arm, and must not be given one

Checked against `tests/FmpDotNet.Tests/EndpointCoverageTests.cs:296-347`: its `Argument` maps `symbol` and
`name` to `"AAPL"` (**no comma**, so the new guard does not fire), `year` to `2025` and `quarter` to `3` (inside
1–4, so that guard does not fire either). All nine methods will drive successfully and appear in the table.

This is the opposite of `Probe.Argument` in the smoke project, which talks to the live API and **does** need two
new arms — see Task 8. That harness records what came back; this one records only which path went out.

## Expected red suite between Tasks 6 and 9

**From the moment Task 6 lands until Task 9 regenerates the README,
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` fails.**
That is correct and expected: the generated block still describes 198 endpoints while the code now calls 207.
Tasks 7 and 8 inherit a suite failing for that **one known reason**.

**Any other failing test is a real failure.** Before assuming a red suite is "the known one", run the suite and
confirm the failure count is exactly one and that its name is the test above.

---

### Task 1: `PercentSuffixedDecimalJsonConverter`, and the two weightings records

The two paths are one letter apart in the URL, carry the same idea, and **disagree about the type of the field
they share**. They land in one task because the test that matters compares them.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append at end of file, after `ScalarAsStringJsonConverter`)
- Create: `src/FmpDotNet/Models/EtfCountryWeighting.cs`
- Create: `src/FmpDotNet/Models/EtfSectorWeighting.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two `[JsonSerializable]` entries)
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-country-weightings.SPY.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-sector-weightings.SPY.json`
- Create: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public sealed class FmpDotNet.Serialization.PercentSuffixedDecimalJsonConverter :
  JsonConverter<decimal?>`; `public sealed record FmpDotNet.Models.EtfCountryWeighting` with
  `string? Country`, `decimal? WeightPercentage`; `public sealed record FmpDotNet.Models.EtfSectorWeighting`
  with `string? Symbol`, `string? Sector`, `decimal? WeightPercentage`;
  `FmpJsonContext.Default.ListEtfCountryWeighting` and `.ListEtfSectorWeighting`. Tasks 3, 6 and 7 use these.

- [ ] **Step 1: Write the two fixtures**

Create `tests/FmpDotNet.Tests/Fixtures/etf-country-weightings.SPY.json` — the whole response captured
2026-08-30, nine rows, verbatim:

```json
[{"country":"United States","weightPercentage":"97.52%"},
 {"country":"Ireland","weightPercentage":"1.18%"},
 {"country":"United Kingdom","weightPercentage":"0.44%"},
 {"country":"Switzerland","weightPercentage":"0.31%"},
 {"country":"Singapore","weightPercentage":"0.27%"},
 {"country":"Other","weightPercentage":"0.1%"},
 {"country":"Netherlands","weightPercentage":"0.09%"},
 {"country":"Bermuda","weightPercentage":"0.08%"},
 {"country":"Canada","weightPercentage":"0.02%"}]
```

Create `tests/FmpDotNet.Tests/Fixtures/etf-sector-weightings.SPY.json` — the whole response captured
2026-08-30, twelve rows, verbatim. **Do not reformat the `Cash & Others` exponent**; it is the value the
decimal-scale test pins:

```json
[{"symbol":"SPY","sector":"Basic Materials","weightPercentage":1.62},
 {"symbol":"SPY","sector":"Cash & Others","weightPercentage":1.4210854715202004e-14},
 {"symbol":"SPY","sector":"Communication Services","weightPercentage":9.91},
 {"symbol":"SPY","sector":"Consumer Cyclical","weightPercentage":9.57},
 {"symbol":"SPY","sector":"Consumer Defensive","weightPercentage":4.61},
 {"symbol":"SPY","sector":"Energy","weightPercentage":3.36},
 {"symbol":"SPY","sector":"Financial Services","weightPercentage":12.24},
 {"symbol":"SPY","sector":"Healthcare","weightPercentage":9.1},
 {"symbol":"SPY","sector":"Industrials","weightPercentage":8.16},
 {"symbol":"SPY","sector":"Real Estate","weightPercentage":1.88},
 {"symbol":"SPY","sector":"Technology","weightPercentage":37.4},
 {"symbol":"SPY","sector":"Utilities","weightPercentage":2.15}]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The nine ETF and mutual-fund paths, checked against captures taken live 2026-08-30.</summary>
public class EtfAndFundsTests
{
    [Fact]
    public void A_country_weighting_binds_both_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-country-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfCountryWeighting)!;

        Assert.Equal(9, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("United States", rows[0].Country);
        Assert.Equal(97.52m, rows[0].WeightPercentage);
    }

    [Theory]
    [InlineData("97.52%", "97.52")]
    [InlineData("0.1%", "0.1")]
    [InlineData("0.02%", "0.02")]
    [InlineData("0%", "0")]
    [InlineData("100%", "100")]
    [InlineData("0.01%", "0.01")]
    public void A_country_weight_parses_the_percent_suffix(string wire, string expected)
    {
        // Measured 2026-08-30: 227 of 227 rows on this path sent the weight as a STRING with a trailing `%`,
        // with a varying number of decimals. TolerantDecimalJsonConverter cannot read it —
        // decimal.TryParse("97.52%", NumberStyles.Float, ...) is false — so reaching for the existing converter
        // here would silently null every row on the path. This test fails if that swap is ever made.
        var row = JsonSerializer.Deserialize(
            $$"""[{"country":"X","weightPercentage":"{{wire}}"}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            row.WeightPercentage);
    }

    [Fact]
    public void A_country_weight_is_null_when_it_cannot_be_parsed_and_the_country_survives()
    {
        // The file's standing convention: one bad value costs one field, never the whole response.
        var row = JsonSerializer.Deserialize(
            """[{"country":"Narnia","weightPercentage":"about a third%"}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Null(row.WeightPercentage);
        Assert.Equal("Narnia", row.Country);
    }

    [Fact]
    public void A_country_weight_sent_as_a_bare_number_still_binds()
    {
        // No measured row did this, so it is not a claim about the wire — it is the converter refusing to lose
        // a value it can plainly read if FMP ever normalises the field to match its sibling path.
        var row = JsonSerializer.Deserialize(
            """[{"country":"X","weightPercentage":1.18}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Equal(1.18m, row.WeightPercentage);
    }

    [Fact]
    public void A_sector_weighting_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(12, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal(1.62m, rows[0].WeightPercentage);
    }

    [Fact]
    public void The_sector_weight_is_a_bare_number_and_takes_no_percent_converter()
    {
        // The trap this pins: `weightPercentage` is a NUMBER on stable/etf/sector-weightings and a
        // "97.52%" STRING on stable/etf/country-weightings, measured 2026-08-30. The two records therefore
        // carry different converters on identically-named properties. Giving this one the percent converter
        // would still pass — it reads bare numbers — but giving the country one no converter nulls 227 rows.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"SPY","sector":"Technology","weightPercentage":37.4}]""",
            FmpJsonContext.Default.ListEtfSectorWeighting)![0];

        Assert.Equal(37.4m, row.WeightPercentage);
    }

    [Fact]
    public void The_sectors_are_alphabetical_and_not_ordered_by_weight()
    {
        // Measured 2026-08-30, and it is the surprise in the group: `etf/country-weightings` sorts by weight
        // descending while its sibling `etf/sector-weightings` sorts alphabetically. Nothing re-sorts these
        // client-side, so the <returns> doc reports the measured order and this test holds the report honest.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(
            rows.Select(r => r.Sector).OrderBy(s => s, StringComparer.Ordinal),
            rows.Select(r => r.Sector));
        Assert.NotEqual(
            rows.Select(r => r.WeightPercentage).OrderByDescending(w => w),
            rows.Select(r => r.WeightPercentage));
    }

    [Fact]
    public void The_thirty_place_sector_weight_rounds_and_does_not_throw()
    {
        // 1.4210854715202004e-14 is SPY's `Cash & Others` weight — 2^-46, the residue of a floating-point
        // subtraction. It needs 30 decimal places and decimal has 28. Checked on .NET 10 rather than assumed:
        // System.Text.Json ROUNDS it and does not throw. Recorded here so that nobody later "fixes" this by
        // switching the slice to double, which would round every large figure in the group far more
        // damagingly — `etf/asset-exposure.marketValue` reaches 7,434,183,997,921.512 with 17 significant
        // digits, which double cannot hold.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        var cash = rows.Single(r => r.Sector == "Cash & Others");

        Assert.Equal(0.0000000000000142108547152020m, cash.WeightPercentage);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure**, not a test failure — `FmpJsonContext.Default.ListEtfCountryWeighting` and
`ListEtfSectorWeighting` do not exist (CS1061), and neither do the records.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `ScalarAsStringJsonConverter`:

```csharp

/// <summary>Reads a percentage FMP sends as a string with a trailing <c>%</c> — <c>"97.52%"</c> — as a
/// <see cref="decimal"/>.
///
/// <para><b>Written for <c>stable/etf/country-weightings</c>, which is the only path measured to do this.</b>
/// Measured 2026-08-30, all 227 rows returned across 13 ETFs sent <c>weightPercentage</c> as a quoted string
/// with a trailing <c>%</c> and a varying number of decimals — <c>"97.52%"</c>, <c>"0.1%"</c>, <c>"0%"</c>,
/// <c>"100%"</c>. Its sibling <c>stable/etf/sector-weightings</c>, one letter apart in the URL, sends the
/// identically-named field as a <b>bare JSON number</b>. One name, two wire types, two converters.</para>
///
/// <para><b>Why not <see cref="TolerantDecimalJsonConverter"/>.</b> That converter parses quoted numbers with
/// <c>NumberStyles.Float</c>, and <c>decimal.TryParse("97.52%", NumberStyles.Float, …)</c> is
/// <see langword="false"/> — so it would bind <see langword="null"/> on all 227 rows without failing anything.
/// <c>NumberStyles.AllowTrailingSign</c> does not help either; <c>%</c> is not a sign.</para>
///
/// <para>A bare JSON number passes through unchanged, so a future normalisation of the field costs nothing.
/// An unparseable value becomes <see langword="null"/> rather than throwing, following this file's standing
/// convention that one bad value costs one field rather than the whole response.</para></summary>
public sealed class PercentSuffixedDecimalJsonConverter : JsonConverter<decimal?>
{
    /// <inheritdoc/>
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.TryGetDecimal(out var value) ? value : null;
            case JsonTokenType.String:
                var text = (reader.GetString() ?? "").AsSpan().Trim();
                if (text.Length > 0 && text[^1] == '%') text = text[..^1];
                return decimal.TryParse(
                    text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default:
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        // The wire form, not a bare number: a caller who serialises a row and hands it back to something that
        // expects FMP's own shape gets what FMP sent. Read accepts both, so this cannot round-trip lossily.
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(
            value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
    }
}
```

- [ ] **Step 5: Write `EtfCountryWeighting`**

Create `src/FmpDotNet/Models/EtfCountryWeighting.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One country's share of an ETF's holdings, from <c>stable/etf/country-weightings</c>.
///
/// <para><b>Two keys, and no symbol.</b> Measured 2026-08-30 over 226 rows across 13 ETFs, the shape is exactly
/// <c>country</c> and <c>weightPercentage</c> — the response never names the fund it describes, unlike
/// <see cref="EtfSectorWeighting"/>, which echoes <c>symbol</c> on every row. A caller holding rows from two
/// funds has to keep track of which is which.</para>
///
/// <para><b>The weight arrives as a percent-suffixed STRING here and as a bare number on the sibling
/// path.</b> See <see cref="WeightPercentage"/>.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>ordered by weight, descending</b>. A commodity fund still
/// answers a row rather than an empty list: GLD and SLV each returned one row, <c>"Other"</c> at
/// <c>"100%"</c>, and TLT returned two — <c>"United States"</c> at <c>"98.19%"</c> and <c>"Other"</c> at
/// <c>"1.81%"</c>. Some symbols do answer an empty list at HTTP 200 rather than an error, so the list can
/// still come back empty.</para></summary>
public sealed record EtfCountryWeighting
{
    /// <summary>The country name, as FMP spells it — <c>"United States"</c>, <c>"United Kingdom"</c>. Not an
    /// ISO code, and <c>"Other"</c> is one of the values, so this is not a country vocabulary a caller can
    /// map exhaustively. Nullable because the deserialiser cannot promise a key is present, not because any
    /// measured row omitted it: no row was missing a key across all 226 measured 2026-08-30.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>97.52</c> means 97.52%.
    ///
    /// <para><b>The wire sends this as a string with a trailing <c>%</c></b> — <c>"97.52%"</c>, 227 of 227
    /// rows measured 2026-08-30 — while <see cref="EtfSectorWeighting.WeightPercentage"/>, one letter apart in
    /// the URL, sends a bare JSON number. <see cref="PercentSuffixedDecimalJsonConverter"/> reconciles them, so
    /// both properties mean the same thing to a caller. <b>Do not swap in
    /// <see cref="TolerantDecimalJsonConverter"/></b>: it cannot read a trailing <c>%</c> and would bind
    /// <see langword="null"/> on every row without failing anything.</para></summary>
    [JsonPropertyName("weightPercentage")]
    [JsonConverter(typeof(PercentSuffixedDecimalJsonConverter))]
    public decimal? WeightPercentage { get; init; }
}
```

- [ ] **Step 6: Write `EtfSectorWeighting`**

Create `src/FmpDotNet/Models/EtfSectorWeighting.cs`. Note the deferred cref on `EtfInfoSector` — it is written
as `<c>EtfInfoSector</c>` here and promoted in Task 3, which creates that type:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One sector's share of an ETF's holdings, from <c>stable/etf/sector-weightings</c>.
///
/// <para><b>This data is also inside <c>EtfInfo</c>, under a different pair of key names.</b> Measured
/// 2026-08-30, <c>etf/info.sectorsList</c> and this path agreed on the key set and on <b>every value</b>, with
/// no rounding difference, on all 13 ETFs cross-checked — including SPY's and VOO's 12-element lists, QQQ's
/// 11-element list, and the 1-element lists of GLD, SLV, TLT and BND. The nested objects spell the same two
/// facts <c>industry</c> and <c>exposure</c>; see <c>EtfInfoSector</c>. So a caller who already has an
/// <c>EtfInfo</c> does not need this path, and the duplication in this SDK is deliberate rather than an
/// oversight — the two wire shapes cannot share one record, because System.Text.Json binds one
/// <see cref="JsonPropertyNameAttribute"/> per property.</para>
///
/// <para><b>Ordered alphabetically by sector, not by weight</b>, measured 2026-08-30 — the opposite of
/// <see cref="EtfCountryWeighting"/>, which looks like its matched pair and sorts by weight descending.</para>
///
/// <para>Twelve sectors is the measured maximum. A commodity fund answers one row, <c>Cash &amp; Others</c>
/// — GLD, SLV and TLT all did.</para></summary>
public sealed record EtfSectorWeighting
{
    /// <summary>The fund, echoed on every row. Measured 2026-08-30 it was constant across every row of all
    /// 13 responses. Nullable for the reason on <see cref="EtfCountryWeighting.Country"/>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The sector name — <c>"Basic Materials"</c>, <c>"Technology"</c>, <c>"Cash &amp; Others"</c>.
    ///
    /// <para>A free string rather than the SDK's <see cref="Sector"/> enum, and the reason is
    /// <c>Cash &amp; Others</c>: it is not a sector, it is the residual, and it appeared on all 13 ETFs
    /// measured 2026-08-30. An enum here would have to invent a member for it or lose the row.</para></summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>37.4</c> means 37.4%.
    ///
    /// <para><b>A bare JSON number here, and a <c>"97.52%"</c> string on
    /// <see cref="EtfCountryWeighting.WeightPercentage"/></b>, measured 2026-08-30. That is why one of the two
    /// properties carries a converter and this one does not.</para>
    ///
    /// <para><b><see cref="decimal"/>, and it must stay <see cref="decimal"/>.</b> SPY's
    /// <c>Cash &amp; Others</c> weight measured <c>1.4210854715202004e-14</c> — 2⁻⁴⁶, the residue of a
    /// floating-point subtraction — which needs 30 decimal places where <see cref="decimal"/> has 28.
    /// Checked on .NET 10 rather than assumed: System.Text.Json <b>rounds it to 28 places and does not
    /// throw</b>, losing about 4e-31 of a percentage point on a value that is already numerical noise.
    /// Switching this slice to <see cref="double"/> to "fix" that would round every large figure in the group
    /// far more damagingly — <c>EtfAssetExposure.MarketValue</c> reaches 7,434,183,997,921.512 with 17
    /// significant digits.</para></summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }
}
```

**Deferred crefs introduced by this task** — three, all written as `<c>…</c>` above, all promoted in Task 7:
`EtfInfoSector` and `EtfInfo` (Task 3 creates them) and `EtfAssetExposure.MarketValue` (Task 2 creates it).
Writing any of the three as a real `<see cref>` now is **CS1574, a build error**.

- [ ] **Step 7: Register both records with the source generator**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add two lines to the end of the
`[JsonSerializable]` list (after the last existing entry, keeping the file's one-per-line style):

```csharp
[JsonSerializable(typeof(List<EtfCountryWeighting>))]
[JsonSerializable(typeof(List<EtfSectorWeighting>))]
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS, 9 tests (the `[Theory]` contributes 6 cases).

- [ ] **Step 9: Run the whole suite**

Run: `dotnet test`
Expected: PASS. Nothing is wired to the client yet, so `EndpointCoverageTests` is still consistent.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/EtfCountryWeighting.cs src/FmpDotNet/Models/EtfSectorWeighting.cs \
        tests/FmpDotNet.Tests/EtfAndFundsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/etf-country-weightings.SPY.json \
        tests/FmpDotNet.Tests/Fixtures/etf-sector-weightings.SPY.json
git commit -m "feat: read the percent-suffixed weight, and the two ETF weightings shapes (#34)"
```

---

### Task 2: `SentinelStringJsonConverter`, and the two holdings records

The largest decision in the slice, and the two records that first need it. `stable/etf/holdings` is also where
the UTC reading of `updatedAt` is pinned — the falsification that settled it is reproduced in a test.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append after `PercentSuffixedDecimalJsonConverter`)
- Create: `src/FmpDotNet/Models/EtfHolding.cs`
- Create: `src/FmpDotNet/Models/EtfAssetExposure.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two entries)
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-holdings.SPY.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-holdings.BND.sentinels.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-asset-exposure.SPY.head.json`
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs` (append tests inside the existing class)

**Interfaces:**
- Consumes: nothing from Task 1 — independent records; the two shared files are only appended to.
- Produces: `public sealed class FmpDotNet.Serialization.SentinelStringJsonConverter : JsonConverter<string?>`;
  `public sealed record FmpDotNet.Models.EtfHolding` with `string? Symbol`, `string? Asset`, `string? Name`,
  `string? Isin`, `string? SecurityCusip`, `decimal? SharesNumber`, `decimal? WeightPercentage`,
  `decimal? MarketValue`, `Instant? UpdatedAt`; `public sealed record FmpDotNet.Models.EtfAssetExposure` with
  `string? Symbol`, `string? Asset`, `decimal? SharesNumber`, `decimal? WeightPercentage`,
  `decimal? MarketValue`; `FmpJsonContext.Default.ListEtfHolding` and `.ListEtfAssetExposure`.
  **Tasks 4, 5, 6 and 7 all use `SentinelStringJsonConverter`.**

- [ ] **Step 1: Write the three fixtures**

Create `tests/FmpDotNet.Tests/Fixtures/etf-holdings.SPY.head.json` — the first three rows of the 505-row
response captured 2026-08-30, verbatim:

```json
[{"symbol":"SPY","asset":"NVDA","name":"NVIDIA CORP","isin":"US67066G1040","securityCusip":"67066G104",
  "sharesNumber":296861422,"weightPercentage":8.29427804,"marketValue":67656626530,
  "updatedAt":"2026-08-29 13:47:36"},
 {"symbol":"SPY","asset":"AAPL","name":"APPLE INC","isin":"US0378331005","securityCusip":"037833100",
  "sharesNumber":180016521,"weightPercentage":6.94019363,"marketValue":56611327255,
  "updatedAt":"2026-08-29 13:47:36"},
 {"symbol":"SPY","asset":"MSFT","name":"MICROSOFT CORP","isin":"US5949181045","securityCusip":"594918104",
  "sharesNumber":91046214,"weightPercentage":5.63550616,"marketValue":45968959992,
  "updatedAt":"2026-08-29 13:47:36"}]
```

Create `tests/FmpDotNet.Tests/Fixtures/etf-holdings.BND.sentinels.json` — two rows lifted verbatim from the
17,252-row BND response captured 2026-08-30. The first has an empty `asset` and `isin` but a real
`securityCusip`; the second is empty on all three. Together they are what a bond fund looks like:

```json
[{"symbol":"BND","asset":"","name":"MKTLIQ 12/31/2049","isin":"","securityCusip":"CMT001142",
  "sharesNumber":54112647.476,"weightPercentage":1.35712107,"marketValue":5410723621.13,
  "updatedAt":"2026-08-28 15:08:19"},
 {"symbol":"BND","asset":"","name":"US Dollar","isin":"","securityCusip":"",
  "sharesNumber":1093207268.48,"weightPercentage":0.27419893,"marketValue":1093207268.48,
  "updatedAt":"2026-08-28 15:08:19"}]
```

Create `tests/FmpDotNet.Tests/Fixtures/etf-asset-exposure.SPY.head.json` — the first three rows of the 39-row
response captured 2026-08-30 for `symbol=SPY`. Note what this path answers: given an asset it lists the
**ETFs that hold it**, so `symbol` names a different fund on every row and `asset` is the constant:

```json
[{"symbol":"XCHG","asset":"SPY","sharesNumber":3189,"weightPercentage":0.34179638,"marketValue":2459037.9},
 {"symbol":"WSGE","asset":"SPY","sharesNumber":1572,"weightPercentage":1.94,"marketValue":1209418.2},
 {"symbol":"VWNFX","asset":"SPY","sharesNumber":263841,"weightPercentage":0.29892,
  "marketValue":197028543.57}]
```

- [ ] **Step 2: Write the failing tests**

Append inside the existing `EtfAndFundsTests` class in `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`, and add
`using NodaTime;` to the file's using block:

```csharp
    [Fact]
    public void A_holding_binds_all_nine_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.SPY.head.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("NVDA", rows[0].Asset);
        Assert.Equal("NVIDIA CORP", rows[0].Name);
        Assert.Equal("US67066G1040", rows[0].Isin);
        Assert.Equal("67066G104", rows[0].SecurityCusip);
        Assert.Equal(296861422m, rows[0].SharesNumber);
        Assert.Equal(8.29427804m, rows[0].WeightPercentage);
        Assert.Equal(67656626530m, rows[0].MarketValue);
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 13, 47, 36), rows[0].UpdatedAt);
    }

    [Fact]
    public void An_empty_asset_isin_or_cusip_becomes_null_and_the_rest_of_the_row_survives()
    {
        // Measured 2026-08-30 over 35,185 rows: `asset` was "" on 51.1%, `isin` on 51.0% and `securityCusip`
        // on 22.8%. That is not an anomaly to route around — it is what a bond fund looks like. BND's 17,252
        // holdings are mostly unlisted debt with no ticker. Without the converter a caller writing
        // `row.Asset ?? "unlisted"` gets "" on half the rows and no warning.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.BND.sentinels.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Null(rows[0].Asset);
        Assert.Null(rows[0].Isin);
        Assert.Equal("CMT001142", rows[0].SecurityCusip);   // one field absent, its neighbour present
        Assert.Null(rows[1].SecurityCusip);

        // Everything else on both rows still bound.
        Assert.Equal("MKTLIQ 12/31/2049", rows[0].Name);
        Assert.Equal(54112647.476m, rows[0].SharesNumber);
        Assert.Equal(5410723621.13m, rows[0].MarketValue);
        Assert.Equal("US Dollar", rows[1].Name);
        Assert.Equal(1093207268.48m, rows[1].SharesNumber);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"N/A\"")]
    [InlineData("\"NULL\"")]
    [InlineData("null")]
    public void Every_spelling_of_absence_reads_as_null(string wire)
    {
        // Four spellings, one meaning. `etf/holdings` was only measured sending "" — the other two string
        // forms were measured on `funds/disclosure` and `funds/disclosure-holders-search` — but this is one
        // converter and this is where its whole domain is pinned.
        // The interpolation hole is NOT last in the object on purpose: `{{wire}}}]` would put three closing
        // braces together, which is ambiguous inside a $$-interpolated raw string. Do not reorder these keys.
        var row = JsonSerializer.Deserialize(
            $$"""[{"asset":{{wire}},"symbol":"BND"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Null(row.Asset);
        Assert.Equal("BND", row.Symbol);
    }

    [Fact]
    public void A_real_value_survives_the_sentinel_converter()
    {
        var row = JsonSerializer.Deserialize(
            """[{"asset":"NVDA","isin":"US67066G1040","securityCusip":"67066G104"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("NVDA", row.Asset);
        Assert.Equal("US67066G1040", row.Isin);
        Assert.Equal("67066G104", row.SecurityCusip);
    }

    [Fact]
    public void A_number_sent_into_a_sentinel_field_binds_as_its_literal_text()
    {
        // No measured row did this. The branch exists because a JSON number read into a plain string property
        // THROWS under this SDK's context options, and the throw aborts the whole array — the failure measured
        // on NetWorthDebtDetails.Rate, where 23 numeric rows would have cost all 250. Two of the fields this
        // converter is applied to are numeric strings, so it is a shape FMP could plausibly unquote.
        var row = JsonSerializer.Deserialize(
            """[{"asset":30}]""", FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("30", row.Asset);
    }

    [Fact]
    public void The_holding_name_is_not_sentinel_converted()
    {
        // `name` was populated on all 35,185 rows measured 2026-08-30, so an empty name would be information,
        // not absence — and this SDK does not convert a field whose sentinel it has never seen. This test
        // fails if the converter is ever added to Name "for consistency".
        var row = JsonSerializer.Deserialize(
            """[{"name":""}]""", FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("", row.Name);
    }

    [Fact]
    public void The_holdings_timestamp_reads_as_utc_and_not_as_eastern()
    {
        // THE falsification, reproduced. Measured 2026-08-30, `etf/holdings?symbol=SCHD` returned
        // `updatedAt 2026-08-30 06:51:13` in a response whose own Date header read
        // `Sun, 30 Aug 2026 10:05:35 GMT`. Read as Eastern, 06:51:13 EDT is 10:51:13Z — 46 minutes AFTER FMP
        // generated the response carrying it, and a cache stamp cannot postdate its own response. Read as UTC
        // it is 3h14m old, which is ordinary. Reproduced 18 seconds later against a fresh response.
        //
        // So this field takes NullableFmpInstantJsonConverter (UTC) while FundDisclosure.AcceptedDate takes
        // NullableEasternInstantJsonConverter, on the identical `uuuu-MM-dd HH:mm:ss` wire shape. Swapping
        // them costs four or five hours and nothing throws.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"SCHD","updatedAt":"2026-08-30 06:51:13"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 30, 6, 51, 13), row.UpdatedAt);
        Assert.NotEqual(Instant.FromUtc(2026, 8, 30, 10, 51, 13), row.UpdatedAt);
    }

    [Fact]
    public void The_holdings_timestamp_is_one_value_for_the_whole_response()
    {
        // Measured 2026-08-30: 33 of 33 responses carried exactly ONE distinct `updatedAt` across every row.
        // It is a per-symbol cache stamp, not a per-holding as-of date, and staleness ranged from 3.2 hours
        // (ARKK) to 284 hours (IJH, IJR) on one sweep. The XML doc says so; this test holds the shape.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.SPY.head.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Single(rows.Select(r => r.UpdatedAt).Distinct());
    }

    [Fact]
    public void Negative_and_fractional_holdings_survive()
    {
        // Both rows carry measured extremes, not invented ones: across the 35,185 rows measured 2026-08-30
        // `sharesNumber` reached -2,920,694,176 and 0.0001383508577753182, `weightPercentage` -0.34898692 and
        // 100, and `marketValue` -560,343,250 and 155,526,370,000. An integer type is wrong for `sharesNumber`
        // twice over — it is signed AND fractional.
        var rows = JsonSerializer.Deserialize(
            """
            [{"sharesNumber":-2920694176,"weightPercentage":-0.34898692,"marketValue":-560343250},
             {"sharesNumber":0.0001383508577753182,"weightPercentage":100,"marketValue":155526370000}]
            """,
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Equal(-2920694176m, rows[0].SharesNumber);
        Assert.Equal(-0.34898692m, rows[0].WeightPercentage);
        Assert.Equal(-560343250m, rows[0].MarketValue);
        Assert.Equal(0.0001383508577753182m, rows[1].SharesNumber);
        Assert.Equal(155526370000m, rows[1].MarketValue);
    }

    [Fact]
    public void An_asset_exposure_row_binds_all_five_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-asset-exposure.SPY.head.json"),
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("XCHG", rows[0].Symbol);
        Assert.Equal("SPY", rows[0].Asset);
        Assert.Equal(3189m, rows[0].SharesNumber);
        Assert.Equal(0.34179638m, rows[0].WeightPercentage);
        Assert.Equal(2459037.9m, rows[0].MarketValue);
    }

    [Fact]
    public void The_asset_is_the_constant_on_asset_exposure_and_the_symbol_is_not()
    {
        // This path runs the other way from the four other `etf/*` paths: given an asset it answers which
        // ETFs hold it. Measured 2026-08-30, `asset` was identical across every row of all 8 responses while
        // `symbol` named a different fund on each. A caller who reads `symbol` as "the fund I asked about"
        // is reading the wrong field, which is why both properties say so in their docs.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-asset-exposure.SPY.head.json"),
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Single(rows.Select(r => r.Asset).Distinct());
        Assert.Equal(3, rows.Select(r => r.Symbol).Distinct().Count());
    }

    [Fact]
    public void An_asset_exposure_weight_is_bounded_by_neither_zero_nor_one_hundred()
    {
        // Both rows are verbatim measured captures. NVD is an inverse NVDA product; HEMI's MSFT line reported
        // a 50,506% weight against a zero market value. Measured 2026-08-30, this field's range on
        // `etf/asset-exposure` was -199.9869 to 50,506 — so it cannot be range-checked, cannot be documented
        // as a 0-100 percentage, and cannot take an unsigned type. This test fails if a guard is ever added.
        var rows = JsonSerializer.Deserialize(
            """
            [{"symbol":"NVD","asset":"NVDA","sharesNumber":-457235,"weightPercentage":-199.9869,
              "marketValue":-103015045.5},
             {"symbol":"HEMI","asset":"MSFT","sharesNumber":0,"weightPercentage":50506,"marketValue":0}]
            """,
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Equal(-199.9869m, rows[0].WeightPercentage);
        Assert.Equal(-457235m, rows[0].SharesNumber);
        Assert.Equal(-103015045.5m, rows[0].MarketValue);
        Assert.Equal(50506m, rows[1].WeightPercentage);
    }

    [Fact]
    public void An_asset_exposure_market_value_keeps_all_seventeen_of_its_significant_digits()
    {
        // 7,434,183,997,921.512 is the measured maximum on this field, 2026-08-30. double holds about 15-17
        // significant digits and would not round-trip it; decimal does. This is the other half of the
        // argument in The_thirty_place_sector_weight_rounds_and_does_not_throw.
        var row = JsonSerializer.Deserialize(
            """[{"marketValue":7434183997921.512}]""",
            FmpJsonContext.Default.ListEtfAssetExposure)![0];

        Assert.Equal(7434183997921.512m, row.MarketValue);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — `ListEtfHolding` and `ListEtfAssetExposure` do not exist (CS1061).

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `PercentSuffixedDecimalJsonConverter`:

```csharp

/// <summary>Maps FMP's three string spellings of absence — <c>""</c>, <c>"N/A"</c> and <c>"NULL"</c> — to
/// <see langword="null"/>, and passes every other value through verbatim.
///
/// <para><b>Absence is spelled four ways in the ETF and mutual-fund group, and one field uses two of
/// them.</b> Measured 2026-08-30: <c>etf/holdings.asset</c> was <c>""</c> on 17,988 of 35,185 rows (51.1%);
/// <c>funds/disclosure.lei</c> was <c>"N/A"</c> on 495; <c>funds/disclosure-holders-search</c> sent the literal
/// four-character string <c>"NULL"</c> on six fields at once — <c>symbol</c>, <c>entityOrgType</c>,
/// <c>reportingFileNumber</c>, <c>city</c>, <c>zipCode</c>, <c>state</c> — on 26-28% of rows, alongside a real
/// JSON <see langword="null"/> in <c>address</c> on the same rows. On the widest query taken
/// (<c>name=Trust</c>, 66,065 rows) <c>className</c> carried <b>both</b> string spellings: <c>"NULL"</c> ×1,278
/// and <c>"N/A"</c> ×192.</para>
///
/// <para><b>What this costs, stated plainly.</b> A caller can no longer tell "FMP sent nothing" from "FMP sent
/// the word NULL". That is the same trade <see cref="TolerantDecimalJsonConverter"/> already documents, and it
/// is accepted here for a reason that converter cannot claim: the alternative is asking every caller to know
/// four spellings, on more than a quarter of the rows, on the fields they most want. A caller who writes
/// <c>row.State ?? "unknown"</c> without this converter gets the string <c>"NULL"</c> and no warning.</para>
///
/// <para><b>Applied to exactly the properties measured to carry a sentinel, and to no others.</b>
/// <c>etf/holdings.name</c> was populated on all 35,185 rows, so an empty name would be information rather
/// than absence and that property is left alone — as are <c>title</c>, <c>units</c>, <c>assetCat</c>,
/// <c>issuerCat</c>, <c>cik</c>, <c>classId</c>, <c>seriesId</c>, <c>entityName</c>, <c>seriesName</c> and
/// <c>fairValLevel</c>, none of which was ever measured sending one.</para>
///
/// <para>A JSON number reads as its literal text rather than throwing. No measured row sent one into these
/// fields; the branch is there because a number read into a plain <see cref="string"/> property throws under
/// this SDK's context options, and the throw aborts the <b>whole array</b> — the failure measured on
/// <see cref="Models.NetWorthDebtDetails.Rate"/> and documented on
/// <see cref="ScalarAsStringJsonConverter"/>. Two of the fields this converter is applied to are numeric
/// strings (<c>entityOrgType</c> is <c>"30"</c>, <c>"32"</c>, <c>"33"</c>), so it is a shape FMP could
/// plausibly unquote.</para></summary>
public sealed class SentinelStringJsonConverter : JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() switch
                {
                    null or "" or "N/A" or "NULL" => null,
                    var text => text,
                };
            case JsonTokenType.Number:
                return Encoding.UTF8.GetString(
                    reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
            case JsonTokenType.Null:
                return null;
            default:
                // Skip() is required, not optional: returning from a StartObject without consuming to its
                // EndObject desynchronises the reader for every field after it.
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
```

- [ ] **Step 5: Write `EtfHolding`**

Create `src/FmpDotNet/Models/EtfHolding.cs`. **Three deferred crefs** are written as `<c>…</c>` below and
promoted in Task 7: `EtfInfo.HoldingsCount` (Task 3), `FundDisclosure.AcceptedDate` and `FundDisclosure.Date`
(Task 4):

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One position an ETF holds, from <c>stable/etf/holdings</c>.
///
/// <para><b>There is no pagination and responses get large.</b> Measured 2026-08-30, <c>limit</c> and
/// <c>page</c> are ignored exactly the way an unknown parameter is: <c>etf/holdings?symbol=BND</c> returned
/// 17,252 rows and 4,949,598 bytes with and without either, byte-identical. VXUS returned 8,821 rows and
/// 2.5 MB. There is no way to ask for less than everything, and <c>EtfInfo.HoldingsCount</c> cannot be used
/// to pre-size the result — it disagreed with this path on 32 of 33 ETFs.</para>
///
/// <para><b>Half of a bond fund's rows have no ticker.</b> Measured 2026-08-30 over 35,185 rows,
/// <see cref="Asset"/> was empty on 51.1% and <see cref="Isin"/> on 51.0% — unlisted debt and foreign lines.
/// <see cref="Name"/> was populated on <b>all</b> 35,185, so the human-readable identity is always
/// there.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>ordered by weight, descending</b>, and the order held over the
/// full 17,252-row BND response. A stock symbol answers <c>[]</c> at HTTP 200 rather than an error — AAPL
/// did.</para></summary>
public sealed record EtfHolding
{
    /// <summary>The fund, echoed on every row — measured 2026-08-30 it was constant across every row of all 33
    /// responses. Nullable because the deserialiser cannot promise a key is present, not because any measured
    /// row omitted it: no row was ever missing a key on this path.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The held security's ticker, or <see langword="null"/> when it has none.
    ///
    /// <para>Measured <c>""</c> on 17,988 of 35,185 rows 2026-08-30 and mapped to <see langword="null"/> by
    /// <see cref="SentinelStringJsonConverter"/>. That is not a defect to route around: BND's 17,252 holdings
    /// are mostly unlisted debt. Use <see cref="Name"/> when this is <see langword="null"/>.</para></summary>
    [JsonPropertyName("asset")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Asset { get; init; }

    /// <summary>The held security's name — <c>"NVIDIA CORP"</c>, <c>"MKTLIQ 12/31/2049"</c>, <c>"US
    /// Dollar"</c>. Populated on all 35,185 rows measured 2026-08-30, and deliberately <b>not</b> routed
    /// through <see cref="SentinelStringJsonConverter"/>: no sentinel was ever measured here, and an empty
    /// name would be information rather than absence.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The held security's ISIN, or <see langword="null"/>. Empty on 17,927 of 35,185 rows measured
    /// 2026-08-30; see <see cref="Asset"/>.</summary>
    [JsonPropertyName("isin")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Isin { get; init; }

    /// <summary>The held security's CUSIP, or <see langword="null"/>. Empty on 8,036 of 35,185 rows measured
    /// 2026-08-30 — a different population from <see cref="Asset"/>'s, so a row can carry a CUSIP and no
    /// ticker.</summary>
    [JsonPropertyName("securityCusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? SecurityCusip { get; init; }

    /// <summary>Shares held. <b>Signed and fractional</b>: measured 2026-08-30 the range was
    /// −2,920,694,176 to 71,557,356,084, and values like <c>54112647.476</c> and
    /// <c>0.0001383508577753182</c> are ordinary in bond funds. An integer type is wrong for this field
    /// twice over.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>The position's share of the fund, as a percentage — <c>8.29427804</c> means 8.29%. A bare JSON
    /// number, like <see cref="EtfSectorWeighting.WeightPercentage"/> and unlike
    /// <see cref="EtfCountryWeighting.WeightPercentage"/>. Measured range 2026-08-30: −0.34898692 to
    /// 100.</summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }

    /// <summary>The position's value. Measured range 2026-08-30: −560,343,250 to 155,526,370,000.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>When FMP last refreshed this fund's holdings — <b>a cache stamp, not an as-of date.</b>
    ///
    /// <para><b>Read as UTC, and that was established by falsification rather than assumed.</b> Measured
    /// 2026-08-30, <c>symbol=SCHD</c> returned <c>2026-08-30 06:51:13</c> in a response whose own HTTP
    /// <c>Date</c> header read <c>Sun, 30 Aug 2026 10:05:35 GMT</c>. Read as Eastern that stamp is
    /// <c>10:51:13Z</c> — 46 minutes <b>after</b> the response that carried it, which a cache stamp cannot be.
    /// Read as UTC it is 3h14m old. Reproduced 18 seconds later against a fresh response. So this takes
    /// <see cref="NullableFmpInstantJsonConverter"/>, while the identical wire shape on
    /// <c>FundDisclosure.AcceptedDate</c> takes <see cref="NullableEasternInstantJsonConverter"/>.</para>
    ///
    /// <para><b>One value for the whole response, and it can be days old.</b> Measured 2026-08-30, 33 of 33
    /// responses carried exactly one distinct value across every row, and staleness ranged from <b>3.2
    /// hours</b> (ARKK) to <b>284 hours</b> (IJH, IJR). It says when FMP refreshed its copy — not when the
    /// fund held these positions. Do not use it as a portfolio as-of date; <c>FundDisclosure.Date</c> is
    /// that.</para></summary>
    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(NullableFmpInstantJsonConverter))]
    public Instant? UpdatedAt { get; init; }
}
```

- [ ] **Step 6: Write `EtfAssetExposure`**

Create `src/FmpDotNet/Models/EtfAssetExposure.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One ETF's position in a given security, from <c>stable/etf/asset-exposure</c>.
///
/// <para><b>This path runs the opposite way from the other four <c>etf/*</c> paths.</b> They take a fund and
/// answer what it holds; this one takes an <b>asset</b> and answers <b>which funds hold it</b>. Measured
/// 2026-08-30, <c>symbol=AAPL</c> returned 3,293 rows, each naming a different ETF in <see cref="Symbol"/>
/// with <see cref="Asset"/> fixed at <c>AAPL</c>. The parameter is "any asset", not "any stock":
/// <c>symbol=SPY</c> answered 39 rows, the ETFs that hold SPY.</para>
///
/// <para><b>No ordering was found</b> in the responses measured 2026-08-30, and there is no pagination —
/// <c>limit</c> and <c>page</c> were ignored, with <c>symbol=NVDA</c> returning 3,860 rows and 588,479 bytes
/// with and without them.</para></summary>
public sealed record EtfAssetExposure
{
    /// <summary>The <b>fund</b> that holds the asset — a different one on every row. This is not the symbol
    /// the caller asked for; see <see cref="Asset"/>. Nullable for the reason on
    /// <see cref="EtfHolding.Symbol"/>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The security being held — <b>the symbol the caller asked for</b>, echoed on every row.
    /// Measured 2026-08-30 it was identical across every row of all 8 responses. Not routed through
    /// <see cref="Serialization.SentinelStringJsonConverter"/>: no sentinel was ever measured on this
    /// path.</summary>
    [JsonPropertyName("asset")] public string? Asset { get; init; }

    /// <summary>Shares of <see cref="Asset"/> held by <see cref="Symbol"/>. Signed — an inverse product
    /// reports a negative count, e.g. <c>NVD</c> at <c>−457,235</c> shares of NVDA, measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>The position's share of the holding fund, as a percentage.
    ///
    /// <para><b>Bounded by neither 0 nor 100.</b> Measured 2026-08-30 the range on this field was
    /// <b>−199.9869</b> (the <c>NVD</c> inverse product) to <b>50,506</b> (a <c>HEMI</c> row whose market
    /// value was zero). It is therefore not range-checked anywhere in this SDK and must not be: a guard would
    /// reject real data.</para></summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }

    /// <summary>The position's value. Measured range 2026-08-30: <b>−103,015,045.5</b> to
    /// <b>7,434,183,997,921.512</b> — 17 significant digits, which is why every figure in this group is
    /// <see cref="decimal"/> and not <see cref="double"/>.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }
}
```

- [ ] **Step 7: Register both records with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<EtfHolding>))]
[JsonSerializable(typeof(List<EtfAssetExposure>))]
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS.

- [ ] **Step 9: Run the whole suite**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/EtfHolding.cs src/FmpDotNet/Models/EtfAssetExposure.cs \
        tests/FmpDotNet.Tests/EtfAndFundsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/etf-holdings.SPY.head.json \
        tests/FmpDotNet.Tests/Fixtures/etf-holdings.BND.sentinels.json \
        tests/FmpDotNet.Tests/Fixtures/etf-asset-exposure.SPY.head.json
git commit -m "feat: map the four spellings of absence, and the two holdings shapes (#34)"
```

---

### Task 3: `NullableIsoInstantJsonConverter`, `EtfInfo` and `EtfInfoSector`

The widest record in the slice, the second `updatedAt` format, and the nested array that duplicates a whole
path. `EtfInfoSector` lives in `EtfInfo.cs` — it is unreachable except through `EtfInfo.SectorsList`, and the
precedent is `NetWorthDebtDetails` sharing `SenateNetWorth.cs` with its parent.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append after `SentinelStringJsonConverter`)
- Create: `src/FmpDotNet/Models/EtfInfo.cs` (**both** records)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (**one** entry — see Step 6)
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-info.SPY.json`
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: `FmpJsonContext.Default.ListEtfSectorWeighting` and `EtfSectorWeighting` (Task 1) — one test
  compares the nested list against the sibling path's fixture, value for value.
- Produces: `public sealed class FmpDotNet.Serialization.NullableIsoInstantJsonConverter :
  JsonConverter<Instant?>`; `public sealed record FmpDotNet.Models.EtfInfo` with `string? Symbol`,
  `string? Name`, `string? Description`, `string? Isin`, `string? AssetClass`, `string? SecurityCusip`,
  `string? Domicile`, `string? Website`, `string? EtfCompany`, `decimal? ExpenseRatio`,
  `decimal? AssetsUnderManagement`, `decimal? AvgVolume`, `LocalDate? InceptionDate`, `decimal? Nav`,
  `string? NavCurrency`, `int? HoldingsCount`, `bool? IsActivelyTrading`, `Instant? UpdatedAt`,
  `IReadOnlyList<EtfInfoSector>? SectorsList`; `public sealed record FmpDotNet.Models.EtfInfoSector` with
  `string? Sector`, `decimal? Exposure`; `FmpJsonContext.Default.ListEtfInfo`. Tasks 6 and 7 use these.

- [ ] **Step 1: Write the fixture**

Create `tests/FmpDotNet.Tests/Fixtures/etf-info.SPY.json` — the whole one-element response captured 2026-08-30,
verbatim. **Keep the `Cash & Others` exponent exactly as written**; one test pins it:

```json
[{"symbol":"SPY","name":"State Street SPDR S&P 500 ETF","description":"SPY is the best-recognized and oldest US listed ETF and typically tops rankings for largest AUM and greatest trading volume. The fund tracks the massively popular US index, the S&P 500. Few realize that S&P's index committee chooses 500 securities to represent the US large-cap space - not necessarily the 500 largest by market cap, which can lead to some omissions of single names. Still, the index offers outstanding exposure to the US large-cap space. It's important to note, SPY is a unit investment trust, an older but entirely viable structure. As a UIT, SPY must fully replicate its index (it probably would anyway) and forgo the small risk and reward of securities lending. It also can`t reinvest portfolio dividends between distributions, the resulting cash drag will slightly hurt performance in up markets and help in downtrends. SPY is a favored vanilla trading vehicle.","isin":"US78462F1030","assetClass":"Equity","securityCusip":"78462F103","domicile":"US","website":"https://www.ssga.com/us/en/institutional/etfs/state-street-spdr-sp-500-etf-trust-spy","etfCompany":"SPDR","expenseRatio":0.09,"assetsUnderManagement":816147480000,"avgVolume":49440271,"inceptionDate":"1993-01-22","nav":771.27,"navCurrency":"USD","holdingsCount":504,"isActivelyTrading":true,"updatedAt":"2026-08-29T23:12:50.006Z","sectorsList":[{"industry":"Basic Materials","exposure":1.62},{"industry":"Cash & Others","exposure":1.4210854715202004e-14},{"industry":"Communication Services","exposure":9.91},{"industry":"Consumer Cyclical","exposure":9.57},{"industry":"Consumer Defensive","exposure":4.61},{"industry":"Energy","exposure":3.36},{"industry":"Financial Services","exposure":12.24},{"industry":"Healthcare","exposure":9.1},{"industry":"Industrials","exposure":8.16},{"industry":"Real Estate","exposure":1.88},{"industry":"Technology","exposure":37.4},{"industry":"Utilities","exposure":2.15}]}]
```

- [ ] **Step 2: Write the failing tests**

Append inside `EtfAndFundsTests`:

```csharp
    [Fact]
    public void An_etf_info_row_binds_all_nineteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-info.SPY.json"), FmpJsonContext.Default.ListEtfInfo)!;

        Assert.Single(rows);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("State Street SPDR S&P 500 ETF", rows[0].Name);
        Assert.StartsWith("SPY is the best-recognized", rows[0].Description);
        Assert.Equal("US78462F1030", rows[0].Isin);
        Assert.Equal("Equity", rows[0].AssetClass);
        Assert.Equal("78462F103", rows[0].SecurityCusip);
        Assert.Equal("US", rows[0].Domicile);
        Assert.StartsWith("https://www.ssga.com/", rows[0].Website);
        Assert.Equal("SPDR", rows[0].EtfCompany);
        Assert.Equal(0.09m, rows[0].ExpenseRatio);
        Assert.Equal(816147480000m, rows[0].AssetsUnderManagement);
        Assert.Equal(49440271m, rows[0].AvgVolume);
        Assert.Equal(new LocalDate(1993, 1, 22), rows[0].InceptionDate);
        Assert.Equal(771.27m, rows[0].Nav);
        Assert.Equal("USD", rows[0].NavCurrency);
        Assert.Equal(504, rows[0].HoldingsCount);
        Assert.True(rows[0].IsActivelyTrading);
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 23, 12, 50) + Duration.FromMilliseconds(6),
            rows[0].UpdatedAt);
        Assert.Equal(12, rows[0].SectorsList!.Count);
    }

    [Fact]
    public void The_info_timestamp_reads_the_iso_form_and_keeps_its_milliseconds()
    {
        // The SECOND `updatedAt` format in this group. `etf/holdings` sends `2026-08-30 06:51:13` — space
        // separated, no zone marker, and measured UTC by falsification. `etf/info` sends
        // `2026-08-29T23:12:50.006Z`, 33 of 33 rows measured 2026-08-30: ISO-8601 with milliseconds and an
        // explicit Z, so it needs no zone measurement — it is UTC because it says so.
        //
        // NullableFmpInstantJsonConverter cannot read this shape: its pattern expects a space separator and
        // no Z, so it would bind null on every row. This test fails if it is ever substituted here.
        var row = JsonSerializer.Deserialize(
            """[{"updatedAt":"2026-08-29T23:12:50.006Z"}]""", FmpJsonContext.Default.ListEtfInfo)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 29, 23, 12, 50) + Duration.FromMilliseconds(6), row.UpdatedAt);
    }

    [Fact]
    public void A_nested_sector_binds_industry_and_exposure_and_not_the_sibling_paths_key_names()
    {
        // The nested objects spell the same two facts with DIFFERENT keys from stable/etf/sector-weightings:
        // `industry` where the path says `sector`, `exposure` where it says `weightPercentage`. And the
        // `industry` key holds SECTOR names — "Basic Materials", "Cash & Others" — not industries.
        //
        // The property is Sector and the attribute is [JsonPropertyName("industry")], under the same rule that
        // binds `senateID` to SenateId. DO NOT "fix" the attribute: the property would then bind nothing,
        // silently, and this test is the only thing that would notice.
        var row = JsonSerializer.Deserialize(
            """[{"sectorsList":[{"industry":"Technology","exposure":37.4}]}]""",
            FmpJsonContext.Default.ListEtfInfo)![0];

        Assert.Equal("Technology", row.SectorsList![0].Sector);
        Assert.Equal(37.4m, row.SectorsList[0].Exposure);
    }

    [Fact]
    public void The_nested_sectors_are_the_sector_weightings_path_value_for_value()
    {
        // Measured 2026-08-30: all 13 ETFs cross-checked agreed on the key set AND on every value, with no
        // rounding difference. One of the nine paths is fully contained in another. That is why the SDK ships
        // two records for one fact and says so in both docs — a maintainer who finds the duplication should
        // find this test before deleting either one.
        var info = JsonSerializer.Deserialize(
            Binding.Fixture("etf-info.SPY.json"), FmpJsonContext.Default.ListEtfInfo)![0];
        var weightings = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(
            weightings.Select(w => (w.Sector, w.WeightPercentage)),
            info.SectorsList!.Select(s => (s.Sector, s.Exposure)));
    }

    [Fact]
    public void The_holdings_count_binds_as_a_count_and_zero_is_a_value_not_an_absence()
    {
        // `holdingsCount` is NOT the number of holdings. Cross-checked on 33 ETFs against the row count
        // stable/etf/holdings returned for the same symbol on the same day, they agreed on ONE: BND reports
        // 346 and returns 17,252; ARKK reports 10 and returns 47; GLD and SLV report 0 and return 1. It
        // cannot pre-size a buffer, cannot page (there is none), and cannot decide whether calling the
        // holdings path is worthwhile.
        //
        // Zero is therefore a real measured value on this field, not a missing one, which is what this test
        // pins: it fails if the property is ever narrowed to a non-nullable int with 0 as its "absent".
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"GLD","holdingsCount":0},{"symbol":"BND","holdingsCount":346}]""",
            FmpJsonContext.Default.ListEtfInfo)!;

        Assert.Equal(0, rows[0].HoldingsCount);
        Assert.Equal(346, rows[1].HoldingsCount);
    }

    [Fact]
    public void Is_actively_trading_is_a_real_json_boolean_and_takes_no_converter()
    {
        // The only genuine JSON boolean in the whole slice — true on all 33 rows measured 2026-08-30. The
        // four `is*` fields on funds/disclosure are `Y`/`N` STRINGS and need YesNoBooleanJsonConverter; this
        // one does not, and giving it that converter would bind null on every row.
        var row = JsonSerializer.Deserialize(
            """[{"isActivelyTrading":false}]""", FmpJsonContext.Default.ListEtfInfo)![0];

        Assert.False(row.IsActivelyTrading);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — `ListEtfInfo` does not exist (CS1061).

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `SentinelStringJsonConverter`:

```csharp

/// <summary>Reads an ISO-8601 timestamp that carries its own <c>Z</c> — <c>"2026-08-29T23:12:50.006Z"</c> — as
/// an <see cref="Instant"/>.
///
/// <para><b>The fourth converter in this file for a timestamp, and the only one that needs no zone
/// measurement.</b> <see cref="NullableFmpInstantJsonConverter"/> and
/// <see cref="NullableEasternInstantJsonConverter"/> both read <c>"uuuu-MM-dd HH:mm:ss"</c>, which carries no
/// offset, and each had to establish its zone by measuring a DST shift.
/// <see cref="NullableLocalDateTimeJsonConverter"/> declines to guess where nobody measured. This form states
/// its offset, so there is nothing to establish.</para>
///
/// <para><b>Written for <c>stable/etf/info.updatedAt</c></b>, which sent
/// <c>uuuu-MM-dd'T'HH:mm:ss.fff'Z'</c> on 33 of 33 rows measured 2026-08-30 — while its sibling
/// <c>stable/etf/holdings</c> sends the space-separated form for the same concept. One name, two formats, on
/// two paths one word apart in the URL. Substituting
/// <see cref="NullableFmpInstantJsonConverter"/> here binds <see langword="null"/> on every row: its pattern
/// expects a space separator and no <c>Z</c>.</para>
///
/// <para>Uses NodaTime's <see cref="InstantPattern.ExtendedIso"/>, which reads the fractional seconds and the
/// <c>Z</c> and tolerates a value with no fractional part. Null on an unparseable value, like the rest of this
/// file.</para></summary>
public sealed class NullableIsoInstantJsonConverter : JsonConverter<Instant?>
{
    private static readonly InstantPattern Pattern = InstantPattern.ExtendedIso;

    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}
```

- [ ] **Step 5: Write `EtfInfo` and `EtfInfoSector`**

Create `src/FmpDotNet/Models/EtfInfo.cs`. Both records go in this one file. **Two deferred crefs**, promoted in
Task 7: `EtfAndFundsEndpoints.GetEtfHoldingsAsync` and `EtfAndFundsEndpoints.GetEtfSectorWeightingsAsync`
(Task 6 creates the facade):

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>An ETF's fact sheet, from <c>stable/etf/info</c> — nineteen keys, the widest shape in the ETF and
/// mutual-fund group.
///
/// <para><b>One row per call.</b> All 33 responses measured 2026-08-30 were single-element arrays, which is why
/// the SDK surfaces this as one record rather than a list. An unknown symbol answers <c>[]</c> at HTTP 200,
/// which becomes <see langword="null"/>.</para>
///
/// <para><b><see cref="SectorsList"/> duplicates a whole endpoint.</b> Measured 2026-08-30 it agreed with
/// <c>stable/etf/sector-weightings</c> on the key set and on every value, on all 13 ETFs cross-checked. A
/// caller holding this record does not need that path.</para>
///
/// <para><b><see cref="HoldingsCount"/> is not the number of holdings.</b> Read its doc before using it for
/// anything.</para></summary>
public sealed record EtfInfo
{
    /// <summary>The fund's ticker. Nullable because the deserialiser cannot promise a key is present, not
    /// because any measured row omitted it — no row was missing a key across all 33 measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fund's name — <c>"State Street SPDR S&amp;P 500 ETF"</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>A prose description of the fund, several hundred words on the funds measured 2026-08-30. It is
    /// editorial copy, not structured data.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The fund's own ISIN.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>What the fund holds, as FMP labels it.
    ///
    /// <para><b>A free string, not an enum, and the reason is in the measurement.</b> Six values appeared
    /// across 33 funds on 2026-08-30 — <c>Equity</c>, <c>Fixed Income</c>, <c>Commodities</c>,
    /// <c>International Equity</c>, <c>Large Cap Equity</c>, <c>Core Investment Grade Bond</c> — and those are
    /// not one vocabulary: <c>Equity</c>, <c>Large Cap Equity</c> and <c>International Equity</c> overlap
    /// rather than partition. An enum over a sample of 33 would fail on the 34th fund.</para></summary>
    [JsonPropertyName("assetClass")] public string? AssetClass { get; init; }

    /// <summary>The fund's own CUSIP.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>Where the fund is domiciled. <c>US</c> on all 33 rows measured 2026-08-30 — a small sample,
    /// and stated as one; this is not a claim that FMP only covers US funds.</summary>
    [JsonPropertyName("domicile")] public string? Domicile { get; init; }

    /// <summary>The issuer's page for the fund.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The issuer's brand — <c>"SPDR"</c>, <c>"Vanguard"</c>.</summary>
    [JsonPropertyName("etfCompany")] public string? EtfCompany { get; init; }

    /// <summary>The expense ratio as a <b>fraction</b>, not a percentage: SPY measured <c>0.09</c>, which is
    /// 0.09% — nine basis points — and not 9%. Measured 2026-08-30.</summary>
    [JsonPropertyName("expenseRatio")] public decimal? ExpenseRatio { get; init; }

    /// <summary>Assets under management, in <see cref="NavCurrency"/>. SPY measured
    /// <c>816,147,480,000</c>.</summary>
    [JsonPropertyName("assetsUnderManagement")] public decimal? AssetsUnderManagement { get; init; }

    /// <summary>Average daily share volume.</summary>
    [JsonPropertyName("avgVolume")] public decimal? AvgVolume { get; init; }

    /// <summary>The fund's inception date — SPY measured <c>1993-01-22</c>. A plain ISO date on the wire, with
    /// no time component, unlike the two timestamps on this record's siblings.</summary>
    [JsonPropertyName("inceptionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? InceptionDate { get; init; }

    /// <summary>Net asset value per share, in <see cref="NavCurrency"/>.</summary>
    [JsonPropertyName("nav")] public decimal? Nav { get; init; }

    /// <summary>The currency <see cref="Nav"/> and <see cref="AssetsUnderManagement"/> are quoted in.
    /// <c>USD</c> on all 33 rows measured 2026-08-30 — a small sample, stated as one.</summary>
    [JsonPropertyName("navCurrency")] public string? NavCurrency { get; init; }

    /// <summary>FMP's holdings count — <b>which is not the number of holdings
    /// <c>EtfAndFundsEndpoints.GetEtfHoldingsAsync</c> returns.</b>
    ///
    /// <para>Cross-checked on 33 ETFs 2026-08-30 against the row count <c>stable/etf/holdings</c> returned for
    /// the same symbol on the same day, the two agreed on <b>one</b>. BND reports <b>346</b> and returns
    /// <b>17,252</b>. ARKK reports <b>10</b> and returns <b>47</b>. GLD and SLV report <b>0</b> and return
    /// <b>1</b>. Most gaps are small — the two paths refresh from different snapshots — but the field cannot
    /// be used to pre-size a buffer, cannot be used to page (there is no pagination on any path in this
    /// group), and cannot be used to decide whether calling the holdings path is worthwhile.</para>
    ///
    /// <para>Zero is a measured value here, not an absence.</para></summary>
    [JsonPropertyName("holdingsCount")] public int? HoldingsCount { get; init; }

    /// <summary>Whether FMP considers the fund actively trading. <b>The only genuine JSON boolean in the ETF
    /// and mutual-fund group</b> — the four <c>is*</c> fields on <c>FundDisclosure</c> are <c>Y</c>/<c>N</c>
    /// strings. <see langword="true"/> on all 33 rows measured 2026-08-30.</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }

    /// <summary>When FMP last refreshed this fact sheet.
    ///
    /// <para><b>A different wire format from <see cref="EtfHolding.UpdatedAt"/>, for the same concept.</b>
    /// This one is ISO-8601 with milliseconds and an explicit <c>Z</c> —
    /// <c>"2026-08-29T23:12:50.006Z"</c>, 33 of 33 rows measured 2026-08-30 — so it is UTC because it says so,
    /// and takes <see cref="NullableIsoInstantJsonConverter"/>. The holdings path sends
    /// <c>"2026-08-30 06:51:13"</c> for the same idea and had to have its zone established by measurement.
    /// Neither converter can read the other's format.</para></summary>
    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(NullableIsoInstantJsonConverter))]
    public Instant? UpdatedAt { get; init; }

    /// <summary>The fund's sector breakdown, nested inside this response.
    ///
    /// <para><b>This is <c>stable/etf/sector-weightings</c>, under different key names.</b> Measured
    /// 2026-08-30, the two agreed on the key set and on <b>every value</b>, with no rounding difference, on
    /// all 13 ETFs cross-checked — SPY's and VOO's 12-element lists, QQQ's 11-element list, and the 1-element
    /// lists of GLD, SLV, TLT and BND. A caller holding this record does not need to call
    /// <c>EtfAndFundsEndpoints.GetEtfSectorWeightingsAsync</c>.</para>
    ///
    /// <para>The nested objects use <c>industry</c> and <c>exposure</c> where the path uses <c>sector</c> and
    /// <c>weightPercentage</c>, which is why <see cref="EtfInfoSector"/> exists rather than reusing
    /// <see cref="EtfSectorWeighting"/> — System.Text.Json binds one
    /// <see cref="JsonPropertyNameAttribute"/> per property, so one record cannot answer to both.</para>
    ///
    /// <para>The list came back <b>alphabetical by sector</b> on every response measured, matching the sibling
    /// path's order.</para></summary>
    [JsonPropertyName("sectorsList")] public IReadOnlyList<EtfInfoSector>? SectorsList { get; init; }
}

/// <summary>One sector's share of a fund, as nested inside <see cref="EtfInfo.SectorsList"/>.
///
/// <para><b>The same two facts as <see cref="EtfSectorWeighting"/>, under different wire keys.</b> Measured
/// 2026-08-30 the two shapes carried identical data on all 13 ETFs cross-checked, with no rounding difference.
/// The duplication in this SDK is deliberate: the nested objects say <c>industry</c> and <c>exposure</c> where
/// <c>stable/etf/sector-weightings</c> says <c>sector</c> and <c>weightPercentage</c>, and one record cannot
/// carry two <see cref="JsonPropertyNameAttribute"/> values on one property. A shared type would have to
/// rename keys in a converter, and its own doc would then be wrong about one of its two wire
/// shapes.</para></summary>
public sealed record EtfInfoSector
{
    /// <summary>The sector name — <c>"Basic Materials"</c>, <c>"Technology"</c>, <c>"Cash &amp; Others"</c>.
    ///
    /// <para><b>The wire key is <c>industry</c>, and it holds sector names.</b> The property takes the name
    /// the data actually has while the attribute carries the wire verbatim, under the same rule that binds
    /// <c>senateID</c> to <c>SenateId</c> and <c>changesPercentage</c> to
    /// <see cref="MarketMover.ChangePercentage"/>. <b>Do not "fix" the attribute</b> — the property would then
    /// bind nothing, silently.</para></summary>
    [JsonPropertyName("industry")] public string? Sector { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>37.4</c> means 37.4%. The wire key is
    /// <c>exposure</c>; the same figure is <c>weightPercentage</c> on
    /// <see cref="EtfSectorWeighting.WeightPercentage"/>, where the decimal-scale argument is
    /// recorded.</summary>
    [JsonPropertyName("exposure")] public decimal? Exposure { get; init; }
}
```

- [ ] **Step 6: Register `EtfInfo` with the source generator**

Add **one** line to `src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<EtfInfo>))]
```

`EtfInfoSector` needs **no entry of its own** — the generator walks the object graph and emits its metadata as
part of `List<EtfInfo>`, the same way `NetWorthRange` and `NetWorthDebtDetails` are reached through
`SenateNetWorthLine` (see the comment at `FmpJsonContext.cs:125`). If the build reports missing metadata for
`EtfInfoSector`, add `[JsonSerializable(typeof(EtfInfoSector))]` beside it and note why in the commit — but do
not add it pre-emptively.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/EtfInfo.cs tests/FmpDotNet.Tests/EtfAndFundsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/etf-info.SPY.json
git commit -m "feat: read the ISO updatedAt, and the ETF fact sheet with its nested sectors (#34)"
```

---

### Task 4: `YesNoBooleanJsonConverter`, `FundDisclosure` and `FundDisclosureDate`

The widest wire shape in the slice (23 keys), the Eastern timestamp, the `Y`/`N` pseudo-booleans, the
snake_case key, and the two records a caller uses together — `FundDisclosureDate` is where the `year` and
`quarter` arguments for `GetFundDisclosureAsync` come from.

**Files:**
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs` (append after `NullableIsoInstantJsonConverter`)
- Create: `src/FmpDotNet/Models/FundDisclosure.cs`
- Create: `src/FmpDotNet/Models/FundDisclosureDate.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two entries)
- Create: `tests/FmpDotNet.Tests/Fixtures/funds-disclosure.SPY.2026q1.head.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/funds-disclosure.dst-pair.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-dates.SPY.json`
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: `SentinelStringJsonConverter` (Task 2).
- Produces: `public sealed class FmpDotNet.Serialization.YesNoBooleanJsonConverter : JsonConverter<bool?>`;
  `public sealed record FmpDotNet.Models.FundDisclosure` with `string? Cik`, `LocalDate? Date`,
  `Instant? AcceptedDate`, `string? Symbol`, `string? Name`, `string? Lei`, `string? Title`, `string? Cusip`,
  `string? Isin`, `decimal? Balance`, `string? Units`, `string? CurrencyCode`, `decimal? ValueUsd`,
  `decimal? PercentValue`, `string? PayoffProfile`, `string? AssetCategory`, `string? IssuerCategory`,
  `string? InvestmentCountry`, `bool? IsRestrictedSecurity`, `string? FairValueLevel`,
  `bool? IsCashCollateral`, `bool? IsNonCashCollateral`, `bool? IsLoanByFund`;
  `public sealed record FmpDotNet.Models.FundDisclosureDate` with `LocalDate? Date`, `int? Year`,
  `int? Quarter`; `FmpJsonContext.Default.ListFundDisclosure` and `.ListFundDisclosureDate`.
  Tasks 6 and 7 use these.

**Property naming.** Five wire keys are abbreviated past readability and take house names, with the wire
spelling in the attribute exactly as `MarketMover.ChangePercentage` does: `cur_cd` → `CurrencyCode`,
`valUsd` → `ValueUsd`, `pctVal` → `PercentValue`, `assetCat` → `AssetCategory`,
`issuerCat` → `IssuerCategory`, `invCountry` → `InvestmentCountry`, `isRestrictedSec` →
`IsRestrictedSecurity`, `fairValLevel` → `FairValueLevel`. **Never "fix" an attribute to match its property.**

- [ ] **Step 1: Write the three fixtures**

Create `tests/FmpDotNet.Tests/Fixtures/funds-disclosure.SPY.2026q1.head.json` — the first two rows of the
503-row response captured 2026-08-30 for `symbol=SPY&year=2026&quarter=1`, verbatim:

```json
[{"cik":"0000884394","date":"2026-03-31","acceptedDate":"2026-05-28 15:11:03","symbol":"PM",
  "name":"Philip Morris International Inc","lei":"HL3H1H2BGXWVG3BSWR90",
  "title":"Philip Morris International Inc","cusip":"718172109","isin":"US7181721090",
  "balance":18128850,"units":"NS","cur_cd":"USD","valUsd":2997424059,"pctVal":0.4602323652851295,
  "payoffProfile":"Long","assetCat":"EC","issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N",
  "fairValLevel":"1","isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"},
 {"cik":"0000884394","date":"2026-03-31","acceptedDate":"2026-05-28 15:11:03","symbol":"AEP",
  "name":"American Electric Power Co Inc","lei":"1B4S6S7G0TW5EE83BO58",
  "title":"American Electric Power Co Inc","cusip":"025537101","isin":"US0255371017",
  "balance":6301711,"units":"NS","cur_cd":"USD","valUsd":826028277.88,"pctVal":0.1268305520467281,
  "payoffProfile":"Long","assetCat":"EC","issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N",
  "fairValLevel":"1","isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"}]
```

Create `tests/FmpDotNet.Tests/Fixtures/funds-disclosure.dst-pair.json` — **two rows assembled from two
different measured responses**, and that is the whole point: the first is the head of SPY's 2026 Q1 filing,
accepted in EDT, and the second the head of SPY's 2025 Q4 filing, accepted in EST. Each `acceptedDate` was the
only distinct value across all 503 rows of its own response. A fixed −4 or −5 offset fails one of the two:

```json
[{"cik":"0000884394","date":"2026-03-31","acceptedDate":"2026-05-28 15:11:03","symbol":"PM",
  "name":"Philip Morris International Inc","lei":"HL3H1H2BGXWVG3BSWR90",
  "title":"Philip Morris International Inc","cusip":"718172109","isin":"US7181721090",
  "balance":18128850,"units":"NS","cur_cd":"USD","valUsd":2997424059,"pctVal":0.4602323652851295,
  "payoffProfile":"Long","assetCat":"EC","issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N",
  "fairValLevel":"1","isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"},
 {"cik":"0000884394","date":"2025-12-31","acceptedDate":"2026-02-26 16:49:39","symbol":"BDX",
  "name":"Becton Dickinson & Co","lei":"ICE2EP6D98PQUILVRZ91","title":"Becton Dickinson & Co",
  "cusip":"075887109","isin":"US0758871091","balance":3487338,"units":"NS","cur_cd":"USD",
  "valUsd":676787685.66,"pctVal":0.09505083965591922,"payoffProfile":"Long","assetCat":"EC",
  "issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N","fairValLevel":"1",
  "isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"}]
```

Create `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-dates.SPY.json` — the first eight rows of the 28-row
response captured 2026-08-30, verbatim:

```json
[{"date":"2026-06-30","year":2026,"quarter":2},
 {"date":"2026-03-31","year":2026,"quarter":1},
 {"date":"2025-12-31","year":2025,"quarter":4},
 {"date":"2025-09-30","year":2025,"quarter":3},
 {"date":"2025-06-30","year":2025,"quarter":2},
 {"date":"2025-03-31","year":2025,"quarter":1},
 {"date":"2024-12-31","year":2024,"quarter":4},
 {"date":"2024-09-30","year":2024,"quarter":3}]
```

- [ ] **Step 2: Write the failing tests**

Append inside `EtfAndFundsTests`:

```csharp
    [Fact]
    public void A_fund_disclosure_row_binds_all_twenty_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure.SPY.2026q1.head.json"),
            FmpJsonContext.Default.ListFundDisclosure)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0000884394", rows[0].Cik);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[0].Date);
        Assert.Equal("PM", rows[0].Symbol);
        Assert.Equal("Philip Morris International Inc", rows[0].Name);
        Assert.Equal("HL3H1H2BGXWVG3BSWR90", rows[0].Lei);
        Assert.Equal("Philip Morris International Inc", rows[0].Title);
        Assert.Equal("718172109", rows[0].Cusip);
        Assert.Equal("US7181721090", rows[0].Isin);
        Assert.Equal(18128850m, rows[0].Balance);
        Assert.Equal("NS", rows[0].Units);
        Assert.Equal("USD", rows[0].CurrencyCode);
        Assert.Equal(2997424059m, rows[0].ValueUsd);
        Assert.Equal(0.4602323652851295m, rows[0].PercentValue);
        Assert.Equal("Long", rows[0].PayoffProfile);
        Assert.Equal("EC", rows[0].AssetCategory);
        Assert.Equal("CORP", rows[0].IssuerCategory);
        Assert.Equal("US", rows[0].InvestmentCountry);
        Assert.False(rows[0].IsRestrictedSecurity);
        Assert.Equal("1", rows[0].FairValueLevel);
        Assert.False(rows[0].IsCashCollateral);
        Assert.False(rows[0].IsNonCashCollateral);
        Assert.False(rows[0].IsLoanByFund);
    }

    [Fact]
    public void The_accepted_date_reads_as_eastern_on_both_sides_of_the_dst_boundary()
    {
        // The zone was established by identity against a field this SDK already measured against EDGAR.
        // Twenty NPORT-P filings across two CIKs and ten quarters were looked up a second time through
        // stable/sec-filings-search/cik, whose acceptedDate was measured Eastern against EDGAR on 2026-08-26.
        // Twelve of nineteen matched TO THE SECOND (10 of 10 for the SPY trust); the largest residual across
        // all nineteen was 90 SECONDS, against 3,600 for an hour. Nothing in that distribution is an offset.
        //
        // The two rows below are the heads of two different measured responses, chosen so that a FIXED offset
        // fails one of them: 15:11:03 on 2026-05-28 is EDT (UTC-4) and 16:49:39 on 2026-02-26 is EST (UTC-5).
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure.dst-pair.json"),
            FmpJsonContext.Default.ListFundDisclosure)!;

        Assert.Equal(Instant.FromUtc(2026, 5, 28, 19, 11, 3), rows[0].AcceptedDate);   // EDT, -4
        Assert.Equal(Instant.FromUtc(2026, 2, 26, 21, 49, 39), rows[1].AcceptedDate);  // EST, -5

        // And it is NOT the UTC reading that EtfHolding.UpdatedAt takes on the identical wire shape.
        Assert.NotEqual(Instant.FromUtc(2026, 5, 28, 15, 11, 3), rows[0].AcceptedDate);
    }

    [Theory]
    [InlineData("\"Y\"", true)]
    [InlineData("\"N\"", false)]
    [InlineData("\"X\"", null)]
    [InlineData("\"\"", null)]
    [InlineData("\"N/A\"", null)]
    [InlineData("null", null)]
    public void Yes_and_no_become_true_and_false_and_everything_else_becomes_null(string wire, bool? expected)
    {
        // The four `is*` fields are Y/N STRINGS, not JSON booleans — unlike EtfInfo.IsActivelyTrading, which
        // is a real one. Written as a total function over a measured domain rather than a two-case parse:
        // isRestrictedSec and isNonCashCollateral were `N` on all 3,861 rows sampled 2026-08-30, so their `Y`
        // form is unmeasured, and an unexpected third value must cost one field rather than the whole row.
        // The hole is not last in the object — see the note in Every_spelling_of_absence_reads_as_null.
        var row = JsonSerializer.Deserialize(
            $$"""[{"isLoanByFund":{{wire}},"cik":"0000884394"}]""",
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Equal(expected, row.IsLoanByFund);
    }

    [Fact]
    public void The_disclosure_sentinels_become_null_and_the_row_survives()
    {
        // A verbatim measured row: ARKK's 2026 Q1 BRERA HOLDINGS PLC WTS line, which carries THREE spellings
        // of absence at once — a real JSON null in `symbol`, "N/A" in `lei`, and "" in `isin`.
        var row = JsonSerializer.Deserialize(
            """
            [{"cik":"0001579982","date":"2026-01-30","acceptedDate":"2026-03-31 14:42:43","symbol":null,
              "name":"BRERA HOLDINGS PLC WTS","lei":"N/A","title":"BRERA HOLDINGS PLC WTS",
              "cusip":"000000000","isin":"","balance":4316257,"units":"NS","cur_cd":"USD",
              "valUsd":4359419.57,"pctVal":0.06529031951794871,"payoffProfile":"Long","assetCat":"EC",
              "issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N","fairValLevel":"1",
              "isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"}]
            """,
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Null(row.Symbol);   // a real JSON null — 176 of 11,522 rows measured 2026-08-30
        Assert.Null(row.Lei);      // "N/A" — 495 rows
        Assert.Null(row.Isin);     // ""    — 149 rows
        Assert.Equal("BRERA HOLDINGS PLC WTS", row.Name);
        Assert.Equal("000000000", row.Cusip);
        Assert.Equal(4316257m, row.Balance);
    }

    [Fact]
    public void The_currency_code_can_be_usdusd_and_binds_verbatim()
    {
        // A verbatim measured row: FXAIX's 2026 Q1 S&P 500 E-mini futures line. `cur_cd` was USDUSD on 29 of
        // 3,861 rows measured 2026-08-30 — all of them equity-futures lines (units NC, assetCat DE,
        // payoffProfile N/A). A doubled currency code, not a typo in this test. It is recorded so that a
        // strict three-letter currency type is never chosen for this field: this row would not fit it.
        var row = JsonSerializer.Deserialize(
            """
            [{"symbol":"ESH6","name":"CHICAGO MERCANTILE EXCH INC","cusip":"N/A","isin":"",
              "title":"S and P500 EMINI FUT MAR26 ESH6","balance":2288,"units":"NC","cur_cd":"USDUSD",
              "valUsd":5282494.16,"pctVal":0.0007040306952703573,"payoffProfile":"N/A","assetCat":"DE"}]
            """,
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Equal("USDUSD", row.CurrencyCode);
        Assert.Equal("NC", row.Units);
        Assert.Null(row.Cusip);           // "N/A"
        Assert.Null(row.PayoffProfile);   // "N/A" — 123 rows measured
        Assert.Equal("DE", row.AssetCategory);
    }

    [Fact]
    public void The_fair_value_level_stays_a_string_and_takes_no_sentinel_converter()
    {
        // fairValLevel is a quoted integer — "1" x3,829, "2" x28, "3" x4, measured 2026-08-30 — and it is a
        // CODE, not a quantity: an ASC 820 fair-value level. Parsing it to int? would invent a numeric
        // identity the source does not have. It carries NO sentinel converter, because no measured row ever
        // sent a sentinel here — see the ruling recorded at the top of this plan.
        var row = JsonSerializer.Deserialize(
            """[{"fairValLevel":"3"}]""", FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Equal("3", row.FairValueLevel);
    }

    [Fact]
    public void A_fund_disclosure_date_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-dates.SPY.json"),
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        Assert.Equal(8, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(2, rows[0].Quarter);
    }

    [Fact]
    public void The_disclosure_dates_come_back_newest_first()
    {
        // Measured 2026-08-30 over 127 rows: `date` descending on every response. Nothing re-sorts this
        // client-side, so the <returns> doc reports the measured order and this test holds it honest.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-dates.SPY.json"),
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        Assert.Equal(rows.Select(r => r.Date).OrderByDescending(d => d), rows.Select(r => r.Date));
    }

    [Fact]
    public void The_year_and_quarter_are_calendar_quarters_of_a_fiscal_period_end()
    {
        // The two fields do not describe the same calendar the `date` does. `date` is the fund's FISCAL
        // period-end — FXAIX reports on 2026-05-31 and 2025-11-30, ARKK on 2026-01-30 — while `year` and
        // `quarter` count CALENDAR quarters, so FXAIX's May date reads as Q2. Verified over 80 rows across
        // three funds 2026-08-30: year == date.Year and quarter == (date.Month - 1) / 3 + 1, with ZERO
        // mismatches. That relation is what makes the two fields usable as arguments to
        // GetFundDisclosureAsync, which is the only reason a caller reads them.
        //
        // The rows below are verbatim measured captures from FXAIX and ARKK.
        var rows = JsonSerializer.Deserialize(
            """
            [{"date":"2026-05-31","year":2026,"quarter":2},
             {"date":"2026-02-28","year":2026,"quarter":1},
             {"date":"2025-11-30","year":2025,"quarter":4},
             {"date":"2026-01-30","year":2026,"quarter":1}]
            """,
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        foreach (var row in rows)
        {
            Assert.Equal(row.Date!.Value.Year, row.Year);
            Assert.Equal((row.Date.Value.Month - 1) / 3 + 1, row.Quarter);
        }
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — `ListFundDisclosure` and `ListFundDisclosureDate` do not exist (CS1061).

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `NullableIsoInstantJsonConverter`:

```csharp

/// <summary>Reads FMP's <c>Y</c>/<c>N</c> string flags as a <see cref="bool"/>.
///
/// <para><b>Written for the four <c>is*</c> fields on <c>stable/funds/disclosure</c></b> —
/// <c>isRestrictedSec</c>, <c>isCashCollateral</c>, <c>isNonCashCollateral</c> and <c>isLoanByFund</c> — which
/// are quoted single letters and not JSON booleans. <c>stable/etf/info.isActivelyTrading</c>, by contrast, is a
/// real JSON boolean and must not take this converter.</para>
///
/// <para><b>A total function over a measured domain, not a two-case parse.</b> Measured 2026-08-30 over a
/// 3,861-row sample, two of the four were <c>N</c> on <b>every</b> row — <c>isRestrictedSec</c> and
/// <c>isNonCashCollateral</c> — so their <c>Y</c> form is inferred from the other two rather than observed.
/// Anything that is neither <c>Y</c> nor <c>N</c>, including <c>""</c> and <c>"N/A"</c>, becomes
/// <see langword="null"/>: an unmeasured third value costs one field rather than the whole row, and this
/// converter never has to be right about a value nobody has seen.</para>
///
/// <para>A real JSON <see langword="true"/> or <see langword="false"/> passes through, so a future
/// normalisation of the field costs nothing. No measured row sent one.</para></summary>
public sealed class YesNoBooleanJsonConverter : JsonConverter<bool?>
{
    /// <inheritdoc/>
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString() switch
            {
                "Y" => true,
                "N" => false,
                _ => null,
            },
            _ => null,
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        // The wire form, not a JSON boolean: a caller who serialises a row gets back what FMP sent. Read
        // accepts both forms, so this cannot round-trip lossily.
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value ? "Y" : "N");
    }
}
```

- [ ] **Step 5: Write `FundDisclosure`**

Create `src/FmpDotNet/Models/FundDisclosure.cs`. **Two deferred crefs**, promoted in Task 7:
`EtfAndFundsEndpoints.GetFundDisclosureAsync` and `EtfAndFundsEndpoints.GetFundDisclosureDatesAsync`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One holding line from a fund's SEC Form N-PORT filing, from <c>stable/funds/disclosure</c> —
/// twenty-three keys, the widest shape in the ETF and mutual-fund group.
///
/// <para><b>This is the fund's own filed portfolio, not FMP's cached view of it.</b> Where
/// <see cref="EtfHolding"/> answers "what does FMP think this ETF holds right now", this answers "what did the
/// fund tell the SEC it held on this date". <see cref="Date"/> is a real as-of date;
/// <see cref="EtfHolding.UpdatedAt"/> is not.</para>
///
/// <para><b>The only path in this SDK with a snake_case key.</b> <c>cur_cd</c> sits between <c>units</c> and
/// <c>valUsd</c>, both camelCase, in the same object — see <see cref="CurrencyCode"/>.</para>
///
/// <para><b>No ordering was found</b> in the responses measured 2026-08-30, and there is no pagination:
/// <c>limit</c> and <c>page</c> were ignored. A quarter outside the fund's coverage answers <c>[]</c> at
/// HTTP 200 — 2026 Q3 and Q4 both did on 2026-08-30 — as does a <c>quarter</c> of 0 or 5, which is why
/// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c> guards that argument.</para></summary>
public sealed record FundDisclosure
{
    /// <summary>The filing fund's SEC Central Index Key, zero-padded to ten characters — the padding is the
    /// value, so this is a <see cref="string"/>. Measured 2026-08-30 it was constant across every row of all
    /// 27 responses.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The portfolio as-of date the filing reports — the fund's <b>fiscal</b> period end. Measured
    /// 2026-08-30, SPY reports on calendar quarter ends while FXAIX reports on 2026-05-31 and ARKK on
    /// 2026-01-30. See <see cref="FundDisclosureDate"/>, which is how a caller discovers which dates a given
    /// fund has.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When EDGAR accepted the filing.
    ///
    /// <para><b>Read as US Eastern wall clock, not UTC</b> — see
    /// <see cref="NullableEasternInstantJsonConverter"/>. The zone was established by identity rather than
    /// assumed: twenty NPORT-P filings across two CIKs and ten quarters were looked up a second time through
    /// <c>stable/sec-filings-search/cik</c>, whose <c>acceptedDate</c> was measured against EDGAR on
    /// 2026-08-26. Measured 2026-08-30, <b>12 of 19 matched to the second</b> (10 of 10 for the SPY trust) and
    /// the largest residual across all nineteen was <b>90 seconds</b> — against 3,600 for an hour. The seven
    /// misses are same-day sibling filings, one per fund series, minutes apart.</para>
    ///
    /// <para><b>The identical wire shape on <see cref="EtfHolding.UpdatedAt"/> is UTC.</b> Two paths in this
    /// group send <c>"uuuu-MM-dd HH:mm:ss"</c> and they mean different zones. Swapping the converters costs
    /// four or five hours and nothing throws.</para>
    ///
    /// <para>Constant across a response, because a response is one filing: measured 2026-08-30, each of the
    /// twenty responses sampled carried exactly one distinct value.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The held security's ticker, or <see langword="null"/>.
    ///
    /// <para><b>Nullable because FMP actually sent JSON <see langword="null"/></b> — 176 of 11,522 rows
    /// measured 2026-08-30, not merely because the deserialiser cannot promise a key. Warrants, unlisted debt
    /// and foreign lines have no ticker. Use <see cref="Name"/> or <see cref="Cusip"/> instead.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name. <c>"N/A"</c> on 120 of 11,522 rows measured 2026-08-30, mapped to
    /// <see langword="null"/> by <see cref="SentinelStringJsonConverter"/>.</summary>
    [JsonPropertyName("name")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Name { get; init; }

    /// <summary>The issuer's Legal Entity Identifier, or <see langword="null"/>. <c>"N/A"</c> on 495 of 11,522
    /// rows measured 2026-08-30 — the most common sentinel on this path.</summary>
    [JsonPropertyName("lei")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Lei { get; init; }

    /// <summary>The security's title as filed — often the same text as <see cref="Name"/>, but not always:
    /// a futures line measured 2026-08-30 read <c>"S and P500 EMINI FUT MAR26 ESH6"</c> against a
    /// <see cref="Name"/> of <c>"CHICAGO MERCANTILE EXCH INC"</c>. Never measured carrying a sentinel, so it
    /// takes no converter.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>The security's CUSIP, or <see langword="null"/>. <c>"N/A"</c> on 202 of 11,522 rows measured
    /// 2026-08-30. Note that <c>"000000000"</c> also appears and is <b>not</b> treated as absence — it is a
    /// real filed value, and this SDK does not invent sentinels FMP did not send.</summary>
    [JsonPropertyName("cusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Cusip { get; init; }

    /// <summary>The security's ISIN, or <see langword="null"/>. <c>""</c> on 149 of 11,522 rows measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("isin")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Isin { get; init; }

    /// <summary>The position size, in the unit named by <see cref="Units"/>. <b>Signed and fractional</b> —
    /// measured values include <c>0.668</c>, so an integer type is wrong here for the same reason it is wrong
    /// on <see cref="EtfHolding.SharesNumber"/>.</summary>
    [JsonPropertyName("balance")] public decimal? Balance { get; init; }

    /// <summary>What <see cref="Balance"/> counts, as an SEC N-PORT code — measured 2026-08-30 over a
    /// 3,861-row sample: <c>NS</c> (number of shares) ×3,830, <c>NC</c> (contracts) ×29, <c>PA</c> (principal
    /// amount) ×2.
    ///
    /// <para>A free string rather than an enum: three values in one sample is a sample, not a vocabulary, and
    /// the SEC's list is longer than what was observed.</para></summary>
    [JsonPropertyName("units")] public string? Units { get; init; }

    /// <summary>The currency the position is denominated in.
    ///
    /// <para><b>The wire key is <c>cur_cd</c> — the only snake_case key in this SDK</b> — and the property
    /// takes a readable name while the attribute carries the wire verbatim, the same trade
    /// <see cref="MarketMover.ChangePercentage"/> makes. <b>Do not "fix" the attribute.</b></para>
    ///
    /// <para><b>It is not always three letters.</b> Measured 2026-08-30, 29 of 3,861 rows sent
    /// <c>"USDUSD"</c> — a doubled code, all of them equity-futures lines (<c>units NC</c>,
    /// <c>assetCat DE</c>). This field must therefore never be given a strict three-letter currency
    /// type.</para></summary>
    [JsonPropertyName("cur_cd")] public string? CurrencyCode { get; init; }

    /// <summary>The position's value in US dollars. Wire key <c>valUsd</c>. Measured range 2026-08-30:
    /// −41,402,229.68 to 125,580,304,518.46 — 17 significant digits, which is why this is
    /// <see cref="decimal"/>.</summary>
    [JsonPropertyName("valUsd")] public decimal? ValueUsd { get; init; }

    /// <summary>The position's share of the fund, as a percentage. Wire key <c>pctVal</c>.
    ///
    /// <para><b>Not bounded by 0 and 100.</b> Measured range 2026-08-30: −0.0032285713047007715 to
    /// <b>10.880031435864327</b>. Not range-checked, and must not be.</para></summary>
    [JsonPropertyName("pctVal")] public decimal? PercentValue { get; init; }

    /// <summary>The direction of the position — <c>"Long"</c> ×3,831 and <c>"N/A"</c> ×30 over the 3,861-row
    /// sample measured 2026-08-30, the <c>N/A</c> rows all being futures lines. The sentinel becomes
    /// <see langword="null"/>; no short position appeared in the sample, so <c>"Short"</c> is
    /// unmeasured.</summary>
    [JsonPropertyName("payoffProfile")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? PayoffProfile { get; init; }

    /// <summary>The SEC N-PORT asset category code. Wire key <c>assetCat</c>. Measured 2026-08-30 over a
    /// 3,861-row sample: <c>EC</c> ×3,818, <c>DE</c> ×30, <c>STIV</c> ×10, <c>DBT</c> ×2, <c>EP</c> ×1.
    /// Five values in one sample is a sample and not a vocabulary, so this is a free string rather than an
    /// enum, and the values above are recorded as observations.</summary>
    [JsonPropertyName("assetCat")] public string? AssetCategory { get; init; }

    /// <summary>The SEC N-PORT issuer category code. Wire key <c>issuerCat</c>. Measured 2026-08-30 over the
    /// same sample: <c>CORP</c> ×3,736, <c>OTHER</c> ×115, <c>RF</c> ×6, <c>UST</c> ×2, <c>PF</c> ×2. A free
    /// string, for the reason on <see cref="AssetCategory"/>.</summary>
    [JsonPropertyName("issuerCat")] public string? IssuerCategory { get; init; }

    /// <summary>The ISO-2 country the investment is attributed to. Wire key <c>invCountry</c>. Seventeen
    /// distinct codes plus <c>"N/A"</c> in the sample measured 2026-08-30; the sentinel becomes
    /// <see langword="null"/>.</summary>
    [JsonPropertyName("invCountry")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? InvestmentCountry { get; init; }

    /// <summary>Whether the security is restricted. Wire key <c>isRestrictedSec</c>, and the wire value is the
    /// <b>string</b> <c>"N"</c> or <c>"Y"</c> — see <see cref="YesNoBooleanJsonConverter"/>.
    ///
    /// <para><b>Its <c>Y</c> form is unmeasured</b>: <c>N</c> on all 3,861 rows sampled 2026-08-30. The
    /// converter is written so that an unexpected value nulls this one field rather than the row.</para></summary>
    [JsonPropertyName("isRestrictedSec")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsRestrictedSecurity { get; init; }

    /// <summary>The ASC 820 fair-value hierarchy level. Wire key <c>fairValLevel</c>.
    ///
    /// <para><b>A quoted integer that stays a <see cref="string"/>.</b> Measured 2026-08-30: <c>"1"</c>
    /// ×3,829, <c>"2"</c> ×28, <c>"3"</c> ×4, always quoted. It is a <b>code, not a quantity</b> — nothing a
    /// caller does with a fair-value level is arithmetic — so parsing it to <see cref="int"/> would invent a
    /// numeric identity the source does not have and gain nothing.</para>
    ///
    /// <para>No sentinel was ever measured on this field, so unlike its numeric-string cousin
    /// <c>FundShareClass.EntityOrgType</c> it carries no converter.</para></summary>
    [JsonPropertyName("fairValLevel")] public string? FairValueLevel { get; init; }

    /// <summary>Whether the position is cash collateral for a loaned security. <c>N</c> ×3,855, <c>Y</c> ×6
    /// over the sample measured 2026-08-30 — one of the two <c>is*</c> fields whose <c>Y</c> form was actually
    /// observed.</summary>
    [JsonPropertyName("isCashCollateral")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsCashCollateral { get; init; }

    /// <summary>Whether the position is non-cash collateral. <b>Its <c>Y</c> form is unmeasured</b>: <c>N</c>
    /// on all 3,861 rows sampled 2026-08-30.</summary>
    [JsonPropertyName("isNonCashCollateral")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsNonCashCollateral { get; init; }

    /// <summary>Whether the security is on loan from the fund. <c>N</c> ×3,605, <c>Y</c> ×256 over the sample
    /// measured 2026-08-30 — the most balanced of the four.</summary>
    [JsonPropertyName("isLoanByFund")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? IsLoanByFund { get; init; }
}
```

**Deferred crefs introduced by this record** — three, all already written as `<c>…</c>` above and promoted in
Task 7: `EtfAndFundsEndpoints.GetFundDisclosureAsync` (Task 6) and `FundShareClass.EntityOrgType` (Task 5).

- [ ] **Step 6: Write `FundDisclosureDate`**

Create `src/FmpDotNet/Models/FundDisclosureDate.cs`. `EtfAndFundsEndpoints.GetFundDisclosureAsync` is a
**deferred cref**:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One reporting period a fund has filed, from <c>stable/funds/disclosure-dates</c> — and the
/// <c>year</c>/<c>quarter</c> pair that selects it.
///
/// <para><b>This is the index for <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</b> That method takes a
/// year and a quarter, and answers <c>[]</c> at HTTP 200 for a period the fund never filed. This path is how a
/// caller finds out which periods exist, and <see cref="Year"/> and <see cref="Quarter"/> are the arguments to
/// pass, ready-made.</para>
///
/// <para><b><see cref="Date"/> is a FISCAL period end; <see cref="Year"/> and <see cref="Quarter"/> are
/// CALENDAR.</b> Measured 2026-08-30, SPY files on calendar quarter ends but FXAIX files on 2026-05-31 and
/// 2025-11-30, and ARKK on 2026-01-30 — so FXAIX's May date is reported as Q2. Verified over 80 rows across
/// three funds: <c>Year == Date.Year</c> and <c>Quarter == (Date.Month - 1) / 3 + 1</c> with <b>zero
/// mismatches</b>.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>newest first</b> across 127 rows. Coverage reaches back to
/// 2019-09-30 for SPY, 2019-11-30 for FXAIX and 2020-04-30 for ARKK — it differs per fund, which is why
/// nothing in this SDK bounds the <c>year</c> argument.</para></summary>
public sealed record FundDisclosureDate
{
    /// <summary>The fund's fiscal period end. Nullable because the deserialiser cannot promise a key is
    /// present, not because any measured row omitted it — no row was missing a key across all 127 measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The calendar year of <see cref="Date"/>. Pass it to
    /// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter of <see cref="Date"/> — 1 to 4. <b>Not the fund's own fiscal quarter
    /// number</b>: FXAIX's 2026-05-31 period end is reported here as <c>2</c>. Pass it to
    /// <c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }
}
```

- [ ] **Step 7: Register both records with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<FundDisclosure>))]
[JsonSerializable(typeof(List<FundDisclosureDate>))]
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS.

- [ ] **Step 9: Run the whole suite**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Serialization/NodaConverters.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Models/FundDisclosure.cs src/FmpDotNet/Models/FundDisclosureDate.cs \
        tests/FmpDotNet.Tests/EtfAndFundsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/funds-disclosure.SPY.2026q1.head.json \
        tests/FmpDotNet.Tests/Fixtures/funds-disclosure.dst-pair.json \
        tests/FmpDotNet.Tests/Fixtures/funds-disclosure-dates.SPY.json
git commit -m "feat: read Y/N flags and the Eastern acceptedDate, and the N-PORT disclosure shapes (#34)"
```

---

### Task 5: `FundHolder` and `FundShareClass`

The last two records, and the heaviest concentration of sentinels in the group. No new converter — both use
`SentinelStringJsonConverter` from Task 2.

**Files:**
- Create: `src/FmpDotNet/Models/FundHolder.cs`
- Create: `src/FmpDotNet/Models/FundShareClass.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs` (two entries)
- Create: `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-latest.SPY.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-search.nulls.json`
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: `SentinelStringJsonConverter` (Task 2), `NullableLocalDateJsonConverter` (existing).
- Produces: `public sealed record FmpDotNet.Models.FundHolder` with `string? Cik`, `string? Holder`,
  `string? SecurityCusip`, `decimal? Shares`, `LocalDate? DateReported`, `decimal? Change`,
  `decimal? WeightPercent`; `public sealed record FmpDotNet.Models.FundShareClass` with `string? Symbol`,
  `string? Cik`, `string? ClassId`, `string? SeriesId`, `string? EntityName`, `string? EntityOrgType`,
  `string? SeriesName`, `string? ClassName`, `string? ReportingFileNumber`, `string? Address`, `string? City`,
  `string? ZipCode`, `string? State`; `FmpJsonContext.Default.ListFundHolder` and `.ListFundShareClass`.
  Tasks 6 and 7 use these.

**One deviation from the spec's fixture list, and why.** The spec names the holders fixture
`funds-disclosure-holders-latest.SPY.head.json`. It is **not** a head here: the head of that response is three
rows all reporting the same date, which cannot show the mixed-date behaviour the record's doc turns on. The
fixture below is those three rows **plus two lifted from further down the same response**, and is named
`funds-disclosure-holders-latest.SPY.json` so the name does not claim to be a head. Every row is verbatim from
the 220-row capture; nothing is constructed.

- [ ] **Step 1: Write the two fixtures**

Create `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-latest.SPY.json` — five rows from the 220-row
response captured 2026-08-30 for `symbol=SPY`. The first three are the head; the last two are lifted from
further down to carry the older reporting dates and a negative `change`:

```json
[{"cik":"0001181848","holder":"SKYBRIDGE MULTI-ADVISER HEDGE FUND PORTFOLIOS LLC",
  "securityCusip":"78462F103","shares":122518791.23,"dateReported":"2026-06-30","change":0,
  "weightPercent":11.79723956},
 {"cik":"0001520568","holder":"Skybridge G II Fund, LLC","securityCusip":"78462F103","shares":3209819,
  "dateReported":"2026-06-30","change":0,"weightPercent":16.19559762},
 {"cik":"0001494928","holder":"RIVERPARK FUNDS TRUST","securityCusip":"78462F103","shares":3049046.052,
  "dateReported":"2026-06-30","change":0,"weightPercent":0.64064907},
 {"cik":"0000107606","holder":"VANGUARD WINDSOR FUNDS","securityCusip":"78462F103","shares":397921,
  "dateReported":"2026-04-30","change":-894335,"weightPercent":0.01063548},
 {"cik":"0000886048","holder":"FIRST INVESTORS EQUITY FUNDS","securityCusip":"78462F103","shares":4400,
  "dateReported":"2019-09-30","change":4400,"weightPercent":0.00082039}]
```

Create `tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-search.nulls.json` — four rows, each verbatim
from a measured capture, chosen so that all three sentinel spellings on this path appear beside a fully
populated row:

```json
[{"symbol":"BRACX","cik":"0001221845","classId":"C000003891","seriesId":"S000001469",
  "entityName":"BLACKROCK ALLOCATION TARGET SHARES","entityOrgType":"30","seriesName":"BATS SERIES C",
  "className":"BATS SERIES C","reportingFileNumber":"811-21457","address":"100 BELLEVUE PARKWAY",
  "city":"WILMINGTON","zipCode":"19809","state":"DE"},
 {"symbol":"NULL","cik":"0000110055","classId":"C000005579","seriesId":"S000002175",
  "entityName":"BLACKROCK SUSTAINABLE BALANCED FUND, INC.","entityOrgType":"NULL",
  "seriesName":"BLACKROCK SUSTAINABLE BALANCED FUND, INC.","className":"Investor B",
  "reportingFileNumber":"NULL","address":null,"city":"NULL","zipCode":"NULL","state":"NULL"},
 {"symbol":"PPPAX","cik":"0001175959","classId":"C000027617","seriesId":"S000009986",
  "entityName":"PIONEER PROTECTED PRINCIPAL TRUST","entityOrgType":"30",
  "seriesName":"Pioneer Protected Principal Plus",
  "className":"Pioneer Protected Principal Plus: Class A","reportingFileNumber":"811-21163","address":"",
  "city":"","zipCode":"","state":""},
 {"symbol":"OPPE","cik":"0001350487","classId":"C000151994","seriesId":"S000048091",
  "entityName":"WisdomTree Trust","entityOrgType":"30",
  "seriesName":"WisdomTree Europe Hedged SmallCap Equity Fund","className":"N/A",
  "reportingFileNumber":"811-21864","address":"250 WEST 34TH STREET","city":"NEW YORK","zipCode":"10119",
  "state":"NY"}]
```

- [ ] **Step 2: Write the failing tests**

Append inside `EtfAndFundsTests`:

```csharp
    [Fact]
    public void A_fund_holder_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(5, rows.Count);
        Assert.Equal("0001181848", rows[0].Cik);
        Assert.Equal("SKYBRIDGE MULTI-ADVISER HEDGE FUND PORTFOLIOS LLC", rows[0].Holder);
        Assert.Equal("78462F103", rows[0].SecurityCusip);
        Assert.Equal(122518791.23m, rows[0].Shares);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].DateReported);
        Assert.Equal(0m, rows[0].Change);
        Assert.Equal(11.79723956m, rows[0].WeightPercent);

        // Change is 0 on the head row, which Binding.Unbound does NOT count as unbound (only null, blank and
        // empty collections count), so the whole-record check goes on a row where every field is non-zero.
        Assert.Empty(Binding.Unbound(rows[3]));
    }

    [Fact]
    public void One_holders_response_mixes_reporting_dates_across_years()
    {
        // "Latest" is each HOLDER's own most recent filing, not a single as-of date for the response.
        // Measured 2026-08-30, SPY's 220 rows carried 19 distinct dates spanning 2019-09-30 to 2026-06-30,
        // and AAPL's 3,209 rows carried 66 spanning 2019-09-30 to 2026-07-31. Four recent dates dominate, but
        // 18 of SPY's rows and 292 of AAPL's report a date before 2026 at all — a holder that stopped filing
        // in 2019 is still in the response, with its 2019 position. Rows in one response are therefore NOT
        // comparable as of one date, and DateReported must be read per row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(3, rows.Select(r => r.DateReported).Distinct().Count());
        Assert.Equal(new LocalDate(2019, 9, 30), rows.Min(r => r.DateReported));
        Assert.Equal(new LocalDate(2026, 6, 30), rows.Max(r => r.DateReported));
    }

    [Fact]
    public void A_holders_change_is_signed_and_shares_are_fractional()
    {
        // Measured 2026-08-30: `change` was 0 on 2,532 of AAPL's 3,209 rows, positive on 291 and negative on
        // 386; `shares` ranged -990 to 1,016,998,069 and is fractional (122518791.23, 3049046.052).
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(-894335m, rows[3].Change);
        Assert.Equal(3049046.052m, rows[2].Shares);
    }

    [Fact]
    public void An_empty_holder_or_na_cusip_becomes_null()
    {
        // Both rows are verbatim measured captures from the AAPL response. `holder` was "" on 16 rows and
        // `securityCusip` was "N/A" on 3 — two different spellings on one path, and the reason the sentinel
        // converter is applied to both properties.
        var rows = JsonSerializer.Deserialize(
            """
            [{"cik":"0002042316","holder":"","securityCusip":"037833100","shares":3264563,
              "dateReported":"2026-06-30","change":-150796,"weightPercent":0.00216968},
             {"cik":"0002042513","holder":"Somebody","securityCusip":"N/A","shares":46772,
              "dateReported":"2026-06-30","change":3469,"weightPercent":0.04495317}]
            """,
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Null(rows[0].Holder);
        Assert.Equal("037833100", rows[0].SecurityCusip);
        Assert.Null(rows[1].SecurityCusip);
        Assert.Equal("Somebody", rows[1].Holder);
        Assert.Equal(-150796m, rows[0].Change);
    }

    [Fact]
    public void A_holder_weight_can_exceed_one_hundred()
    {
        // Measured range 2026-08-30: 1.2e-07 to 264.39824722. Not range-checked, and must not be — the third
        // percentage field in this group that exceeds 100.
        var rows = JsonSerializer.Deserialize(
            """[{"weightPercent":264.39824722},{"weightPercent":1.2e-07}]""",
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(264.39824722m, rows[0].WeightPercent);
        Assert.Equal(0.00000012m, rows[1].WeightPercent);
    }

    [Fact]
    public void A_fund_share_class_binds_all_thirteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Equal(4, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BRACX", rows[0].Symbol);
        Assert.Equal("0001221845", rows[0].Cik);
        Assert.Equal("C000003891", rows[0].ClassId);
        Assert.Equal("S000001469", rows[0].SeriesId);
        Assert.Equal("BLACKROCK ALLOCATION TARGET SHARES", rows[0].EntityName);
        Assert.Equal("30", rows[0].EntityOrgType);
        Assert.Equal("BATS SERIES C", rows[0].SeriesName);
        Assert.Equal("BATS SERIES C", rows[0].ClassName);
        Assert.Equal("811-21457", rows[0].ReportingFileNumber);
        Assert.Equal("100 BELLEVUE PARKWAY", rows[0].Address);
        Assert.Equal("WILMINGTON", rows[0].City);
        Assert.Equal("19809", rows[0].ZipCode);
        Assert.Equal("DE", rows[0].State);
    }

    [Fact]
    public void The_null_row_nulls_its_whole_address_block_and_keeps_everything_else()
    {
        // The sharpest case in the slice. Measured 2026-08-30, `entityOrgType`, `reportingFileNumber`,
        // `city`, `zipCode` and `state` were the literal string "NULL" on exactly the same 1,540 rows on
        // which `address` was a real JSON null — one missing address block, encoded two different ways inside
        // one object. `symbol` was "NULL" on 82 more rows than that, so it is not purely the same population.
        //
        // What survives is the point: cik, classId, seriesId, entityName, seriesName and className are all
        // real on this row. The sentinel converter must not cost them.
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)![1];

        Assert.Null(row.Symbol);
        Assert.Null(row.EntityOrgType);
        Assert.Null(row.ReportingFileNumber);
        Assert.Null(row.Address);
        Assert.Null(row.City);
        Assert.Null(row.ZipCode);
        Assert.Null(row.State);

        Assert.Equal("0000110055", row.Cik);
        Assert.Equal("C000005579", row.ClassId);
        Assert.Equal("S000002175", row.SeriesId);
        Assert.Equal("BLACKROCK SUSTAINABLE BALANCED FUND, INC.", row.EntityName);
        Assert.Equal("BLACKROCK SUSTAINABLE BALANCED FUND, INC.", row.SeriesName);
        Assert.Equal("Investor B", row.ClassName);
    }

    [Fact]
    public void The_address_block_carries_both_a_json_null_and_an_empty_string()
    {
        // Two rows, two encodings, one meaning. The BlackRock row sends address:null with "NULL" siblings;
        // the Pioneer row sends "" on all four. Both were measured 2026-08-30 — which is why Address takes
        // the converter even though its headline absence is a real JSON null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Null(rows[1].Address);   // JSON null   — 1,540 of 5,869 rows
        Assert.Null(rows[2].Address);   // ""          — 8 rows in the same corpus
        Assert.Null(rows[2].City);
        Assert.Equal("PPPAX", rows[2].Symbol);
        Assert.Equal("0001175959", rows[2].Cik);
    }

    [Theory]
    [InlineData("\"NULL\"")]
    [InlineData("\"N/A\"")]
    public void The_class_name_carries_two_spellings_of_absence(string wire)
    {
        // One field, two sentinels, in one corpus. On the widest query taken 2026-08-30 (`name=Trust`, 66,065
        // rows) `className` was "NULL" x1,278 AND "N/A" x192. A caller checking for one of the two would miss
        // the other, which is the argument for a converter over documentation here.
        // The hole is not last in the object — see the note in Every_spelling_of_absence_reads_as_null.
        var row = JsonSerializer.Deserialize(
            $$"""[{"className":{{wire}},"cik":"0001350487"}]""",
            FmpJsonContext.Default.ListFundShareClass)![0];

        Assert.Null(row.ClassName);
    }

    [Fact]
    public void The_entity_org_type_stays_a_string_and_its_sentinel_becomes_null()
    {
        // A numeric string with a non-numeric sentinel in the same field: "30" x3,635, "32" x17, "33" x5 and
        // "NULL" x1,540, measured 2026-08-30. Any caller reaching for int.Parse gets an outright failure on a
        // quarter of the rows. It stays a string because it is an SEC entity ORGANISATION TYPE — a code, not
        // a quantity — and nothing a caller does with it is arithmetic.
        var rows = JsonSerializer.Deserialize(
            """[{"entityOrgType":"30"},{"entityOrgType":"NULL"}]""",
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Equal("30", rows[0].EntityOrgType);
        Assert.Null(rows[1].EntityOrgType);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — `ListFundHolder` and `ListFundShareClass` do not exist (CS1061).

- [ ] **Step 4: Write `FundHolder`**

Create `src/FmpDotNet/Models/FundHolder.cs`. `EtfAndFundsEndpoints.GetFundHoldersAsync` is a **deferred cref**:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One institution's reported position in a security, from
/// <c>stable/funds/disclosure-holders-latest</c>.
///
/// <para><b>This is the reverse of <see cref="FundDisclosure"/>.</b> That answers "what does this fund hold";
/// this answers "which funds hold this security". The argument is the <b>held</b> symbol, and it need not be a
/// fund — measured 2026-08-30, <c>symbol=AAPL</c> answered 3,209 rows.</para>
///
/// <para><b>"Latest" is per holder, not per response.</b> One response mixes reporting dates by <b>years</b>:
/// measured 2026-08-30, SPY's 220 rows carried <b>19 distinct dates spanning 2019-09-30 to 2026-06-30</b> and
/// AAPL's 3,209 rows carried <b>66</b>. Four recent dates dominate both, but 18 of SPY's rows and 292 of
/// AAPL's report a date before 2026 at all — a holder that stopped filing in 2019 is still here, with its
/// 2019 position. <b>Rows in one response are not comparable as of one date</b>; read
/// <see cref="DateReported"/> per row before summing or ranking anything.</para>
///
/// <para><b><see cref="SecurityCusip"/> is not constant per response either.</b> AAPL's mixes the common stock
/// <c>037833100</c> with the bonds <c>037833EF3</c> and <c>037833DZ0</c>, and SPY's mixes <c>78462F103</c>
/// with <c>000000000</c> and synthetic identifiers. The path answers "funds holding <b>any security of this
/// issuer</b>".</para>
///
/// <para>No ordering was found, and there is no pagination — <c>limit</c> and <c>page</c> were ignored,
/// <c>symbol=AAPL</c> returning 3,209 rows and 701,175 bytes with and without them.</para></summary>
public sealed record FundHolder
{
    /// <summary>The holding institution's SEC Central Index Key, zero-padded to ten characters — the padding
    /// is the value, so this is a <see cref="string"/>. Nullable because the deserialiser cannot promise a key
    /// is present; no measured row omitted it.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The holding institution's name, or <see langword="null"/>. <c>""</c> on 16 of 3,979 rows
    /// measured 2026-08-30, mapped by <see cref="SentinelStringJsonConverter"/>. <see cref="Cik"/> was present
    /// on those rows, so an unnamed holder is still identifiable.</summary>
    [JsonPropertyName("holder")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Holder { get; init; }

    /// <summary>The CUSIP of the specific security held. <c>"N/A"</c> on 3 of 3,979 rows measured 2026-08-30.
    /// <b>Not constant across a response</b> — see the record's own summary.</summary>
    [JsonPropertyName("securityCusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? SecurityCusip { get; init; }

    /// <summary>Shares held. <b>Signed and fractional</b>: measured range 2026-08-30 was −990 to
    /// 1,016,998,069, with values like <c>122518791.23</c> and <c>3049046.052</c>.</summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The date this holder's position was reported as of. <b>Read it per row</b> — see the record's
    /// summary for why a single response is not one as-of date.</summary>
    [JsonPropertyName("dateReported")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateReported { get; init; }

    /// <summary>The change in shares since the holder's previous report. <b>Signed, and frequently zero</b> —
    /// measured 2026-08-30 over AAPL's 3,209 rows: 2,532 zero, 291 positive, 386 negative. A zero here is a
    /// reported no-change, not a missing value.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>The position's share of the <b>holder's</b> portfolio, as a percentage.
    ///
    /// <para><b>Not bounded by 100.</b> Measured range 2026-08-30: 1.2e-07 to <b>264.39824722</b>. Not
    /// range-checked, and must not be.</para></summary>
    [JsonPropertyName("weightPercent")] public decimal? WeightPercent { get; init; }
}
```

- [ ] **Step 5: Write `FundShareClass`**

Create `src/FmpDotNet/Models/FundShareClass.cs`. `EtfAndFundsEndpoints.SearchFundsByNameAsync` is a **deferred
cref**:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One SEC-registered fund share class, from <c>stable/funds/disclosure-holders-search</c>.
///
/// <para><b>These rows are not holders, despite the path's name.</b> Nothing in a row says who holds what:
/// they are registrant, series and class identifiers plus a filer address. The SDK's method is named for what
/// it returns — <c>EtfAndFundsEndpoints.SearchFundsByNameAsync</c> — and this doc carries the wire path, the
/// same trade <see cref="MarketMover.ChangePercentage"/> makes for a property name.</para>
///
/// <para><b>Matching is case-insensitive, whole-word and single-word.</b> Measured 2026-08-30:
/// <c>Vanguard</c>, <c>vanguard</c> and <c>VANGUARD</c> each returned the same 548 rows; <c>Vangua</c>
/// returned <b>0</b>; <c>van</c> returned 201 (<c>VAN KAMPEN…</c>); <c>Fid</c> and <c>fidelit</c> returned 0;
/// and <c>Vanguard Group</c> — a two-word company name, the most likely thing a caller types — returned
/// <b>0</b>. The exact tokenisation was not established and this SDK does not assert one.</para>
///
/// <para><b>The single largest response in the group comes from this path.</b> <c>name=Trust</c> returned
/// <b>66,065 rows and 27.4 MB</b> measured 2026-08-30, and there is no pagination anywhere in this group —
/// <c>limit</c> and <c>page</c> were ignored. There is no way to ask for less.</para>
///
/// <para><b>More than a quarter of rows are missing their address block</b>, spelled two ways at once. See
/// <see cref="Address"/>.</para></summary>
public sealed record FundShareClass
{
    /// <summary>The share class's ticker, or <see langword="null"/>. The literal string <c>"NULL"</c> on 1,622
    /// of 5,869 rows measured 2026-08-30 (27.6%), mapped by <see cref="SentinelStringJsonConverter"/>. Many
    /// share classes are not exchange-traded and have none.</summary>
    [JsonPropertyName("symbol")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Symbol { get; init; }

    /// <summary>The registrant's SEC Central Index Key, zero-padded to ten characters. Never measured
    /// carrying a sentinel, so no converter.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The SEC class identifier — <c>"C000003891"</c>. Never measured carrying a sentinel.</summary>
    [JsonPropertyName("classId")] public string? ClassId { get; init; }

    /// <summary>The SEC series identifier — <c>"S000001469"</c>. A series may have several classes, so this is
    /// the field that groups them. Never measured carrying a sentinel.</summary>
    [JsonPropertyName("seriesId")] public string? SeriesId { get; init; }

    /// <summary>The registrant's name — this is the field <c>name</c> matches against. Never measured
    /// carrying a sentinel.</summary>
    [JsonPropertyName("entityName")] public string? EntityName { get; init; }

    /// <summary>The SEC entity organisation type, or <see langword="null"/>.
    ///
    /// <para><b>A numeric string with a non-numeric sentinel in the same field.</b> Measured 2026-08-30:
    /// <c>"30"</c> ×3,635, <c>"32"</c> ×17, <c>"33"</c> ×5 — and the literal <c>"NULL"</c> ×1,540. A caller
    /// reaching for <c>int.Parse</c> gets an outright failure on more than a quarter of rows, which is why the
    /// sentinel is converted here.</para>
    ///
    /// <para>It stays a <see cref="string"/> because it is a <b>code, not a quantity</b>: nothing a caller
    /// does with an organisation type is arithmetic, and parsing it would invent a numeric identity the source
    /// does not have.</para></summary>
    [JsonPropertyName("entityOrgType")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? EntityOrgType { get; init; }

    /// <summary>The fund series' name. Never measured carrying a sentinel — unlike
    /// <see cref="ClassName"/>, which is often the same text.</summary>
    [JsonPropertyName("seriesName")] public string? SeriesName { get; init; }

    /// <summary>The share class's name — <c>"Investor B"</c>, <c>"BATS SERIES C"</c> — or
    /// <see langword="null"/>.
    ///
    /// <para><b>The one field measured carrying two different string sentinels.</b> On the widest query taken
    /// 2026-08-30 (<c>name=Trust</c>, 66,065 rows) it was <c>"NULL"</c> ×1,278 <b>and</b> <c>"N/A"</c> ×192.
    /// A caller checking for one spelling would miss the other; <see cref="SentinelStringJsonConverter"/>
    /// maps both.</para></summary>
    [JsonPropertyName("className")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ClassName { get; init; }

    /// <summary>The registrant's SEC file number — <c>"811-21457"</c> — or <see langword="null"/>.
    /// <c>"NULL"</c> on 1,540 of 5,869 rows measured 2026-08-30, the same rows on which
    /// <see cref="Address"/> is absent.</summary>
    [JsonPropertyName("reportingFileNumber")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ReportingFileNumber { get; init; }

    /// <summary>The filer's street address, or <see langword="null"/>.
    ///
    /// <para><b>Absent on more than a quarter of rows, in two encodings.</b> Measured 2026-08-30 it was a real
    /// JSON <see langword="null"/> on 1,540 of 5,869 rows (26.2%) and <c>""</c> on 8 more — which is why it
    /// carries <see cref="SentinelStringJsonConverter"/> even though its headline absence is a genuine
    /// null.</para>
    ///
    /// <para><b>The whole address block travels together.</b> <see cref="EntityOrgType"/>,
    /// <see cref="ReportingFileNumber"/>, <see cref="City"/>, <see cref="ZipCode"/> and <see cref="State"/>
    /// were the literal string <c>"NULL"</c> on exactly the same 1,540 rows — one missing block, encoded two
    /// different ways inside one JSON object.</para></summary>
    [JsonPropertyName("address")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Address { get; init; }

    /// <summary>The filer's city, or <see langword="null"/>. <c>"NULL"</c> on 1,540 of 5,869 rows measured
    /// 2026-08-30; see <see cref="Address"/>.</summary>
    [JsonPropertyName("city")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? City { get; init; }

    /// <summary>The filer's postal code, or <see langword="null"/>. A <see cref="string"/> because leading
    /// zeros are part of a ZIP code. <c>"NULL"</c> on 1,540 of 5,869 rows measured 2026-08-30.</summary>
    [JsonPropertyName("zipCode")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ZipCode { get; init; }

    /// <summary>The filer's state, as a two-letter code, or <see langword="null"/>. <c>"NULL"</c> on 1,540 of
    /// 5,869 rows measured 2026-08-30 — the case that makes this converter's cost worth paying: without it a
    /// caller writing <c>row.State ?? "unknown"</c> gets the string <c>"NULL"</c> on a quarter of rows and no
    /// warning.</summary>
    [JsonPropertyName("state")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? State { get; init; }
}
```

- [ ] **Step 6: Register both records with the source generator**

Add to `src/FmpDotNet/Serialization/FmpJsonContext.cs`:

```csharp
[JsonSerializable(typeof(List<FundHolder>))]
[JsonSerializable(typeof(List<FundShareClass>))]
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test`
Expected: PASS. All nine records now exist; nothing calls the API yet.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Models/FundHolder.cs \
        src/FmpDotNet/Models/FundShareClass.cs tests/FmpDotNet.Tests/EtfAndFundsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-latest.SPY.json \
        tests/FmpDotNet.Tests/Fixtures/funds-disclosure-holders-search.nulls.json
git commit -m "feat: model fund holders and SEC share classes, sentinels and all (#34)"
```

---

### Task 6: The facade, the five ETF methods, and the wiring

Adding a facade to this SDK is **five edits**, and the repo has paid for forgetting one before: the count
assertion in `AddFmpTests` exists because three groups were once added to `FmpClient` and never named in that
test. All five are in this task.

**Files:**
- Create: `src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs`
- Modify: `src/FmpDotNet/FmpClient.cs` (constructor parameter **and** property)
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs:140` (one `TryAddTransient`)
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs:52,57` (one assertion, and the count **19 → 20**)
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: `EtfAssetExposure`, `EtfCountryWeighting`, `EtfHolding`, `EtfInfo`, `EtfSectorWeighting` and their
  `FmpJsonContext` entries (Tasks 1-3).
- Produces: `public sealed class FmpDotNet.Endpoints.EtfAndFundsEndpoints(FmpTransport transport)` with
  `Task<IReadOnlyList<EtfAssetExposure>> GetEtfAssetExposureAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<EtfCountryWeighting>> GetEtfCountryWeightingsAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<EtfHolding>> GetEtfHoldingsAsync(string symbol, CancellationToken ct = default)`,
  `Task<EtfInfo?> GetEtfInfoAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<EtfSectorWeighting>> GetEtfSectorWeightingsAsync(string symbol, CancellationToken ct = default)`,
  and the private `static void ThrowIfNotOneSymbol(string symbol)`; `FmpClient.EtfAndFunds`. **Task 7 adds four
  more methods to this same class and reuses `ThrowIfNotOneSymbol`.**

- [ ] **Step 1: Write the failing tests**

Append inside `EtfAndFundsTests`, adding `using FmpDotNet.Endpoints;` and
`using Microsoft.Extensions.Options;` to the file's using block:

```csharp
    private static (EtfAndFundsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new EtfAndFundsEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Theory]
    [InlineData("asset-exposure", "/stable/etf/asset-exposure")]
    [InlineData("country-weightings", "/stable/etf/country-weightings")]
    [InlineData("holdings", "/stable/etf/holdings")]
    [InlineData("info", "/stable/etf/info")]
    [InlineData("sector-weightings", "/stable/etf/sector-weightings")]
    public async Task Each_etf_method_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        switch (which)
        {
            case "asset-exposure": await endpoints.GetEtfAssetExposureAsync("QQQ"); break;
            case "country-weightings": await endpoints.GetEtfCountryWeightingsAsync("QQQ"); break;
            case "holdings": await endpoints.GetEtfHoldingsAsync("QQQ"); break;
            case "info": await endpoints.GetEtfInfoAsync("QQQ"); break;
            default: await endpoints.GetEtfSectorWeightingsAsync("QQQ"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task An_etf_method_sends_the_symbol_and_nothing_but_the_key_beside_it()
    {
        // Measured 2026-08-30: `limit` and `page` are ignored on all nine paths — byte-identical responses
        // with and without them, including a 17,252-row, 4.9 MB etf/holdings?symbol=BND. Offering either
        // would let a caller believe a page happened. Asserted against the WHOLE query string, not just those
        // two, so any future parameter this method starts sending is caught as well.
        var (endpoints, handler) = Build();

        await endpoints.GetEtfHoldingsAsync("QQQ");

        Assert.Equal("?symbol=QQQ&apikey=k", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Get_etf_info_returns_the_single_row_rather_than_a_list()
    {
        // All 33 responses measured 2026-08-30 were single-element arrays, which is why this one method on
        // the facade returns a record instead of a list — the CompanyEndpoints.GetProfileAsync precedent.
        var (endpoints, _) = Build(Binding.Fixture("etf-info.SPY.json"));

        var info = await endpoints.GetEtfInfoAsync("SPY");

        Assert.NotNull(info);
        Assert.Equal("SPY", info.Symbol);
        Assert.Equal(12, info.SectorsList!.Count);
    }

    [Fact]
    public async Task Get_etf_info_returns_null_when_the_array_is_empty()
    {
        // An unknown symbol answers `[]` at HTTP 200, not an error — measured 2026-08-30, and so does a
        // perfectly valid stock ticker: AAPL returned `[]` on all four ETF-only paths.
        var (endpoints, _) = Build();

        Assert.Null(await endpoints.GetEtfInfoAsync("AAPL"));
    }

    [Theory]
    [InlineData("asset-exposure")]
    [InlineData("country-weightings")]
    [InlineData("holdings")]
    [InlineData("info")]
    [InlineData("sector-weightings")]
    public async Task A_comma_in_the_symbol_is_rejected_before_the_request_goes_out(string which)
    {
        // Measured 2026-08-30: `symbol=SPY,QQQ` returns `[]` with HTTP 200 on etf/info and
        // etf/sector-weightings, while the plural `symbols=` is a 400. So the comma-joined form that works on
        // QuoteEndpoints.Batch is not merely unsupported here — it is a SILENT WRONG ANSWER, indistinguishable
        // from "this ETF has no data". This is the one place in the slice where a signature can prevent one.
        //
        // Deliberately narrow: it rejects the COMMA, not "not a known ETF". An unknown symbol legitimately
        // answers [] and so does a stock; those are honest empties and stay documented rather than guarded.
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "asset-exposure" => endpoints.GetEtfAssetExposureAsync("SPY,QQQ"),
            "country-weightings" => endpoints.GetEtfCountryWeightingsAsync("SPY,QQQ"),
            "holdings" => endpoints.GetEtfHoldingsAsync("SPY,QQQ"),
            "info" => endpoints.GetEtfInfoAsync("SPY,QQQ"),
            _ => endpoints.GetEtfSectorWeightingsAsync("SPY,QQQ"),
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Equal("symbol", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("asset-exposure")]
    [InlineData("country-weightings")]
    [InlineData("holdings")]
    [InlineData("info")]
    [InlineData("sector-weightings")]
    public async Task A_blank_symbol_is_rejected_before_the_request_goes_out(string which)
    {
        // Measured 2026-08-30: a bare `symbol=` is an HTTP 400 from FMP on every one of these paths.
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "asset-exposure" => endpoints.GetEtfAssetExposureAsync("  "),
            "country-weightings" => endpoints.GetEtfCountryWeightingsAsync("  "),
            "holdings" => endpoints.GetEtfHoldingsAsync("  "),
            "info" => endpoints.GetEtfInfoAsync("  "),
            _ => endpoints.GetEtfSectorWeightingsAsync("  "),
        };

        await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — `EtfAndFundsEndpoints` does not exist (CS0246).

- [ ] **Step 3: Write the facade with its five ETF methods**

Create `src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs`. Task 7 appends four more methods to this class:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>ETFs and mutual funds — what a fund holds, who holds a fund, and the SEC filings behind both.
///
/// <para><b>Three things hold across all nine paths, measured 2026-08-30, and a caller should read them
/// once.</b></para>
///
/// <list type="number">
///   <item><description><b>There is no pagination anywhere in this group.</b> <c>limit</c> and <c>page</c>
///     were ignored on every path — verified by byte-identical responses with and without them, including a
///     17,252-row, 4.9 MB <c>etf/holdings?symbol=BND</c>. There are therefore no walk helpers and no page
///     ceilings here, unlike three other facades on this client, and <b>no way to ask for less than
///     everything</b>. Two methods can return a great deal: <see cref="GetEtfHoldingsAsync"/> and
///     <c>SearchFundsByNameAsync</c>, whose <c>name=Trust</c> query returned <b>66,065 rows and
///     27.4 MB</b>.</description></item>
///   <item><description><b>Unknown input answers <c>[]</c> at HTTP 200, not an error.</b> An unknown symbol,
///     a stock symbol on an ETF-only path (AAPL returned <c>[]</c> on all four), a year outside a fund's
///     coverage, and a <c>quarter</c> of 0 or 5 all do this. Only a missing or malformed parameter is a
///     400.</description></item>
///   <item><description><b>One symbol per call.</b> <c>symbol=SPY,QQQ</c> answers <c>[]</c> at HTTP 200 —
///     a silent wrong answer — and the plural <c>symbols=</c> is a 400. Every method here rejects a comma
///     rather than letting that happen.</description></item>
/// </list>
///
/// <para><b>Method names carry <c>Etf</c> or <c>Fund</c> on purpose.</b> <c>GetHoldings</c> and
/// <c>GetDisclosure</c> on one facade would read as two views of one thing. They point opposite ways:
/// <see cref="GetEtfHoldingsAsync"/> is what a fund owns, <c>GetFundHoldersAsync</c> is who owns a
/// security.</para></summary>
public sealed class EtfAndFundsEndpoints(FmpTransport transport)
{
    /// <summary>Which ETFs hold a given security, from <c>stable/etf/asset-exposure</c>.
    ///
    /// <para><b>This runs the opposite way from the other four <c>etf/*</c> methods.</b> The argument is the
    /// <b>held asset</b>, not the fund: measured 2026-08-30, <c>AAPL</c> answered 3,293 rows each naming a
    /// different ETF. Any asset works, including an ETF — <c>SPY</c> answered 39 rows.</para></summary>
    /// <param name="symbol">The held security. One symbol; a comma-joined list is rejected, because FMP
    /// answers it with an empty array at HTTP 200.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every ETF position in the asset, in FMP's own order. <b>No ordering was found</b> in the
    /// responses measured 2026-08-30. An asset no ETF holds answers an empty list. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<EtfAssetExposure>> GetEtfAssetExposureAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/asset-exposure").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfAssetExposure, ct);
    }

    /// <summary>An ETF's country breakdown, from <c>stable/etf/country-weightings</c>.
    ///
    /// <para><b>The weights arrive as percent-suffixed strings on this path and as bare numbers on
    /// <see cref="GetEtfSectorWeightingsAsync"/></b>, one letter apart in the URL. The SDK reconciles them —
    /// see <see cref="EtfCountryWeighting.WeightPercentage"/>.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The breakdown, in FMP's own order. Measured 2026-08-30 that order is <b>by weight,
    /// descending</b>. A commodity fund still answers a row: GLD and SLV each returned <c>"Other"</c> at
    /// <c>"100%"</c>. The list can be empty — some symbols answer <c>[]</c> at HTTP 200 rather than an
    /// error — but is never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfCountryWeighting>> GetEtfCountryWeightingsAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/country-weightings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfCountryWeighting, ct);
    }

    /// <summary>Everything an ETF holds, from <c>stable/etf/holdings</c>.
    ///
    /// <para><b>This is the method to size before calling.</b> There is no pagination and no way to ask for
    /// less: measured 2026-08-30, <c>BND</c> answered <b>17,252 rows and 4.9 MB</b> and <c>VXUS</c> 8,821 rows
    /// and 2.5 MB, and <c>limit</c> and <c>page</c> changed neither by a byte.
    /// <see cref="EtfInfo.HoldingsCount"/> cannot be used to predict the size — it agreed with this path on
    /// <b>one</b> of 33 ETFs.</para>
    ///
    /// <para>Rows for a bond fund mostly have no ticker: <see cref="EtfHolding.Asset"/> was empty on 51.1% of
    /// 35,185 rows measured. <see cref="EtfHolding.Name"/> was populated on all of them.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holding, in FMP's own order. Measured 2026-08-30 that order is <b>by weight,
    /// descending</b>, and it held over the full 17,252-row BND response. A stock symbol answers an empty
    /// list. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfHolding>> GetEtfHoldingsAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/holdings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfHolding, ct);
    }

    /// <summary>An ETF's fact sheet, from <c>stable/etf/info</c>.
    ///
    /// <para>All 33 responses measured 2026-08-30 were single-element arrays, so this returns one record
    /// rather than a list — the <see cref="CompanyEndpoints.GetProfileAsync"/> precedent. The record carries
    /// the fund's sector breakdown inline: <see cref="EtfInfo.SectorsList"/> measured <b>identical</b> to
    /// <see cref="GetEtfSectorWeightingsAsync"/> on all 13 ETFs cross-checked, so a caller holding this does
    /// not need that call.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The fact sheet, or <see langword="null"/> when FMP answered an empty array — which is what an
    /// unknown symbol and a stock symbol both do, at HTTP 200.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<EtfInfo?> GetEtfInfoAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/etf/info").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfInfo, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>An ETF's sector breakdown, from <c>stable/etf/sector-weightings</c>.
    ///
    /// <para><b>This data is already inside <see cref="GetEtfInfoAsync"/>'s answer.</b> Measured 2026-08-30,
    /// <see cref="EtfInfo.SectorsList"/> agreed with this path on the key set and on every value, with no
    /// rounding difference, on all 13 ETFs cross-checked. Calling both is a wasted request.</para>
    ///
    /// <para>The weights are bare JSON numbers here, unlike
    /// <see cref="GetEtfCountryWeightingsAsync"/>'s.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The breakdown, in FMP's own order. Measured 2026-08-30 that order is <b>alphabetical by
    /// sector</b> — not by weight, unlike <see cref="GetEtfCountryWeightingsAsync"/>. A commodity fund answers
    /// one row, <c>Cash &amp; Others</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfSectorWeighting>> GetEtfSectorWeightingsAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/sector-weightings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfSectorWeighting, ct);
    }

    /// <summary>Rejects a symbol FMP would answer with silence.
    ///
    /// <para>Two failures, one guard. A blank <c>symbol=</c> is an HTTP 400 on every path in this group,
    /// measured 2026-08-30 — an error the caller would see. A comma-joined list is worse: <c>symbol=SPY,QQQ</c>
    /// answers <b><c>[]</c> at HTTP 200</b> on <c>etf/info</c> and <c>etf/sector-weightings</c>, which is
    /// indistinguishable from "this fund has no data", while the plural <c>symbols=</c> is a 400. The
    /// comma-joined form that <see cref="QuoteEndpoints"/>' batch methods take is therefore not merely
    /// unsupported here — it is a silent wrong answer.</para>
    ///
    /// <para><b>Narrow on purpose.</b> This rejects the comma, not "not a known ETF". An unknown symbol
    /// legitimately answers <c>[]</c>, and so does a perfectly valid stock — measured 2026-08-30,
    /// <c>AAPL</c> returned <c>[]</c> on all four ETF-only paths. Those are honest empties and are documented
    /// rather than guarded.</para>
    ///
    /// <para>The parameter is named <c>symbol</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentException.ParamName"/>.</para></summary>
    private static void ThrowIfNotOneSymbol(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (symbol.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "These paths take one symbol. Measured 2026-08-30, a comma-joined list answers an empty "
                + "array with HTTP 200 — a silent wrong answer, not an error. Call once per symbol.",
                nameof(symbol));
        }
    }
}
```

**Deferred crefs introduced here** — `GetFundHoldersAsync` and `SearchFundsByNameAsync` are referenced in the
class summary and do not exist until Task 7. Both are already written as `<c>…</c>` above; Task 7 promotes
them.

- [ ] **Step 4: Wire the facade into `FmpClient`**

In `src/FmpDotNet/FmpClient.cs`, add the constructor parameter to the last line of the parameter list:

```csharp
    TechnicalIndicatorsEndpoints technicalIndicators, MarketPerformanceEndpoints marketPerformance,
    EtfAndFundsEndpoints etfAndFunds)
```

and the property at the end of the class, after `MarketPerformance`:

```csharp
    /// <summary>ETFs and mutual funds — holdings, exposures, fund fact sheets, and the SEC N-PORT filings
    /// behind them.
    ///
    /// <para><b>No path in this group paginates and none can be narrowed</b>, so two of the nine methods can
    /// return tens of thousands of rows. See <see cref="EtfAndFundsEndpoints"/> before calling
    /// <see cref="EtfAndFundsEndpoints.GetEtfHoldingsAsync"/> or
    /// <c>EtfAndFundsEndpoints.SearchFundsByNameAsync</c> in a loop.</para></summary>
    public EtfAndFundsEndpoints EtfAndFunds { get; } = etfAndFunds;
```

**`SearchFundsByNameAsync` does not exist until Task 7**, which is why it is written as `<c>…</c>` above.
Task 7 promotes it.

- [ ] **Step 5: Register the facade for dependency injection**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, after line 140's
`services.TryAddTransient<MarketPerformanceEndpoints>();` and **before** `services.TryAddTransient<FmpClient>();`:

```csharp
        services.TryAddTransient<EtfAndFundsEndpoints>();
```

- [ ] **Step 6: Update `AddFmpTests`**

In `tests/FmpDotNet.Tests/AddFmpTests.cs`, add one line after `Assert.NotNull(client.MarketPerformance);`:

```csharp
        Assert.NotNull(client.EtfAndFunds);
```

and change the count on line 57 from `19` to `20`:

```csharp
        Assert.Equal(20, typeof(FmpClient)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length);
```

Both edits are required. The count assertion exists precisely because the `Assert.NotNull` list was once three
short — changing one without the other reintroduces that failure in the opposite direction.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EtfAndFundsTests|FullyQualifiedName~AddFmpTests"`
Expected: PASS.

- [ ] **Step 8: Run the whole suite and confirm the ONE expected failure**

Run: `dotnet test`
Expected: **exactly one failure**, and its name must be
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`. The
code now calls five paths the README's generated block does not list. Task 9 regenerates it.

If the failure count is anything other than one, or the name is different, that is a real failure — stop and
investigate rather than treating it as the known one.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Tests/EtfAndFundsTests.cs
git commit -m "feat: add the EtfAndFunds facade and its five ETF methods (#34)"
```

---

### Task 7: The four fund methods, and promoting every deferred cref

The facade's second half, and the task that pays off the deferred-cref debt from Tasks 1-6. Every type
referenced anywhere in this slice now exists, so every `<c>…</c>` placeholder becomes a real `<see cref>` and
the compiler starts checking them.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs` (four methods, one guard, and two crefs)
- Modify: `src/FmpDotNet/FmpClient.cs` (one cref)
- Modify: `src/FmpDotNet/Models/EtfSectorWeighting.cs` (four crefs)
- Modify: `src/FmpDotNet/Models/EtfHolding.cs` (three crefs)
- Modify: `src/FmpDotNet/Models/EtfInfo.cs` (two crefs)
- Modify: `src/FmpDotNet/Models/FundDisclosure.cs` (two crefs)
- Modify: `src/FmpDotNet/Models/FundDisclosureDate.cs` (three crefs)
- Modify: `src/FmpDotNet/Models/FundShareClass.cs` (one cref)
- Modify: `tests/FmpDotNet.Tests/EtfAndFundsTests.cs`

**Interfaces:**
- Consumes: `EtfAndFundsEndpoints` and its private `ThrowIfNotOneSymbol` (Task 6); `FundDisclosure`,
  `FundDisclosureDate` (Task 4); `FundHolder`, `FundShareClass` (Task 5).
- Produces:
  `Task<IReadOnlyList<FundDisclosure>> GetFundDisclosureAsync(string symbol, int year, int quarter, CancellationToken ct = default)`,
  `Task<IReadOnlyList<FundDisclosureDate>> GetFundDisclosureDatesAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<FundHolder>> GetFundHoldersAsync(string symbol, CancellationToken ct = default)`,
  `Task<IReadOnlyList<FundShareClass>> SearchFundsByNameAsync(string name, CancellationToken ct = default)`,
  and the private `static void ThrowIfQuarterOutOfRange(int quarter)`. Task 8 supplies live arguments for all
  four by parameter name; Task 9 regenerates the README from them.

- [ ] **Step 1: Write the failing tests**

Append inside `EtfAndFundsTests`:

```csharp
    [Theory]
    [InlineData("disclosure", "/stable/funds/disclosure")]
    [InlineData("dates", "/stable/funds/disclosure-dates")]
    [InlineData("holders", "/stable/funds/disclosure-holders-latest")]
    [InlineData("search", "/stable/funds/disclosure-holders-search")]
    public async Task Each_fund_method_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        switch (which)
        {
            case "disclosure": await endpoints.GetFundDisclosureAsync("SPY", 2026, 1); break;
            case "dates": await endpoints.GetFundDisclosureDatesAsync("SPY"); break;
            case "holders": await endpoints.GetFundHoldersAsync("AAPL"); break;
            default: await endpoints.SearchFundsByNameAsync("Schwab"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task The_disclosure_call_sends_the_symbol_the_year_and_the_quarter()
    {
        // Asserted against the whole query string: `limit` and `page` are ignored by FMP on this path
        // (measured 2026-08-30, `funds/disclosure?symbol=SPY&year=2026&quarter=1&limit=10` returned all 503
        // rows), so offering either would let a caller believe a page happened.
        var (endpoints, handler) = Build();

        await endpoints.GetFundDisclosureAsync("SPY", 2026, 1);

        Assert.Equal("?symbol=SPY&year=2026&quarter=1&apikey=k", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_search_call_sends_the_name_under_its_own_parameter()
    {
        var (endpoints, handler) = Build();

        await endpoints.SearchFundsByNameAsync("Schwab");

        Assert.Equal("?name=Schwab&apikey=k", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_rejected_before_the_request_goes_out(int quarter)
    {
        // Measured 2026-08-30: quarter=0 and quarter=5 both return HTTP 200 with `[]`, while quarter=Q1 is a
        // 400. So a caller who sends 0 is told "no holdings", not "bad request" — the same silent-empty
        // failure the comma guard exists for. Four quarters is not a measurement; it is what a quarter is.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFundDisclosureAsync("SPY", 2026, quarter));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(1990)]
    [InlineData(2030)]
    public async Task The_year_is_deliberately_not_bounded(int year)
    {
        // Measured 2026-08-30: year=1990 and year=2030 both return HTTP 200 with `[]`, and year=abc is a 400.
        // No bound is imposed here, and this test is what stops one being added: a lower bound would have to
        // come from measured coverage extents, which differ per fund (2019-09-30 SPY, 2019-11-30 FXAIX,
        // 2020-04-30 ARKK) and will move. Encoding one of them would be inventing a fact.
        var (endpoints, handler) = Build();

        var rows = await endpoints.GetFundDisclosureAsync("SPY", year, 1);

        Assert.Empty(rows);
        Assert.Single(handler.Requests);
        Assert.Contains($"year={year}", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData("disclosure")]
    [InlineData("dates")]
    [InlineData("holders")]
    public async Task A_comma_in_the_symbol_is_rejected_on_the_fund_paths_too(string which)
    {
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "disclosure" => endpoints.GetFundDisclosureAsync("SPY,QQQ", 2026, 1),
            "dates" => endpoints.GetFundDisclosureDatesAsync("SPY,QQQ"),
            _ => endpoints.GetFundHoldersAsync("SPY,QQQ"),
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Equal("symbol", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("disclosure")]
    [InlineData("dates")]
    [InlineData("holders")]
    public async Task A_blank_symbol_is_rejected_on_the_fund_paths_too(string which)
    {
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "disclosure" => endpoints.GetFundDisclosureAsync("  ", 2026, 1),
            "dates" => endpoints.GetFundDisclosureDatesAsync("  "),
            _ => endpoints.GetFundHoldersAsync("  "),
        };

        await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_name_is_rejected_before_the_request_goes_out()
    {
        // Measured 2026-08-30: a bare `name=` is an HTTP 400 on this path.
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.SearchFundsByNameAsync("  "));

        Assert.Equal("name", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task All_nine_paths_are_reachable_and_each_asks_a_different_one()
    {
        // The whole surface in one assertion. Nine methods, nine distinct paths, no duplicates and no typos —
        // measured 2026-08-30, no two of the nine share a key tuple either, so a copy-paste that pointed two
        // methods at one path would bind the wrong shape without failing anything else here.
        var (endpoints, handler) = Build();

        await endpoints.GetEtfAssetExposureAsync("QQQ");
        await endpoints.GetEtfCountryWeightingsAsync("QQQ");
        await endpoints.GetEtfHoldingsAsync("QQQ");
        await endpoints.GetEtfInfoAsync("QQQ");
        await endpoints.GetEtfSectorWeightingsAsync("QQQ");
        await endpoints.GetFundDisclosureAsync("QQQ", 2025, 3);
        await endpoints.GetFundDisclosureDatesAsync("QQQ");
        await endpoints.GetFundHoldersAsync("QQQ");
        await endpoints.SearchFundsByNameAsync("Schwab");

        Assert.Equal(
            [
                "/stable/etf/asset-exposure",
                "/stable/etf/country-weightings",
                "/stable/etf/holdings",
                "/stable/etf/info",
                "/stable/etf/sector-weightings",
                "/stable/funds/disclosure",
                "/stable/funds/disclosure-dates",
                "/stable/funds/disclosure-holders-latest",
                "/stable/funds/disclosure-holders-search",
            ],
            handler.Requests.Select(u => u.AbsolutePath).ToArray());
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: **build failure** — the four methods do not exist (CS1061).

- [ ] **Step 3: Add the four methods and the quarter guard**

Append to `EtfAndFundsEndpoints`, before the private `ThrowIfNotOneSymbol`:

```csharp
    /// <summary>A fund's filed portfolio for one quarter, from <c>stable/funds/disclosure</c> — the holding
    /// lines of its SEC Form N-PORT.
    ///
    /// <para><b>This is the fund's own filing, not FMP's cached view.</b> Where
    /// <see cref="GetEtfHoldingsAsync"/> answers "what does FMP think this ETF holds now", this answers "what
    /// did the fund tell the SEC it held on this date", with a real as-of date and an EDGAR acceptance
    /// timestamp.</para>
    ///
    /// <para><b>Find the periods first.</b> A quarter the fund never filed answers an empty list at HTTP 200,
    /// and so does a quarter outside FMP's coverage — measured 2026-08-30, every quarter from 2024 Q1 to
    /// 2026 Q2 answered while 2026 Q3 and Q4 were empty. <see cref="GetFundDisclosureDatesAsync"/> returns the
    /// <c>year</c> and <c>quarter</c> pairs that exist, ready to pass here.</para>
    ///
    /// <para><b>Funds do not all file on calendar quarters.</b> <paramref name="quarter"/> is the
    /// <b>calendar</b> quarter of the fund's fiscal period end, so FXAIX's 2026-05-31 period is Q2 — see
    /// <see cref="FundDisclosureDate"/>.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="year">The calendar year, as <see cref="FundDisclosureDate.Year"/> reports it.
    /// <b>Deliberately unbounded</b>: coverage differs per fund — measured 2026-08-30 it reached back to
    /// 2019-09-30 for SPY, 2019-11-30 for FXAIX and 2020-04-30 for ARKK — and will move, so any bound written
    /// here would be a fabricated fact. A year outside coverage answers an empty list.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4, as <see cref="FundDisclosureDate.Quarter"/> reports
    /// it. Rejected outside that range: measured 2026-08-30, FMP answers <c>quarter=0</c> and
    /// <c>quarter=5</c> with an empty list at HTTP 200 rather than an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holding line in the filing, in FMP's own order. <b>No ordering was found</b> in the
    /// responses measured 2026-08-30. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is not 1 to 4.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundDisclosure>> GetFundDisclosureAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        ThrowIfQuarterOutOfRange(quarter);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListFundDisclosure, ct);
    }

    /// <summary>Which reporting periods a fund has filed, from <c>stable/funds/disclosure-dates</c>.
    ///
    /// <para><b>This is the index for <see cref="GetFundDisclosureAsync"/>.</b> Each row carries the fiscal
    /// period end together with the calendar <c>year</c> and <c>quarter</c> that select it, so a caller pairs
    /// the two calls without doing the arithmetic — which matters because funds do not all file on calendar
    /// quarters.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every filed period, in FMP's own order. Measured 2026-08-30 that order is <b>newest
    /// first</b>. A symbol with no filings answers an empty list — AAPL did. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundDisclosureDate>> GetFundDisclosureDatesAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-dates").With("symbol", symbol),
            FmpJsonContext.Default.ListFundDisclosureDate, ct);
    }

    /// <summary>Which institutions hold a given security, from
    /// <c>stable/funds/disclosure-holders-latest</c>.
    ///
    /// <para><b>The reverse of <see cref="GetFundDisclosureAsync"/>, and the argument need not be a fund</b> —
    /// measured 2026-08-30, <c>AAPL</c> answered 3,209 rows.</para>
    ///
    /// <para><b>"Latest" is per holder, not per response.</b> One response mixes reporting dates by years:
    /// SPY's 220 rows carried <b>19 distinct dates spanning 2019-09-30 to 2026-06-30</b>. Read
    /// <see cref="FundHolder.DateReported"/> per row before summing or ranking anything — see
    /// <see cref="FundHolder"/>, where the distribution is recorded.</para></summary>
    /// <param name="symbol">The held security. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every reported holder, in FMP's own order. <b>No ordering was found</b> in the responses
    /// measured 2026-08-30, and the rows are <b>not a single as-of snapshot</b>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundHolder>> GetFundHoldersAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-holders-latest").With("symbol", symbol),
            FmpJsonContext.Default.ListFundHolder, ct);
    }

    /// <summary>SEC-registered fund share classes whose registrant name matches a word, from
    /// <c>stable/funds/disclosure-holders-search</c>.
    ///
    /// <para><b>Named for what it returns, not for the path.</b> The rows are not holders: they are
    /// registrant, series and class identifiers plus a filer address — see
    /// <see cref="FundShareClass"/>.</para>
    ///
    /// <para><b>Matching is case-insensitive, whole-word and single-word.</b> Measured 2026-08-30:
    /// <c>Vanguard</c>, <c>vanguard</c> and <c>VANGUARD</c> each returned the same 548 rows; <c>Vangua</c>
    /// returned <b>0</b>; <c>Fid</c> and <c>fidelit</c> returned 0; and <c>Vanguard Group</c> — a two-word
    /// company name, the most likely thing a caller types — returned <b>0</b>. Pass one whole word:
    /// <c>"Vanguard"</c>, <c>"Fidelity"</c>, <c>"Schwab"</c>. The exact tokenisation was not established and
    /// this SDK does not assert one.</para>
    ///
    /// <para><b>This is the largest response in the group and it cannot be narrowed.</b> Measured 2026-08-30,
    /// <c>name=Trust</c> returned <b>66,065 rows and 27.4 MB</b>, and <c>limit</c> and <c>page</c> changed it
    /// by not one byte. A common word is a very expensive query.</para></summary>
    /// <param name="name">One whole word from the registrant's name. Case does not matter; a prefix and a
    /// two-word phrase both match nothing.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every matching share class, in FMP's own order. A word that matches nothing answers an empty
    /// list at HTTP 200. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundShareClass>> SearchFundsByNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-holders-search").With("name", name),
            FmpJsonContext.Default.ListFundShareClass, ct);
    }
```

and append this guard after `ThrowIfNotOneSymbol`:

```csharp
    /// <summary>Rejects a quarter FMP would answer with an empty list rather than an error.
    ///
    /// <para>Measured 2026-08-30, <c>quarter=0</c> and <c>quarter=5</c> both return HTTP 200 with <c>[]</c>,
    /// while <c>quarter=Q1</c> is a 400 — so a caller who sends 0 is told "no holdings", not "bad request".
    /// The range is the calendar's and not a measured cap: there is no fifth quarter to measure. This follows
    /// <see cref="InstitutionalOwnershipEndpoints"/>, which guards the same argument for the same
    /// reason.</para>
    ///
    /// <para>The parameter is named <c>quarter</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentException.ParamName"/>.</para></summary>
    private static void ThrowIfQuarterOutOfRange(int quarter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quarter, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quarter, 4);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~EtfAndFundsTests`
Expected: PASS.

- [ ] **Step 5: Promote every deferred cref**

Every type in this slice now exists, so each `<c>…</c>` placeholder introduced in Tasks 1-6 becomes a real
`<see cref>`. **This is the whole list** — fifteen edits across eight files. A model file lives in
`FmpDotNet.Models` and reaches the facade through the parent namespace, so those crefs need the
`Endpoints.` prefix; the precedent is `EmployeeCount.cs:14`.

| file | from | to |
|---|---|---|
| `Models/EtfSectorWeighting.cs` | `<c>EtfInfo</c>` (×2) | `<see cref="EtfInfo"/>` |
| `Models/EtfSectorWeighting.cs` | `<c>EtfInfoSector</c>` | `<see cref="EtfInfoSector"/>` |
| `Models/EtfSectorWeighting.cs` | `<c>EtfAssetExposure.MarketValue</c>` | `<see cref="EtfAssetExposure.MarketValue"/>` |
| `Models/EtfHolding.cs` | `<c>EtfInfo.HoldingsCount</c>` | `<see cref="EtfInfo.HoldingsCount"/>` |
| `Models/EtfHolding.cs` | `<c>FundDisclosure.AcceptedDate</c>` | `<see cref="FundDisclosure.AcceptedDate"/>` |
| `Models/EtfHolding.cs` | `<c>FundDisclosure.Date</c>` | `<see cref="FundDisclosure.Date"/>` |
| `Models/EtfInfo.cs` | `<c>EtfAndFundsEndpoints.GetEtfHoldingsAsync</c>` | `<see cref="Endpoints.EtfAndFundsEndpoints.GetEtfHoldingsAsync"/>` |
| `Models/EtfInfo.cs` | `<c>EtfAndFundsEndpoints.GetEtfSectorWeightingsAsync</c>` | `<see cref="Endpoints.EtfAndFundsEndpoints.GetEtfSectorWeightingsAsync"/>` |
| `Models/FundDisclosure.cs` | `<c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>` | `<see cref="Endpoints.EtfAndFundsEndpoints.GetFundDisclosureAsync"/>` |
| `Models/FundDisclosure.cs` | `<c>FundShareClass.EntityOrgType</c>` | `<see cref="FundShareClass.EntityOrgType"/>` |
| `Models/FundDisclosureDate.cs` | `<c>EtfAndFundsEndpoints.GetFundDisclosureAsync</c>` (×3) | `<see cref="Endpoints.EtfAndFundsEndpoints.GetFundDisclosureAsync"/>` |
| `Models/FundShareClass.cs` | `<c>EtfAndFundsEndpoints.SearchFundsByNameAsync</c>` | `<see cref="Endpoints.EtfAndFundsEndpoints.SearchFundsByNameAsync"/>` |
| `Endpoints/EtfAndFundsEndpoints.cs` | `<c>SearchFundsByNameAsync</c>` | `<see cref="SearchFundsByNameAsync"/>` |
| `Endpoints/EtfAndFundsEndpoints.cs` | `<c>GetFundHoldersAsync</c>` | `<see cref="GetFundHoldersAsync"/>` |
| `FmpClient.cs` | `<c>EtfAndFundsEndpoints.SearchFundsByNameAsync</c>` | `<see cref="EtfAndFundsEndpoints.SearchFundsByNameAsync"/>` |

Then confirm none was missed:

```bash
grep -rn '<c>Etf\|<c>Fund\|<c>EtfAndFundsEndpoints\|<c>GetFund\|<c>SearchFunds' \
     src/FmpDotNet/Models/Etf*.cs src/FmpDotNet/Models/Fund*.cs \
     src/FmpDotNet/Endpoints/EtfAndFundsEndpoints.cs src/FmpDotNet/FmpClient.cs
```

Expected: no output. A surviving `<c>` placeholder is not a build error — it is a doc that quietly stopped
being checked, which is exactly what this step exists to prevent.

- [ ] **Step 6: Build and confirm the crefs resolve**

Run: `dotnet build`
Expected: **build succeeds with no warnings.** `TreatWarningsAsErrors=true` means a cref pointing at a type
that does not exist is CS1574 and fails here — so a green build is the promotion's proof.

- [ ] **Step 7: Run the whole suite and confirm the ONE expected failure**

Run: `dotnet test`
Expected: **exactly one failure**, still
`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` — now
nine paths short rather than five. Anything else is a real failure.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet tests/FmpDotNet.Tests/EtfAndFundsTests.cs
git commit -m "feat: add the four fund methods, and promote the deferred crefs (#34)"
```

---

### Task 8: Teach the live sweep to ask the nine paths something worth answering

**This is not optional and it is not cosmetic.** `Probe.Argument` supplies `symbol` as `LiveApi.Symbol`, which
is `AAPL` — and measured 2026-08-30, AAPL returns `[]` on all four ETF-only paths and on
`funds/disclosure-dates`. Without a new arm the live sweep records `outcome empty` as the baseline for **five
of nine** endpoints and agrees with itself green for ever. That is precisely the failure `LiveApi.Exchange`,
`LiveApi.Industry` and `LiveApi.FilerCik` each exist to prevent, arriving through the same door.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` (two constants)
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` (two `Argument` arms)
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` (one pinning test, and the class doc's counts)

**Interfaces:**
- Consumes: `EtfAndFundsEndpoints` and all nine of its methods (Tasks 6-7).
- Produces: `LiveApi.EtfSymbol` (`"QQQ"`) and `LiveApi.FundNameQuery` (`"Schwab"`), and the two dispatch arms
  that route `EtfAndFundsEndpoints`' `symbol` and `name` parameters to them. Task 9's baseline run depends on
  all of it.

**No new arm is needed for `year`, `quarter` or `ct`.** `Probe.Argument`'s `int` switch already maps
`"year" => LiveApi.SettledYear` (2025) and `"quarter" => LiveApi.SettledQuarter` (3), and measured 2026-08-30
`funds/disclosure?symbol=QQQ&year=2025&quarter=3` answered **101 rows**. Adding one would be duplication.

- [ ] **Step 1: Write the failing pinning test**

Add to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`, after
`The_sweep_asks_the_thirteen_f_paths_for_a_filer_cik_rather_than_an_issuer_cik`:

```csharp
    [Fact]
    public void The_sweep_asks_the_etf_and_fund_paths_for_a_fund_rather_than_for_apple()
    {
        // The synthesiser produces a well-formed symbol for every one of these, so the generic argument check
        // above passes either way — this is the check that the symbol means the right thing. Measured
        // 2026-08-30, LiveApi.Symbol (AAPL) answers ZERO rows on all four ETF-only paths AND on
        // funds/disclosure-dates: five of the nine endpoints would record `outcome empty` as their baseline
        // and match it every week after.
        //
        // QQQ was chosen by measurement rather than by taste: of the ETFs probed it is the smallest that
        // answers non-empty on all eight symbol paths — 30 / 8 / 107 / 1 / 11 / 28 / 87 rows across the seven
        // symbol-only paths, plus 101 rows for funds/disclosure at SettledYear/SettledQuarter (2025 Q3) —
        // for roughly 124 KB in total, against SPY's ~500 KB.
        var symbolKeyed = Probe.EndpointMethods(typeof(Endpoints.EtfAndFundsEndpoints))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.Name == "symbol")
            .ToList();

        // Eight of the nine methods take a symbol; SearchFundsByNameAsync takes a name. If that number
        // changes this test should be revisited rather than adjusted.
        Assert.Equal(8, symbolKeyed.Count);
        Assert.All(symbolKeyed, p => Assert.Equal(LiveApi.EtfSymbol, Probe.Argument(p)));
        Assert.NotEqual(LiveApi.Symbol, LiveApi.EtfSymbol);

        // And the ninth gets a fund-company word, not a ticker and not the M&A acquirer name. Measured
        // 2026-08-30, `name` on this path is a whole-word match against the REGISTRANT name: "Schwab"
        // answered 211 rows, while a prefix and a two-word phrase both answer zero.
        var nameKeyed = Probe.EndpointMethods(typeof(Endpoints.EtfAndFundsEndpoints))
            .SelectMany(m => m.GetParameters())
            .Single(p => p.Name == "name");

        Assert.Equal(LiveApi.FundNameQuery, Probe.Argument(nameKeyed));
        Assert.NotEqual(LiveApi.AcquirerNameQuery, LiveApi.FundNameQuery);

        // The AAPL default survives everywhere else: the quote path still gets a ticker.
        var quoteSymbol = Probe.EndpointMethods(typeof(Endpoints.QuoteEndpoints))
            .SelectMany(m => m.GetParameters())
            .First(p => p.Name == "symbol");
        Assert.Equal(LiveApi.Symbol, Probe.Argument(quoteSymbol));
    }
```

Then update the class's own doc, which counts its checks. Change **"All thirteen checks below"** to **"All
fourteen checks below"**, and **"The remaining ten pin the literal argument"** to **"The remaining eleven pin
the literal argument"**. Extend that sentence's list of examples with:

```
a ticker where five of nine ETF and mutual-fund paths want a fund and a sixth wants a fund company's name
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: **build failure** — `LiveApi.EtfSymbol` and `LiveApi.FundNameQuery` do not exist (CS0117).

This suite is keyless by design (see the class doc), so it runs with no `FMP_API_KEY` set. **Do not set one.**

- [ ] **Step 3: Add the two `LiveApi` constants**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, add after `SecondSymbol`:

```csharp
    /// <summary>The fund every ETF and mutual-fund probe uses.
    ///
    /// <para><b>Named rather than falling out of the default string case, because the default is silently
    /// wrong here in the worst way.</b> <c>Probe.Argument</c> maps any unrecognised string to
    /// <see cref="Symbol"/>, and measured 2026-08-30 <c>AAPL</c> answers an <b>empty array at HTTP 200</b> on
    /// all four ETF-only paths and on <c>funds/disclosure-dates</c> — five of the nine endpoints in that group
    /// would record <c>outcome empty</c> as their healthy baseline and agree with themselves for ever. This is
    /// the same failure <see cref="Exchange"/> and <see cref="Industry"/> exist to prevent.</para>
    ///
    /// <para><b>QQQ was chosen by measurement, not by taste.</b> Of the ETFs probed 2026-08-30 it is the
    /// smallest that answers non-empty on <b>all eight</b> symbol paths: 30 rows on
    /// <c>etf/asset-exposure</c>, 8 on <c>etf/country-weightings</c>, 107 on <c>etf/holdings</c>, 1 on
    /// <c>etf/info</c>, 11 on <c>etf/sector-weightings</c>, 28 on <c>funds/disclosure-dates</c>, 87 on
    /// <c>funds/disclosure-holders-latest</c>, and 101 on <c>funds/disclosure</c> at
    /// <see cref="SettledYear"/>/<see cref="SettledQuarter"/>. That is roughly <b>124 KB</b> across the eight,
    /// against SPY's ~500 KB — and none of these paths can be narrowed, so payload size is the whole
    /// cost.</para></summary>
    public const string EtfSymbol = "QQQ";
```

and after `CompanyNameQuery`:

```csharp
    /// <summary>The word the fund share-class search is probed with.
    ///
    /// <para>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>: <c>funds/disclosure-holders-search</c> matches a <b>registrant's</b> name, so
    /// <c>name=Apple</c> — which is what <see cref="AcquirerNameQuery"/> would supply — answers an empty array
    /// with HTTP 200.</para>
    ///
    /// <para><b>One whole word, because that is all this path matches.</b> Measured 2026-08-30 the match is
    /// case-insensitive, whole-word and single-word: <c>Vanguard</c> answered 548 rows while <c>Vangua</c> and
    /// <c>Vanguard Group</c> each answered <b>0</b>. <c>"Schwab"</c> answered <b>211 rows, 90 KB</b> — chosen
    /// over <c>Vanguard</c> and <c>Fidelity</c> (2,379 rows, 1.0 MB) because the sweep measures shape rather
    /// than depth, and over <c>Trust</c>, which answers 66,065 rows and 27.4 MB and cannot be
    /// narrowed.</para>
    ///
    /// <para>Its own constant rather than reusing <see cref="CompanyNameQuery"/> or
    /// <see cref="AcquirerNameQuery"/>, for the reason those two are separate from each other: a change to one
    /// probe must not silently move another.</para></summary>
    public const string FundNameQuery = "Schwab";
```

- [ ] **Step 4: Add the two `Probe.Argument` arms**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, inside the `type == typeof(string)` switch.

Add beside the other declaring-type-dispatched `name` arms, **before** the generic
`"name" => LiveApi.AcquirerNameQuery`:

```csharp
                // funds/disclosure-holders-search matches a fund REGISTRANT's name — "Vanguard", "Schwab" —
                // not a company being acquired. Its own constant for the reason the InsiderTrades and
                // Congress arms above have theirs.
                "name" when parameter.Member.DeclaringType == typeof(Endpoints.EtfAndFundsEndpoints)
                    => LiveApi.FundNameQuery,
```

and beside the COT `symbol` arm, **before** `_ => LiveApi.Symbol`:

```csharp
                // The ETF and mutual-fund paths want a FUND. Measured 2026-08-30, AAPL answers `[]` with HTTP
                // 200 on all four ETF-only paths and on funds/disclosure-dates — five of nine endpoints
                // recording `outcome empty` as their baseline, which is the silent green this file's other
                // named constants exist to stop. QQQ answers non-empty on all eight symbol paths.
                "symbol" when parameter.Member.DeclaringType == typeof(Endpoints.EtfAndFundsEndpoints)
                    => LiveApi.EtfSymbol,
```

- [ ] **Step 5: Run the sweep-coverage suite to verify it passes**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: PASS, 14 tests. Still no `FMP_API_KEY`.

- [ ] **Step 6: Run the whole suite and confirm the ONE expected failure**

Run: `dotnet test`
Expected: still exactly one failure, still the README coverage test. The live smoke tests skip without a key.

- [ ] **Step 7: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs
git commit -m "test: probe the ETF and fund paths with a fund, not with AAPL (#34)"
```

---

### Task 9: Regenerate the README and re-record the live baseline

Two generated artifacts and one block of hand-written prose the generator does not read. This is the task that
turns the suite green.

**Files:**
- Modify: `README.md`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

**Interfaces:** none — nothing downstream depends on this task.

- [ ] **Step 1: Regenerate the coverage table**

```bash
FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests
```

Then check the result rather than trusting it:

```bash
git diff --stat README.md
grep -n "of FMP.s 243 endpoint paths are modelled" README.md
```

Expected: the headline reads **207 of FMP's 243 endpoint paths are modelled**, and a new `` `fmp.EtfAndFunds` ``
section lists **nine paths against nine methods**. The generator orders groups ordinally by property name, so
that section lands between `` `fmp.Esg` `` and `` `fmp.InsiderTrades` ``. If the headline is not 207, an
endpoint is not being discovered — `EndpointCoverageTests.Every_public_endpoint_method_reaches_the_api` names
which one.

- [ ] **Step 2: Fix the prose the generator does not read**

The paragraph beginning "The rest is unbuilt rather than blocked" and the one beginning "That remainder is
tracked as five issues" both carry arithmetic that this slice changes. `EndpointCoverageTests` regenerates the
table above them but never reads them, so they rot silently.

Replace this paragraph:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **45 paths
remain**, of which **38 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are News (10) and Fundraisers & DCF (10);
ETF & Mutual Funds and Indexes & Market Hours carry 9 apiece.
```

with:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **36 paths
remain**, of which **29 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are News (10) and Fundraisers & DCF (10),
and Indexes & Market Hours carries 9.
```

and replace this one:

```markdown
That remainder is tracked as five issues under the epic, four of them actionable, each 7 to 10 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 198 modelled plus 45 remaining, with no path counted twice and none missing.
```

with:

```markdown
That remainder is tracked as four issues under the epic, three of them actionable, each 7 to 10 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 207 modelled plus 36 remaining, with no path counted twice and none missing.
```

Leave the two paragraphs after those unchanged — the one about the equity/asset-class imbalance and the one
about Commodity, Forex and Crypto are both still true.

- [ ] **Step 3: Verify the arithmetic against the issues rather than trusting it**

The four issues that remain under the epic once #34 closes are **#33** (News), **#38** (Indexes and Market
Hours), **#39** (Fundraisers and DCF) and **#41** (TipRanks):

```bash
for n in 33 38 39 41; do
  gh issue view $n --json body --jq .body | grep -coE 'stable/[a-z0-9-]'
done | paste -sd+ | bc
```

Expected: `36`. That is `243 - 207`, so the partition holds with no gap and no double count. If it prints
anything else, the prose is wrong — fix the prose, not this check.

- [ ] **Step 4: Run the unit suite green**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS, all of it, including `EndpointCoverageTests`. **This is the first point in the plan where the
whole unit suite is green** — the known single failure from Tasks 6-8 is now resolved.

- [ ] **Step 5: Re-record the live baseline**

The baseline is a measurement, not a specification — **never hand-edit it**. Record it in one run so its header
date is true of every line:

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY\s*=\s*\"?([^\"\s]+)\"?', open('.env').read(), re.M).group(1))") \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

Do **not** `source` the `.env` — it has clobbered `PATH` for a whole shell before; extract the one variable
into the one command, as above. Do **not** set `FMPDOTNET_SMOKE_BULK`: `baseline-bulk.txt` is untouched by this
slice, and re-recording it would spend the key's standing on twenty of FMP's most restricted endpoints for
nothing.

`ShapeAssertions.Updated` refuses to write a baseline from a run in which any endpoint errored, so a transport
fault or a throttled key fails loudly here instead of writing `outcome error` in as an endpoint's recorded
truth. If it refuses, wait and re-run rather than working around it.

**Expect this run to move more bytes than usual.** Nine new endpoints, and QQQ's eight symbol paths are about
124 KB together while `name=Schwab` is another 90 KB.

- [ ] **Step 6: Read the baseline diff before committing it**

```bash
git diff --stat tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
grep -c '^\[' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | grep '^[+-]\[' 
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | grep 'outcome'
```

Expected, and each item is a thing to check rather than assume:

1. The entry count goes from **178 to 187** — nine new `[EtfAndFunds.*]` blocks, one per method.
2. Every one of the nine reads `outcome rows`. **Not one may read `empty`.** An empty on any of the eight
   symbol paths means the `EtfSymbol` arm from Task 8 is not being reached; an empty on
   `SearchFundsByNameAsync` means the `FundNameQuery` arm is not. `error` fails the write outright.
3. The header date is today's.
4. Nothing else changed. Any `now always null, was populated` line on an endpoint this slice did not touch is a
   real finding — stop and investigate rather than committing it.

Two properties are expected to record as `null` inside the new blocks and are **correct**, not defects — check
them against this list rather than treating a `null` as automatically wrong:

- `EtfHolding.Asset`, `Isin` and `SecurityCusip` record `set` for QQQ, whose holdings are listed equities.
  (They would record `null` for a bond fund, which is why the sentinel converter exists.)
- `FundShareClass.Symbol`, `City`, `State`, `ZipCode`, `EntityOrgType`, `ReportingFileNumber` and `Address`
  record `set` if any of Schwab's 211 rows carries a real value — measured 2026-08-30 they did.

If any of those records `null` across the whole sweep, that is a converter mapping a live value to
`null` that it should be passing through — investigate before committing.

- [ ] **Step 7: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "docs: regenerate the coverage table at 207 of 243, and re-record the live baseline (#34)"
```

- [ ] **Step 8: Final whole-suite run**

```bash
dotnet build && dotnet test
```

Expected: build with **no warnings** (`TreatWarningsAsErrors=true` makes any doc-comment defect a failure), and
the whole suite green. The live smoke tests skip without a key, which is the intended state for a normal run.

---

## Self-review notes

Checked while writing, recorded so a reviewer does not repeat the work:

- **Spec coverage.** Every section of the design spec maps to a task: the nine methods → Tasks 6-7; the ten
  records → Tasks 1-5; the four converters → Tasks 1, 2, 3, 4; the two timestamps → Tasks 2 and 4; the
  numeric-string fields → Tasks 4 and 5; the guards → Tasks 6 and 7; "what is documented rather than guarded"
  → the XML docs in Tasks 2-7 and the tests that pin them; serialisation and wiring → Tasks 1-6; the smoke
  sweep → Task 8; the README → Task 9.
- **One spec ambiguity was found and ruled on**, not left for the implementer: `FundDisclosure.FairValLevel`.
  See "Ruling carried into this plan" above.
- **One spec figure was found wrong and corrected at source** before this plan was written: the
  `funds/disclosure-holders-latest` date spread, which both committed docs recorded as four dates in one
  quarter and which is 19 dates over seven years. Corrected in commit `18fec44`; this plan uses the corrected
  figures throughout.
- **One spec fixture name was changed**, with the reason stated in Task 5: the holders fixture is not a head
  and is not named `.head.json`.
- **One file is modified beyond the spec's list of nine**: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`.
  The spec names the two `Probe.Argument` arms but not the test that pins them. Every other named constant in
  `LiveApi` has such a test — `Exchange`, `Industry`, `FilerCik` and the rest — and without one the two new
  arms could be deleted and the sweep would go quietly back to probing five of nine endpoints with AAPL, which
  is the exact failure they exist to prevent. Ten modified files, not nine.
- **Type consistency.** `SentinelStringJsonConverter` is created in Task 2 and used in Tasks 2, 4 and 5 under
  that exact name. `ThrowIfNotOneSymbol` is created in Task 6 and reused in Task 7. `EtfInfoSector.Sector`
  binds `industry` in both Task 3's test and Task 3's record. Every `FmpJsonContext.Default.ListX` name used
  in a test is registered in the same task that uses it.
- **`EndpointCoverageTests` was read, not assumed**: its `Argument` supplies `"AAPL"` for `symbol` and `name`
  (no comma), `2025` for `year` and `3` for `quarter`, so all nine methods drive cleanly with no new arm.
- **Fixture provenance.** All eleven fixtures are verbatim rows from the 2026-08-30 capture set. Two are
  assemblies of rows from more than one response — `funds-disclosure.dst-pair.json` and
  `funds-disclosure-holders-search.nulls.json` — and both say so where they are defined. **No row is
  constructed.** The inline JSON literals in tests are likewise measured rows, each with its source named in
  the comment above it.
