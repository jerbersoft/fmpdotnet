using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three ESG paths, checked against captures taken live 2026-08-29.</summary>
public class EsgTests
{
    private static (EsgEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new EsgEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_disclosure_binds_all_eleven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-disclosures.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgDisclosure)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 27), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 7, 31), rows[0].AcceptedDate);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("Apple Inc.", rows[0].CompanyName);
        Assert.Equal("10-Q", rows[0].FormType);
        Assert.Equal(68.41m, rows[0].EnvironmentalScore);
        Assert.Equal(47.36m, rows[0].SocialScore);
        Assert.Equal(61.32m, rows[0].GovernanceScore);
        Assert.Equal(59.03m, rows[0].EsgScore);
        Assert.StartsWith("https://www.sec.gov/Archives/edgar/", rows[0].Url);
    }

    [Fact]
    public void Cik_keeps_its_leading_zeros()
    {
        // "0000320193" is ten characters and only 320193 as a number. Typing this int? or long? drops four
        // significant characters and breaks every join against another cik-keyed path in this SDK.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-disclosures.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgDisclosure)!;

        Assert.Equal("0000320193", rows[0].Cik);
    }

    [Fact]
    public void The_uppercase_ESG_wire_names_bind_to_house_cased_properties()
    {
        // FMP spells these `ESGScore` and `ESGRiskRating`. The properties are EsgScore and EsgRiskRating,
        // following `cik -> Cik` and `growthEPS -> GrowthEps`. The attribute carries the wire spelling and
        // this test fails if either is "tidied" in the wrong direction.
        var disclosure = JsonSerializer.Deserialize(
            """[{"ESGScore":59.03}]""", FmpJsonContext.Default.ListEsgDisclosure)![0];
        var rating = JsonSerializer.Deserialize(
            """[{"ESGRiskRating":"B"}]""", FmpJsonContext.Default.ListEsgRating)![0];

        Assert.Equal(59.03m, disclosure.EsgScore);
        Assert.Equal("B", rating.EsgRiskRating);
    }

    [Fact]
    public void Industry_rank_is_a_sentence_and_not_a_number()
    {
        // The natural guess is int?, and it would throw on every row: the measured value is "3 out of 9".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-ratings.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgRating)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("3 out of 9", rows[0].IndustryRank);
        Assert.Equal("19 out of 21", rows[1].IndustryRank);
        Assert.Equal("1 out of 2", rows[2].IndustryRank);
    }

    [Fact]
    public void Ratings_are_not_returned_in_year_order()
    {
        // 1998, then 2025, then 1994. Captured as they arrived so nothing downstream assumes an ordering FMP
        // does not promise.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-ratings.AAPL.head.json"),
            FmpJsonContext.Default.ListEsgRating)!;

        Assert.Equal(new int?[] { 1998, 2025, 1994 }, rows.Select(r => r.FiscalYear));
    }

    [Fact]
    public void A_benchmark_row_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("esg-benchmark.2023.head.json"),
            FmpJsonContext.Default.ListEsgBenchmark)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(2023, rows[0].FiscalYear);
        Assert.Equal("Q2", rows[0].Period);
        Assert.Equal("APPAREL RETAIL", rows[0].Sector);
        Assert.Equal(65.63m, rows[0].EsgScore);

        // `period` is not always a quarter — row 2 is FY. A closed enum over it would be wrong twice over.
        Assert.Equal("FY", rows[2].Period);
    }

    [Fact]
    public async Task The_benchmark_never_sends_a_sector_parameter()
    {
        // THE trap of this facade's request surface. Measured 2026-08-29, `?sector=APPAREL RETAIL` came back
        // BYTE-IDENTICAL to the bare call — 1003 rows across 291 sectors. FMP accepts the parameter and
        // discards it, so offering one would promise filtering that never happens. The caller filters the
        // list on EsgBenchmark.Sector, which is on the record precisely because the field IS returned.
        var (endpoints, handler) = Build();

        await endpoints.GetBenchmarkAsync(2020);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/esg-benchmark", handler.Requests[0].AbsolutePath);
        Assert.Contains("year=2020", query);
        Assert.DoesNotContain("sector", query);
    }

    [Fact]
    public async Task The_benchmark_year_is_optional_and_omitted_rather_than_sent_empty()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetBenchmarkAsync();

        Assert.DoesNotContain("year=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (disclosures, disclosuresHandler) = Build();
        await disclosures.GetDisclosuresAsync("AAPL");

        var (ratings, ratingsHandler) = Build();
        await ratings.GetRatingsAsync("AAPL");

        Assert.Equal("/stable/esg-disclosures", disclosuresHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", disclosuresHandler.Requests[0].Query);
        Assert.Equal("/stable/esg-ratings", ratingsHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", ratingsHandler.Requests[0].Query);
    }

    [Fact]
    public async Task An_empty_answer_is_an_empty_list_rather_than_null_on_all_three_methods()
    {
        // Pins the "never null" half of each method's doc comment: GetDisclosuresAsync's and
        // GetRatingsAsync's "empty for a symbol FMP has not scored/rated, not an error", and
        // GetBenchmarkAsync's "empty for a year FMP has no benchmark for, not an error". Driven through a
        // bare `[]` response on each; a separate Build() per call since each stub handler answers one
        // request only.
        var (disclosures, _) = Build();
        var (ratings, _) = Build();
        var (benchmark, _) = Build();

        var disclosureRows = await disclosures.GetDisclosuresAsync("NOSUCH");
        var ratingRows = await ratings.GetRatingsAsync("NOSUCH");
        var benchmarkRows = await benchmark.GetBenchmarkAsync(1900);

        Assert.NotNull(disclosureRows);
        Assert.Empty(disclosureRows);
        Assert.NotNull(ratingRows);
        Assert.Empty(ratingRows);
        Assert.NotNull(benchmarkRows);
        Assert.Empty(benchmarkRows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_refused_before_the_request_goes_out(string? symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => endpoints.GetRatingsAsync(symbol!));

        Assert.Empty(handler.Requests);
    }
}
