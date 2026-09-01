# Senate Net Worth Fields Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SenateNetWorthSummary` bind all 27 keys `stable/senate-net-worth-aggregated` sends — eleven of which it drops today — and make any key it does not name visible in an `UnmappedFields` dictionary instead of vanishing.

**Architecture:** Eleven new `decimal?` properties join the record, then a hand-written `SenateNetWorthSummaryJsonConverter` (modelled on `FinancialReportJsonConverter`) takes over binding for the type: it reads every property of the object into a case-insensitive dictionary of `JsonElement`, takes the 27 named members out of it with the same number-or-numeric-string rules `FmpJsonContext` applies, and hands the remainder to `UnmappedFields` as `IReadOnlyDictionary<string, JsonElement>`. The live sweep gains a second `senateID` constant so it probes a member who actually carries the new keys.

**Tech Stack:** .NET 10, C#, source-generated `System.Text.Json` (`FmpJsonContext`), xUnit, `Binding` fixture helpers, the live smoke suite.

**Spec:** [`docs/superpowers/specs/2026-09-01-senate-net-worth-fields-design.md`](../specs/2026-09-01-senate-net-worth-fields-design.md), argued from [`docs/superpowers/specs/2026-09-01-senate-net-worth-fields-measurements.md`](../specs/2026-09-01-senate-net-worth-fields-measurements.md). Both are committed on this branch (`d8880f4`). Read the design before Task 1. Every number in every doc comment below is taken from the measurements document; if a number here disagrees with it, the measurements document wins and the discrepancy is reported.

## Global Constraints

Copied from `CONTRIBUTING.md` and the repository's existing conventions. Every task's requirements implicitly include this section.

- **A claim in this repository should have a measurement behind it.** Every number written into a doc comment in this plan comes from the measurements document. Do not invent one, and do not round one.
- **No reflection in the library.** `FmpDotNet.csproj` declares `IsAotCompatible`; `IL2026` and `IL3050` are build errors. Tests may reflect — they have `InternalsVisibleTo`.
- **Doc comments are compiled.** `GenerateDocumentationFile` plus `-warnaserror` means a `<see cref="X"/>` to a member that does not exist yet is a `CS1574` build error, and an undocumented public member is `CS1591`. Task 1 therefore names `UnmappedFields` in plain text, and Task 2 upgrades it to a `cref`.
- **NodaTime only in public signatures.** Nothing in this plan adds a date.
- **Everything throws.** No `Try`-prefixed methods and no sentinel returns. `null` means "an answer FMP gave", never "a failure".
- **Nullable models, nothing `required`.** All eleven new properties are `decimal?`. `UnmappedFields` is non-nullable because it is never null — an empty dictionary is the "FMP sent nothing extra" answer.
- **`decimal` over `long`/`int`** for anything numeric off the wire. Every one of the eleven is money.
- **Never paste an API key, including inside a URL.** The fixture in Task 1 is response bodies only. The smoke baseline in Task 3 is re-recorded with the key in an environment variable read from `.env`; it is never echoed, never written into a file, never included in a report.
- **Branch is `fix/senate-net-worth-fields`**, already created, already carrying the spec commit. Commit in conventional-commit form referencing `#57`. End every commit message with `Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy`.
- **Build must be clean under `-warnaserror`.** Run `dotnet build FmpDotNet.slnx -warnaserror` before every commit.
- **Full unit suite must be green.** `dotnet test tests/FmpDotNet.Tests` — 1,434 tests on `master`, and this plan adds to that count without removing any.
- **Match the surrounding doc-comment style.** Prose wrapped at about 110 columns, `<para>` blocks, a bold lead sentence when the paragraph makes a claim, and the measurement date on every measured figure.

## File Structure

| file | responsibility | task |
|---|---|---|
| `tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated-all-keys.json` | **Create.** Nine real rows from six members, between them carrying all 27 keys. The existing two-row `H000601` fixture stays as it is. | 1 |
| `src/FmpDotNet/Models/SenateNetWorth.cs` | **Modify.** `SenateNetWorthSummary` gains eleven properties and a rewritten class summary (Task 1), then `UnmappedFields`, the `[JsonConverter]` attribute and `SenateNetWorthSummaryJsonConverter` at the end of the file (Task 2). | 1, 2 |
| `tests/FmpDotNet.Tests/CongressTests.cs` | **Modify.** One new binding test and one corrected comment (Task 1); eight converter tests (Task 2). | 1, 2 |
| `src/FmpDotNet/Endpoints/CongressEndpoints.cs` | **Modify.** `GetNetWorthSummaryAsync`'s remarks. | 3 |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | **Modify.** New constant `NetWorthSummarySenateId`; `SenateId`'s remarks gain a paragraph. | 3 |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | **Modify.** One new `when` arm on the `senateId` dispatch. | 3 |
| `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` | **Modify.** Re-recorded; the diff is read against an expectation before it is committed. | 3 |
| `src/FmpDotNet/Serialization/ShapeConverters.cs` | **Modify.** `NetWorthRangeJsonConverter`'s remarks: two sample figures upgraded to the population. | 4 |
| `docs/superpowers/specs/2026-08-29-senate-and-house-trading-measurements.md` | **Modify.** Two correction blockquotes. | 4 |
| `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-measurements.md` | **Modify.** One sentence each on per-member shape constancy and the sixth fixture member. | 4 |
| `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-design.md` | **Modify.** The fixture sentence: six members, not five. | 4 |

---

### Task 1: The eleven typed fields

**Files:**
- Create: `tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated-all-keys.json`
- Modify: `src/FmpDotNet/Models/SenateNetWorth.cs` — the `SenateNetWorthSummary` record, currently lines 166–227
- Modify: `tests/FmpDotNet.Tests/CongressTests.cs` — the test at line 305, and a new test after it

**Interfaces:**
- Consumes: `Binding.Fixture(string)` and `Binding.Unbound<T>(T)` from `tests/FmpDotNet.Tests/Binding.cs`; `FmpJsonContext.Default.ListSenateNetWorthSummary`.
- Produces: eleven `decimal?` properties on `SenateNetWorthSummary` — `Other`, `BusinessAndSelfEmployment`, `PensionAndRetirementIncome`, `OtherIncome`, `SpousalIncome`, `InvestmentAndCapitalGains`, `Options`, `AssetBackedSecurities`, `PersonalLiabilities`, `EducationLiabilities`, `OtherLiabilities` — each carrying `[JsonPropertyName]` with the wire name shown in Step 3. Task 2's converter binds exactly these names and Task 2's tests assert them.

- [ ] **Step 1: Create the fixture**

Create `tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated-all-keys.json` with exactly this content. These are real rows captured 2026-09-01 — four from `G000581`, one each from `K000375`, `M001160`, `Q000023`, `C001061` and `S001145` — chosen so that every one of the eleven new keys appears, and non-zero wherever the population has a non-zero value for it. Do not retype values from memory; copy the block.

