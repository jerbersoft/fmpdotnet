using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>SC 13D/G beneficial-ownership disclosures — the one path in this slice whose numbers arrive as
/// JSON strings.
///
/// <para><b>Six of the fifteen fields are quoted numbers</b> — <c>"soleVotingPower": "0"</c>,
/// <c>"percentOfClass": "7.48"</c> — and across 422 rows measured 2026-08-28 every non-null value parsed
/// cleanly: no <c>"N/A"</c>, no separators, no currency symbols. <see cref="TolerantDecimalJsonConverter"/>
/// already reads a String token and returns null rather than throwing on anything it cannot parse, so it is
/// used exactly as shipped.</para>
///
/// <para><b>The path honours <c>limit</c> and ignores <c>page</c></b>, measured separately: <c>page=0</c> and
/// <c>page=1</c> returned byte-identical bodies. That asymmetry is why the method has one and not the
/// other.</para></summary>
public class BeneficialOwnershipTests
{
    private static (InstitutionalOwnershipEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InstitutionalOwnershipEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_quoted_number_binds_as_a_decimal()
    {
        // The wire sends "7.48", not 7.48. Without TolerantDecimalJsonConverter these six properties would
        // need the context's AllowReadingFromString to carry them, which it would — but a value the parser
        // rejects would then throw and cost the response, rather than binding null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(7.48m, rows[0].PercentOfClass);
        Assert.Equal(1099168953m, rows[0].AmountBeneficiallyOwned);
        Assert.Equal(0m, rows[0].SoleVotingPower);
        Assert.Equal(5.66m, rows[2].PercentOfClass);
        Assert.Equal(10208579m, rows[2].SoleVotingPower);
        Assert.Equal(322573028m, rows[2].SoleDispositivePower);
    }

    [Fact]
    public void A_null_quoted_number_binds_null_rather_than_throwing()
    {
        // Row 3 is the capture's row 55 — the only one of 99 with a null sharedVotingPower. The head of the
        // response had none, which is why it was pulled forward: a converter that throws on null would pass
        // every test written against the first three rows and fail in production.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Null(rows[2].SharedVotingPower);
        // And the rest of that row still arrives.
        Assert.Equal(9666535m, rows[2].SharedDispositivePower);
        Assert.Equal(332239563m, rows[2].AmountBeneficiallyOwned);
        Assert.Equal("Vanguard Group - 23-1945930", rows[2].NameOfReportingPerson);
    }

    [Fact]
    public void An_unparseable_quoted_number_costs_one_field_not_the_row()
    {
        // Not a measured wire form — all 422 values parsed. This pins the converter's contract: a value it
        // cannot read must bind null rather than abort the response and take the other fourteen fields with it.
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","percentOfClass":"N/A","amountBeneficiallyOwned":"1,234"}]""",
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.Single(rows);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Null(rows[0].PercentOfClass);
        Assert.Null(rows[0].AmountBeneficiallyOwned);
    }

    [Fact]
    public void A_captured_disclosure_binds_all_fifteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        // SharedVotingPower is the row's one deliberate null (see A_null_quoted_number_binds_null_rather_than_
        // throwing) — every other field binds, which is what this test is really pinning.
        Assert.Equal(["SharedVotingPower"], Binding.Unbound(rows[2]));
        Assert.Equal("0000320193", rows[2].Cik);
        Assert.Equal("AAPL", rows[2].Symbol);
        Assert.Equal("037833100", rows[2].Cusip);
        Assert.Equal("Pennsylvania", rows[2].CitizenshipOrPlaceOfOrganization);
        // Two SEC reporting-person codes in one field, comma-joined. FMP's value, not a parse target.
        Assert.Equal("EP, IN", rows[2].TypeOfReportingPerson);
        Assert.Equal(new LocalDate(2015, 2, 10), rows[2].FilingDate);
        Assert.Equal(new LocalDate(2015, 2, 10), rows[2].AcceptedDate);
    }

    [Fact]
    public void The_reporting_person_is_an_entity_which_is_why_this_path_is_not_on_the_insider_facade()
    {
        // The one assertion that pins the regrouping decision. An SC 13D/G reporting person is an institution —
        // "Vanguard Capital Management", "The Vanguard Group" — filing about a stake, not an officer filing
        // about a transaction. Every row in the capture names an entity, and none carries a transaction type,
        // a transaction date, a price or a securities-transacted count. Filed next to insider-trading/* it
        // would be the only path in that facade that is not an insider transaction.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("acquisition-of-beneficial-ownership.AAPL.json"),
            FmpJsonContext.Default.ListBeneficialOwnership)!;

        Assert.All(rows, r => Assert.Contains("Vanguard", r.NameOfReportingPerson));
        Assert.All(rows, r => Assert.NotNull(r.SoleDispositivePower));
    }

    [Fact]
    public async Task The_beneficial_ownership_call_sends_a_limit_and_no_page()
    {
        // Measured 2026-08-28 and separately from `limit`: page=0 and page=1 returned byte-identical bodies.
        // A `page` parameter here would be accepted, ignored, and invisible in the response — so it is not
        // offered, and this test fails if somebody adds it back by symmetry with the group's other paged paths.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetBeneficialOwnershipAsync("AAPL", limit: 50);

        Assert.Equal("/stable/acquisition-of-beneficial-ownership", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    public async Task A_beneficial_ownership_limit_above_the_sibling_cap_is_refused(int limit)
    {
        // 1,000 is a sibling-derived bound rather than a measured one on this path: the widest result set found
        // was 180 rows and limit=2000 for AAPL answered its whole 99-row set, so no query provoked a clamp. The
        // guard is applied because an unbounded limit is worse than a conservative one — see
        // MaxOwnershipPageSize, which says so.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetBeneficialOwnershipAsync("AAPL", limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_beneficial_ownership_limit_exactly_at_the_cap_is_accepted()
    {
        // The off-by-one boundary, on the third of the three guards that share this shape.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetBeneficialOwnershipAsync(
            "AAPL", limit: InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_blank_symbol_is_refused_with_ArgumentException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetBeneficialOwnershipAsync("   "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_with_ArgumentNullException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => endpoints.GetBeneficialOwnershipAsync(null!));

        Assert.Empty(handler.Requests);
    }
}
