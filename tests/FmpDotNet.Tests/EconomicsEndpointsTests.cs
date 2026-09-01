using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/economic-calendar</c>, checked against responses captured live from FMP on 2026-08-26.
///
/// <para>The two fixtures are chosen for what they prove together rather than for coverage. The August one is a
/// single day — 78 rows across 19 countries — and carries the EDT anchor; the January one is a whole quiet week
/// that legitimately contains just 2 rows, and carries the EST anchor. Between them they pin the timezone reading,
/// which is the entire risk on this endpoint, and they are the evidence that a row count says nothing about
/// completeness here.</para></summary>
public class EconomicsEndpointsTests
{
    private static readonly DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (EconomicsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new EconomicsEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static async Task<EconomicRelease> RowAsync(string fixture, LocalDate from, LocalDate to, string @event)
    {
        // A stub each: FmpTransport disposes the response once it has read it, so one canned message cannot serve
        // two calls — the second would throw ObjectDisposedException from inside the stream, pointing at the
        // reader rather than at the lifetime that ended it.
        var rows = await Build(Fixture(fixture)).Endpoints.GetEconomicCalendarAsync(from, to);
        return Assert.Single(rows, r => r.Event == @event);
    }

    [Fact]
    public async Task Both_dst_anchors_resolve_to_the_right_eastern_wall_clock_through_tzdb()
    {
        // THE test on this endpoint. The wire form "yyyy-MM-dd HH:mm:ss" is shared, character for character, with
        // the statement endpoints' acceptedDate — which is EASTERN — so the string cannot tell you which converter
        // is right and the compiler will never object. What settles it is the DST shift, and settling it needs
        // BOTH anchors: an August row alone is satisfied by a fixed UTC-4 and a January row alone by a fixed
        // UTC-5, while no single offset satisfies the pair.
        //
        // Asserting the UTC Instant alone would NOT catch this. NullableEasternInstantJsonConverter parses the
        // same string happily; it would simply produce an Instant 4 or 5 hours late, and a test that pinned that
        // wrong Instant would be just as green. So the assertion goes the whole way a caller would: through the
        // tz database, to the wall clock a person reading the release actually sees.
        var august = await RowAsync(
            "economic-calendar.2026-08-26.json",
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26),
            "Core PCE Price Index MoM (Jul)");

        var january = await RowAsync(
            "economic-calendar.2027-01.json",
            new LocalDate(2027, 1, 25), new LocalDate(2027, 2, 1),
            "Fed Interest Rate Decision");

        // BEA releases Personal Income and Outlays at 08:30 New York; 26 August is EDT, so UTC-4.
        var edt = august.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDateTime(2026, 8, 26, 8, 30, 0), edt.LocalDateTime);
        Assert.Equal(Offset.FromHours(-4), edt.Offset);

