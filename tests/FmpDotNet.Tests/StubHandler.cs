using System.Net;
using System.Text;

namespace FmpDotNet.Tests;

/// <summary>Answers every request from a queue of canned responses and records what was asked.
///
/// <para>A <see cref="StringContent"/> template is cloned per dispatch, so a single canned response can back
/// more than one request without the second read hitting the first's disposed content. Any other
/// <see cref="HttpContent"/> — the streaming payloads a couple of tests hand-build to pin flat-memory
/// behaviour — is handed out as-is, unmaterialised, exactly as before.</para></summary>
internal sealed class StubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private int _index;

    public List<Uri> Requests { get; } = [];

    /// <summary>The request headers of each dispatch, in the same order as <see cref="Requests"/>. A snapshot,
    /// because the transport disposes the message as soon as the response is back.</summary>
    public List<IReadOnlyDictionary<string, string[]>> Headers { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        Headers.Add(request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        var template = responses[Math.Min(_index++, responses.Length - 1)];
        // Only a StringContent template is cloned. Any other HttpContent — the streaming payloads a couple
        // of tests hand-build to pin flat-memory behaviour — is handed back as the same instance every
        // dispatch, so a second call against one such response would read content the transport already
        // disposed. Safe today: every non-StringContent response in this suite is dispatched once.
        return template.Content is StringContent ? await CloneAsync(template).ConfigureAwait(false) : template;
    }

    private static async Task<HttpResponseMessage> CloneAsync(HttpResponseMessage template)
    {
        var clone = new HttpResponseMessage(template.StatusCode) { ReasonPhrase = template.ReasonPhrase };
        foreach (var header in template.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        var bytes = await template.Content!.ReadAsByteArrayAsync().ConfigureAwait(false);
        var content = new ByteArrayContent(bytes);
        foreach (var header in template.Content.Headers)
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        clone.Content = content;

        return clone;
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Csv(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/csv") };

    public static HttpResponseMessage Status(HttpStatusCode status) =>
        new(status) { Content = new StringContent("", Encoding.UTF8, "text/plain") };
}