```json
[
  {
    "senateID": "G000581",
    "year": 2024,
    "total": 9684459,
    "mutualFundsAndETFs": 817991,
    "realEstate": 7552500,
    "personalLiabilities": 0,
    "stock": 251550,
    "realEstateLiabilities": 12500,
    "educationLiabilities": 0,
    "revolvingAndCreditLines": 0,
    "cashAndCashEquivalents": 462191,
    "otherAssets": 0,
    "businessAndSelfEmployment": 0,
    "pensionAndRetirementIncome": 0,
    "otherIncome": 0,
    "ownershipInterest": 8000,
    "pensionAndRetirementAssets": 572727,
    "Other": 32000,
    "trusts": 0,
    "otherLiabilities": 0,
    "governmentSecurities": 0
  },
  {
    "senateID": "G000581",
    "year": 2023,
    "total": -13885500,
    "mutualFundsAndETFs": 0,
    "realEstate": 4500000,
    "personalLiabilities": 37500000,
    "stock": 0,
    "realEstateLiabilities": 0,
    "educationLiabilities": 0,
    "revolvingAndCreditLines": 0,
    "cashAndCashEquivalents": 707000,
    "otherAssets": 18407500,
    "businessAndSelfEmployment": 0,
    "pensionAndRetirementIncome": 0,
    "otherIncome": 0,
    "ownershipInterest": 0,
    "pensionAndRetirementAssets": 0,
    "Other": 0,
    "trusts": 0,
    "otherLiabilities": 0,
    "governmentSecurities": 0
  },
  {
    "senateID": "G000581",
    "year": 2022,
    "total": 17684500,
    "mutualFundsAndETFs": 0,
    "realEstate": 9175000,
    "personalLiabilities": 0,
    "stock": 8000,
    "realEstateLiabilities": 0,
    "educationLiabilities": 0,
    "revolvingAndCreditLines": 12500,
    "cashAndCashEquivalents": 4414000,
    "otherAssets": 0,
    "businessAndSelfEmployment": 0,
    "pensionAndRetirementIncome": 0,
    "otherIncome": 0,
    "ownershipInterest": 0,
    "pensionAndRetirementAssets": 4275000,
    "Other": 0,
    "trusts": 0,
    "otherLiabilities": 175000,
    "governmentSecurities": 0
  },
  {
    "senateID": "G000581",
    "year": 2017,
    "total": -450000,
    "mutualFundsAndETFs": 0,
    "realEstate": 0,
    "personalLiabilities": 0,
    "stock": 0,
    "realEstateLiabilities": 375000,
    "educationLiabilities": 75000,
    "revolvingAndCreditLines": 0,
    "cashAndCashEquivalents": 0,
    "otherAssets": 0,
    "businessAndSelfEmployment": 0,
    "pensionAndRetirementIncome": 0,
    "otherIncome": 0,
    "ownershipInterest": 0,
    "pensionAndRetirementAssets": 0,
    "Other": 0,
    "trusts": 0,
    "otherLiabilities": 0,
    "governmentSecurities": 0
  },
  {
    "senateID": "K000375",
    "year": 2021,
    "total": 18080590,
    "mutualFundsAndETFs": 2107544,
    "realEstateLiabilities": 0,
    "personalLiabilities": 0,
    "pensionAndRetirementAssets": 8001,
    "otherLiabilities": 0,
    "pensionAndRetirementIncome": 0,
    "assetBackedSecurities": 15325011,
    "stock": 80507,
    "trusts": 16002,
    "cashAndCashEquivalents": 0,
    "salaryAndWages": 0,
    "otherIncome": 0,
    "otherAssets": 0,
    "realEstate": 175001,
    "Other": 193523,
    "ownershipInterest": 175001,
    "governmentSecurities": 0
  },
  {
    "senateID": "M001160",
    "year": 2021,
    "total": 9893000,
    "realEstateLiabilities": 175000,
    "personalLiabilities": 0,
    "pensionAndRetirementAssets": 1192000,
    "stock": 718500,
    "otherAssets": 32500,
    "pensionAndRetirementIncome": 0,
    "realEstate": 375000,
    "trusts": 0,
    "cashAndCashEquivalents": 375000,
    "otherIncome": 0,
    "ownershipInterest": 6375000,
    "options": 48500,
    "mutualFundsAndETFs": 951500,
    "salaryAndWages": 0,
    "Other": 0,
    "educationLiabilities": 0
  },
  {
    "senateID": "Q000023",
    "year": 2024,
    "total": -200000,
    "realEstateLiabilities": 375000,
    "revolvingAndCreditLines": 0,
    "stock": 0,
    "otherAssets": 0,
    "Other": 0,
    "salaryAndWages": 0,
    "pensionAndRetirementIncome": 0,
    "otherIncome": 0,
    "cashAndCashEquivalents": 0,
    "spousalIncome": 0,
    "otherLiabilities": 0,
    "pensionAndRetirementAssets": 175000,
    "ownershipInterest": 0,
    "mutualFundsAndETFs": 0,
    "personalLiabilities": 0,
    "governmentSecurities": 0
  },
  {
    "senateID": "C001061",
    "year": 2024,
    "total": -2678491,
    "educationLiabilities": 0,
    "realEstateLiabilities": 375001,
    "governmentSecurities": 183002,
    "personalLiabilities": 3000001,
    "investmentAndCapitalGains": 0,
    "Other": 0,
    "pensionAndRetirementIncome": 0,
    "ownershipInterest": 0,
    "businessAndSelfEmployment": 0,
    "businessLiabilities": 0,
    "pensionAndRetirementAssets": 440506,
    "salaryAndWages": 0,
    "mutualFundsAndETFs": 73003
  },
  {
    "senateID": "S001145",
    "year": 2018,
    "total": -169999,
    "mutualFundsAndETFs": 348001,
    "revolvingAndCreditLines": 73000,
    "realEstateLiabilities": 625000,
    "otherLiabilities": 0,
    "otherIncome": 0,
    "pensionAndRetirementIncome": 289473.83,
    "cashAndCashEquivalents": 30500,
    "otherAssets": 157500,
    "salaryAndWages": 0,
    "Other": 8000,
    "ownershipInterest": 0,
    "personalLiabilities": 0,
    "pensionAndRetirementAssets": 0,
    "stock": 0
  }
]
```

Verify the transcription before going on — a wrong digit here silently becomes a wrong assertion later:

```bash
f=tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated-all-keys.json
jq 'length' "$f"                              # 9
jq '[.[]|keys[]]|unique|length' "$f"          # 27
jq -cS . "$f" | shasum -a 256                 # 150046e275393a9ffb20fcb8f811659af8a919e72bf4bc525c465234c6bf8408
```

All three must match. The hash is over `jq -cS` output — key-sorted, compact — so indentation cannot affect it, only content. The `.csproj` already copies `Fixtures\*.json` to the output directory; nothing to register.

- [ ] **Step 2: Write the failing test**

In `tests/FmpDotNet.Tests/CongressTests.cs`, directly after the existing
`Every_money_field_on_the_aggregate_binds_whether_or_not_it_carries_a_decimal_point` test (its last assertion is `Assert.Equal(0m, rows[0].Trusts);` at line 333, followed by the closing brace), add:

```csharp
    [Fact]
    public void The_eleven_categories_one_member_never_showed_bind_from_the_census_fixture()
    {
        // The shipped record was modelled from H000601's six rows, which carry exactly 16 of the 27 keys FMP
        // sends on this path. Measured 2026-09-01 across all 535 members, the other eleven are on 3,130 of
        // 3,425 rows. This fixture is nine real rows from six members, chosen so every one of the eleven
        // appears — and is non-zero wherever the population ever has it non-zero. A test that asserted 0m
        // alone could pass against a property that binds nothing.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(9, rows.Count);

        // G000581 carries 21 keys — the most of any member — and the six he lacks are exactly these. A
        // property absent from this list is one that bound; a property missing from the record altogether
        // would not appear here either, which is why the value assertions below are not optional.
        Assert.Equal("G000581", rows[0].SenateId);
        Assert.Equal(2024, rows[0].Year);
        Assert.Equal(
            ["AssetBackedSecurities", "BusinessLiabilities", "InvestmentAndCapitalGains", "Options",
             "SalaryAndWages", "SpousalIncome"],
            Binding.Unbound(rows[0]));

        Assert.Equal(32000m, rows[0].Other);                          // asset on this row: total reconciles
        Assert.Equal(0m, rows[0].BusinessAndSelfEmployment);          // income: zero on every row measured
        Assert.Equal(0m, rows[0].PensionAndRetirementIncome);
        Assert.Equal(0m, rows[0].OtherIncome);                        // income: zero on every row measured
        Assert.Equal(0m, rows[0].PersonalLiabilities);
        Assert.Equal(0m, rows[0].EducationLiabilities);
        Assert.Equal(0m, rows[0].OtherLiabilities);

        Assert.Equal(37500000m, rows[1].PersonalLiabilities);         // G000581 2023
        Assert.Equal(175000m, rows[2].OtherLiabilities);              // G000581 2022
        Assert.Equal(75000m, rows[3].EducationLiabilities);           // G000581 2017

        Assert.Equal(15325011m, rows[4].AssetBackedSecurities);       // K000375 2021 — on 42 rows in the census
        Assert.Equal(193523m, rows[4].Other);

        Assert.Equal(48500m, rows[5].Options);                        // M001160 2021 — on 66 rows

        Assert.Equal(0m, rows[6].SpousalIncome);                      // Q000023 2024 — never non-zero, on 153 rows
        Assert.DoesNotContain("SpousalIncome", Binding.Unbound(rows[6]));

        Assert.Equal(0m, rows[7].InvestmentAndCapitalGains);          // C001061 2024 — never non-zero, on 100 rows
        Assert.DoesNotContain("InvestmentAndCapitalGains", Binding.Unbound(rows[7]));

        Assert.Equal(289473.83m, rows[8].PensionAndRetirementIncome); // S001145 2018 — one of four non-zero rows
        Assert.Equal(8000m, rows[8].Other);                           // liability on this row: -169999 needs it subtracted
    }
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~The_eleven_categories" 2>&1 | tail -20`

