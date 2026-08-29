using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet;

/// <summary>The single place that turns an <see cref="FmpRequest"/> into typed results.
///
/// <para>Two pipelines, deliberately kept apart because the upstream keeps them apart:</para>
/// <list type="bullet">
/// <item><description>ordinary endpoints answer a JSON array and materialise as
/// <see cref="IReadOnlyList{T}"/> — small, indexable, and what every caller does with them anyway;</description></item>
/// <item><description><c>*-bulk</c> endpoints answer CSV that runs to tens of megabytes and stream as
/// <see cref="IAsyncEnumerable{T}"/>, which is the only shape that keeps the working set flat.</description></item>
/// </list>
///
/// <para>Exposed publicly on purpose: the SDK types a fraction of FMP's 263 documented endpoints, and a caller who
/// needs one that is not yet modelled should have a supported way to reach it rather than a reason to build a
/// second HttpClient without the throttle.</para></summary>
public class FmpTransport(HttpClient http, IOptions<FmpOptions> options)
{
    private readonly FmpOptions _options = options.Value;

    /// <summary>GETs a JSON array and deserialises it through a source-generated
    /// <see cref="JsonTypeInfo{T}"/>. An empty or null body yields an empty list, never null, so callers do not
    /// branch on "no rows" twice.
    ///
    /// <para><b>Every failure is an exception, and null is never one of them.</b> There is no Try-prefixed twin:
    /// C# forbids <c>out</c> parameters on async methods (CS1988), so the BCL's <c>bool TryX(out T)</c> shape
    /// cannot be expressed here at all — which is why there is no <c>TryReadAsync</c> anywhere in the framework
    /// either. Where the BCL does offer both, as <see cref="System.Threading.Channels.ChannelReader{T}"/> does,
    /// the <c>Try</c> form is the synchronous one and the async form throws. This follows that.</para>
    ///
    /// <para>An earlier version returned null on 402/403 so an optional fast path could degrade in one branch.
    /// That put two error channels on one surface and overloaded a nullable return with a meaning its signature
    /// could not carry — a caller had to read the docs to learn that null meant "refused" rather than "nothing
    /// there". A caller that wants to degrade catches <see cref="FmpPlanRestrictedException"/>, which is
    /// self-describing at the catch site and lets them tell 402 from 403.</para></summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    /// <exception cref="FmpApiException">FMP reported an error — either in the body of a 200, or on a non-success
    /// status, whose body text becomes <see cref="FmpApiException.ErrorMessage"/> and whose status becomes
    /// <see cref="FmpApiException.StatusCode"/>.</exception>
    public Task<IReadOnlyList<T>> GetListAsync<T>(
        FmpRequest request, JsonTypeInfo<List<T>> typeInfo, CancellationToken ct = default)
        => ReadListAsync(request, (body, token) => JsonSerializer.DeserializeAsync(body, typeInfo, token), ct);

    /// <summary>Sends the request, classifies the body, and deserialises it — all while the response is still
    /// alive.
    ///
    /// <para>The response must not be disposed before the body has been read: disposing it closes the content
    /// stream, and the read then fails with an <see cref="ObjectDisposedException"/> that points at the stream
    /// rather than at the lifetime that ended it.</para></summary>
    private async Task<IReadOnlyList<T>> ReadListAsync<T>(
        FmpRequest request,
        Func<Stream, CancellationToken, ValueTask<List<T>?>> deserialise,
        CancellationToken ct)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        var raw = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var (prefix, prefixLength, body) = await PeekAsync(raw, ct).ConfigureAwait(false);
        await using var _ = body.ConfigureAwait(false);

        // An error envelope is a JSON OBJECT where success is a JSON ARRAY, so the first non-space byte separates
        // them without parsing either.
        var first = FirstMeaningfulByte(prefix, prefixLength);
        if (first == (byte)'{')
            throw await ReadErrorAsync(body, request, ct).ConfigureAwait(false);

