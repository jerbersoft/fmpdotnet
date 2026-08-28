# SEC Filings Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model the twelve documented `stable/` paths FMP files under SEC Filings, closing that section at 12 of 12 and taking SDK coverage from 114 of 243 paths to 126.

**Architecture:** One new facade — `fmp.SecFilings`, the eleventh — takes the nine paths that return filings or registrant data; the other three are reference lists and go to the facades whose job they already are, `fmp.Directory` (+2) and `fmp.Search` (+1). Every one of the twelve is an ordinary `GET` returning a JSON array that `FmpTransport.GetListAsync` already serves: no new transport primitive, no streaming, no CSV. Four new records, two new converters, four `FmpJsonContext` entries. Every type is built from the 2026-08-28 measurement pass rather than from FMP's documentation, and each measured trap gets a test that fails when the trap is reintroduced.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` source generation via `FmpJsonContext`, NodaTime (`LocalDate`, `Instant`), xUnit v2 (2.9.3).

**Spec:** `docs/superpowers/specs/2026-08-28-sec-filings-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-28-sec-filings-measurements.md`

## Global Constraints

- `TreatWarningsAsErrors=true` (`Directory.Build.props`) covers `CS*` and `NU*`. `IsAotCompatible` turns IL2026/IL3050 into build errors — never call a reflection-based `JsonSerializer.Deserialize`; every model goes through `FmpJsonContext`.
- **Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as `[JsonSerializable(typeof(List<X>))]` or it fails at runtime, not at compile time.** Four entries are added across this plan: `IndustryClassification`, `SecFiling`, `SecProfile`, `SicCodeEntry`.
- Models are `public sealed record` with `init` properties and an explicit `[JsonPropertyName]` on every member. **No `required` members and no non-nullable properties** — an absent JSON key binds an `init` member to `default` rather than honouring a field initialiser, and every one of these twelve paths was measured returning at least one blank or null field.
- `cik` and `sicCode` are **`string?`, never an integer type.** Measured 2026-08-28: `cik` arrives zero-padded to ten characters (`"0000320193"`), and `sicCode` arrives four characters wide on `all-industry-classification` (`"6021"`) but with the leading zero stripped on `standard-industrial-classification-list` (`"100"` for SIC 0100, "AGRICULTURAL PRODUCTION-CROPS"). The SDK preserves what FMP sent and normalises neither. `sicCode` also arrives as `""` on `sec-filings-company-search/name`.
- Dates carrying no time of day are `LocalDate?`. `filingDate` arrives as `uuuu-MM-dd HH:mm:ss` with a dummy midnight and uses the new `NullableDateAtMidnightJsonConverter`; the existing `NullableLocalDateJsonConverter` **cannot read it** (it uses `LocalDatePattern.Iso`, which rejects the trailing time).
- EDGAR acceptance stamps are `Instant?` via `NullableEasternInstantJsonConverter` — EDGAR's wall clock, matching `IncomeStatement.AcceptedDate`. **Never `NullableFmpInstantJsonConverter`**, which is the UTC one and would shift every value by 4 or 5 hours with nothing in the data to reveal it.
- Every public member carries XML documentation in house style: it records **what was measured and on what date** (every measurement in this slice is 2026-08-28 against an Ultimate key), and states plainly anything a caller would otherwise get wrong. Where a value is a trap, the documentation is the deliverable, not decoration.
- Public list-returning methods return `IReadOnlyList<T>`, never null. Single-row lookups return `T?`, because an unknown-but-well-formed input answers an empty array with HTTP 200 rather than a 404.
- A signature must not accept a parameter the endpoint ignores. Three such parameters are measured in this slice; see Task 2 and Task 9.
- Tests are xUnit `[Fact]`/`[Theory]` with sentence-style method names using underscores, matching `CompanyEndpointsTests`.
- **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the response after reading. A test driving N calls builds N responses.
- Fixtures are verbatim captures from the 2026-08-28 measurement pass, five rows each, and **must not contain the API key**. The key travels in the query string, so never write a built URL into a fixture or a log line. The `Fixtures\*.json` glob in `FmpDotNet.Tests.csproj` copies them automatically — no csproj change is needed.
- Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore. A mutation that fails to compile is a stronger result than a failing test — record it as such.
- **`EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls` goes red at Task 2 and stays red until Task 11.** It compares the README's generated table against the paths the code actually requests, so it fails the moment the first new endpoint ships and cannot pass again until the table is regenerated. Every per-task run below is filtered to the tests that task owns; a full-suite run between Task 2 and Task 11 is expected to show exactly that one failure and no other.
- **`EndpointCoverageTests.Argument` needs no new cases**, and that is worth checking rather than assuming: its `string` arm ends in `_ => "AAPL"`, its `int` arm returns 5 for `limit` and 0 for `page`, and it supplies `new LocalDate(2026, 1, 2)` for every `LocalDate`. All of that is valid for the twelve new methods — `from` equals `to`, which is not backwards — and that harness only records which path went out, so a meaningless-but-valid value is harmless. The live sweep is the harness where a meaningless value *does* harm, and Task 10 is where that is fixed.
- Work happens on a branch off `master`. `master` carries a ruleset requiring a pull request and the `.NET — build + test` check, so the path is branch → PR → green → merge. Suggested branch name: `feat/sec-filings-coverage`.

## File Structure

**Create:**
- `src/FmpDotNet/DateRange.cs` — `internal static class DateRange`, the one backwards-range guard (Task 4)
- `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs` — the new facade, 10 methods over 9 paths
- `src/FmpDotNet/Models/IndustryClassification.cs` — `IndustryClassification` and `SicCodeEntry`, the two SIC-vocabulary shapes
- `src/FmpDotNet/Models/SecFiling.cs` — `SecFiling`
- `src/FmpDotNet/Models/SecProfile.cs` — `SecProfile`
- `tests/FmpDotNet.Tests/IndustryClassificationTests.cs` — the converter, the shared record, and the three facades' classification methods
- `tests/FmpDotNet.Tests/SecFilingsTests.cs` — the two feeds and the three search paths
- `tests/FmpDotNet.Tests/SecProfileTests.cs` — `sec-profile` and the company-search paths
- `tests/FmpDotNet.Tests/DateRangeTests.cs` — the promoted guard
- 13 fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modify:**
- `src/FmpDotNet/Serialization/NodaConverters.cs` — +`NullableDateAtMidnightJsonConverter`, +`BusinessAddressJsonConverter`
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +4 entries
- `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs` — +3 methods, +1 constant
- `src/FmpDotNet/Endpoints/SearchEndpoints.cs` — +1 method
- `src/FmpDotNet/Endpoints/ChartEndpoints.cs` — private `ThrowIfBackwards` deleted, calls redirected (Task 4)
- `src/FmpDotNet/Endpoints/CompanyEndpoints.cs` — private `ThrowIfBackwards` deleted, call redirected (Task 4)
- `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs` — inline guard replaced by the call (Task 4)
- `src/FmpDotNet/FmpClient.cs` — +`SecFilings` property
- `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` — +1 registration
- `tests/FmpDotNet.Tests/AddFmpTests.cs` — +`SecFilings` assertion, and the four groups the test silently omits
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — +4 constants
- `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()` name dispatch for `company`, `formType`, `sicCode` and for `from`; `Observation` gains `Rows`
- `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` — +2 keyless guards
- `tests/FmpDotNet.SmokeTests/OrdinaryEndpointShapeTests.cs` — +1 live tripwire
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded live
- `README.md` — regenerated coverage table, and the stale prose above it

---

## Deviations from the spec, decided while planning

Seven. Each is a ruling made against the spec text, with the evidence that forced it. Everything not listed here follows the spec as written.

**1. `GetProfileAsync` and `GetProfileByCikAsync` return `SecProfile?`, not `IReadOnlyList<SecProfile>`.** The spec's signature block writes both as lists. That contradicts the house convention stated in the same spec's ancestors and shipped in the adjacent facade: `CompanyEndpoints.GetProfileAsync` and `GetProfileByCikAsync` both return `CompanyProfile?`, unwrapping FMP's single-element array. `sec-profile` was measured returning exactly one row for all six symbols sampled, and one row for both the padded and unpadded CIK. A caller comparing `fmp.Company.GetProfileAsync("AAPL")` with `fmp.SecFilings.GetProfileAsync("AAPL")` would otherwise get two different shapes for the same question. Cost if wrong: if FMP ever returns two rows here, the second is dropped silently — the same exposure `Company.GetProfileAsync` already carries, and the weekly smoke sweep records the row count.

**2. A `limit` above either cap is rejected, not passed through.** The spec says `MaxSecFilingPageSize` is a ceiling FMP "clamps silently", and says of `MaxIndustryClassificationPageSize` that "a `limit` above the cap is not an error". Both sentences describe **FMP's** behaviour, not the SDK's, and all three shipped page-size caps — `MaxDelistedPageSize`, `MaxCikListPageSize`, `MaxMergerAcquisitionPageSize` — say exactly the same thing about FMP and then throw `ArgumentOutOfRangeException`. The reasoning is strongest on the filing feeds, which paginate properly: a caller who asks for 5,000 and steps `page` by 5,000 reads a fifth of the archive and is told nothing. Ruling: both constants reject. Cost if wrong: a caller who genuinely wants FMP's clamp has to write `Math.Min` — visible at their call site, which is the point.

**3. The backwards-range guard is promoted to a shared helper, and `SecFilingsEndpoints` does not get its own copy.** The spec rules that `SecFilingsEndpoints` gets a private copy "rather than the two sharing an extracted helper… two call sites is where extraction is still premature. A third occurrence is the point to promote it." The spec counted two. There are **three**: `ChartEndpoints.ThrowIfBackwards`, `CompanyEndpoints.ThrowIfBackwards`, and an unextracted inline copy inside `EconomicsEndpoints.GetEconomicCalendarAsync` — all three throwing `ArgumentOutOfRangeException(nameof(to), to, "'to' must not be earlier than 'from' (…)")`, character for character. `SecFilingsEndpoints` would be the fourth. The spec's own promotion trigger has therefore already fired, and Task 4 acts on it. Cost if wrong: three call sites reach one line of code instead of three copies of it; no test asserts on the message or the `ParamName`, so the change is behaviour-preserving.

**4. Thirteen fixtures, not fifteen.** The spec derives twelve path fixtures plus three named for the traps they pin. Two of those three traps are already carried, verbatim, by a path fixture measured the same day, and a hand-built duplicate would be weaker evidence rather than stronger:
   - *A filing row whose `FilingDate` falls past the requested `to`* — `sec-filings-8k.head.json` rows 1 and 2 are `SUNE` and `CGBDL`, both `filingDate` `2024-03-04` against `acceptedDate` `2024-03-01 22:47:48` and `22:45:43`. That is the after-hours mechanism itself, in real rows.
   - *A search-path row with `hasFinancials` absent* — all five rows of all three `sec-filings-search.*` fixtures omit the field. That is the trap, unedited.
   The third trap, the `XI'AN` address, is a converter-level trap and is asserted from an inline JSON literal carrying the measured string verbatim, the way `DelistedCompaniesTests.A_missing_date_reads_as_null_rather_than_costing_the_whole_row` already does. The thirteenth fixture is `sec-profile.TSM.json`, which earns its place: it is the only capture in which `isAdr` is `true`, so without it all four booleans on `SecProfile` are `false` in every fixture and a converter that always answered `false` would pass.

**5. `industry-classification-search` sends the bracketed address, so three paths never do, not four.** The spec's converter section says the pass-through branch "is also what makes it safe on the four paths that never send the bracketed form". Measured 2026-08-28: `industry-classification-search?symbol=AAPL` and `?sicCode=3571` both return `"['ONE APPLE PARK WAY', 'CUPERTINO CA 95014']"`. So of the five `IndustryClassification` paths, **two** send the bracketed form (`all-industry-classification`, `industry-classification-search`) and **three** send the joined one (the `sec-filings-company-search/*` trio). The converter's behaviour is unchanged; only the count in its documentation is.

**6. The live row-count tripwire goes in `OrdinaryEndpointShapeTests`, not `SweepCoverageTests`.** The spec puts it in `SweepCoverageTests`. That class's own documentation says it holds "the only tests in this project that are not gated on `FMP_API_KEY`… pure reflection over the SDK's own types, so they run on every push, cost nothing". A row-count assertion needs a live call and cannot live there. It goes beside the other `[LiveFact]`s, reading the count off the sweep the fixture has already run — so it costs no extra request. `SweepCoverageTests` gains two guards that genuinely are keyless and request-free, pinning the argument values the sweep needs (Task 10).

**7. The sweep's date range is widened from one day to ninety, because a one-day range makes three of the twelve endpoints record nothing.** `Probe.Argument` dispatches `LocalDate` on type alone, so `from` and `to` both become `LiveApi.SettledWeekday` — a range of one day. Measured 2026-08-28: `sec-filings-search/symbol?symbol=AAPL&from=2026-08-21&to=2026-08-21` returns **0 rows**, while the same call over 2026-05-30..2026-08-28 returns **7**. A zero-row answer records `outcome empty` with no properties, and every later run agrees with it — the exact silent-green failure `LiveApi.Exchange` and `LiveApi.Cik` were written to prevent, arriving through the date synthesiser instead. Task 10 gives `from` its own arm. This also widens the window for five already-shipped endpoints (`Chart.GetDailyAsync` ×4, `Chart.GetIntradayAsync`, `Company.GetHistoricalMarketCapAsync`, `Calendar.GetEarningsCalendarAsync`, `Economics.GetCalendarAsync`), all of which record `outcome rows` today and will continue to; the baseline is re-recorded in Task 11 regardless.

## A finding this plan got wrong, and what replaced it

This section originally read: "`CalendarEndpoints.GetEarningsCalendarAsync` has **no backwards-range guard**, while the three other date-ranged endpoint groups all do… It is not taken here: it changes the behaviour of a shipped public method from 'returns whatever FMP answers' to 'throws', which is a scope decision for the maintainer."

**That was false, and Task 4's implementer caught it.** `CalendarEndpoints.cs:139` carries a guard — a *fourth* copy, and the only one worded differently:

| | condition | `ParamName` | message |
|---|---|---|---|
| Chart, Company, Economics | `to < from` | `nameof(to)` | `'to' must not be earlier than 'from' ({start:uuuu-MM-dd}).` |
| **Calendar** | `to < from` | `nameof(to)` | `The range end must not precede its start; 'from' was {from:uuuu-MM-dd}.` |

So the codebase held **four** copies of one rule, one of which had already drifted in wording — which is a sharper argument for promoting the guard than the three-copy count the spec reasoned from, not a weaker one.

The recorded objection is void: the method already throws, so redirecting it changes no caller from "gets rows" to "gets an exception". What remains is a message string, on a method whose exception type, parameter name and trigger condition are all unchanged, and which no test pins — grepped across the whole test tree; `CalendarEndpointsTests.cs:495` asserts the exception type only. Leaving one stray copy beside a freshly-promoted shared helper is worse than the state before it, because the stray then looks deliberate.

**Ruling: Task 4 redirects `CalendarEndpoints.GetEarningsCalendarAsync` too, and `DateRange`'s own documentation says four rather than three.** Cost if wrong: a caller who string-matches that one exception message breaks — which no message contract supports, and this SDK has not been published, so no such caller exists.

---

### Task 1: `BusinessAddressJsonConverter` and the shared classification record

The trap that decides the shape of five paths across three facades: one address field, two encodings, and a bracketed form that cannot be parsed as the list it pretends to be.

