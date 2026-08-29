# Senate and House Trading Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cover issue #31's twelve congressional-disclosure paths behind a new `fmp.Congress` facade.

**Architecture:** One new facade, `CongressEndpoints`, with twelve methods over five records plus two nested
ones. Eight of the twelve paths return the same trade row. One new converter exists solely because
`incomeRange` arrives as the empty string on 14 of 250 measured rows and would otherwise cost the caller the
whole response.

**Tech Stack:** .NET 10, System.Text.Json source generation, NodaTime, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-senate-and-house-trading-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-29-senate-and-house-trading-measurements.md`

## Global Constraints

- **Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as
  `[JsonSerializable(typeof(List<X>))]` or it fails at runtime, not at compile time.** Five entries are added
  across this plan: `CongressionalTrade`, `CongressMemberPosition`, `CongressMemberProfile`,
  `SenateNetWorthLine`, `SenateNetWorthSummary`. The two nested records (`NetWorthRange`,
  `NetWorthDebtDetails`) are reached through `SenateNetWorthLine` and need no entry of their own.
- **`TreatWarningsAsErrors` is on and covers XML-doc warnings.** Every public member needs a doc comment, and
  every `<see cref="..."/>` must resolve or the build fails.
- **No reflection in `src/`.** `IsAotCompatible` is declared; `IL2026` and `IL3050` are build errors.
- **NodaTime only in public signatures** — no `DateTime`, `DateOnly`, `DateTimeOffset`, `TimeSpan`.
- **Every quantity off the wire is `decimal?`.** The only `int?` properties in this slice are
  `CongressMemberPosition.CongressNumber`, `SenateNetWorthLine.Year` and `SenateNetWorthSummary.Year` — whole
  by their own nature, which is the test `CONTRIBUTING.md` states. `yearsInTerm`, `yearsActive`, `value`,
  `income`, `min`, `max` and all fourteen aggregate money fields are `decimal?`.
- **No enums.** `type`, `assetType`, `owner`, `party`, `position`, `section`, `formType` are all `string?`.
- **Empty strings are preserved, never normalised to null.**
- **`senateId` is required on every method that takes it** — `ArgumentException.ThrowIfNullOrWhiteSpace`.
  The endpoints answer without it and return the wrong member's data; the SDK must not reproduce that.
- **Tests may index into fixtures freely; the live sweep may not.** Row order is not stable between calls.

## File Structure

**Create:**
- `src/FmpDotNet/Models/CongressionalTrade.cs` — the 16-property row shared by eight paths
- `src/FmpDotNet/Models/CongressMember.cs` — `CongressMemberPosition` (8) and `CongressMemberProfile` (10)
- `src/FmpDotNet/Models/SenateNetWorth.cs` — `SenateNetWorthLine` (17), `SenateNetWorthSummary` (16),
  `NetWorthRange` (2), `NetWorthDebtDetails` (4)
- `src/FmpDotNet/Endpoints/CongressEndpoints.cs` — the facade, 12 methods
- `tests/FmpDotNet.Tests/CongressTests.cs`
- six fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modify:**
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — five entries
- `src/FmpDotNet/Serialization/NodaConverters.cs` — `NetWorthRangeJsonConverter`
- `src/FmpDotNet/FmpClient.cs` — constructor parameter and `Congress` property
- `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — one registration
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — `SenateId`, `CongressNameQuery`
- `tests/FmpDotNet.SmokeTests/Probe.cs` — two dispatch arms
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded
- `README.md` — a `fmp.Congress` coverage block

---

### Task 1: `CongressionalTrade`

**Files:**
- Create: `src/FmpDotNet/Models/CongressionalTrade.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-house-latest.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-latest.json`
- Create: `tests/FmpDotNet.Tests/CongressTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public sealed record CongressionalTrade` with the sixteen properties below, and
  `FmpJsonContext.Default.ListCongressionalTrade`.

- [ ] **Step 1: Write the fixtures**

`tests/FmpDotNet.Tests/Fixtures/congress-house-latest.json` — captured 2026-08-29 from
`stable/house-latest`, three rows chosen to carry a blank `owner`, a blank `district` and a **null**
`senateID`:

```json
[
  {
    "symbol": "STE",
    "senateID": "M001217",
    "disclosureDate": "2026-08-28",
    "transactionDate": "2026-07-13",
    "firstName": "Jared",
    "lastName": "Moskowitz",
    "office": "Jared Moskowitz",
    "district": "FL23",
    "owner": "",
    "assetDescription": "Steris PLC",
    "assetType": "Stock",
    "type": "Sale",
    "amount": "$1,001 - $15,000",
    "capitalGainsOver200USD": "False",
    "comment": "",
    "link": "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2026/20035243.pdf"
  },
  {
    "symbol": "GOOGL",
    "senateID": null,
    "disclosureDate": "2026-08-26",
    "transactionDate": "2026-08-24",
    "firstName": "Michael",
    "lastName": "Rulli",
    "office": "Michael Rulli",
    "district": "",
    "owner": "",
    "assetDescription": "Alphabet Inc",
    "assetType": "Stock",
    "type": "Sale",
    "amount": "$50,001 - $100,000",
    "capitalGainsOver200USD": "False",
    "comment": "",
    "link": "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2026/20035309.pdf"
  },
  {
    "symbol": "SOLS",
    "senateID": "M001234",
    "disclosureDate": "2026-08-26",
    "transactionDate": "2026-08-11",
    "firstName": "Kelly Louise",
    "lastName": "Morrison",
    "office": "Kelly Louise Morrison",
    "district": "MN03",
    "owner": "Spouse",
    "assetDescription": "SOLSTICE ADVANCED MTRILS INC",
    "assetType": "Stock",
    "type": "Sale",
    "amount": "$1,001 - $15,000",
    "capitalGainsOver200USD": "False",
    "comment": "",
    "link": "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2026/20035244.pdf"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/congress-senate-latest.json` — captured 2026-08-29 from
`stable/senate-latest`. **These rows have fifteen keys, not sixteen**: this is the only trade feed that omits
`capitalGainsOver200USD`, and the fixture must keep that omission.

```json
[
  {
    "symbol": "GS",
    "senateID": "M001243",
    "disclosureDate": "2026-08-27",
    "transactionDate": "2026-08-12",
    "firstName": "Dave",
    "lastName": "McCormick",
    "office": "Dave McCormick",
    "district": "PA",
    "owner": "Spouse",
    "assetDescription": "The Goldman Sachs Group Inc (1)",
    "assetType": "Corporate Bond",
    "type": "Purchase",
    "amount": "$100,001 - $250,000",
    "comment": "",
    "link": "https://efdsearch.senate.gov/search/view/ptr/257795ae-e1b2-411d-b562-8fe4c2a4f2a1/"
  },
  {
    "symbol": "GS",
    "senateID": "M001243",
    "disclosureDate": "2026-08-27",
    "transactionDate": "2026-08-05",
    "firstName": "Dave",
    "lastName": "McCormick",
    "office": "Dave McCormick",
    "district": "PA",
    "owner": "Spouse",
    "assetDescription": "The Goldman Sachs Group Inc (1)",
    "assetType": "Corporate Bond",
    "type": "Purchase",
    "amount": "$100,001 - $250,000",
    "comment": "",
    "link": "https://efdsearch.senate.gov/search/view/ptr/257795ae-e1b2-411d-b562-8fe4c2a4f2a1/"
  }
]
```

Both files need `<None Update="Fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />` coverage — check
`tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj` for the existing glob before adding anything; the other
fixtures are already picked up by one.

- [ ] **Step 2: Write the failing tests**

Add to `tests/FmpDotNet.Tests/CongressTests.cs`:

```csharp
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The congressional-disclosure records and the facade that serves them, checked against captures
/// taken live 2026-08-29.</summary>
public class CongressTests
{
    private static (CongressEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CongressEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_captured_house_trade_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-house-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal(3, rows.Count);

        // Row 2 (Morrison) is the fullest: `comment` is the only field FMP left blank on it. Asserting the
        // exact unbound set rather than Assert.Empty is deliberate — `Binding.Unbound` counts a blank string
        // as unbound, and `comment` was empty on 100 of 100 rows measured, so Assert.Empty could never pass.
        Assert.Equal(["Comment"], Binding.Unbound(rows[2]));

        Assert.Equal("SOLS", rows[2].Symbol);
        Assert.Equal("M001234", rows[2].SenateId);
        Assert.Equal(new LocalDate(2026, 8, 26), rows[2].DisclosureDate);
        Assert.Equal(new LocalDate(2026, 8, 11), rows[2].TransactionDate);
        Assert.Equal("Kelly Louise", rows[2].FirstName);
        Assert.Equal("Morrison", rows[2].LastName);
        Assert.Equal("Kelly Louise Morrison", rows[2].Office);
        Assert.Equal("MN03", rows[2].District);
        Assert.Equal("Spouse", rows[2].Owner);
        Assert.Equal("SOLSTICE ADVANCED MTRILS INC", rows[2].AssetDescription);
        Assert.Equal("Stock", rows[2].AssetType);
        Assert.Equal("Sale", rows[2].Type);
        Assert.Equal("$1,001 - $15,000", rows[2].Amount);
        Assert.Equal("False", rows[2].CapitalGainsOver200Usd);
        Assert.Equal("", rows[2].Comment);
        Assert.StartsWith("https://disclosures-clerk.house.gov/", rows[2].Link);
    }

    [Fact]
    public void An_empty_string_is_kept_as_an_empty_string_and_a_null_is_kept_as_null()
    {
        // Both forms occur in this one record and mean different things: measured 2026-08-29, `owner` was ""
        // on 54 of 100 House rows while `senateID` was JSON null on 2. Collapsing either into the other
        // destroys a distinction FMP makes.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-house-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal("", rows[0].Owner);
        Assert.Null(rows[1].SenateId);
        Assert.Equal("", rows[1].District);
    }

    [Fact]
    public void Capital_gains_binds_from_the_string_False_that_FMP_actually_sends()
    {
        // Measured 2026-08-29: the field is the JSON string "False", and `bool?` THROWS on it — the context's
        // AllowReadingFromString covers numbers, not booleans. Only "False" was ever observed, so the
        // affirmative spelling is unknown and no converter can be written for it honestly.
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","capitalGainsOver200USD":"False"}]""",
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal("False", rows[0].CapitalGainsOver200Usd);
    }

    [Fact]
    public void The_senate_feed_binds_with_capital_gains_absent_and_the_other_fifteen_populated()
    {
        // senate-latest is the ONE trade feed that omits capitalGainsOver200USD — 0 of its 100 rows carry it,
        // against 100% on the other seven. One nullable property covers all eight paths.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].CapitalGainsOver200Usd);
        Assert.Equal(["CapitalGainsOver200Usd", "Comment"], Binding.Unbound(rows[0]).Order());
        Assert.Equal("GS", rows[0].Symbol);
        Assert.Equal("Corporate Bond", rows[0].AssetType);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: FAIL — `CongressionalTrade` does not exist, so this will not compile.

- [ ] **Step 4: Write the record**

`src/FmpDotNet/Models/CongressionalTrade.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One disclosed trade by a member of Congress or their immediate family, from any of the eight
/// congressional trade paths.
///
/// <para><b>One record for eight paths.</b> <c>house-latest</c>, <c>house-trades</c>,
/// <c>house-trades-by-id</c>, <c>house-trades-by-name</c> and their four Senate counterparts all answer these
/// keys. Measured 2026-08-29, seven of the eight carry all sixteen; see
/// <see cref="CapitalGainsOver200Usd"/> for the one that does not.</para>
///
/// <para><b>Nothing here is an enum.</b> <see cref="Type"/>, <see cref="AssetType"/> and <see cref="Owner"/>
/// read like closed vocabularies and are not: measured 2026-08-29, the House and Senate feeds already
/// disagree, with <c>Cryptocurrency</c> appearing only on the House side and <c>Mutual Fund</c> only on the
/// Senate side. The union of seven <see cref="AssetType"/> values is a floor, not a vocabulary, and a closed
/// C# enum over an open server-side list is a breaking change waiting for a Tuesday.</para>
///
/// <para><b>Empty strings are kept as empty strings.</b> <see cref="Comment"/> was blank on every one of the
/// 200 rows measured across both latest feeds, and <see cref="SenateId"/> is the only field here that arrives
/// as a JSON <see langword="null"/>. Both forms occur and they mean different things.</para></summary>
public sealed record CongressionalTrade
{
    /// <summary>The ticker traded, as FMP spells it. Blank on 3 of 100 House rows measured
    /// 2026-08-29 — a disclosed asset with no ticker, not a missing value.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The member's Bioguide identifier — <c>M001217</c>, <c>P000197</c>.
    ///
    /// <para><b>Named <c>senateID</c> on the wire even for Representatives</b>, which is FMP's naming rather
    /// than a fault in the capture. This is the value
    /// <see cref="Endpoints.CongressEndpoints.GetHouseTradesByMemberAsync"/> filters on.</para>
    ///
    /// <para>The only field on this record measured to arrive as JSON <see langword="null"/> — 2 of 100 House
    /// rows on 2026-08-29.</para></summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>The date the disclosure was filed. Always later than <see cref="TransactionDate"/>.</summary>
    [JsonPropertyName("disclosureDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DisclosureDate { get; init; }

    /// <summary>The date the trade was executed.</summary>
    [JsonPropertyName("transactionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? TransactionDate { get; init; }

    /// <summary>The member's given name.</summary>
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }

    /// <summary>The member's surname. This is what
    /// <see cref="Endpoints.CongressEndpoints.GetHouseTradesByNameAsync"/> matches on.</summary>
    [JsonPropertyName("lastName")] public string? LastName { get; init; }

    /// <summary>The member's full name as the disclosure spells it.</summary>
    [JsonPropertyName("office")] public string? Office { get; init; }

    /// <summary>The district for a Representative (<c>FL23</c>) or the state for a Senator (<c>PA</c>). Blank
    /// on 28 of 100 House rows measured 2026-08-29.</summary>
    [JsonPropertyName("district")] public string? District { get; init; }

    /// <summary>Who holds the position — <c>Self</c>, <c>Spouse</c>, <c>Joint</c>, or blank. Blank on 54 of
    /// 100 House rows and 2 of 100 Senate rows measured 2026-08-29, and kept blank rather than
    /// nulled.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }

    /// <summary>The asset as the disclosure describes it, which is prose rather than a normalised
    /// name.</summary>
    [JsonPropertyName("assetDescription")] public string? AssetDescription { get; init; }

    /// <summary>What kind of asset. Seven values measured 2026-08-29 across both feeds — <c>Stock</c>,
    /// <c>Stock Option</c>, <c>ETF</c>, <c>REIT</c>, <c>Corporate Bond</c>, <c>Mutual Fund</c>,
    /// <c>Cryptocurrency</c> — and the two feeds do not agree on that list, so it is a floor rather than a
    /// vocabulary. See the record summary.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>The transaction — <c>Purchase</c>, <c>Sale</c> or <c>Exchange</c> on every row measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>The disclosed value, as a bracketed band rather than a figure — <c>$1,001 - $15,000</c>
    /// through <c>$1,000,001 - $5,000,000</c>, seven distinct values measured 2026-08-29.
    ///
    /// <para><b>A string, and deliberately not parsed.</b> Congressional disclosure reports a range, so there
    /// is no exact amount to model and none is invented. FMP publishes structured bounds only on the net-worth
    /// path — see <see cref="NetWorthRange"/>.</para></summary>
    [JsonPropertyName("amount")] public string? Amount { get; init; }

    /// <summary>Whether the sale realised more than $200 in capital gains.
    ///
    /// <para><b>A string, not a <see cref="bool"/>, and both halves of that are measured.</b> It arrives as
    /// the JSON string <c>"False"</c>, and measured 2026-08-29 against this library's own
    /// <c>FmpJsonContext</c> options a <c>bool?</c> property <b>throws</b> on it — the context's
    /// <c>NumberHandling = AllowReadingFromString</c> rescues numbers, not booleans. Only <c>"False"</c> was
    /// ever observed, so the spelling of the affirmative is unknown and a converter would be guessing at the
    /// one value it exists to handle.</para>
    ///
    /// <para><b>Always <see langword="null"/> from <c>senate-latest</c>.</b> That path is the only one of the
    /// eight that omits the key — 0 of its 100 rows carried it on 2026-08-29, against 100% on the other
    /// seven.</para></summary>
    [JsonPropertyName("capitalGainsOver200USD")] public string? CapitalGainsOver200Usd { get; init; }

    /// <summary>The filer's note. Blank on all 200 rows measured across both latest feeds on
    /// 2026-08-29.</summary>
    [JsonPropertyName("comment")] public string? Comment { get; init; }

    /// <summary>The disclosure document — a House clerk PDF or a Senate EFD record.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}
```

- [ ] **Step 5: Register it**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, beside the other `[JsonSerializable]` attributes:

```csharp
[JsonSerializable(typeof(List<CongressionalTrade>))]
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: PASS, 4 tests.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Models/CongressionalTrade.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/CongressTests.cs tests/FmpDotNet.Tests/Fixtures/congress-house-latest.json \
        tests/FmpDotNet.Tests/Fixtures/congress-senate-latest.json
git commit -m "feat: add CongressionalTrade, shared by eight congressional paths (#31)"
```

---

### Task 2: `CongressMemberPosition` and `CongressMemberProfile`

**Files:**
- Create: `src/FmpDotNet/Models/CongressMember.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-positions.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-profile.json`
- Modify: `tests/FmpDotNet.Tests/CongressTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `CongressMemberPosition`, `CongressMemberProfile`, and their two context entries.

- [ ] **Step 1: Write the fixtures**

`congress-senate-positions.json` — three distinct rows, carrying a whole `yearsInTerm`, a fractional one, and
a null `endDate`:

```json
[
  {
    "senateID": "Z000018",
    "congressNumber": 118,
    "startDate": "2023-01-02",
    "endDate": "2025-01-02",
    "party": "Republican",
    "position": "Representative",
    "state": "MT",
    "yearsInTerm": 2
  },
  {
    "senateID": "Z000018",
    "congressNumber": 119,
    "startDate": "2025-01-02",
    "endDate": null,
    "party": "Republican",
    "position": "Representative",
    "state": "MT",
    "yearsInTerm": 0.7
  },
  {
    "senateID": "Y000064",
    "congressNumber": 119,
    "startDate": "2025-01-02",
    "endDate": null,
    "party": "Republican",
    "position": "Senator",
    "state": "IN",
    "yearsInTerm": 0.7
  }
]
```

`congress-senate-profile.json` — a fractional `yearsActive` and one of the seven integral ones:

```json
[
  {
    "senateID": "L000397",
    "firstName": "Zoe",
    "lastName": "Lofgren",
    "birthDate": "1947-12-20",
    "latestParty": "Democrat",
    "latestState": "CA",
    "latestPosition": "Representative",
    "image": "https://images.financialmodelingprep.com/senate/L000397.jpg",
    "active": true,
    "yearsActive": 31.7
  },
  {
    "senateID": "B001306",
    "firstName": "Troy",
    "lastName": "Balderson",
    "birthDate": "1962-01-15",
    "latestParty": "Republican",
    "latestState": "OH",
    "latestPosition": "Representative",
    "image": "https://images.financialmodelingprep.com/senate/B001306.jpg",
    "active": true,
    "yearsActive": 8
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `CongressTests`:

```csharp
    [Fact]
    public void A_captured_position_binds_all_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-positions.json"),
            FmpJsonContext.Default.ListCongressMemberPosition)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Z000018", rows[0].SenateId);
        Assert.Equal(118, rows[0].CongressNumber);
        Assert.Equal(new LocalDate(2023, 1, 2), rows[0].StartDate);
        Assert.Equal(new LocalDate(2025, 1, 2), rows[0].EndDate);
        Assert.Equal("Republican", rows[0].Party);
        Assert.Equal("Representative", rows[0].Position);
        Assert.Equal("MT", rows[0].State);
        Assert.Equal(2m, rows[0].YearsInTerm);
    }

    [Fact]
    public void A_fractional_years_in_term_binds_and_does_not_cost_the_rows_around_it()
    {
        // THE trap of this record. Measured 2026-08-29, `yearsInTerm` is a bare integer on 266 of 300 rows
        // and carries a decimal point on 34 — so a smaller sample sees only integers and types it `int`.
        // Under `int?` row 1 does not merely bind wrong: it aborts the whole array and takes rows 0 and 2
        // with it, which is why they are here.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-positions.json"),
            FmpJsonContext.Default.ListCongressMemberPosition)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(2m, rows[0].YearsInTerm);
        Assert.Equal(0.7m, rows[1].YearsInTerm);
        Assert.Equal(0.7m, rows[2].YearsInTerm);
        Assert.Null(rows[1].EndDate);
    }

