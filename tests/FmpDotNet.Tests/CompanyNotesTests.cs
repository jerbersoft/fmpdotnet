using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

/// <summary>stable/company-notes — four fields and three traps, all measured 2026-08-27.</summary>
public class CompanyNotesTests
{
    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
                handler);
    }

    [Fact]
    public async Task Note_symbols_are_the_notes_own_listings_not_the_issuers_ticker()
    {
        // symbol=T answers 20 rows whose symbols are T, T 25, T 25B, ... T PRA, T PRC — 19 of 20 differ from
        // the requested ticker, and they contain SPACES. Anything that treats this as a tradeable ticker, or
        // normalises it back to the requested one, is wrong.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("company-notes.T.json")));

        var notes = await endpoints.GetNotesAsync("T");

        Assert.Equal(5, notes.Count);
        Assert.Equal(
            new string?[] { "T", "T 33", "T 33A", "T 32A", "T 32" },
            notes.Select(n => n.Symbol));
        Assert.Contains(notes, n => n.Symbol!.Contains(' '));
    }

    [Fact]
    public async Task Note_titles_keep_the_html_entities_fmp_does_not_decode()
    {
        // FMP sends "AT&amp;T Inc. ...". The SDK does not decode it: decoding would be a silent transformation
        // of the upstream value, and a caller that wants display text calls WebUtility.HtmlDecode themselves.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("company-notes.T.json")));

        var notes = await endpoints.GetNotesAsync("T");

        Assert.Equal("AT&amp;T Inc. 5.200% Global Notes due November 18, 2033", notes[1].Title);
        Assert.DoesNotContain("AT&T", notes[1].Title);
    }

    [Fact]
    public async Task Exchange_is_null_on_almost_every_note()
    {
        // Null on 19 of T's 20 rows. A one-row AAPL sample shows "NASDAQ" and hides this entirely, which is
        // why the fixture is AT&T's and why this property is nullable.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("company-notes.T.json")));

        var notes = await endpoints.GetNotesAsync("T");

        Assert.Equal("NYSE", notes[0].Exchange);
        Assert.All(notes.Skip(1), n => Assert.Null(n.Exchange));
    }

    [Fact]
    public async Task An_issuer_with_no_notes_is_empty_not_an_error()
    {
        // JPM, BAC, VZ, GS, MS, PG and JNJ all answered [] on 2026-08-27. The dataset is sparse and empty is
        // the common case.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Empty(await endpoints.GetNotesAsync("JPM"));
    }

    [Fact]
    public async Task Rejects_a_blank_symbol_before_spending_a_request()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetNotesAsync("  "));
        Assert.Empty(handler.Requests);
    }
}