**Files:**
- Create: `src/FmpDotNet/Models/IndustryClassification.cs`
- Create: `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/all-industry-classification.head.json`
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: `Binding.Fixture`, `Binding.Unbound<T>`, `FmpJsonContext` (internal, visible to `FmpDotNet.Tests` via `InternalsVisibleTo`).
- Produces: `FmpDotNet.Serialization.BusinessAddressJsonConverter` with `internal static string? Normalise(string?)`; `FmpDotNet.Models.IndustryClassification` (`Symbol`, `Name`, `Cik`, `SicCode`, `IndustryTitle`, `BusinessAddress`, `PhoneNumber`, all `string?`); `FmpDotNet.Models.SicCodeEntry` (`Office`, `SicCode`, `IndustryTitle`, all `string?`); `FmpJsonContext.Default.ListIndustryClassification` and `.ListSicCodeEntry`.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/all-industry-classification.head.json` — the first five rows of `stable/all-industry-classification?page=0&limit=5`, captured 2026-08-28, verbatim:

```json
[
  {
    "symbol": "0Q16.L",
    "name": "BANK OF AMERICA CORP /DE/",
    "cik": "0000070858",
    "sicCode": "6021",
    "industryTitle": "NATIONAL COMMERCIAL BANKS",
    "businessAddress": "['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']",
    "phoneNumber": "7043868486"
  },
  {
    "symbol": "A",
    "name": "AGILENT TECHNOLOGIES, INC.",
    "cik": "0001090872",
    "sicCode": "3826",
    "industryTitle": "LABORATORY ANALYTICAL INSTRUMENTS",
    "businessAddress": "['5301 STEVENS CREEK BLVD', 'SANTA CLARA CA 95051']",
    "phoneNumber": "(408) 345-8886"
  },
  {
    "symbol": "AA",
    "name": "Alcoa Corp",
    "cik": "0001675149",
    "sicCode": "3334",
    "industryTitle": "PRIMARY PRODUCTION OF  ALUMINUM",
    "businessAddress": "['201 ISABELLA STREET', 'PITTSBURGH PA 15212']",
    "phoneNumber": "412-315-2900"
  },
  {
    "symbol": "AAAU",
    "name": "Goldman Sachs Physical Gold ETF",
    "cik": "0001708646",
    "sicCode": "6221",
    "industryTitle": "COMMODITY CONTRACTS BROKERS & DEALERS",
    "businessAddress": "['240 GREENWICH STREET', '8TH FLOOR', 'NEW YORK NY 10286']",
    "phoneNumber": "718-315-4591"
  },
  {
    "symbol": "AAC",
    "name": "Ares Acquisition Corp",
    "cik": "0001829432",
    "sicCode": "3443",
    "industryTitle": "FABRICATED PLATE WORK (BOILER SHOPS)",
    "businessAddress": "['C/O ARES MANAGEMENT LLC', 'NEW YORK NY 10167']",
    "phoneNumber": "310-201-4100"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`. This file grows over Tasks 1–3; this step writes only the converter and binding sections.

```csharp
using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The seven-field company row FMP serves from five paths across three facades, and the one field on
/// it that arrives in two encodings.
///
/// <para>Measured 2026-08-28: for CIK <c>0000070858</c> (Bank of America), <c>all-industry-classification</c> and
/// <c>sec-filings-company-search/cik</c> returned byte-identical values for <c>symbol</c>, <c>name</c>,
/// <c>cik</c>, <c>sicCode</c>, <c>industryTitle</c> and <c>phoneNumber</c>. Only <c>businessAddress</c> differed,
/// and only in encoding — which is what makes one record right for all five rather than five records that happen
/// to share field names.</para></summary>
public class IndustryClassificationTests
{
    // ---- the address converter ---------------------------------------------------------------------------------

    [Fact]
    public void The_bracketed_encoding_becomes_the_joined_one()
    {
        // FMP publishes the normalisation target itself: measured 2026-08-28 on five randomly sampled CIKs,
        // `", ".join(parts)` of the bracketed value matched the sibling path's plain string exactly, 5 of 5.
        Assert.Equal(
            "BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255",
            BusinessAddressJsonConverter.Normalise(
                "['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']"));
    }

    [Fact]
    public void An_apostrophe_inside_an_element_survives_because_the_transform_is_textual()
    {
        // The row that rules out parsing. Of 1,000 bracketed values sampled 2026-08-28, 999 parse as a Python
        // literal and this one does not: XI'AN carries an unescaped apostrophe inside a single-quoted repr, so
        // the string was built by naive formatting rather than by a serialiser. Every Xi'an, O'Brien and L'Oreal
        // reproduces it, so this is a class of row and not one bad row. Splitting on "', '" is unbothered by it,
        // because the apostrophe is not followed by a comma and a space.
        Assert.Equal(
            "NO. 65, LN, 114, XISHI RD., XI'AN VIL., TAICHUNG CITY  ",
            BusinessAddressJsonConverter.Normalise(
                "['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"));
    }

    [Fact]
    public void A_plain_string_is_returned_untouched()
    {
        // Three of the five paths never send the bracketed form. Measured 2026-08-28:
        // sec-filings-company-search/name answered 0 bracketed values in 976 rows.
        Assert.Equal(
            "ONE APPLE PARK WAY, CUPERTINO CA 95014",
            BusinessAddressJsonConverter.Normalise("ONE APPLE PARK WAY, CUPERTINO CA 95014"));
    }

    [Fact]
    public void A_null_stays_null_and_an_unrecognised_shape_passes_through()
    {
        // The converter never throws and never drops a value. Anything that is not bracketed at both ends is
        // returned as sent, which is what makes it safe on the three paths that never bracket.
        Assert.Null(BusinessAddressJsonConverter.Normalise(null));
        Assert.Equal("", BusinessAddressJsonConverter.Normalise(""));
        Assert.Equal("[]", BusinessAddressJsonConverter.Normalise("[]"));
        Assert.Equal("['unterminated", BusinessAddressJsonConverter.Normalise("['unterminated"));
    }

    [Fact]
    public void A_single_element_address_loses_its_brackets_and_nothing_else()
    {
        // One of the 1,000 sampled values had a single element; 737 had two, 229 three, 27 four and 5 five.
        Assert.Equal("PO BOX 1", BusinessAddressJsonConverter.Normalise("['PO BOX 1']"));
    }

    // ---- binding -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_captured_row_binds_every_one_of_its_seven_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0Q16.L", rows[0].Symbol);
        Assert.Equal("BANK OF AMERICA CORP /DE/", rows[0].Name);
        Assert.Equal("0000070858", rows[0].Cik);
        Assert.Equal("6021", rows[0].SicCode);
        Assert.Equal("NATIONAL COMMERCIAL BANKS", rows[0].IndustryTitle);
        Assert.Equal("7043868486", rows[0].PhoneNumber);
    }

    [Fact]
    public void The_converter_is_wired_to_the_property_and_not_merely_written()
    {
        // The failure this guards is silent: without the [JsonConverter] attribute the property still binds, and
        // still carries an address — the bracketed one — so five paths would disagree about what the same field
        // means and nothing would throw.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.Equal("BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255", rows[0].BusinessAddress);
        Assert.Equal("240 GREENWICH STREET, 8TH FLOOR, NEW YORK NY 10286", rows[3].BusinessAddress);
        Assert.DoesNotContain(rows, r => r.BusinessAddress!.StartsWith("['", StringComparison.Ordinal));
    }

    [Fact]
    public void The_cik_keeps_its_leading_zeros()
    {
        // Ten characters, zero-padded. An integer type would destroy the padding that makes the value match
        // EDGAR, and there is no round trip back to it: 320193 could pad to any width.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.All(rows, r => Assert.Equal(10, r.Cik!.Length));
        Assert.StartsWith("0000", rows[0].Cik);
    }
}
```

- [ ] **Step 3: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: build error — `BusinessAddressJsonConverter`, `IndustryClassification` and `ListIndustryClassification` do not exist. A compile failure is the strongest form of red here.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, after `TolerantDecimalJsonConverter`:

```csharp
/// <summary>Normalises <c>businessAddress</c> so that one property means the same thing on all five paths that
/// send it.
///
/// <para><b>Two encodings for one field, measured 2026-08-28.</b> <c>all-industry-classification</c> and
/// <c>industry-classification-search</c> send a stringified Python list —
/// <c>"['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']"</c>, 1,000 of 1,000 rows sampled — while the
/// three <c>sec-filings-company-search/*</c> paths send the same address for the same CIK as
/// <c>"BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255"</c>, 0 of 976 rows bracketed. The joined form is
/// FMP's own: <c>", ".join(parts)</c> of the bracketed value reproduced the sibling path's string exactly on
/// five of five randomly sampled CIKs, so this converter adopts a target FMP publishes rather than inventing
/// one.</para>
///
/// <para><b>The transform is textual, not a parse, and the difference is load-bearing.</b> Of those 1,000
/// values, 999 parse as a Python literal and one does not:
/// <c>"['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"</c> (AGCC, CIK 0002060016), where
/// <c>XI'AN</c> carries an unescaped apostrophe inside a single-quoted repr. The string was built by naive
/// formatting rather than by a serialiser, so every apostrophe in an address — Xi'an, O'Brien, L'Oreal —
/// reproduces the fault. Stripping the brackets and replacing <c>', '</c> handles that row correctly, because
/// the apostrophe is not followed by a comma and a space. A real parse fails on it.</para>
///
/// <para><b>One direction only.</b> Splitting the joined form back into parts would be lossy: nineteen of the
/// 1,000 sampled values carry a comma or a quote inside an element.</para>
///
/// <para>Anything that is not bracketed at both ends is returned exactly as sent. The converter never throws and
/// never drops a value, which is also what makes it safe on the three paths that never bracket. Whitespace is
/// not trimmed — <c>'TAICHUNG CITY  '</c> keeps its trailing spaces, because FMP sent them and trimming would be
/// a second unmeasured transform riding on this one.</para></summary>
public sealed class BusinessAddressJsonConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Normalise(reader.GetString());

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);

    /// <summary>The transform itself, exposed so it can be tested without a serialiser around it.</summary>
    internal static string? Normalise(string? raw)
    {
        if (raw is null) return null;
        if (!raw.StartsWith("['", StringComparison.Ordinal) || !raw.EndsWith("']", StringComparison.Ordinal))
            return raw;

        return raw[2..^2].Replace("', '", ", ", StringComparison.Ordinal);
    }
}
```

- [ ] **Step 5: Write the models**

`src/FmpDotNet/Models/IndustryClassification.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>A registrant and its SIC classification, from any of five paths across three facades:
/// <c>stable/all-industry-classification</c>, <c>stable/industry-classification-search</c>, and all three of
/// <c>stable/sec-filings-company-search/{symbol,cik,name}</c>.
///
/// <para><b>One record rather than five, because the five are the same data and not merely the same field
/// names.</b> Measured 2026-08-28: for CIK <c>0000070858</c>, <c>all-industry-classification</c> and
/// <c>sec-filings-company-search/cik</c> returned byte-identical values for all six non-address fields. Only
/// <see cref="BusinessAddress"/> differed, and only in encoding — see
/// <see cref="BusinessAddressJsonConverter"/>, which makes that difference invisible here.</para></summary>
public sealed record IndustryClassification
{
    /// <summary>The ticker, where the registrant has one.
    ///
    /// <para><b>The literal four-character string <c>"None"</c> stands in for "no ticker" on some rows</b>, rather
    /// than a JSON null — measured 2026-08-28 on <c>industry-classification-search?sicCode=3571</c>, where three
    /// of five rows read <c>"None"</c>, and on <c>sec-filings-company-search/name?company=Apple</c>, where four of
    /// five do. It is the same naive-formatting fault that produces the bracketed address: a Python <c>None</c>
    /// rendered into a string field. The SDK passes it through rather than translating it, because translating it
    /// would be asserting that FMP will never send a real security called <c>None</c>, and because a caller who
    /// filters on it can see what they are filtering.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The registrant's name as EDGAR spells it — <c>"BANK OF AMERICA CORP /DE/"</c>. Upper-cased on
    /// most rows and mixed-case on others; FMP passes EDGAR through and so does this.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters — <c>"0000320193"</c>.
    ///
    /// <para><see cref="string"/> rather than an integer type: the padding is what makes the value match EDGAR,
    /// and there is no round trip back to it once it is gone.</para></summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The Standard Industrial Classification code — <c>"6021"</c>.
    ///
    /// <para><b><see cref="string"/>, and the two endpoints that serve SIC codes disagree about their width.</b>
    /// Measured 2026-08-28: this path sends four characters, while
    /// <c>stable/standard-industrial-classification-list</c> sends <c>"100"</c> for SIC 0100
    /// ("AGRICULTURAL PRODUCTION-CROPS") — the same code space with the leading zero stripped. The SDK preserves
    /// what each endpoint sent and normalises neither, because normalising would mean choosing one of two
    /// spellings FMP itself does not reconcile. Blank on rows FMP has not classified — measured on four of five
    /// <c>sec-filings-company-search/name</c> rows.</para></summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The SIC code's label — <c>"NATIONAL COMMERCIAL BANKS"</c>. Blank wherever
    /// <see cref="SicCode"/> is.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The registrant's business address as one line — <c>"ONE APPLE PARK WAY, CUPERTINO CA 95014"</c>.
    ///
    /// <para><b>Normalised on the way in, because FMP sends this field in two encodings.</b> See
    /// <see cref="BusinessAddressJsonConverter"/> for the measurement, the target, and why the transform is
    /// textual rather than a parse. Not split into parts: nineteen of 1,000 sampled values carry a comma inside
    /// an element, so a structured address type would have to guess.</para></summary>
    [JsonPropertyName("businessAddress")]
    [JsonConverter(typeof(BusinessAddressJsonConverter))]
    public string? BusinessAddress { get; init; }

    /// <summary>The registrant's telephone number, in whatever form EDGAR holds it — <c>"7043868486"</c> and
    /// <c>"(408) 345-8886"</c> both appear in the first five rows. Unnormalised on purpose.</summary>
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }
}

/// <summary>One row of <c>stable/standard-industrial-classification-list</c> — the SIC vocabulary itself, and
/// the SEC review office that owns each code.
///
/// <para>Named for the <see cref="CikEntry"/> precedent: a reference-list row that is an entry in a vocabulary
/// rather than a thing in the market. Measured 2026-08-28, the endpoint answers all <b>444</b> rows for every
/// combination of <c>page</c> and <c>limit</c> tried — see
/// <c>DirectoryEndpoints.GetSicCodesAsync</c>, added in a later task on this same slice.</para></summary>
/// <remarks>A <c>&lt;c&gt;</c> span rather than a <c>&lt;see cref&gt;</c> because the method does not exist yet:
/// an unresolved cref is CS1574, and <c>TreatWarningsAsErrors</c> makes that a build error. Task 2 promotes it
/// to a real cref once <c>GetSicCodesAsync</c> is there to point at.</remarks>
public sealed record SicCodeEntry
{
    /// <summary>The SEC review office that handles filings under this code — <c>"Office of Life Sciences"</c>.
    /// Present on every one of the 444 rows measured 2026-08-28.</summary>
    [JsonPropertyName("office")] public string? Office { get; init; }

    /// <summary>The SIC code, <b>with any leading zero stripped</b> — <c>"100"</c> is SIC 0100.
    ///
    /// <para>That is not this SDK's doing and is not corrected here: <see cref="IndustryClassification.SicCode"/>
    /// carries the same code space four characters wide on a different endpoint, measured the same day. A caller
    /// joining the two must pad, and this documentation is where they find that out rather than in a lookup that
    /// silently matches nothing.</para></summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The code's label — <c>"AGRICULTURAL PRODUCTION-CROPS"</c>.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }
}
```

- [ ] **Step 6: Register both models**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, insert immediately after the `[JsonSerializable(typeof(List<IntradayBar>))]` line and before the comment block that introduces the five CSV-built models — the four SEC Filings entries go together, in alphabetical order, and must not land inside that comment block:

```csharp
[JsonSerializable(typeof(List<IndustryClassification>))]
[JsonSerializable(typeof(List<SecFiling>))]
[JsonSerializable(typeof(List<SecProfile>))]
[JsonSerializable(typeof(List<SicCodeEntry>))]
```

**`SecFiling` and `SecProfile` do not exist yet** — add only the two lines for `IndustryClassification` and `SicCodeEntry` in this task, and the other two in Tasks 5 and 6 where those records are written. Adding all four now will not compile.

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: PASS. Eight `[Fact]`s — five for the converter, two for binding, one for the CIK padding.

- [ ] **Step 8: Mutation-check the converter**

Change `Replace("', '", ", ")` to `Replace("','", ", ")` and re-run.
Expected: exactly three fail — `The_bracketed_encoding_becomes_the_joined_one`, `An_apostrophe_inside_an_element_survives_because_the_transform_is_textual` and `The_converter_is_wired_to_the_property_and_not_merely_written`. `A_single_element_address_loses_its_brackets_and_nothing_else` passes under both implementations and that is correct, not a gap: a one-element address has no internal separator for the mutation to corrupt, so that test discriminates bracket-stripping and nothing else. Restore.

Then delete the `[JsonConverter(typeof(BusinessAddressJsonConverter))]` attribute and re-run.
Expected: only `The_converter_is_wired_to_the_property_and_not_merely_written` fails — which is the point of that test: nothing else notices. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/IndustryClassification.cs \
        src/FmpDotNet/Serialization/NodaConverters.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/IndustryClassificationTests.cs \
        tests/FmpDotNet.Tests/Fixtures/all-industry-classification.head.json
git commit -m "feat: normalise businessAddress and model the shared SIC classification row (#30)"
```

---

### Task 2: `fmp.Directory` — the classification universe and the SIC vocabulary

Two paths, three methods, and an endpoint whose pagination is broken in a way that makes the broken behaviour the only route to 96% of the data.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/standard-industrial-classification-list.head.json`

**Interfaces:**
- Consumes: `IndustryClassification`, `SicCodeEntry`, `FmpJsonContext.Default.ListIndustryClassification`, `.ListSicCodeEntry` (Task 1); `FmpTransport.GetListAsync`, `FmpRequest.With` (existing).
- Produces: `DirectoryEndpoints.MaxIndustryClassificationPageSize` (`public const int` = 1000); `GetIndustryClassificationsAsync(int limit = 100, CancellationToken ct = default)` → `Task<IReadOnlyList<IndustryClassification>>`; `GetAllIndustryClassificationsAsync(CancellationToken ct = default)` → `Task<IReadOnlyList<IndustryClassification>>`; `GetSicCodesAsync(CancellationToken ct = default)` → `Task<IReadOnlyList<SicCodeEntry>>`.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/standard-industrial-classification-list.head.json` — the first five rows of `stable/standard-industrial-classification-list`, captured 2026-08-28, verbatim:

```json
[
  {
    "office": "Office of Life Sciences",
    "sicCode": "100",
    "industryTitle": "AGRICULTURAL PRODUCTION-CROPS"
  },
  {
    "office": "Office of Life Sciences",
    "sicCode": "200",
    "industryTitle": "AGRICULTURAL PROD-LIVESTOCK & ANIMAL SPECIALTIES"
  },
  {
    "office": "Office of Life Sciences",
    "sicCode": "700",
    "industryTitle": "AGRICULTURAL SERVICES"
  },
  {
    "office": "Office of Life Sciences",
    "sicCode": "800",
    "industryTitle": "FORESTRY"
  },
  {
    "office": "Office of Life Sciences",
    "sicCode": "900",
    "industryTitle": "FISHING, HUNTING AND TRAPPING"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`, inside the class, after the binding section. Add these usings at the top of the file if they are not already there: `using Microsoft.Extensions.Options;` and `using FmpDotNet.Endpoints;`.

```csharp
    // ---- fmp.Directory -----------------------------------------------------------------------------------------

    private static (DirectoryEndpoints Endpoints, StubHandler Handler) BuildDirectory(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DirectoryEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task One_page_of_classifications_sends_page_zero_and_the_limit()
    {
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("all-industry-classification.head.json")));

        var rows = await endpoints.GetIndustryClassificationsAsync(limit: 5);

        Assert.Equal(5, rows.Count);
        var uri = handler.Requests.Single();
        Assert.Equal("/stable/all-industry-classification", uri.AbsolutePath);
        Assert.Contains("page=0", uri.Query);
        Assert.Contains("limit=5", uri.Query);
    }

    [Fact]
    public async Task The_whole_universe_is_reached_by_sending_page_one_and_no_limit()
    {
        // The anomaly this method exists for, measured 2026-08-28. page=0 honours `limit` but caps at 1,000 rows,
        // and the dataset is 25,952 — so rows 1,001 onward are reachable only through page>=1, which ignores
        // `limit` entirely and answers the whole universe. page=1, page=2, page=1&limit=10 and page=1 with no
        // limit all returned the same 25,952 rows and the same 7,288,535 bytes, byte-identical.
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("all-industry-classification.head.json")));

        await endpoints.GetAllIndustryClassificationsAsync();

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/all-industry-classification", uri.AbsolutePath);
        Assert.Contains("page=1", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(5000)]
    [InlineData(30000)]
    public async Task A_limit_above_the_measured_cap_is_refused_rather_than_clamped_by_fmp(int limit)
    {
        // Measured 2026-08-28: limit=1000, 5000, 26000 and 30000 all answered exactly 1,000 rows on page 0, with
        // HTTP 200 and nothing in the body to say the request had been trimmed. A caller who asked for 5,000 and
        // believed they had it would be short by four fifths and never told.
        var (endpoints, handler) = BuildDirectory(StubHandler.Json("[]"));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustryClassificationsAsync(limit));

        Assert.Equal("limit", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_non_positive_limit_is_refused(int limit)
    {
        var (endpoints, handler) = BuildDirectory(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustryClassificationsAsync(limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_classification_cap_is_the_measured_one()
    {
        Assert.Equal(1000, DirectoryEndpoints.MaxIndustryClassificationPageSize);
    }

    [Fact]
    public async Task The_sic_list_takes_no_parameters_at_all()
    {
        // Measured 2026-08-28: the endpoint answered all 444 rows for every combination of page and limit tried,
        // so a `limit` parameter would be a control that controls nothing.
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("standard-industrial-classification-list.head.json")));

        var rows = await endpoints.GetSicCodesAsync();

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Office of Life Sciences", rows[0].Office);
        Assert.Equal("100", rows[0].SicCode);
        Assert.Equal("AGRICULTURAL PRODUCTION-CROPS", rows[0].IndustryTitle);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/standard-industrial-classification-list", uri.AbsolutePath);
        Assert.Equal("?apikey=k", uri.Query);
    }

    [Fact]
    public async Task The_sic_list_strips_a_leading_zero_that_the_classification_paths_keep()
    {
        // The join trap, pinned. SIC 0100 is "AGRICULTURAL PRODUCTION-CROPS"; this endpoint calls it "100" while
        // all-industry-classification carries four-character codes. A caller joining the two on string equality
        // silently matches nothing for every code below 1000, and nothing in either payload says why.
        var (endpoints, _) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("standard-industrial-classification-list.head.json")));

        var rows = await endpoints.GetSicCodesAsync();

        Assert.All(rows, r => Assert.Equal(3, r.SicCode!.Length));
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: build error — `DirectoryEndpoints` has no `GetIndustryClassificationsAsync`, `GetAllIndustryClassificationsAsync`, `GetSicCodesAsync` or `MaxIndustryClassificationPageSize`.

- [ ] **Step 4: Write the three methods**

In `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`, insert immediately after `StreamCikListAsync` and **before** the doc comment on the private `Symbols<T>` helper:

```csharp
    /// <summary>The largest page <c>stable/all-industry-classification</c> will serve, measured rather than
    /// documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="CompanyEndpoints.MaxDelistedPageSize"/>: measured 2026-08-28, <c>limit=1000</c>,
    /// <c>limit=5000</c>, <c>limit=26000</c> and <c>limit=30000</c> all answered exactly 1,000 rows with HTTP 200
    /// and nothing in the body to say the request had been trimmed.
    /// <see cref="GetIndustryClassificationsAsync(int, CancellationToken)"/> therefore rejects a larger
    /// <c>limit</c> rather than passing it on to be clamped — a caller who asks for 5,000 and gets 1,000 has no
    /// way to tell which happened.</para></summary>
    public const int MaxIndustryClassificationPageSize = 1000;

    /// <summary>One capped page of <c>stable/all-industry-classification</c> — every SEC registrant FMP knows,
    /// with its SIC code and business address.
    ///
    /// <para><b>There is no <c>page</c> parameter, and that is not an oversight.</b> Measured 2026-08-28: page 0
    /// honours <c>limit</c> and caps at 1,000 rows, while <b>every non-zero page answers the entire 25,952-row
    /// universe</b> — byte-identical across page numbers, ignoring <c>limit</c> entirely. There is no page index
    /// that advances through the data, so exposing one would be exposing a control that does not control
    /// anything. The two behaviours FMP actually has are modelled as two methods: this one, and
    /// <see cref="GetAllIndustryClassificationsAsync(CancellationToken)"/>.</para>
    ///
    /// <para>This method reaches the first 1,000 rows and no further. If you need the rest, you need the other
    /// one — there is no walk that gets there from here.</para></summary>
    /// <param name="limit">Rows to ask for, 1 to <see cref="MaxIndustryClassificationPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Up to <paramref name="limit"/> rows in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to
    /// <see cref="MaxIndustryClassificationPageSize"/> — see that constant for why the upper bound is enforced
    /// here rather than silently clamped upstream.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndustryClassification>> GetIndustryClassificationsAsync(
        int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxIndustryClassificationPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/all-industry-classification").With("page", 0).With("limit", limit),
            FmpJsonContext.Default.ListIndustryClassification, ct);
    }

    /// <summary>Every row of <c>stable/all-industry-classification</c> in one response — 25,952 registrants,
    /// about 7.3 MB, measured 2026-08-28.
    ///
    /// <para><b>This method depends on a bug, deliberately, and this paragraph is the whole justification.</b>
    /// The endpoint's <c>page</c> parameter does not paginate: page 0 caps at 1,000 rows however large a
    /// <c>limit</c> is sent, and any non-zero page returns the complete dataset ignoring <c>limit</c>.
    /// <c>page=1</c>, <c>page=2</c>, <c>page=1&amp;limit=10</c> and <c>page=1</c> with no limit each answered the
    /// same 25,952 rows and the same 7,288,535 bytes. Since the data is 25,952 rows and the only paged route
    /// stops at 1,000, the anomaly is the <b>only</b> way to reach rows 1,001 onward. The choice is between
    /// depending on it and leaving 96% of the dataset unreachable.</para>
    ///
    /// <para><b>If FMP fixes it, this method silently returns 5 rows instead of 25,952</b> — the row shape would
    /// not change, so nothing about the response would look wrong. The smoke suite carries a row-count assertion
    /// for exactly that, because it is the only thing that can catch it.</para>
    ///
    /// <para>One request, but a large one. <see cref="GetIndustryClassificationsAsync(int, CancellationToken)"/>
    /// is there for a caller who only wants a taste of the shape.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every classified registrant FMP knows, in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> GetAllIndustryClassificationsAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/all-industry-classification").With("page", 1),
            FmpJsonContext.Default.ListIndustryClassification, ct);

    /// <summary>The SIC vocabulary — <c>stable/standard-industrial-classification-list</c>, a fixed 444 rows
    /// measured 2026-08-28.
    ///
    /// <para><b>No parameters, because the endpoint has none that work.</b> It answered all 444 rows for every
    /// combination of <c>page</c> and <c>limit</c> tried. A signature that accepted either would let a caller
    /// believe they had asked for five rows while holding 444.</para>
    ///
    /// <para>This is the authoritative spelling of the <c>sicCode</c> and <c>industryTitle</c> values that come
    /// back on <see cref="IndustryClassification"/> — with one catch that will silently break a join. See
    /// <see cref="SicCodeEntry.SicCode"/>: this endpoint strips a leading zero and the classification endpoints
    /// do not.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>All 444 SIC codes with their review offices. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SicCodeEntry>> GetSicCodesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/standard-industrial-classification-list"),
            FmpJsonContext.Default.ListSicCodeEntry, ct);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: PASS, including the two `[Theory]`s (five cases between them). Counts reported by the runner are cases, not methods.