Expected: **build failure**, `CS1061` — `'SenateNetWorthSummary' does not contain a definition for 'Other'` (and the other ten). A compile error is the correct RED here: the properties do not exist.

- [ ] **Step 4: Add the eleven properties and rewrite the class summary**

In `src/FmpDotNet/Models/SenateNetWorth.cs`, replace the class summary of `SenateNetWorthSummary` — the `/// <summary>One year of a Senator's net worth …` block that ends `… costs the whole response rather than the field.</para></summary>` immediately above `public sealed record SenateNetWorthSummary` — with this, verbatim:

```csharp
/// <summary>One year of a Senator's net worth, totalled by category, from
/// <c>stable/senate-net-worth-aggregated</c>.
///
/// <para>One row per reporting year. Measured 2026-09-01 across <b>every member <c>senate-profile</c>
/// enumerates</b> — 535 asked, 455 answering, 3,425 rows — the years run 2013 through 2024 and a member has
/// between one and twelve rows.</para>
///
/// <para><b>The row shape is per member, and no member shows all of it.</b> The census found <b>27 keys</b>:
/// <c>senateID</c>, <c>year</c>, <c>total</c>, and 24 money categories. Every row of a given member carries
/// the same key set, and that set is the categories the member has ever disclosed — <c>H000601</c> carries 16,
/// <c>G000581</c> carries 21, and nobody carries 27. This type was first modelled from <c>H000601</c>'s six rows
/// and had 16 properties as a result; a 25-member sample (#57) raised that to 25; the census raised it to 27,
/// finding <see cref="SpousalIncome"/> and <see cref="InvestmentAndCapitalGains"/> on members the sample never
/// asked. Three samples, three undercounts, which is why a catch-all for names this type does not know follows
/// the typed fields.</para>
///
/// <para><b><see cref="Total"/> is assets minus liabilities, and the parts reproduce it except inside
/// <see cref="Other"/>.</b> Summing the eleven asset fields, subtracting the six liability fields and ignoring
/// the six income fields gives <c>total</c> exactly on every row where <see cref="Other"/> is zero — 2,907 of
/// 2,907. Where it is not, <see cref="Other"/> reconciles as an asset on 246 rows, as a liability on 228, and as
/// neither on 44. The SDK derives nothing from this; it is recorded so a caller reconstructing net worth knows
/// where the uncertainty lives.</para>
///
/// <para><b>Every one of the 24 money fields is <see cref="decimal"/>, including the seven that never carried a
/// decimal point.</b> Across the census, 18 of the 25 numeric keys flip between bare-integer and decimal-point
/// representation on some row, and the seven that do not include five income fields that are zero on every
/// row — an integral sample of zeros says nothing about the next row, and one fractional value under
/// <see cref="int"/> costs the whole response rather than the field.</para></summary>
```

Then, inside the record, immediately after the `Trusts` property (the last one, `[JsonPropertyName("trusts")] public decimal? Trusts { get; init; }`), add the eleven, verbatim:

```csharp

    // ---- the eleven the first sample never showed (#57) -------------------------------------------------

    /// <summary>FMP's own catch-all category. Capital <c>O</c> on the wire, alone among this path's keys.
    ///
    /// <para><b>Carries either sign, and the row does not say which.</b> Measured 2026-09-01 it is on 2,552
    /// of 3,425 rows and non-zero on 518: on 246 of those <see cref="Total"/> reconciles only if this is added,
    /// on 228 only if it is subtracted, and on 44 neither way. Every row where it is zero reconciles exactly.
    /// Passed through as sent.</para></summary>
    [JsonPropertyName("Other")] public decimal? Other { get; init; }

    /// <summary>Income from business and self-employment. Present on 1,118 rows measured 2026-09-01 and
    /// <b>zero on every one of them</b> — income is disclosed on this path but does not enter
    /// <see cref="Total"/>.</summary>
    [JsonPropertyName("businessAndSelfEmployment")] public decimal? BusinessAndSelfEmployment { get; init; }

    /// <summary>Pension and retirement income. Present on 1,193 rows measured 2026-09-01 and non-zero on
    /// <b>four</b>; income does not enter <see cref="Total"/>.</summary>
    [JsonPropertyName("pensionAndRetirementIncome")] public decimal? PensionAndRetirementIncome { get; init; }

    /// <summary>Income not covered by another income category. Present on 341 rows measured 2026-09-01 and
    /// <b>zero on every one of them</b>.</summary>
    [JsonPropertyName("otherIncome")] public decimal? OtherIncome { get; init; }

    /// <summary>A spouse's income. Present on 153 rows measured 2026-09-01 and <b>zero on every one of
    /// them</b>. One of the two keys the 25-member sample in #57 never saw.</summary>
    [JsonPropertyName("spousalIncome")] public decimal? SpousalIncome { get; init; }

    /// <summary>Investment income and capital gains. Present on 100 rows measured 2026-09-01 and <b>zero on
    /// every one of them</b>. One of the two keys the 25-member sample in #57 never saw.</summary>
    [JsonPropertyName("investmentAndCapitalGains")] public decimal? InvestmentAndCapitalGains { get; init; }

    /// <summary>Stock options held. Present on 66 rows measured 2026-09-01, non-zero on 12.</summary>
    [JsonPropertyName("options")] public decimal? Options { get; init; }

    /// <summary>Asset-backed securities held. Present on 42 rows measured 2026-09-01 — the rarest key on the
    /// path — non-zero on 12.</summary>
    [JsonPropertyName("assetBackedSecurities")] public decimal? AssetBackedSecurities { get; init; }

    /// <summary>Personal loans and other personal debt. Present on 777 rows measured 2026-09-01 and non-zero
    /// on 280 — with <see cref="EducationLiabilities"/>, the liability most often dropped before
    /// #57.</summary>
    [JsonPropertyName("personalLiabilities")] public decimal? PersonalLiabilities { get; init; }

    /// <summary>Student and other education debt. Present on 462 rows measured 2026-09-01 and non-zero on
    /// <b>306</b> — the issue's sample saw it on 5 of 119 rows and ranked it rarest; the census ranks it the
    /// second most consequential of the eleven.</summary>
    [JsonPropertyName("educationLiabilities")] public decimal? EducationLiabilities { get; init; }

    /// <summary>Liabilities not covered by another liability category. Present on 342 rows measured
    /// 2026-09-01, non-zero on 98 — <c>K000389</c>'s 2017 row carries 6,000,000 here against a
    /// <see cref="Total"/> of −73,000.</summary>
    [JsonPropertyName("otherLiabilities")] public decimal? OtherLiabilities { get; init; }
```

Also replace the `SalaryAndWages` property's doc — currently `/// <summary>Salary and wage income.</summary>` — with:

```csharp
    /// <summary>Salary and wage income. Present on 2,033 rows measured 2026-09-01 and <b>zero on every one of
    /// them</b> — income is disclosed on this path but does not enter <see cref="Total"/>.</summary>
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~The_eleven_categories" 2>&1 | tail -5`

Expected: PASS, 1 test.

- [ ] **Step 6: Correct the comment on the existing money-field test**

In `CongressTests.cs`, the test `Every_money_field_on_the_aggregate_binds_whether_or_not_it_carries_a_decimal_point` opens with a three-line comment beginning `// 8 of the 14 money fields changed representation across only six measured rows;`. Replace that comment with:

