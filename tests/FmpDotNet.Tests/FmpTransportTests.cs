using System.Net;
using Microsoft.Extensions.Options;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

using NodaTime;

namespace FmpDotNet.Tests;

public class FmpTransportTests
{
    private static (FmpTransport Transport, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var options = Options.Create(new FmpOptions { ApiKey = "test-key" });
        return (new FmpTransport(http, options), handler);
    }

    [Fact]
    public async Task Appends_the_api_key_to_every_request()
    {
        var (transport, handler) = Build(StubHandler.Json("[]"));

        await transport.GetListAsync(new FmpRequest("stable/profile").With("symbol", "AAPL"),
            FmpJsonContext.Default.ListCompanyProfile);

        Assert.Contains("apikey=test-key", handler.Requests[0].Query);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Empty_array_becomes_an_empty_list_never_null()
    {
        var (transport, _) = Build(StubHandler.Json("[]"));

        var rows = await transport.GetListAsync(new FmpRequest("stable/profile"),
            FmpJsonContext.Default.ListCompanyProfile);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Reads_numbers_fmp_delivers_as_strings()
    {
        // Measured: stable quotes some numerics ("fiscalYear":"2026"), and profile quotes fullTimeEmployees.
        // Without AllowReadingFromString the first quoted number aborts the whole response, not just that field.
        var (transport, _) = Build(StubHandler.Json(
            """[{"symbol":"AAPL","marketCap":"4551611624400","fullTimeEmployees":"166000"}]"""));

        var rows = await transport.GetListAsync(new FmpRequest("stable/profile"),
            FmpJsonContext.Default.ListCompanyProfile);

        Assert.Equal(4551611624400m, rows[0].MarketCap);
        Assert.Equal("166000", rows[0].FullTimeEmployees);
    }

    [Fact]
    public async Task Status429_becomes_a_rate_limited_exception_carrying_the_advice()
    {
        var response = StubHandler.Json("", HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var (transport, _) = Build(response);

        var ex = await Assert.ThrowsAsync<FmpRateLimitedException>(() =>
            transport.GetListAsync(new FmpRequest("stable/profile"), FmpJsonContext.Default.ListCompanyProfile));

        Assert.Equal(Duration.FromSeconds(30), ex.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Plan_gating_is_an_exception_and_never_a_return_value(HttpStatusCode status)
    {
        // There is no TryGetListAsync half any more: C# forbids `out` on async methods, so the BCL's Try shape
        // cannot be expressed here, and the nullable-return imitation meant one signature carried two error
        // channels.
        var (getTransport, _) = Build(StubHandler.Status(status));
        await Assert.ThrowsAsync<FmpPlanRestrictedException>(() =>
            getTransport.GetListAsync(new FmpRequest("stable/profile-bulk"),
                FmpJsonContext.Default.ListCompanyProfile));
    }

    [Fact]
    public async Task Error_object_where_an_array_was_expected_becomes_an_api_exception()
    {
        var (transport, _) = Build(StubHandler.Json("""{"Error Message": "Invalid API KEY."}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() =>
            transport.GetListAsync(new FmpRequest("stable/profile"), FmpJsonContext.Default.ListCompanyProfile));

        Assert.Equal("Invalid API KEY.", ex.ErrorMessage);
    }

    [Fact]
    public async Task Error_message_never_leaks_the_api_key()
    {
        var (transport, _) = Build(StubHandler.Json("""{"Error Message": "Invalid API KEY."}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(() =>
            transport.GetListAsync(new FmpRequest("stable/profile").With("symbol", "AAPL"),
                FmpJsonContext.Default.ListCompanyProfile));

        Assert.DoesNotContain("test-key", ex.Message);
    }

    [Fact]
    public async Task Csv_streams_records_as_typed_rows()
    {
        var (transport, _) = Build(StubHandler.Csv(
            "\"symbol\",\"date\",\"open\",\"low\",\"high\",\"close\",\"adjClose\",\"volume\"\n"
            + "\"AAPL\",\"2025-10-22\",1.5,1.0,2.0,1.8,1.8,1000\n"
            + "\"MONKEUSD\",\"2025-10-22\",1.8646e-8,1.8646e-8,1.8646e-8,1.8646e-8,1.8646e-8,79\n"));

        var rows = new List<BulkEndOfDayPrice>();
        await foreach (var row in transport.StreamCsvAsync(
            new FmpRequest("stable/eod-bulk"), BulkEndOfDayPrice.FromCsv)) rows.Add(row);

        Assert.Equal(2, rows.Count);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(new LocalDate(2025, 10, 22), rows[0].Date);
        Assert.Equal(1000, rows[0].Volume);
        Assert.Equal(1.8646e-8, rows[1].Open!.Value, 15);
    }

    [Fact]
    public async Task Bulk_throttling_arrives_as_HTTP_200_with_a_json_body_and_is_still_an_error()
    {
        // Measured 2026-08-26. This is the single most dangerous FMP behaviour for a client to miss: a CSV
        // endpoint answering 200 with a JSON error. EnsureSuccessStatusCode passes and a naive CSV parse yields
        // zero rows, so the caller sees "no data today" instead of "you were throttled".
        var (transport, _) = Build(StubHandler.Json(
            """{"Error Message": "Limit Reach. This is a bulk endpoint that provides a significant amount of data"}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(async () =>
        {
            await foreach (var _ in transport.StreamCsvAsync(
                new FmpRequest("stable/eod-bulk"), BulkEndOfDayPrice.FromCsv)) { }
        });

        Assert.Contains("Limit Reach", ex.ErrorMessage);
    }

    [Fact]
    public async Task Json_error_body_is_caught_even_when_the_content_type_claims_csv()
    {
        // Belt and braces: the media type is upstream-controlled, so the first byte decides too.
        var (transport, _) = Build(StubHandler.Csv("""{"Error Message": "Limit Reach."}"""));

        await Assert.ThrowsAsync<FmpApiException>(async () =>
        {
            await foreach (var _ in transport.StreamCsvAsync(
                new FmpRequest("stable/eod-bulk"), BulkEndOfDayPrice.FromCsv)) { }
        });
    }

    [Fact]
    public async Task Csv_payload_larger_than_the_peek_window_still_parses_from_its_first_byte()
    {
        // The transport reads 256 bytes to classify the body, then replays them. A header shorter than that
        // window would be silently swallowed if the replay were wrong.
        var header = "\"symbol\",\"date\",\"open\",\"low\",\"high\",\"close\",\"adjClose\",\"volume\"\n";
        var body = string.Concat(Enumerable.Range(0, 50)
            .Select(i => $"\"SYM{i}\",\"2025-10-22\",1,1,1,1,1,{i}\n"));
        var (transport, _) = Build(StubHandler.Csv(header + body));

        var rows = new List<BulkEndOfDayPrice>();
        await foreach (var row in transport.StreamCsvAsync(
            new FmpRequest("stable/eod-bulk"), BulkEndOfDayPrice.FromCsv)) rows.Add(row);

        Assert.Equal(50, rows.Count);
        Assert.Equal("SYM0", rows[0].Symbol);
        Assert.Equal("SYM49", rows[49].Symbol);
        Assert.Equal(49, rows[49].Volume);
    }

    [Fact]
    public async Task GetObjectAsync_reads_a_json_object_body()
    {
        var (transport, handler) = Build(StubHandler.Json("""{"symbol":"AAPL","period":"FY","year":"2025"}"""));

        var report = await transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport);

        Assert.NotNull(report);
        Assert.Equal("AAPL", report.Symbol);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetObjectAsync_raises_an_error_envelope_that_arrived_as_a_200()
    {
        // The reason this method cannot use GetListAsync's first-byte test: here BOTH the success shape and the
        // error shape are JSON objects. Measured 2026-08-27, a miss on financial-reports-json is HTTP 200
        // carrying {"Error Message": "No Data for this symbol or invalid API call…"}.
        var (transport, _) = Build(StubHandler.Json(
            """{"Error Message":"No Data for this symbol or invalid API call."}"""));

        var ex = await Assert.ThrowsAsync<FmpApiException>(
            () => transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport));

        Assert.Contains("No Data for this symbol", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetObjectAsync_raises_a_plan_restriction_before_reading_the_body()
    {
        var (transport, _) = Build(StubHandler.Status(HttpStatusCode.PaymentRequired));

        await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => transport.GetObjectAsync(new FmpRequest("stable/x"), FmpJsonContext.Default.FinancialReport));
    }

    [Fact]
    public async Task GetBytesAsync_returns_the_body_verbatim_without_looking_at_it()
    {
        // Deliberately not JSON and deliberately not a zip. The transport does not classify a binary body — the
        // endpoint that knows what it asked for does.
        byte[] payload = [0x50, 0x4B, 0x03, 0x04, 0xFF, 0x00, 0x41];
        var (transport, _) = Build(new HttpResponseMessage(HttpStatusCode.OK)
        {
            // The content type FMP actually sends for a workbook, which is a lie, and is ignored here.
            Content = new ByteArrayContent(payload)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
            },
        });

        Assert.Equal(payload, await transport.GetBytesAsync(new FmpRequest("stable/x")));
    }

    [Fact]
    public async Task GetBytesAsync_still_raises_a_failure_status()
    {
        var (transport, _) = Build(StubHandler.Status(HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<FmpApiException>(() => transport.GetBytesAsync(new FmpRequest("stable/x")));
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetBytesAsync_raises_a_plan_restriction_before_reading_the_body(HttpStatusCode status)
    {
        var (transport, _) = Build(StubHandler.Status(status));

        await Assert.ThrowsAsync<FmpPlanRestrictedException>(() => transport.GetBytesAsync(new FmpRequest("stable/x")));
    }
}