- [ ] **Step 6: Restore the cref Task 1 could not compile**

`GetSicCodesAsync` now exists, so the placeholder Task 1 was forced to leave in
`src/FmpDotNet/Models/IndustryClassification.cs` can become a real reference. Replace

```csharp
/// combination of <c>page</c> and <c>limit</c> tried — see <c>DirectoryEndpoints.GetSicCodesAsync</c>, added in a
/// later task on this same slice.</para></summary>
/// <remarks>A <c>&lt;c&gt;</c> span rather than a <c>&lt;see cref&gt;</c> because the method does not exist yet:
/// an unresolved cref is CS1574, and <c>TreatWarningsAsErrors</c> makes that a build error. Task 2 promotes it
/// to a real cref once <c>GetSicCodesAsync</c> is there to point at.</remarks>
```

with

```csharp
/// combination of <c>page</c> and <c>limit</c> tried — see
/// <see cref="Endpoints.DirectoryEndpoints.GetSicCodesAsync(CancellationToken)"/>.</para></summary>
```

Both the `<remarks>` block and the "added in a later task" clause describe scaffolding rather than the
type, and neither survives. The compiler checks the restoration for free: an unresolved cref is CS1574,
which `TreatWarningsAsErrors` turns into a build error, so a green build IS the assertion here.

- [ ] **Step 7: Mutation-check the pagination decision**

Change `GetAllIndustryClassificationsAsync` to send `.With("page", 0)` and re-run.
Expected: `The_whole_universe_is_reached_by_sending_page_one_and_no_limit` fails on the `page=1` assertion. Restore.

Then add `.With("limit", MaxIndustryClassificationPageSize)` to `GetAllIndustryClassificationsAsync` and re-run.
Expected: the same test fails on `Assert.DoesNotContain("limit=", …)`. This is the mutation worth pinning: a `limit` on the anomaly path looks harmless and is not — it is what a maintainer would add if they mistook this for an ordinary paged endpoint. Restore.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Endpoints/DirectoryEndpoints.cs \
        tests/FmpDotNet.Tests/IndustryClassificationTests.cs \
        tests/FmpDotNet.Tests/Fixtures/standard-industrial-classification-list.head.json
git commit -m "feat: fmp.Directory reaches the classification universe and the SIC vocabulary (#30)"
```

---

### Task 3: `fmp.Search` — classification lookup by symbol, CIK or SIC code

One path, one method, and one guard that turns FMP's own error message into an exception raised before the call is spent.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/SearchEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/industry-classification-search.sic3571.json`