    [Fact]
    public void A_captured_profile_binds_all_ten_of_its_fields_including_a_fractional_tenure()
    {
        // `yearsActive` is the same trap from the other side: 493 of 500 rows carry a decimal point, so here
        // the integral value is the rare one. Both are asserted.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-profile.json"),
            FmpJsonContext.Default.ListCongressMemberProfile)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("L000397", rows[0].SenateId);
        Assert.Equal("Zoe", rows[0].FirstName);
        Assert.Equal("Lofgren", rows[0].LastName);
        Assert.Equal(new LocalDate(1947, 12, 20), rows[0].BirthDate);
        Assert.Equal("Democrat", rows[0].LatestParty);
        Assert.Equal("CA", rows[0].LatestState);
        Assert.Equal("Representative", rows[0].LatestPosition);
        Assert.True(rows[0].Active);
        Assert.Equal(31.7m, rows[0].YearsActive);
        Assert.Equal(8m, rows[1].YearsActive);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: FAIL — the two record types do not exist.

- [ ] **Step 4: Write the records**

`src/FmpDotNet/Models/CongressMember.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One term a member of Congress served, from
/// <c>stable/senate-positions</c>.
///
/// <para>One row per Congress the member sat in. The path serves the House as well as the Senate despite its
/// name — measured 2026-08-29, <c>Representative</c> and <c>Senator</c> both appear in
/// <see cref="Position"/>.</para>
///
/// <para><b>Paged 300 at a time, and <c>limit</c> is ignored.</b> Measured 2026-08-29, <c>limit=500</c>
/// answered 300; page 1 answered a further 300 with no overlap, so the universe is at least 600 and was not
/// enumerated.</para></summary>
public sealed record CongressMemberPosition
{
    /// <summary>The member's Bioguide identifier. FMP's spelling; see
    /// <see cref="CongressionalTrade.SenateId"/>.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Which Congress — 118, 119. A count of Congresses and whole by its own nature, hence
    /// <see cref="int"/> where the tenure beside it is <see cref="decimal"/>.</summary>
    [JsonPropertyName("congressNumber")] public int? CongressNumber { get; init; }

    /// <summary>The day the term began.</summary>
    [JsonPropertyName("startDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? StartDate { get; init; }

    /// <summary>The day the term ended, or <see langword="null"/> for a term still running — 22 of 300 rows
    /// measured 2026-08-29.</summary>
    [JsonPropertyName("endDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? EndDate { get; init; }

    /// <summary>The member's party for this term — <c>Democrat</c> or <c>Republican</c> on every row
    /// measured. A string rather than an enum; see <see cref="CongressionalTrade"/>.</summary>
    [JsonPropertyName("party")] public string? Party { get; init; }

    /// <summary>The seat — <c>Representative</c> or <c>Senator</c>.</summary>
    [JsonPropertyName("position")] public string? Position { get; init; }

    /// <summary>The two-letter state.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>Years served in this term so far.
    ///
    /// <para><b><see cref="decimal"/>, and the measurement is the reason.</b> Measured 2026-08-29 across 300
    /// rows, 266 values arrived as bare JSON integers and <b>34 carried a decimal point</b> — 0.7, 0.2. A
    /// smaller sample sees only the 266 and types this <see cref="int"/>, and <see cref="int"/> rejects
    /// <c>0.7</c> by throwing out of the entire 300-row response rather than the one field. See
    /// <c>CONTRIBUTING.md</c>'s typing rule, which this field is the reason for.</para></summary>
    [JsonPropertyName("yearsInTerm")] public decimal? YearsInTerm { get; init; }
}

/// <summary>One member of Congress, from <c>stable/senate-profile</c>.
///
/// <para><b>The one path in this group whose universe was enumerated to exhaustion:</b> measured 2026-08-29,
/// page 0 answered 500, page 1 answered 35 and page 2 answered none — <b>535 members</b>. <c>limit</c> is
/// ignored.</para>
///
/// <para>Serves the House as well as the Senate, like <see cref="CongressMemberPosition"/>. Measured
/// 2026-08-29 <see cref="LatestPosition"/> also carries <c>Vice President</c>.</para></summary>
public sealed record CongressMemberProfile
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Given name.</summary>
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }

    /// <summary>Surname.</summary>
    [JsonPropertyName("lastName")] public string? LastName { get; init; }

    /// <summary>Date of birth. Measured 2026-08-29 across 500 rows, these run from 1932-12-31 to
    /// 1997-01-16.</summary>
    [JsonPropertyName("birthDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? BirthDate { get; init; }

    /// <summary>Most recent party — <c>Democrat</c>, <c>Republican</c> or <c>Independent</c>.</summary>
    [JsonPropertyName("latestParty")] public string? LatestParty { get; init; }

    /// <summary>Most recent state.</summary>
    [JsonPropertyName("latestState")] public string? LatestState { get; init; }

    /// <summary>Most recent seat — <c>Representative</c>, <c>Senator</c> or <c>Vice President</c>.</summary>
    [JsonPropertyName("latestPosition")] public string? LatestPosition { get; init; }

    /// <summary>FMP's headshot URL.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>Whether the member currently serves.
    ///
    /// <para><b>A genuine JSON boolean</b>, unlike
    /// <see cref="CongressionalTrade.CapitalGainsOver200Usd"/> which is the string <c>"False"</c>. The two are
    /// deliberately not modelled alike; see that property.</para></summary>
    [JsonPropertyName("active")] public bool? Active { get; init; }

    /// <summary>Total years served.
    ///
    /// <para><b><see cref="decimal"/> for the reason <see cref="CongressMemberPosition.YearsInTerm"/> is</b>,
    /// and more emphatically: measured 2026-08-29 across 500 rows, <b>493 carried a decimal
    /// point</b>.</para></summary>
    [JsonPropertyName("yearsActive")] public decimal? YearsActive { get; init; }
}
```

- [ ] **Step 5: Register both**

```csharp
[JsonSerializable(typeof(List<CongressMemberPosition>))]
[JsonSerializable(typeof(List<CongressMemberProfile>))]
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: PASS, 7 tests.

- [ ] **Step 7: Commit**

```bash
git add -A src/FmpDotNet/Models/CongressMember.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
       tests/FmpDotNet.Tests/CongressTests.cs tests/FmpDotNet.Tests/Fixtures/
git commit -m "feat: add CongressMemberPosition and CongressMemberProfile (#31)"
```

---

### Task 3: the net-worth records and the one converter

This is the hardest task in the plan. Read the two measurement sections on `incomeRange` and `debtDetails`
before starting.

**Files:**
- Create: `src/FmpDotNet/Models/SenateNetWorth.cs`
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated.json`
- Modify: `tests/FmpDotNet.Tests/CongressTests.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: nothing from Tasks 1-2.
- Produces: `SenateNetWorthLine`, `SenateNetWorthSummary`, `NetWorthRange`, `NetWorthDebtDetails`,
  `NetWorthRangeJsonConverter`, and two context entries.

- [ ] **Step 1: Write the fixtures**

`congress-senate-net-worth.json` — three rows: a **string** `rate`, a **numeric** `rate`, and an Asset row
carrying `incomeRange`/`income` with a null `debtDetails`:

```json
[
  {
    "senateID": "H000601",
    "formType": "Candidate Report",
    "year": 2019,
    "filingDate": "2020-03-12",
    "section": "Liabilities",
    "category": "Business Liability",
    "name": "Contour Opportunity Fund, LP         New York, New York",
    "assetType": "Other (Capital Commitment)",
    "incomeType": null,
    "owner": "Self",
    "comment": "n/a",
    "debtDetails": {
      "dateIncurred": "2015",
      "points": "-",
      "rate": "N/A%                        (10 years)"
    },
    "valueRange": { "min": 50001, "max": 100000 },
    "value": 75000.5,
    "incomeRange": null,
    "income": null,
    "link": "https://efdsearch.senate.gov/search/view/annual/a03ec2f7-ec8b-4d30-9d3c-1ee97430ca85/"
  },
  {
    "senateID": "H000601",
    "formType": "Annual Report",
    "year": 2022,
    "filingDate": "2023-08-14",
    "section": "Liabilities",
    "category": "Line of Credit",
    "name": "Roundstone Ventures LLC         Nashville, TN",
    "assetType": "Line of Credit",
    "incomeType": null,
    "owner": "Child",
    "comment": "LOC drawn by DC Trust 4",
    "debtDetails": {
      "dateIncurred": "2021",
      "points": "-",
      "rate": 1.4
    },
    "valueRange": { "min": 1000001, "max": 5000000 },
    "value": 3000000.5,
    "incomeRange": null,
    "income": null,
    "link": "https://efdsearch.senate.gov/search/view/annual/dcc46fee-29a4-4328-a975-0f6e559541dd/"
  },
  {
    "senateID": "H000601",
    "formType": "Annual Report",
    "year": 2022,
    "filingDate": "2023-08-14",
    "section": "Asset",
    "category": "Mutual Funds",
    "name": "PRJIX - T. Rowe Price New Horizons Fund I Class",
    "assetType": "Mutual Funds Mutual Fund",
    "incomeType": "Capital Gains,",
    "owner": "Self",
    "comment": null,
    "debtDetails": null,
    "valueRange": { "min": 100001, "max": 250000 },
    "value": 175000.5,
    "incomeRange": { "min": 2501, "max": 5000 },
    "income": 3750.5,
    "link": "https://efdsearch.senate.gov/search/view/annual/dcc46fee-29a4-4328-a975-0f6e559541dd/"
  }
]
```

`congress-senate-net-worth-aggregated.json`:

```json
[
  {
    "senateID": "H000601",
    "year": 2024,
    "total": 45074069.5,
    "revolvingAndCreditLines": 5250002,
    "salaryAndWages": 0,
    "businessLiabilities": 97501.5,
    "realEstateLiabilities": 1500001,
    "mutualFundsAndETFs": 14156520,
    "cashAndCashEquivalents": 5559509.5,
    "ownershipInterest": 17006511.5,
    "stock": 12741527.5,
    "governmentSecurities": 1075002.5,
    "otherAssets": 825001,
    "pensionAndRetirementAssets": 0,
    "realEstate": 557502,
    "trusts": 0
  },
  {
    "senateID": "H000601",
    "year": 2023,
    "total": 45904572.5,
    "revolvingAndCreditLines": 5250002,
    "salaryAndWages": 0,
    "businessLiabilities": 140001.5,
    "realEstateLiabilities": 1500001,
    "mutualFundsAndETFs": 14488518,
    "cashAndCashEquivalents": 5494010,
    "ownershipInterest": 19299011.5,
    "stock": 9730531.5,
    "governmentSecurities": 2300003,
    "otherAssets": 825001,
    "pensionAndRetirementAssets": 0,
    "realEstate": 657502,
    "trusts": 0
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `CongressTests`:

```csharp
    [Fact]
    public void An_income_range_sent_as_the_empty_string_binds_null_and_costs_no_other_row()
    {
        // THE trap of this slice, and the reason NetWorthRangeJsonConverter exists. Measured 2026-08-29,
        // `incomeRange` is an object on 136 of 250 rows, JSON null on 100, and THE EMPTY STRING on 14.
        // System.Text.Json cannot read a string into an object, so without the converter those 14 rows throw
        // — and the throw is not confined to its row: a three-row array where only the middle row sends ""
        // recovered 0 of 3. On this one member that is 14 rows costing all 250.
        //
        // The object-valued rows either side are the point of the test: remove the converter and they are
        // lost too.
        var rows = JsonSerializer.Deserialize(
            """
            [{"senateID":"H000601","incomeRange":{"min":2501,"max":5000},"income":3750.5},
             {"senateID":"H000601","incomeRange":"","income":null,"incomeType":""},
             {"senateID":"H000601","incomeRange":{"min":0,"max":201},"income":0}]
            """,
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(2501m, rows[0].IncomeRange!.Min);
        Assert.Equal(5000m, rows[0].IncomeRange!.Max);
        Assert.Null(rows[1].IncomeRange);
        Assert.Equal("", rows[1].IncomeType);
        Assert.Equal(0m, rows[2].IncomeRange!.Min);
        Assert.Equal(201m, rows[2].IncomeRange!.Max);

        // Row 2 is the measured mismatch that proves `income` is not derived from `incomeRange`: the midpoint
        // of 0 and 201 is 100.5 and FMP reports 0. Asserted so nobody "fixes" this into a computed property.
        Assert.Equal(0m, rows[2].Income);
        Assert.NotEqual((rows[2].IncomeRange!.Min + rows[2].IncomeRange!.Max) / 2, rows[2].Income);
    }

    [Fact]
    public void Debt_details_binds_in_both_of_the_shapes_FMP_sends()
    {
        // Measured 2026-08-29, `debtDetails` is a union of two DISJOINT shapes — 87 rows carry
        // dateIncurred/points/rate and 13 carry `source` alone. Never all four keys at once. One record with
        // four nullable properties covers both because an absent key binds null.
        var rows = JsonSerializer.Deserialize(
            """
            [{"debtDetails":{"dateIncurred":"2021","points":"-","rate":1.4}},
             {"debtDetails":{"source":"Hall Capital Management Co, LLC         Oklahoma City, OK"}}]
            """,
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("2021", rows[0].DebtDetails!.DateIncurred);
        Assert.Equal("-", rows[0].DebtDetails!.Points);
        Assert.Equal("1.4", rows[0].DebtDetails!.Rate);
        Assert.Null(rows[0].DebtDetails!.Source);

        Assert.StartsWith("Hall Capital", rows[1].DebtDetails!.Source);
        Assert.Null(rows[1].DebtDetails!.DateIncurred);
        Assert.Null(rows[1].DebtDetails!.Rate);
    }

    [Fact]
    public void A_rate_carrying_a_term_survives_intact_beside_a_numeric_one()
    {
        // `rate` arrives as float, int OR string. The strings are not placeholders — they carry a term as
        // well as a rate, so a tolerant numeric converter would bind null and discard "10 years" with it.
        // 64 of the 100 rows where debtDetails is present look like this.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal("N/A%                        (10 years)", rows[0].DebtDetails!.Rate);
        Assert.Equal("1.4", rows[1].DebtDetails!.Rate);
        Assert.Null(rows[2].DebtDetails);
    }

    [Fact]
    public void A_captured_net_worth_line_binds_and_value_is_the_midpoint_of_its_range()
    {
        // `value` is the midpoint of `valueRange` on 214 of 214 rows where both are present, measured
        // 2026-08-29. Neither figure is recomputed by the SDK; this pins that FMP's own arithmetic is what
        // was measured, and it is where the `.5` endings across this group come from.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("H000601", rows[2].SenateId);
        Assert.Equal("Annual Report", rows[2].FormType);
        Assert.Equal(2022, rows[2].Year);
        Assert.Equal(new LocalDate(2023, 8, 14), rows[2].FilingDate);
        Assert.Equal("Asset", rows[2].Section);
        Assert.Equal(100001m, rows[2].ValueRange!.Min);
        Assert.Equal(250000m, rows[2].ValueRange!.Max);
        Assert.Equal(175000.5m, rows[2].Value);
        Assert.Equal((rows[2].ValueRange!.Min + rows[2].ValueRange!.Max) / 2, rows[2].Value);

        // The sibling pair does NOT follow that rule — 35 hold, 101 fail — so nothing here asserts it.
        Assert.Equal(3750.5m, rows[2].Income);
    }

    [Fact]
    public void Date_incurred_is_a_year_string_and_not_a_date()
    {
        // Seven distinct values measured 2026-08-29, every one a bare four-digit year. Typing it LocalDate?
        // would fail on every row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("2015", rows[0].DebtDetails!.DateIncurred);
    }

    [Fact]
    public void Every_money_field_on_the_aggregate_binds_whether_or_not_it_carries_a_decimal_point()
    {
        // 8 of the 14 money fields changed representation across only six measured rows; the other 6 stayed
        // integral, which proves nothing about the seventh. All fourteen are decimal?, and this test asserts
        // one of each kind so typing any of them `int` fails here.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(2, rows.Count);
        Assert.Equal("H000601", rows[0].SenateId);
        Assert.Equal(2024, rows[0].Year);

        Assert.Equal(45074069.5m, rows[0].Total);                    // flipped
        Assert.Equal(97501.5m, rows[0].BusinessLiabilities);          // flipped
        Assert.Equal(12741527.5m, rows[0].Stock);                     // flipped
        Assert.Equal(5559509.5m, rows[0].CashAndCashEquivalents);     // flipped
        Assert.Equal(17006511.5m, rows[0].OwnershipInterest);         // flipped
        Assert.Equal(1075002.5m, rows[0].GovernmentSecurities);       // flipped
        Assert.Equal(14156520m, rows[0].MutualFundsAndEtfs);          // integral here, flips on other rows
        Assert.Equal(557502m, rows[0].RealEstate);                    // integral here, flips on other rows
        Assert.Equal(5250002m, rows[0].RevolvingAndCreditLines);      // integral on all six
        Assert.Equal(0m, rows[0].SalaryAndWages);
        Assert.Equal(1500001m, rows[0].RealEstateLiabilities);
        Assert.Equal(825001m, rows[0].OtherAssets);
        Assert.Equal(0m, rows[0].PensionAndRetirementAssets);
        Assert.Equal(0m, rows[0].Trusts);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: FAIL — the record types and the converter do not exist.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs` (the file holds the SDK's converters despite its
name; follow the existing class layout there):

```csharp
/// <summary>Reads <c>SenateNetWorthLine.incomeRange</c>, which FMP sends as an object, as JSON
/// <see langword="null"/>, <b>or as the empty string</b>.
///
/// <para><b>This converter is not a convenience.</b> Measured 2026-08-29 over 250 rows for one filer,
/// <c>incomeRange</c> was an object on 136, <c>null</c> on 100 and <c>""</c> on 14.
/// <see cref="System.Text.Json.JsonSerializer"/> cannot read a string into an object, so a plain
/// <see cref="Models.NetWorthRange"/> property throws on those 14 — and the throw aborts the whole array
/// rather than the row, so on that filer 14 rows cost all 250.</para>
///
/// <para><b>Applied to <c>incomeRange</c> only.</b> Its sibling <c>valueRange</c> was an object on all 214
/// rows where it was present and never a string; putting this converter there too would assert a wire form
/// that was never measured.</para></summary>
public sealed class NetWorthRangeJsonConverter : JsonConverter<Models.NetWorthRange?>
{
    /// <inheritdoc />
    public override Models.NetWorthRange? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // The empty string is the whole reason this type exists. Any other string is unmeasured and is also
        // read as null rather than thrown on, because one unrecognised value must not cost the response.
        if (reader.TokenType == JsonTokenType.String)
        {
            reader.GetString();
            return null;
        }

        if (reader.TokenType == JsonTokenType.Null) return null;

        return JsonSerializer.Deserialize(ref reader, FmpJsonContext.Default.NetWorthRange);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer, Models.NetWorthRange? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value, FmpJsonContext.Default.NetWorthRange);
    }
}
```

`FmpJsonContext` must also gain `[JsonSerializable(typeof(NetWorthRange))]` — the bare type, not the list —
for `FmpJsonContext.Default.NetWorthRange` to exist.

- [ ] **Step 5: Write the records**

`src/FmpDotNet/Models/SenateNetWorth.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>A disclosed dollar band — the <c>min</c> and <c>max</c> of a bracket on a Senate financial
/// disclosure.
///
/// <para>Used twice on <see cref="SenateNetWorthLine"/>, for <c>valueRange</c> and <c>incomeRange</c>. Both
/// bounds were integral on every row measured 2026-08-29 — 428 numbers under <c>valueRange</c>, 272 under
/// <c>incomeRange</c>, none carrying a decimal point — and both are <see cref="decimal"/> anyway: they are
/// money, and integral samples say nothing about the next row.</para></summary>
public sealed record NetWorthRange
{
    /// <summary>The bottom of the band.</summary>
    [JsonPropertyName("min")] public decimal? Min { get; init; }

    /// <summary>The top of the band.</summary>
    [JsonPropertyName("max")] public decimal? Max { get; init; }
}

/// <summary>The terms of a disclosed liability, nested on <see cref="SenateNetWorthLine"/>.
///
/// <para><b>A union of two disjoint shapes.</b> Measured 2026-08-29 over the 100 rows where it is present, 87
/// carried <see cref="DateIncurred"/>, <see cref="Points"/> and <see cref="Rate"/>, and 13 carried
/// <see cref="Source"/> alone. Never all four together — an absent key binds
/// <see langword="null"/>.</para></summary>
public sealed record NetWorthDebtDetails
{
    /// <summary>When the debt was incurred.
    ///
    /// <para><b>A year, not a date, and therefore <see cref="string"/>.</b> Measured 2026-08-29, seven
    /// distinct values and every one a bare four-digit year — <c>2003</c>, <c>2021</c>. A
    /// <see cref="LocalDate"/> would fail on all of them.</para></summary>
    [JsonPropertyName("dateIncurred")] public string? DateIncurred { get; init; }

    /// <summary>Points on the loan.
    ///
    /// <para><b><see cref="string"/> because FMP sends two types.</b> Measured 2026-08-29, this was the
    /// string <c>"-"</c> on 82 of 100 rows and the number <c>0</c> on 5. Mapping <c>"-"</c> to
    /// <see langword="null"/> would collapse it into the 13 rows that are genuinely null, and those are
    /// three states FMP distinguishes.</para></summary>
    [JsonPropertyName("points")] public string? Points { get; init; }

    /// <summary>The interest rate.
    ///
    /// <para><b><see cref="string"/>, and this is the one place in the slice where the SDK hands back
    /// something it could have parsed.</b> Measured 2026-08-29, <c>rate</c> arrives as a number on 23 of 100
    /// rows (<c>1.4</c>, <c>2.75</c>, <c>5.25</c>, <c>3</c>) and as a string on 64. The strings are not
    /// placeholders — they carry a term as well as a rate:</para>
    ///
    /// <code>
    /// "N/A%                        (10 years)"
    /// "NA%                        (On Demand)"
    /// </code>
    ///
    /// <para>A tolerant numeric converter would bind <see langword="null"/> on those 64 and discard
    /// "10 years" and "On Demand" with them. FMP has overloaded the field; the SDK reports it rather than
    /// guessing at it.</para></summary>
    [JsonPropertyName("rate")] public string? Rate { get; init; }

    /// <summary>Who the debt is owed to. Present on the 13 rows that carry no rate terms; see the record
    /// summary.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
}

/// <summary>One line of a Senator's financial disclosure, from <c>stable/senate-net-worth</c>.
///
/// <para>One row per disclosed asset, income source or liability, across every report the member has filed.
/// Measured 2026-08-29, <c>H000601</c> answered <b>250 rows</b> and <c>limit</c> was ignored.</para>
///
/// <para><b>Read <see cref="IncomeRange"/> before changing anything here.</b> It is the one property in this
/// slice that needs a converter, and the reason is a hard binding failure rather than a nicety.</para>
///
/// <para><b><see cref="Value"/> is the midpoint of <see cref="ValueRange"/>; <see cref="Income"/> is NOT the
/// midpoint of <see cref="IncomeRange"/>.</b> The symmetry is a trap — see
/// <see cref="Income"/>.</para></summary>
public sealed record SenateNetWorthLine
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Which filing — <c>Annual Report</c> or <c>Candidate Report</c>.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The reporting year. A calendar year and whole by its own nature, hence
    /// <see cref="int"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>When the report was filed.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>Which part of the disclosure — <c>Asset</c>, <c>Income</c> or <c>Liabilities</c>.</summary>
    [JsonPropertyName("section")] public string? Section { get; init; }

    /// <summary>FMP's category for the line.</summary>
    [JsonPropertyName("category")] public string? Category { get; init; }

    /// <summary>The asset or counterparty as the disclosure names it. Carries the runs of internal whitespace
    /// the filing does; it is passed through rather than tidied.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The instrument. Free text from the filing, not the vocabulary
    /// <see cref="CongressionalTrade.AssetType"/> uses.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>What kind of income the line produced, where it produces any.</summary>
    [JsonPropertyName("incomeType")] public string? IncomeType { get; init; }

    /// <summary>Whose holding — <c>Self</c>, <c>Joint</c> or <c>Child</c>.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }

    /// <summary>The filer's note.</summary>
    [JsonPropertyName("comment")] public string? Comment { get; init; }

    /// <summary>The liability's terms, on the rows that are liabilities. Null on 150 of 250 rows measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("debtDetails")] public NetWorthDebtDetails? DebtDetails { get; init; }

    /// <summary>The disclosed value band. An object on all 214 rows where it was present, measured
    /// 2026-08-29 — never the empty string its sibling <see cref="IncomeRange"/> sends, which is why this one
    /// carries no converter.</summary>
    [JsonPropertyName("valueRange")] public NetWorthRange? ValueRange { get; init; }

    /// <summary>The midpoint of <see cref="ValueRange"/>, as FMP computes it.
    ///
    /// <para>Verified on <b>214 of 214 rows</b> where both are present, failing on none, measured 2026-08-29.
    /// The SDK passes it through rather than recomputing it. This is where the <c>.5</c> endings across this
    /// group come from.</para></summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }

    /// <summary>The disclosed income band.
    ///
    /// <para><b>Carries <see cref="NetWorthRangeJsonConverter"/>, and must keep it.</b> Measured 2026-08-29
    /// over 250 rows, this arrives as an object on 136, as JSON <see langword="null"/> on 100, and <b>as the
    /// empty string on 14</b>. <c>System.Text.Json</c> cannot read a string into an object, so without the
    /// converter those 14 rows throw — and the throw aborts the entire array, so they cost all 250. The
    /// converter reads <c>""</c> as <see langword="null"/>.</para></summary>
    [JsonPropertyName("incomeRange")]
    [JsonConverter(typeof(NetWorthRangeJsonConverter))]
    public NetWorthRange? IncomeRange { get; init; }

    /// <summary>The income figure FMP reports for the line.
    ///
    /// <para><b>Not the midpoint of <see cref="IncomeRange"/>, and the symmetry with
    /// <see cref="Value"/> is a trap.</b> Measured 2026-08-29 over the 136 rows where the range is an object
    /// and this is present, the midpoint holds on <b>35</b> and fails on <b>101</b> — the first mismatch being
    /// a range of 0 to 201 against an income of 0. Neither figure is derived by the SDK; both are passed
    /// through as sent.</para></summary>
    [JsonPropertyName("income")] public decimal? Income { get; init; }

    /// <summary>The filed disclosure document.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}

/// <summary>One year of a Senator's net worth, totalled by category, from
/// <c>stable/senate-net-worth-aggregated</c>.
///
/// <para>One row per reporting year. Measured 2026-08-29, <c>H000601</c> answered six, 2019 through
/// 2024.</para>
///
/// <para><b>Every one of the fourteen money fields is <see cref="decimal"/>, including the six that looked
/// integral.</b> Measured 2026-08-29 across those six rows, 8 of the 14 changed between bare-integer and
/// decimal-point representation. The other 6 did not, and that is not an exemption: six rows all landing on
/// integers says nothing about the seventh, and one fractional value under <see cref="int"/> costs the whole
/// response rather than the field.</para></summary>
public sealed record SenateNetWorthSummary
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>The reporting year. Whole by its own nature, hence <see cref="int"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>Net worth for the year.</summary>
    [JsonPropertyName("total")] public decimal? Total { get; init; }

    /// <summary>Revolving credit and lines of credit owed.</summary>
    [JsonPropertyName("revolvingAndCreditLines")] public decimal? RevolvingAndCreditLines { get; init; }

    /// <summary>Salary and wage income.</summary>
    [JsonPropertyName("salaryAndWages")] public decimal? SalaryAndWages { get; init; }

    /// <summary>Liabilities arising from business interests.</summary>
    [JsonPropertyName("businessLiabilities")] public decimal? BusinessLiabilities { get; init; }

    /// <summary>Mortgages and other property debt.</summary>
    [JsonPropertyName("realEstateLiabilities")] public decimal? RealEstateLiabilities { get; init; }

    /// <summary>Holdings in mutual funds and ETFs.</summary>
    [JsonPropertyName("mutualFundsAndETFs")] public decimal? MutualFundsAndEtfs { get; init; }

    /// <summary>Cash and equivalents.</summary>
    [JsonPropertyName("cashAndCashEquivalents")] public decimal? CashAndCashEquivalents { get; init; }

    /// <summary>Equity in privately held businesses.</summary>
    [JsonPropertyName("ownershipInterest")] public decimal? OwnershipInterest { get; init; }

    /// <summary>Directly held stock.</summary>
    [JsonPropertyName("stock")] public decimal? Stock { get; init; }

    /// <summary>Treasuries and other government paper.</summary>
    [JsonPropertyName("governmentSecurities")] public decimal? GovernmentSecurities { get; init; }

    /// <summary>Everything not covered by another category.</summary>
    [JsonPropertyName("otherAssets")] public decimal? OtherAssets { get; init; }

    /// <summary>Pension and retirement balances.</summary>
    [JsonPropertyName("pensionAndRetirementAssets")] public decimal? PensionAndRetirementAssets { get; init; }

    /// <summary>Real property held.</summary>
    [JsonPropertyName("realEstate")] public decimal? RealEstate { get; init; }

    /// <summary>Assets held in trust.</summary>
    [JsonPropertyName("trusts")] public decimal? Trusts { get; init; }
}
```

- [ ] **Step 6: Register everything**

```csharp
[JsonSerializable(typeof(List<SenateNetWorthLine>))]
[JsonSerializable(typeof(List<SenateNetWorthSummary>))]
[JsonSerializable(typeof(NetWorthRange))]
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: PASS, 13 tests.

- [ ] **Step 8: Prove the converter is load-bearing**

Temporarily delete `[JsonConverter(typeof(NetWorthRangeJsonConverter))]` from `IncomeRange` and re-run.
Expected: `An_income_range_sent_as_the_empty_string_binds_null_and_costs_no_other_row` FAILS with a
`JsonException`. Restore the attribute and re-run to green. **Record both outcomes in the ledger** — a trap
test that was never seen to fail is not yet a guard.

- [ ] **Step 9: Commit**

```bash
git add -A src/FmpDotNet tests/FmpDotNet.Tests
git commit -m "feat: add the Senate net-worth records and the incomeRange converter (#31)"
```

---

### Task 4: the `CongressEndpoints` facade

**Files:**
- Create: `src/FmpDotNet/Endpoints/CongressEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/CongressTests.cs`

**Interfaces:**
- Consumes: all five records from Tasks 1-3 and their `FmpJsonContext` entries.
- Produces: `public sealed class CongressEndpoints(FmpTransport transport)` with the twelve methods below and
  `public const int MaxCongressionalTradePageSize = 250`.

- [ ] **Step 1: Write the failing tests**

Append to `CongressTests`:

```csharp
    [Fact]
    public async Task By_member_sends_senateID_and_never_id()
    {
        // THE trap of this slice's request surface. Measured 2026-08-29, `stable/house-trades-by-id` is named
        // for a parameter it does not accept: `?id=M001217` came back BYTE-IDENTICAL to the bare call — 100
        // rows spanning 21 different members, HTTP 200, no error. The wire parameter is `senateID`.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHouseTradesByMemberAsync("M001217");

        var query = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("senateID=M001217", query);
        Assert.DoesNotContain("id=M001217", query.Replace("senateID=M001217", ""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task By_member_refuses_a_missing_id_before_it_reaches_the_wire(string? senateId)
    {
        // The endpoint ANSWERS without the parameter, with someone else's data. That is exactly why the SDK
        // must not pass a blank through: FMP's willingness to reply is the hazard.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => endpoints.GetSenateTradesByMemberAsync(senateId!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_latest_feeds_refuse_a_limit_above_the_measured_cap()
    {
        // Measured 2026-08-29: limit=1000 and limit=5000 each answered exactly 250 with HTTP 200 and nothing
        // in the body saying the request was trimmed. A caller who asks for 1000 and pages by 1000 reads a
        // quarter of the feed and is never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHouseLatestAsync(limit: CongressEndpoints.MaxCongressionalTradePageSize + 1));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"),
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetHouseLatestAsync();
        await endpoints.GetSenateLatestAsync();
        await endpoints.GetHouseTradesAsync("AAPL");
        await endpoints.GetHouseTradesByNameAsync("Pelosi");
        await endpoints.GetPositionsAsync();
        await endpoints.GetNetWorthSummaryAsync("H000601");

        Assert.Equal("/stable/house-latest", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/stable/senate-latest", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("/stable/house-trades", handler.Requests[2].RequestUri!.AbsolutePath);
        Assert.Equal("/stable/house-trades-by-name", handler.Requests[3].RequestUri!.AbsolutePath);
        Assert.Equal("/stable/senate-positions", handler.Requests[4].RequestUri!.AbsolutePath);
        Assert.Equal("/stable/senate-net-worth-aggregated", handler.Requests[5].RequestUri!.AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[2].RequestUri!.Query);
        Assert.Contains("name=Pelosi", handler.Requests[3].RequestUri!.Query);
        Assert.Contains("senateID=H000601", handler.Requests[5].RequestUri!.Query);
    }
```

Check `StubHandler`'s existing helper name before writing these — the other test files use it, and this plan
assumes `StubHandler.Json(string)` plus a `Requests` list. If the real names differ, follow the real ones.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: FAIL — `CongressEndpoints` does not exist.

- [ ] **Step 3: Write the facade**

`src/FmpDotNet/Endpoints/CongressEndpoints.cs`. The twelve methods, all following the two shapes below.

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's congressional disclosure group — what members of Congress traded, who they are, and what
/// Senators are worth.
///
/// <para><b>Twelve paths, five row shapes.</b> Eight of the twelve answer
/// <see cref="CongressionalTrade"/>; the other four each answer their own.</para>
///
/// <para><b>Two of those paths are named for a parameter they do not accept, and this facade exists partly to
/// close that.</b> <c>house-trades-by-id</c> and <c>senate-trades-by-id</c> take <c>senateID</c>. Measured
/// 2026-08-29, passing <c>id</c> is not rejected — it is discarded, and the endpoint answers 200 with the
/// unfiltered latest feed: 100 well-formed rows belonging to 21 members the caller did not ask about. See
/// <see cref="GetHouseTradesByMemberAsync"/>.</para>
///
/// <para><b>Row order is not stable between calls.</b> Measured 2026-08-29, two requests seconds apart
/// returned the same 142 rows with 104 of 142 positions changed. Nothing that consumes these methods may
/// depend on position.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-29 against an Ultimate key. No path in
/// the group answered 402.</para></summary>
public sealed class CongressEndpoints(FmpTransport transport)
{
    /// <summary>The largest page either latest feed will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-29, <c>house-latest?limit=1000</c> and
    /// <c>?limit=5000</c> each answered exactly 250 rows at HTTP 200, with nothing in the body saying the
    /// request had been trimmed.</para></summary>
    public const int MaxCongressionalTradePageSize = 250;

    /// <summary>Every House disclosure as it arrives, newest first — <c>stable/house-latest</c>.
    ///
    /// <para>The 100 rows a bare call returns is a default rather than a cap; see
    /// <see cref="MaxCongressionalTradePageSize"/> for where it stops.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an
    /// error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCongressionalTradePageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's disclosures. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCongressionalTradePageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseLatestAsync(
        int page = 0, int limit = 100, CancellationToken ct = default)
    {
        ThrowIfPagingOutOfRange(page, limit);
        return transport.GetListAsync(
            new FmpRequest("stable/house-latest").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure of one ticker — <c>stable/house-trades</c>.</summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unknown symbol, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades").With("symbol", symbol),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by one member — <c>stable/house-trades-by-id</c>.
    ///
    /// <para><b>The path is named <c>-by-id</c> and the parameter is <c>senateID</c>.</b> Measured
    /// 2026-08-29, <c>?id=M001217</c> was silently ignored and answered the unfiltered latest feed —
    /// 100 rows spanning 21 members — while <c>?senateID=M001217</c> answered that member alone. This method
    /// sends <c>senateID</c> and requires it, because the endpoint's willingness to answer without one is the
    /// hazard rather than a convenience. For the unfiltered feed, call
    /// <see cref="GetHouseLatestAsync"/>, which says so in its name.</para></summary>
    /// <param name="senateId">The member's Bioguide identifier — <c>M001217</c>. Carried on every row as
    /// <see cref="CongressionalTrade.SenateId"/>, and listed by
    /// <see cref="GetProfilesAsync"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That member's disclosures. Never <see langword="null"/>; empty for a member with none, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="senateId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="senateId"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByMemberAsync(
        string senateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senateId);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades-by-id").With("senateID", senateId),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>Every House disclosure by surname — <c>stable/house-trades-by-name</c>.
    ///
    /// <para><b>Matches the last name.</b> Measured 2026-08-29, <c>name=Pelosi</c> answered 142 rows all
    /// belonging to <c>P000197</c>, and a given name — <c>name=Zach</c> — answered none.</para>
    ///
    /// <para>An empty result means the member disclosed nothing, not that the lookup failed: Zach Nunn is a
    /// sitting Representative in <see cref="GetProfilesAsync"/> with no trades.</para></summary>
    /// <param name="name">The member's surname.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching disclosures. Never <see langword="null"/>; empty for an unmatched surname, not an
    /// error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CongressionalTrade>> GetHouseTradesByNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/house-trades-by-name").With("name", name),
            FmpJsonContext.Default.ListCongressionalTrade, ct);
    }

    /// <summary>The paging guard the two latest feeds share, extracted for the reason
    /// <see cref="InsiderTradesEndpoints"/> extracts its own: the two callers need an identical guard set, so
    /// the body is the thing that must not drift between them.</summary>
    private static void ThrowIfPagingOutOfRange(int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxCongressionalTradePageSize);
    }
}
```

**The remaining eight methods follow exactly these two shapes.** Write each in full — do not abbreviate, and
do not share a body between the House and Senate variants: they are separate endpoints and each is modelled as
itself, the way `InsiderTradesEndpoints.GetLatestAsync` and `SearchAsync` are.

| method | path | parameter | returns |
|---|---|---|---|
| `GetSenateLatestAsync` | `stable/senate-latest` | `page`, `limit` | `CongressionalTrade` |
| `GetSenateTradesAsync` | `stable/senate-trades` | `symbol` | `CongressionalTrade` |
| `GetSenateTradesByMemberAsync` | `stable/senate-trades-by-id` | `senateID` | `CongressionalTrade` |
| `GetSenateTradesByNameAsync` | `stable/senate-trades-by-name` | `name` | `CongressionalTrade` |
| `GetPositionsAsync` | `stable/senate-positions` | `page` only | `CongressMemberPosition` |
| `GetProfilesAsync` | `stable/senate-profile` | `page` only | `CongressMemberProfile` |
| `GetNetWorthAsync` | `stable/senate-net-worth` | `senateID` | `SenateNetWorthLine` |
| `GetNetWorthSummaryAsync` | `stable/senate-net-worth-aggregated` | `senateID` | `SenateNetWorthSummary` |

`GetPositionsAsync` and `GetProfilesAsync` take `int page = 0` and **no `limit`**, guarded with
`ArgumentOutOfRangeException.ThrowIfNegative(page)` alone. Their XML docs must say that FMP ignores `limit` —
measured 2026-08-29, `senate-positions?limit=500` answered 300 and `senate-profile?limit=1000` answered 500 —
so a reader does not think the omission is an oversight.

`GetSenateLatestAsync`'s XML doc must carry the `capitalGainsOver200USD` warning: it is the one path of the
eight whose rows leave that property null.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj --filter CongressTests`
Expected: PASS, 18 tests.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/Endpoints/CongressEndpoints.cs tests/FmpDotNet.Tests/CongressTests.cs
git commit -m "feat: add the fmp.Congress facade over twelve congressional paths (#31)"
```

---

### Task 5: wiring

**Files:**
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `CongressEndpoints` from Task 4.
- Produces: `FmpClient.Congress`.

- [ ] **Step 1: Add the constructor parameter and property**

In `src/FmpDotNet/FmpClient.cs`, add `CongressEndpoints congress` to the primary constructor — **append it
after `insiderTrades`**, keeping the existing order, and add:

```csharp
    /// <summary>Congressional disclosure — what members of Congress traded, who they are, and what Senators
    /// are worth.</summary>
    public CongressEndpoints Congress { get; } = congress;
```

- [ ] **Step 2: Register it**

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, beside the others:

```csharp
        services.TryAddTransient<CongressEndpoints>();
```

- [ ] **Step 3: Add the README coverage block**

Follow the existing per-facade format exactly. Place it in the same alphabetical position the other facades
use:

```markdown
`fmp.Congress`

| FMP endpoint | Method |
|---|---|
| `stable/house-latest` | `GetHouseLatestAsync` |
| `stable/house-trades` | `GetHouseTradesAsync` |
| `stable/house-trades-by-id` | `GetHouseTradesByMemberAsync` |
| `stable/house-trades-by-name` | `GetHouseTradesByNameAsync` |
| `stable/senate-latest` | `GetSenateLatestAsync` |
| `stable/senate-net-worth` | `GetNetWorthAsync` |
| `stable/senate-net-worth-aggregated` | `GetNetWorthSummaryAsync` |
| `stable/senate-positions` | `GetPositionsAsync` |
| `stable/senate-profile` | `GetProfilesAsync` |
| `stable/senate-trades` | `GetSenateTradesAsync` |
| `stable/senate-trades-by-id` | `GetSenateTradesByMemberAsync` |
| `stable/senate-trades-by-name` | `GetSenateTradesByNameAsync` |
```

Also update the coverage count wherever README states it — it moves from **154 of 243** to **166 of 243**.
Search the file for `154` before editing; do not assume there is only one occurrence.

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj`
Expected: PASS, every test. The DI container test — if one exists that resolves `FmpClient` — must still
resolve.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/FmpClient.cs src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs README.md
git commit -m "feat: expose fmp.Congress on FmpClient and document its coverage (#31)"
```

---

### Task 6: the live guard

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`

**Interfaces:**
- Consumes: `CongressEndpoints` from Task 4, reached by the probe's reflection over `FmpClient`.
- Produces: twelve new baseline rows.

- [ ] **Step 1: Add the constants**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`:

```csharp
    /// <summary>A Senator's Bioguide identifier for the three <c>senateID</c>-keyed congressional probes —
    /// Bill Hagerty, <c>H000601</c>.
    ///
    /// <para><b>Chosen because he answers on all three.</b> Measured 2026-08-29 he returns 250 rows from
    /// <c>senate-net-worth</c> and six from <c>senate-net-worth-aggregated</c>. The same silent green
    /// <see cref="FilerCik"/> was named for applies here with a sharper edge: the two <c>-by-id</c> paths
    /// answer 200 with the WRONG member's data rather than zero rows when the parameter does not reach
    /// them.</para></summary>
    public const string SenateId = "H000601";

    /// <summary>A surname for the two congressional <c>by-name</c> probes — <c>Pelosi</c>.
    ///
    /// <para>Measured 2026-08-29, answers 142 rows on <c>house-trades-by-name</c>. Its own constant rather
    /// than <see cref="InsiderNameQuery"/>'s, so a change to the insider probe cannot silently move this one —
    /// the same separation that constant was created for.</para>
    ///
    /// <para><b>Deliberately not a member with no disclosures.</b> <c>Nunn</c> answers zero rows and is a
    /// sitting Representative, so it would record <c>rows 0</c> as the baseline and match it green
    /// forever.</para></summary>
    public const string CongressNameQuery = "Pelosi";
```

- [ ] **Step 2: Add the probe dispatch arms**

In `Probe.Argument`'s `string` switch, before the general `"name"` arm and following the existing
declaring-type pattern:

```csharp
                // The two congressional by-name paths match a surname, not a company and not an insider.
                // Its own constant for the reason the InsiderTrades arm above has one.
                "name" when parameter.Member.DeclaringType == typeof(Endpoints.CongressEndpoints)
                    => LiveApi.CongressNameQuery,

                // Three congressional paths key on a member's Bioguide id. No default would be right here:
                // the two -by-id paths answer 200 with the unfiltered feed when the parameter is wrong, so a
                // fallen-through AAPL would record a green baseline over someone else's data.
                "senateId" => LiveApi.SenateId,
```

Confirm the parameter is spelled `senateId` in the facade before adding this — the arm dispatches on the C#
parameter name, not the wire name.

- [ ] **Step 3: Check the coverage test passes**

Run: `dotnet test tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj --filter SweepCoverage`
Expected: PASS. `SweepCoverageTests` asserts every public endpoint method is reachable by the sweep; the
twelve new methods must all be covered without a skip.

- [ ] **Step 4: Re-record the ordinary baseline**

This is the only step in the plan that calls the live API. **Do not set `FMPDOTNET_SMOKE_BULK`** — no bulk
path is involved in this slice and setting it risks the key.

Follow the procedure in `CONTRIBUTING.md` for re-recording, then inspect the diff before committing:

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Expected: **twelve added rows and nothing else changed.** Any modified pre-existing row means this slice moved
something it should not have — stop and investigate rather than committing it.

Every new row must read `outcome rows` rather than `outcome empty`. An `empty` row means the probe argument
did not reach the endpoint, which is the failure `SenateId` and `CongressNameQuery` were chosen to prevent.

- [ ] **Step 5: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/
git commit -m "test: add the twelve congressional paths to the live sweep (#31)"
```

---

## Definition of done

- All twelve paths of #31 reachable through `fmp.Congress`, each with an XML doc carrying its measurements.
- Full unit suite green; solution builds clean under `TreatWarningsAsErrors`.
- Twelve new `outcome rows` lines in `baseline-ordinary.txt`, no pre-existing row modified.
- README coverage moved from 154 to 166 of 243.
- The ledger records the Step 8 result from Task 3 — the converter test observed both failing and passing.
