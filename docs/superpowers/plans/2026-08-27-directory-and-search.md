# Directory and Search Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model FMP's six `stable/search-*` paths and eleven remaining directory-style list paths onto the two SDK facades that already exist, taking coverage from 65 of 230 paths to 82.

**Architecture:** Two existing endpoint classes gain methods; no new facade, no `FmpClient` change, no DI change. Fourteen new records go into five model files, and every one is typed from a whole-universe sweep rather than from a single symbol — the crypto supply fields overflow `long` on exactly one of 4,793 rows, which no sample-of-one would have found. Four measured traps drive the design and each gets a test whose failure is otherwise invisible in a passing response.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` source generation via `FmpJsonContext`, NodaTime (`LocalDate`), xUnit v2 (2.9.3).

**Spec:** `docs/superpowers/specs/2026-08-27-directory-and-search-design.md`
**Measurements:** `docs/superpowers/specs/2026-08-27-directory-and-search-measurements.md`

## Global Constraints

- `TreatWarningsAsErrors=true` covers `CS*` and `NU*`. `IsAotCompatible` turns IL2026/IL3050 into build errors — never call a reflection-based `JsonSerializer.Deserialize`; every model goes through `FmpJsonContext`.
- Every new model must be registered in `src/FmpDotNet/Serialization/FmpJsonContext.cs` as `[JsonSerializable(typeof(List<X>))]` or it will not deserialise.
- Every public member carries XML documentation in house style: it records **what was measured, and on what date** (all measurements here are 2026-08-27), and states plainly anything a caller would otherwise get wrong. Where a value is a trap, the documentation is the deliverable, not decoration.
- Wire-shape records that exist only to be unwrapped are `internal` (precedent: `SectorName`, `StockListRow` in `Models/DirectoryNames.cs`).
- Public list-returning methods return `IReadOnlyList<T>`, never null. Single-row lookups return `T?`. Search methods return lists — see Task 6.
- Tests are xUnit `[Fact]`/`[Theory]` with sentence-style method names using underscores, matching `ChartEndpointsTests`.
- **One `StubHandler` response cannot serve more than one call** — `FmpTransport` disposes the response after reading. A test driving N calls builds N responses.
- Every new behaviour is mutation-checked: break the implementation, confirm the *specific* test fails, restore. A mutation that fails to compile is a stronger result than a failing test — record it as such.
- Branch is `feat/directory-and-search`, already created. `master` carries a ruleset requiring the check `.NET — build + test` and a pull request, so the path is branch → PR → green → merge.

## File Structure

**Create:**
- `src/FmpDotNet/Models/SearchResults.cs` — the four search row shapes
- `src/FmpDotNet/Models/ExchangeVariant.cs` — the 36-field v3-era profile
- `src/FmpDotNet/Models/ExchangeInfo.cs` — `available-exchanges`
- `src/FmpDotNet/Models/DirectoryListings.cs` — `FinancialStatementSymbol`, `SymbolChange`, `CikEntry`, `TranscriptSymbol`
- `src/FmpDotNet/Models/AssetClassListings.cs` — `CommodityInfo`, `CryptocurrencyInfo`, `ForexPair`, `IndexInfo`
- `tests/FmpDotNet.Tests/SearchEndpointsTests.cs`
- `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- 13 fixtures under `tests/FmpDotNet.Tests/Fixtures/`

**Modify:**
- `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs` — +12 methods (183 lines now; lands near `BulkEndpoints.cs`'s 386/21, inside the codebase's range, so no split)
- `src/FmpDotNet/Endpoints/SearchEndpoints.cs` — +6 methods
- `src/FmpDotNet/Models/DirectoryNames.cs` — +`CountryName` internal wire record
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — +15 entries
- `tests/FmpDotNet.Tests/EndpointCoverageTests.cs` — `Argument()` name dispatch
- `tests/FmpDotNet.SmokeTests/Probe.cs` — `Argument()` name dispatch
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` — identifier constants
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — re-recorded live
- `README.md` — regenerated table, corrected prose

**Deviation from the spec, decided while planning:** the spec says `search-cusip` and `search-isin` unify their company-name divergence "with internal wire shapes mapped in the endpoint class". That is unnecessary once they are separate public models — each binds its own wire key directly, and both name the C# property `CompanyName`. The unification happens at the property name, so no internal wire types and no endpoint-level mapping are needed. Simpler, same guarantee, and the test in Task 6 still pins it.

---

### Task 1: The two reuses — countries and the ETF list

Proves the existing `Labels()` and `Symbols()` helpers absorb two of the eleven list endpoints with no new public model.

**Files:**
- Modify: `src/FmpDotNet/Models/DirectoryNames.cs` (append)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/available-countries.json`
- Create: `tests/FmpDotNet.Tests/Fixtures/etf-list.head.json`

**Interfaces:**
- Consumes: `DirectoryEndpoints.Labels<T>`, `DirectoryEndpoints.Symbols<T>` (both existing `private static`), `ActivelyTradingRow` (existing `internal`)
- Produces: `DirectoryEndpoints.GetCountriesAsync(CancellationToken) -> Task<IReadOnlyList<string>>`, `DirectoryEndpoints.GetEtfListAsync(CancellationToken) -> Task<IReadOnlyList<CompanySymbol>>`, and the `DirectoryListsTests.Build`/`Fixture` helpers every later Directory task reuses

- [ ] **Step 1: Write the fixtures**

`tests/FmpDotNet.Tests/Fixtures/available-countries.json` — the head of the measured 117-row response:

```json
[
  { "country": "FK" },
  { "country": "MT" },
  { "country": "SG" },
  { "country": "PH" },
  { "country": "US" }
]
```

`tests/FmpDotNet.Tests/Fixtures/etf-list.head.json` — note the key is `name`, matching `actively-trading-list` and **not** `stock-list`'s `companyName`:

```json
[
  { "symbol": "BREM", "name": "iShares Emerging Markets Bond Active ETF" },
  { "symbol": "SPY", "name": "SPDR S&P 500 ETF Trust" },
  { "symbol": "VOO", "name": "Vanguard S&P 500 ETF" }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/DirectoryListsTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary>The eleven list endpoints on <see cref="DirectoryEndpoints"/> that answer "what exists", checked
/// against responses captured live from FMP on 2026-08-27.
///
/// <para>Separate from <see cref="DirectoryEndpointsTests"/>, which pins the two reference vocabularies, and from
/// <see cref="DirectorySymbolsTests"/>, which pins the two symbol directories.</para></summary>
public class DirectoryListsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (DirectoryEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DirectoryEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_country_list_unwraps_to_iso_two_letter_codes()
    {
        var (endpoints, _) = Build(Fixture("available-countries.json"));

        var countries = await endpoints.GetCountriesAsync();

        // Codes, not names. FMP calls the key `country` and sends "FK", not "Falkland Islands" — a caller
        // building a display label needs a lookup, and the measured 117 rows are all two characters.
        Assert.Equal(["FK", "MT", "SG", "PH", "US"], countries);
    }

    [Fact]
    public async Task The_etf_list_reads_the_name_key_that_stock_list_spells_differently()
    {
        var (endpoints, _) = Build(Fixture("etf-list.head.json"));

        var etfs = await endpoints.GetEtfListAsync();

        // The point of the assertion is Name being populated at all. etf-list sends `name`; if this bound
        // StockListRow (`companyName`) every name would be null and the row count would still be 3.
        Assert.Equal(3, etfs.Count);
        Assert.Equal("BREM", etfs[0].Symbol);
        Assert.Equal("iShares Emerging Markets Bond Active ETF", etfs[0].Name);
    }

    [Fact]
    public async Task The_etf_list_asks_for_the_path_fmp_serves()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetEtfListAsync();

        Assert.Equal("/stable/etf-list", handler.Requests.Single().AbsolutePath);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: FAIL to compile — `'DirectoryEndpoints' does not contain a definition for 'GetCountriesAsync'`.

- [ ] **Step 4: Add the `CountryName` wire record**

Append to `src/FmpDotNet/Models/DirectoryNames.cs`:

```csharp
/// <summary>One row of <c>stable/available-countries</c>: an ISO 3166-1 alpha-2 country code wrapped in a
/// single-property object.
///
/// <para>The same packaging as <see cref="SectorName"/> and <see cref="IndustryName"/> under a third key —
/// <c>[{"country":"FK"}, …]</c>, 117 rows measured 2026-08-27 — and unwrapped in the same place and for the same
/// reason, so it is <see langword="internal"/> too.</para>
///
/// <para><b>These are codes, not names.</b> The key is spelled <c>country</c>, which reads like a name, and every
/// measured value is a two-letter code. <c>available-exchanges</c> carries both spellings for the same fact —
/// <see cref="ExchangeInfo.CountryCode"/> and <see cref="ExchangeInfo.CountryName"/> — so a caller who needs
/// display text can join against that rather than shipping its own table.</para></summary>
internal sealed record CountryName
{
    /// <summary>The ISO alpha-2 code. See <see cref="SectorName.Sector"/> for why it is nullable.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }
}
```

- [ ] **Step 5: Register both types in the JSON context**

In `src/FmpDotNet/Serialization/FmpJsonContext.cs`, after `[JsonSerializable(typeof(List<ActivelyTradingRow>))]`:

```csharp
[JsonSerializable(typeof(List<CountryName>))]
```

(`ActivelyTradingRow` is already registered — `etf-list` reuses it.)

- [ ] **Step 6: Add both methods to `DirectoryEndpoints`**

Insert after `GetIndustriesAsync`, before `GetStockListAsync`:

```csharp
    /// <summary>Every country FMP classifies an exchange against, as ISO 3166-1 alpha-2 codes — 117 of them
    /// measured 2026-08-27.
    ///
    /// <para><b>Codes, not names.</b> The wire key is <c>country</c> and the values are <c>"FK"</c>, <c>"MT"</c>,
    /// <c>"SG"</c> — two characters on every measured row. A caller rendering these to a user needs a lookup;
    /// <see cref="GetExchangesAsync(CancellationToken)"/> carries both spellings of the same fact and is the
    /// cheapest join for it.</para>
    ///
    /// <para>Ignores <c>limit</c>, like every list endpoint in this group except <c>cik-list</c> and
    /// <c>symbol-change</c>. Order is the wire order, unsorted — see <see cref="Labels{T}"/>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetCountriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-countries"),
            FmpJsonContext.Default.ListCountryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Country);
    }

    /// <summary>Every ETF symbol FMP carries — 14,567 measured 2026-08-27.
    ///
    /// <para><b>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>.</b> All 14,567 appeared in
    /// that endpoint's 91,845, none outside it — the same relation already measured for
    /// <see cref="GetActivelyTradingAsync(CancellationToken)"/>. So this is a filter of the universe rather than a
    /// separate one, and a caller holding the stock list already has these rows; what this endpoint adds is
    /// knowing <i>which</i> of them are funds, which no field on the stock list says.</para>
    ///
    /// <para><b>The name arrives under <c>name</c>, not <c>companyName</c></b> — the <c>actively-trading-list</c>
    /// spelling rather than the <c>stock-list</c> one, which is why this reuses that endpoint's wire shape. Both
    /// unwrap to <see cref="CompanySymbol"/>; see that type for why the SDK absorbs the inconsistency instead of
    /// publishing it.</para>
    ///
    /// <para>Ignores <c>limit</c>: asking for 5 rows still transfers all 14,567. Order is the wire order.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetEtfListAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/etf-list"),
            FmpJsonContext.Default.ListActivelyTradingRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.Name);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: PASS, 3 tests.

- [ ] **Step 8: Mutation-check the ETF name binding**