```csharp
        // Measured 2026-08-29, 8 of the 14 money fields then modelled changed representation across only six
        // rows; re-measured 2026-09-01 across all 3,425 rows, 18 of the 25 numeric keys do, and the seven that
        // never do include five income fields that are zero everywhere. All 24 are decimal?, and this test
        // asserts one of each kind so typing any of them `int` fails here. The fixture is H000601's two rows
        // and carries none of the eleven keys added by #57; the census fixture in the next test does.
```

- [ ] **Step 7: Build clean and run the whole unit suite**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | tail -3 && dotnet test tests/FmpDotNet.Tests 2>&1 | tail -3`

Expected: `0 Warning(s)`, `0 Error(s)`; 1,435 passed, 0 failed.

- [ ] **Step 8: Commit**

```bash
git add tests/FmpDotNet.Tests/Fixtures/congress-senate-net-worth-aggregated-all-keys.json \
        src/FmpDotNet/Models/SenateNetWorth.cs tests/FmpDotNet.Tests/CongressTests.cs
git commit -m "feat(congress): model the eleven senate-net-worth-aggregated categories one member never showed (#57)

Measured 2026-09-01 across all 535 members, the path sends 27 keys; the
record had 16, built from one member's six rows. The eleven now bind, with
the census figures on each, and a nine-row fixture from six members carries
all 27.

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 2: The catch-all and its converter

**Files:**
- Modify: `src/FmpDotNet/Models/SenateNetWorth.cs` — `SenateNetWorthSummary` (usings at the top, the class summary, one new property, one attribute) and a new converter class appended at the end of the file
- Modify: `tests/FmpDotNet.Tests/CongressTests.cs` — eight new tests after Task 1's test

**Interfaces:**
- Consumes: the eleven properties Task 1 added; `FinancialReportJsonConverter` in `src/FmpDotNet/Models/FinancialReports.cs` as the pattern; `ReadOnlyDictionary<string, JsonElement>.Empty`.
- Produces: `public IReadOnlyDictionary<string, JsonElement> UnmappedFields { get; init; }` on `SenateNetWorthSummary`, never null; `public sealed class SenateNetWorthSummaryJsonConverter : JsonConverter<SenateNetWorthSummary>` in namespace `FmpDotNet.Models`. Task 3's smoke expectation depends on `UnmappedFields` being an empty collection on a normal row.

- [ ] **Step 1: Write the failing tests**

In `CongressTests.cs`, directly after Task 1's `The_eleven_categories_one_member_never_showed_bind_from_the_census_fixture` test, add all eight:

```csharp
    // ---- the catch-all (#57) --------------------------------------------------------------------------------

    [Fact]
    public void A_category_the_type_does_not_name_lands_in_UnmappedFields_under_its_wire_spelling()
    {
        // The twenty-eighth key. Three counts of this type — 16, 25, 27 — were each drawn from a sample and
        // each wrong, so the next one must be visible rather than dropped. Deserialised through the context's
        // list type info, not a bare converter call, because that is the path the endpoint uses and the
        // converter only helps if the source generator actually routes through it.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":42,"stock":40,"cryptocurrency":2}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(40m, row.Stock);
        var (name, value) = Assert.Single(row.UnmappedFields);
        Assert.Equal("cryptocurrency", name);
        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        Assert.Equal(2m, value.GetDecimal());
    }

    [Fact]
    public void A_string_the_type_does_not_name_does_not_cost_the_response()
    {
        // The reason the catch-all is JsonElement and not decimal. The likeliest unmodelled key is not a 25th
        // money bucket but an envelope field copied from senate-net-worth, where formType, filingDate and
        // link are strings on all 67,801 rows. A decimal dictionary would throw here and lose every row.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":1,"formType":"Annual Report","filingDate":"2025-05-15"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(1m, row.Total);
        Assert.Equal(2, row.UnmappedFields.Count);
        Assert.Equal("Annual Report", row.UnmappedFields["formType"].GetString());
        Assert.Equal("2025-05-15", row.UnmappedFields["filingDate"].GetString());
    }

    [Fact]
    public void UnmappedFields_is_empty_and_never_null_on_every_census_row()
    {
        // Two claims in one: no named key leaks into the catch-all (if the converter's name table misspelt
        // `stock`, the real `stock` would land here), and an object with nothing unrecognised binds an empty
        // dictionary rather than null. Every row measured 2026-09-01 binds an empty one.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(9, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.UnmappedFields);
            Assert.Empty(row.UnmappedFields);
        });
    }

    [Fact]
    public void The_converters_name_table_and_the_JsonPropertyName_attributes_agree()
    {
        // The attributes are documentation once the converter owns the binding — the generated binder no
        // longer reads them. So they can drift from the converter's table, and this pins them together from
        // both sides: every key in a fixture that carries all 27 is a [JsonPropertyName] on the type (the
        // attributes cover the wire), and none of those keys reaches UnmappedFields (the converter binds
        // every attributed name — asserted in the test above, and re-asserted here on the same fixture so
        // this test stands on its own).
        var fixture = Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json");
        var wireKeys = JsonDocument.Parse(fixture).RootElement
            .EnumerateArray()
            .SelectMany(row => row.EnumerateObject().Select(p => p.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var attributed = typeof(SenateNetWorthSummary)
            .GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(27, wireKeys.Count);
        Assert.Equal(wireKeys, attributed);

        var rows = JsonSerializer.Deserialize(fixture, FmpJsonContext.Default.ListSenateNetWorthSummary)!;
        Assert.All(rows, row => Assert.Empty(row.UnmappedFields));
    }

    [Fact]
    public void A_named_money_field_reads_a_numeric_string_as_the_context_would()
    {
        // FmpJsonContext sets AllowReadingFromString for every model, and a hand-written converter bypasses
        // it. No row measured 2026-09-01 sent a string here, but the context-wide setting exists because FMP
        // flips number representation elsewhere, and the typed members must not be the one place it is off.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":"2024","total":"12.5","stock":"7"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(2024, row.Year);
        Assert.Equal(12.5m, row.Total);
        Assert.Equal(7m, row.Stock);
        Assert.Empty(row.UnmappedFields);
    }

    [Fact]
    public void A_named_money_field_given_a_non_numeric_string_throws_as_the_context_would()
    {
        // Parity in the other direction. The generated binder throws JsonException on "n/a" in a decimal?
        // slot, and reading it as null or zero here would make the typed members quietly more lenient than
        // every other model in the SDK. A non-numeric `stock` is a defect worth hearing about.
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"stock":"n/a"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary));

        Assert.Contains("stock", ex.Message);
    }

    [Fact]
    public void A_named_key_in_a_different_case_binds_its_property_not_the_catch_all()
    {
        // PropertyNameCaseInsensitive = true on the context, re-implemented by the converter. `Other` is the
        // one key on this path that is not camelCase; if FMP ever re-cases it, every other model in the SDK
        // would still bind it and this one must too. A null binds null rather than throwing, for the same
        // parity reason.
        var rows = JsonSerializer.Deserialize(
            """[{"SENATEID":"X000001","Year":2024,"other":5,"STOCK":9,"trusts":null}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal("X000001", row.SenateId);
        Assert.Equal(2024, row.Year);
        Assert.Equal(5m, row.Other);
        Assert.Equal(9m, row.Stock);
        Assert.Null(row.Trusts);
        Assert.Empty(row.UnmappedFields);
    }

    [Fact]
    public void A_row_survives_a_round_trip_with_its_typed_values_and_its_unmapped_keys()
    {
        // The write path exists for symmetry with FinancialReportJsonConverter and must not lose a member.
        // Null members are skipped on write because absence and null bind identically on read, so the
        // comparison is on values rather than on bytes.
        var original = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":42,"stock":40,"Other":2,"cryptocurrency":2,"formType":"Annual Report"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var json = JsonSerializer.Serialize(original, FmpJsonContext.Default.ListSenateNetWorthSummary);
        var again = JsonSerializer.Deserialize(json, FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(again);
        Assert.Equal("X000001", row.SenateId);
        Assert.Equal(2024, row.Year);
        Assert.Equal(42m, row.Total);
        Assert.Equal(40m, row.Stock);
        Assert.Equal(2m, row.Other);
        Assert.Null(row.Trusts);
        Assert.Equal(2, row.UnmappedFields.Count);
        Assert.Equal(2m, row.UnmappedFields["cryptocurrency"].GetDecimal());
        Assert.Equal("Annual Report", row.UnmappedFields["formType"].GetString());
        Assert.DoesNotContain("trusts", json, StringComparison.Ordinal);
    }
```

