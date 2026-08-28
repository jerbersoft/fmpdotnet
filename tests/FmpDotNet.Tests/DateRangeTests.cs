using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The one backwards-range guard, promoted out of four copies.
///
/// <para>It existed as <c>ChartEndpoints.ThrowIfBackwards</c>, as
/// <c>CompanyEndpoints.ThrowIfBackwards</c>, as an unextracted <c>if</c> inside
/// <c>EconomicsEndpoints.GetEconomicCalendarAsync</c>, and inline again in
/// <c>CalendarEndpoints.GetEarningsCalendarAsync</c> — the same exception type and parameter thrown four
/// times, though the fourth had already drifted onto its own wording for the message. The SEC Filings slice
/// would have been the fifth. These tests fix the behaviour so the four call sites that used to own it cannot
/// drift apart now that they share it.</para></summary>
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