**Interfaces:**
- Consumes: `IndustryClassification`, `FmpJsonContext.Default.ListIndustryClassification` (Task 1).
- Produces: `SearchEndpoints.FindIndustryClassificationAsync(string? symbol = null, string? cik = null, string? sicCode = null, CancellationToken ct = default)` → `Task<IReadOnlyList<IndustryClassification>>`.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/industry-classification-search.sic3571.json` — the first five rows of `stable/industry-classification-search?sicCode=3571`, captured 2026-08-28, verbatim. Three of the five carry the literal string `"None"` in `symbol`; that is the wire value, not a placeholder:

```json
[
  {
    "symbol": "AAPL",
    "name": "APPLE INC.",
    "cik": "0000320193",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "['ONE APPLE PARK WAY', 'CUPERTINO CA 95014']",
    "phoneNumber": "(408) 996-1010"
  },
  {
    "symbol": "DELL",
    "name": "DELL TECHNOLOGIES INC.",
    "cik": "0001571996",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "['ONE DELL WAY', 'ROUND ROCK TX 78682']",
    "phoneNumber": "800-289-3355"
  },
  {
    "symbol": "None",
    "name": "GRAPHICS PROPERTIES HOLDINGS, INC.",
    "cik": "0000802301",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "['56 HARRISON STREET', 'NEW ROCHELLE NY 10801']",
    "phoneNumber": "914-235-1075"
  },
  {
    "symbol": "None",
    "name": "DELL INC",
    "cik": "0000826083",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "['ONE DELL WAY', 'ROUND ROCK TX 78682-2244']",
    "phoneNumber": "5127284737"
  },
  {
    "symbol": "None",
    "name": "XRS CORP",
    "cik": "0000854398",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "['965 PRAIRIE CENTER DRIVE', 'EDEN PRAIRIE MN 55344']",
    "phoneNumber": "952-707-5600"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/IndustryClassificationTests.cs`, inside the class:

```csharp
    // ---- fmp.Search --------------------------------------------------------------------------------------------

    private static (SearchEndpoints Endpoints, StubHandler Handler) BuildSearch(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SearchEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Classification_search_sends_only_the_values_it_was_given()
    {
        var (endpoints, handler) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal(5, rows.Count);
        var uri = handler.Requests.Single();
        Assert.Equal("/stable/industry-classification-search", uri.AbsolutePath);
        Assert.Contains("sicCode=3571", uri.Query);
        Assert.DoesNotContain("symbol=", uri.Query);
        Assert.DoesNotContain("cik=", uri.Query);
    }

    [Fact]
    public async Task Classification_search_sends_all_three_when_all_three_are_given()
    {
        // Measured 2026-08-28: symbol=AAPL, cik=320193 and sicCode=3571 together answered 1 row, so the three
        // narrow the result rather than conflicting. That is what makes an all-optional signature safe.
        var (endpoints, handler) = BuildSearch(StubHandler.Json("[]"));

        await endpoints.FindIndustryClassificationAsync("AAPL", "320193", "3571");

        var uri = handler.Requests.Single();
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("cik=320193", uri.Query);
        Assert.Contains("sicCode=3571", uri.Query);
    }

    [Fact]
    public async Task Classification_search_refuses_an_empty_query_before_spending_a_call()
    {
        // FMP answers a bare call with HTTP 400 and "Please enter at least one search value: cik, sicCode, or
        // symbol." (measured 2026-08-28). Raising it here costs nothing and says the same thing at the call site.
        var (endpoints, handler) = BuildSearch(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindIndustryClassificationAsync());
        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.FindIndustryClassificationAsync("  ", "", null));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Classification_search_carries_the_literal_None_symbol_through_unchanged()
    {
        // Three of the five captured rows read "None" in `symbol` — a Python None rendered into a string field,
        // the same naive-formatting fault that produces the bracketed address. The SDK does not translate it to
        // null: doing so would assert FMP will never list a security called None, and would hide the fault from
        // the caller who has to decide what to do about it.
        var (endpoints, _) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("None", rows[2].Symbol);
        Assert.Equal(3, rows.Count(r => r.Symbol == "None"));
    }

    [Fact]
    public async Task The_search_path_sends_the_bracketed_address_too_and_it_is_normalised()
    {
        // Two of the five IndustryClassification paths bracket, not one: this and all-industry-classification.
        // Measured 2026-08-28 on both ?symbol=AAPL and ?sicCode=3571.
        var (endpoints, _) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal("ONE APPLE PARK WAY, CUPERTINO CA 95014", rows[0].BusinessAddress);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: build error — `SearchEndpoints` has no `FindIndustryClassificationAsync`.

- [ ] **Step 4: Write the method**

In `src/FmpDotNet/Endpoints/SearchEndpoints.cs`, insert after `GetExchangeVariantsAsync` and before the private `QueryAsync` helper. Add `using FmpDotNet.Models;` at the top if it is not already present:

```csharp
    /// <summary>Registrants matching a symbol, a CIK, a SIC code, or any combination —
    /// <c>stable/industry-classification-search</c>.
    ///
    /// <para>The narrow counterpart to
    /// <see cref="DirectoryEndpoints.GetAllIndustryClassificationsAsync(CancellationToken)"/>: that answers all
    /// 25,952 registrants as a 7.3 MB download, this answers a question about them. Same row shape either way —
    /// see <see cref="IndustryClassification"/>.</para>
    ///
    /// <para><b>The three arguments narrow rather than conflict.</b> Measured 2026-08-28,
    /// <c>symbol=AAPL&amp;cik=320193&amp;sicCode=3571</c> answered one row, and <c>sicCode=3571</c> alone
    /// answered a list headed by Apple and Dell. All three are optional individually and at least one is
    /// required: FMP answers a bare call with HTTP 400 and "Please enter at least one search value: cik,
    /// sicCode, or symbol.", so this raises <see cref="ArgumentException"/> before the call is spent rather than
    /// letting a caller buy that sentence.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it. Optional.</param>
    /// <param name="cik">The SEC Central Index Key. Padded or unpadded — measured 2026-08-28, both forms answer
    /// identically. Optional.</param>
    /// <param name="sicCode">A four-character SIC code as the classification paths spell it, e.g. <c>"3571"</c>.
    /// Note that <see cref="DirectoryEndpoints.GetSicCodesAsync"/> spells codes below 1000 without their leading
    /// zero; pad before using one here. Optional.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every matching registrant. Empty when nothing matches. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">All three of <paramref name="symbol"/>, <paramref name="cik"/> and
    /// <paramref name="sicCode"/> are null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindIndustryClassificationAsync(
        string? symbol = null, string? cik = null, string? sicCode = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol) && string.IsNullOrWhiteSpace(cik) && string.IsNullOrWhiteSpace(sicCode))
            throw new ArgumentException(
                "At least one of 'symbol', 'cik' or 'sicCode' is required — "
                + "stable/industry-classification-search answers 400 without one.", nameof(symbol));

        return transport.GetListAsync(
            new FmpRequest("stable/industry-classification-search")
                .With("symbol", symbol).With("cik", cik).With("sicCode", sicCode),
            FmpJsonContext.Default.ListIndustryClassification, ct);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~IndustryClassificationTests`
Expected: PASS, with five new `[Fact]`s for the classification search.

- [ ] **Step 6: Mutation-check the guard**

Replace the three-way `IsNullOrWhiteSpace` check with `symbol is null && cik is null && sicCode is null` and re-run.
Expected: `Classification_search_refuses_an_empty_query_before_spending_a_call` fails on its second assertion — the blank-and-empty case, which is what `FmpRequest.With` would drop on the floor before sending, producing exactly the bare call FMP rejects. Restore.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Endpoints/SearchEndpoints.cs \
        tests/FmpDotNet.Tests/IndustryClassificationTests.cs \
        tests/FmpDotNet.Tests/Fixtures/industry-classification-search.sic3571.json
git commit -m "feat: fmp.Search finds registrants by symbol, CIK or SIC code (#30)"
```

---

### Task 4: Promote the backwards-range guard

Three copies of one guard exist today and this slice would add a fourth. The spec named a third occurrence as the promotion trigger; it has already fired. No new behaviour — this task is behaviour-preserving by construction, and its tests exist to prove that.

**Files:**
- Create: `src/FmpDotNet/DateRange.cs`
- Create: `tests/FmpDotNet.Tests/DateRangeTests.cs`
- Modify: `src/FmpDotNet/Endpoints/ChartEndpoints.cs`
- Modify: `src/FmpDotNet/Endpoints/CompanyEndpoints.cs`
- Modify: `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs`

**Interfaces:**
- Produces: `internal static class FmpDotNet.DateRange` with `internal static void ThrowIfBackwards(LocalDate? from, LocalDate? to)`. Tasks 7 and 8 call it; nothing else in this plan does.

- [ ] **Step 1: Write the failing tests**

`tests/FmpDotNet.Tests/DateRangeTests.cs`:

```csharp
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The one backwards-range guard, promoted out of three copies.
///
/// <para>It existed as <c>ChartEndpoints.ThrowIfBackwards</c>, as
/// <c>CompanyEndpoints.ThrowIfBackwards</c>, and as an unextracted <c>if</c> inside
/// <c>EconomicsEndpoints.GetEconomicCalendarAsync</c> — three identical throws, character for character. The
/// SEC Filings slice would have been the fourth. These tests fix the behaviour so the three call sites that
/// used to own it cannot drift apart now that they share it.</para></summary>
public class DateRangeTests
{
    [Fact]
    public void A_transposed_range_throws_naming_to()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => DateRange.ThrowIfBackwards(new LocalDate(2024, 1, 10), new LocalDate(2024, 1, 1)));

        Assert.Equal("to", error.ParamName);
        Assert.Contains("2024-01-10", error.Message);
    }

    [Fact]
    public void An_equal_range_is_allowed()
    {
        // The boundary the guard must not swallow. `from == to` is a one-day range and is the only range size
        // measured to be safe from the economic calendar's wide-window truncation.
        var same = new LocalDate(2024, 1, 10);

        DateRange.ThrowIfBackwards(same, same);
    }

    [Fact]
    public void One_end_alone_cannot_be_backwards()
    {
        // The nullable signature is what lets one helper serve both the optional ranges (Company, the SEC filing
        // feeds) and the required ones (Chart, Economics, the SEC filing searches), which pass non-nullable
        // LocalDates that convert implicitly.
        DateRange.ThrowIfBackwards(new LocalDate(2024, 1, 10), null);
        DateRange.ThrowIfBackwards(null, new LocalDate(2024, 1, 1));
        DateRange.ThrowIfBackwards(null, null);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~DateRangeTests`
Expected: build error — `DateRange` does not exist.

- [ ] **Step 3: Write the helper**

`src/FmpDotNet/DateRange.cs`:

```csharp
using NodaTime;

namespace FmpDotNet;

/// <summary>The one guard against a transposed date range.
///
/// <para><b>Why one and not one per endpoint group.</b> This check was written three separate times — in
/// <c>ChartEndpoints</c>, in <c>CompanyEndpoints</c>, and inline in <c>EconomicsEndpoints</c> — with identical
/// exception type, parameter name and message. Three copies of a rule is where the rule starts drifting, and
/// the SEC Filings work would have made four.</para>
///
/// <para><b>Why it is a guard at all.</b> FMP does not report a transposed range; it answers one. Measured
/// 2026-08-27, <c>historical-chart</c> answered a backwards range with 390 well-formed rows dated to the
/// <c>to</c> day — plausible data for the wrong end of the range — while the daily endpoints and the economic
/// calendar answered <c>[]</c> with HTTP 200, which reads as "nothing happened that week". Both cost a call from
/// the key's quota to say something untrue. Rejecting before the request is the only place the endpoints can be
/// made to behave alike.</para>
///
/// <para>Nullable on both ends so one helper serves the optional ranges and the required ones alike: one end
/// alone cannot be backwards, so the guard fires only when both are supplied.</para></summary>
internal static class DateRange
{
    /// <summary>Throws when <paramref name="to"/> is earlier than <paramref name="from"/> and both are
    /// supplied.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards.</exception>
    internal static void ThrowIfBackwards(LocalDate? from, LocalDate? to)
    {
        if (from is { } start && to is { } end && end < start)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"'to' must not be earlier than 'from' ({start:uuuu-MM-dd}).");
    }
}
```

- [ ] **Step 4: Redirect the three existing call sites**

In `src/FmpDotNet/Endpoints/ChartEndpoints.cs`: delete the private `ThrowIfBackwards` method and its doc comment, and change both call sites (in `GetIntradayAsync` and in the daily helper) from `ThrowIfBackwards(from, to);` to `DateRange.ThrowIfBackwards(from, to);`.

In `src/FmpDotNet/Endpoints/CompanyEndpoints.cs`: delete the private `ThrowIfBackwards` method and its doc comment, and change the call site in `GetHistoricalMarketCapAsync` to `DateRange.ThrowIfBackwards(from, to);`.

In `src/FmpDotNet/Endpoints/EconomicsEndpoints.cs`, in `GetEconomicCalendarAsync`, replace:

```csharp
        if (to < from)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"'to' must not be earlier than 'from' ({from:uuuu-MM-dd}).");
```

with:

```csharp
        DateRange.ThrowIfBackwards(from, to);
```

`DateRange` lives in the `FmpDotNet` namespace and all three files are in `FmpDotNet.Endpoints`, so no `using` is needed — the parent namespace is in scope.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS. The three pre-existing backwards-range tests —
`ChartEndpointsTests.A_backwards_range_is_rejected_before_it_costs_a_call`,
`CompanyMarketCapTests.Historical_rejects_a_backwards_range_before_spending_a_request` and
`EconomicsEndpointsTests.Rejects_a_backwards_range_before_spending_a_request` — must all still pass, unchanged. None of them asserts on the message or the `ParamName`, so the promotion is invisible to them; that is the evidence the change is behaviour-preserving.

- [ ] **Step 6: Mutation-check**

Change `end < start` to `end <= start` and re-run the whole suite.
Expected: `DateRangeTests.An_equal_range_is_allowed` and
`EconomicsEndpointsTests.<the from == to test>` both fail — the second is the pre-existing one, which is what proves the shared helper is genuinely the code the old call sites now run. Restore.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/DateRange.cs \
        src/FmpDotNet/Endpoints/ChartEndpoints.cs \
        src/FmpDotNet/Endpoints/CompanyEndpoints.cs \
        src/FmpDotNet/Endpoints/EconomicsEndpoints.cs \
        tests/FmpDotNet.Tests/DateRangeTests.cs
git commit -m "refactor: one backwards-range guard instead of three copies (#30)"
```

---

### Task 5: `NullableDateAtMidnightJsonConverter` and the filing row

The eight-field row five paths return, and the two date fields on it that look alike, arrive in the same format, and mean different things in different time zones.

**Files:**
- Create: `src/FmpDotNet/Models/SecFiling.cs`
- Create: `tests/FmpDotNet.Tests/SecFilingsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-8k.head.json`
- Modify: `src/FmpDotNet/Serialization/NodaConverters.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`

**Interfaces:**
- Consumes: `NullableEasternInstantJsonConverter` (existing), `Binding.Fixture`, `Binding.Unbound<T>`.
- Produces: `FmpDotNet.Serialization.NullableDateAtMidnightJsonConverter`; `FmpDotNet.Models.SecFiling` (`Symbol` `string?`, `Cik` `string?`, `FilingDate` `LocalDate?`, `AcceptedDate` `Instant?`, `FormType` `string?`, `HasFinancials` `bool?`, `Link` `string?`, `FinalLink` `string?`); `FmpJsonContext.Default.ListSecFiling`.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/sec-filings-8k.head.json` — the first five rows of `stable/sec-filings-8k?page=0&limit=5`, captured 2026-08-28, verbatim. Rows 1 and 2 are the trap: their `filingDate` is three days later than their `acceptedDate`, while rows 3–5 were accepted in the same late hour and carry a `filingDate` that matches:

```json
[
  {
    "symbol": "SUNE",
    "cik": "0000022701",
    "filingDate": "2024-03-04 00:00:00",
    "acceptedDate": "2024-03-01 22:47:48",
    "formType": "8-K",
    "hasFinancials": null,
    "link": "https://www.sec.gov/Archives/edgar/data/22701/000089710124000091/0000897101-24-000091-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/22701/000089710124000091/pegy240248_8k.htm"
  },
  {
    "symbol": "CGBDL",
    "cik": "0001544206",
    "filingDate": "2024-03-04 00:00:00",
    "acceptedDate": "2024-03-01 22:45:43",
    "formType": "8-K",
    "hasFinancials": null,
    "link": "https://www.sec.gov/Archives/edgar/data/1544206/000154420624000016/0001544206-24-000016-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1544206/000154420624000016/csl-20240301.htm"
  },
  {
    "symbol": "SLE",
    "cik": "0001621672",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 22:27:32",
    "formType": "8-K",
    "hasFinancials": null,
    "link": "https://www.sec.gov/Archives/edgar/data/1621672/000143774924006324/0001437749-24-006324-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1621672/000143774924006324/slgg20240301_8k.htm"
  },
  {
    "symbol": "SBCWW",
    "cik": "0001930313",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 22:22:15",
    "formType": "8-K",
    "hasFinancials": null,
    "link": "https://www.sec.gov/Archives/edgar/data/1930313/000149315224008595/0001493152-24-008595-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1930313/000149315224008595/form8-k.htm"
  },
  {
    "symbol": "SBC",
    "cik": "0001930313",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 22:22:15",
    "formType": "8-K",
    "hasFinancials": null,
    "link": "https://www.sec.gov/Archives/edgar/data/1930313/000149315224008595/0001493152-24-008595-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1930313/000149315224008595/form8-k.htm"
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/SecFilingsTests.cs`. This file grows over Tasks 5, 7 and 8; this step writes the converter and binding sections only.

```csharp
using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The filing row and the two date fields on it, checked against captures taken live 2026-08-28.
///
/// <para><b>The two dates arrive in the same format and mean different things.</b> Across 2,115 rows sampled
/// from three paths, <c>filingDate</c>'s time component was <c>00:00:00</c> in 2,115 of 2,115 cases — it is a
/// date wearing a dummy time. <c>acceptedDate</c> was 19 characters in all 2,115 and is a real EDGAR wall clock
/// in US Eastern. Reading either with the other's converter compiles, binds, and is wrong by hours or by a
/// meaningless midnight.</para></summary>
public class SecFilingsTests
{
    // ---- the filingDate converter ------------------------------------------------------------------------------

    [Fact]
    public void A_filing_date_loses_its_dummy_midnight()
    {
        var row = JsonSerializer.Deserialize(
            """[{"filingDate":"2025-03-06 00:00:00"}]""", FmpJsonContext.Default.ListSecFiling)![0];

        Assert.Equal(new LocalDate(2025, 3, 6), row.FilingDate);
    }

    [Fact]
    public void A_filing_date_that_is_null_or_unreadable_costs_one_field_not_the_row()
    {
        // House rule for every date converter in this file: a single bad stamp must not abort the response and
        // take the other seven fields with it. The bare-ISO case is NOT a measured wire form — 2,115 of 2,115
        // rows carried the time — it is here to pin that an unexpected shape reads as null rather than throwing.
        var rows = JsonSerializer.Deserialize(
            """
            [{"symbol":"A","filingDate":null},
             {"symbol":"B","filingDate":""},
             {"symbol":"C","filingDate":"2025-03-06"}]
            """, FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Null(r.FilingDate));
        Assert.Equal("C", rows[2].Symbol);
    }

    // ---- binding -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_captured_eight_k_row_binds_seven_of_its_eight_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(5, rows.Count);
        // hasFinancials is explicitly null on all five: measured 2026-08-28 over 1,000 sec-filings-8k rows it was
        // null 107 times, false 725 and true 168, so a null here is the field FMP sent, not a field it omitted.
        Assert.Equal(["HasFinancials"], Binding.Unbound(rows[0]));
        Assert.Equal("SUNE", rows[0].Symbol);
        Assert.Equal("0000022701", rows[0].Cik);
        Assert.Equal("8-K", rows[0].FormType);
        Assert.Null(rows[0].HasFinancials);
        Assert.EndsWith("0000897101-24-000091-index.htm", rows[0].Link);
        Assert.EndsWith("pegy240248_8k.htm", rows[0].FinalLink);
    }

    [Fact]
    public void The_accepted_date_is_read_as_eastern_wall_clock_not_as_utc()
    {
        // The silent one. 2024-03-01 falls before that year's DST switch, so Eastern is UTC-5 and
        // "2024-03-01 22:47:48" is 2024-03-02T03:47:48Z. Read with NullableFmpInstantJsonConverter — the UTC twin,
        // one identifier away and the same wire format — every value would land five hours early, still sort
        // correctly, and still look entirely plausible.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 47, 48), rows[0].AcceptedDate);
        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 27, 32), rows[2].AcceptedDate);
    }

    [Fact]
    public void Filing_date_cannot_be_derived_from_accepted_date()
    {
        // The trap, in one response. Rows 1 and 2 were accepted at 22:47 and 22:45 on 2024-03-01 and carry a
        // filingDate of 2024-03-04. Rows 3 to 5 were accepted at 22:27 and 22:22 the same evening and carry a
        // filingDate of 2024-03-01. Same endpoint, same page, same acceptance hour, two different answers — so
        // neither field is computable from the other, and a caller filtering on the wrong one is not told.
        //
        // It matters because `from` and `to` filter acceptedDate, NOT filingDate: measured 2026-08-28,
        // sec-filings-financials over 2025-03-01..2025-03-05 answered 722 rows, of which 16 carried a filingDate
        // past the requested `to` — and all 16 of those carried an acceptedDate inside it, with zero rows in the
        // whole response falling outside. 722 is comfortably under the 1,000 cap, so truncation cannot explain it.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        var acceptedOn = new LocalDate(2024, 3, 1);
        Assert.All(rows, r => Assert.Equal(acceptedOn, r.AcceptedDate!.Value.InZone(
            DateTimeZoneProviders.Tzdb["America/New_York"]).Date));

        Assert.Equal(new LocalDate(2024, 3, 4), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 4), rows[1].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 1), rows[2].FilingDate);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: build error — `SecFiling` and `ListSecFiling` do not exist.

- [ ] **Step 4: Write the converter**

Append to `src/FmpDotNet/Serialization/NodaConverters.cs`, immediately after `NullableLocalDateTimeJsonConverter` so the four readers of the `uuuu-MM-dd HH:mm:ss` wire form sit together:

```csharp
/// <summary>Reads FMP's <c>"uuuu-MM-dd HH:mm:ss"</c> form as a <see cref="LocalDate"/>, discarding a time
/// component that carries no information.
///
/// <para><b>The fourth converter for this one wire format, and the measurement is what earns it.</b> Across
/// 2,115 rows sampled 2026-08-28 from <c>sec-filings-8k</c>, <c>sec-filings-financials</c> and
/// <c>sec-filings-search/form-type</c>, the time component of <c>filingDate</c> was <c>00:00:00</c> in
/// <b>2,115 of 2,115</b> cases. It is a date with a dummy midnight bolted on, not a timestamp.</para>
///
/// <para><b>Neither existing converter fits.</b> <see cref="NullableLocalDateJsonConverter"/> uses
/// <c>LocalDatePattern.Iso</c>, which rejects the trailing time outright and would null every value.
/// <see cref="NullableLocalDateTimeJsonConverter"/> binds it and then leaks a meaningless midnight into every
/// comparison a caller writes.</para>
///
/// <para><b>One pattern, no fallback, deliberately.</b> If FMP ever drops the dummy time, this reads null rather
/// than quietly accepting a second format — and the weekly smoke baseline reports that as
/// <c>FilingDate: now always null, was populated</c>, on the run after it happens. A silent fallback would make
/// the change invisible, which is the opposite of what a measured SDK is for.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad stamp costs one field rather
/// than the whole response.</para></summary>
public sealed class NullableDateAtMidnightJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value.Date : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value.AtMidnight()));
    }
}
```

- [ ] **Step 5: Write the model**

`src/FmpDotNet/Models/SecFiling.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One EDGAR filing, from any of five paths: <c>stable/sec-filings-8k</c>,
/// <c>stable/sec-filings-financials</c>, and all three of
/// <c>stable/sec-filings-search/{symbol,cik,form-type}</c>.
///
/// <para><b>One record with a nullable rather than two records.</b> The two feeds send eight fields; the three
/// search paths send the same seven minus <c>hasFinancials</c>. A second record would duplicate seven properties
/// to express one absence — see <see cref="HasFinancials"/>, where the absence is documented instead.</para>
///
/// <para><b>The two feeds differ by filter, not by shape.</b> Measured 2026-08-28 over 1,000 rows each:
/// <c>sec-filings-8k</c> returned <c>formType</c> <c>8-K</c> 1,000 times, while
/// <c>sec-filings-financials</c> returned <c>8-K</c> 861 times, <c>6-K</c> 137 and <c>10-K</c> twice. So one
/// filters by form and the other by whether financial data is attached.</para></summary>
public sealed record SecFiling
{
    /// <summary>The ticker FMP attributes the filing to. Two rows can share one filing — <c>SBC</c> and
    /// <c>SBCWW</c> in the captured page are the same accession number under two tickers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters. <see cref="string"/> for the
    /// reason on <see cref="IndustryClassification.Cik"/>: the padding is the value.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The date EDGAR stamps the filing with.
    ///
    /// <para><b>A date, not a timestamp, and not derivable from <see cref="AcceptedDate"/>.</b> The wire sends
    /// <c>"2024-03-04 00:00:00"</c>; the time was <c>00:00:00</c> on 2,115 of 2,115 rows measured 2026-08-28, so
    /// it is discarded — see <see cref="NullableDateAtMidnightJsonConverter"/>. A filing accepted late in the
    /// evening may be stamped a later business day, and may not: in the five captured rows of one page,
    /// <c>SUNE</c> and <c>CGBDL</c> were accepted at 22:47 and 22:45 on 2024-03-01 and stamped 2024-03-04, while
    /// <c>SLE</c>, <c>SBCWW</c> and <c>SBC</c> were accepted at 22:27 and 22:22 the same evening and stamped
    /// 2024-03-01.</para>
    ///
    /// <para><b>This is not the field <c>from</c> and <c>to</c> filter on.</b> They filter
    /// <see cref="AcceptedDate"/>, so a response legitimately contains rows whose <c>FilingDate</c> falls outside
    /// the range you asked for — measured 2026-08-28, 16 of 722 rows over a five-day window. Those rows are not
    /// errors and are not dropped here.</para></summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The moment EDGAR accepted the submission.
    ///
    /// <para><b>Read as US Eastern wall clock, not UTC</b> — see
    /// <see cref="NullableEasternInstantJsonConverter"/>, which establishes the zone from a measured DST shift
    /// rather than assuming it. The UTC twin reads the identical wire format and would land every value four or
    /// five hours early, sorting correctly and looking plausible.</para>
    ///
    /// <para><b>This is the field <c>from</c> and <c>to</c> actually filter on</b>, which is why a response can
    /// carry rows whose <see cref="FilingDate"/> sits outside the requested range. Corroborated 2026-08-28 by the
    /// acceptance-hour distribution over 1,000 8-K rows: a spike of 434 at 16:00 — the post-close surge — and 63
    /// rows from 21:00 onward, which is exactly the population that can spill into a later
    /// <see cref="FilingDate"/>.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form type — <c>"8-K"</c>, <c>"6-K"</c>, <c>"10-K"</c>, <c>"4"</c>, <c>"25-NSE"</c>.
    ///
    /// <para>A raw <see cref="string"/> rather than an enum, for the reason
    /// <see cref="EconomicRelease.Impact"/> gives: a form type this SDK has never seen must not cost the caller
    /// the response. Three distinct values appeared in 1,000 rows of one endpoint alone, and EDGAR defines
    /// hundreds.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>Whether FMP has financial data attached to this filing.
    ///
    /// <para><b>Null means two different things, and which one depends on the path you called.</b> On the three
    /// <c>sec-filings-search/*</c> paths the field is <b>absent from the payload entirely</b> — measured
    /// 2026-08-28 — so null there means "this endpoint does not say". On <c>sec-filings-8k</c> the field is
    /// present and explicitly <c>null</c> on some rows (107 of 1,000), alongside <c>false</c> (725) and
    /// <c>true</c> (168), so null there is FMP's own answer.</para>
    ///
    /// <para>On <c>sec-filings-financials</c> it was <c>true</c> on 1,000 of 1,000 rows, which is what that
    /// endpoint selects on — so the field carries no information there.</para></summary>
    [JsonPropertyName("hasFinancials")] public bool? HasFinancials { get; init; }

    /// <summary>The EDGAR filing-index page for the accession.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The primary document itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
```

- [ ] **Step 6: Register the model**

Add `[JsonSerializable(typeof(List<SecFiling>))]` to `src/FmpDotNet/Serialization/FmpJsonContext.cs`, immediately after the `IndustryClassification` entry added in Task 1.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: PASS. Two converter facts and three binding facts.

- [ ] **Step 8: Mutation-check both converters**

Swap `NullableEasternInstantJsonConverter` for `NullableFmpInstantJsonConverter` on `AcceptedDate` and re-run.
Expected: **exactly one test fails — `The_accepted_date_is_read_as_eastern_wall_clock_not_as_utc`.** This is the
single most valuable mutation in the slice: the two converters read the same wire format, differ by one
identifier, and the wrong one produces plausible values. Restore.

`Filing_date_cannot_be_derived_from_accepted_date` does **not** fail under this mutation, and that is correct
rather than a gap. Its acceptance assertion reads the instant back out through
`InZone(DateTimeZoneProviders.Tzdb["America/New_York"]).Date`, so it round-trips through the same zone it
came in by. Work it through on the fixture's own rows: `2024-03-01 22:47:48` read as Eastern is
`2024-03-02T03:47:48Z`, which is 2024-03-01 in New York; read as UTC it is `2024-03-01T22:47:48Z`, which is
17:47 EST — still 2024-03-01 in New York. Every captured row is a 22:xx acceptance, and a five-hour shift
does not cross midnight from there. That test exists to pin the `filingDate` overshoot, not the zone, and
it should not be rewritten to catch a zone bug: the test above already owns that job, and giving one test
two jobs makes a future failure ambiguous about which trap fired.

Then swap `NullableDateAtMidnightJsonConverter` for `NullableLocalDateJsonConverter` on `FilingDate`. This compiles — both converters are `JsonConverter<LocalDate?>` and the property is `LocalDate?`, which is exactly why the mistake is worth a test rather than a code review.
Expected: `A_filing_date_loses_its_dummy_midnight` and `Filing_date_cannot_be_derived_from_accepted_date` fail with every `FilingDate` null, because `LocalDatePattern.Iso` rejects the trailing time and the converter answers null rather than throwing. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/SecFiling.cs \
        src/FmpDotNet/Serialization/NodaConverters.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        tests/FmpDotNet.Tests/SecFilingsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-8k.head.json
git commit -m "feat: model the EDGAR filing row and read filingDate as a date (#30)"
```

---

### Task 6: `SecProfile`, and the `fmp.SecFilings` facade it arrives on

The eleventh facade is born here, wired into `FmpClient` and DI, carrying the two `sec-profile` paths. The remaining seven paths hang off it in Tasks 7–9.

**Files:**
- Create: `src/FmpDotNet/Models/SecProfile.cs`
- Create: `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/SecProfileTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-profile.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-profile.TSM.json`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/FmpClient.cs`
- Modify: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs`

**Interfaces:**
- Consumes: `FmpTransport`, `FmpRequest`, `FmpJsonContext`, `NullableLocalDateJsonConverter` (all existing).
- Produces: `FmpDotNet.Models.SecProfile` (35 properties, listed in Step 3); `FmpDotNet.Endpoints.SecFilingsEndpoints(FmpTransport transport)` with `GetProfileAsync(string symbol, CancellationToken ct = default)` → `Task<SecProfile?>` and `GetProfileByCikAsync(string cik, CancellationToken ct = default)` → `Task<SecProfile?>`; `FmpClient.SecFilings` → `SecFilingsEndpoints`; `FmpJsonContext.Default.ListSecProfile`.

- [ ] **Step 1: Write the two fixtures**

`tests/FmpDotNet.Tests/Fixtures/sec-profile.AAPL.json` — `stable/sec-profile?symbol=AAPL`, captured 2026-08-28, verbatim, with the `description` field shortened to its first sentence. **That is the only edit**, and it is made because the captured value is a 2,400-character marketing paragraph that carries no shape information; every other value is byte-for-byte what FMP sent:

```json
[
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "registrantName": "Apple Inc.",
    "sicCode": "3571",
    "sicDescription": "Electronic Computers",
    "sicGroup": "Consumer Electronics",
    "isin": "US0378331005",
    "businessAddress": "ONE APPLE PARK WAY,CUPERTINO CA 95014,(408) 996-1010",
    "mailingAddress": "ONE APPLE PARK WAY,CUPERTINO CA 95014",
    "phoneNumber": "(408) 996-1010",
    "postalCode": "95014",
    "city": "Cupertino",
    "state": "CA",
    "country": "US",
    "description": "Apple Inc. is a global technology corporation that specializes in the conceptualization, production, and sale of a diverse suite of electronic devices.",
    "ceo": "Timothy D. Cook",
    "website": "https://www.apple.com",
    "exchange": "NASDAQ",
    "stateLocation": "CA",
    "stateOfIncorporation": "CA",
    "fiscalYearEnd": "09-30",
    "ipoDate": "1980-12-12",
    "employees": "166000",
    "secFilingsUrl": "https://www.sec.gov/cgi-bin/browse-edgar?CIK=0000320193",
    "taxIdentificationNumber": "94-2404110",
    "fiftyTwoWeekRange": "225.95 - 344.57",
    "isActive": true,
    "assetType": "stock",
    "openFigiComposite": "BBG000B9XRY4",
    "priceCurrency": "USD",
    "marketSector": "Technology",
    "securityType": null,
    "isEtf": false,
    "isAdr": false,
    "isFund": false
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/sec-profile.TSM.json` — `stable/sec-profile?symbol=TSM`, same capture, same single edit to `description`. This is the thirteenth fixture and it earns its place by being the only capture where `isAdr` is `true`; without it every boolean on this record is `false` in every fixture, and a converter that always answered `false` would pass:

```json
[
  {
    "symbol": "TSM",
    "cik": "0001046179",
    "registrantName": "Taiwan Semiconductor Manufacturing Company Limited",
    "sicCode": "3674",
    "sicDescription": "Semiconductors & Related Devices",
    "sicGroup": "Semiconductors",
    "isin": "US8740391003",
    "businessAddress": "NO. 8, LI-HSIN ROAD 6,HSINCHU F5 300-096,886-3-5636688",
    "mailingAddress": "NO. 8, LI-HSIN ROAD 6,HSINCHU F5 300-096",
    "phoneNumber": "886-3-5636688",
    "postalCode": "300096",
    "city": "Hsinchu City",
    "state": "TPE",
    "country": "TW",
    "description": "Taiwan Semiconductor Manufacturing Company Limited (TSMC), along with its affiliated entities, operates globally in the semiconductor industry.",
    "ceo": "Che Chia Wei",
    "website": "https://www.tsmc.com",
    "exchange": "NYSE",
    "stateLocation": "TPE",
    "stateOfIncorporation": "F5",
    "fiscalYearEnd": "12-31",
    "ipoDate": "1997-10-09",
    "employees": "65152",
    "secFilingsUrl": "https://www.sec.gov/cgi-bin/browse-edgar?CIK=0001046179",
    "taxIdentificationNumber": "00-0000000",
    "fiftyTwoWeekRange": "225.63 - 479",
    "isActive": true,
    "assetType": "stock",
    "openFigiComposite": "BBG000BD8ZK0",
    "priceCurrency": "USD",
    "marketSector": "Technology",
    "securityType": null,
    "isEtf": false,
    "isAdr": true,
    "isFund": false
  }
]
```

- [ ] **Step 2: Write the failing tests**

`tests/FmpDotNet.Tests/SecProfileTests.cs`. This file grows again in Task 9.

```csharp
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/sec-profile</c> — EDGAR registrant data, checked against two captures taken live
/// 2026-08-28.
///
/// <para><b>Not a second <see cref="Models.CompanyProfile"/>.</b> That models <c>stable/profile</c>, which is
/// market data and carries <c>price</c> and <c>marketCap</c>. This is the registration record and carries
/// <c>taxIdentificationNumber</c>, <c>stateOfIncorporation</c> and <c>secFilingsUrl</c>. Different sources,
/// different field sets, no overlap worth sharing.</para>
///
/// <para><b>Almost everything on the wire is a string.</b> Measured across AAPL, TSM, SHEL, BRK-B, NVO and SPY:
/// every value is a JSON string except <c>isActive</c>, <c>isEtf</c>, <c>isAdr</c> and <c>isFund</c>, which are
/// real booleans. <c>employees</c> is <c>"166000"</c>, quoted.</para></summary>
public class SecProfileTests
{
    private static (SecFilingsEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SecFilingsEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Binds_thirty_four_of_its_thirty_five_fields()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")));

        var profile = await endpoints.GetProfileAsync("AAPL");

        Assert.NotNull(profile);
        // securityType was null on all six symbols sampled 2026-08-28. It is modelled rather than dropped: an
        // always-null field that is dropped becomes invisible on the day it starts arriving.
        Assert.Equal(["SecurityType"], Binding.Unbound(profile));
        Assert.Equal("AAPL", profile.Symbol);
        Assert.Equal("0000320193", profile.Cik);
        Assert.Equal("Apple Inc.", profile.RegistrantName);
        Assert.Equal("Electronic Computers", profile.SicDescription);
        Assert.Equal("US0378331005", profile.Isin);
        Assert.Equal("94-2404110", profile.TaxIdentificationNumber);
        Assert.Equal("https://www.sec.gov/cgi-bin/browse-edgar?CIK=0000320193", profile.SecFilingsUrl);
    }

    [Fact]
    public async Task The_employee_count_is_a_quoted_string_on_the_wire_and_an_int_here()
    {
        // AllowReadingFromString is set globally on FmpJsonContext, so `"166000"` binds to int? without a
        // converter. Asserting it here means a future change to that option cannot pass unnoticed.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")));

        var profile = await endpoints.GetProfileAsync("AAPL");

        Assert.Equal(166_000, profile!.Employees);
    }

    [Fact]
    public async Task The_ipo_date_is_plain_iso_and_binds_to_a_date()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")));

        var profile = await endpoints.GetProfileAsync("AAPL");

        Assert.Equal(new LocalDate(1980, 12, 12), profile!.IpoDate);
    }

    [Fact]
    public async Task The_fiscal_year_end_and_the_fifty_two_week_range_stay_as_sent()
    {
        // Two fields that look parseable and are not. "09-30" is a month and a day with no year, which no date
        // type can hold without inventing one. "225.95 - 344.57" is one formatted string rather than two numbers,
        // and splitting it would be the SDK asserting a format FMP has never promised.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")),
            StubHandler.Json(Binding.Fixture("sec-profile.TSM.json")));

        var apple = await endpoints.GetProfileAsync("AAPL");
        var tsmc = await endpoints.GetProfileAsync("TSM");

        Assert.Equal("09-30", apple!.FiscalYearEnd);
        Assert.Equal("12-31", tsmc!.FiscalYearEnd);
        Assert.Equal("225.95 - 344.57", apple.FiftyTwoWeekRange);
        // Not "225.63 - 479.00". FMP does not pad, so a caller parsing on a fixed shape breaks here.
        Assert.Equal("225.63 - 479", tsmc.FiftyTwoWeekRange);
    }

    [Fact]
    public async Task The_four_booleans_are_real_booleans_and_at_least_one_of_them_varies()
    {
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")),
            StubHandler.Json(Binding.Fixture("sec-profile.TSM.json")));

        var apple = await endpoints.GetProfileAsync("AAPL");
        var tsmc = await endpoints.GetProfileAsync("TSM");

        Assert.True(apple!.IsActive);
        Assert.False(apple.IsAdr);
        // The reason this fixture exists: without a row where a boolean is true, a model that read every one of
        // them as false would pass every assertion above.
        Assert.True(tsmc!.IsAdr);
        Assert.False(tsmc.IsEtf);
        Assert.False(tsmc.IsFund);
    }

    [Fact]
    public async Task The_business_address_is_left_exactly_as_sent_here()
    {
        // Deliberately NOT normalised. This endpoint's businessAddress is already comma-joined, has no space
        // after the comma, and appends the phone number — a different convention from the five paths
        // BusinessAddressJsonConverter serves. Applying that converter here would be a no-op today and a
        // silent corruption the day FMP changes either format.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")));

        var profile = await endpoints.GetProfileAsync("AAPL");

        Assert.Equal("ONE APPLE PARK WAY,CUPERTINO CA 95014,(408) 996-1010", profile!.BusinessAddress);
        Assert.Equal("ONE APPLE PARK WAY,CUPERTINO CA 95014", profile.MailingAddress);
    }

    [Fact]
    public async Task An_unknown_symbol_is_null_rather_than_an_error()
    {
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetProfileAsync("ZZZZNOPE"));
    }

    [Fact]
    public async Task Both_profile_paths_send_what_they_were_given()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")),
            StubHandler.Json(Binding.Fixture("sec-profile.AAPL.json")));

        await endpoints.GetProfileAsync("AAPL");
        await endpoints.GetProfileByCikAsync("320193");

        Assert.Equal("/stable/sec-profile", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Equal("/stable/sec-profile", handler.Requests[1].AbsolutePath);
        Assert.Contains("cik=320193", handler.Requests[1].Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_argument_is_refused_before_a_call_is_spent(string blank)
    {
        // Not cosmetic: FmpRequest.With drops an empty value, so a blank symbol would reach FMP as a bare
        // sec-profile call — which measured 2026-08-28 answers HTTP 200 with Apple's profile. The caller would
        // get a well-formed answer to a question they did not ask.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetProfileAsync(blank));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetProfileByCikAsync(blank));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Write the model**

`src/FmpDotNet/Models/SecProfile.cs`. Thirty-five properties in wire order; twenty-six are plain `string?` and the nine that are not carry the reason on them:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>An SEC registrant's profile, from <c>stable/sec-profile</c>.
///
/// <para><b>Not a reuse of <see cref="CompanyProfile"/>, and the difference is the source rather than the
/// spelling.</b> That models <c>stable/profile</c>, which is market data: it carries <c>price</c>,
/// <c>marketCap</c>, <c>beta</c> and <c>volume</c>. This is the EDGAR registration record and carries
/// <c>taxIdentificationNumber</c>, <c>stateOfIncorporation</c> and <c>secFilingsUrl</c>. Sharing one record
/// would mean a caller could not tell which fields their answer actually had.</para>
///
/// <para><b>Thirty-five fields, of which all but four are JSON strings.</b> Measured 2026-08-28 across AAPL,
/// TSM, SHEL, BRK-B, NVO and SPY — every one returned exactly one row, for both the padded and the unpadded
/// CIK. The four exceptions are <see cref="IsActive"/>, <see cref="IsEtf"/>, <see cref="IsAdr"/> and
/// <see cref="IsFund"/>, which are real booleans.</para></summary>
public sealed record SecProfile
{
    /// <summary>The ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters. <see cref="string"/> for the reason on
    /// <see cref="IndustryClassification.Cik"/>.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The name the registrant files under — <c>"Apple Inc."</c>. Mixed case here, unlike the
    /// upper-cased <see cref="IndustryClassification.Name"/> the classification paths send.</summary>
    [JsonPropertyName("registrantName")] public string? RegistrantName { get; init; }

    /// <summary>The SIC code — <c>"3571"</c>. Blank on one of the six symbols sampled 2026-08-28.</summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The SIC code's label as this endpoint spells it — <c>"Electronic Computers"</c>, title case.
    /// <see cref="IndustryClassification.IndustryTitle"/> spells the same concept
    /// <c>"ELECTRONIC COMPUTERS"</c>; neither is normalised.</summary>
    [JsonPropertyName("sicDescription")] public string? SicDescription { get; init; }

    /// <summary>FMP's own grouping above the SIC code — <c>"Consumer Electronics"</c>. Not an EDGAR
    /// field.</summary>
    [JsonPropertyName("sicGroup")] public string? SicGroup { get; init; }

    /// <summary>The security's ISIN.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The business address as one comma-joined line, <b>with the telephone number appended</b> —
    /// <c>"ONE APPLE PARK WAY,CUPERTINO CA 95014,(408) 996-1010"</c>.
    ///
    /// <para><b>Deliberately not put through <see cref="BusinessAddressJsonConverter"/>.</b> That converter
    /// serves the five <see cref="IndustryClassification"/> paths, which join with <c>", "</c> and do not append
    /// the phone. This endpoint joins with a bare <c>","</c> and does. Two different conventions, left as each
    /// was measured.</para></summary>
    [JsonPropertyName("businessAddress")] public string? BusinessAddress { get; init; }

    /// <summary>The mailing address, comma-joined and <b>without</b> the phone number. Frequently identical to
    /// <see cref="BusinessAddress"/> minus that suffix, but not guaranteed to be.</summary>
    [JsonPropertyName("mailingAddress")] public string? MailingAddress { get; init; }

    /// <summary>The registrant's telephone number, unnormalised.</summary>
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }

    /// <summary>Postal code as EDGAR holds it — <c>"95014"</c> for a US filer, <c>"300096"</c> for a Taiwanese
    /// one. <see cref="string"/>, not a number: leading zeros are real in most of the world.</summary>
    [JsonPropertyName("postalCode")] public string? PostalCode { get; init; }

    /// <summary>City.</summary>
    [JsonPropertyName("city")] public string? City { get; init; }

    /// <summary>State or region code — <c>"CA"</c>, <c>"TPE"</c>.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>ISO country code — <c>"US"</c>, <c>"TW"</c>.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>FMP's prose description of the business. Long — the captured Apple value runs to about 2,400
    /// characters.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The chief executive as EDGAR holds the name. Blank on one of the six symbols sampled
    /// 2026-08-28.</summary>
    [JsonPropertyName("ceo")] public string? Ceo { get; init; }

    /// <summary>The registrant's website.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The exchange FMP attributes the security to — <c>"NASDAQ"</c>. A raw string rather than an enum,
    /// for the reason <see cref="Quote.Exchange"/> gives.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>Where the registrant is located, as a state or region code. Distinct from
    /// <see cref="StateOfIncorporation"/>: measured 2026-08-28, TSM reads <c>"TPE"</c> here and <c>"F5"</c>
    /// there.</summary>
    [JsonPropertyName("stateLocation")] public string? StateLocation { get; init; }

    /// <summary>Where the registrant is incorporated, in EDGAR's own state-code vocabulary — which includes
    /// non-US codes such as <c>"F5"</c>. Blank on one of the six symbols sampled 2026-08-28.</summary>
    [JsonPropertyName("stateOfIncorporation")] public string? StateOfIncorporation { get; init; }

    /// <summary>The fiscal year end as a <b>month and day with no year</b> — <c>"09-30"</c>.
    ///
    /// <para><see cref="string"/>, and that is the honest type. No date type holds a month and a day without a
    /// year, and choosing one would mean inventing the year — which every caller would then have to know to
    /// ignore. NodaTime's <c>AnnualDate</c> would fit the concept, but the wire value has not been measured
    /// against February 29 or against any malformed form, and a parse that throws would cost the caller the
    /// other 34 fields.</para></summary>
    [JsonPropertyName("fiscalYearEnd")] public string? FiscalYearEnd { get; init; }

    /// <summary>The IPO date — plain ISO, <c>"1980-12-12"</c>, unlike the space-separated stamps on
    /// <see cref="SecFiling"/>. Read with <see cref="NullableLocalDateJsonConverter"/>.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>Headcount.
    ///
    /// <para>The wire sends this <b>quoted</b> — <c>"166000"</c> — and it binds to <see cref="int"/> because
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = JsonNumberHandling.AllowReadingFromString</c> globally. No
    /// converter is needed and none is used.</para></summary>
    [JsonPropertyName("employees")] public int? Employees { get; init; }

    /// <summary>A ready-made EDGAR browse URL for this CIK.</summary>
    [JsonPropertyName("secFilingsUrl")] public string? SecFilingsUrl { get; init; }

    /// <summary>The IRS Employer Identification Number — <c>"94-2404110"</c>. Foreign filers carry the
    /// placeholder <c>"00-0000000"</c>, measured on TSM 2026-08-28, which is a value rather than an
    /// absence.</summary>
    [JsonPropertyName("taxIdentificationNumber")] public string? TaxIdentificationNumber { get; init; }

    /// <summary>The 52-week price range as <b>one formatted string</b> — <c>"225.95 - 344.57"</c>.
    ///
    /// <para><see cref="string"/> rather than two decimals. FMP does not pad: the same field reads
    /// <c>"225.63 - 479"</c> for TSM, measured the same day. Splitting on the separator would be the SDK
    /// asserting a format FMP has never promised, and the failure would be a null price rather than an
    /// error.</para></summary>
    [JsonPropertyName("fiftyTwoWeekRange")] public string? FiftyTwoWeekRange { get; init; }

    /// <summary>Whether FMP considers the registrant active. A real JSON boolean.</summary>
    [JsonPropertyName("isActive")] public bool? IsActive { get; init; }

    /// <summary>FMP's asset classification — <c>"stock"</c>.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>The OpenFIGI composite identifier — <c>"BBG000B9XRY4"</c>.</summary>
    [JsonPropertyName("openFigiComposite")] public string? OpenFigiComposite { get; init; }

    /// <summary>The currency the security is priced in — <c>"USD"</c>.</summary>
    [JsonPropertyName("priceCurrency")] public string? PriceCurrency { get; init; }

    /// <summary>FMP's market sector — <c>"Technology"</c>.</summary>
    [JsonPropertyName("marketSector")] public string? MarketSector { get; init; }

    /// <summary>The security type.
    ///
    /// <para><b>Null on all six symbols sampled 2026-08-28, and modelled anyway.</b> A field that always arrives
    /// empty is recorded and flagged rather than dropped: dropping it would make the day it starts arriving
    /// invisible, and the weekly smoke baseline records it as <c>null</c> today so that day is reported as
    /// drift.</para></summary>
    [JsonPropertyName("securityType")] public string? SecurityType { get; init; }

    /// <summary>Whether the security is an exchange-traded fund. A real JSON boolean.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>Whether the security is an American Depositary Receipt. A real JSON boolean — <c>true</c> for
    /// TSM, measured 2026-08-28.</summary>
    [JsonPropertyName("isAdr")] public bool? IsAdr { get; init; }

    /// <summary>Whether the security is a fund. A real JSON boolean.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }
}
```

- [ ] **Step 4: Write the facade with its two profile methods**

`src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`. Tasks 7–9 append to this file; write only what is below now:

```csharp
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>SEC Filings</c> group — what companies have filed with EDGAR, and who the filers are.
///
/// <para><b>Nine of the twelve paths FMP files under this heading.</b> The other three are reference lists
/// rather than filings and live where their job already is: <c>all-industry-classification</c> and
/// <c>standard-industrial-classification-list</c> on <see cref="DirectoryEndpoints"/>, and
/// <c>industry-classification-search</c> on <see cref="SearchEndpoints"/>. That follows existing practice rather
/// than departing from it — <c>commodities-list</c>, <c>forex-list</c> and <c>index-list</c> are already on
/// <see cref="DirectoryEndpoints"/> although FMP documents them under Commodity, Forex and Indexes. This SDK
/// files a path by what it returns.</para>
///
/// <para><b>Two families here, and they do not share a row shape.</b>
/// <see cref="GetProfileAsync(string, CancellationToken)"/> answers a registrant;
/// <c>Get8KFilingsAsync</c> and its neighbours answer filings; the three <c>FindCompany*</c> methods
/// answer the same classification row <see cref="DirectoryEndpoints"/> and <see cref="SearchEndpoints"/> serve,
/// which is why they return <see cref="IndustryClassification"/> rather than a type of their own.</para>
///
/// <para><b>Dates are the trap in this group.</b> <c>from</c> and <c>to</c> filter
/// <see cref="SecFiling.AcceptedDate"/>, not <see cref="SecFiling.FilingDate"/>, so a response legitimately
/// carries rows dated outside the range you asked for. See <see cref="SecFiling"/> for the measurement.</para>
///
/// <para>Every measurement quoted in this class was taken on 2026-08-28 against an Ultimate key. No path in the
/// group answered 402.</para></summary>
public sealed class SecFilingsEndpoints(FmpTransport transport)
{
    /// <summary>The EDGAR registrant profile for one symbol, or <see langword="null"/> when FMP knows no such
    /// symbol — <c>stable/sec-profile</c>.
    ///
    /// <para><b>Not the same thing as <see cref="CompanyEndpoints.GetProfileAsync"/>.</b> That answers market
    /// data; this answers the registration record. See <see cref="SecProfile"/>.</para>
    ///
    /// <para><b>A bare call to this endpoint answers Apple's profile with HTTP 200</b>, measured 2026-08-28 —
    /// it defaults rather than erroring. A blank symbol would reach FMP as a bare call, because
    /// <c>FmpRequest</c> drops empty values, so it is rejected here instead: a caller must not receive a
    /// well-formed answer to a question they did not ask.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The profile, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<SecProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/sec-profile").With("symbol", symbol),
            FmpJsonContext.Default.ListSecProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>The EDGAR registrant profile for one Central Index Key, or <see langword="null"/> when FMP knows
    /// no such filer — <c>stable/sec-profile</c> with <c>cik</c> instead of <c>symbol</c>.
    ///
    /// <para>The same path and the same 35 fields as
    /// <see cref="GetProfileAsync(string, CancellationToken)"/>; measured 2026-08-28, AAPL and CIK
    /// <c>0000320193</c> answered identically, and the padded and unpadded forms of the CIK both answered one
    /// row.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded — both work.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The profile, or <see langword="null"/> when FMP has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank — see
    /// <see cref="GetProfileAsync(string, CancellationToken)"/> for why a blank one is refused rather than
    /// sent.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<SecProfile?> GetProfileByCikAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/sec-profile").With("cik", cik),
            FmpJsonContext.Default.ListSecProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
}
```

The `using NodaTime;` is unused until Task 7 adds the date-ranged methods. `TreatWarningsAsErrors` does **not** flag an unused `using` by default (that is IDE0005, an analyser rather than a compiler warning), but if the build does complain, omit it now and add it in Task 7.

- [ ] **Step 5: Register the model, the facade and the DI entry**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, add `[JsonSerializable(typeof(List<SecProfile>))]` after the `SecFiling` entry from Task 5.

In `src/FmpDotNet/FmpClient.cs`, add `SecFilingsEndpoints secFilings` to the primary constructor's parameter list — after `search` and before `quote`, matching the property order below — and add the property after `Search`:

```csharp
    /// <summary>What companies have filed with the SEC, and who the filers are — EDGAR registrant profiles,
    /// the 8-K and financial-statement filing feeds, and filing search by symbol, CIK or form type.
    ///
    /// <para>Three of the twelve paths FMP documents under this heading are reference lists rather than
    /// filings, and are on <see cref="Directory"/> and <see cref="Search"/> instead. See
    /// <see cref="SecFilingsEndpoints"/>.</para></summary>
    public SecFilingsEndpoints SecFilings { get; } = secFilings;
```

In `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs`, add `services.TryAddTransient<SecFilingsEndpoints>();` after the `SearchEndpoints` line and before `QuoteEndpoints`.

- [ ] **Step 6: Fix and future-proof the DI resolution test**

`tests/FmpDotNet.Tests/AddFmpTests.cs`, `Resolves_the_client_and_every_endpoint_group` currently asserts seven of the ten groups — `Search`, `Quote` and `Chart` are silently absent, which is how a group can be added without anyone noticing the test does not cover it. Replace the assertion block with all eleven, plus a guard that fails when the twelfth is added without a line here:

```csharp
        Assert.NotNull(client.Company);
        Assert.NotNull(client.Directory);
        Assert.NotNull(client.Statements);
        Assert.NotNull(client.Calendar);
        Assert.NotNull(client.Analyst);
        Assert.NotNull(client.Economics);
        Assert.NotNull(client.Search);
        Assert.NotNull(client.SecFilings);
        Assert.NotNull(client.Quote);
        Assert.NotNull(client.Chart);
        Assert.NotNull(client.Bulk);

        // The list above was three short when SecFilings was added — Search, Quote and Chart had never been
        // named here. A missing line is invisible: the test passes, and the group it forgot is untested for
        // resolution. This makes the omission fail instead.
        Assert.Equal(11, typeof(FmpClient)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length);
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~SecProfileTests|FullyQualifiedName~AddFmpTests"`
Expected: PASS. Then run the whole suite: the only failure may be `EndpointCoverageTests`, red since Task 2 and fixed in Task 11 — see Global Constraints. Every other test must pass.

- [ ] **Step 8: Mutation-check the facade wiring**

Comment out `services.TryAddTransient<SecFilingsEndpoints>();` and re-run `AddFmpTests`.
Expected: `Resolves_the_client_and_every_endpoint_group` fails at `GetRequiredService<FmpClient>()` — the constructor parameter cannot be satisfied. Restore.

Then add a dummy twelfth property to `FmpClient` and re-run.
Expected: the count guard fails. Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/SecProfile.cs \
        src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/FmpClient.cs \
        src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs \
        tests/FmpDotNet.Tests/SecProfileTests.cs \
        tests/FmpDotNet.Tests/AddFmpTests.cs \
        tests/FmpDotNet.Tests/Fixtures/sec-profile.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/sec-profile.TSM.json
git commit -m "feat: add the fmp.SecFilings facade with the EDGAR registrant profile (#30)"
```

---

### Task 7: The two filing feeds

Two paths that share a row shape and differ only in what they filter on, plus the page-size cap that decides whether a caller's walk is complete.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/SecFilingsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-financials.head.json`

**Interfaces:**
- Consumes: `SecFiling`, `FmpJsonContext.Default.ListSecFiling` (Task 5); `DateRange.ThrowIfBackwards` (Task 4); `SecFilingsEndpoints` (Task 6).
- Produces: `SecFilingsEndpoints.MaxSecFilingPageSize` (`public const int` = 1000); `Get8KFilingsAsync(LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100, CancellationToken ct = default)` → `Task<IReadOnlyList<SecFiling>>`; `GetFilingsWithFinancialsAsync` with the identical signature.

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/sec-filings-financials.head.json` — the first five rows of `stable/sec-filings-financials?page=0&limit=5`, captured 2026-08-28, verbatim. Note `hasFinancials` is `true` on every row and `formType` is not:

```json
[
  {
    "symbol": "DNN",
    "cik": "0001063259",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 16:52:35",
    "formType": "6-K",
    "hasFinancials": true,
    "link": "https://www.sec.gov/Archives/edgar/data/1063259/000110465924030026/0001104659-24-030026-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1063259/000110465924030026/dnn-20231231xex99d1.htm"
  },
  {
    "symbol": "LL",
    "cik": "0001396033",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 16:38:37",
    "formType": "8-K",
    "hasFinancials": true,
    "link": "https://www.sec.gov/Archives/edgar/data/1396033/000095017024023941/0000950170-24-023941-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1396033/000095017024023941/ll-ex99_1.htm"
  },
  {
    "symbol": "TTEC",
    "cik": "0001013880",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 16:30:35",
    "formType": "8-K",
    "hasFinancials": true,
    "link": "https://www.sec.gov/Archives/edgar/data/1013880/000110465924030001/0001104659-24-030001-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1013880/000110465924030001/tm247637d1_ex99-1.htm"
  },
  {
    "symbol": "DTG",
    "cik": "0000936340",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 16:20:34",
    "formType": "8-K",
    "hasFinancials": true,
    "link": "https://www.sec.gov/Archives/edgar/data/936340/000093634024000090/0000936340-24-000090-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/936340/000093634024000090/dtegasexhibit991123123.htm"
  },
  {
    "symbol": "DTW",
    "cik": "0000936340",
    "filingDate": "2024-03-01 00:00:00",
    "acceptedDate": "2024-03-01 16:20:34",
    "formType": "8-K",
    "hasFinancials": true,
    "link": "https://www.sec.gov/Archives/edgar/data/936340/000093634024000090/0000936340-24-000090-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/936340/000093634024000090/dtegasexhibit991123123.htm"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/SecFilingsTests.cs`, inside the class. Add `using Microsoft.Extensions.Options;` and `using FmpDotNet.Endpoints;` at the top of the file:

```csharp
    // ---- the two feeds -----------------------------------------------------------------------------------------

    private static (SecFilingsEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SecFilingsEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task The_two_feeds_return_the_same_shape_and_differ_by_what_they_filter()
    {
        // Measured 2026-08-28 over 1,000 rows each. sec-filings-8k: formType "8-K" 1,000 times, hasFinancials
        // null 107 / false 725 / true 168. sec-filings-financials: formType "8-K" 861, "6-K" 137, "10-K" 2, and
        // hasFinancials true 1,000 times. One filters by form; the other by whether financials are attached —
        // which is why hasFinancials carries no information on the financials feed.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-8k.head.json")),
            StubHandler.Json(Binding.Fixture("sec-filings-financials.head.json")));

        var eightK = await endpoints.Get8KFilingsAsync(limit: 5);
        var financials = await endpoints.GetFilingsWithFinancialsAsync(limit: 5);

        Assert.All(eightK, r => Assert.Equal("8-K", r.FormType));
        Assert.All(eightK, r => Assert.Null(r.HasFinancials));

        Assert.All(financials, r => Assert.True(r.HasFinancials));
        Assert.Contains(financials, r => r.FormType == "6-K");
        Assert.Empty(Binding.Unbound(financials[0]));
    }

    [Fact]
    public async Task The_feeds_send_page_and_limit_and_omit_an_unset_range()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.Get8KFilingsAsync(page: 2, limit: 50);
        await endpoints.GetFilingsWithFinancialsAsync(
            new LocalDate(2025, 3, 1), new LocalDate(2025, 3, 5), page: 0, limit: 1000);

        Assert.Equal("/stable/sec-filings-8k", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("from=", handler.Requests[0].Query);
        Assert.DoesNotContain("to=", handler.Requests[0].Query);

        Assert.Equal("/stable/sec-filings-financials", handler.Requests[1].AbsolutePath);
        Assert.Contains("from=2025-03-01", handler.Requests[1].Query);
        Assert.Contains("to=2025-03-05", handler.Requests[1].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    [InlineData(5000)]
    public async Task A_limit_above_the_measured_cap_is_refused_on_both_feeds(int limit)
    {
        // Measured 2026-08-28: limit=2000 and limit=5000 each answered exactly 1,000 rows, HTTP 200, with
        // nothing in the response to say so. These feeds DO paginate — page 0 and page 1 return disjoint rows —
        // so a caller who asked for 5,000 and stepped `page` by 5,000 would read a fifth of the archive and be
        // told nothing at all.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        var first = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.Get8KFilingsAsync(limit: limit));
        var second = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFilingsWithFinancialsAsync(limit: limit));

        Assert.Equal("limit", first.ParamName);
        Assert.Equal("limit", second.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task A_negative_page_or_a_non_positive_limit_is_refused(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.Get8KFilingsAsync(page: page, limit: limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_filing_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, SecFilingsEndpoints.MaxSecFilingPageSize);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_on_both_feeds()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));
        var from = new LocalDate(2025, 3, 5);
        var to = new LocalDate(2025, 3, 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.Get8KFilingsAsync(from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFilingsWithFinancialsAsync(from, to));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task One_end_of_the_range_alone_is_allowed()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.Get8KFilingsAsync(from: new LocalDate(2025, 3, 1));
        await endpoints.Get8KFilingsAsync(to: new LocalDate(2025, 3, 5));

        Assert.Equal(2, handler.Requests.Count);
    }
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: build error — `SecFilingsEndpoints` has no `Get8KFilingsAsync`, `GetFilingsWithFinancialsAsync` or `MaxSecFilingPageSize`.

- [ ] **Step 4: Write the two methods**

Append to `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`, after `GetProfileByCikAsync`. The two bodies are written out rather than sharing a private helper: that is the ruling made in #29 for the duplicated `Batch` helper — two call sites is where extraction is still premature — and the searches in Task 8 are where the third occurrence appears and the extraction is taken.

```csharp
    /// <summary>The largest page any of the five filing paths will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>, for the same reason as
    /// <see cref="CompanyEndpoints.MaxMergerAcquisitionPageSize"/>: measured 2026-08-28, <c>limit=2000</c> and
    /// <c>limit=5000</c> each answered exactly 1,000 rows with HTTP 200 and nothing in the body to say the
    /// request had been trimmed. These feeds genuinely paginate — page 0 and page 1 return disjoint rows — so a
    /// caller who asks for 5,000 and advances <c>page</c> by 5,000 reads a fifth of the archive and is never
    /// told. Every method here therefore rejects a larger <c>limit</c> rather than passing it on to be
    /// clamped.</para></summary>
    public const int MaxSecFilingPageSize = 1000;

    /// <summary>The 8-K feed — <c>stable/sec-filings-8k</c>, every current-report filing across the market,
    /// newest first.
    ///
    /// <para><b>Filtered by form.</b> Measured 2026-08-28 over 1,000 rows, <c>formType</c> was <c>8-K</c> on all
    /// 1,000. <see cref="SecFiling.HasFinancials"/> varies here — null on 107, false on 725, true on 168 — and
    /// carries real information, unlike on
    /// <see cref="GetFilingsWithFinancialsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/>.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> filter
    /// <see cref="SecFiling.AcceptedDate"/>, not <see cref="SecFiling.FilingDate"/>.</b> A response therefore
    /// carries rows whose <c>FilingDate</c> falls outside the range you asked for — 21 of them on the measured
    /// five-day window. They are not errors and are not dropped; see <see cref="SecFiling"/> for the hypothesis
    /// test that established it.</para>
    ///
    /// <para>Both ends are optional and the endpoint answers without them.</para></summary>
    /// <param name="from">Start of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="to">End of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative,
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxSecFilingPageSize"/>, or both ends of the range
    /// were supplied with <paramref name="to"/> earlier than <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> Get8KFilingsAsync(
        LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/sec-filings-8k")
                .With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }

    /// <summary>The feed of filings that carry financial data — <c>stable/sec-filings-financials</c>.
    ///
    /// <para><b>Filtered by content, not by form.</b> Measured 2026-08-28 over 1,000 rows, <c>formType</c> was
    /// <c>8-K</c> 861 times, <c>6-K</c> 137 and <c>10-K</c> twice, while
    /// <see cref="SecFiling.HasFinancials"/> was <c>true</c> on all 1,000 — so that property is constant here
    /// and tells a caller nothing. This is the same row shape as
    /// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/> over a different
    /// selection.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> filter
    /// <see cref="SecFiling.AcceptedDate"/>.</b> This is the endpoint the hypothesis test was run against:
    /// 2025-03-01 to 2025-03-05 answered 722 rows — comfortably under the cap, so truncation cannot explain it —
    /// of which 16 carried a <c>FilingDate</c> past the requested <c>to</c>, and all 16 of those carried an
    /// <c>AcceptedDate</c> inside it, with zero rows in the whole response falling outside.</para></summary>
    /// <param name="from">Start of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="to">End of the range, inclusive, applied to <see cref="SecFiling.AcceptedDate"/>.
    /// Optional.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">As
    /// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> GetFilingsWithFinancialsAsync(
        LocalDate? from = null, LocalDate? to = null, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/sec-filings-financials")
                .With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: PASS. The two `[Theory]`s add six cases; the runner reports cases, not methods.

- [ ] **Step 6: Restore the cref Task 6 could not compile**

`Get8KFilingsAsync` now exists, so the placeholder in the `SecFilingsEndpoints` class summary can become a
real reference. In `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`, replace

```csharp
/// <c>Get8KFilingsAsync</c> and its neighbours answer filings; the three <c>FindCompany*</c> methods
```

with

```csharp
/// <see cref="Get8KFilingsAsync(LocalDate?, LocalDate?, int, int, CancellationToken)"/> and its neighbours
/// answer filings; the three <c>FindCompany*</c> methods
```

Leave the surrounding `<c>FindCompany*</c>` alone: it is a wildcard standing for three methods, not a
reference to one, so it stays a code span permanently. The compiler checks the restoration for free — an
unresolved cref is CS1574, which `TreatWarningsAsErrors` turns into a build error, so a green build IS the
assertion.

**This is the second instance of one mistake, and the plan has now been swept for a third.** Task 1 carried
a cref to `DirectoryEndpoints.GetSicCodesAsync`, which Task 2 creates; Task 6 carried this one. Every
`<see cref>` in every task was checked against the task that first defines its target — these two were the
only forward references, and both are now paired with a restoration step.

- [ ] **Step 7: Mutation-check the cap**

Change `ThrowIfGreaterThan(limit, MaxSecFilingPageSize)` to `ThrowIfGreaterThan(limit, 10_000)` on `Get8KFilingsAsync` only, and re-run.
Expected: `A_limit_above_the_measured_cap_is_refused_on_both_feeds` fails on the first assertion for all three inline values, and passes for the second — which is the point of checking both feeds in one test rather than one each. Restore.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs \
        tests/FmpDotNet.Tests/SecFilingsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-financials.head.json
git commit -m "feat: the 8-K and financials filing feeds (#30)"
```

---

### Task 8: Filing search by symbol, CIK and form type

Three paths, one private helper, and a required date range the compiler enforces because FMP would otherwise answer 400.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/SecFilingsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-search-symbol.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-search-cik.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-search-form-type.10-K.json`

**Interfaces:**
- Consumes: `SecFiling`, `DateRange.ThrowIfBackwards`, `MaxSecFilingPageSize`.
- Produces: `SearchBySymbolAsync(string symbol, LocalDate from, LocalDate to, int page = 0, int limit = 100, CancellationToken ct = default)` → `Task<IReadOnlyList<SecFiling>>`; `SearchByCikAsync(string cik, …)` and `SearchByFormTypeAsync(string formType, …)` with the same shape; `private Task<IReadOnlyList<SecFiling>> SearchAsync(string path, string parameter, string value, LocalDate from, LocalDate to, int page, int limit, CancellationToken ct)`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/sec-filings-search-symbol.AAPL.json` — the first five rows of `stable/sec-filings-search/symbol?symbol=AAPL&from=2025-01-01&to=2025-12-31`, captured 2026-08-28, verbatim. **There is no `hasFinancials` key on any row** — that absence is the shape, not an omission from the capture:

```json
[
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "filingDate": "2025-12-05 00:00:00",
    "acceptedDate": "2025-12-05 16:31:42",
    "formType": "8-K",
    "link": "https://www.sec.gov/Archives/edgar/data/320193/000114036125044561/0001140361-25-044561-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/320193/000114036125044561/ef20060722_8k.htm"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "filingDate": "2025-11-14 00:00:00",
    "acceptedDate": "2025-11-14 18:30:15",
    "formType": "4",
    "link": "https://www.sec.gov/Archives/edgar/data/320193/000146235625000012/0001462356-25-000012-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/320193/000146235625000012/xslF345X05/wk-form4_1763163012.xml"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "filingDate": "2025-11-14 00:00:00",
    "acceptedDate": "2025-11-14 16:10:12",
    "formType": "25-NSE",
    "link": "https://www.sec.gov/Archives/edgar/data/320193/000135445725001138/0001354457-25-001138-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/320193/000135445725001138/xslF25X02/primary_doc.xml"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "filingDate": "2025-11-12 00:00:00",
    "acceptedDate": "2025-11-12 18:30:10",
    "formType": "4",
    "link": "https://www.sec.gov/Archives/edgar/data/320193/000163198225000011/0001631982-25-000011-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/320193/000163198225000011/xslF345X05/wk-form4_1762990206.xml"
  },
  {
    "symbol": "AAPL",
    "cik": "0000320193",
    "filingDate": "2025-10-31 00:00:00",
    "acceptedDate": "2025-10-31 06:01:26",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/320193/000032019325000079/0000320193-25-000079-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/320193/000032019325000079/aapl-20250927.htm"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/sec-filings-search-cik.AAPL.json` — **byte-identical to the file above.** It is the response to `stable/sec-filings-search/cik?cik=0000320193&from=2025-01-01&to=2025-12-31`, captured in the same pass, and the two paths answered the same rows. Copy the file rather than shortening it: two files that happen to be identical is the measurement, and a test asserts it.

`tests/FmpDotNet.Tests/Fixtures/sec-filings-search-form-type.10-K.json` — the first five rows of `stable/sec-filings-search/form-type?formType=10-K&from=2025-01-01&to=2025-01-31`, captured 2026-08-28, verbatim:

```json
[
  {
    "symbol": "JVA",
    "cik": "0001007019",
    "filingDate": "2025-01-31 00:00:00",
    "acceptedDate": "2025-01-31 17:25:25",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/1007019/000149315225004500/0001493152-25-004500-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1007019/000149315225004500/form10-k.htm"
  },
  {
    "symbol": "ISRG",
    "cik": "0001035267",
    "filingDate": "2025-01-31 00:00:00",
    "acceptedDate": "2025-01-31 17:20:49",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/1035267/000103526725000017/0001035267-25-000017-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1035267/000103526725000017/isrg-20241231.htm"
  },
  {
    "symbol": "INTC",
    "cik": "0000050863",
    "filingDate": "2025-01-31 00:00:00",
    "acceptedDate": "2025-01-31 17:13:51",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/50863/000005086325000009/0000050863-25-000009-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/50863/000005086325000009/intc-20241228.htm"
  },
  {
    "symbol": "CCZ",
    "cik": "0001166691",
    "filingDate": "2025-01-31 00:00:00",
    "acceptedDate": "2025-01-31 16:10:33",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/1166691/000116669125000011/0001166691-25-000011-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1166691/000116669125000011/cmcsa-20241231.htm"
  },
  {
    "symbol": "CMCSA",
    "cik": "0001166691",
    "filingDate": "2025-01-31 00:00:00",
    "acceptedDate": "2025-01-31 16:10:33",
    "formType": "10-K",
    "link": "https://www.sec.gov/Archives/edgar/data/1166691/000116669125000011/0001166691-25-000011-index.htm",
    "finalLink": "https://www.sec.gov/Archives/edgar/data/1166691/000116669125000011/cmcsa-20241231.htm"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/SecFilingsTests.cs`, inside the class:

```csharp
    // ---- the three searches ------------------------------------------------------------------------------------

    [Fact]
    public async Task The_search_paths_omit_has_financials_entirely()
    {
        // The one-field difference that decides whether this is one record or two. On the two feeds the field is
        // present and sometimes null; here it is absent from the payload, so `null` means "this endpoint does not
        // say" rather than "FMP says no". Both read as null in C#, which is why the distinction lives in the
        // documentation on SecFiling.HasFinancials and in this test rather than in the type.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-search-symbol.AAPL.json")));

        var rows = await endpoints.SearchBySymbolAsync(
            "AAPL", new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Null(r.HasFinancials));
        Assert.Equal(["HasFinancials"], Binding.Unbound(rows[0]));
    }

    [Fact]
    public async Task A_search_row_binds_its_seven_fields_and_its_form_types_vary()
    {
        // formType is a raw string and not an enum for exactly this reason: one symbol over one year returned
        // "8-K", "4", "25-NSE" and "10-K" in five rows. EDGAR defines hundreds more.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-search-symbol.AAPL.json")));

        var rows = await endpoints.SearchBySymbolAsync(
            "AAPL", new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));

        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("0000320193", rows[0].Cik);
        Assert.Equal(new LocalDate(2025, 12, 5), rows[0].FilingDate);
        Assert.Equal(Instant.FromUtc(2025, 12, 5, 21, 31, 42), rows[0].AcceptedDate);
        Assert.Equal(["8-K", "4", "25-NSE", "4", "10-K"], rows.Select(r => r.FormType));
    }

    [Fact]
    public async Task Searching_by_symbol_and_by_cik_answers_the_same_rows()
    {
        // Measured 2026-08-28: sec-filings-search/symbol?symbol=AAPL and sec-filings-search/cik?cik=0000320193
        // over the same range returned byte-identical bodies, and the unpadded CIK answered the same 80 rows as
        // the padded one. The two fixtures are the same file for that reason, and this asserts it rather than
        // leaving a reader to wonder whether one was copied by mistake.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-search-symbol.AAPL.json")),
            StubHandler.Json(Binding.Fixture("sec-filings-search-cik.AAPL.json")));
        var from = new LocalDate(2025, 1, 1);
        var to = new LocalDate(2025, 12, 31);

        var bySymbol = await endpoints.SearchBySymbolAsync("AAPL", from, to);
        var byCik = await endpoints.SearchByCikAsync("0000320193", from, to);

        Assert.Equal(bySymbol, byCik);
    }

    [Fact]
    public async Task Each_search_sends_its_own_path_and_its_own_parameter()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));
        var from = new LocalDate(2025, 1, 1);
        var to = new LocalDate(2025, 1, 31);

        await endpoints.SearchBySymbolAsync("AAPL", from, to);
        await endpoints.SearchByCikAsync("320193", from, to);
        await endpoints.SearchByFormTypeAsync("10-K", from, to, page: 1, limit: 25);

        Assert.Equal("/stable/sec-filings-search/symbol", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("from=2025-01-01", handler.Requests[0].Query);
        Assert.Contains("to=2025-01-31", handler.Requests[0].Query);

        Assert.Equal("/stable/sec-filings-search/cik", handler.Requests[1].AbsolutePath);
        Assert.Contains("cik=320193", handler.Requests[1].Query);

        Assert.Equal("/stable/sec-filings-search/form-type", handler.Requests[2].AbsolutePath);
        Assert.Contains("formType=10-K", handler.Requests[2].Query);
        Assert.Contains("page=1", handler.Requests[2].Query);
        Assert.Contains("limit=25", handler.Requests[2].Query);
    }

    [Fact]
    public async Task A_form_type_search_returns_many_issuers_for_one_form()
    {
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-search-form-type.10-K.json")));

        var rows = await endpoints.SearchByFormTypeAsync(
            "10-K", new LocalDate(2025, 1, 1), new LocalDate(2025, 1, 31));

        Assert.All(rows, r => Assert.Equal("10-K", r.FormType));
        Assert.Equal(4, rows.Select(r => r.Cik).Distinct().Count());
        // CCZ and CMCSA are the same accession under two tickers, the way SBC and SBCWW are on the 8-K feed.
        Assert.Equal(rows[3].Link, rows[4].Link);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Every_search_refuses_a_blank_value_before_spending_a_call(string blank)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));
        var from = new LocalDate(2025, 1, 1);
        var to = new LocalDate(2025, 1, 31);

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.SearchBySymbolAsync(blank, from, to));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.SearchByCikAsync(blank, from, to));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.SearchByFormTypeAsync(blank, from, to));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Every_search_refuses_a_backwards_range()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));
        var from = new LocalDate(2025, 1, 31);
        var to = new LocalDate(2025, 1, 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchBySymbolAsync("AAPL", from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchByCikAsync("320193", from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchByFormTypeAsync("10-K", from, to));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Every_search_refuses_a_limit_above_the_cap()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));
        var from = new LocalDate(2025, 1, 1);
        var to = new LocalDate(2025, 1, 31);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchBySymbolAsync("AAPL", from, to, limit: 1001));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchByCikAsync("320193", from, to, limit: 5000));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchByFormTypeAsync("10-K", from, to, page: -1));

        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: build error — the three `Search*Async` methods do not exist.

- [ ] **Step 4: Write the three methods and the helper**

Append to `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`, after `GetFilingsWithFinancialsAsync`. Unlike the two feeds, these three share a private helper — **this is the third occurrence, which is the point the #29 ruling names for extraction**:

```csharp
    /// <summary>Filings for one symbol over a date range — <c>stable/sec-filings-search/symbol</c>.
    ///
    /// <para>Every form type, not just 8-Ks: measured 2026-08-28, AAPL over 2025 answered 80 rows including
    /// <c>8-K</c>, <c>4</c>, <c>25-NSE</c> and <c>10-K</c>.</para>
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> are required, and that is FMP's rule rather
    /// than a choice made here.</b> The endpoint reveals its requirements one at a time: <c>symbol</c> alone
    /// answers 400 "Invalid or missing query parameter - from", and <c>symbol</c> with <c>from</c> answers the
    /// same for <c>to</c>. An optional parameter would ship a signature whose default can only fail, so the
    /// compiler enforces what FMP would otherwise charge a call to tell you.</para>
    ///
    /// <para><b>The range filters <see cref="SecFiling.AcceptedDate"/>.</b> Measured on the sibling form-type
    /// path 2026-08-28: 398 rows over a five-day window, of which 7 carried a <c>FilingDate</c> outside
    /// it.</para>
    ///
    /// <para>No <see cref="SecFiling.HasFinancials"/> on this path — the field is absent from the payload, so it
    /// binds null on every row.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="from">Start of the range, inclusive. Required.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Empty for an unknown symbol. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative,
    /// <paramref name="limit"/> is out of range, or <paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchBySymbolAsync(
        string symbol, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return SearchAsync("stable/sec-filings-search/symbol", "symbol", symbol, from, to, page, limit, ct);
    }

    /// <summary>Filings for one Central Index Key over a date range — <c>stable/sec-filings-search/cik</c>.
    ///
    /// <para>The same rows as <see cref="SearchBySymbolAsync"/> where both identify the same filer: measured
    /// 2026-08-28, <c>symbol=AAPL</c> and <c>cik=0000320193</c> over 2025 returned byte-identical bodies of 80
    /// rows, and the unpadded <c>320193</c> returned the same 80.</para>
    ///
    /// <para>Reach for this one when the filer has no ticker — most SEC registrants do not.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded.</param>
    /// <param name="from">Start of the range, inclusive. Required — see
    /// <see cref="SearchBySymbolAsync"/>.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="SearchBySymbolAsync"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchByCikAsync(
        string cik, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return SearchAsync("stable/sec-filings-search/cik", "cik", cik, from, to, page, limit, ct);
    }

    /// <summary>Every filing of one form type across the market over a date range —
    /// <c>stable/sec-filings-search/form-type</c>.
    ///
    /// <para><paramref name="formType"/> is EDGAR's own spelling — <c>"10-K"</c>, <c>"8-K"</c>, <c>"4"</c>,
    /// <c>"25-NSE"</c>. Not validated here and not an enum, for the reason on
    /// <see cref="SecFiling.FormType"/>: EDGAR defines hundreds and a value this SDK has never seen must not
    /// cost the caller the call.</para>
    ///
    /// <para>Whole-market and therefore wide: measured 2026-08-28, <c>10-K</c> over one January month answered
    /// 398 rows, and over a recent 90-day window it filled the default page. Page it, or narrow the
    /// range.</para></summary>
    /// <param name="formType">The EDGAR form type, spelled as EDGAR spells it.</param>
    /// <param name="from">Start of the range, inclusive. Required — see
    /// <see cref="SearchBySymbolAsync"/>.</param>
    /// <param name="to">End of the range, inclusive. Required.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxSecFilingPageSize"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's filings, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="formType"/> is null, empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="SearchBySymbolAsync"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SecFiling>> SearchByFormTypeAsync(
        string formType, LocalDate from, LocalDate to, int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formType);
        return SearchAsync("stable/sec-filings-search/form-type", "formType", formType, from, to, page, limit, ct);
    }

    /// <summary>The body the three <c>sec-filings-search/*</c> paths share: one required identifier, one
    /// required range, and the page-size cap.
    ///
    /// <para>Extracted rather than written three times, which is the trigger #29 named when it left the
    /// duplicated <c>Batch</c> helper alone at two call sites. The two feeds above are still written out for the
    /// same reason: they are two.</para></summary>
    private Task<IReadOnlyList<SecFiling>> SearchAsync(
        string path, string parameter, string value, LocalDate from, LocalDate to, int page, int limit,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSecFilingPageSize);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest(path)
                .With(parameter, value).With("from", from).With("to", to).With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListSecFiling, ct);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecFilingsTests`
Expected: PASS, with eight new methods for the three search paths.

- [ ] **Step 6: Mutation-check the path dispatch**

Change `SearchByCikAsync` to pass `"stable/sec-filings-search/symbol"` and re-run.
Expected: `Each_search_sends_its_own_path_and_its_own_parameter` fails on the second path assertion. Note that `Searching_by_symbol_and_by_cik_answers_the_same_rows` **still passes** — because the stub answers whatever it is handed. That is worth seeing: a fixture-equality test cannot police routing, which is why the request assertions are a separate test. Restore.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs \
        tests/FmpDotNet.Tests/SecFilingsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-search-symbol.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-search-cik.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-search-form-type.10-K.json
git commit -m "feat: filing search by symbol, CIK and form type (#30)"
```

---

### Task 9: Company search by symbol, CIK and name

The last three paths. They return the classification row rather than a filing, and the third of them takes no `limit` because the endpoint ignores one.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/SecProfileTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-symbol.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-cik.AAPL.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-name.Apple.json`

**Interfaces:**
- Consumes: `IndustryClassification`, `FmpJsonContext.Default.ListIndustryClassification` (Task 1); `SecFilingsEndpoints` (Task 6).
- Produces: `FindCompanyBySymbolAsync(string symbol, CancellationToken ct = default)`, `FindCompanyByCikAsync(string cik, …)`, `FindCompanyByNameAsync(string company, …)`, each → `Task<IReadOnlyList<IndustryClassification>>`; `private Task<IReadOnlyList<IndustryClassification>> FindCompanyAsync(string path, string parameter, string value, CancellationToken ct)`.

- [ ] **Step 1: Write the three fixtures**

`tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-symbol.AAPL.json` — `stable/sec-filings-company-search/symbol?symbol=AAPL`, captured 2026-08-28, verbatim. One row, and the address is **already joined** — no brackets:

```json
[
  {
    "symbol": "AAPL",
    "name": "APPLE INC.",
    "cik": "0000320193",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "ONE APPLE PARK WAY, CUPERTINO CA 95014",
    "phoneNumber": "(408) 996-1010"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-cik.AAPL.json` — `stable/sec-filings-company-search/cik?cik=0000320193`, same pass. Byte-identical to the file above, and asserted to be:

```json
[
  {
    "symbol": "AAPL",
    "name": "APPLE INC.",
    "cik": "0000320193",
    "sicCode": "3571",
    "industryTitle": "ELECTRONIC COMPUTERS",
    "businessAddress": "ONE APPLE PARK WAY, CUPERTINO CA 95014",
    "phoneNumber": "(408) 996-1010"
  }
]
```

`tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-name.Apple.json` — the first five rows of `stable/sec-filings-company-search/name?company=Apple`, captured 2026-08-28, verbatim. Four of five carry the literal `"None"` symbol; four of five carry blank `sicCode` and `industryTitle`; one row matches on `"APPLING"` rather than `"APPLE"`:

```json
[
  {
    "symbol": "None",
    "name": "APPLE ISPORTS, INC.",
    "cik": "0001945176",
    "sicCode": "",
    "industryTitle": "",
    "businessAddress": "FIRST FLOOR, 9/30 PROHASKY STREET, PORT MELBOURNE C3 3702",
    "phoneNumber": "61414532751"
  },
  {
    "symbol": "None",
    "name": "GREEN APPLE VENTURES LLC",
    "cik": "0001509068",
    "sicCode": "",
    "industryTitle": "",
    "businessAddress": "3250 NE 1ST AVENUE, #317, MIAMI FL 33137",
    "phoneNumber": "954-599-3322"
  },
  {
    "symbol": "APLE",
    "name": "APPLE HOSPITALITY REIT, INC.",
    "cik": "0001418121",
    "sicCode": "6798",
    "industryTitle": "REAL ESTATE INVESTMENT TRUSTS",
    "businessAddress": "814 EAST MAIN STREET, RICHMOND VA 23219",
    "phoneNumber": "804.344.8121"
  },
  {
    "symbol": "None",
    "name": "APPLE CAPITAL GROUP, INC.",
    "cik": "0001718804",
    "sicCode": "",
    "industryTitle": "",
    "businessAddress": "201 E ABRAM ST., ARLINGTON VA 76010",
    "phoneNumber": "866-611-7457"
  },
  {
    "symbol": "None",
    "name": "APPLING PARTNERS, LLC",
    "cik": "0001489715",
    "sicCode": "",
    "industryTitle": "",
    "businessAddress": "211 KING STREET, CHARLESTON SC 29401",
    "phoneNumber": "843-722-2615"
  }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/SecProfileTests.cs`, inside the class. Add `using FmpDotNet.Models;` at the top if it is not already there:

```csharp
    // ---- the three company searches ----------------------------------------------------------------------------

    [Fact]
    public async Task Company_search_returns_the_classification_row_not_a_filing()
    {
        // Same seven fields fmp.Directory and fmp.Search serve, which is why these three methods return
        // IndustryClassification rather than a type of their own. Measured 2026-08-28 for CIK 0000070858: all six
        // non-address fields were byte-identical across all-industry-classification and this path.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-company-search-symbol.AAPL.json")));

        var rows = await endpoints.FindCompanyBySymbolAsync("AAPL");

        var row = Assert.Single(rows);
        Assert.Empty(Binding.Unbound(row));
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal("APPLE INC.", row.Name);
        Assert.Equal("0000320193", row.Cik);
        Assert.Equal("3571", row.SicCode);
        Assert.Equal("ELECTRONIC COMPUTERS", row.IndustryTitle);
        Assert.Equal("(408) 996-1010", row.PhoneNumber);
    }

    [Fact]
    public async Task This_path_sends_the_address_already_joined_and_the_converter_leaves_it_alone()
    {
        // Three of the five IndustryClassification paths never bracket, and this is one of them — measured
        // 2026-08-28, sec-filings-company-search/name answered 0 bracketed values in 976 rows. The converter's
        // pass-through branch is what makes one record safe across both conventions.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-company-search-symbol.AAPL.json")));

        var rows = await endpoints.FindCompanyBySymbolAsync("AAPL");

        Assert.Equal("ONE APPLE PARK WAY, CUPERTINO CA 95014", rows[0].BusinessAddress);
    }

    [Fact]
    public async Task Symbol_and_cik_answer_the_same_row()
    {
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-company-search-symbol.AAPL.json")),
            StubHandler.Json(Binding.Fixture("sec-filings-company-search-cik.AAPL.json")));

        var bySymbol = await endpoints.FindCompanyBySymbolAsync("AAPL");
        var byCik = await endpoints.FindCompanyByCikAsync("0000320193");

        Assert.Equal(bySymbol, byCik);
    }

    [Fact]
    public async Task A_name_search_matches_loosely_and_leaves_unclassified_filers_blank()
    {
        // Measured 2026-08-28: company=Apple, company=apple and company=Appl each answered the same 52 rows, so
        // matching is case-insensitive and not an exact-name comparison. company=a answered 0 rows, so very short
        // queries are rejected rather than matching broadly. The exact rule was not established and the SDK does
        // not assert one — this test pins only what was seen.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-company-search-name.Apple.json")));

        var rows = await endpoints.FindCompanyByNameAsync("Apple");

        Assert.Equal(5, rows.Count);
        // "APPLING PARTNERS, LLC" contains no "APPLE" at all, so the match is looser than a substring on the
        // query. A caller must not assume every row contains what they typed.
        Assert.Contains(rows, r => !r.Name!.Contains("APPLE", StringComparison.OrdinalIgnoreCase));
        // Most filers matched by name are unclassified: four of these five carry a blank SIC code and title.
        Assert.Equal(["IndustryTitle", "SicCode"], Binding.Unbound(rows[0]));
        Assert.Equal(4, rows.Count(r => r.SicCode == ""));
        Assert.Equal(4, rows.Count(r => r.Symbol == "None"));
    }

    [Fact]
    public async Task Each_company_search_sends_its_own_path_and_parameter_and_no_limit()
    {
        // No `limit` on any of the three signatures. Measured 2026-08-28: company=Apple answered 52 rows both
        // with and without limit=5 — the endpoint returns its whole result set every time. A parameter the
        // endpoint ignores would let a caller believe they had asked for five rows while holding 52, which is
        // the ruling already made for CompanyEndpoints.SearchMergersAcquisitionsAsync.
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.FindCompanyBySymbolAsync("AAPL");
        await endpoints.FindCompanyByCikAsync("320193");
        await endpoints.FindCompanyByNameAsync("Apple");

        Assert.Equal("/stable/sec-filings-company-search/symbol", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Equal("/stable/sec-filings-company-search/cik", handler.Requests[1].AbsolutePath);
        Assert.Contains("cik=320193", handler.Requests[1].Query);
        Assert.Equal("/stable/sec-filings-company-search/name", handler.Requests[2].AbsolutePath);
        Assert.Contains("company=Apple", handler.Requests[2].Query);
        Assert.All(handler.Requests, uri => Assert.DoesNotContain("limit=", uri.Query));
        Assert.All(handler.Requests, uri => Assert.DoesNotContain("page=", uri.Query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Every_company_search_refuses_a_blank_value(string blank)
    {
        // Each of the three answers 400 naming its own parameter when called bare, measured 2026-08-28.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindCompanyBySymbolAsync(blank));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindCompanyByCikAsync(blank));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindCompanyByNameAsync(blank));

        Assert.Empty(handler.Requests);
    }
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecProfileTests`
Expected: build error — the three `FindCompany*Async` methods do not exist.

- [ ] **Step 4: Write the three methods and the helper**

Append to `src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs`, after the private `SearchAsync` helper:

```csharp
    /// <summary>The registrant behind one ticker — <c>stable/sec-filings-company-search/symbol</c>.
    ///
    /// <para><b>Returns <see cref="IndustryClassification"/>, the same seven-field row
    /// <see cref="DirectoryEndpoints.GetIndustryClassificationsAsync"/> and
    /// <see cref="SearchEndpoints.FindIndustryClassificationAsync"/> serve.</b> Measured 2026-08-28 for CIK
    /// <c>0000070858</c>, this path and <c>all-industry-classification</c> returned byte-identical values for
    /// all six non-address fields — the same data, not merely the same field names. The address differs only in
    /// encoding, and <see cref="Serialization.BusinessAddressJsonConverter"/> makes that invisible.</para>
    ///
    /// <para><b>No <c>limit</c> and no <c>page</c>, because the endpoint honours neither.</b> Measured
    /// 2026-08-28, the name variant answered 52 rows with and without <c>limit=5</c>. Take what comes back and
    /// page it yourself.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Empty when nothing matches. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or blank — FMP answers 400
    /// naming the parameter, so it is raised here instead of bought.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyBySymbolAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return FindCompanyAsync("stable/sec-filings-company-search/symbol", "symbol", symbol, ct);
    }

    /// <summary>The registrant behind one Central Index Key —
    /// <c>stable/sec-filings-company-search/cik</c>.
    ///
    /// <para>The route for the majority of SEC registrants, which have no ticker. Measured 2026-08-28, the
    /// padded and unpadded forms of the CIK each answered the same single row, identical to what
    /// <see cref="FindCompanyBySymbolAsync"/> answers for the same filer.</para></summary>
    /// <param name="cik">The SEC Central Index Key, padded or unpadded.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyByCikAsync(
        string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return FindCompanyAsync("stable/sec-filings-company-search/cik", "cik", cik, ct);
    }

    /// <summary>Registrants whose name matches — <c>stable/sec-filings-company-search/name</c>.
    ///
    /// <para><b>Matching is loose and its exact rule was not established.</b> Measured 2026-08-28:
    /// <c>Apple</c>, <c>apple</c> and <c>Appl</c> each answered the same 52 rows, so it is case-insensitive and
    /// not an exact comparison; the results include <c>APPLING PARTNERS, LLC</c>, which contains no "apple" at
    /// all, so it is looser than a substring test. A single character, <c>a</c>, answered <b>0</b> rows, so very
    /// short queries are rejected rather than matching broadly. This SDK records what it saw and asserts no
    /// rule.</para>
    ///
    /// <para><b>Most rows come back unclassified.</b> Four of the first five carry a blank
    /// <see cref="IndustryClassification.SicCode"/> and <see cref="IndustryClassification.IndustryTitle"/>, and
    /// four carry the literal string <c>"None"</c> as their symbol — see
    /// <see cref="IndustryClassification.Symbol"/>.</para>
    ///
    /// <para>No <c>limit</c>: measured 2026-08-28, <c>company=Apple</c> answered 52 rows with and without
    /// one.</para></summary>
    /// <param name="company">The name to match. Matched loosely — see above.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching registrants, unpaged. Empty when nothing matches, including for a query FMP considers
    /// too short. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="company"/> is null, empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryClassification>> FindCompanyByNameAsync(
        string company, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        return FindCompanyAsync("stable/sec-filings-company-search/name", "company", company, ct);
    }

    /// <summary>The body the three <c>sec-filings-company-search/*</c> paths share: one required parameter and
    /// nothing else. Extracted at three call sites, for the reason on <see cref="SearchAsync"/>.</summary>
    private Task<IReadOnlyList<IndustryClassification>> FindCompanyAsync(
        string path, string parameter, string value, CancellationToken ct) =>
        transport.GetListAsync(
            new FmpRequest(path).With(parameter, value),
            FmpJsonContext.Default.ListIndustryClassification, ct);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~SecProfileTests`
Expected: PASS, with six new methods for the three company-search paths.

- [ ] **Step 6: Mutation-check the omitted parameters**

Add `.With("limit", 100)` to `FindCompanyAsync` and re-run.
Expected: `Each_company_search_sends_its_own_path_and_parameter_and_no_limit` fails on the `limit=` assertion. The parameter would be accepted by FMP and ignored, so nothing else in the suite — or in production — would ever notice. Restore.

- [ ] **Step 7: Run the whole unit suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: everything passes except `EndpointCoverageTests.The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls`, which now reports all twelve new paths and a headline of 126. Read its failure output and check that all twelve are listed — this is the earliest point at which a path that was written but never reached would show up as missing. It stays red until Task 11.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Endpoints/SecFilingsEndpoints.cs \
        tests/FmpDotNet.Tests/SecProfileTests.cs \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-symbol.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-cik.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/sec-filings-company-search-name.Apple.json
git commit -m "feat: company search by symbol, CIK and name (#30)"
```

---

### Task 10: Teach the live sweep to call the twelve new paths properly

The sweep finds new endpoints by reflection, so all twelve are already in it. What it cannot infer is what to *ask* them, and its `string` arm defaults every unknown parameter name to `"AAPL"` — which does not throw, does not fail any test, and records `outcome empty` as an endpoint's permanent baseline.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`
- Modify: `tests/FmpDotNet.SmokeTests/OrdinaryEndpointShapeTests.cs`

**Interfaces:**
- Consumes: `SecFilingsEndpoints`, `DirectoryEndpoints.MaxIndustryClassificationPageSize`.
- Produces: `LiveApi.RangeStart` (`LocalDate`), `LiveApi.CompanyNameQuery`, `LiveApi.FormType`, `LiveApi.SicCode` (all `const string` except `RangeStart`); `Observation.Rows` (`int`, trailing positional parameter defaulting to 0).

- [ ] **Step 1: Write the failing keyless guards**

Append to `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs`, inside the class. These run on every push with no key and no request, which is the whole point:

```csharp
    [Fact]
    public void The_sweep_asks_the_filing_searches_for_a_range_wider_than_one_day()
    {
        // Probe.Argument dispatches LocalDate on TYPE alone, so `from` and `to` both became SettledWeekday and
        // the three sec-filings-search paths were probed over a single day. Measured 2026-08-28:
        // sec-filings-search/symbol?symbol=AAPL over 2026-08-21..2026-08-21 answered 0 rows, while the same call
        // over 2026-05-30..2026-08-28 answered 7. A zero-row answer records `outcome empty` with no properties,
        // and every run after it agrees — the endpoint would be probed weekly and never checked.
        var search = typeof(Endpoints.SecFilingsEndpoints)
            .GetMethod(nameof(Endpoints.SecFilingsEndpoints.SearchBySymbolAsync))!;
        var from = (NodaTime.LocalDate)Probe.Argument(search.GetParameters()[1]);
        var to = (NodaTime.LocalDate)Probe.Argument(search.GetParameters()[2]);

        Assert.True(NodaTime.Period.DaysBetween(from, to) >= 60,
            $"The sweep would probe the filing searches over {NodaTime.Period.DaysBetween(from, to)} day(s). "
            + "A short window answers zero rows and records an empty baseline that agrees with itself forever.");
    }

    [Fact]
    public void The_sweep_asks_each_new_search_for_a_value_of_its_own_kind()
    {
        // The string arm of Probe.Argument ends in `_ => LiveApi.Symbol`, so an unrecognised parameter name is
        // NOT an error — it silently becomes "AAPL". company=AAPL, formType=AAPL and sicCode=AAPL each answer
        // HTTP 200 with an empty array rather than an error, so the other coverage test in this file cannot see
        // the problem: the argument IS synthesisable, it is just meaningless. Same failure LiveApi.Exchange and
        // LiveApi.AcquirerNameQuery were written for.
        var filings = typeof(Endpoints.SecFilingsEndpoints);

        Assert.Equal("Apple", Probe.Argument(
            filings.GetMethod(nameof(Endpoints.SecFilingsEndpoints.FindCompanyByNameAsync))!.GetParameters()[0]));
        Assert.Equal("10-K", Probe.Argument(
            filings.GetMethod(nameof(Endpoints.SecFilingsEndpoints.SearchByFormTypeAsync))!.GetParameters()[0]));
        Assert.Equal("3571", Probe.Argument(
            typeof(Endpoints.SearchEndpoints)
                .GetMethod(nameof(Endpoints.SearchEndpoints.FindIndustryClassificationAsync))!
                .GetParameters()[2]));
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/FmpDotNet.SmokeTests --filter FullyQualifiedName~SweepCoverageTests`
Expected: both new tests fail — the first because `from` and `to` are the same date, the second because all three arguments come back as `"AAPL"`. No key is needed and no request goes out.

- [ ] **Step 3: Add the four `LiveApi` constants**

In `tests/FmpDotNet.SmokeTests/LiveApi.cs`, after `SettledWeekday`:

```csharp
    /// <summary>The start of the range every date-ranged probe asks for — ninety days before
    /// <see cref="SettledWeekday"/>.
    ///
    /// <para><b>Named rather than falling out of the <c>LocalDate</c> type case, because that case is silently
    /// wrong for anything sparse.</b> <c>Probe.Argument</c> dispatched <c>LocalDate</c> on type alone, so
    /// <c>from</c> and <c>to</c> both became <see cref="SettledWeekday"/> — a range of one day. Measured
    /// 2026-08-28, <c>sec-filings-search/symbol?symbol=AAPL</c> over a single settled weekday answered
    /// <b>0 rows</b>; the same call over ninety days answered <b>7</b>. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties, and matches that baseline every week thereafter — the silent
    /// green this suite exists to prevent.</para>
    ///
    /// <para>Ninety days rather than a year: it is enough for one issuer's Form 4s and 8-Ks to appear, and short
    /// enough that the whole-market probes it also widens — the earnings and economic calendars — stay a
    /// download rather than an outage.</para></summary>
    public static LocalDate RangeStart => SettledWeekday.PlusDays(-90);

    /// <summary>The name the SEC company search is probed with.
    ///
    /// <para>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>: <c>sec-filings-company-search/name</c> matches company names, so
    /// <c>company=AAPL</c> would answer an empty array with HTTP 200 and record <c>rows 0</c> as the baseline.
    /// <c>"Apple"</c> answered 52 rows on 2026-08-28. Separate from <see cref="AcquirerNameQuery"/> although
    /// both spell the same word — they are probing different endpoints, and a future change to one must not
    /// silently move the other.</para></summary>
    public const string CompanyNameQuery = "Apple";

    /// <summary>The EDGAR form type the form-type filing search is probed with.
    ///
    /// <para><c>"10-K"</c> because it is filed by every domestic issuer, so any window of ninety days contains
    /// some — measured 2026-08-28, a recent ninety-day window filled the default page of 100 rows. An
    /// unrecognised form type answers an empty array with HTTP 200 rather than an error.</para></summary>
    public const string FormType = "10-K";

    /// <summary>The SIC code the classification search is probed with — <c>"3571"</c>, "ELECTRONIC COMPUTERS".
    ///
    /// <para>Chosen to agree with <see cref="Symbol"/> and <see cref="Cik"/>: <c>industry-classification-search</c>
    /// takes all three and narrows on them, and measured 2026-08-28,
    /// <c>symbol=AAPL&amp;cik=320193&amp;sicCode=3571</c> answered one row. A SIC code that contradicted the
    /// other two would answer nothing and record an empty baseline.</para>
    ///
    /// <para>Four characters, which is how the classification paths spell it —
    /// <c>standard-industrial-classification-list</c> strips the leading zero on codes below 1000 and this one
    /// has none, so the two agree here.</para></summary>
    public const string SicCode = "3571";
```

- [ ] **Step 4: Add the `Probe.Argument` arms**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, extend the `string` switch with three arms, placed before the `_ =>` default:

```csharp
                "company" => LiveApi.CompanyNameQuery,
                "formType" => LiveApi.FormType,
                "sicCode" => LiveApi.SicCode,
```

and replace the single `LocalDate` line

```csharp
        if (type == typeof(LocalDate)) return LiveApi.SettledWeekday;
```

with a name-dispatched pair:

```csharp
        // Dispatched on NAME, not just type, for the reason the string arm is: `from` and `to` both taking
        // SettledWeekday makes every range one day wide, and a one-day window answers zero rows on anything
        // sparse. See LiveApi.RangeStart for the measurement that forced this.
        if (type == typeof(LocalDate))
            return parameter.Name switch
            {
                "from" => LiveApi.RangeStart,
                _ => LiveApi.SettledWeekday,
            };
```

The `_ =>` default is kept rather than throwing on an unknown date parameter: `to` is the only other name in the SDK today, and the fall-through is the correct value for it.

- [ ] **Step 5: Run the keyless guards again**

Run: `dotnet test tests/FmpDotNet.SmokeTests`
Expected: PASS. Every live test skips for want of a key, which is what CI does too.

- [ ] **Step 6: Add the row count to `Observation` and the live tripwire**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, add a trailing parameter to the `Observation` record and document it:

```csharp
/// <param name="Rows">How many rows came back, or 0 for any outcome other than <see cref="Probe.Rows"/>. Not
/// written to a baseline — a row count changes daily and recording one would make every run drift against the
/// last. It is here for the one assertion that has to be about volume rather than shape: see
/// <c>OrdinaryEndpointShapeTests.The_classification_universe_still_comes_back_whole</c>.</param>
public sealed record Observation(
    string Group, string Method, string Outcome, string? Detail,
    IReadOnlyList<string> Set, IReadOnlyList<string> Unset, int Rows = 0)
```

and set it at the one construction site that has rows — the final `return` of `ObserveAsync`:

```csharp
        return new Observation(group, method.Name, Rows, $"{rows.Count} rows", set, unset, rows.Count);
```

The other three construction sites keep six arguments and get `Rows = 0`; they represent an error, a plan refusal, or an empty answer, none of which has a count worth carrying.

Then append to `tests/FmpDotNet.SmokeTests/OrdinaryEndpointShapeTests.cs`, inside the class:

```csharp
    [LiveFact]
    public async Task The_classification_universe_still_comes_back_whole()
    {
        // The tripwire for FMP fixing the all-industry-classification pagination anomaly.
        // GetAllIndustryClassificationsAsync sends page=1 because that is the ONLY route to rows 1,001 onward:
        // page 0 caps at 1,000 however large a limit is sent, and the dataset is 25,952 rows. If FMP ever makes
        // page=1 mean "the second page", this method starts answering a handful of rows — with the same shape,
        // the same fields populated, and nothing at all for the two baseline comparisons to notice. The row
        // count is the only thing that can catch it.
        //
        // No extra request: this reads the count off the sweep the fixture has already run.
        var live = await sweep.ObservationsAsync();
        var observed = live.Single(o => o.Name == "Directory.GetAllIndustryClassificationsAsync");

        Assert.Equal(Probe.Rows, observed.Outcome);
        Assert.True(observed.Rows > Endpoints.DirectoryEndpoints.MaxIndustryClassificationPageSize,
            $"stable/all-industry-classification?page=1 answered {observed.Rows} rows. It answered 25,952 on "
            + "2026-08-28, and page 0 caps at 1,000 — so anything at or below the cap means the anomaly this "
            + "method depends on has been fixed and callers are now silently reading a fraction of the data. "
            + "See DirectoryEndpoints.GetAllIndustryClassificationsAsync.");
    }
```

- [ ] **Step 7: Run the smoke suite twice — keyless, then live**

Run: `dotnet test tests/FmpDotNet.SmokeTests`
Expected: PASS with every live test skipped. This is exactly what CI runs.

Then, with the key — this spends about 60 ordinary calls and roughly 30 MB of downloads, and takes a couple of minutes:

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY\s*=\s*\"?([^\"\s]+)\"?', open('.env').read(), re.M).group(1))") \
  dotnet test tests/FmpDotNet.SmokeTests
```

Extract the key exactly that way. **Do not `source` or `set -a` the `.env` file** — it has clobbered `PATH` for the whole shell before. **Do not set `FMPDOTNET_SMOKE_BULK`**: FMP restricts keys for frequent bulk use, and nothing in this slice touches a `*-bulk` path.

Expected: the two baseline tests fail, reporting the twelve new endpoints as drift plus the widened-range changes on the five date-ranged endpoints. That is Task 11's job. **What must NOT appear in that output is `outcome empty` or `outcome error` against any of the twelve new endpoint names.** Read the failure listing for those specifically:

| expected `outcome rows` | if it says `empty`, the argument is wrong |
|---|---|
| `Directory.GetIndustryClassificationsAsync` | — |
| `Directory.GetAllIndustryClassificationsAsync` | — |
| `Directory.GetSicCodesAsync` | — |
| `Search.FindIndustryClassificationAsync` | `LiveApi.SicCode` contradicts `Symbol`/`Cik` |
| `SecFilings.GetProfileAsync` / `GetProfileByCikAsync` | — |
| `SecFilings.Get8KFilingsAsync` / `GetFilingsWithFinancialsAsync` | — |
| `SecFilings.SearchBySymbolAsync` / `SearchByCikAsync` / `SearchByFormTypeAsync` | the window is too narrow — widen `LiveApi.RangeStart` |
| `SecFilings.FindCompanyBySymbolAsync` / `ByCikAsync` / `ByNameAsync` | `LiveApi.CompanyNameQuery` |

Fix any that came back empty, and re-run before going on. An endpoint that records `empty` here records it forever.

- [ ] **Step 8: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs \
        tests/FmpDotNet.SmokeTests/OrdinaryEndpointShapeTests.cs
git commit -m "test: probe the twelve new paths with arguments they can answer (#30)"
```

---

### Task 11: Regenerate the README and re-record the live baseline

Two generated artifacts and one block of hand-written prose that has been stale since #29 shipped.

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
grep -n 'of FMP.s 243 endpoint paths are modelled' README.md
```

Expected: the headline reads **126 of FMP's 243 endpoint paths are modelled**, a new `` `fmp.SecFilings` `` section lists nine paths, `` `fmp.Directory` `` gains `stable/all-industry-classification` (against two methods) and `stable/standard-industrial-classification-list`, and `` `fmp.Search` `` gains `stable/industry-classification-search`. If the headline is not 126, an endpoint is not being discovered — `EndpointCoverageTests.Every_public_endpoint_method_reaches_the_api` names it.

- [ ] **Step 2: Fix the stale prose under "Reaching an endpoint that is not modelled"**

The paragraph beginning "The rest is unbuilt rather than blocked" is **already wrong before this slice**: it says "142 paths remain, of which 135 are actionable" and names Company as the next slice, both of which describe the state before #29 shipped. `EndpointCoverageTests` regenerates the table above it but does not read this prose, so it rots silently.

Replace the two paragraphs beginning "The rest is unbuilt rather than blocked" and "That remainder is tracked as twelve actionable issues" with:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **117 paths
remain**, of which **110 are actionable** — the seven `tipranks-*` paths need a separately-purchased add-on and
return 402 even on FMP's top tier, so they cannot be built or tested by buying a bigger plan. The remainder is not
spread the way FMP's own section headings suggest: the largest groups are Form 13F & Insider Trades (14) and
Analyst & Calendar (14), then Senate & House (12) and Economics/Transcripts/ESG/COT (12), Market Performance (11),
News (10) and Fundraisers & DCF (10); ETF & Mutual Funds, Technical Indicators and Indexes & Market Hours carry 9
apiece.
```

and

```markdown
That remainder is tracked as eleven issues under the epic, ten of them actionable, each 9 to 14 paths and each
carrying the measured path list for its group. The counts above are the sum of those issues and reconcile exactly
against the 243-path inventory: 126 modelled plus 117 remaining, with no path counted twice and none missing.
```

Leave the two paragraphs after those — the one about the equity/asset-class imbalance and the one about Commodity, Forex and Crypto — unchanged; both are still true.

- [ ] **Step 3: Verify the arithmetic against the issues rather than trusting it**

```bash
for n in 31 32 33 34 35 36 37 38 39 40 41; do
  gh issue view $n --json body --jq .body | grep -coE 'stable/[a-z0-9-]'
done | paste -sd+ | bc
```

Expected: `117`. That is `243 - 126`, so the partition holds with no gap and no double count. If it prints anything else, the prose is wrong — fix the prose, not this check.

- [ ] **Step 4: Run the unit suite green**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS, all of it, including `EndpointCoverageTests`. This is the first point in the plan where the whole unit suite is green.

- [ ] **Step 5: Re-record the live baseline**

The baseline is a measurement, not a specification — never hand-edit it. Record it in one run so its header date is true of every line:

```bash
FMP_API_KEY=$(python3 -c "import re;print(re.search(r'^FMP_API_KEY\s*=\s*\"?([^\"\s]+)\"?', open('.env').read(), re.M).group(1))") \
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 \
  dotnet test tests/FmpDotNet.SmokeTests
```

Again: do not `source` the `.env`, and do not set `FMPDOTNET_SMOKE_BULK` — `baseline-bulk.txt` is untouched by this slice and re-recording it would spend the key's standing on twenty of FMP's most restricted endpoints for nothing.

`ShapeAssertions.Updated` refuses to write a baseline from a run in which any endpoint errored, so a transport fault or a throttled key fails loudly here instead of writing `outcome error` in as an endpoint's recorded truth. If it refuses, wait and re-run rather than working around it.

- [ ] **Step 6: Read the baseline diff before committing it**

```bash
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt | head -200
git diff --stat tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
grep -c '^\[' tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Expected, and each item is a thing to check rather than assume:

1. The entry count goes from **100 to 114** — twelve new endpoint methods on the twelve new paths, plus `GetIndustryClassificationsAsync` and `GetAllIndustryClassificationsAsync` sharing one path. Fourteen new `[Group.Method]` blocks.
2. Every one of the fourteen reads `outcome rows`. **Not one may read `empty`** — see Task 10, Step 7 for what each empty would mean. `error` fails the write outright.
3. The header date is today's.
4. Some of the five date-ranged endpoints widened by `LiveApi.RangeStart` may flip a property from `null` to `set` — ninety days of data can populate a field one day of data left empty. That is drift in the harmless direction and is expected; read the lines and satisfy yourself none went the other way.
5. Nothing else changed. Any `now always null, was populated` line on an endpoint this slice did not touch is a real finding — stop and investigate it rather than committing it.

- [ ] **Step 7: Commit**

```bash
git add README.md tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "docs: record 126 of 243 paths modelled, and the live shapes of the twelve new ones (#30)"
```

- [ ] **Step 8: Finish the branch**

Run the full solution suite one last time, then use `superpowers:finishing-a-development-branch`. `master` requires a pull request and the `.NET — build + test` check, so the route is branch → PR → green → merge; option 1 (merge locally) is not available on this repository unless the maintainer takes the admin bypass deliberately.

```bash
dotnet test
```


---

## Coverage of the spec

Run as the writing-plans self-review, and left here because an executor reading one task at a time cannot see it.

**Every path in the spec reaches a task.** Twelve paths, fourteen public methods:

| spec section | paths | task |
|---|---|---|
| `fmp.Directory` — two additions | `all-industry-classification`, `standard-industrial-classification-list` | 2 (three methods, because the first path has two behaviours) |
| `fmp.Search` — one addition | `industry-classification-search` | 3 |
| `fmp.SecFilings` — profile | `sec-profile` | 6 (two methods, one path) |
| `fmp.SecFilings` — feeds | `sec-filings-8k`, `sec-filings-financials` | 7 |
| `fmp.SecFilings` — filing search | `sec-filings-search/{symbol,cik,form-type}` | 8 |
| `fmp.SecFilings` — company search | `sec-filings-company-search/{symbol,cik,name}` | 9 |

**Every model, converter and registration reaches a task.** Four records — `IndustryClassification` and
`SicCodeEntry` (Task 1), `SecFiling` (Task 5), `SecProfile` (Task 6). Two converters —
`BusinessAddressJsonConverter` (Task 1), `NullableDateAtMidnightJsonConverter` (Task 5). Four
`[JsonSerializable]` entries, added in the task that writes each record, never all at once — the two missing
types would not compile.

**Every trap the spec names gets a test that fails when it is reintroduced:**

| trap | test | task |
|---|---|---|
| Address normalisation, including the `XI'AN` row | `The_bracketed_encoding_becomes_the_joined_one`, `An_apostrophe_inside_an_element_survives_because_the_transform_is_textual`, `A_plain_string_is_returned_untouched`, `A_null_stays_null_and_an_unrecognised_shape_passes_through` | 1 |
| The converter is wired, not merely written | `The_converter_is_wired_to_the_property_and_not_merely_written` | 1 |
| `page=1` is the only route to the whole classification universe | `The_whole_universe_is_reached_by_sending_page_one_and_no_limit` | 2 |
| SIC codes lose a leading zero on one endpoint and not the other | `The_sic_list_strips_a_leading_zero_that_the_classification_paths_keep` | 2 |
| `filingDate` is a date wearing a dummy midnight | `A_filing_date_loses_its_dummy_midnight` | 5 |
| `acceptedDate` is Eastern, not UTC | `The_accepted_date_is_read_as_eastern_wall_clock_not_as_utc` | 5 |
| `to` filters `acceptedDate`, so `FilingDate` can fall outside the range | `Filing_date_cannot_be_derived_from_accepted_date` | 5 |
| `HasFinancials` absent on the search paths | `The_search_paths_omit_has_financials_entirely` | 8 |
| `SecProfile` string-typed numerics | `The_employee_count_is_a_quoted_string_on_the_wire_and_an_int_here` | 6 |
| `FiscalYearEnd` and `FiftyTwoWeekRange` bind unparsed | `The_fiscal_year_end_and_the_fifty_two_week_range_stay_as_sent` | 6 |
| A `limit` above the cap is silently clamped by FMP | `A_limit_above_the_measured_cap_is_refused_rather_than_clamped_by_fmp` (2), `A_limit_above_the_measured_cap_is_refused_on_both_feeds` (7), `Every_search_refuses_a_limit_above_the_cap` (8) | 2, 7, 8 |
| A parameter the endpoint ignores | `Each_company_search_sends_its_own_path_and_parameter_and_no_limit` | 9 |
| The `page=1` anomaly being fixed upstream | `The_classification_universe_still_comes_back_whole` (live) | 10 |
| The sweep probing a new path with a meaningless argument | `The_sweep_asks_the_filing_searches_for_a_range_wider_than_one_day`, `The_sweep_asks_each_new_search_for_a_value_of_its_own_kind` (both keyless) | 10 |

Two traps are pinned that the spec did not name, both found while planning: the literal string `"None"` in
`symbol` (`Classification_search_carries_the_literal_None_symbol_through_unchanged`, Task 3) and the loose
name matching that returns `APPLING PARTNERS, LLC` for `company=Apple`
(`A_name_search_matches_loosely_and_leaves_unclassified_filers_blank`, Task 9).

**Totals.** 61 new test methods across ten test files (four created, six modified), 13 fixtures, 12 paths, 14
public methods, 4 records, 2 converters, 1 new facade, 1 promoted helper. The spec estimated 45–55 tests; the
excess is the two unplanned traps above, the promoted guard's own tests, and the two keyless sweep guards.
