using System.Net;
using System.Text;
using FmpDotNet.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The developer bulk cache (#11). Every assertion here is about not calling FMP twice, or about not
/// keeping something that should not be kept.</summary>
public sealed class FmpDeveloperBulkCacheHandlerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "fmpdotnet-cache-tests", Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Counts how many times the request actually reached "the network".</summary>
    private sealed class UpstreamHandler(string body, string mediaType = "text/csv",
        HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
                RequestMessage = req,
            });
        }
    }

    private HttpClient Client(UpstreamHandler upstream, string? directory)
    {
        var cache = new FmpDeveloperBulkCacheHandler(
            Options.Create(new FmpOptions { DeveloperBulkCacheDirectory = directory }),
            NullLogger<FmpDeveloperBulkCacheHandler>.Instance)
        { InnerHandler = upstream };

        return new HttpClient(cache)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private const string Url = "stable/profile-bulk?part=0&apikey=super-secret-key";
    private const string Csv = "symbol,sector\nAAA,Technology\n";

    [Fact]
    public async Task The_second_call_is_served_from_disk_and_never_reaches_the_upstream()
    {
        // The whole reason the milestone starts here. Bulk is throttled to a trickle and FMP's own error text
        // warns that repeated calls can get a key restricted, so iterating on a mapper must not mean re-fetching.
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);

        var first = await (await http.GetAsync(Url)).Content.ReadAsStringAsync();
        var second = await (await http.GetAsync(Url)).Content.ReadAsStringAsync();

        Assert.Equal(Csv, first);
        Assert.Equal(Csv, second);
        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task The_api_key_never_reaches_the_file_name()
    {
        // The key is in the query because that is how FMP authenticates, so a name derived from the raw URI would
        // put it on disk and into every log line quoting the path.
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);

        await http.GetAsync(Url);

        var names = Directory.GetFiles(_directory).Select(Path.GetFileName).ToList();
        Assert.NotEmpty(names);
        Assert.DoesNotContain(names, n => n!.Contains("super-secret-key"));
        Assert.Contains(names, n => n!.StartsWith("profile-bulk_"));   // still recognisable to a human
    }

    [Fact]
    public async Task Two_different_queries_do_not_share_an_entry()
    {
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);

        await http.GetAsync("stable/profile-bulk?part=0");
        await http.GetAsync("stable/profile-bulk?part=1");

        Assert.Equal(2, upstream.Sends);
    }

    [Fact]
    public async Task The_same_query_with_a_different_key_is_the_same_entry()
    {
        // Any key on the URI is stripped before the name is derived. The transport sends the key as a header, so
        // this is the caller-pasted `?apikey=` case — and rotating that key must not silently orphan the cache
        // and send every bulk call back to the upstream.
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);

        await http.GetAsync("stable/profile-bulk?part=0&apikey=one");
        await http.GetAsync("stable/profile-bulk?part=0&apikey=two");

        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task The_content_type_survives_the_round_trip()
    {
        // Load-bearing: the transport recognises an error envelope on the CSV path by its content type, so
        // replaying a CSV without its own type would change how the response is interpreted.
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);

        await http.GetAsync(Url);
        using var replayed = await http.GetAsync(Url);

        Assert.Equal("text/csv", replayed.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_json_error_payload_served_with_status_200_is_not_kept()
    {
        // FMP answers some failures with 200 and a JSON envelope. Caching one would replay the failure forever and
        // leave a developer debugging a mapper against an error document.
        var upstream = new UpstreamHandler("""{"Error Message":"Limit reached"}""", "application/json");
        using var http = Client(upstream, _directory);

        string body;
        using (var first = await http.GetAsync(Url))
            body = await first.Content.ReadAsStringAsync();
        (await http.GetAsync(Url)).Dispose();

        Assert.Contains("Limit reached", body);       // still delivered to the caller
        Assert.Equal(2, upstream.Sends);              // but fetched again, not replayed

        // Nothing survives. The rejected payload is served from a temporary file opened with DeleteOnClose, so it
        // is gone the moment the caller disposes the response — which is why the responses above are disposed
        // rather than left to the finalizer.
        Assert.Empty(Directory.Exists(_directory) ? Directory.GetFiles(_directory) : []);
    }

    [Fact]
    public async Task A_json_body_mislabelled_as_csv_is_still_recognised_and_not_kept()
    {
        // The 400 that ends the profile-bulk part walk arrives under a content-type of application/json that is a
        // lie; the reverse happens too, so the first byte is checked as well as the header.
        var upstream = new UpstreamHandler("""  {"Error Message":"nope"}""", "text/csv");
        using var http = Client(upstream, _directory);

        await http.GetAsync(Url);
        await http.GetAsync(Url);

        Assert.Equal(2, upstream.Sends);
    }

    [Fact]
    public async Task A_failed_response_is_not_kept()
    {
        var upstream = new UpstreamHandler("Query Error: Invalid or missing query parameter - part",
            "application/json", HttpStatusCode.BadRequest);
        using var http = Client(upstream, _directory);

        var first = await http.GetAsync(Url);
        await http.GetAsync(Url);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(2, upstream.Sends);
    }

    [Fact]
    public async Task An_empty_body_is_kept_because_an_empty_part_is_a_real_answer()
    {
        var upstream = new UpstreamHandler("");
        using var http = Client(upstream, _directory);

        await http.GetAsync(Url);
        await http.GetAsync(Url);

        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task It_is_inert_when_no_directory_is_configured()
    {
        // The default. Every call goes to FMP and nothing is written anywhere.
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, directory: null);

        await http.GetAsync(Url);
        await http.GetAsync(Url);

        Assert.Equal(2, upstream.Sends);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task A_truncated_partial_file_is_never_replayed_as_if_it_were_complete()
    {
        // The download is written to `.partial` and moved into place, so an interrupted fetch cannot leave a
        // half-file that every later run serves as the whole response.
        Directory.CreateDirectory(_directory);
        var upstream = new UpstreamHandler(Csv);
        using var http = Client(upstream, _directory);
        await http.GetAsync(Url);

        var body = Directory.GetFiles(_directory).Single(f => !f.EndsWith(".mediatype"));
        File.Move(body, body + ".partial");

        var refetched = await (await http.GetAsync(Url)).Content.ReadAsStringAsync();

        Assert.Equal(Csv, refetched);
        Assert.Equal(2, upstream.Sends);
    }
}