`CongressTests.cs` needs `using System.Reflection;` for `GetCustomAttribute` and `using System.Text.Json.Serialization;` for `JsonPropertyNameAttribute`; add both to the `using` block at the top of the file, keeping it alphabetical.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CongressTests" 2>&1 | tail -20`

Expected: **build failure**, `CS1061` — `'SenateNetWorthSummary' does not contain a definition for 'UnmappedFields'`. Correct RED: the property does not exist.

- [ ] **Step 3: Add `UnmappedFields`, the attribute, and the converter**

Four edits to `src/FmpDotNet/Models/SenateNetWorth.cs`.

**(a) Usings.** The file currently opens with:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;
```

Replace with:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;
```

**(b) Class summary.** In the summary Task 1 wrote, replace the sentence
`Three samples, three undercounts, which is why a catch-all for names this type does not know follows the typed fields.`
with
`Three samples, three undercounts, which is why <see cref="UnmappedFields"/> exists.`
— keeping the line wrap tidy. Then, immediately before the closing `</para></summary>` of that same summary block (i.e. after the "Every one of the 24 money fields is decimal" paragraph), insert one more paragraph:

```csharp
///
/// <para><b>Carries <see cref="SenateNetWorthSummaryJsonConverter"/>, which is what feeds
/// <see cref="UnmappedFields"/> — and the dictionary is why this record's value equality is not meaningful.</b>
/// A dictionary member compares by reference, so two rows byte-identical on the wire are not <c>==</c>.
/// <see cref="AsReportedStatement"/> and <see cref="RevenueSegmentation"/> carry the same cost for the same
/// reason, and nothing in the SDK compares rows of this type.</para></summary>
```

(The existing `</para></summary>` that closed the decimal paragraph becomes `</para>`, and this block supplies the new closing.)

**(c) Attribute and property.** Directly above `public sealed record SenateNetWorthSummary`, add the attribute line:

```csharp
[JsonConverter(typeof(SenateNetWorthSummaryJsonConverter))]
public sealed record SenateNetWorthSummary
```

And inside the record, after the `OtherLiabilities` property Task 1 added (the last one), add:

```csharp

    /// <summary>Every key FMP sent that this type does not name, under its wire spelling. Never
    /// <see langword="null"/>; empty when there was nothing unrecognised — which, measured 2026-09-01, is every
    /// one of the 3,425 rows across all 535 members.
    ///
    /// <para><b>Typed <see cref="JsonElement"/> rather than <see cref="decimal"/>, because what arrives here is by
    /// definition unmeasured.</b> A <c>decimal</c> dictionary throws on a string, and a throw costs the whole
    /// response. The likeliest key to appear here is not a 25th money bucket but an envelope field copied from
    /// <c>senate-net-worth</c>, where <c>formType</c>, <c>filingDate</c> and <c>link</c> are strings on all
    /// 67,801 rows. Read a number with <c>UnmappedFields["name"].GetDecimal()</c>, and check
    /// <see cref="JsonElement.ValueKind"/> first on a key you have not measured.</para>
    ///
    /// <para>This auto-property's <c>= Empty</c> initialiser is the form <see cref="AsReportedStatement.Data"/>
    /// documents as unsafe under the generator's object-initialiser binding. It is safe here for the reason
    /// <see cref="FinancialReport.Sections"/> gives: the type carries a <see cref="JsonConverterAttribute"/>, so
    /// the generator emits the value-converter path and the initialiser is never bypassed.</para></summary>
    public IReadOnlyDictionary<string, JsonElement> UnmappedFields { get; init; } =
        ReadOnlyDictionary<string, JsonElement>.Empty;
```

No `[JsonPropertyName]` on it — it has no single wire name, and `Binding.Unbound` and the smoke sweep both key off that attribute's absence to treat it as computed rather than bound.

**(d) The converter.** Append at the end of the file, after the record's closing brace:

```csharp

/// <summary>Splits <c>stable/senate-net-worth-aggregated</c>'s flat object into the 27 members
/// <see cref="SenateNetWorthSummary"/> names and everything else.
///
/// <para><b>Hand-written rather than <c>[JsonExtensionData]</c>, for the reason
/// <see cref="FinancialReportJsonConverter"/> gives:</b> that attribute demands a public, mutable
/// <c>Dictionary&lt;string, JsonElement&gt;</c> on a record whose other collections are read-only.</para>
///
/// <para><b>The named members bind exactly as they would under <see cref="FmpJsonContext"/>.</b> The context
/// sets <c>PropertyNameCaseInsensitive</c> and <c>AllowReadingFromString</c>, and a converter bypasses both, so
/// they are re-implemented here: a name matches regardless of case, a money field reads a JSON number or a
/// numeric string, a null reads as <see langword="null"/>, and anything else throws <see cref="JsonException"/>
/// as the generated binder would. No caller can tell from the typed members that a converter is present. Only
/// a key the type does not name reaches <see cref="SenateNetWorthSummary.UnmappedFields"/>, under FMP's
/// spelling.</para>
///
/// <para>Null members are skipped on write, because absence and null bind identically on read.</para></summary>
public sealed class SenateNetWorthSummaryJsonConverter : JsonConverter<SenateNetWorthSummary>
{
    /// <inheritdoc/>
    public override SenateNetWorthSummary Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A net worth summary must be a JSON object.");

        // Every property, keyed as FmpJsonContext keys them — case-insensitively — so `Other` and `other`
        // reach the same member. Dictionary keeps the key string as first inserted, so a leftover keeps FMP's
        // spelling for UnmappedFields. ParseValue rather than a JsonDocument so nothing needs disposing and the
        // leftovers can outlive this call.
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            reader.Read();
            fields[name] = JsonElement.ParseValue(ref reader);
        }

        decimal? Money(string wireName) =>
            fields.Remove(wireName, out var element) ? ReadMoney(element, wireName) : null;

        // Object-initialiser members are evaluated in textual order, so UnmappedFields — which is whatever the
        // 27 named lookups above it did NOT remove — must stay last.
        return new SenateNetWorthSummary
        {
            SenateId = fields.Remove("senateID", out var senateId) ? ReadText(senateId, "senateID") : null,
            Year = fields.Remove("year", out var year) ? ReadYear(year) : null,
            Total = Money("total"),
            RevolvingAndCreditLines = Money("revolvingAndCreditLines"),
            SalaryAndWages = Money("salaryAndWages"),
            BusinessLiabilities = Money("businessLiabilities"),
            RealEstateLiabilities = Money("realEstateLiabilities"),
            MutualFundsAndEtfs = Money("mutualFundsAndETFs"),
            CashAndCashEquivalents = Money("cashAndCashEquivalents"),
            OwnershipInterest = Money("ownershipInterest"),
            Stock = Money("stock"),
            GovernmentSecurities = Money("governmentSecurities"),
            OtherAssets = Money("otherAssets"),
            PensionAndRetirementAssets = Money("pensionAndRetirementAssets"),
            RealEstate = Money("realEstate"),
            Trusts = Money("trusts"),
            Other = Money("Other"),
            BusinessAndSelfEmployment = Money("businessAndSelfEmployment"),
            PensionAndRetirementIncome = Money("pensionAndRetirementIncome"),
            OtherIncome = Money("otherIncome"),
            SpousalIncome = Money("spousalIncome"),
            InvestmentAndCapitalGains = Money("investmentAndCapitalGains"),
            Options = Money("options"),
            AssetBackedSecurities = Money("assetBackedSecurities"),
            PersonalLiabilities = Money("personalLiabilities"),
            EducationLiabilities = Money("educationLiabilities"),
            OtherLiabilities = Money("otherLiabilities"),
            UnmappedFields = fields.Count == 0
                ? ReadOnlyDictionary<string, JsonElement>.Empty
                : new Dictionary<string, JsonElement>(fields, StringComparer.Ordinal),
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SenateNetWorthSummary value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.SenateId is { } senateId) writer.WriteString("senateID", senateId);
        if (value.Year is { } year) writer.WriteNumber("year", year);
        WriteMoney(writer, "total", value.Total);
        WriteMoney(writer, "revolvingAndCreditLines", value.RevolvingAndCreditLines);
        WriteMoney(writer, "salaryAndWages", value.SalaryAndWages);
        WriteMoney(writer, "businessLiabilities", value.BusinessLiabilities);
        WriteMoney(writer, "realEstateLiabilities", value.RealEstateLiabilities);
        WriteMoney(writer, "mutualFundsAndETFs", value.MutualFundsAndEtfs);
        WriteMoney(writer, "cashAndCashEquivalents", value.CashAndCashEquivalents);
        WriteMoney(writer, "ownershipInterest", value.OwnershipInterest);
        WriteMoney(writer, "stock", value.Stock);
        WriteMoney(writer, "governmentSecurities", value.GovernmentSecurities);
        WriteMoney(writer, "otherAssets", value.OtherAssets);
        WriteMoney(writer, "pensionAndRetirementAssets", value.PensionAndRetirementAssets);
        WriteMoney(writer, "realEstate", value.RealEstate);
        WriteMoney(writer, "trusts", value.Trusts);
        WriteMoney(writer, "Other", value.Other);
        WriteMoney(writer, "businessAndSelfEmployment", value.BusinessAndSelfEmployment);
        WriteMoney(writer, "pensionAndRetirementIncome", value.PensionAndRetirementIncome);
        WriteMoney(writer, "otherIncome", value.OtherIncome);
        WriteMoney(writer, "spousalIncome", value.SpousalIncome);
        WriteMoney(writer, "investmentAndCapitalGains", value.InvestmentAndCapitalGains);
        WriteMoney(writer, "options", value.Options);
        WriteMoney(writer, "assetBackedSecurities", value.AssetBackedSecurities);
        WriteMoney(writer, "personalLiabilities", value.PersonalLiabilities);
        WriteMoney(writer, "educationLiabilities", value.EducationLiabilities);
        WriteMoney(writer, "otherLiabilities", value.OtherLiabilities);
        foreach (var (name, element) in value.UnmappedFields)
        {
            writer.WritePropertyName(name);
            element.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static void WriteMoney(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is { } money) writer.WriteNumber(name, money);
    }

    // The three readers below are the AllowReadingFromString + case-insensitive contract of FmpJsonContext,
    // restated for the three shapes this object carries. Each throws on a type it was not measured to carry,
    // because the generated binder would, and a converter that is quietly more lenient than every other model
    // would be a second binding contract nobody asked for.

    private static decimal? ReadMoney(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.String when decimal.TryParse(
            element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new JsonException($"'{name}' must be a number or a numeric string, not {element.ValueKind}."),
    };

    private static int? ReadYear(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Number => element.GetInt32(),
        JsonValueKind.String when int.TryParse(
            element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new JsonException($"'year' must be an integer or an integral string, not {element.ValueKind}."),
    };

    private static string? ReadText(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => element.GetString(),
        _ => throw new JsonException($"'{name}' must be a string, not {element.ValueKind}."),
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~CongressTests" 2>&1 | tail -5`

