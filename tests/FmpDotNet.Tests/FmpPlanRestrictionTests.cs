using System.Net;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>How a refused request is reported.
///
/// <para>402 and 403 are handled the same way but do not mean the same thing, and the SDK used to say they did:
/// both produced an identical "outside this API key's plan" message with no status attached. A revoked or
/// mistyped key therefore sent someone to the billing page. These pin the distinction.</para></summary>
public class FmpPlanRestrictionTests
{
    private static (FmpTransport Transport, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "super-secret-key" })), handler);
    }

    private static BulkEndpoints Bulk(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new BulkEndpoints(new FmpBulkTransport(http, Options.Create(new FmpOptions { ApiKey = "super-secret-key" })));
    }

    [Fact]
    public async Task A_402_is_reported_as_an_entitlement_answer_about_the_endpoint()
    {
        var (transport, _) = Build(StubHandler.Status(HttpStatusCode.PaymentRequired));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => transport.GetListAsync(new FmpRequest("stable/profile").With("symbol", "AAPL"),
                                         FmpJsonContext.Default.ListCompanyProfile));

        Assert.Equal(HttpStatusCode.PaymentRequired, ex.StatusCode);
        Assert.True(ex.IsPlanLimitation);
        Assert.False(ex.IsRejectedCredential);
        Assert.Contains("402", ex.Message);
        Assert.Contains("outside this API key's plan", ex.Message);
    }

    [Fact]
    public async Task A_403_says_the_key_may_be_the_problem_rather_than_the_plan()
    {
        // The whole point. FMP warns it will restrict a key that abuses the bulk endpoints, and a revoked or
        // mistyped key answers here too — so "upgrade your plan" is the wrong thing to tell someone on a 403.
        var (transport, _) = Build(StubHandler.Status(HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => transport.GetListAsync(new FmpRequest("stable/profile").With("symbol", "AAPL"),
                                         FmpJsonContext.Default.ListCompanyProfile));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.True(ex.IsRejectedCredential);
        Assert.False(ex.IsPlanLimitation);
        Assert.Contains("403", ex.Message);
        Assert.Contains("revoked, mistyped, or restricted", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task The_message_names_the_request_and_never_the_api_key(HttpStatusCode status)
    {
        var (transport, _) = Build(StubHandler.Status(status));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => transport.GetListAsync(new FmpRequest("stable/profile").With("symbol", "AAPL"),
                                         FmpJsonContext.Default.ListCompanyProfile));

        Assert.Contains("stable/profile?symbol=AAPL", ex.Message);
        Assert.DoesNotContain("super-secret-key", ex.Message);
        Assert.DoesNotContain("super-secret-key", ex.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task The_Try_form_still_answers_null_for_both_statuses(HttpStatusCode status)
    {
        // The degrade-in-one-branch path is unchanged: a caller with a fallback does not want to catch anything.
        // Null remains "not entitled" and never "no rows" — an entitled call with nothing to say returns [].
        var (transport, _) = Build(StubHandler.Status(status));

        var rows = await transport.TryGetListAsync(
            new FmpRequest("stable/shares-float-all"), FmpJsonContext.Default.ListSharesFloat);

        Assert.Null(rows);
    }

    [Fact]
    public async Task An_entitled_call_with_nothing_to_say_returns_an_empty_list_not_null()
    {
        // The distinction the null is protecting. Trader's adapter collapsed 402 into an empty result and its own
        // notes record that as a defect, because a paywalled endpoint then reads exactly like a real empty answer.
        var (transport, _) = Build(StubHandler.Json("[]"));

        var rows = await transport.TryGetListAsync(
            new FmpRequest("stable/shares-float-all"), FmpJsonContext.Default.ListSharesFloat);

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task The_csv_pipeline_carries_the_same_distinction(HttpStatusCode status, bool isPlan)
    {
        // Bulk is the most plan-gated part of the API, so this path sees refusals more than any other — and it
        // throws on the first MoveNextAsync rather than yielding an empty stream.
        var endpoints = Bulk(StubHandler.Csv("", status));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(async () =>
        {
            await foreach (var _ in endpoints.StreamRatiosTtmAsync()) { }
        });

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(isPlan, ex.IsPlanLimitation);
        Assert.Contains("ratios-ttm-bulk", ex.Message);
    }

    [Fact]
    public void An_exception_built_without_a_status_reports_neither_cause()
    {
        // The message-only constructor is still public, so the flags must not claim a cause that was never given.
        var ex = new FmpPlanRestrictedException("refused");

        Assert.Null(ex.StatusCode);
        Assert.False(ex.IsPlanLimitation);
        Assert.False(ex.IsRejectedCredential);
    }
}