Change `FmpJsonContext.Default.ListActivelyTradingRow` to `FmpJsonContext.Default.ListStockListRow` and `static r => r.Name` to `static r => r.CompanyName` in `GetEtfListAsync`.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~The_etf_list_reads_the_name_key"`
Expected: FAIL — `Assert.Equal() Failure: Values differ. Expected: "iShares…" Actual: null`. This is the whole point of the test: the row count stays 3 and only the name goes null.
Restore both.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/DirectoryNames.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/DirectoryEndpoints.cs tests/FmpDotNet.Tests/DirectoryListsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/available-countries.json tests/FmpDotNet.Tests/Fixtures/etf-list.head.json
git commit -m "feat: the country list and the ETF list, both onto shapes that already exist (#25)"
```

---

### Task 2: The four asset-class lists, and the supply that overflows `long`

**Files:**
- Create: `src/FmpDotNet/Models/AssetClassListings.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/commodities-list.json`, `cryptocurrency-list.overflow.json`, `forex-list.head.json`, `index-list.head.json`

**Interfaces:**
- Consumes: `DirectoryListsTests.Build`, `DirectoryListsTests.Fixture` (Task 1)
- Produces: `CommodityInfo`, `CryptocurrencyInfo`, `ForexPair`, `IndexInfo`; `DirectoryEndpoints.GetCommodityListAsync`, `.GetCryptocurrencyListAsync`, `.GetForexListAsync`, `.GetIndexListAsync`, each `(CancellationToken) -> Task<IReadOnlyList<T>>`

- [ ] **Step 1: Write the fixtures**

`commodities-list.json` — `exchange` is null on all 40 measured rows, and `currency` includes `USX` (US cents):

```json
[
  { "symbol": "ZMUSD", "name": "Soybean Meal Futures", "exchange": null, "tradeMonth": "Dec", "currency": "USD" },
  { "symbol": "ZCUSX", "name": "Corn Futures", "exchange": null, "tradeMonth": "Dec", "currency": "USX" },
  { "symbol": "GCUSD", "name": "Gold Futures", "exchange": null, "tradeMonth": "Dec", "currency": "USD" }
]
```

`cryptocurrency-list.overflow.json` — the first row is the measured `SHIBDOGEUSD`, the single row of 4,793 that exceeds `long.MaxValue` on **both** supply fields. The second is fractional, which 953 of 4,792 circulating values are. The third exercises the 1,474 null `totalSupply` rows and the 33 null `icoDate` rows:

```json
[
  { "symbol": "SHIBDOGEUSD", "name": "SHIBADOGE USD", "exchange": "CCC", "icoDate": "2021-10-27",
    "circulatingSupply": 9223372036854776000, "totalSupply": 1.8398528382123738e+23 },
  { "symbol": "MIOTAUSD", "name": "IOTA USD", "exchange": "CCC", "icoDate": "2017-11-09",
    "circulatingSupply": 6304286374.701883, "totalSupply": 6304286374.701883 },
  { "symbol": "NULLSUPPLYUSD", "name": "Null Supply USD", "exchange": "CCC", "icoDate": null,
    "circulatingSupply": 21000000, "totalSupply": null }
]
```

`forex-list.head.json`:

```json
[
  { "symbol": "ARSMXN", "fromCurrency": "ARS", "toCurrency": "MXN",
    "fromName": "Argentine Peso", "toName": "Mexican Peso" },
  { "symbol": "EURUSD", "fromCurrency": "EUR", "toCurrency": "USD",
    "fromName": "Euro", "toName": "US Dollar" }
]
```

`index-list.head.json`:

```json
[
  { "symbol": "^TTIN", "name": "S&P/TSX Capped Industrials Index", "exchange": "TSX", "currency": "CAD" },
  { "symbol": "^GSPC", "name": "S&P 500", "exchange": "SNP", "currency": "USD" }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/FmpDotNet.Tests/DirectoryListsTests.cs`:

```csharp
    [Fact]
    public async Task A_crypto_supply_beyond_long_max_is_read_rather_than_refused()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // SHIBDOGEUSD is the single row of 4,793 measured 2026-08-27 that exceeds long.MaxValue on both supply
        // fields. Typed `long?` this throws a JsonException and costs the whole 4,793-row response, not one field.
        Assert.Equal("SHIBDOGEUSD", coins[0].Symbol);
        Assert.Equal(9223372036854776000m, coins[0].CirculatingSupply);
        Assert.Equal(183985283821237380000000m, coins[0].TotalSupply);
    }

    [Fact]
    public async Task A_fractional_crypto_supply_is_read_rather_than_refused()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // 953 of 4,792 circulating values carried a fractional part on 2026-08-27. A whole-number type refuses
        // every one of them.
        Assert.Equal(6304286374.701883m, coins[1].CirculatingSupply);
    }

    [Fact]
    public async Task A_missing_crypto_supply_reads_as_null_rather_than_zero()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // 1,474 of 4,793 rows omitted totalSupply. Zero would be a claim; null is the absence of one.
        Assert.Null(coins[2].TotalSupply);
        Assert.Null(coins[2].IcoDate);
    }

    [Fact]
    public async Task A_commodity_carries_no_exchange_and_that_is_not_a_fault()
    {
        var (endpoints, _) = Build(Fixture("commodities-list.json"));

        var commodities = await endpoints.GetCommodityListAsync();

        // Null on all 40 measured rows. Pinned so the day it starts arriving is a visible change rather than a
        // silent one, and so the smoke baseline recording it empty reads as correct rather than as drift.
        Assert.All(commodities, c => Assert.Null(c.Exchange));
        Assert.Equal("Dec", commodities[0].TradeMonth);
        // USX is US cents, not a typo for USD. A caller converting prices must not treat the two alike.
        Assert.Equal("USX", commodities[1].Currency);
    }

    [Fact]
    public async Task A_forex_pair_carries_both_sides_of_the_cross()
    {
        var (endpoints, _) = Build(Fixture("forex-list.head.json"));

        var pairs = await endpoints.GetForexListAsync();

        Assert.Equal("ARSMXN", pairs[0].Symbol);
        Assert.Equal("ARS", pairs[0].FromCurrency);
        Assert.Equal("Mexican Peso", pairs[0].ToName);
    }

    [Fact]
    public async Task An_index_carries_its_exchange_and_currency()
    {
        var (endpoints, _) = Build(Fixture("index-list.head.json"));

        var indexes = await endpoints.GetIndexListAsync();

        Assert.Equal("^TTIN", indexes[0].Symbol);
        Assert.Equal("CAD", indexes[0].Currency);
    }

    public static TheoryData<string, Func<DirectoryEndpoints, Task>> AssetClassCalls => new()
    {
        { "/stable/commodities-list", e => e.GetCommodityListAsync() },
        { "/stable/cryptocurrency-list", e => e.GetCryptocurrencyListAsync() },
        { "/stable/forex-list", e => e.GetForexListAsync() },
        { "/stable/index-list", e => e.GetIndexListAsync() },
    };

    [Theory]
    [MemberData(nameof(AssetClassCalls))]
    public async Task Each_asset_class_list_asks_for_the_path_fmp_serves(
        string path, Func<DirectoryEndpoints, Task> call)
    {
        var (endpoints, handler) = Build("[]");

        await call(endpoints);

        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: FAIL to compile — `'DirectoryEndpoints' does not contain a definition for 'GetCryptocurrencyListAsync'`.

- [ ] **Step 4: Write the four models**

Create `src/FmpDotNet/Models/AssetClassListings.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/commodities-list</c> — 40 measured 2026-08-27, the whole set.
///
/// <para>FMP documents this under its Commodity section rather than under Directory. The SDK puts it on
/// <see cref="Endpoints.DirectoryEndpoints"/> anyway, because it answers Directory's question — what exists — and
/// because there is no <c>fmp.Commodity</c> facade for it to join: one
/// <see cref="Endpoints.QuoteEndpoints.GetQuoteAsync"/> already serves commodities alongside every other asset
/// class.</para></summary>
public sealed record CommodityInfo
{
    /// <summary>The symbol as FMP spells it — <c>GCUSD</c>, <c>ZMUSD</c>. Feed it to
    /// <see cref="Endpoints.QuoteEndpoints.GetQuoteAsync"/> or
    /// <see cref="Endpoints.ChartEndpoints.GetEndOfDayAsync"/> unchanged.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The contract's name — <c>Soybean Meal Futures</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary><b>Null on all 40 rows measured 2026-08-27.</b> A field FMP documents and never populates.
    ///
    /// <para>Kept rather than dropped so that the day it starts arriving is a visible change. The smoke suite will
    /// record it empty; that is the measured truth, not drift.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The delivery month as a three-letter abbreviation — <c>"Dec"</c>. <b>Not a date</b>: there is no
    /// year on it, and nothing in the response says which year the front month belongs to.</summary>
    [JsonPropertyName("tradeMonth")] public string? TradeMonth { get; init; }

    /// <summary>The quote currency. <b><c>USX</c> is US cents, not a misspelling of <c>USD</c></b> — both appear
    /// across the 40 rows, and a caller converting prices that treats them alike is out by a factor of 100.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

/// <summary>One row of <c>stable/cryptocurrency-list</c> — 4,793 measured 2026-08-27.
///
/// <para>Filed under Crypto in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>.</para></summary>
public sealed record CryptocurrencyInfo
{
    /// <summary>The pair symbol — <c>BTCUSD</c>, <c>MIOTAUSD</c>. Every measured row quotes against USD.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The display name — <c>IOTA USD</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Always <c>CCC</c> on every measured row — FMP's crypto aggregate, not a venue.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The ICO date, or null. Null on 33 of 4,793 rows; the other 4,760 were ISO <c>uuuu-MM-dd</c> and
    /// none was malformed.</summary>
    [JsonPropertyName("icoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IcoDate { get; init; }

    /// <summary>Coins in circulation.
    ///
    /// <para><b><see cref="decimal"/> rather than a whole-number type, and both halves of that were measured.</b>
    /// 953 of the 4,792 populated values carry a fractional part, and <c>SHIBDOGEUSD</c> reports
    /// <c>9223372036854776000</c> — past <see cref="long.MaxValue"/>. Either alone makes an integer type throw, and
    /// a <see cref="System.Text.Json.JsonException"/> here costs the entire 4,793-row response rather than one
    /// field. Nothing measured came within five orders of magnitude of <see cref="decimal"/>'s ceiling.</para></summary>
    [JsonPropertyName("circulatingSupply")] public decimal? CirculatingSupply { get; init; }

    /// <summary>The maximum supply, or null where the coin does not define one — <b>null on 1,474 of 4,793
    /// rows</b>, so absence is ordinary here rather than exceptional.
    ///
    /// <para>Same typing argument as <see cref="CirculatingSupply"/>, and this field is the more extreme of the
    /// two: <c>SHIBDOGEUSD</c> reports <c>1.8398528382123738e+23</c>, five orders of magnitude past
    /// <see cref="long.MaxValue"/> and still comfortably inside <see cref="decimal"/>.</para></summary>
    [JsonPropertyName("totalSupply")] public decimal? TotalSupply { get; init; }
}

/// <summary>One row of <c>stable/forex-list</c> — 1,551 pairs measured 2026-08-27.
///
/// <para>Filed under Forex in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>.</para></summary>
public sealed record ForexPair
{
    /// <summary>The pair symbol, base then quote with no separator — <c>EURUSD</c>, <c>ARSMXN</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The base currency's ISO code — the <c>ARS</c> of <c>ARSMXN</c>.</summary>
    [JsonPropertyName("fromCurrency")] public string? FromCurrency { get; init; }

    /// <summary>The quote currency's ISO code — the <c>MXN</c> of <c>ARSMXN</c>.</summary>
    [JsonPropertyName("toCurrency")] public string? ToCurrency { get; init; }

    /// <summary>The base currency's name — <c>Argentine Peso</c>.</summary>
    [JsonPropertyName("fromName")] public string? FromName { get; init; }

    /// <summary>The quote currency's name — <c>Mexican Peso</c>.</summary>
    [JsonPropertyName("toName")] public string? ToName { get; init; }
}

/// <summary>One row of <c>stable/index-list</c> — 425 measured 2026-08-27.
///
/// <para>Filed under Indexes in FMP's documentation and placed on <see cref="Endpoints.DirectoryEndpoints"/> here,
/// for the reason given on <see cref="CommodityInfo"/>. Note that the rest of FMP's Indexes section is
/// <c>quote</c> and <c>historical-price-eod</c> re-documented, which <see cref="Endpoints.QuoteEndpoints"/> and
/// <see cref="Endpoints.ChartEndpoints"/> already reach; the constituent lists — S&amp;P 500, Nasdaq, Dow Jones —
/// remain unmodelled.</para></summary>
public sealed record IndexInfo
{
    /// <summary>The index symbol, carat-prefixed — <c>^GSPC</c>, <c>^TTIN</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The index name — <c>S&amp;P/TSX Capped Industrials Index</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The exchange code the index is published under — <c>TSX</c>, <c>SNP</c>. Populated on all 425
    /// measured rows, unlike <see cref="CommodityInfo.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The currency the index is denominated in — <c>CAD</c>, <c>USD</c>. Populated on all 425 rows.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}
```

- [ ] **Step 5: Register the four models**

In `FmpJsonContext.cs`, after the `CountryName` entry:

```csharp
[JsonSerializable(typeof(List<CommodityInfo>))]
[JsonSerializable(typeof(List<CryptocurrencyInfo>))]
[JsonSerializable(typeof(List<ForexPair>))]
[JsonSerializable(typeof(List<IndexInfo>))]
```

- [ ] **Step 6: Add the four methods**

Append to `DirectoryEndpoints`, after `GetEtfListAsync`. Each is a straight pass-through — no unwrapping, because unlike the vocabularies these rows carry structure:

```csharp
    /// <summary>Every commodity FMP carries — 40 measured 2026-08-27, the whole set.
    ///
    /// <para>FMP documents this under Commodity rather than Directory. It lives here because it answers
    /// Directory's question, and because no <c>fmp.Commodity</c> facade exists for it to join — see
    /// <see cref="CommodityInfo"/>.</para>
    ///
    /// <para><b><see cref="CommodityInfo.Exchange"/> is null on every row</b>, and
    /// <see cref="CommodityInfo.Currency"/> distinguishes <c>USD</c> from <c>USX</c>, which is US cents. Ignores
    /// <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CommodityInfo>> GetCommodityListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/commodities-list"), FmpJsonContext.Default.ListCommodityInfo, ct);

    /// <summary>Every cryptocurrency pair FMP carries — 4,793 measured 2026-08-27.
    ///
    /// <para>Filed under Crypto in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>.</para>
    ///
    /// <para><b>The supply fields are <see cref="decimal"/> because a whole-number type refuses real rows.</b> 953
    /// circulating values are fractional and one row exceeds <see cref="long.MaxValue"/> on both fields — see
    /// <see cref="CryptocurrencyInfo.CirculatingSupply"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CryptocurrencyInfo>> GetCryptocurrencyListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/cryptocurrency-list"), FmpJsonContext.Default.ListCryptocurrencyInfo, ct);

    /// <summary>Every forex pair FMP carries — 1,551 measured 2026-08-27.
    ///
    /// <para>Filed under Forex in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ForexPair>> GetForexListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/forex-list"), FmpJsonContext.Default.ListForexPair, ct);

    /// <summary>Every market index FMP carries — 425 measured 2026-08-27.
    ///
    /// <para>Filed under Indexes in FMP's documentation; here for the reason given on
    /// <see cref="CommodityInfo"/>. The <b>constituent</b> lists — S&amp;P 500, Nasdaq, Dow Jones, current and
    /// historical — are a separate six paths and are not modelled. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IndexInfo>> GetIndexListAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/index-list"), FmpJsonContext.Default.ListIndexInfo, ct);
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: PASS, 13 tests (3 from Task 1, 6 facts and 4 theory cases here).

- [ ] **Step 8: Mutation-check the supply typing**

Change `CirculatingSupply` and `TotalSupply` on `CryptocurrencyInfo` from `decimal?` to `long?`.
Run: `dotnet build src/FmpDotNet`
Expected: **BUILD ERROR** — `error CS0266: Cannot implicitly convert type 'decimal' to 'long?'` at the test's assertion, or a `JsonException` at run time if the assertions are loosened. A build error is the stronger outcome; record which one occurred.
Restore `decimal?`.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/AssetClassListings.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/DirectoryEndpoints.cs tests/FmpDotNet.Tests/DirectoryListsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/commodities-list.json \
        tests/FmpDotNet.Tests/Fixtures/cryptocurrency-list.overflow.json \
        tests/FmpDotNet.Tests/Fixtures/forex-list.head.json tests/FmpDotNet.Tests/Fixtures/index-list.head.json
git commit -m "feat: the four asset-class symbol lists, with supply typed from the sweep (#25)"
```

---

### Task 3: Exchanges, statement symbols, and transcript counts

Three list endpoints carrying three separate type hazards: free-text prose where a duration is implied, a literal `"N/A"` where null is implied, and a number that arrives as a string on every row.

**Files:**
- Create: `src/FmpDotNet/Models/ExchangeInfo.cs`
- Create: `src/FmpDotNet/Models/DirectoryListings.cs` (`FinancialStatementSymbol`, `TranscriptSymbol` here; `SymbolChange` and `CikEntry` added in Tasks 4 and 5)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/available-exchanges.json`, `financial-statement-symbol-list.head.json`, `earnings-transcript-list.head.json`

**Interfaces:**
- Consumes: `DirectoryListsTests.Build`, `.Fixture`
- Produces: `ExchangeInfo`, `FinancialStatementSymbol`, `TranscriptSymbol`; `DirectoryEndpoints.GetExchangesAsync`, `.GetFinancialStatementSymbolsAsync`, `.GetTranscriptSymbolsAsync`, each `(CancellationToken) -> Task<IReadOnlyList<T>>`

- [ ] **Step 1: Write the fixtures**

`available-exchanges.json` — includes the `"N/A"` suffix row, the null-delay row (`FSX`, the only one of 63), and three of the four delay spellings:

```json
[
  { "exchange": "AMEX", "name": "New York Stock Exchange Arca", "countryName": "United States of America",
    "countryCode": "US", "symbolSuffix": "N/A", "delay": "Real-time" },
  { "exchange": "ASX", "name": "Australian Securities Exchange", "countryName": "Australia",
    "countryCode": "AU", "symbolSuffix": ".AX", "delay": "20 min" },
  { "exchange": "ATH", "name": "Athens Stock Exchange", "countryName": "Greece",
    "countryCode": "GR", "symbolSuffix": ".AT", "delay": "15 min" },
  { "exchange": "FSX", "name": "Frankfurt Stock Exchange", "countryName": "Germany",
    "countryCode": "DE", "symbolSuffix": ".F", "delay": null }
]
```

`financial-statement-symbol-list.head.json` — `TOELY` is the measured row where trading and reporting currency differ:

```json
[
  { "symbol": "TOELY", "companyName": "Tokyo Electron Limited",
    "tradingCurrency": "USD", "reportingCurrency": "JPY" },
  { "symbol": "AAPL", "companyName": "Apple Inc.",
    "tradingCurrency": "USD", "reportingCurrency": "USD" },
  { "symbol": "NOCUR", "companyName": "No Reporting Currency Corp.",
    "tradingCurrency": "USD", "reportingCurrency": null }
]
```

`earnings-transcript-list.head.json` — the count is a **string** on all 11,178 measured rows:

```json
[
  { "symbol": "INBS", "companyName": "Intelligent Bio Solutions Inc.", "noOfTranscripts": "6" },
  { "symbol": "AAPL", "companyName": "Apple Inc.", "noOfTranscripts": "16" }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `DirectoryListsTests.cs`:

```csharp
    [Fact]
    public async Task An_exchange_delay_is_kept_as_the_prose_fmp_sends()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // Free text, not a duration. Four spellings measured across 63 rows — "Real-time", "20 min", "15 min",
        // "10 min" — with no published mapping, so parsing to a Duration would mean inventing one.
        Assert.Equal("Real-time", exchanges[0].Delay);
        Assert.Equal("20 min", exchanges[1].Delay);
    }

    [Fact]
    public async Task An_exchange_with_no_delay_reads_as_null()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // FSX was the only one of 63 with a null delay on 2026-08-27.
        Assert.Equal("FSX", exchanges[3].Exchange);
        Assert.Null(exchanges[3].Delay);
    }

    [Fact]
    public async Task A_symbol_suffix_of_not_applicable_arrives_as_that_literal_string()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // 5 of 63 rows carry the literal "N/A" rather than null. The SDK does not normalise it — see the model —
        // so this test exists to make the hazard visible rather than to assert a fix.
        Assert.Equal("N/A", exchanges[0].SymbolSuffix);
        Assert.Equal(".AX", exchanges[1].SymbolSuffix);
    }

    [Fact]
    public async Task A_statement_symbol_distinguishes_trading_from_reporting_currency()
    {
        var (endpoints, _) = Build(Fixture("financial-statement-symbol-list.head.json"));

        var symbols = await endpoints.GetFinancialStatementSymbolsAsync();

        // TOELY trades in USD and reports in JPY. Reading either field as "the currency" is wrong for one of them.
        Assert.Equal("USD", symbols[0].TradingCurrency);
        Assert.Equal("JPY", symbols[0].ReportingCurrency);
        // Null on 149 of 68,200 measured rows.
        Assert.Null(symbols[2].ReportingCurrency);
    }

    [Fact]
    public async Task A_transcript_count_arrives_as_a_string_and_reads_as_a_number()
    {
        var (endpoints, _) = Build(Fixture("earnings-transcript-list.head.json"));

        var symbols = await endpoints.GetTranscriptSymbolsAsync();

        // The wire sends "6", quoted, on all 11,178 rows. This passes only because FmpJsonContext sets
        // NumberHandling = AllowReadingFromString — load-bearing here rather than incidental.
        Assert.Equal(6, symbols[0].TranscriptCount);
        Assert.Equal(16, symbols[1].TranscriptCount);
    }

    public static TheoryData<string, Func<DirectoryEndpoints, Task>> ReferenceCalls => new()
    {
        { "/stable/available-countries", e => e.GetCountriesAsync() },
        { "/stable/available-exchanges", e => e.GetExchangesAsync() },
        { "/stable/financial-statement-symbol-list", e => e.GetFinancialStatementSymbolsAsync() },
        { "/stable/earnings-transcript-list", e => e.GetTranscriptSymbolsAsync() },
    };

    [Theory]
    [MemberData(nameof(ReferenceCalls))]
    public async Task Each_reference_list_asks_for_the_path_fmp_serves(
        string path, Func<DirectoryEndpoints, Task> call)
    {
        var (endpoints, handler) = Build("[]");

        await call(endpoints);

        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: FAIL to compile — `'DirectoryEndpoints' does not contain a definition for 'GetExchangesAsync'`.

- [ ] **Step 4: Write `ExchangeInfo`**

Create `src/FmpDotNet/Models/ExchangeInfo.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One exchange from <c>stable/available-exchanges</c> — 63 measured 2026-08-27, the whole set.
///
/// <para>This is the authoritative spelling of the exchange codes that appear on
/// <see cref="CompanyProfile.Exchange"/>, on <see cref="SymbolSearchResult.Exchange"/> and as the
/// <c>exchange</c> argument to <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/> — which answers an
/// unknown exchange with an empty array and HTTP 200 rather than an error, so validating against this list is
/// cheaper than debugging an empty result.</para></summary>
public sealed record ExchangeInfo
{
    /// <summary>The short code — <c>AMEX</c>, <c>ASX</c>, <c>FSX</c>. This is the value the rest of the API
    /// expects.
    ///
    /// <para><b>Note which side of the naming this is.</b> On <see cref="CompanyProfile"/> the code lives under
    /// <c>exchange</c> and the display name under <c>exchangeFullName</c>; on
    /// <see cref="ExchangeVariant"/> those two are the other way round. This field is the code.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The display name — <c>Australian Securities Exchange</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The country's full name — <c>United States of America</c>.</summary>
    [JsonPropertyName("countryName")] public string? CountryName { get; init; }

    /// <summary>The country's ISO alpha-2 code — <c>US</c>. This is the same vocabulary
    /// <see cref="Endpoints.DirectoryEndpoints.GetCountriesAsync"/> returns, so the two join directly.</summary>
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }

    /// <summary>The suffix FMP appends to symbols on this exchange — <c>.AX</c>, <c>.AT</c>.
    ///
    /// <para><b>Five of the 63 rows carry the literal string <c>"N/A"</c> rather than null</b>, measured
    /// 2026-08-27. The SDK does not normalise it, because doing so would hide which value FMP actually sent — but
    /// a caller appending this blindly produces <c>AAPL.N/A</c>. Test for it explicitly, or use
    /// <see cref="Endpoints.SearchEndpoints.GetExchangeVariantsAsync"/>, which answers the same question by
    /// returning the symbols themselves.</para></summary>
    [JsonPropertyName("symbolSuffix")] public string? SymbolSuffix { get; init; }

    /// <summary>How delayed this exchange's quotes are, <b>as free-text prose</b> — <c>"Real-time"</c>,
    /// <c>"15 min"</c>, <c>"20 min"</c>, <c>"10 min"</c>.
    ///
    /// <para><b>A <see cref="string"/> rather than a <see cref="NodaTime.Duration"/>, deliberately.</b> Those four
    /// spellings are every value measured across the 63 rows, and FMP publishes no mapping from them to a
    /// quantity — <c>"Real-time"</c> is not a duration at all. Parsing would mean inventing a contract the API
    /// does not offer, and would then silently mis-report the day a fifth spelling appears.</para>
    ///
    /// <para>Null on one row of 63 (<c>FSX</c>), so absence is possible.</para></summary>
    [JsonPropertyName("delay")] public string? Delay { get; init; }
}
```

- [ ] **Step 5: Write `FinancialStatementSymbol` and `TranscriptSymbol`**

Create `src/FmpDotNet/Models/DirectoryListings.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/financial-statement-symbol-list</c> — the symbols FMP holds statements for,
/// 68,200 measured 2026-08-27.
///
/// <para><b>A strict subset of <c>stable/stock-list</c>'s 91,845</b> — none of the 68,200 fell outside it. So the
/// difference, 23,645 symbols, is exactly the set FMP carries but has no statements for, which is the question
/// this endpoint answers that the stock list cannot.</para></summary>
public sealed record FinancialStatementSymbol
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c> — the <c>stock-list</c> spelling, not the
    /// <c>actively-trading-list</c> one.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The currency the security trades in. Populated on all 68,200 measured rows.</summary>
    [JsonPropertyName("tradingCurrency")] public string? TradingCurrency { get; init; }

    /// <summary>The currency the company reports its statements in, which is <b>not always the one it trades
    /// in</b> — <c>TOELY</c> trades in USD and reports in JPY. Reading either field as "the currency" is wrong for
    /// one of them, and a caller comparing statement figures across symbols must group by this one.
    ///
    /// <para>Null on 149 of 68,200 rows.</para></summary>
    [JsonPropertyName("reportingCurrency")] public string? ReportingCurrency { get; init; }
}