Expected: PASS — every `CongressTests` test, including Task 1's and the existing `H000601` fixture test, which now bind through the converter.

**If `A_category_the_type_does_not_name_lands_in_UnmappedFields_under_its_wire_spelling` fails with the key absent** — i.e. `UnmappedFields` is empty and `cryptocurrency` vanished — the source generator did not route `List<SenateNetWorthSummary>` through the type-level attribute. That is not expected (`FinancialReport` is bound this way today), but the fix is known: add `[JsonSerializable(typeof(SenateNetWorthSummary))]` immediately after the existing `[JsonSerializable(typeof(List<SenateNetWorthSummary>))]` line in `src/FmpDotNet/Serialization/FmpJsonContext.cs`, with the comment `// The bare type as well as the list, so the type-level converter is honoured on the element — see SenateNetWorthSummaryJsonConverter.`, and report that it was needed.

- [ ] **Step 5: Build clean and run the whole unit suite**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | tail -3 && dotnet test tests/FmpDotNet.Tests 2>&1 | tail -3`

Expected: `0 Warning(s)`, `0 Error(s)`; 1,443 passed, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Models/SenateNetWorth.cs tests/FmpDotNet.Tests/CongressTests.cs
git commit -m "feat(congress): surface unrecognised senate-net-worth-aggregated keys in UnmappedFields (#57)

A hand-written converter, on the FinancialReportJsonConverter pattern, binds
the 27 named members with the context's case-insensitive and
numeric-string rules and hands anything else to an IReadOnlyDictionary of
JsonElement — JsonElement rather than decimal because the likeliest unmodelled
key is a string envelope field from the sibling path, and a decimal
dictionary would turn that into a total outage.

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 3: The endpoint doc and the live sweep

**Files:**
- Modify: `src/FmpDotNet/Endpoints/CongressEndpoints.cs` — `GetNetWorthSummaryAsync`'s remarks (lines 299–304)
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs` — `SenateId` (line 386–398) and a new constant after `HouseMemberId`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` — the `senateId` dispatch (lines 364–373)
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded

**Interfaces:**
- Consumes: `SenateNetWorthSummary.UnmappedFields` from Task 2 (its emptiness is what the baseline records); `Endpoints.CongressEndpoints.GetNetWorthSummaryAsync` as a `nameof` target.
- Produces: `LiveApi.NetWorthSummarySenateId`, used only by `Probe.cs`.

This task has no offline RED/GREEN: the sweep's assertion *is* the baseline diff, and the diff is read against a written expectation before it is committed. The unit suite guards the `Probe.cs` change through `SweepCoverageTests`, which already fails if a `when` arm names a method that does not exist.

- [ ] **Step 1: Rewrite `GetNetWorthSummaryAsync`'s remarks**

In `CongressEndpoints.cs`, the method's doc opens:

```csharp
    /// <summary>One Senator's net worth by year, totalled by category —
    /// <c>stable/senate-net-worth-aggregated</c>.
    ///
    /// <para>One row per reporting year. Measured 2026-08-29, <c>H000601</c> answered six, 2019 through
    /// 2024 — the aggregate of what <see cref="GetNetWorthAsync"/> returns line by line.</para>
    ///
    /// <para><b>No <c>totalsCol</c>: it is accepted and ignored.</b>
```

Replace the middle paragraph — the one beginning `One row per reporting year.` — with these two:

```csharp
    /// <para>One row per reporting year — the aggregate of what <see cref="GetNetWorthAsync"/> returns line by
    /// line. Measured 2026-09-01 across every member <see cref="GetProfilesAsync"/> enumerates, 455 of 535
    /// answer between one and twelve rows each, 3,425 in all, and 80 answer an empty list.</para>
    ///
    /// <para><b>The row shape varies by member, and the record was once modelled from one.</b> FMP sends 27
    /// keys across the population and each member carries the subset they have ever disclosed; <c>H000601</c>
    /// carries 16, which is how this type shipped with 16 properties and dropped eleven categories on 91% of
    /// rows (#57). All 27 bind now, and a key the type does not name lands in
    /// <see cref="SenateNetWorthSummary.UnmappedFields"/> rather than vanishing.</para>
```

- [ ] **Step 2: Add the second `senateID` constant and the paragraph on the first**

In `LiveApi.cs`, `SenateId`'s doc currently ends with the paragraph beginning `<b>A Senator cannot probe the House path.</b>` and `See <see cref="HouseMemberId"/>.</para></summary>`. Change that closing to `</para>` and append, before the new `</summary>`:

```csharp
    ///
    /// <para><b>And he no longer probes <c>senate-net-worth-aggregated</c>.</b> His six rows there carry exactly
    /// the 16 keys the record was first modelled from — because they are the rows it was modelled from — and
    /// none of the eleven #57 added, so a sweep keyed on him stayed green through that defect. That probe uses
    /// <see cref="NetWorthSummarySenateId"/>; this constant keeps the two paths he does answer.</para></summary>
