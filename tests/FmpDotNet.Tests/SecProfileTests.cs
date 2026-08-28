using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
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
}
