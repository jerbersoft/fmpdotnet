using System.Text;
using FinancialModelingPrep.Serialization;

using NodaTime;

namespace FinancialModelingPrep.Tests;

public class CsvStreamReaderTests
{
    private static async Task<List<Dictionary<string, string?>>> ReadAsync(string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = new List<Dictionary<string, string?>>();
        await foreach (var row in CsvStreamReader.ReadAsync(stream))
            result.Add(row.Columns.ToDictionary(c => c.Key, c => row.GetString(c.Key)));
        return result;
    }

    [Fact]
    public async Task Reads_quoted_and_bare_fields_in_the_same_record()
    {
        // Measured shape: eod-bulk quotes strings and leaves numbers bare.
        var rows = await ReadAsync("\"symbol\",\"date\",\"close\"\n\"TJSEUR\",\"2025-10-22\",0.09346581\n");

        var row = Assert.Single(rows);
        Assert.Equal("TJSEUR", row["symbol"]);
        Assert.Equal("2025-10-22", row["date"]);
        Assert.Equal("0.09346581", row["close"]);
    }

    [Fact]
    public async Task Keeps_commas_inside_quoted_fields()
    {
        var rows = await ReadAsync("\"symbol\",\"name\"\n\"AAPL\",\"Apple, Inc.\"\n");

        Assert.Equal("Apple, Inc.", Assert.Single(rows)["name"]);
    }

    [Fact]
    public async Task Unescapes_doubled_quotes()
    {
        var rows = await ReadAsync("\"symbol\",\"name\"\n\"BRK\",\"The \"\"B\"\" shares\"\n");

        Assert.Equal("The \"B\" shares", Assert.Single(rows)["name"]);
    }

    [Fact]
    public async Task Keeps_newlines_inside_quoted_fields()
    {
        // A line-oriented reader splits this record in two; the state machine must not.
        var rows = await ReadAsync("\"symbol\",\"description\"\n\"AAPL\",\"line one\nline two\"\n");

        Assert.Single(rows);
        Assert.Equal("line one\nline two", rows[0]["description"]);
    }

    [Fact]
    public async Task Handles_crlf_and_lf_line_endings_alike()
    {
        var crlf = await ReadAsync("\"a\",\"b\"\r\n\"1\",\"2\"\r\n\"3\",\"4\"\r\n");
        var lf = await ReadAsync("\"a\",\"b\"\n\"1\",\"2\"\n\"3\",\"4\"\n");

        Assert.Equal(2, crlf.Count);
        Assert.Equal(2, lf.Count);
        Assert.Equal("2", crlf[0]["b"]);
        Assert.Equal("4", lf[1]["b"]);
    }

    [Fact]
    public async Task Trailing_newline_does_not_produce_a_phantom_record()
    {
        var rows = await ReadAsync("\"a\"\n\"1\"\n");

        Assert.Single(rows);
    }

    [Fact]
    public async Task Reads_a_final_record_with_no_trailing_newline()
    {
        var rows = await ReadAsync("\"a\",\"b\"\n\"1\",\"2\"");

        Assert.Equal("2", Assert.Single(rows)["b"]);
    }

    [Fact]
    public async Task Empty_stream_yields_nothing()
    {
        Assert.Empty(await ReadAsync(""));
    }

    [Fact]
    public async Task Header_only_stream_yields_nothing()
    {
        Assert.Empty(await ReadAsync("\"a\",\"b\"\n"));
    }

    [Fact]
    public async Task Survives_a_record_that_spans_the_read_buffer()
    {
        // The internal buffer is 64 KB; a field longer than that exercises the refill path mid-field.
        var big = new string('x', 200_000);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"\"a\",\"b\"\n\"1\",\"{big}\"\n"));

        var values = new List<string?>();
        await foreach (var row in CsvStreamReader.ReadAsync(stream)) values.Add(row.GetString("b"));

        Assert.Equal(big, Assert.Single(values));
    }

    [Fact]
    public async Task Column_lookup_is_case_insensitive()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\"adjClose\"\n\"1.5\"\n"));

        await foreach (var row in CsvStreamReader.ReadAsync(stream))
            Assert.Equal(1.5m, row.GetDecimal("ADJCLOSE"));
    }

    [Fact]
    public async Task Empty_and_absent_fields_both_read_as_null()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\"a\",\"b\"\n\"1\",\n"));

        await foreach (var row in CsvStreamReader.ReadAsync(stream))
        {
            Assert.Null(row.GetString("b"));
            Assert.Null(row.GetString("nosuchcolumn"));
            Assert.Null(row.GetDecimal("b"));
        }
    }

    [Fact]
    public async Task Parses_exponent_notation()
    {
        // Measured: crypto rows in eod-bulk carry values like 1.8646e-8.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\"open\"\n1.8646e-8\n"));

        await foreach (var row in CsvStreamReader.ReadAsync(stream))
            Assert.Equal(1.8646e-8, row.GetDouble("open")!.Value, 15);
    }

    [Fact]
    public async Task Instant_column_reads_fmp_timestamps_as_utc()
    {
        // FMP's "yyyy-MM-dd HH:mm:ss" is space-separated, not ISO-T, and the reading is UTC — established by the
        // DST shift across two measured rows, not assumed.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\"date\"\n\"2026-08-29 12:30:00\"\n"));

        await foreach (var row in CsvStreamReader.ReadAsync(stream))
            Assert.Equal(Instant.FromUtc(2026, 8, 29, 12, 30, 0), row.GetInstant("date"));
    }

    [Fact]
    public async Task Date_column_accepts_a_timestamp_and_takes_the_date_part()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\"date\"\n\"2026-07-21 14:21:00\"\n"));

        await foreach (var row in CsvStreamReader.ReadAsync(stream))
            Assert.Equal(new LocalDate(2026, 7, 21), row.GetDate("date"));
    }
}