/// <summary>One row of <c>stable/earnings-transcript-list</c> — every symbol FMP holds an earnings-call transcript
/// for, and how many, 11,178 measured 2026-08-27.
///
/// <para>FMP files this under both Directory and Earnings Transcript. It is on
/// <see cref="Endpoints.DirectoryEndpoints"/> because it is a directory: it says what exists, not what any
/// transcript says. <b>The transcripts themselves are not modelled</b> — that is three further paths in the long
/// tail of issue #25.</para></summary>
public sealed record TranscriptSymbol
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>How many transcripts FMP holds for this symbol.
    ///
    /// <para><b>The wire sends this as a quoted string on all 11,178 rows</b> — <c>"noOfTranscripts": "6"</c>, not
    /// <c>6</c>. It binds to an <see cref="int"/> only because <c>FmpJsonContext</c> sets
    /// <c>NumberHandling = AllowReadingFromString</c>; that option is load-bearing for this property rather than
    /// incidental, and removing it would break this endpoint alone.</para>
    ///
    /// <para>The C# name drops FMP's <c>noOf</c> prefix, which is Hungarian for the type the property already
    /// declares.</para></summary>
    [JsonPropertyName("noOfTranscripts")] public int? TranscriptCount { get; init; }
}
```

- [ ] **Step 6: Register the three models**

In `FmpJsonContext.cs`, after the asset-class entries:

```csharp
[JsonSerializable(typeof(List<ExchangeInfo>))]
[JsonSerializable(typeof(List<FinancialStatementSymbol>))]
[JsonSerializable(typeof(List<TranscriptSymbol>))]
```

- [ ] **Step 7: Add the three methods**

Append to `DirectoryEndpoints`:

```csharp
    /// <summary>Every exchange FMP carries, with its country, symbol suffix and quote delay — 63 measured
    /// 2026-08-27, the whole set.
    ///
    /// <para><b>This is the vocabulary to validate an exchange code against.</b>
    /// <see cref="QuoteEndpoints.GetExchangeQuotesAsync"/> answers an unrecognised exchange with an empty array
    /// and HTTP 200, not an error, so a typo there is indistinguishable from an exchange that went dark.</para>
    ///
    /// <para><see cref="ExchangeInfo.Delay"/> is prose, not a duration, and
    /// <see cref="ExchangeInfo.SymbolSuffix"/> is the literal <c>"N/A"</c> on five rows — see those properties.
    /// Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeInfo>> GetExchangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/available-exchanges"), FmpJsonContext.Default.ListExchangeInfo, ct);

    /// <summary>Every symbol FMP holds financial statements for — 68,200 measured 2026-08-27, 5.6 MB of JSON.
    ///
    /// <para><b>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>.</b> None of the 68,200 fell
    /// outside that endpoint's 91,845, so the 23,645-symbol difference is exactly the set FMP lists but has no
    /// fundamentals for — the question to ask before calling
    /// <see cref="StatementEndpoints.GetIncomeStatementAsync"/> across a universe and reading empty results as
    /// "no data this period".</para>
    ///
    /// <para>Carries the reporting currency as well as the trading one, and they differ — see
    /// <see cref="FinancialStatementSymbol.ReportingCurrency"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<FinancialStatementSymbol>> GetFinancialStatementSymbolsAsync(
        CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/financial-statement-symbol-list"),
            FmpJsonContext.Default.ListFinancialStatementSymbol, ct);

    /// <summary>Every symbol FMP holds an earnings-call transcript for, with the count — 11,178 measured
    /// 2026-08-27.
    ///
    /// <para>A directory rather than content: it says which symbols have transcripts and how many, not what any
    /// of them says. <b>The transcripts themselves are not modelled</b> — three further paths in issue #25's long
    /// tail.</para>
    ///
    /// <para>The count arrives as a quoted string on every row; see
    /// <see cref="TranscriptSymbol.TranscriptCount"/>. Ignores <c>limit</c>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<TranscriptSymbol>> GetTranscriptSymbolsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/earnings-transcript-list"), FmpJsonContext.Default.ListTranscriptSymbol, ct);
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: PASS, 22 tests.

- [ ] **Step 9: Mutation-check the string-number tolerance**

In `FmpJsonContext.cs`, remove `NumberHandling = JsonNumberHandling.AllowReadingFromString` from `[JsonSourceGenerationOptions]`.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~A_transcript_count_arrives_as_a_string"`
Expected: FAIL with `JsonException` — the quoted `"6"` cannot bind to `int?`. Note that other tests across the suite will also fail; that is the point — the option is load-bearing in several places.
Restore the option.

- [ ] **Step 10: Commit**

