using NodaTime;

namespace FinancialModelingPrep.Tests;

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
    public void ToString_never_carries_the_api_key_so_a_request_is_safe_to_log()
    {
        var request = new FmpRequest("stable/profile").With("symbol", "AAPL");

        Assert.DoesNotContain("apikey", request.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apikey=secret", request.Build("secret"));
    }

    [Fact]
    public void Appends_the_key_with_the_right_separator_whether_or_not_a_query_exists()
    {
        Assert.Equal("stable/available-sectors?apikey=k", new FmpRequest("stable/available-sectors").Build("k"));
        Assert.Equal("stable/profile?symbol=AAPL&apikey=k",
            new FmpRequest("stable/profile").With("symbol", "AAPL").Build("k"));
    }
}
