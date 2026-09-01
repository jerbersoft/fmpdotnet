using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The two <c>stable/price-target-*</c> paths and the converter one of them needs, checked against
/// captures taken live 2026-08-28.
///
/// <para><b><c>publishers</c> arrives as a string whose content is a JSON array</b> — the only nested-format
/// field in this slice. Unlike the <c>businessAddress</c> field of the previous slice, which was a stringified
/// Python list that broke on an apostrophe, this one is real JSON and survives a real parse:
/// <c>Investor's Business Daily</c> comes back intact.</para>
///
/// <para>The shipped <see cref="BulkPriceTargetSummary.Publishers"/> is already
/// <see cref="IReadOnlyList{T}"/> of <see cref="string"/>, so before this slice the bulk path and the ordinary
/// path disagreed about the type of one field. They no longer do.</para></summary>
public class PriceTargetTests
{
    private static (AnalystEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new AnalystEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    // ---- the converter, exercised directly ----------------------------------------------------------------

    [Fact]
    public void A_json_array_inside_a_string_is_parsed_into_a_list()
    {
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[\"StreetInsider\",\"Benzinga\"]"}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Equal(["StreetInsider", "Benzinga"], row.Publishers);
    }

    [Fact]
    public void An_apostrophe_inside_a_publisher_name_survives_the_parse()
    {
        // The measured value, and the reason a real parse is safe here where it was not on businessAddress:
        // the apostrophe sits inside a double-quoted JSON string and is correctly escaped, so nothing has to
        // guess where the element boundaries are.
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[\"Investor's Business Daily\",\"Barrons\"]"}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Equal(["Investor's Business Daily", "Barrons"], row.Publishers);
    }

    [Fact]
    public void An_empty_json_array_reads_as_an_empty_list_and_not_as_null()
    {
        // Empty and null mean different things, deliberately: an empty list is FMP saying there are no
        // publishers, null is this SDK saying the field could not be read. The shipped
        // BulkPriceTargetSummary.Publishers already draws that distinction and measured 874 empty arrays across
        // 5,277 bulk rows, so the empty case is common rather than theoretical.
        var row = JsonSerializer.Deserialize(
            """[{"publishers":"[]"}]""", FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.NotNull(row.Publishers);
        Assert.Empty(row.Publishers);
    }

    [Fact]
    public void A_string_that_is_not_json_costs_that_field_and_nothing_else()
    {
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","publishers":"not json at all","allTimeCount":259}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(259, row.AllTimeCount);
    }

    [Fact]
    public void A_json_null_reads_as_null()
    {
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","publishers":null}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
    }

    [Theory]
    [InlineData("""["StreetInsider","Benzinga"]""")]      // a real array, not a string containing one
    [InlineData("""{"a":1}""")]                            // an object
    [InlineData("""42""")]                                 // a number
    [InlineData("""true""")]                               // a boolean
    public void A_token_that_is_not_a_string_costs_that_field_and_never_the_response(string publishers)
    {
        // The defect the previous slice found on BusinessAddressJsonConverter, guarded against from the start
        // here. The realistic trigger is FMP fixing the double-encoding: if `publishers` ever arrives as a real
        // JSON array, an unguarded GetString() throws -- and because FmpTransport does not wrap
        // DeserializeAsync, that costs the WHOLE response rather than the one field.
        //
        // The array and object rows are the ones that matter most: for those the reader sits on the OPENING
        // token only, and returning null without calling reader.Skip() makes System.Text.Json's VerifyRead
        // throw its own JsonException ("read too much or not enough") in place of the one the guard exists to
        // avoid. A guard without Skip() passes the scalar rows here and fails these two.
        var row = JsonSerializer.Deserialize(
            $$"""[{"symbol":"AAPL","allTimeCount":259,"publishers":{{publishers}}}]""",
            FmpJsonContext.Default.ListPriceTargetSummary)![0];

        Assert.Null(row.Publishers);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(259, row.AllTimeCount);
    }

    // ---- price-target-consensus ---------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_consensus_row_binds_all_five_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-consensus.AAPL.json"));

        var consensus = await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.NotNull(consensus);
        Assert.Empty(Binding.Unbound(consensus));
        Assert.Equal("AAPL", consensus.Symbol);
        Assert.Equal(400m, consensus.TargetHigh);
        Assert.Equal(245m, consensus.TargetLow);
        Assert.Equal(340.72m, consensus.TargetConsensus);
        Assert.Equal(360m, consensus.TargetMedian);
    }

    [Fact]
    public async Task The_consensus_can_sit_outside_the_median_and_the_sdk_does_not_reconcile_them()
    {
        // Measured: consensus 340.72, median 360 -- the mean below the median, which is what a left-skewed
        // distribution of targets looks like and is not a fault. Nothing here recomputes or cross-checks.
        var (endpoints, _) = Build(Binding.Fixture("price-target-consensus.AAPL.json"));

        var consensus = await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.True(consensus!.TargetConsensus < consensus.TargetMedian);
    }

    [Fact]
    public async Task The_consensus_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetPriceTargetConsensusAsync("AAPL");

        Assert.Equal("stable/price-target-consensus", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL", handler.Requests.Single().Query);
    }

    // ---- price-target-summary -----------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_summary_row_binds_all_ten_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-summary.AAPL.json"));

        var summary = await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.NotNull(summary);
        Assert.Empty(Binding.Unbound(summary));
        Assert.Equal(5, summary.LastMonthCount);
        Assert.Equal(323.73m, summary.LastMonthAvgPriceTarget);
        Assert.Equal(17, summary.LastQuarterCount);
        Assert.Equal(331.69m, summary.LastQuarterAvgPriceTarget);
        Assert.Equal(71, summary.LastYearCount);
        Assert.Equal(307.39m, summary.LastYearAvgPriceTarget);
        Assert.Equal(259, summary.AllTimeCount);
        Assert.Equal(232.31m, summary.AllTimeAvgPriceTarget);
    }