```bash
git add src/FmpDotNet/Models/ExchangeInfo.cs src/FmpDotNet/Models/DirectoryListings.cs \
        src/FmpDotNet/Serialization/FmpJsonContext.cs src/FmpDotNet/Endpoints/DirectoryEndpoints.cs \
        tests/FmpDotNet.Tests/DirectoryListsTests.cs tests/FmpDotNet.Tests/Fixtures/available-exchanges.json \
        tests/FmpDotNet.Tests/Fixtures/financial-statement-symbol-list.head.json \
        tests/FmpDotNet.Tests/Fixtures/earnings-transcript-list.head.json
git commit -m "feat: exchanges, statement symbols and transcript counts (#25)"
```

---

### Task 4: `symbol-change`, and the default that hides 98% of it

One path, and the whole task is the trap: the endpoint answers 100 rows by default and holds 5,456. The failure is invisible in the response, so the test asserts the **request**.

**Files:**
- Modify: `src/FmpDotNet/Models/DirectoryListings.cs` (append `SymbolChange`)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/symbol-change.head.json`

**Interfaces:**
- Consumes: `DirectoryListsTests.Build`, `.Fixture`
- Produces: `SymbolChange`; `DirectoryEndpoints.SymbolChangeRequestLimit` (`public const int`), `DirectoryEndpoints.GetSymbolChangesAsync(CancellationToken) -> Task<IReadOnlyList<SymbolChange>>`

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/symbol-change.head.json`:

