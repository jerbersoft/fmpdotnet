using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
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
    /// branch on "no rows" twice.</summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error in the body.</exception>
    public async Task<IReadOnlyList<T>> GetListAsync<T>(
        FmpRequest request, JsonTypeInfo<List<T>> typeInfo, CancellationToken ct = default)
        => await TryGetListAsync(request, typeInfo, ct).ConfigureAwait(false)
           ?? throw new FmpPlanRestrictedException(
               $"FMP refused '{request}' as outside this API key's plan (402/403).");

    /// <summary>As <see cref="GetListAsync{T}(FmpRequest, JsonTypeInfo{List{T}}, CancellationToken)"/>, but returns
    /// null instead of throwing when the endpoint is outside the key's plan, so an optional fast path can degrade
    /// to a slower one.
    ///
    /// <para>Worth using even where gating looks settled: <c>profile-bulk</c> and <c>shares-float-all</c> were
    /// recorded as 402-on-Premium and both answered 200 when re-probed on 2026-08-26.</para></summary>
    public Task<IReadOnlyList<T>?> TryGetListAsync<T>(
        FmpRequest request, JsonTypeInfo<List<T>> typeInfo, CancellationToken ct = default)
        => ReadListAsync(request, (body, token) => JsonSerializer.DeserializeAsync(body, typeInfo, token), ct);

    /// <summary>Reflection-based convenience for endpoints the SDK does not yet model. Not trim- or AOT-safe; the
    /// typed endpoint clients all use the <see cref="JsonTypeInfo{T}"/> overload instead.</summary>
    [RequiresUnreferencedCode("Deserialises T by reflection. Use the JsonTypeInfo overload for trimmed apps.")]
    [RequiresDynamicCode("Deserialises T by reflection. Use the JsonTypeInfo overload for AOT apps.")]
    public Task<IReadOnlyList<T>?> TryGetListAsync<T>(FmpRequest request, CancellationToken ct = default)
        => ReadListAsync(request,
            (body, token) => JsonSerializer.DeserializeAsync<List<T>>(body, FmpJson.Options, token), ct);

    /// <summary>Sends the request, classifies the body, and deserialises it — all while the response is still
    /// alive.
    ///
    /// <para>The response must not be disposed before the body has been read: disposing it closes the content
    /// stream, and the read then fails with an <see cref="ObjectDisposedException"/> that points at the stream
    /// rather than at the lifetime that ended it.</para></summary>
    private async Task<IReadOnlyList<T>?> ReadListAsync<T>(
        FmpRequest request,
        Func<Stream, CancellationToken, ValueTask<List<T>?>> deserialise,
        CancellationToken ct)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var (prefix, prefixLength, body) = await PeekAsync(raw, ct).ConfigureAwait(false);
        await using var _ = body.ConfigureAwait(false);

        // An error envelope is a JSON OBJECT where success is a JSON ARRAY, so the first non-space byte separates
        // them without parsing either.
        if (FirstMeaningfulByte(prefix, prefixLength) == (byte)'{')
            throw await ReadErrorAsync(body, request, ct).ConfigureAwait(false);

        return await deserialise(body, ct).ConfigureAwait(false) ?? [];
    }

    /// <summary>GETs a CSV payload and streams it, mapping each record as it arrives. The response is never
    /// buffered whole.</summary>
    /// <exception cref="FmpRateLimitedException">FMP answered 429.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    /// <exception cref="FmpApiException">FMP reported an error in the body — which on the bulk surface arrives
    /// with a 200 status.</exception>
    public async IAsyncEnumerable<T> StreamCsvAsync<T>(
        FmpRequest request, Func<CsvRow, T> map, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden)
            throw new FmpPlanRestrictedException(
                $"FMP refused '{request}' as outside this API key's plan (402/403).");
        response.EnsureSuccessStatusCode();

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
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in document.RootElement.EnumerateObject())
                    if (property.NameEquals("Error Message") || property.NameEquals("error")
                        || property.NameEquals("message"))
                    {
                        message = property.Value.GetString();
                        break;
                    }
        }
        catch (JsonException)
        {
            // Fall through — an unparseable body is still an error, just an undescribed one.
        }
        return new FmpApiException(message ?? "FMP returned an error payload with no message.", request.ToString());
    }
}
