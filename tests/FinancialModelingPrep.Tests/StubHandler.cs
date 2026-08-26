using System.Net;
using System.Text;

namespace FinancialModelingPrep.Tests;

/// <summary>Answers every request from a queue of canned responses and records what was asked.</summary>
internal sealed class StubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private int _index;

    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        var response = responses[Math.Min(_index++, responses.Length - 1)];
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Csv(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/csv") };

    public static HttpResponseMessage Status(HttpStatusCode status) =>
        new(status) { Content = new StringContent("", Encoding.UTF8, "text/plain") };
}