```json
[
  { "date": "2026-08-26", "companyName": "Tema MLCC & PowerSemi ETF", "oldSymbol": "SIC", "newSymbol": "PSOX" },
  { "date": "2026-08-24", "companyName": "Endovia Health Sciences, Inc. Common Stock",
    "oldSymbol": "SBEV", "newSymbol": "EDVA" },
  { "date": "2026-08-21", "companyName": "Corgi U.S. Mega-Cap Growth 2x Daily ETF",
    "oldSymbol": "MGKX", "newSymbol": "MEGX" }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `DirectoryListsTests.cs`:

```csharp
    [Fact]
    public async Task A_symbol_change_request_asks_for_more_than_the_hidden_default()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetSymbolChangesAsync();

        // THE POINT OF THIS TEST IS THE URL, NOT THE RESPONSE. Measured 2026-08-27: with no `limit` the endpoint
        // answers 100 rows and holds 5,456. Both responses are HTTP 200 arrays of well-formed rows, so nothing
        // downstream can tell a complete history from a 1.8% sample — the only place the bug is visible is here.
        var query = handler.Requests.Single().Query;
        Assert.Contains($"limit={DirectoryEndpoints.SymbolChangeRequestLimit}", query, StringComparison.Ordinal);
        Assert.Equal(10000, DirectoryEndpoints.SymbolChangeRequestLimit);
    }

    [Fact]
    public async Task A_symbol_change_request_does_not_offer_a_page_that_does_nothing()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetSymbolChangesAsync();

        // `page` is accepted and silently ignored — page=0 and page=1 returned identical rows on 2026-08-27.
        // Sending it would imply it works.
        Assert.DoesNotContain("page=", handler.Requests.Single().Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_symbol_change_reads_both_tickers_and_the_date()
    {
        var (endpoints, _) = Build(Fixture("symbol-change.head.json"));

        var changes = await endpoints.GetSymbolChangesAsync();

        Assert.Equal(3, changes.Count);
        Assert.Equal(new LocalDate(2026, 8, 26), changes[0].Date);
        Assert.Equal("SIC", changes[0].OldSymbol);
        Assert.Equal("PSOX", changes[0].NewSymbol);
    }
```

Add `using NodaTime;` to the file's usings if not already present.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~symbol_change"`
Expected: FAIL to compile — `'DirectoryEndpoints' does not contain a definition for 'SymbolChangeRequestLimit'`.

- [ ] **Step 4: Write `SymbolChange`**

Append to `src/FmpDotNet/Models/DirectoryListings.cs`:

```csharp
/// <summary>One ticker rename from <c>stable/symbol-change</c> — 5,456 measured 2026-08-27, back to the start of
/// FMP's record.
///
/// <para>This is the endpoint that explains a symbol vanishing from
/// <see cref="Endpoints.DirectoryEndpoints.GetActivelyTradingAsync"/> without being delisted. A caller
/// reconciling historical positions against current tickers needs the whole set, which is why
/// <see cref="Endpoints.DirectoryEndpoints.GetSymbolChangesAsync"/> takes no paging arguments and asks for all of
/// it — see that method for what the default would otherwise cost.</para></summary>
public sealed record SymbolChange
{
    /// <summary>The date the change took effect. ISO <c>uuuu-MM-dd</c> on all 5,456 measured rows, none null.
    ///
    /// <para>A <see cref="LocalDate"/> rather than an <see cref="Instant"/>: a rename belongs to a trading day,
    /// and the payload carries no time of day.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The company's name at the time of the change. Populated on all 5,456 rows.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The ticker before the change — the one to look up in historical data.</summary>
    [JsonPropertyName("oldSymbol")] public string? OldSymbol { get; init; }

    /// <summary>The ticker after the change — the one FMP's current endpoints answer to.</summary>
    [JsonPropertyName("newSymbol")] public string? NewSymbol { get; init; }
}
```

Add `using FmpDotNet.Serialization;` and `using NodaTime;` to the top of `DirectoryListings.cs`.

- [ ] **Step 5: Register the model**

```csharp
[JsonSerializable(typeof(List<SymbolChange>))]
```

- [ ] **Step 6: Add the constant and the method**

Append to `DirectoryEndpoints`:

```csharp
    /// <summary>The <c>limit</c> the SDK sends to <c>stable/symbol-change</c>, and the reason it sends one at all.
    ///
    /// <para><b>Without it the endpoint answers 100 rows and holds 5,456</b>, measured 2026-08-27 — 1.8% of the
    /// history, returned as a well-formed HTTP 200 array indistinguishable from a complete one. FMP documents no
    /// parameters for this path whatsoever; <c>limit</c> works regardless, and <c>page</c> is accepted and
    /// silently ignored, so this is the only lever there is.</para>
    ///
    /// <para>10,000 rather than 5,456 is headroom against growth, not a guess: the ceiling was probed to
    /// <c>limit=100000</c> and the answer stayed 5,456, so there is no server-side cap between the two and asking
    /// for more costs nothing.</para></summary>
    public const int SymbolChangeRequestLimit = 10_000;

    /// <summary>Every ticker rename FMP has recorded — 5,456 measured 2026-08-27, newest first.
    ///
    /// <para>This is what explains a symbol disappearing from
    /// <see cref="GetActivelyTradingAsync(CancellationToken)"/> without appearing in
    /// <see cref="CompanyEndpoints.GetDelistedAsync"/>: it was renamed, not delisted. A caller reconciling
    /// historical positions against current tickers wants all of it.</para>
    ///
    /// <para><b>Takes no paging arguments, deliberately.</b> The endpoint's undocumented default returns 100 rows
    /// of 5,456 and its <c>page</c> parameter does nothing — see
    /// <see cref="SymbolChangeRequestLimit"/>. Offering a <c>page</c> the SDK knows is ignored would be worse than
    /// offering nothing, and there is no correct partial answer to "what has been renamed": a reconciliation
    /// against 1.8% of the history is silently wrong rather than incomplete.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every recorded rename in FMP's order, newest first. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolChange>> GetSymbolChangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/symbol-change").With("limit", SymbolChangeRequestLimit),
            FmpJsonContext.Default.ListSymbolChange, ct);
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: PASS, 25 tests.

- [ ] **Step 8: Mutation-check the limit**

Remove `.With("limit", SymbolChangeRequestLimit)` from `GetSymbolChangesAsync`.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~A_symbol_change_request_asks_for_more"`
Expected: FAIL — `Assert.Contains() Failure: Sub-string not found`. Confirm that `A_symbol_change_reads_both_tickers_and_the_date` still **passes** with the mutation in place: that is the demonstration that the response cannot reveal this bug and the request assertion is the only guard.
Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/DirectoryListings.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/DirectoryEndpoints.cs tests/FmpDotNet.Tests/DirectoryListsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/symbol-change.head.json
git commit -m "feat: the symbol-change archive, asked for in full rather than at its hidden default (#25)"
```

---

### Task 5: `cik-list`, paged and streamed

512,665 rows over 52 pages, capped at 10,000 each. Unlike its sibling in Task 4, `page` genuinely works here.

**Files:**
- Modify: `src/FmpDotNet/Models/DirectoryListings.cs` (append `CikEntry`)
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/DirectoryEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/DirectoryListsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/cik-list.head.json`

**Interfaces:**
- Consumes: `DirectoryListsTests.Build`, `.Fixture`
- Produces: `CikEntry`; `DirectoryEndpoints.MaxCikListPageSize` (`public const int` = 10000), `DirectoryEndpoints.GetCikListAsync(int page, int limit, CancellationToken) -> Task<IReadOnlyList<CikEntry>>`, `DirectoryEndpoints.StreamCikListAsync(CancellationToken) -> IAsyncEnumerable<CikEntry>`

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/cik-list.head.json` — note the CIKs are 10-character zero-padded on every measured row, and that the registrants include individuals:

```json
[
  { "cik": "0002150676", "companyName": "Advus Financial Partners, LLC" },
  { "cik": "0002150492", "companyName": "Thompson David Blair" },
  { "cik": "0002150231", "companyName": "TOP Private Wealth LLC." }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `DirectoryListsTests.cs`:

```csharp
    [Fact]
    public async Task A_cik_page_asks_for_the_page_and_limit_it_was_given()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetCikListAsync(page: 3, limit: 500);

        var query = handler.Requests.Single().Query;
        Assert.Contains("page=3", query, StringComparison.Ordinal);
        Assert.Contains("limit=500", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cik_entry_keeps_the_zero_padding_fmp_sends()
    {
        var (endpoints, _) = Build(Fixture("cik-list.head.json"));

        var entries = await endpoints.GetCikListAsync(page: 0, limit: 3);

        // A string, not an int. Every measured CIK is 10 characters zero-padded, and search-cik echoes that form
        // back — parsing to a number and reformatting is a round-trip that only ever loses.
        Assert.Equal("0002150676", entries[0].Cik);
        // Not all registrants are companies. This one is a person.
        Assert.Equal("Thompson David Blair", entries[1].CompanyName);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(0, 10001)]
    public async Task A_cik_page_outside_what_fmp_serves_is_rejected_before_it_costs_a_call(int page, int limit)
    {
        var (endpoints, handler) = Build("[]");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetCikListAsync(page, limit));

        // Rejected locally: no request went out.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_cik_stream_walks_every_page_until_one_comes_back_short()
    {
        // Three pages: two full at the cap, then a short one that ends the walk. A fourth response is queued to
        // prove it is never requested — StubHandler repeats its last response forever, so a walk that failed to
        // stop would spin rather than fail, and the request count is what catches that.
        var full = string.Join(",", Enumerable.Range(0, DirectoryEndpoints.MaxCikListPageSize)
            .Select(i => $$"""{"cik":"{{i:D10}}","companyName":"Registrant {{i}}"}"""));
        var handler = new StubHandler(
            StubHandler.Json($"[{full}]"),
            StubHandler.Json($"[{full}]"),
            StubHandler.Json("""[{"cik":"0000000001","companyName":"Last Registrant"}]"""),
            StubHandler.Json("[]"));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new DirectoryEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        var count = 0;
        await foreach (var _ in endpoints.StreamCikListAsync()) count++;

        Assert.Equal(DirectoryEndpoints.MaxCikListPageSize * 2 + 1, count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("page=0", handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.Contains("page=2", handler.Requests[2].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_cik_stream_stops_on_an_empty_first_page_without_a_second_request()
    {
        var (endpoints, handler) = Build("[]");

        var count = 0;
        await foreach (var _ in endpoints.StreamCikListAsync()) count++;

        Assert.Equal(0, count);
        Assert.Single(handler.Requests);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~cik"`
Expected: FAIL to compile — `'DirectoryEndpoints' does not contain a definition for 'GetCikListAsync'`.

- [ ] **Step 4: Write `CikEntry`**

Append to `src/FmpDotNet/Models/DirectoryListings.cs`:

```csharp
/// <summary>One SEC registrant from <c>stable/cik-list</c> — about 512,665 measured 2026-08-27.
///
/// <para><b>This is not a symbol directory.</b> Against <c>stable/stock-list</c>'s 91,845 tickers, this endpoint
/// carries every entity with an SEC Central Index Key, most of which have no ticker at all: investment advisers,
/// funds, and <b>individuals</b> — <c>Thompson David Blair</c> is a measured row. A caller expecting a company
/// list will find five and a half times more rows than there are listed securities.</para></summary>
public sealed record CikEntry
{
    /// <summary>The Central Index Key, <b>zero-padded to ten characters</b> — <c>0002150676</c>. All 200 rows
    /// sampled carried exactly ten.
    ///
    /// <para><b>A <see cref="string"/> rather than an integer, deliberately.</b> The padding is part of the
    /// identifier as SEC systems and FMP's own <c>search-cik</c> spell it, and parsing to a number discards it —
    /// after which every consumer has to remember to re-pad, and the one that forgets fails a lookup silently.
    /// <see cref="Endpoints.SearchEndpoints.FindByCikAsync"/> accepts either form and always echoes this
    /// one.</para></summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as filed. Populated on every measured row. Not necessarily a company —
    /// see the type's own remarks.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
}
```

- [ ] **Step 5: Register the model**

```csharp
[JsonSerializable(typeof(List<CikEntry>))]
```

- [ ] **Step 6: Add the constant and both methods**

Append to `DirectoryEndpoints`. The `using System.Runtime.CompilerServices;` import is needed for `[EnumeratorCancellation]`:

```csharp
    /// <summary>The largest page <c>stable/cik-list</c> will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>: on 2026-08-27 <c>limit=10000</c>, <c>limit=50000</c> and
    /// <c>limit=200000</c> all answered exactly 10,000 rows. A caller who asks for 50,000 and advances the page
    /// index by 50,000 skips four fifths of the registry and never sees an error, so
    /// <see cref="GetCikListAsync(int, int, CancellationToken)"/> rejects a larger <c>limit</c> rather than
    /// passing it on to be clamped — the same treatment
    /// <see cref="CompanyEndpoints.MaxDelistedPageSize"/> gives the delisted archive.</para></summary>
    public const int MaxCikListPageSize = 10_000;

    /// <summary>One page of <c>stable/cik-list</c> — the SEC registrant index, about 512,665 entries measured
    /// 2026-08-27 across 52 pages.
    ///
    /// <para><b>Not a symbol directory.</b> Most registrants have no ticker, and some are people — see
    /// <see cref="CikEntry"/>. Ordered by CIK descending, so page 0 is the most recently assigned.</para>
    ///
    /// <para><b><c>page</c> works here, unlike on <see cref="GetSymbolChangesAsync(CancellationToken)"/>.</b> The
    /// two endpoints sit in the same group and disagree: page 0 and page 1 of this one start at
    /// <c>0002150676</c> and <c>0002150170</c> respectively, while <c>symbol-change</c> answers both with
    /// identical rows. Nothing in either payload says which behaviour you are getting.</para>
    ///
    /// <para>The walk ends short rather than empty: page 51 carried 2,665 rows and page 52 answered <c>[]</c>.
    /// Either terminator works; <see cref="StreamCikListAsync(CancellationToken)"/> stops at the first short page
    /// and saves a request.</para></summary>
    /// <param name="page">Zero-based page index. A page past the end answers an empty list, not an error.</param>
    /// <param name="limit">Rows per page, 1 to <see cref="MaxCikListPageSize"/>. Required rather than defaulted,
    /// matching <see cref="CompanyEndpoints.GetDelistedAsync"/>: the page size and the page index have to agree
    /// for a walk to be complete, and a default would let them disagree invisibly.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's rows in FMP's order. Empty past the end. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxCikListPageSize"/> — see that constant for why the
    /// upper bound is enforced here rather than silently clamped upstream.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CikEntry>> GetCikListAsync(int page, int limit, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxCikListPageSize);
        return transport.GetListAsync(
            new FmpRequest("stable/cik-list").With("page", page).With("limit", limit),
            FmpJsonContext.Default.ListCikEntry, ct);
    }

    /// <summary>Walks <c>stable/cik-list</c> from page 0 and streams every registrant as one sequence — about
    /// 512,665 rows over 52 requests, measured 2026-08-27.
    ///
    /// <para><b>The termination rule is sound here, unlike the bulk walks.</b> This endpoint answers a page past
    /// the end with an empty HTTP 200 array rather than an error, so the walk needs no heuristic about what a
    /// status code means: a page that comes back shorter than
    /// <see cref="MaxCikListPageSize"/> is the last one, and an empty page ends it too. Compare
    /// <see cref="BulkEndpoints.StreamAllProfilesAsync"/>, which has to read an HTTP 400 as "past the end"
    /// because that family offers nothing better.</para>
    ///
    /// <para><b>52 requests on the ordinary throttle.</b> Not free, and the whole registry is rarely what a caller
    /// wants — <see cref="GetCikListAsync(int, int, CancellationToken)"/> is there for taking one page.</para></summary>
    /// <param name="ct">Cancels the walk between pages as well as mid-page.</param>
    /// <exception cref="FmpRateLimitedException">FMP answered 429. Possible if 52 pages are walked flat out.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async IAsyncEnumerable<CikEntry> StreamCikListAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var page = 0; ; page++)
        {
            var rows = await GetCikListAsync(page, MaxCikListPageSize, ct).ConfigureAwait(false);
            foreach (var row in rows) yield return row;

            // A short page is the last page. An empty one ends it too, and is the same condition — nothing
            // measured returned a short page followed by a full one.
            if (rows.Count < MaxCikListPageSize) yield break;
        }
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DirectoryListsTests"`
Expected: PASS, 33 tests.

- [ ] **Step 8: Mutation-check the walk terminator**

Change `if (rows.Count < MaxCikListPageSize) yield break;` to `if (rows.Count == 0) yield break;`.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~The_cik_stream_walks_every_page"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 3, Actual: 4`. The walk makes one unnecessary request against the fourth queued response. This is the cost the terminator saves, made visible.
Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/DirectoryListings.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/DirectoryEndpoints.cs tests/FmpDotNet.Tests/DirectoryListsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/cik-list.head.json
git commit -m "feat: the SEC registrant index, one page at a time or all 52 (#25)"
```

---

### Task 6: The five search shapes

**Files:**
- Create: `src/FmpDotNet/Models/SearchResults.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/SearchEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/SearchEndpointsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/search-symbol.AAPL.json`, `search-cik.AAPL.json`, `search-cusip.AAPL.json`, `search-isin.AAPL.json`

**Interfaces:**
- Consumes: nothing from earlier tasks
- Produces: `SymbolSearchResult`, `CikSearchResult`, `CusipSearchResult`, `IsinSearchResult`; `SearchEndpoints.FindBySymbolAsync(string query, int? limit, string? exchange, CancellationToken)`, `.FindByNameAsync(string query, int? limit, string? exchange, CancellationToken)`, `.FindByCikAsync(string cik, CancellationToken)`, `.FindByCusipAsync(string cusip, CancellationToken)`, `.FindByIsinAsync(string isin, CancellationToken)` — all returning `Task<IReadOnlyList<T>>`

- [ ] **Step 1: Write the fixtures**

`search-symbol.AAPL.json` — note `exchange` here is the **code** and `exchangeFullName` the display name, the opposite of `search-exchange-variants` in Task 7:

```json
[
  { "symbol": "AAPL", "name": "Apple Inc.", "currency": "USD",
    "exchangeFullName": "NASDAQ Global Select", "exchange": "NASDAQ" },
  { "symbol": "APC.F", "name": "Apple Inc.", "currency": "EUR",
    "exchangeFullName": "Frankfurt Stock Exchange", "exchange": "FSX" }
]
```

`search-cik.AAPL.json` — the CIK comes back padded even when the query was not:

```json
[
  { "symbol": "AAPL", "companyName": "Apple Inc.", "cik": "0000320193",
    "exchangeFullName": "NASDAQ Global Select", "exchange": "NASDAQ", "currency": "USD" }
]
```

`search-cusip.AAPL.json` — the company field is `companyName`, and the Mexican listing leads with a market cap in MXN:

```json
[
  { "symbol": "AAPL.MX", "companyName": "Apple Inc.", "cusip": "037833100",
    "marketCap": 78694853448000 },
  { "symbol": "AAPL", "companyName": "Apple Inc.", "cusip": "037833100",
    "marketCap": 4537071141960 }
]
```

`search-isin.AAPL.json` — the same fact under `name` rather than `companyName`:

```json
[
  { "symbol": "AAPL", "name": "Apple Inc.", "isin": "US0378331005", "marketCap": 4603751738200 },
  { "symbol": "AAPL.MX", "name": "Apple Inc.", "isin": "US0378331005", "marketCap": 78283607480000 }
]
```

- [ ] **Step 2: Write the failing tests**

Create `tests/FmpDotNet.Tests/SearchEndpointsTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

/// <summary>The five <c>stable/search-*</c> lookups, checked against responses captured live from FMP on
/// 2026-08-27.
///
/// <para>Separate from <see cref="CompanyScreenerTests"/>, which covers the sixth member of FMP's Search group.</para></summary>
public class SearchEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (SearchEndpoints Endpoints, StubHandler Handler) Build(params string[] bodies)
    {
        // One response per call: FmpTransport disposes the response after reading it, so a single
        // HttpResponseMessage cannot serve two requests.
        var handler = new StubHandler([.. bodies.Select(b => StubHandler.Json(b))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SearchEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_symbol_search_reads_the_code_from_exchange_and_the_name_from_exchange_full_name()
    {
        var (endpoints, _) = Build(Fixture("search-symbol.AAPL.json"));

        var matches = await endpoints.FindBySymbolAsync("AAPL");

        // On THIS endpoint `exchange` is the code. On search-exchange-variants it is the display name and the
        // code lives in exchangeShortName. Pinned on both sides so the inversion cannot be "tidied up".
        Assert.Equal("NASDAQ", matches[0].Exchange);
        Assert.Equal("NASDAQ Global Select", matches[0].ExchangeFullName);
    }

    [Fact]
    public async Task A_symbol_search_returns_every_listing_rather_than_the_first()
    {
        var (endpoints, _) = Build(Fixture("search-symbol.AAPL.json"));

        var matches = await endpoints.FindBySymbolAsync("AAPL");

        // A list, not a T?. "AAPL" matched 7 listings across exchanges on 2026-08-27; returning one would pick a
        // listing — and therefore a currency — without saying so.
        Assert.Equal(2, matches.Count);
        Assert.Equal("EUR", matches[1].Currency);
    }

    [Fact]
    public async Task A_cik_search_echoes_the_padded_form_whichever_was_asked_for()
    {
        var (endpoints, _) = Build(Fixture("search-cik.AAPL.json"), Fixture("search-cik.AAPL.json"));

        var padded = await endpoints.FindByCikAsync("0000320193");
        var bare = await endpoints.FindByCikAsync("320193");

        // Both forms are accepted upstream and both answer with the 10-character form, so a caller can round-trip
        // through CikEntry.Cik without normalising.
        Assert.Equal("0000320193", padded[0].Cik);
        Assert.Equal("0000320193", bare[0].Cik);
    }

    [Fact]
    public async Task A_cusip_match_and_an_isin_match_agree_on_the_company_name()
    {
        var (endpoints, _) = Build(Fixture("search-cusip.AAPL.json"), Fixture("search-isin.AAPL.json"));

        var byCusip = await endpoints.FindByCusipAsync("037833100");
        var byIsin = await endpoints.FindByIsinAsync("US0378331005");

        // The wire disagrees: search-cusip sends `companyName`, search-isin sends `name`, for the identical fact.
        // Both models surface it as CompanyName so a caller never learns which endpoint spells it which way.
        Assert.Equal("Apple Inc.", byCusip[0].CompanyName);
        Assert.Equal("Apple Inc.", byIsin[0].CompanyName);
    }

    [Fact]
    public async Task An_identifier_match_carries_a_market_cap_in_an_unstated_currency()
    {
        var (endpoints, _) = Build(Fixture("search-cusip.AAPL.json"));

        var matches = await endpoints.FindByCusipAsync("037833100");

        // Both rows are Apple. The first is the Mexican listing, quoted in MXN, and NOTHING on the row says so —
        // there is no currency field and no exchange field on this shape. Sorting by MarketCap ranks currencies.
        Assert.Equal("AAPL.MX", matches[0].Symbol);
        Assert.Equal(78694853448000m, matches[0].MarketCap);
        Assert.True(matches[0].MarketCap > matches[1].MarketCap);
    }

    [Fact]
    public async Task A_symbol_search_sends_the_optional_filters_only_when_given()
    {
        var (endpoints, handler) = Build("[]", "[]");

        await endpoints.FindBySymbolAsync("AAPL");
        await endpoints.FindBySymbolAsync("AAPL", limit: 3, exchange: "NASDAQ");

        Assert.DoesNotContain("limit=", handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.DoesNotContain("exchange=", handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.Contains("limit=3", handler.Requests[1].Query, StringComparison.Ordinal);
        Assert.Contains("exchange=NASDAQ", handler.Requests[1].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_identifier_searches_do_not_offer_a_limit_that_does_nothing()
    {
        // search-cusip and search-isin ignore `limit` — measured 4 -> 4 and 5 -> 5 on 2026-08-27. The guarantee is
        // that no overload offers one, which is a compile-time fact: these calls take exactly (string, ct).
        var cusip = typeof(SearchEndpoints).GetMethod(nameof(SearchEndpoints.FindByCusipAsync))!;
        var isin = typeof(SearchEndpoints).GetMethod(nameof(SearchEndpoints.FindByIsinAsync))!;

        Assert.Equal(["cusip", "ct"], cusip.GetParameters().Select(p => p.Name));
        Assert.Equal(["isin", "ct"], isin.GetParameters().Select(p => p.Name));
        await Task.CompletedTask;
    }

    public static TheoryData<string, Func<SearchEndpoints, Task<int>>> Lookups => new()
    {
        { "/stable/search-symbol", async e => (await e.FindBySymbolAsync("ZZZZQQQQ9")).Count },
        { "/stable/search-name", async e => (await e.FindByNameAsync("ZZZZQQQQ9")).Count },
        { "/stable/search-cik", async e => (await e.FindByCikAsync("9999999999")).Count },
        { "/stable/search-cusip", async e => (await e.FindByCusipAsync("000000000")).Count },
        { "/stable/search-isin", async e => (await e.FindByIsinAsync("XX0000000000")).Count },
    };

    [Theory]
    [MemberData(nameof(Lookups))]
    public async Task An_unknown_identifier_reads_as_an_empty_list(
        string path, Func<SearchEndpoints, Task<int>> call)
    {
        var (endpoints, handler) = Build("[]");

        // All five answer garbage with HTTP 200 and [], never an error — measured 2026-08-27. An empty list is
        // therefore "no match", and is indistinguishable from a query FMP did not understand.
        Assert.Equal(0, await call(endpoints));
        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_query_is_rejected_before_it_costs_a_call(string query)
    {
        var (endpoints, handler) = Build("[]");

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindBySymbolAsync(query));

        Assert.Empty(handler.Requests);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~SearchEndpointsTests"`
Expected: FAIL to compile — `'SearchEndpoints' does not contain a definition for 'FindBySymbolAsync'`.

- [ ] **Step 4: Write the four models**

Create `src/FmpDotNet/Models/SearchResults.cs`:

```csharp
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/search-symbol</c> or <c>stable/search-name</c>.
///
/// <para>The two endpoints return an identical five-field shape and share this type — one searches the ticker,
/// the other the company name, and both answer with the same row. Measured 2026-08-27: <c>query=AAPL</c> matched
/// 7 listings and <c>query=Apple</c> matched 37.</para>
///
/// <para><b>A match is a listing, not a company.</b> Apple appears once per exchange it trades on, each with its
/// own symbol and currency. Taking the first row picks a listing arbitrarily.</para></summary>
public sealed record SymbolSearchResult
{
    /// <summary>The ticker as FMP spells it, exchange suffix included — <c>AAPL</c>, <c>APC.F</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>name</c> on this endpoint pair.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The currency this listing trades in — <c>USD</c> for <c>AAPL</c>, <c>EUR</c> for <c>APC.F</c>.
    /// Present here, and notably absent from <see cref="CusipSearchResult"/> and
    /// <see cref="IsinSearchResult"/>.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>The exchange's display name — <c>NASDAQ Global Select</c>.</summary>
    [JsonPropertyName("exchangeFullName")] public string? ExchangeFullName { get; init; }

    /// <summary>The exchange's short code — <c>NASDAQ</c>, <c>FSX</c>. The value
    /// <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/> expects, and the vocabulary
    /// <see cref="Endpoints.DirectoryEndpoints.GetExchangesAsync"/> publishes.
    ///
    /// <para><b>The code, not the display name.</b> <see cref="ExchangeVariant.Exchange"/> is the other way
    /// round — same field name, opposite meaning, on an endpoint in the same group.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }
}

/// <summary>One match from <c>stable/search-cik</c> — the SEC Central Index Key resolved to the listings it
/// covers.
///
/// <para>Measured 2026-08-27: <c>0000320193</c> answered a single row. <b>The query accepts either form</b> —
/// padded or bare — and the response always carries the ten-character padded one.</para></summary>
public sealed record CikSearchResult
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c> on this endpoint — <b>not</b> <c>name</c>, which is
    /// what <see cref="SymbolSearchResult.Name"/> binds on its siblings.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The Central Index Key, zero-padded to ten characters regardless of how it was asked for. Matches
    /// <see cref="CikEntry.Cik"/> exactly, so the two round-trip.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The exchange's display name — <c>NASDAQ Global Select</c>.</summary>
    [JsonPropertyName("exchangeFullName")] public string? ExchangeFullName { get; init; }

    /// <summary>The exchange's short code — <c>NASDAQ</c>. The code, as on
    /// <see cref="SymbolSearchResult.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The currency this listing trades in.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }
}

/// <summary>One match from <c>stable/search-cusip</c> — a CUSIP resolved to the listings that carry it.
///
/// <para>Measured 2026-08-27: <c>037833100</c> answered 4 rows, because one CUSIP spans a security's listings.
/// <b>This endpoint ignores <c>limit</c></b> (4 rows asked down to 1 still answered 4), which is why
/// <see cref="Endpoints.SearchEndpoints.FindByCusipAsync"/> offers no such parameter.</para>
///
/// <para>Separate from <see cref="IsinSearchResult"/> rather than shared with it: the shapes are otherwise
/// identical, but a CUSIP and an ISIN are different facts and one shared type would carry a permanently-null
/// field on every row.</para></summary>
public sealed record CusipSearchResult
{
    /// <summary>The ticker as FMP spells it — <c>AAPL.MX</c>, <c>AAPL</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name.
    ///
    /// <para><b>The wire key is <c>companyName</c> here and <c>name</c> on <see cref="IsinSearchResult"/></b>, for
    /// the identical fact on two sibling endpoints. Both models call it <c>CompanyName</c> so a caller never has
    /// to learn which endpoint spells it which way — the same treatment
    /// <see cref="CompanySymbol.Name"/> gives the two symbol directories.</para></summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The CUSIP, echoed back — nine characters.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The listing's market capitalisation.
    ///
    /// <para><b>Denominated in the listing's local currency, and nothing on this row says which.</b> Measured
    /// 2026-08-27, <c>037833100</c> answered <c>AAPL.MX</c> at 78,694,853,448,000 — MXN, confirmed against that
    /// symbol's profile — alongside <c>AAPL</c> at 4,537,071,141,960 in USD. This shape carries no
    /// <c>currency</c> field and no <c>exchange</c> field, unlike <see cref="SymbolSearchResult"/>, so
    /// <b>ordering these rows by market capitalisation ranks currencies rather than companies</b> and the
    /// Mexican listing sorts seventeen times above the American one.</para>
    ///
    /// <para>To compare across listings, resolve each symbol through
    /// <see cref="Endpoints.CompanyEndpoints.GetProfileAsync"/> and read
    /// <see cref="CompanyProfile.Currency"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}

/// <summary>One match from <c>stable/search-isin</c> — an ISIN resolved to the listings that carry it.
///
/// <para>Measured 2026-08-27: <c>US0378331005</c> answered 5 rows, one of them with a market capitalisation of
/// zero. <b>This endpoint ignores <c>limit</c></b>, as <see cref="CusipSearchResult"/> notes of its
/// sibling.</para></summary>
public sealed record IsinSearchResult
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name.
    ///
    /// <para><b>The wire key is <c>name</c> here and <c>companyName</c> on
    /// <see cref="CusipSearchResult"/></b> — see that property. The C# name is deliberately the same on both.</para></summary>
    [JsonPropertyName("name")] public string? CompanyName { get; init; }

    /// <summary>The ISIN, echoed back — twelve characters.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The listing's market capitalisation, in the listing's local currency, unlabelled — see
    /// <see cref="CusipSearchResult.MarketCap"/> for the full account and the measured example. One of the five
    /// measured rows (<c>AAPL.DE</c>) reported zero rather than null.</summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}
```

- [ ] **Step 5: Register the four models**

```csharp
[JsonSerializable(typeof(List<SymbolSearchResult>))]
[JsonSerializable(typeof(List<CikSearchResult>))]
[JsonSerializable(typeof(List<CusipSearchResult>))]
[JsonSerializable(typeof(List<IsinSearchResult>))]
```

- [ ] **Step 6: Add the five methods**

Append to `SearchEndpoints`, after `ScreenAsync`:

```csharp
    /// <summary>Finds listings whose <b>ticker</b> matches <paramref name="query"/> — 7 rows for <c>AAPL</c>,
    /// measured 2026-08-27.
    ///
    /// <para><b>A prefix match across every exchange, not an exact lookup.</b> <c>query=AA</c> answered 50 rows.
    /// Fifty is also the undocumented default cap — pass <paramref name="limit"/> to change it.</para>
    ///
    /// <para><b>Returns listings, not companies.</b> Apple appears once per exchange, each with its own symbol
    /// and currency, so taking the first row picks one arbitrarily. Narrow with
    /// <paramref name="exchange"/> instead.</para></summary>
    /// <param name="query">The ticker or ticker prefix. Required and non-blank.</param>
    /// <param name="limit">Rows to return. Omitted by default, which asks FMP for its own default of 50.</param>
    /// <param name="exchange">Restricts to one exchange by short code — <c>NASDAQ</c>. Undocumented by FMP and
    /// measured working: <c>AAPL</c> narrowed from 7 rows to 1. Validate against
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/>; an unknown code answers an empty list, not an
    /// error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matches in FMP's order. Empty when nothing matched — and also empty when the query was not
    /// understood, which this endpoint does not distinguish. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolSearchResult>> FindBySymbolAsync(
        string query, int? limit = null, string? exchange = null, CancellationToken ct = default) =>
        QueryAsync("stable/search-symbol", query, limit, exchange, ct);

    /// <summary>Finds listings whose <b>company name</b> matches <paramref name="query"/> — 37 rows for
    /// <c>Apple</c>, measured 2026-08-27.
    ///
    /// <para>The same row shape and the same behaviour as
    /// <see cref="FindBySymbolAsync(string, int?, string?, CancellationToken)"/>, searching the other
    /// field.</para></summary>
    /// <param name="query">The company name or a fragment of it. Required and non-blank.</param>
    /// <param name="limit">Rows to return. Omitted by default.</param>
    /// <param name="exchange">Restricts to one exchange by short code. See the sibling method.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matches in FMP's order. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<SymbolSearchResult>> FindByNameAsync(
        string query, int? limit = null, string? exchange = null, CancellationToken ct = default) =>
        QueryAsync("stable/search-name", query, limit, exchange, ct);

    /// <summary>Resolves an SEC Central Index Key to the listings it covers.
    ///
    /// <para><b>Accepts the padded and the bare form alike</b> — <c>0000320193</c> and <c>320193</c> both answered
    /// the same single row on 2026-08-27 — and always answers with the ten-character padded form, matching
    /// <see cref="CikEntry.Cik"/>.</para>
    ///
    /// <para>This is the useful direction for CIK: <c>search-exchange-variants</c> returns one only for a
    /// symbol's primary listing, and <see cref="DirectoryEndpoints.StreamCikListAsync"/> is a 52-request walk of
    /// the whole registry.</para></summary>
    /// <param name="cik">The Central Index Key, padded or bare. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown CIK. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cik"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CikSearchResult>> FindByCikAsync(string cik, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        return transport.GetListAsync(
            new FmpRequest("stable/search-cik").With("cik", cik),
            FmpJsonContext.Default.ListCikSearchResult, ct);
    }

    /// <summary>Resolves a CUSIP to the listings that carry it — 4 rows for <c>037833100</c>, measured
    /// 2026-08-27.
    ///
    /// <para><b>The rows carry a market capitalisation in an unstated currency</b>, and the first is not the US
    /// listing. See <see cref="CusipSearchResult.MarketCap"/> before ordering or comparing them.</para>
    ///
    /// <para>Takes no <c>limit</c>: the endpoint ignores it — 4 rows asked down to 1 still answered 4.</para></summary>
    /// <param name="cusip">The nine-character CUSIP. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown CUSIP. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="cusip"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<CusipSearchResult>> FindByCusipAsync(string cusip, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cusip);
        return transport.GetListAsync(
            new FmpRequest("stable/search-cusip").With("cusip", cusip),
            FmpJsonContext.Default.ListCusipSearchResult, ct);
    }

    /// <summary>Resolves an ISIN to the listings that carry it — 5 rows for <c>US0378331005</c>, measured
    /// 2026-08-27.
    ///
    /// <para>Same caveats as <see cref="FindByCusipAsync(string, CancellationToken)"/>: an unstated market-cap
    /// currency, and no <c>limit</c> because the endpoint ignores it. One of the five measured rows reported a
    /// market capitalisation of zero.</para></summary>
    /// <param name="isin">The twelve-character ISIN. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The matching listings. Empty for an unknown ISIN. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="isin"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<IsinSearchResult>> FindByIsinAsync(string isin, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isin);
        return transport.GetListAsync(
            new FmpRequest("stable/search-isin").With("isin", isin),
            FmpJsonContext.Default.ListIsinSearchResult, ct);
    }

    /// <summary>The shared body of the two query-shaped searches, which differ only in path.</summary>
    private Task<IReadOnlyList<SymbolSearchResult>> QueryAsync(
        string path, string query, int? limit, string? exchange, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is not null) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit.Value);
        return transport.GetListAsync(
            new FmpRequest(path).With("query", query).With("limit", limit).With("exchange", exchange),
            FmpJsonContext.Default.ListSymbolSearchResult, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~SearchEndpointsTests"`
Expected: PASS, 16 tests.

- [ ] **Step 8: Mutation-check the name unification**

Change `IsinSearchResult.CompanyName`'s attribute from `[JsonPropertyName("name")]` to `[JsonPropertyName("companyName")]`.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~A_cusip_match_and_an_isin_match_agree"`
Expected: FAIL — the ISIN row's `CompanyName` is null, because `search-isin` sends `name`.
Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/SearchResults.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/SearchEndpoints.cs tests/FmpDotNet.Tests/SearchEndpointsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/search-symbol.AAPL.json tests/FmpDotNet.Tests/Fixtures/search-cik.AAPL.json \
        tests/FmpDotNet.Tests/Fixtures/search-cusip.AAPL.json tests/FmpDotNet.Tests/Fixtures/search-isin.AAPL.json
git commit -m "feat: symbol, name, CIK, CUSIP and ISIN lookup (#25)"
```

---

### Task 7: `search-exchange-variants`, and the inverted `exchange`

One path. It returns a v3-era company profile under a Search path, and the field that would silently produce wrong code is `exchange`.

**Files:**
- Create: `src/FmpDotNet/Models/ExchangeVariant.cs`
- Modify: `src/FmpDotNet/Serialization/FmpJsonContext.cs`
- Modify: `src/FmpDotNet/Endpoints/SearchEndpoints.cs`
- Modify: `tests/FmpDotNet.Tests/SearchEndpointsTests.cs`
- Create: `tests/FmpDotNet.Tests/Fixtures/search-exchange-variants.AAPL.json`

**Interfaces:**
- Consumes: `SearchEndpointsTests.Build`, `.Fixture` (Task 6)
- Produces: `ExchangeVariant`; `SearchEndpoints.GetExchangeVariantsAsync(string symbol, CancellationToken) -> Task<IReadOnlyList<ExchangeVariant>>`

- [ ] **Step 1: Write the fixture**

`tests/FmpDotNet.Tests/Fixtures/search-exchange-variants.AAPL.json` — three of the six measured rows, carrying the inversion, the null `cik` on non-primary listings, and the row where `price` itself is null:

```json
[
  { "symbol": "AAPL", "price": 313.45, "beta": 1.109, "volAvg": 55604384, "mktCap": 4603751738200,
    "lastDiv": 1.05, "range": "169.21-320.85", "changes": 3.55, "companyName": "Apple Inc.",
    "currency": "USD", "cik": "0000320193", "isin": "US0378331005", "cusip": "037833100",
    "exchange": "NASDAQ Global Select", "exchangeShortName": "NASDAQ", "industry": "Consumer Electronics",
    "website": "https://www.apple.com", "description": "Apple Inc. designs consumer electronics.",
    "ceo": "Mr. Timothy D. Cook", "sector": "Technology", "country": "US", "fullTimeEmployees": "164000",
    "phone": "408 996 1010", "address": "One Apple Park Way", "city": "Cupertino", "state": "CA",
    "zip": "95014", "dcfDiff": 170.10931, "dcf": 142.8506908215328,
    "image": "https://images.financialmodelingprep.com/symbol/AAPL.png", "ipoDate": "1980-12-12",
    "defaultImage": false, "isEtf": false, "isActivelyTrading": true, "isAdr": false, "isFund": false },
  { "symbol": "AAPL.MX", "price": 5330, "beta": 1.109, "volAvg": 3142, "mktCap": 78283607480000,
    "lastDiv": 1.05, "range": "2874.06-5449.99", "changes": 60, "companyName": "Apple Inc.",
    "currency": "MXN", "cik": null, "isin": "US0378331005", "cusip": "037833100",
    "exchange": "Mexican Stock Exchange", "exchangeShortName": "MEX", "industry": "Consumer Electronics",
    "website": "https://www.apple.com", "description": "Apple Inc. designs consumer electronics.",
    "ceo": "Mr. Timothy D. Cook", "sector": "Technology", "country": "US", "fullTimeEmployees": "164000",
    "phone": "408 996 1010", "address": "One Apple Park Way", "city": "Cupertino", "state": "CA",
    "zip": "95014", "dcfDiff": 3441.67061, "dcf": 1858.3393897900064,
    "image": "https://images.financialmodelingprep.com/symbol/AAPL.MX.png", "ipoDate": "1980-12-12",
    "defaultImage": false, "isEtf": false, "isActivelyTrading": true, "isAdr": false, "isFund": false },
  { "symbol": "AAPL.DE", "price": null, "beta": 1.109, "volAvg": 0, "mktCap": 0,
    "lastDiv": 1.05, "range": null, "changes": null, "companyName": "Apple Inc.",
    "currency": "EUR", "cik": null, "isin": "US0378331005", "cusip": "037833100",
    "exchange": "Deutsche Börse", "exchangeShortName": "XETRA", "industry": "Consumer Electronics",
    "website": "https://www.apple.com", "description": "Apple Inc. designs consumer electronics.",
    "ceo": "Mr. Timothy D. Cook", "sector": "Technology", "country": "US", "fullTimeEmployees": "164000",
    "phone": "408 996 1010", "address": "One Apple Park Way", "city": "Cupertino", "state": "CA",
    "zip": "95014", "dcfDiff": null, "dcf": 0,
    "image": "https://images.financialmodelingprep.com/symbol/AAPL.DE.png", "ipoDate": "1980-12-12",
    "defaultImage": false, "isEtf": false, "isActivelyTrading": true, "isAdr": false, "isFund": false }
]
```

- [ ] **Step 2: Write the failing tests**

Append to `SearchEndpointsTests.cs`:

```csharp
    [Fact]
    public async Task An_exchange_variant_reads_the_code_from_exchange_short_name()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // THE INVERSION. On stable/profile, `exchange` is the code and `exchangeFullName` the display name. Here
        // `exchange` is the DISPLAY NAME and the code lives in exchangeShortName. A caller filtering on
        // Exchange == "NASDAQ" against this endpoint gets nothing, with no error.
        Assert.Equal("NASDAQ", variants[0].ExchangeShortName);
        Assert.Equal("NASDAQ Global Select", variants[0].Exchange);
    }

    [Fact]
    public async Task An_exchange_variant_is_not_a_company_profile()
    {
        // The two shapes have 36 fields each and 29 in common, so a reader comparing counts would conclude they
        // are interchangeable. These four wire keys are the ones that differ, and binding CompanyProfile to this
        // payload leaves all four null while every other field populates — the worst kind of near-miss.
        var variant = typeof(ExchangeVariant).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false))
            .Cast<JsonPropertyNameAttribute>().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        var profile = typeof(CompanyProfile).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false))
            .Cast<JsonPropertyNameAttribute>().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("mktCap", variant);
        Assert.Contains("marketCap", profile);
        Assert.DoesNotContain("marketCap", variant);
        Assert.DoesNotContain("mktCap", profile);
        // And the field only this endpoint carries.
        Assert.Contains("dcf", variant);
        Assert.DoesNotContain("dcf", profile);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task An_exchange_variant_carries_a_cik_only_for_the_primary_listing()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // 5 of 6 measured rows had a null cik. This endpoint looks like a symbol -> CIK bridge and is not one;
        // FindByCikAsync goes the other way and DirectoryEndpoints.StreamCikListAsync walks the registry.
        Assert.Equal("0000320193", variants[0].Cik);
        Assert.Null(variants[1].Cik);
        Assert.Null(variants[2].Cik);
    }

    [Fact]
    public async Task An_exchange_variant_dcf_does_not_reconcile_with_its_own_price()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // dcf + dcfDiff implies a price the row does not carry: 142.85 + 170.11 = 312.96 against price 313.45.
        // Measured on every row, and the direction is not consistent — the Mexican row implies 5300.01 against
        // 5330. Pinned so a caller cannot infer price from the pair.
        var implied = variants[0].Dcf!.Value + variants[0].DcfDiff!.Value;
        Assert.NotEqual(variants[0].Price!.Value, implied);
        Assert.Equal(312.96m, Math.Round(implied, 2));
    }

    [Fact]
    public async Task An_exchange_variant_row_can_be_missing_its_price_entirely()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // AAPL.DE carried nulls for price, range, changes and dcfDiff while still reporting isActivelyTrading.
        Assert.Null(variants[2].Price);
        Assert.Null(variants[2].Changes);
        Assert.True(variants[2].IsActivelyTrading);
    }
```

Add `using System.Text.Json.Serialization;` and `using FmpDotNet.Models;` to the file's usings.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~exchange_variant"`
Expected: FAIL to compile — `The type or namespace name 'ExchangeVariant' could not be found`.

- [ ] **Step 4: Write `ExchangeVariant`**

Create `src/FmpDotNet/Models/ExchangeVariant.cs`:

```csharp
using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One listing of a security from <c>stable/search-exchange-variants</c> — every exchange the symbol
/// trades on, with a full company profile attached to each. AAPL answered 6 rows measured 2026-08-27.
///
/// <para><b>This is a v3-era profile shape served under a <c>stable</c> path, and it is not
/// <see cref="CompanyProfile"/>.</b> Both carry 36 fields and 29 of them agree, which is exactly what makes the
/// difference dangerous. Three are pure renames, confirmed by value equality on AAPL —
/// <c>change</c>/<c>changes</c>, <c>lastDividend</c>/<c>lastDiv</c>, <c>marketCap</c>/<c>mktCap</c>. Two more have
/// no counterpart at all: this shape carries <see cref="Dcf"/> and <see cref="DcfDiff"/>, which
/// <see cref="CompanyProfile"/> does not, and omits <c>volume</c> and <c>changePercentage</c>, which it does.
/// <c>averageVolume</c> and <see cref="VolAvg"/> are <b>not</b> a rename: 53,379,406 against 55,604,384 on the
/// same symbol, so they are computed differently or refreshed on different schedules.</para>
///
/// <para><b>The trap is <see cref="Exchange"/>.</b> On <see cref="CompanyProfile"/>, <c>exchange</c> holds the
/// short code and <c>exchangeFullName</c> the display name. Here they are inverted: <c>exchange</c> is
/// <c>"NASDAQ Global Select"</c> and the code <c>"NASDAQ"</c> lives in <see cref="ExchangeShortName"/>. Same field
/// name, opposite meaning, on two endpoints a caller will reasonably use together — and the failure is a filter
/// that silently matches nothing.</para>
///
/// <para><b><see cref="Cik"/> is populated only on the primary listing</b> — null on 5 of the 6 measured rows —
/// so this is not the symbol-to-CIK bridge it appears to be.</para></summary>
public sealed record ExchangeVariant
{
    /// <summary>The ticker on this exchange — <c>AAPL</c>, <c>AAPL.MX</c>, <c>APC.F</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The last price, <b>in <see cref="Currency"/> rather than a common one</b>. Null on one of the six
    /// measured rows, which still reported <see cref="IsActivelyTrading"/> true.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Beta against the market. Identical across all six listings of AAPL, so it describes the company
    /// rather than the listing.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Average volume, under FMP's v3 spelling <c>volAvg</c>.
    ///
    /// <para><b>Not the same number as <see cref="CompanyProfile.AverageVolume"/></b> — 55,604,384 here against
    /// 53,379,406 there for AAPL on the same day. Whatever the difference is, FMP does not document it, so the two
    /// are not interchangeable.</para></summary>
    [JsonPropertyName("volAvg")] public decimal? VolAvg { get; init; }

    /// <summary>Market capitalisation, under the v3 spelling <c>mktCap</c>, and <b>in <see cref="Currency"/></b>:
    /// the Mexican listing reads 78,283,607,480,000 MXN against the US listing's 4,603,751,738,200 USD for the
    /// same company. Confirmed equal to <see cref="CompanyProfile.MarketCap"/> for the primary listing.</summary>
    [JsonPropertyName("mktCap")] public decimal? MktCap { get; init; }

    /// <summary>The last dividend, under the v3 spelling <c>lastDiv</c>. Confirmed equal to
    /// <see cref="CompanyProfile.LastDividend"/>.</summary>
    [JsonPropertyName("lastDiv")] public decimal? LastDiv { get; init; }

    /// <summary>The 52-week range as free text — <c>"169.21-320.85"</c>. A string, not a pair: FMP sends one
    /// hyphenated field, and splitting it is guesswork for any symbol whose prices are negative or formatted with
    /// a different separator. Null on one measured row.</summary>
    [JsonPropertyName("range")] public string? Range { get; init; }

    /// <summary>The absolute price change, under the v3 spelling <c>changes</c>. Confirmed equal to
    /// <see cref="CompanyProfile.Change"/>. There is <b>no</b> percentage counterpart on this shape.</summary>
    [JsonPropertyName("changes")] public decimal? Changes { get; init; }

    /// <summary>The company name — the same on every listing.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The currency this listing trades in. <b>Read this before comparing <see cref="Price"/> or
    /// <see cref="MktCap"/> across rows</b>: the six measured rows spanned USD, EUR, MXN and CAD.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>The SEC Central Index Key, <b>populated only on the primary listing</b> — null on 5 of 6 measured
    /// rows. Use <see cref="Endpoints.SearchEndpoints.FindByCikAsync"/> for the reverse direction.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The ISIN, identical across every listing — it identifies the security, not the venue.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The CUSIP, identical across every listing, for the same reason as <see cref="Isin"/>.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The exchange's <b>display name</b> — <c>"NASDAQ Global Select"</c>, <c>"Deutsche Börse"</c>.
    ///
    /// <para><b>This is the inverse of <see cref="CompanyProfile.Exchange"/>, which holds the short code under the
    /// identical field name.</b> The code is in <see cref="ExchangeShortName"/> on this type. A caller who filters
    /// on <c>Exchange == "NASDAQ"</c> here matches nothing and is told nothing.</para></summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The exchange's <b>short code</b> — <c>NASDAQ</c>, <c>XETRA</c>, <c>MEX</c>. This is the value that
    /// matches <see cref="ExchangeInfo.Exchange"/> and <see cref="CompanyProfile.Exchange"/>, and the one to pass
    /// to <see cref="Endpoints.QuoteEndpoints.GetExchangeQuotesAsync"/>.</summary>
    [JsonPropertyName("exchangeShortName")] public string? ExchangeShortName { get; init; }

    /// <summary>The industry label, matching <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The company's website.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The company description.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The chief executive's name as FMP records it.</summary>
    [JsonPropertyName("ceo")] public string? Ceo { get; init; }

    /// <summary>The sector label, matching <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync"/>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The company's country as an ISO alpha-2 code — the company's, not the listing's: every AAPL row
    /// reads <c>US</c> including the Frankfurt and Mexico listings.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>Headcount. <b>Arrives as a quoted string</b> — <c>"164000"</c> — and binds only because
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = AllowReadingFromString</c>.</summary>
    [JsonPropertyName("fullTimeEmployees")] public int? FullTimeEmployees { get; init; }

    /// <summary>The company's telephone number as free text.</summary>
    [JsonPropertyName("phone")] public string? Phone { get; init; }

    /// <summary>Street address of the company's headquarters.</summary>
    [JsonPropertyName("address")] public string? Address { get; init; }

    /// <summary>City of the company's headquarters.</summary>
    [JsonPropertyName("city")] public string? City { get; init; }

    /// <summary>State or region of the company's headquarters.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>Postal code of the company's headquarters.</summary>
    [JsonPropertyName("zip")] public string? Zip { get; init; }

    /// <summary>The gap between <see cref="Dcf"/> and a price.
    ///
    /// <para><b>Not a gap against <see cref="Price"/> on this row.</b> Measured 2026-08-27, <c>dcf + dcfDiff</c>
    /// implies 312.96 for AAPL while <see cref="Price"/> reads 313.45; for the Mexican listing it implies 5300.01
    /// against 5330. Every row disagreed, and not in a consistent direction, so the two fields are computed
    /// against different snapshots and the row does not say which. Do not reconstruct a price from this
    /// pair.</para>
    ///
    /// <para>Null on one of the six measured rows.</para></summary>
    [JsonPropertyName("dcfDiff")] public decimal? DcfDiff { get; init; }

    /// <summary>A discounted-cash-flow valuation, in <see cref="Currency"/>.
    ///
    /// <para>The only DCF value the SDK currently surfaces: FMP's Discounted Cash Flow group is four further paths
    /// in the long tail of issue #25, and none of them is modelled. See <see cref="DcfDiff"/> for why the pair
    /// does not reconcile with <see cref="Price"/>.</para></summary>
    [JsonPropertyName("dcf")] public decimal? Dcf { get; init; }

    /// <summary>URL of the company's logo.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>The company's IPO date — the company's, not this listing's: every AAPL row reads
    /// <c>1980-12-12</c>.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>Whether <see cref="Image"/> is FMP's placeholder rather than a real logo.</summary>
    [JsonPropertyName("defaultImage")] public bool? DefaultImage { get; init; }

    /// <summary>Whether this security is an exchange-traded fund.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>Whether this listing is actively trading. <b>True on the row whose <see cref="Price"/> is
    /// null</b>, so it is not a proxy for "has a price".</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }

    /// <summary>Whether this listing is an American Depositary Receipt.</summary>
    [JsonPropertyName("isAdr")] public bool? IsAdr { get; init; }

    /// <summary>Whether this security is a fund.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }
}
```

- [ ] **Step 5: Register the model**

```csharp
[JsonSerializable(typeof(List<ExchangeVariant>))]
```

- [ ] **Step 6: Add the method**

Append to `SearchEndpoints`, before the private `QueryAsync`:

```csharp
    /// <summary>Every exchange <paramref name="symbol"/> trades on, each with a full company profile attached —
    /// 6 rows for <c>AAPL</c> measured 2026-08-27, spanning USD, EUR, MXN and CAD.
    ///
    /// <para>The question this answers is "where else does this trade, and under what ticker" — the reliable way
    /// to find a symbol's foreign listings, and better than appending
    /// <see cref="ExchangeInfo.SymbolSuffix"/> by hand, which is the literal string <c>"N/A"</c> on five
    /// exchanges.</para>
    ///
    /// <para><b>The rows are <see cref="ExchangeVariant"/>, not <see cref="CompanyProfile"/>, and the difference
    /// is not cosmetic.</b> FMP serves a v3-era shape here: <c>mktCap</c> for <c>marketCap</c>, <c>lastDiv</c> for
    /// <c>lastDividend</c>, and — the one that produces silently wrong code —
    /// <see cref="ExchangeVariant.Exchange"/> holding the display name where
    /// <see cref="CompanyProfile.Exchange"/> holds the short code. See <see cref="ExchangeVariant"/> for the
    /// measured comparison.</para>
    ///
    /// <para><b>Prices and market caps are in each listing's own currency.</b> Comparing them across rows without
    /// reading <see cref="ExchangeVariant.Currency"/> compares magnitudes, not values.</para></summary>
    /// <param name="symbol">The ticker to find listings for. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per exchange, in FMP's order, the primary listing first. Empty for an unknown symbol —
    /// HTTP 200, not an error. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeVariant>> GetExchangeVariantsAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/search-exchange-variants").With("symbol", symbol),
            FmpJsonContext.Default.ListExchangeVariant, ct);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~SearchEndpointsTests"`
Expected: PASS, 21 tests.

- [ ] **Step 8: Mutation-check the inversion**

Swap the two attributes on `ExchangeVariant`: give `Exchange` `[JsonPropertyName("exchangeShortName")]` and `ExchangeShortName` `[JsonPropertyName("exchange")]` — the mistake a reader who assumed `CompanyProfile`'s layout would make.
Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~An_exchange_variant_reads_the_code"`
Expected: FAIL — `Assert.Equal() Failure: Expected: "NASDAQ" Actual: "NASDAQ Global Select"`.
Restore.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet/Models/ExchangeVariant.cs src/FmpDotNet/Serialization/FmpJsonContext.cs \
        src/FmpDotNet/Endpoints/SearchEndpoints.cs tests/FmpDotNet.Tests/SearchEndpointsTests.cs \
        tests/FmpDotNet.Tests/Fixtures/search-exchange-variants.AAPL.json
git commit -m "feat: exchange variants, and the v3 profile shape hiding behind a stable path (#25)"
```

---

### Task 8: Teach both argument synthesisers the new parameter names

Two reflection-driven harnesses build arguments by parameter name. Both currently map **any** string to a symbol, which for `cusip`, `isin`, `cik` and `query` means sending `AAPL` — and every one of those endpoints answers an unrecognised value with HTTP 200 and `[]`. The smoke baseline would record `rows 0` and agree with itself forever. This is the failure #24 hit with `exchange`.

**Files:**
- Modify: `tests/FmpDotNet.SmokeTests/LiveApi.cs`
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs`
- Modify: `tests/FmpDotNet.Tests/EndpointCoverageTests.cs`

**Interfaces:**
- Consumes: every endpoint method added in Tasks 1–7
- Produces: `LiveApi.Cik`, `LiveApi.Cusip`, `LiveApi.Isin`, `LiveApi.SearchQuery` (all `public const string`)

- [ ] **Step 1: Run the coverage test to see it fail on its own**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EndpointCoverageTests"`
Expected: FAIL. `GetCikListAsync(int page, int limit, …)` is fine, but the README table is now stale — the generated block still says 65 paths. Record the exact failure message before changing anything; it is the guard working.

- [ ] **Step 2: Add the identifier constants**

Append to `tests/FmpDotNet.SmokeTests/LiveApi.cs`, before the closing brace:

```csharp
    /// <summary>Apple's SEC Central Index Key, for the <c>search-cik</c> probe.
    ///
    /// <para><b>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>.</b> <c>Probe.Argument</c> maps any unrecognised string to <see cref="Symbol"/>,
    /// which would send <c>cik=AAPL</c> — and every <c>search-*</c> endpoint answers an unrecognised identifier
    /// with an empty array and HTTP 200, not an error (measured 2026-08-27). The probe would record `rows 0` as
    /// the baseline and match it every week thereafter, reporting a healthy endpoint that has never been
    /// exercised.</para>
    ///
    /// <para>Given unpadded deliberately: the endpoint accepts either form and always answers with the padded
    /// one, so this also exercises that normalisation.</para></summary>
    public const string Cik = "320193";

    /// <summary>Apple's CUSIP, for the <c>search-cusip</c> probe. Named for the reason on <see cref="Cik"/>.</summary>
    public const string Cusip = "037833100";

    /// <summary>Apple's ISIN, for the <c>search-isin</c> probe. Named for the reason on <see cref="Cik"/>.</summary>
    public const string Isin = "US0378331005";

    /// <summary>The text the two query-shaped searches are probed with.
    ///
    /// <para><see cref="Symbol"/> itself rather than a company name, because <c>search-symbol</c> matches tickers
    /// and <c>search-name</c> matches names — and "AAPL" is measured to return rows from both, 7 and 1
    /// respectively on 2026-08-27. A value that worked on only one of them would leave the other recording an
    /// empty baseline.</para></summary>
    public const string SearchQuery = Symbol;
```

- [ ] **Step 3: Extend `Probe.Argument`**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, replace the string switch:

```csharp
        if (type == typeof(string))
            return parameter.Name switch
            {
                "exchange" => LiveApi.Exchange,
                "cik" => LiveApi.Cik,
                "cusip" => LiveApi.Cusip,
                "isin" => LiveApi.Isin,
                "query" => LiveApi.SearchQuery,
                _ => LiveApi.Symbol,
            };
```

- [ ] **Step 4: Extend `EndpointCoverageTests.Argument`**

In `tests/FmpDotNet.Tests/EndpointCoverageTests.cs`, replace `if (type == typeof(string)) return "AAPL";` with:

```csharp
        // Name-dispatched for the same reason as Probe.Argument, though the stakes are lower here: this harness
        // only records which path went out, so a meaningless value still produces the right table row. The names
        // are matched anyway so the two harnesses do not drift apart.
        if (type == typeof(string))
        {
            return parameter.Name switch
            {
                "cik" => "320193",
                "cusip" => "037833100",
                "isin" => "US0378331005",
                _ => "AAPL",
            };
        }
```

Also add an `int?` case so the optional `limit` on the search methods is supplied — insert before the existing `if (type == typeof(int))` block, and note that `Nullable.GetUnderlyingType` at the top of the method has already unwrapped `int?` to `int`, so the existing block handles it once `"limit" => 5` is present. Confirm `"limit"` is already in that switch; it is.

- [ ] **Step 5: Regenerate the README table**

Run: `FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EndpointCoverageTests"`
Expected: the generated block in `README.md` is rewritten and now reads **82 of FMP's 230 endpoint paths are modelled**, with `fmp.Directory` carrying 15 rows and `fmp.Search` carrying 7.

- [ ] **Step 6: Verify the count is exactly 82**

Run: `grep -c '^| `stable/' README.md` and `grep '82 of FMP' README.md`
Expected: the table row count reflects the new total and the sentence reads 82. If it reads fewer, a method is driving a path the generator did not record — check that every new method appears in the table before continuing.

- [ ] **Step 7: Run the whole offline suite**

Run: `dotnet test tests/FmpDotNet.Tests`
Expected: PASS. Record the total test count.

- [ ] **Step 8: Commit**

```bash
git add tests/FmpDotNet.SmokeTests/LiveApi.cs tests/FmpDotNet.SmokeTests/Probe.cs \
        tests/FmpDotNet.Tests/EndpointCoverageTests.cs README.md
git commit -m "test: name the identifiers both argument synthesisers would otherwise guess wrong (#25)"
```

---

### Task 9: Correct the README prose, then re-record the live baseline

The generated table is right after Task 8; the hand-written prose around it still repeats issue #25's wrong group list. The smoke baseline must be recorded **last**, because it is the only step that spends live calls and it must run against the finished surface.

**Files:**
- Modify: `README.md` (the "Reaching an endpoint that is not modelled" section)
- Modify: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`
- Modify: `.github/workflows/smoke.yml` (timeout comment)

**Interfaces:**
- Consumes: everything from Tasks 1–8
- Produces: nothing further

- [ ] **Step 1: Correct the remaining-paths prose**

In `README.md`, replace the paragraph beginning "The rest is unbuilt rather than blocked" with:

```markdown
The rest is unbuilt rather than blocked: `trader`, the consumer driving this SDK, does not call it. **148 paths
remain**, and they are not spread the way FMP's own section headings suggest. The largest groups are Statements
(19), Company (13), SEC Filings (12), Market Performance (11) and News (10); Analyst, Calendar and the Indexes
constituent lists carry 7 apiece.

Split by asset class the balance is lopsided. **106 of the 148 are equity-only** — statements, filings, ownership,
analyst opinion, corporate actions — against 42 that are shared or belong to another asset class. That is because
what has been built so far is price plumbing, and one `GetQuoteAsync` serves equities, ETFs, indices, commodities,
forex and crypto alike: the breadth came free, and the equity depth is the part still to build.

Commodity, Forex and Crypto contribute **one path each** to that remainder — their symbol lists, and
`fmp.Directory` now covers all three. Everything else under those headings, and most of what is under Indexes, is
`stable/quote` and `stable/historical-price-eod` re-documented, which `fmp.Quote` and `fmp.Chart` already reach.
`GetQuoteAsync("BTCUSD")`, `GetQuoteAsync("EURUSD")`, `GetQuoteAsync("^GSPC")` and `GetQuoteAsync("GCUSD")` were
each measured returning the ordinary seventeen-field quote. That is why 230 unique paths back FMP's 263 documented
APIs, and why the denominator here is the smaller number.
```

- [ ] **Step 2: Verify the arithmetic**

230 − 82 = 148. Confirm the paragraph says 148 and that 106 + 42 = 148.

- [ ] **Step 3: Commit the prose**

```bash
git add README.md
git commit -m "docs: correct the remaining-paths prose, which repeated the issue's wrong group list (#25)"
```

- [ ] **Step 4: Confirm the key is present and never enters the tree**

Run: `python3 -c "import re,pathlib; print('FMP_API_KEY' in pathlib.Path('.env').read_text())"`
Expected: `True`. **Never `source` or `set -a` the `.env` file** — doing so has previously clobbered `PATH` for the whole shell. Extract only `FMP_API_KEY` with a regex, as `scratchpad/run_live.py` does.

- [ ] **Step 5: Re-record the ordinary baseline against the live API**

Run, with `FMP_API_KEY` exported into that process only:

```bash
FMPDOTNET_UPDATE_SMOKE_BASELINE=1 dotnet test tests/FmpDotNet.SmokeTests \
  --filter "FullyQualifiedName~OrdinaryEndpointShapeTests"
```

Expected: `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` is rewritten with **66 ordinary endpoints** (49 before, plus the 17 here). Do **not** set `FMPDOTNET_SMOKE_BULK` — the bulk tier is 2 calls/minute and none of it changed.

- [ ] **Step 6: Read the diff before trusting it**

Run: `git diff --stat tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`
Then inspect the new groups. Expected, and each of these is measured rather than a fault:
- `Directory.GetCommodityListAsync` records `Exchange` **null** — null on all 40 rows
- `Search.GetExchangeVariantsAsync` records `Cik` **null** if the probe's first row is not the primary listing
- Everything else records `rows`

**Any endpoint recording `empty` is a failure to investigate, not to accept** — it means the synthesised argument was not understood, which is exactly what Task 8 exists to prevent.

- [ ] **Step 7: Re-time the sweep and update the workflow comment**

The run in Step 5 prints its own duration. In `.github/workflows/smoke.yml`, update the timeout comment from "49 endpoints in 13 s, measured 2026-08-27" to the new endpoint count and duration, and note that four of the additions are whole-universe downloads — `financial-statement-symbol-list` at 68,200 rows, `etf-list` at 14,567, `earnings-transcript-list` at 11,178, `cryptocurrency-list` at 4,793 — so the growth is bytes rather than requests. Raise the timeout only if the measured duration warrants it; say so either way.

- [ ] **Step 8: Confirm no key leaked into the tree**

Run: `git diff --cached | grep -ci "$(python3 -c "import re,pathlib;print(re.search(r'FMP_API_KEY\s*=\s*[\"\x27]?([^\"\x27\s#]+)', pathlib.Path('.env').read_text()).group(1))")"`
Expected: `0`. Also confirm `.env` is untracked: `git check-ignore -v .env` should name `.gitignore`.

- [ ] **Step 9: Run everything offline one last time**

Run: `dotnet build && dotnet test tests/FmpDotNet.Tests && dotnet test tests/FmpDotNet.SmokeTests`
Expected: build clean with `TreatWarningsAsErrors`, all unit tests pass, and the smoke assembly's offline tests pass while the live ones skip (no `FMP_API_KEY` in this process).

- [ ] **Step 10: Commit and open the pull request**

```bash
git add tests/FmpDotNet.SmokeTests/baseline-ordinary.txt .github/workflows/smoke.yml
git commit -m "test: re-record the ordinary baseline across 66 endpoints (#25)"
git push -u origin feat/directory-and-search
gh pr create --fill
```

`master` requires the check `.NET — build + test` and a pull request with zero approvals. Wait for green, then merge.

- [ ] **Step 11: Correct the issue text and close out**

Issue #25's body lists Commodity, Forex, Crypto and Indexes as long-tail groups and omits Statements, Company, Analyst, Calendar, Directory, Search and Economics. Edit it to the measured breakdown, note that Directory and Search are now complete, and record that 148 paths remain with the 106/42 equity split — so the next person picking up #25 inherits the measurement rather than repeating it.

---

## Self-Review

**Spec coverage.** Every section of the design maps to a task: the eleven list endpoints across Tasks 1–5, the six search endpoints across Tasks 6–7, all five named traps (`symbol-change` default → Task 4, `cik-list` paging → Task 5, crypto supply → Task 2, unlabelled `marketCap` → Task 6, the `exchange` inversion → Task 7), the argument-synthesiser guards → Task 8, and README plus baseline → Tasks 8–9. The spec's "deliberately not in scope" list needs no task by definition.

**One deviation, recorded above in File Structure:** the spec's internal wire shapes for `search-cusip`/`search-isin` are unnecessary once those are separate public models. Each binds its own wire key and both name the property `CompanyName`, which is the same guarantee with less machinery. The test in Task 6 Step 2 pins it.

**Type consistency.** `MaxCikListPageSize` and `SymbolChangeRequestLimit` are referenced in Tasks 4, 5 and 8 with the spellings defined in Tasks 4 and 5. `CompanySymbol` is reused in Task 1 with its existing `Symbol`/`Name` properties. `ExchangeInfo.Exchange` (Task 3) is cross-referenced from `SymbolSearchResult.Exchange` (Task 6) and `ExchangeVariant.ExchangeShortName` (Task 7) — all three name the short code, which is the point of the cross-references. `CikEntry.Cik` (Task 5) and `CikSearchResult.Cik` (Task 6) are both `string?` carrying the padded form, so they round-trip as the docs claim.

**Count check.** 17 paths: 11 on `fmp.Directory` (Tasks 1–5: countries, ETF, commodity, crypto, forex, index, exchanges, statement symbols, transcripts, symbol-change, cik-list) and 6 on `fmp.Search` (Tasks 6–7). 65 + 17 = 82 modelled, 230 − 82 = 148 remaining. 14 new models: 4 asset-class (Task 2), 3 in Tasks 3, 2 more in Tasks 4–5, 4 search (Task 6), 1 variant (Task 7) = 4+3+2+4+1 = 14, plus the `CountryName` internal wire record which is not a public model.