    [Fact]
    public async Task The_captured_publishers_string_parses_into_its_seven_names()
    {
        var (endpoints, _) = Build(Binding.Fixture("price-target-summary.AAPL.json"));

        var summary = await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.Equal(
            ["StreetInsider", "Benzinga", "Pulse 2.0", "MarketWatch", "Investing", "Barrons",
             "Investor's Business Daily"],
            summary!.Publishers);
    }

    [Fact]
    public void The_ordinary_and_bulk_summaries_now_agree_on_the_type_of_publishers()
    {
        // The whole point of the converter. Before this slice the bulk path parsed the nested array and the
        // ordinary path did not exist; shipping the ordinary one as a raw string would have left two types for
        // one field, and a caller moving between them would have had to know which.
        Assert.Equal(
            typeof(BulkPriceTargetSummary).GetProperty(nameof(BulkPriceTargetSummary.Publishers))!.PropertyType,
            typeof(PriceTargetSummary).GetProperty(nameof(PriceTargetSummary.Publishers))!.PropertyType);
    }

    [Fact]
    public async Task The_summary_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetPriceTargetSummaryAsync("AAPL");

        Assert.Equal("stable/price-target-summary", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL", handler.Requests.Single().Query);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Both_methods_answer_null_for_an_unknown_symbol()
    {
        var (consensus, _) = Build("[]");
        var (summary, _) = Build("[]");

        Assert.Null(await consensus.GetPriceTargetConsensusAsync("NOSUCHTICKER"));
        Assert.Null(await summary.GetPriceTargetSummaryAsync("NOSUCHTICKER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Both_methods_refuse_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (consensus, h1) = Build();
        var (summary, h2) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => consensus.GetPriceTargetConsensusAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => summary.GetPriceTargetSummaryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
    }

    [Fact]
    public async Task Both_methods_refuse_a_null_symbol_before_spending_a_request()
    {
        var (consensus, _) = Build();
        var (summary, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => consensus.GetPriceTargetConsensusAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => summary.GetPriceTargetSummaryAsync(null!));
    }
}
