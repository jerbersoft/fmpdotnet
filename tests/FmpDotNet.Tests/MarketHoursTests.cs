using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three Market Hours paths, checked against captures taken live 2026-08-30.</summary>
public class MarketHoursTests
{
    [Fact]
    public void An_ordinary_exchange_row_binds_its_six_keys_and_parses_both_hours()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(5, rows.Count);
        var asx = rows[0];

        Assert.Equal("ASX", asx.Exchange);
        Assert.Equal("Australian Securities Exchange", asx.Name);
        Assert.Equal("Australia/Sydney", asx.Timezone);
        Assert.False(asx.IsMarketOpen);
        Assert.Equal("10:00 AM +10:00", asx.OpeningHourText);
        Assert.Equal(new OffsetTime(new LocalTime(10, 0), Offset.FromHours(10)), asx.OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(16, 0), Offset.FromHours(10)), asx.ClosingHour);
        Assert.False(asx.IsClosedToday);

        // The afternoon pair is ABSENT on this row, and on 74 of the 81 measured. That is normal, not
        // missing data — see the lunch-break test below.
        Assert.Equal(
            ["ClosingAdditionalText", "OpeningAdditionalText"], Binding.Unbound(asx));
        Assert.Null(asx.OpeningAdditional);
        Assert.Null(asx.ClosingAdditional);
    }

    [Fact]
    public void A_closed_exchange_parses_no_hours_and_says_why()
    {
        // "CLOSED" fills 124 of 176 hour slots measured 2026-08-30. Without IsClosedToday a caller sees a
        // null OffsetTime and cannot tell "the exchange is shut today" from "FMP sent something this SDK
        // could not parse" — two states that call for completely different responses.
        //
        // This test fails if IsClosedToday is dropped, and it fails if the raw text stops being bound: both
        // are the shortcut an implementer takes when a converter looks like the obvious answer.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var nasdaq = rows[3];

        Assert.True(nasdaq.IsClosedToday);
        Assert.Null(nasdaq.OpeningHour);
        Assert.Null(nasdaq.ClosingHour);
        Assert.Equal("CLOSED", nasdaq.OpeningHourText);   // the wire is preserved exactly
        Assert.Equal("CLOSED", nasdaq.ClosingHourText);

        // And an unparseable value that is NOT the sentinel reads as null hours WITHOUT claiming a closure.
        var garbled = JsonSerializer.Deserialize(
            """[{"openingHour":"half past nine"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)![0];

        Assert.Null(garbled.OpeningHour);
        Assert.False(garbled.IsClosedToday);
    }

    [Fact]
    public void The_lunch_break_exchanges_keep_their_afternoon_session()
    {
        // The keys were present on 7 of 81 rows measured 2026-08-30 and absent from 74. All seven break for
        // lunch: SET, JKT, JPX, SHH, SHZ, SES and HOSE. A record built from the response's FIRST row — ASX,
        // six keys — reports Tokyo closing at 11:30 AM and loses the larger half of its trading day.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var jpx = rows[1];

        Assert.Equal(new OffsetTime(new LocalTime(9, 0), Offset.FromHours(9)), jpx.OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(11, 30), Offset.FromHours(9)), jpx.ClosingHour);
        Assert.Equal(new OffsetTime(new LocalTime(12, 30), Offset.FromHours(9)), jpx.OpeningAdditional);
        Assert.Equal(new OffsetTime(new LocalTime(15, 30), Offset.FromHours(9)), jpx.ClosingAdditional);
        Assert.Empty(Binding.Unbound(jpx));
        Assert.False(jpx.IsClosedToday);
    }

    [Fact]
    public void A_negative_offset_hour_parses()
    {
        // Every offset in the 2026-08-30 capture set was POSITIVE, +03:00 to +12:00, because the captures
        // were taken on a Sunday when only Asia-Pacific and Gulf exchanges were trading — every American
        // exchange read "CLOSED". The negative form is therefore covered by this test rather than by a
        // capture, and the test is the only thing standing between this SDK and an offset-blind pattern.
        var rows = JsonSerializer.Deserialize(
            """[{"openingHour":"09:30 AM -05:00","closingHour":"04:00 PM -04:00"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(new OffsetTime(new LocalTime(9, 30), Offset.FromHours(-5)), rows[0].OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(16, 0), Offset.FromHours(-4)), rows[0].ClosingHour);
    }

    [Fact]
    public void Noon_and_midnight_land_on_the_right_hour()
    {
        // The classic 12-hour-clock defect: "12:00 PM" is noon and "12:00 AM" is midnight, and a pattern
        // that gets either backwards is wrong by twelve hours with nothing to reveal it. SES (Singapore)
        // closes its morning session at 12:00 PM +08:00 on the live wire, measured 2026-08-30.
        var rows = JsonSerializer.Deserialize(
            """[{"openingHour":"12:00 PM +08:00","closingHour":"12:00 AM +00:00"}]""",
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(new OffsetTime(new LocalTime(12, 0), Offset.FromHours(8)), rows[0].OpeningHour);
        Assert.Equal(new OffsetTime(new LocalTime(0, 0), Offset.FromHours(0)), rows[0].ClosingHour);
    }

    [Fact]
    public void IsClosedToday_and_IsMarketOpen_are_different_questions()
    {
        // IsClosedToday is about the exchange's own LOCAL CALENDAR DAY; IsMarketOpen is about this instant.
        // EGX shows hours on the Sunday the captures were taken — its Sunday is a trading day — and still
        // reports isMarketOpen false, because the capture landed outside its session. A caller who reads
        // IsClosedToday as "the market is not open right now" is wrong on exactly this row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var egx = rows[2];

        Assert.False(egx.IsClosedToday);
        Assert.False(egx.IsMarketOpen);
        Assert.Equal(new OffsetTime(new LocalTime(14, 15), Offset.FromHours(3)), egx.ClosingHour);
    }

    [Fact]
    public void The_single_exchange_response_is_the_same_row_as_the_list_carries()
    {
        // Not a restatement of the fixture — the reason ONE record serves TWO paths. For each of seven
        // exchanges cross-checked 2026-08-30, the single-exchange row compared equal key for key and value
        // for value to that exchange's row inside all-exchange-market-hours. If that ever stops being true,
        // this test is where it surfaces.
        var single = JsonSerializer.Deserialize(
            Binding.Fixture("exchange-market-hours.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;
        var fromList = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!
            .Single(r => r.Exchange == "NASDAQ");

        Assert.Single(single);
        Assert.Equal(fromList, single[0]);          // record equality: every bound property, all eight
    }

    [Fact]
    public void The_timezone_is_left_as_a_string_for_the_caller_to_resolve()
    {
        // All 81 values resolved as IANA zone identifiers (52 distinct) with no abbreviation and no fixed
        // offset among them, so the caller can hand this straight to DateTimeZoneProviders.Tzdb. The record
        // does not do it for them: which tzdb version to trust is an application decision, and resolving it
        // here would bake this SDK's NodaTime version into the answer.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-exchange-market-hours.head.json"),
            FmpJsonContext.Default.ListExchangeMarketHours)!;

        Assert.Equal(
            new[] { "Australia/Sydney", "Asia/Tokyo", "Africa/Cairo", "America/New_York", "Asia/Kuala_Lumpur" },
            rows.Select(r => r.Timezone).ToArray());
        Assert.All(rows, r => Assert.NotNull(DateTimeZoneProviders.Tzdb.GetZoneOrNull(r.Timezone!)));
    }

    [Fact]
    public void A_holiday_row_binds_its_six_ordinary_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.Equal(4, rows.Count);
        Assert.Equal("NASDAQ", rows[0].Exchange);
        Assert.Equal(new LocalDate(2026, 7, 3), rows[0].Date);
        Assert.Equal("Independence Day", rows[0].Name);
        Assert.True(rows[0].IsClosed);

        // Three properties come back empty on a full-closure row and every one of them is CORRECT.
        // adjOpenTime was null on all 446 rows measured 2026-08-30 — never once populated — adjCloseTime
        // is null wherever the exchange shut completely, and isFullyClosed is absent from that shape.
        Assert.Equal(
            new[] { "AdjustedCloseTime", "AdjustedOpenTime", "IsFullyClosed" }, Binding.Unbound(rows[0]));
    }

    [Fact]
    public void An_early_close_is_not_a_closure()
    {
        // THE trap on this path. Measured across 446 rows the two states are exact complements:
        //   isClosed true,  isFullyClosed absent, no adjusted time  — 396 rows
        //   isClosed null,  isFullyClosed false,  adjCloseTime set  —  50 rows
        //   isClosed false                                          —   0 rows
        // So IsClosed alone cannot answer "is the exchange closed that day?": null means an EARLY CLOSE,
        // not "unknown", and a caller who reads it as unknown treats 50 measured rows as unanswerable.
        //
        // This test fails against `bool IsClosed` (which cannot represent the null) and against any model
        // that folds the two wire fields into one enum.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;
        var early = rows[1];

        Assert.Null(early.IsClosed);                      // NOT false — the wire never sends false
        Assert.False(early.IsFullyClosed);
        Assert.Equal(new LocalTime(13, 0), early.AdjustedCloseTime);
        Assert.True(early.ClosesEarly);

        Assert.False(rows[0].ClosesEarly);                // a full closure does not "close early"
        Assert.True(rows[0].IsClosed);
        Assert.Null(rows[0].IsFullyClosed);
    }

    [Fact]
    public void ClosesEarly_is_derived_from_the_time_and_not_from_the_absent_flag()
    {
        // Both candidate signals — AdjustedCloseTime is not null, and IsFullyClosed == false — selected the
        // IDENTICAL 50 rows across all 446 measured 2026-08-30. The time won because it does not depend on
        // a key that is absent from 89% of rows: a future response that stops sending isFullyClosed would
        // silently turn every early close into a non-event under the other reading.
        var row = JsonSerializer.Deserialize(
            """[{"adjCloseTime":"13:00"}]""", FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.True(row.ClosesEarly);
        Assert.Null(row.IsFullyClosed);                   // and it still says so with the flag missing
    }

    [Fact]
    public void The_adjusted_times_parse_as_a_wall_clock_and_round_trip()
    {
        // All 50 non-null values matched HH:mm — 49 of them "13:00" and one "13:30" on 2015-11-27. Unlike
        // the long-form date converter, this pattern round-trips exactly, so this test may assert the
        // serialised form and does.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.Equal(new LocalTime(13, 0), rows[1].AdjustedCloseTime);
        Assert.Equal(new LocalTime(13, 30), rows[2].AdjustedCloseTime);

        Assert.Contains(
            "\"adjCloseTime\":\"13:30\"",
            JsonSerializer.Serialize(new List<Models.ExchangeHoliday> { rows[2] },
                FmpJsonContext.Default.ListExchangeHoliday),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_adjusted_times_carry_no_offset_which_is_the_other_half_of_the_two_time_spellings()
    {
        // "13:00" here against "09:30 AM +09:00" on ExchangeMarketHours — two spellings of a time in one
        // slice. This one is the sharper case: holidays-by-exchange has NO timezone key at all, verified
        // absent on all 446 rows, so the zone must come from the matching ExchangeMarketHours row. A
        // converter that guessed a zone here would be fabricating one.
        using var wire = JsonDocument.Parse(Binding.Fixture("holidays-by-exchange.NASDAQ.json"));

        Assert.All(
            wire.RootElement.EnumerateArray(),
            row => Assert.False(row.TryGetProperty("timezone", out _)));

        var parsed = JsonSerializer.Deserialize(
            """[{"adjCloseTime":"13:00 PM +09:00"}]""",
            FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.Null(parsed.AdjustedCloseTime);            // an offset-bearing value is not this shape
    }

    [Fact]
    public void AdjustedOpenTime_is_modelled_but_was_never_observed_carrying_a_value()
    {
        // null on ALL 446 rows measured 2026-08-30. It is modelled because the KEY is always present on the
        // six-key shape, and documented as never observed populated — which is a different statement from
        // "it is always null", and the doc must not make the stronger one.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("holidays-by-exchange.NASDAQ.json"),
            FmpJsonContext.Default.ListExchangeHoliday)!;

        Assert.All(rows, r => Assert.Null(r.AdjustedOpenTime));

        // And it binds when a value does arrive, so the modelling is not decorative.
        var populated = JsonSerializer.Deserialize(
            """[{"adjOpenTime":"10:30"}]""", FmpJsonContext.Default.ListExchangeHoliday)![0];

        Assert.Equal(new LocalTime(10, 30), populated.AdjustedOpenTime);
    }
}
