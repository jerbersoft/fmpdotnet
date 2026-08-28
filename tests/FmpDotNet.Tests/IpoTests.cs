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
/// date-shaped field on both filing feeds was 10 characters — a plain ISO date, measured across 8,838 and 165
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
        // characters on all 8,838 disclosure rows and all 165 prospectus rows measured 2026-08-28. Pointing
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
        Func<Task> call = which == "disclosures"
            ? () => endpoints.GetIpoDisclosuresAsync(Day(2026, 8, 28), Day(2026, 8, 1))
            : () => endpoints.GetIpoProspectusesAsync(Day(2026, 8, 28), Day(2026, 8, 1));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(call);

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
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
