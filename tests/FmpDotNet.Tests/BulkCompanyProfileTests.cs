using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/profile-bulk</c>, checked against a response captured live from FMP on 2026-08-26.
///
/// <para>The fixture is the first three records of <c>part=0</c> with its header intact — the whole part was
/// 30,467,596 bytes over 22,857 lines, sent chunked with no <c>Content-Length</c>. PRTA carries an empty
/// <c>cusip</c>, MRV.TO an empty <c>cik</c> and <c>phone</c>, and both PRTA and PRDO a fractional
/// <c>volume</c>, so the three rows between them exercise every mapping decision the model makes.</para>
///
/// <para><b>Nothing here asserts that the endpoint is available.</b> Plan gating on this path is not settled — the
/// predecessor recorded 402 on Premium and it answered 200 on 2026-08-26 — so the tests pin how each answer is
/// handled and never which answer arrives.</para></summary>
public class BulkCompanyProfileTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A fresh endpoint per call. <see cref="FmpTransport"/> disposes the response once it has been read,
    /// so a canned response cannot serve two requests — the second fails with an
    /// <see cref="ObjectDisposedException"/> pointing at the stream rather than at the lifetime that ended it.</summary>
    private static (BulkEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var options = Options.Create(new FmpOptions { ApiKey = "k" });
        return (new BulkEndpoints(new FmpBulkTransport(http, options)), handler);
    }

    private static async Task<List<BulkCompanyProfile>> DrainAsync(IAsyncEnumerable<BulkCompanyProfile> rows)
    {
        var drained = new List<BulkCompanyProfile>();
        await foreach (var row in rows) drained.Add(row);
        return drained;
    }

    // ---- the wire shape --------------------------------------------------------------------------------------

    [Fact]
    public void Header_carries_thirty_six_columns_in_the_captured_order()
    {
        // The count is the point. An earlier written record of this endpoint enumerated 28 columns by stopping at
        // "state"; the capture has eight more after it — zip, image, ipoDate, defaultImage, isEtf,
        // isActivelyTrading, isAdr, isFund — on every row. If FMP adds, drops or reorders a column this turns red,
        // which is the only warning a CSV endpoint gives: there is no schema and no version.
        string[] expected =
        [
            "symbol", "price", "marketCap", "beta", "lastDividend", "range", "change", "changePercentage",
            "volume", "averageVolume", "companyName", "currency", "cik", "isin", "cusip", "exchangeFullName",
            "exchange", "industry", "website", "description", "ceo", "sector", "country", "fullTimeEmployees",
            "phone", "address", "city", "state", "zip", "image", "ipoDate", "defaultImage", "isEtf",
            "isActivelyTrading", "isAdr", "isFund",
        ];

        var header = Fixture("profile-bulk.part0.head.csv").Split('\n')[0].Trim()
            .Split(',').Select(c => c.Trim('"')).ToArray();

        Assert.Equal(36, expected.Length);
        Assert.Equal(expected, header);
    }

    [Fact]
    public async Task Maps_every_column_of_the_captured_prta_row()
    {
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("profile-bulk.part0.head.csv")));

        var row = (await DrainAsync(endpoints.StreamProfilesAsync(0)))[0];

        Assert.Equal("PRTA", row.Symbol);
        Assert.Equal(9.18m, row.Price);
        Assert.Equal(480_602_716m, row.MarketCap);
        Assert.Equal(-0.354m, row.Beta);
        Assert.Equal(0m, row.LastDividend);
        Assert.Equal("7.73-11.8", row.Range);
        Assert.Equal(-0.24m, row.Change);
        Assert.Equal(-2.54777m, row.ChangePercentage);
        Assert.Equal(73305.59636m, row.Volume);
        Assert.Equal(524_492m, row.AverageVolume);
        Assert.Equal("Prothena Corporation plc", row.CompanyName);
        Assert.Equal("USD", row.Currency);
        Assert.Equal("0001559053", row.Cik);
        Assert.Equal("IE00B91XRN20", row.Isin);
        Assert.Null(row.Cusip);
        Assert.Equal("NASDAQ", row.ExchangeFullName);
        Assert.Equal("NASDAQ", row.Exchange);
        Assert.Equal("Biotechnology", row.Industry);
        Assert.Equal("https://www.prothena.com", row.Website);
        Assert.StartsWith("Prothena Corporation plc, a late-stage clinical biotechnology company", row.Description);
        Assert.Equal("Gene G. Kinney", row.Ceo);
        Assert.Equal("Healthcare", row.Sector);
        Assert.Equal("IE", row.Country);
        Assert.Equal("67", row.FullTimeEmployees);
        Assert.Equal("353 1 236 2500", row.Phone);
        Assert.Equal("77 Sir John Rogerson’s Quay", row.Address);
        Assert.Equal("Dublin", row.City);
        Assert.Equal("DU", row.State);
        Assert.Equal("D02 VK60", row.Zip);
        Assert.Equal("https://images.financialmodelingprep.com/symbol/PRTA.png", row.Image);
        Assert.Equal(new LocalDate(2012, 12, 20), row.IpoDate);
        Assert.False(row.DefaultImage);
        Assert.False(row.IsEtf);
        Assert.True(row.IsActivelyTrading);
        Assert.False(row.IsAdr);
        Assert.False(row.IsFund);
    }

    [Fact]
    public async Task Range_is_a_string_and_not_a_number()
    {
        // "7.73-11.8" is not parseable as a number, and a parser lenient enough to try would read the low bound
        // and silently drop the high one. Three measured spellings, one of them with an integer low bound.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("profile-bulk.part0.head.csv")));

        var rows = await DrainAsync(endpoints.StreamProfilesAsync(0));

        Assert.Equal("7.73-11.8", rows[0].Range);
        Assert.Equal("26.66-38.5", rows[1].Range);
        Assert.Equal("26-53.75", rows[2].Range);   // an integer low bound is still not a number
    }

    [Fact]
    public async Task Volume_is_decimal_because_the_bulk_column_is_an_average_not_a_session_count()
    {
        // Measured 2026-08-26: PRTA 73305.59636 and PRDO 60854.19398 are fractional, MRV.TO 37760 is not. A long?
        // mapping would truncate the first two and read clean doing it. This is the one field whose type differs
        // from the per-symbol CompanyProfile, and it is why the two are separate models.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("profile-bulk.part0.head.csv")));

        var rows = await DrainAsync(endpoints.StreamProfilesAsync(0));

        Assert.Equal(73305.59636m, rows[0].Volume);
        Assert.Equal(60854.19398m, rows[1].Volume);
        Assert.Equal(37760m, rows[2].Volume);
        Assert.NotEqual(decimal.Truncate(rows[0].Volume!.Value), rows[0].Volume);
    }

    [Fact]
    public async Task Empty_fields_read_as_null_rather_than_as_empty_strings()
    {
        // CSV has no way to say "absent" — an empty field is the only spelling of no-value. cusip is empty on
        // PRTA; cik and phone are empty on MRV.TO. An empty string reaching a database as an identifier is a
        // phantom key that matches nothing and is invisible in a diff.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("profile-bulk.part0.head.csv")));

        var rows = await DrainAsync(endpoints.StreamProfilesAsync(0));

        Assert.Null(rows[0].Cusip);
        Assert.Null(rows[2].Cik);
        Assert.Null(rows[2].Phone);
        Assert.Equal("71363P106", rows[1].Cusip);   // and the same column populated on the row between them
    }

    [Fact]
    public async Task Quoted_and_bare_fields_mix_inside_one_record()
    {
        // "MRV.TO",39,34154874000,0,0.01,"26-53.75",... — the company name also carries a comma inside its quotes,
        // and the description carries apostrophes and typographic quotes.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("profile-bulk.part0.head.csv")));

        var row = (await DrainAsync(endpoints.StreamProfilesAsync(0)))[2];

        Assert.Equal("MRV.TO", row.Symbol);
        Assert.Equal("Marvell Technology, Inc.", row.CompanyName);
        Assert.Equal(39m, row.Price);
        Assert.Equal("CAD", row.Currency);
        Assert.True(row.DefaultImage);
        Assert.True(row.IsAdr);
        Assert.Equal(new LocalDate(2026, 5, 7), row.IpoDate);
    }

    // ---- streaming -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Yields_the_first_row_without_reading_the_whole_body()
    {
        // The measured part is 30,467,596 bytes with NO Content-Length, so buffering is not a size trade-off — it
        // cannot be bounded at all. This pins that the first row arrives after a bounded read rather than after the
        // body ends. The transport peeks 256 bytes to classify the payload and the CSV reader then fills a 64 KB
        // buffer, so the ceiling is those two and not the payload; a 1 MB body must be barely touched by the time
        // row one is in hand. Measured here: 256 bytes, the peek alone.
        var header = "\"symbol\",\"range\",\"volume\"\n";
        var body = new StringBuilder(header);
        for (var i = 0; body.Length < 1_000_000; i++)
            body.Append($"\"SYM{i}\",\"1-2\",{i}.5\n");
        var counting = new CountingStream(new MemoryStream(Encoding.UTF8.GetBytes(body.ToString())));
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(counting) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        var (endpoints, _) = Build(response);

        await using var rows = endpoints.StreamProfilesAsync(0).GetAsyncEnumerator();
        Assert.True(await rows.MoveNextAsync());

        Assert.Equal("SYM0", rows.Current.Symbol);
        Assert.True(counting.BytesRead is > 0 and <= 64 * 1024 + 256,
            $"read {counting.BytesRead} of {body.Length} bytes to produce one row");
    }

    /// <summary>Counts what an enumeration actually pulls off the wire, so "it streams" can be asserted rather
    /// than assumed.</summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = await inner.ReadAsync(buffer, cancellationToken);
            BytesRead += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            BytesRead += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ---- the request ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Hits_its_own_path_carrying_the_part()
    {
        var (endpoints, handler) = Build(StubHandler.Csv("\"symbol\"\n"));

        await DrainAsync(endpoints.StreamProfilesAsync(7));

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/profile-bulk", uri.AbsolutePath);
        Assert.Equal("?part=7", uri.Query);
    }

    [Fact]
    public void Rejects_a_negative_part_before_spending_a_bulk_request()
    {
        // The bulk throttle refuses calls made moments apart, so a request wasted on an argument the SDK could
        // have rejected costs the next real one. Validation is eager rather than deferred to the first
        // MoveNextAsync for the same reason.
        var (endpoints, handler) = Build(StubHandler.Csv("\"symbol\"\n"));

        Assert.Throws<ArgumentOutOfRangeException>(() => endpoints.StreamProfilesAsync(-1));
        Assert.Empty(handler.Requests);
    }

    // ---- paging termination -----------------------------------------------------------------------------------

    [Fact]
    public async Task Walking_all_parts_stops_at_the_400_that_says_the_part_does_not_exist()
    {
        // Measured 2026-08-26: part=0 and part=1 answer 200 with data, part=99 answers HTTP 400 with the plain
        // text body below. There is no empty-response terminator and no part count to ask for, so the 400 IS the
        // end of the walk. Note the response's declared content-type: FMP says application/json over a body that
        // is not JSON, exactly as measured.
        var (endpoints, handler) = Build(
            StubHandler.Csv("\"symbol\",\"range\"\n\"AAA\",\"1-2\"\n"),
            StubHandler.Csv("\"symbol\",\"range\"\n\"BBB\",\"3-4\"\n"),
            StubHandler.Json("Query Error: Invalid or missing query parameter - part", HttpStatusCode.BadRequest));

        var rows = await DrainAsync(endpoints.StreamAllProfilesAsync());

        Assert.Equal(new[] { "AAA", "BBB" }, rows.Select(r => r.Symbol).ToArray());
        Assert.Equal(new[] { "?part=0", "?part=1", "?part=2" },
            handler.Requests.Select(u => u.Query).ToArray());
    }

    [Fact]
    public async Task A_400_on_part_zero_is_an_error_and_not_an_empty_universe()
    {
        // The termination rule is a heuristic: a 400 saying "invalid or missing query parameter" could equally
        // mean the parameter was malformed, and the walk is only entitled to read it as "past the last part"
        // because it controls the integer it sent. Part 0 was measured to exist, so a 400 there cannot mean the
        // parts ran out — it means the request shape changed, and swallowing it would report an empty universe.
        var (endpoints, _) = Build(
            StubHandler.Json("Query Error: Invalid or missing query parameter - part", HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamAllProfilesAsync()));

        Assert.Equal("Query Error: Invalid or missing query parameter - part", ex.ErrorMessage);
    }

    [Fact]
    public async Task A_part_with_no_data_rows_also_ends_the_walk()
    {
        // Nothing measured behaves this way. The guard exists so that an upstream which starts answering
        // 200-with-header-only cannot spin the walk into an unbounded loop against an endpoint whose throttle is
        // measured in calls per hour.
        var (endpoints, handler) = Build(
            StubHandler.Csv("\"symbol\",\"range\"\n\"AAA\",\"1-2\"\n"),
            StubHandler.Csv("\"symbol\",\"range\"\n"));

        var rows = await DrainAsync(endpoints.StreamAllProfilesAsync());

        Assert.Equal("AAA", Assert.Single(rows).Symbol);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task The_bulk_throttle_stops_the_walk_by_throwing_rather_than_ending_it_quietly()
    {
        // The throttle arrives as HTTP 200 with a JSON error body, so FmpApiException.StatusCode is null where the
        // out-of-range part's is 400. Only the 400 terminates; anything else must surface, or a throttled walk
        // would return a partial universe that looks complete.
        var (endpoints, _) = Build(
            StubHandler.Csv("\"symbol\",\"range\"\n\"AAA\",\"1-2\"\n"),
            StubHandler.Json("""{"Error Message": "Limit Reach. This is a bulk endpoint"}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamAllProfilesAsync()));

        Assert.Contains("Limit Reach", ex.ErrorMessage);
        Assert.Null(ex.StatusCode);
    }

    // ---- plan gating ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Plan_gating_raises_the_typed_exception_and_never_reads_as_an_empty_universe(
        HttpStatusCode status)
    {
        // Gating here is not settled in either direction — the predecessor recorded 402 on Premium and the
        // endpoint answered 200 on 2026-08-26 — so this pins the HANDLING, not the answer.
        //
        // Why an exception rather than the null that TryGetAllSharesFloatAsync returns: a stream has no null, and
        // an empty stream is indistinguishable from a genuinely empty universe. "A paywalled endpoint reading as
        // an empty result" is the defect this asymmetry exists to avoid.
        var (endpoints, _) = Build(StubHandler.Status(status));

        await Assert.ThrowsAsync<FmpPlanRestrictedException>(() => DrainAsync(endpoints.StreamProfilesAsync(0)));
    }

    [Fact]
    public async Task Plan_gating_stops_the_all_parts_walk_too()
    {
        var (endpoints, _) = Build(StubHandler.Status(HttpStatusCode.PaymentRequired));

        await Assert.ThrowsAsync<FmpPlanRestrictedException>(() => DrainAsync(endpoints.StreamAllProfilesAsync()));
    }

    // ---- the error body ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_400_surfaces_the_plain_text_body_that_says_what_went_wrong()
    {
        // Before this, EnsureSuccessStatusCode() raised a bare HttpRequestException naming only the status — the
        // one thing the caller could already see — and discarded the only sentence that explained it. The body is
        // plain text under a content-type of application/json, so no JSON envelope can be unwrapped from it.
        var (endpoints, _) = Build(
            StubHandler.Json("Query Error: Invalid or missing query parameter - part", HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamProfilesAsync(99)));

        Assert.Equal("Query Error: Invalid or missing query parameter - part", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("stable/profile-bulk?part=99", ex.Message);
    }

    [Fact]
    public async Task The_400_message_names_the_request_without_the_api_key()
    {
        // The key is a header, so no rendering of the request carries it — but a message that quoted the
        // header collection, or a caller-pasted `?apikey=` path, would. Pinned on the message and the string.
        var handler = new StubHandler(
            StubHandler.Json("Query Error: Invalid or missing query parameter - part", HttpStatusCode.BadRequest));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new BulkEndpoints(new FmpBulkTransport(
            http, Options.Create(new FmpOptions { ApiKey = "super-secret-key" })));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamProfilesAsync(99)));

        Assert.DoesNotContain("super-secret-key", ex.Message);
        Assert.DoesNotContain("apikey", ex.Message);
        Assert.DoesNotContain("super-secret-key", ex.ToString());
    }

    [Fact]
    public async Task A_huge_error_body_is_truncated_rather_than_carried_into_the_message()
    {
        // A failing bulk request can answer megabytes. An exception message is the last place that should be
        // materialised, so the read is bounded and the message capped.
        var (endpoints, _) = Build(StubHandler.Json(new string('x', 200_000), HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamProfilesAsync(0)));

        Assert.True(ex.ErrorMessage.Length < 1_000, $"error message was {ex.ErrorMessage.Length} chars");
        Assert.StartsWith("xxxx", ex.ErrorMessage);
    }

    [Fact]
    public async Task A_non_success_with_no_body_still_names_the_status()
    {
        var (endpoints, _) = Build(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(endpoints.StreamProfilesAsync(0)));

        Assert.Contains("500", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }
}