        // The FOMC statement lands at 14:00 New York; 27 January is EST, so UTC-5.
        var est = january.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDateTime(2027, 1, 27, 14, 0, 0), est.LocalDateTime);
        Assert.Equal(Offset.FromHours(-5), est.Offset);

        // Two DIFFERENT offsets six months apart is the whole argument: neither a hardcoded -4 nor a hardcoded -5
        // satisfies both rows, so the conversion has to go through tzdb and the stored value has to be UTC.
        Assert.NotEqual(edt.Offset, est.Offset);
        Assert.Equal(Instant.FromUtc(2026, 8, 26, 12, 30, 0), august.Timestamp);
        Assert.Equal(Instant.FromUtc(2027, 1, 27, 19, 0, 0), january.Timestamp);

        // What NullableEasternInstantJsonConverter would have produced from the identical strings.
        Assert.NotEqual(Instant.FromUtc(2026, 8, 26, 16, 30, 0), august.Timestamp);
        Assert.NotEqual(Instant.FromUtc(2027, 1, 28, 0, 0, 0), january.Timestamp);
    }

    [Fact]
    public async Task Zoning_once_keeps_the_eastern_day_and_the_eastern_clock_on_the_same_release()
    {
        // Why the model surfaces one Instant rather than a date and a time-of-day. A release near UTC midnight
        // belongs to the PREVIOUS Eastern day, so deriving the day from one conversion and the clock from another
        // silently puts them a day apart. Converting once makes that unrepresentable.
        var (endpoints, _) = Build(
            """
            [{"date":"2026-01-02 03:00:00","country":"NZ","event":"Midnight Straddle","currency":"NZD",
              "previous":null,"estimate":null,"actual":null,"change":null,"impact":"Low",
              "changePercentage":0,"unit":null}]
            """);

        var row = Assert.Single(
            await endpoints.GetEconomicCalendarAsync(new LocalDate(2026, 1, 2), new LocalDate(2026, 1, 2)));

        var et = row.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDate(2026, 1, 1), et.Date);       // the previous day,
        Assert.Equal(new LocalTime(22, 0, 0), et.TimeOfDay);    // and 22:00 on it
    }

    [Fact]
    public async Task Maps_every_field_of_a_captured_published_row()
    {
        // A row with all four figures populated — the published case. Negative values throughout, which is normal
        // for an inventory draw and would be lost by any unsigned reading.
        var row = await RowAsync(
            "economic-calendar.2026-08-26.json",
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26),
            "EIA Distillate Stocks Change (Aug/21)");

        Assert.Equal(Instant.FromUtc(2026, 8, 26, 14, 30, 0), row.Timestamp);
        Assert.Equal("US", row.Country);
        Assert.Equal("EIA Distillate Stocks Change (Aug/21)", row.Event);
        Assert.Equal("USD", row.Currency);
        Assert.Equal(-1.53m, row.Previous);
        Assert.Equal(-1.6m, row.Estimate);
        Assert.Equal(-2.228m, row.Actual);
        Assert.Equal(-0.698m, row.Change);
        Assert.Equal("Low", row.Impact);
        Assert.Equal(-45.621m, row.ChangePercentage);
        Assert.Equal("M", row.Unit);
    }

    [Theory]
    [InlineData("economic-calendar.2026-08-26.json")]
    [InlineData("economic-calendar.2027-01.json")]
    public void Model_and_payload_agree_field_for_field(string fixture)
    {
        // Both directions matter. A wrong [JsonPropertyName] does not fail — it silently reads null — and a field
        // FMP sends that no property claims is data being thrown away. Every row of both captures carried exactly
        // these eleven wire names, none missing and none extra, so a change on either side should turn this red.
        using var doc = JsonDocument.Parse(Fixture(fixture));
        var mapped = typeof(EconomicRelease).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"EconomicRelease.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var wire = element.EnumerateObject().Select(p => p.Name).ToHashSet();
            Assert.Empty(wire.Except(mapped));   // FMP sends it, the model ignores it
            Assert.Empty(mapped.Except(wire));   // the model expects it, FMP no longer sends it
        }
    }

    [Fact]
    public async Task An_unreported_event_has_four_nulls_but_changePercentage_arrives_as_a_real_zero()
    {
        // The trap this endpoint sets for anyone computing a surprise. previous/estimate/actual/change are all
        // null on a speech, but changePercentage comes back as 0 rather than null, so on that one field an absent
        // value and a measured zero are indistinguishable. Measured 2026-08-26 over the 713-row week: of the 15
        // rows with all four null, 12 carried 0 here and only 3 carried null — so the zero is the COMMON shape and
        // treating it as data skews an average of "moves" hard toward zero.
        var row = await RowAsync(
            "economic-calendar.2026-08-26.json",
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26),
            "Fed Barkin Speech");

        Assert.Null(row.Previous);
        Assert.Null(row.Estimate);
        Assert.Null(row.Actual);
        Assert.Null(row.Change);
        Assert.Equal(0m, row.ChangePercentage);

        // The zero is present in the payload, not absent — this is FMP's value, not the SDK defaulting a null.
        using var doc = JsonDocument.Parse(Fixture("economic-calendar.2026-08-26.json"));
        var wire = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("event").GetString() == "Fed Barkin Speech");
        Assert.Equal(JsonValueKind.Null, wire.GetProperty("actual").ValueKind);
        Assert.Equal(JsonValueKind.Number, wire.GetProperty("changePercentage").ValueKind);
    }

    [Fact]
    public async Task An_unreported_event_can_equally_arrive_with_changePercentage_null()
    {
        // The other half of the same trap, and the reason ChangePercentage is nullable rather than decimal. The
        // zero is not a reliable "unreported" marker either: the same day's Bundesbank speech has the identical
        // four nulls and reports null here instead. Both shapes occur on rows that mean the same thing, so the
        // only sound gate is Actual being non-null.
        var row = await RowAsync(
            "economic-calendar.2026-08-26.json",
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26),
            "Bundesbank Balz Speech");

        Assert.Null(row.Previous);
        Assert.Null(row.Estimate);
        Assert.Null(row.Actual);
        Assert.Null(row.Change);
        Assert.Null(row.ChangePercentage);
    }

    [Fact]
    public async Task Returns_every_country_and_every_impact_because_filtering_is_the_callers_job()
    {
        // The endpoint is global and the SDK adds no country or impact parameter. 78 rows for one day across 19
        // codes, three impact labels, and EU among the countries — which is not ISO-3166, so a caller parsing
        // these as regions rather than matching them as strings gets a surprise.
        var (endpoints, _) = Build(Fixture("economic-calendar.2026-08-26.json"));

        var rows = await endpoints.GetEconomicCalendarAsync(
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26));

        Assert.Equal(78, rows.Count);
        Assert.Equal(19, rows.Select(r => r.Country).Distinct().Count());
        Assert.Contains(rows, r => r.Country == "EU");   // the euro area, not a country and not ISO-3166
        Assert.Contains(rows, r => r.Country == "UK");   // ISO-3166 says GB
        Assert.Equal(["High", "Low", "Medium"], rows.Select(r => r.Impact!).Distinct().Order());
        Assert.All(rows, r => Assert.Equal(2, r.Country!.Length));
    }

    [Fact]
    public async Task A_quiet_week_really_is_two_rows_so_a_row_count_cannot_test_completeness()
    {
        // from=2027-01-25&to=2027-02-01 answered exactly 2 rows, live and complete. That is the argument against
        // guarding wide-window truncation with a count threshold: this legitimately sparse week would trip any
        // threshold that also caught the measured 535-row six-month truncation. Completeness on this endpoint is
        // an edge-coverage question, not a volume one.
        var (endpoints, _) = Build(Fixture("economic-calendar.2027-01.json"));

        var rows = await endpoints.GetEconomicCalendarAsync(
            new LocalDate(2027, 1, 25), new LocalDate(2027, 2, 1));

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("US", r.Country));
        Assert.All(rows, r => Assert.Equal("High", r.Impact));
    }

    [Fact]
    public async Task Rows_are_returned_in_the_order_fmp_sends_them_newest_first()
    {
        // Measured descending on both captures. The SDK does not re-sort — reordering a payload hides an upstream
        // change — but nothing promises this either, so the doc tells callers who need chronological order to say
        // so explicitly.
        var (endpoints, _) = Build(Fixture("economic-calendar.2026-08-26.json"));

        var stamps = (await endpoints.GetEconomicCalendarAsync(
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26))).Select(r => r.Timestamp).ToList();

        Assert.Equal(stamps.OrderByDescending(s => s), stamps);
        Assert.Equal(Instant.FromUtc(2026, 8, 26, 23, 50, 0), stamps[0]);
        Assert.Equal(Instant.FromUtc(2026, 8, 26, 1, 0, 0), stamps[^1]);
    }

    [Fact]
    public async Task An_empty_payload_is_an_empty_list_never_null()
    {
        var (endpoints, _) = Build("[]");

        var rows = await endpoints.GetEconomicCalendarAsync(
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 27));

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Rejects_a_backwards_range_before_spending_a_request()
    {
        // FMP answers a backwards range with [] and HTTP 200, so left alone this reads as "no releases that week"
        // rather than as the argument mistake it is — and costs a call from the key's quota to say nothing.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetEconomicCalendarAsync(new LocalDate(2026, 9, 1), new LocalDate(2026, 8, 25)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Accepts_a_single_day_where_from_equals_to()
    {
        // The boundary the guard must not swallow: from == to is the one range size measured to be safe from the
        // wide-window truncation, so it had better not be rejected as backwards.
        var (endpoints, handler) = Build(Fixture("economic-calendar.2026-08-26.json"));

        var rows = await endpoints.GetEconomicCalendarAsync(
            new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26));

        Assert.Equal(78, rows.Count);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Hits_its_own_path_carrying_from_and_to()
    {
        // Equality, not Contains: there is no country, impact, page or limit parameter on this endpoint, and an
        // invented one would be accepted silently by FMP rather than rejected. Dates go out in FMP's yyyy-MM-dd.
        var (endpoints, handler) = Build();

        await endpoints.GetEconomicCalendarAsync(new LocalDate(2026, 8, 25), new LocalDate(2026, 9, 1));

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/economic-calendar", uri.AbsolutePath);
        Assert.Equal("?from=2026-08-25&to=2026-09-01", uri.Query);
    }

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
        Assert.Equal("", handler.Requests[0].Query);
        Assert.Equal("/stable/treasury-rates", treasuryHandler.Requests[0].AbsolutePath);
        // Asserted on both, not just the first: `treasury-rates` takes an optional range, so this is also the
        // guard that a null `from`/`to` stays off the wire rather than going out empty.
        Assert.Equal("", treasuryHandler.Requests[0].Query);
    }

    [Fact]
    public async Task An_empty_answer_is_an_empty_list_rather_than_null_on_all_three_methods()
    {
        // Pins the "never null" half of each method's doc comment: GetIndicatorAsync's "The observations in
        // the range, or an empty list — never null", GetMarketRiskPremiumsAsync's "Every country FMP
        // publishes a premium for. Never null", and GetTreasuryRatesAsync's "...truncated to about three
        // months. Never null". A separate Build() per call, since each stub handler answers one request only.
        var (indicator, _) = Build();
        var (premiums, _) = Build();
        var (treasury, _) = Build();

        var indicatorRows = await indicator.GetIndicatorAsync(EconomicIndicator.Gdp);
        var premiumRows = await premiums.GetMarketRiskPremiumsAsync();
        var treasuryRows = await treasury.GetTreasuryRatesAsync();

        Assert.NotNull(indicatorRows);
        Assert.Empty(indicatorRows);
        Assert.NotNull(premiumRows);
        Assert.Empty(premiumRows);
        Assert.NotNull(treasuryRows);
        Assert.Empty(treasuryRows);
    }
}