        // A 200 whose body is not JSON at all. Measured 2026-08-29, `stable/economic-indicators` answers an
        // unrecognised `name` with HTTP 200, `content-type: application/json; charset=utf-8`, and twelve
        // bytes of `Invalid name`. The check above cannot catch it: the first meaningful byte is `I`, neither
        // `{` nor the start of an array. Without this the caller gets a raw JsonException naming the byte
        // offset and nothing else — not the request, not what FMP said. GetObjectAsync has had this guard
        // since #21 and this pipeline had not; they were divergent by accident rather than by decision.
        // `stable/financial-reports-xlsx` answers a MISS the same way, with `Error with query`.
        //
        // The filter is what keeps the guard honest, and it is not optional. `deserialise` both PARSES and
        // BINDS, so an unfiltered catch would also swallow a well-formed array whose field is the wrong type
        // — and report it as "not JSON", which is false, and blame FMP for what is a defect in THIS SDK's
        // model. Several models document that throw as the outcome they want: "a non-numeric segment revenue
        // would be a defect worth hearing about, so the decimal dictionary is the right type and this throw
        // is the right outcome" (AsReportedTests), and CompanyMarketCap, PriceTarget and the directory lists
        // all record the same. FmpApiException has no inner-exception constructor, so wrapping would lose the
        // distinction outright. GetObjectAsync's guard makes the same cut for the same reason — it wraps
        // JsonDocument.ParseAsync and leaves RootElement.Deserialize alone. Here the peeked prefix draws the
        // line for free: a body that begins `[` is JSON, and a JsonException out of one is ours, not FMP's.
        try
        {
            return await deserialise(body, ct).ConfigureAwait(false) ?? [];
        }
        catch (JsonException ex) when (first != (byte)'[')
        {
            throw new FmpApiException(
                $"FMP answered a body that is not JSON: {ex.Message}", request.ToString());
        }
    }

    /// <summary>GETs a JSON <b>object</b> and deserialises it through a source-generated
    /// <see cref="JsonTypeInfo{T}"/>. Null only when FMP sent a literal JSON <c>null</c>.
    ///
    /// <para><b>Separate from <see cref="GetListAsync"/> because the error test is different, not because the
    /// shape is.</b> That method tells success from failure by the first meaningful byte: success is a JSON
    /// array and an FMP error envelope is a JSON object, so one byte separates them without parsing either. Here
    /// both are objects — measured 2026-08-27, a miss on <c>stable/financial-reports-json</c> answers HTTP 200
    /// carrying <c>{"Error Message": …}</c>, and a hit answers HTTP 200 carrying a 73-key document. No prefix
    /// distinguishes them.</para>
    ///
    /// <para>So the body is buffered into a <see cref="JsonDocument"/> and its root is offered to the same
    /// error-envelope check the rest of the transport uses. <b>Buffering is a real cost</b> — the measured report
    /// is 558 KB — and it is accepted because the alternative is guessing.</para></summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error — in the body of a 200, on a non-success status,
    /// or in a body that is not valid JSON at all.</exception>
    public async Task<T?> GetObjectAsync<T>(
        FmpRequest request, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var _ = body.ConfigureAwait(false);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new FmpApiException(
                $"FMP answered a body that is not JSON: {ex.Message}", request.ToString());
        }

        using (document)
        {
            if (ErrorTextFrom(document.RootElement) is { } message)
                throw new FmpApiException(message, request.ToString());
            return document.RootElement.Deserialize(typeInfo);
        }
    }

    /// <summary>GETs a body and hands back its bytes, unexamined.
    ///
    /// <para><b>It must not go near a JSON reader, and it deliberately does not classify what arrived.</b>
    /// <c>stable/financial-reports-xlsx</c> answers an XLSX zip under
    /// <c>Content-Type: application/json; charset=utf-8</c> — measured 2026-08-27, 1,399,564 bytes beginning
    /// <c>PK\x03\x04</c> — and answers a MISS the same way: HTTP 200, the same content type, and 16 bytes of
    /// <c>Error with query</c>. Neither the status nor the header separates them, so the only reliable test is
    /// the magic number, and that test belongs to the endpoint that knows it asked for a workbook rather than to
    /// a transport that would be guessing on one path's behalf.</para>
    ///
    /// <para>The whole body is buffered, because bytes are what the caller asked for. Non-success statuses still
    /// raise, so a 402, a 429 or a 400 behaves as it does everywhere else.</para></summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP answered a non-success status.</exception>
    public async Task<byte[]> GetBytesAsync(FmpRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>GETs a CSV payload and streams it, mapping each record as it arrives. The response is never
    /// buffered whole.</summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error — in the body on a 200, which is how the bulk
    /// surface reports throttling, or on a non-success status, whose body text becomes
    /// <see cref="FmpApiException.ErrorMessage"/> and whose status becomes
    /// <see cref="FmpApiException.StatusCode"/>.</exception>
    public async IAsyncEnumerable<T> StreamCsvAsync<T>(
        FmpRequest request, Func<CsvRow, T> map, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw FmpPlanRestrictedException.For(response.StatusCode, request);
        if (!response.IsSuccessStatusCode)
            throw await ReadFailureAsync(response, request, ct).ConfigureAwait(false);

        var raw = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var (prefix, prefixLength, body) = await PeekAsync(raw, ct).ConfigureAwait(false);
        await using var _ = body.ConfigureAwait(false);

        // The bulk surface reports throttling as HTTP 200 carrying {"Error Message": "Limit Reach. ..."} — JSON,
        // on an endpoint whose success shape is CSV (measured 2026-08-26). Status code alone cannot tell these
        // apart, so both the declared media type and the first byte are checked.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is "application/json" || FirstMeaningfulByte(prefix, prefixLength) == (byte)'{')
            throw await ReadErrorAsync(body, request, ct).ConfigureAwait(false);

        await foreach (var row in CsvStreamReader.ReadAsync(body, ct).ConfigureAwait(false))
            yield return map(row);
    }

    private async Task<HttpResponseMessage> SendAsync(
        FmpRequest request, HttpCompletionOption completion, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Build(_options.ApiKey));
        var response = await http.SendAsync(message, completion, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;

        var advised = response.Headers.RetryAfter?.Delta is { } delta ? Duration.FromTimeSpan(delta) : (Duration?)null;
        response.Dispose();
        // The rate-limit handler has already drained the shared reservoir by the time this is raised, so a caller
        // that re-queues the work meets back-pressure rather than the limit that just refused it.
        throw new FmpRateLimitedException($"FMP answered 429 for '{request}'.", advised);
    }

    /// <summary>Reads enough of the body to classify it, then hands back a stream that replays what was read.</summary>
    private static async Task<(byte[] Prefix, int Length, Stream Body)> PeekAsync(Stream stream, CancellationToken ct)
    {
        var prefix = new byte[256];
        var read = 0;
        while (read < prefix.Length)
        {
            var n = await stream.ReadAsync(prefix.AsMemory(read), ct).ConfigureAwait(false);
            if (n == 0) break;
            read += n;
        }
        return (prefix, read, new PrefixedStream(prefix, read, stream));
    }

    private static byte FirstMeaningfulByte(byte[] prefix, int length)
    {
        for (var i = 0; i < length; i++)
            if (prefix[i] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF))
                return prefix[i];
        return 0;
    }

    private static async Task<FmpApiException> ReadErrorAsync(Stream body, FmpRequest request, CancellationToken ct)
    {
        string? message = null;
        try
        {
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);
            message = ErrorTextFrom(document.RootElement);
        }
        catch (JsonException)
        {
            // Fall through — an unparseable body is still an error, just an undescribed one.
        }
        return new FmpApiException(message ?? "FMP returned an error payload with no message.", request.ToString());
    }

    /// <summary>Turns a non-success response into an exception that still carries what the body said.
    ///
    /// <para>The status line alone is not enough. Measured 2026-08-26, <c>stable/profile-bulk?part=99</c> answers
    /// HTTP 400 with the body <c>Query Error: Invalid or missing query parameter - part</c> — plain text, under a
    /// <c>content-type: application/json</c> that is a lie. <c>EnsureSuccessStatusCode()</c> threw that text away
    /// and left a bare <see cref="HttpRequestException"/> naming only the status, which is the one thing the caller
    /// could already see.</para>
    ///
    /// <para>A JSON envelope is still unwrapped when the body happens to be one, so a 4xx carrying
    /// <c>{"Error Message": …}</c> reads the same as the 200 that carries it. Otherwise the text is used as-is.
    /// The read is bounded at <see cref="MaxErrorBodyBytes"/> and the message capped at
    /// <see cref="MaxErrorMessageChars"/>: a failing bulk request can answer megabytes, and an exception message is
    /// the last place that should be materialised. The remainder is discarded with the response.</para></summary>
    private static async Task<FmpApiException> ReadFailureAsync(
        HttpResponseMessage response, FmpRequest request, CancellationToken ct)
    {
        var status = response.StatusCode;
        string? text = null;
        try
        {
            text = await ReadBoundedTextAsync(response.Content, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            // A body that fails mid-read is still a failure. Report the status FMP already sent rather than
            // replacing it with the secondary fault of not being able to read the explanation.
        }

        string? message = null;
        if (!string.IsNullOrEmpty(text))
        {
            if (text[0] == '{')
                try
                {
                    using var document = JsonDocument.Parse(text);
                    message = ErrorTextFrom(document.RootElement);
                }
                catch (JsonException)
                {
                    // Not JSON after all, or truncated by the cap — the raw text below is still the best evidence.
                }
            // A JSON ARRAY body carries no explanation — it is the SUCCESS shape, arriving on a failure. Measured
            // 2026-08-26, `stable/company-symbol-list` — the path that reads like the right name for the symbol
            // directory and is not one — answers HTTP 404 with the body `[]`. Passing that through produced
            // `FmpApiException: []`, a message naming neither the status nor the path, for what is really "you
            // asked for a path that does not exist". The status fallback below says both.
            message ??= text[0] == '[' ? null : text;
        }

        return new FmpApiException(
            message ?? (text is null or ""
                ? $"FMP answered HTTP {(int)status} ({status}) with no body."
                : $"FMP answered HTTP {(int)status} ({status}) with no explanation in the body."),
            request.ToString(), status);
    }

    /// <summary>How much of a failing body is read before the rest is abandoned.</summary>
    private const int MaxErrorBodyBytes = 8 * 1024;

    /// <summary>How much of that survives into the exception message.</summary>
    private const int MaxErrorMessageChars = 500;

    private static async Task<string> ReadBoundedTextAsync(HttpContent content, CancellationToken ct)
    {
        var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[MaxErrorBodyBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct).ConfigureAwait(false);
            if (n == 0) break;
            read += n;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
        return text.Length <= MaxErrorMessageChars ? text : string.Concat(text.AsSpan(0, MaxErrorMessageChars), "…");
    }

    /// <summary>The message out of an FMP error envelope, whichever of its three observed key spellings it used, or
    /// null when the payload is not an object or names no message. A non-string value yields null rather than
    /// throwing — the point of reaching here is to describe a failure, not to add one.</summary>
    private static string? ErrorTextFrom(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in root.EnumerateObject())
            if (property.NameEquals("Error Message") || property.NameEquals("error")
                || property.NameEquals("message"))
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
        return null;
    }
}