```

Then, directly after the `HouseMemberId` constant (`public const string HouseMemberId = "P000197";`) and before the `HouseNameQuery` doc, add:

```csharp

    /// <summary>A Senator's Bioguide identifier for <c>senate-net-worth-aggregated</c> alone — Chuck Grassley,
    /// <c>G000581</c>.
    ///
    /// <para><b>Chosen because his rows carry 21 of the path's 27 keys, the most of any member.</b> Measured
    /// 2026-09-01 across all 535 members, no member carries all 27, so this is a trade rather than a whole: he
    /// carries seven of the eleven keys #57 added — including <c>Other</c>, <c>personalLiabilities</c> and
    /// <c>educationLiabilities</c>, the three most often non-zero — and lacks <c>salaryAndWages</c> and
    /// <c>businessLiabilities</c> of the original sixteen, which <see cref="SenateId"/> carries, as well as
    /// <c>options</c>, <c>assetBackedSecurities</c>, <c>spousalIncome</c> and <c>investmentAndCapitalGains</c>.
    /// A rename of any of those six is invisible to this probe.</para>
    ///
    /// <para><b>Its own constant rather than a new value for <see cref="SenateId"/>, because he answers zero
    /// rows on <c>senate-trades-by-id</c>.</b> Pointing the shared constant at him would record <c>rows 0</c>
    /// on the trade probe — the baseline that matches itself green forever. The same separation
    /// <see cref="HouseNameQuery"/> and <see cref="FundNameQuery"/> exist for.</para>
    ///
    /// <para>The baseline records <c>null UnmappedFields</c> for this probe, and that line is the detector
    /// the catch-all was added for: the day FMP sends a 28th key, it flips to <c>set</c>.</para></summary>
    public const string NetWorthSummarySenateId = "G000581";
```

- [ ] **Step 3: Add the dispatch arm in `Probe.cs`**

In `Probe.cs`, the `senateId` dispatch currently reads:

```csharp
                "senateId" when parameter.Member.Name
                        == nameof(Endpoints.CongressEndpoints.GetHouseTradesByMemberAsync)
                    => LiveApi.HouseMemberId,
                "senateId" => LiveApi.SenateId,
```

Insert a second `when` arm between them:

```csharp
                "senateId" when parameter.Member.Name
                        == nameof(Endpoints.CongressEndpoints.GetHouseTradesByMemberAsync)
                    => LiveApi.HouseMemberId,
                // The aggregated net-worth path gets its own member: the one the other two Senate paths use
                // carries none of the eleven keys #57 added, so a sweep keyed on him could not see the defect.
                // See NetWorthSummarySenateId for why it is a second constant and not a new value.
                "senateId" when parameter.Member.Name
                        == nameof(Endpoints.CongressEndpoints.GetNetWorthSummaryAsync)
                    => LiveApi.NetWorthSummarySenateId,
                "senateId" => LiveApi.SenateId,
```

- [ ] **Step 4: Build clean and run the unit suite**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | tail -3 && dotnet test tests/FmpDotNet.Tests 2>&1 | tail -3`

Expected: `0 Warning(s)`, `0 Error(s)`; 1,443 passed. (`SweepCoverageTests` in the smoke project also compiles here; it is what would fail if the `nameof` were wrong.)

- [ ] **Step 5: Re-record the ordinary smoke baseline and read the diff**

The key lives in `.env` as `FMP_API_KEY=…`. Load it into the environment for one command and never print it:

```bash
set -a && . ./.env && set +a && FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests 2>&1 | tail -5
git diff --stat
git diff tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
```

Expected test output: every live test passed (22 on `master`; the count does not change). Expected diff — **only** the `[Congress.GetNetWorthSummaryAsync]` block changes, and it changes exactly like this:

| line | before | after | why |
|---|---|---|---|
| `set BusinessLiabilities` | present | **`null BusinessLiabilities`** | `G000581` lacks it; `H000601` had it |
| `set SalaryAndWages` | present | **`null SalaryAndWages`** | same |
| `set Other` | — | **new** | seven of the eleven he carries |
| `set BusinessAndSelfEmployment` | — | new | |
| `set EducationLiabilities` | — | new | |
| `set OtherIncome` | — | new | |
| `set OtherLiabilities` | — | new | |
| `set PensionAndRetirementIncome` | — | new | |
| `set PersonalLiabilities` | — | new | |
| `null AssetBackedSecurities` | — | new | the four he does not carry |
| `null InvestmentAndCapitalGains` | — | new | |
| `null Options` | — | new | |
| `null SpousalIncome` | — | new | |
| `null UnmappedFields` | — | **new** | empty collection; `Populated` treats it as not populated, and the file spells that `null` — **the detector** |
| every other line in the block | | unchanged | |

`outcome rows` stays. The file writes every `set` line, then every `null` line, each group sorted ordinally — so the exact position of a line may differ from this table; the *set* of changes must not. (The prefix is `null`, not `unset`: `Baseline.cs` spells an unpopulated property with `NullPrefix`, and the baseline on `master` carries 30 such lines.) If anything outside this block changed, or anything in it changed differently, **stop and report the diff** rather than committing it — a baseline commit that carries an unexplained change is the one thing the smoke suite exists to prevent.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Endpoints/CongressEndpoints.cs tests/FmpDotNet.SmokeTests/LiveApi.cs \
        tests/FmpDotNet.SmokeTests/Probe.cs tests/FmpDotNet.SmokeTests/baseline-ordinary.txt
git commit -m "test(smoke): probe senate-net-worth-aggregated with a member who carries the new keys (#57)

H000601's rows carry exactly the 16 keys the record was modelled from and
none of the eleven, so the sweep was green through the defect. G000581 carries
21 of 27 — the most of any member — and gets his own constant because he
answers nothing on senate-trades-by-id. The re-recorded baseline gains
\`null UnmappedFields\`, which flips to \`set\` the day FMP adds a bucket.

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 4: Corrections to the earlier record

**Files:**
- Modify: `src/FmpDotNet/Serialization/ShapeConverters.cs` — `NetWorthRangeJsonConverter`'s doc (lines 146–158)
- Modify: `docs/superpowers/specs/2026-08-29-senate-and-house-trading-measurements.md` — after line 91 and after line 137
- Modify: `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-measurements.md` — two sentences
- Modify: `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-design.md` — one sentence

**Interfaces:** none. Documentation only; the build is the test (doc comments compile).

- [ ] **Step 1: Upgrade `NetWorthRangeJsonConverter`'s two sample claims**

In `ShapeConverters.cs`, the converter's doc has these two paragraphs:

```csharp
/// <para><b>This converter is not a convenience.</b> Measured 2026-08-29 over 250 rows for one filer,
/// <c>incomeRange</c> was an object on 136, <c>null</c> on 100 and <c>""</c> on 14.
/// <see cref="System.Text.Json.JsonSerializer"/> cannot read a string into an object, so a plain
/// <see cref="Models.NetWorthRange"/> property throws on those 14 — and the throw aborts the whole array
/// rather than the row, so on that filer 14 rows cost all 250.</para>
///
/// <para><b>Applied to <c>incomeRange</c> only.</b> Its sibling <c>valueRange</c> was an object on all 214
/// rows where it was present and never a string; putting this converter there too would assert a wire form
/// that was never measured.</para></summary>
```

Replace them with:

```csharp
/// <para><b>This converter is not a convenience.</b> Measured 2026-08-29 over 250 rows for one filer,
/// <c>incomeRange</c> was an object on 136, <c>null</c> on 100 and <c>""</c> on 14.
/// <see cref="System.Text.Json.JsonSerializer"/> cannot read a string into an object, so a plain
/// <see cref="Models.NetWorthRange"/> property throws on those 14 — and the throw aborts the whole array
/// rather than the row, so on that filer 14 rows cost all 250. Re-measured 2026-09-01 across all 535 members
/// and 67,801 rows (#57): the key still takes exactly those three forms and no fourth.</para>
///
/// <para><b>Applied to <c>incomeRange</c> only.</b> Its sibling <c>valueRange</c> was an object on all 214
/// rows where it was present and never a string; putting this converter there too would assert a wire form
/// that was never measured. Still true at population scale — across the same 67,801 rows, <c>valueRange</c> is
/// <c>null</c> or an object and never a string.</para></summary>
```

- [ ] **Step 2: Add the two correction blockquotes to the 2026-08-29 measurements**

In `docs/superpowers/specs/2026-08-29-senate-and-house-trading-measurements.md`, directly after the table row

```
| net worth aggregate | `senate-net-worth-aggregated` | 16 |
```

and its following blank line, and before `### \`senate-latest\` is the one trade feed missing a field`, insert:

```markdown
> **The aggregate has 27 keys, not 16 — corrected 2026-09-01 (#57).** The 16 came from one member's six rows,
> and the row shape on that path is per member: each carries the categories they have ever disclosed. Across
> all 535 members the union is 27, and 91% of rows carry at least one of the eleven this count missed. See
> [the field-set measurements](2026-09-01-senate-net-worth-fields-measurements.md). The row is kept because it
> was a correct reading of what it looked at, and deleting it would erase why one member looked like a whole.

```

Then, directly after the paragraph

```
The last row is the trap, not the exemption: six rows all landing on bare integers says nothing about the
seventh. Every money field on this record is `decimal?`.
```

and before `## \`debtDetails\` carries three JSON types on one field`, insert:

```markdown
> **"14 money fields" became 24, and "8 flip" became 18 — corrected 2026-09-01 (#57).** Across the whole
> population, 18 of the 25 numeric keys appear with a decimal point on some row. The seven that never do include
> five income categories that are zero on every row, which is the same lesson as the last row of this table,
> only more so. See [the field-set measurements](2026-09-01-senate-net-worth-fields-measurements.md).

```

- [ ] **Step 3: Record two findings the plan surfaced in the new measurements doc**

In `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-measurements.md`:

(a) In *The correction that matters*, the paragraph beginning `So the model is missing **eleven** fields, not nine, and the row shape is per-member:` — replace its second sentence, `it is the union of the categories that member has ever disclosed.`, with:

```markdown
every row of a given member carries the same key set — checked on all 455 members with rows, and not one has
two shapes — and that set is the categories the member has ever disclosed.
```

(b) In *The live sweep cannot see this defect*, the final paragraph beginning `**A fixture covering all 27 keys needs five members' rows:**` — replace the whole paragraph with:

```markdown
**A fixture covering all 27 keys needs five members' rows, and a sixth makes it a better fixture:** `G000581`
for seven, plus one row each from `K000375` (`assetBackedSecurities`), `M001160` (`options`), `Q000023`
(`spousalIncome`) and `C001061` (`investmentAndCapitalGains`). Verified: that union is exactly the eleven.
`S001145`'s 2018 row is added for `pensionAndRetirementIncome` — one of its four non-zero rows in the
population, and a decimal-point value, `289473.83`, where every other member's rows carry a zero.
```

- [ ] **Step 4: Match the design doc's fixture sentence**

In `docs/superpowers/specs/2026-09-01-senate-net-worth-fields-design.md`, under *Testing*, the paragraph begins:

```markdown
Offline, in `CongressTests`, against a new fixture assembled from real rows of five members — `G000581`,
`K000375`, `M001160`, `Q000023`, `C001061` — which between them carry all 27 keys (verified: their union of
unmodelled keys is exactly the eleven).
```

Replace with:

```markdown
Offline, in `CongressTests`, against a new fixture assembled from real rows of six members — `G000581`,
`K000375`, `M001160`, `Q000023`, `C001061`, `S001145` — which between them carry all 27 keys (verified: the
first five's union of unmodelled keys is exactly the eleven; the sixth supplies the one non-zero,
decimal-point `pensionAndRetirementIncome` the population offers).
```

- [ ] **Step 5: Build clean and run the unit suite**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | tail -3 && dotnet test tests/FmpDotNet.Tests 2>&1 | tail -3`

Expected: `0 Warning(s)`, `0 Error(s)`; 1,443 passed.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/Serialization/ShapeConverters.cs \
        docs/superpowers/specs/2026-08-29-senate-and-house-trading-measurements.md \
        docs/superpowers/specs/2026-09-01-senate-net-worth-fields-measurements.md \
        docs/superpowers/specs/2026-09-01-senate-net-worth-fields-design.md
git commit -m "docs: correct the 16-key count and the 8-of-14 flip figure at population scale (#57)

The 2026-08-29 measurements keep their claims with a dated correction, in
the form #49 used on #46. NetWorthRangeJsonConverter's 250-row and 214-row
claims are re-stated on 67,801 rows, where they still hold.

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

## Self-Review

**Spec coverage**, section by section against the design doc:

| design section | task |
|---|---|
| The decision — eleven typed fields | 1 |
| The decision — a catch-all | 2 |
| The catch-all holds `JsonElement` | 2 (property type, and the string-key test) |
| The mechanism — rule 1, context parity | 2 (`ReadMoney`/`ReadYear`/`ReadText`; numeric-string, garbage, case and null tests) |
| The mechanism — rule 2, ordinal unmapped keys | 2 (`new Dictionary(fields, StringComparer.Ordinal)`; wire-spelling test) |
| The mechanism — rule 3, attributes vs table | 2 (the drift test) |
| The mechanism — write path skips nulls | 2 (round-trip test asserts `trusts` absent from the JSON) |
| The mechanism — `= Empty` initialiser is safe | 2 (property doc; the empty-and-never-null test) |
| The mechanism — no new context registration | 2 (Step 4's fallback names the one-line fix if this is wrong) |
| The result type — member table | 1, 2 |
| The result type — `Other` two-signed | 1 (its doc) |
| The result type — income fields zero | 1 (five docs plus `SalaryAndWages`) |
| The result type — equality wart | 2 (class summary paragraph) |
| The live sweep — second constant, arm, baseline expectation | 3 |
| Documentation to correct 1–2 (`SenateNetWorthSummary`, `SalaryAndWages`) | 1 |
| Documentation to correct 3 (`GetNetWorthSummaryAsync`) | 3 |
| Documentation to correct 4 (`LiveApi.SenateId`) | 3 |
| Documentation to correct 5 (`CongressTests` comment) | 1 |
| Documentation to correct 6 (2026-08-29 measurements) | 4 |
| Documentation to correct 7 (`NetWorthRangeJsonConverter`) | 4 |
| Testing table — every row | 1 (fields, existing fixture), 2 (the other nine) |
| Out of scope | nothing in this plan derives, resolves `Other`, or adds a dictionary elsewhere |

**Placeholder scan:** no `TBD`/`TODO`; every code step has its code; no "similar to Task N"; Task 2's fallback for the context registration gives the exact line rather than "register it if needed".

**Type consistency:** `UnmappedFields` is `IReadOnlyDictionary<string, JsonElement>` in Task 2's property, converter and all eight tests; `NetWorthSummarySenateId` is spelled identically in `LiveApi.cs`, `Probe.cs` and the `SenateId` cref; the eleven property names in Task 1's record, Task 1's test, Task 2's converter (`Read` and `Write`) and Task 3's baseline table are the same eleven — `Other`, `BusinessAndSelfEmployment`, `PensionAndRetirementIncome`, `OtherIncome`, `SpousalIncome`, `InvestmentAndCapitalGains`, `Options`, `AssetBackedSecurities`, `PersonalLiabilities`, `EducationLiabilities`, `OtherLiabilities`; the existing property `MutualFundsAndEtfs` (not `ETFs`) is spelled as the record spells it in both converter halves.

**Numbers:** every figure in a doc comment above — 535, 455, 80, 3,425, 27, 24, 16, 21, 2013–2024, one to twelve, 2,907, 246, 228, 44, 18 of 25, 2,552, 518, 1,118, 1,193, four, 341, 153, 100, 66, 12, 42, 777, 280, 462, 306, 5 of 119, 342, 98, 6,000,000, −73,000, 2,033, 91%, 3,130, 67,801 — appears in the measurements document with the same value.
