using NodaTime;

namespace FmpDotNet.Tests;

public class FmpRequestTests
{
    [Fact]
    public void Trims_a_leading_slash_so_the_bare_host_base_address_composes()
    {
        Assert.Equal("stable/profile", new FmpRequest("/stable/profile").ToString());
    }

    [Fact]
    public void Drops_null_and_empty_parameters_so_call_sites_need_no_branching()
    {
        var request = new FmpRequest("stable/earnings")
            .With("symbol", "AAPL")
            .With("limit", (int?)null)
            .With("period", "");

        Assert.Equal("stable/earnings?symbol=AAPL", request.ToString());
    }

    [Fact]
    public void Escapes_parameter_values()
    {
        var request = new FmpRequest("stable/search-name").With("query", "Apple & Co/Ltd");

        Assert.Equal("stable/search-name?query=Apple%20%26%20Co%2FLtd", request.ToString());
    }

    [Fact]
    public void Renders_dates_in_the_form_fmp_expects()
    {
        var request = new FmpRequest("stable/earnings-calendar")
            .With("from", new LocalDate(2026, 5, 13))
            .With("to", new LocalDate(2026, 5, 19))
            .With("includeReportTimes", true);

        Assert.Equal("stable/earnings-calendar?from=2026-05-13&to=2026-05-19&includeReportTimes=true",
            request.ToString());
    }

    [Fact]
    public void Renders_an_instant_as_the_unix_seconds_fmp_reads()
    {
        // stable/*exchange-market-hours read `timestamp` as epoch SECONDS: measured 2026-09-02, the same
        // instant in milliseconds answered 0 open exchanges and 64 CLOSED at HTTP 200. So the overload renders
        // seconds, and it drops sub-second precision rather than rounding — a truncated instant is still the
        // same second.
        var request = new FmpRequest("stable/all-exchange-market-hours")
            .With("timestamp", Instant.FromUnixTimeSeconds(1788091200).PlusNanoseconds(999_999_999))
            .With("unused", (Instant?)null);

        Assert.Equal("stable/all-exchange-market-hours?timestamp=1788091200", request.ToString());
    }
}
