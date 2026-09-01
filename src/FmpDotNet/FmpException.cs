using System.Net;
using NodaTime;

namespace FmpDotNet;

/// <summary>Base type for every error this SDK raises deliberately. Catch this to catch all of them.</summary>
public class FmpException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public FmpException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and the error that caused it.</summary>
    public FmpException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>FMP answered 429. Raised so a caller can re-queue the unit of work rather than drop it — the shared
/// token bucket has already been drained and held by the time this surfaces, so a retry meets back-pressure
/// instead of the limit that just rejected it.</summary>
public sealed class FmpRateLimitedException : FmpException
{
    /// <summary>Creates the exception, optionally recording the <c>Retry-After</c> the response advised.</summary>
    public FmpRateLimitedException(string message, Duration? retryAfter = null) : base(message)
        => RetryAfter = retryAfter;

    /// <summary>The <c>Retry-After</c> the response advised, before clamping, or null when it carried none.</summary>
    public Duration? RetryAfter { get; }
}

/// <summary>FMP put the reason in the response BODY, not in a status line the SDK could act on alone.
///
/// <para>Measured 2026-08-26: a throttled bulk call returns <b>HTTP 200</b> with
/// <c>{"Error Message": "Limit Reach. …"}</c> — a JSON object, on an endpoint whose success shape is CSV. Status
/// code alone therefore cannot distinguish success from failure on the bulk surface, so the transport inspects the
/// payload and raises this.</para>
///
/// <para>The converse also happens, and is why <see cref="StatusCode"/> exists. Measured the same day,
/// <c>stable/profile-bulk?part=99</c> answers <b>HTTP 400</b> with the plain-text body
/// <c>Query Error: Invalid or missing query parameter - part</c> under a <c>content-type: application/json</c> that
/// is a lie — the body is not JSON and no envelope key can be unwrapped from it. That text is the only thing that
/// says what went wrong, so a non-success response surfaces its body here rather than as a bare
/// <see cref="HttpRequestException"/>.</para></summary>
public sealed class FmpApiException : FmpException
{
    /// <summary>Creates the exception from the message FMP put in the body, optionally recording the status the
    /// response carried.</summary>
    public FmpApiException(string message, string? requestUri = null, HttpStatusCode? statusCode = null)
        : base(requestUri is null ? message : $"{message} (request: {requestUri})")
    {
        ErrorMessage = message;
        StatusCode = statusCode;
    }

    /// <summary>The upstream's own error text: unwrapped from the JSON envelope when the body was one, and the
    /// body's own text — trimmed and length-capped — when it was not. Never carries the API key: the key travels as
    /// a header, and the request is rendered through <see cref="FmpRequest.ToString"/>, which is path and query
    /// only.</summary>
    public string ErrorMessage { get; }

    /// <summary>The HTTP status the failing response carried, or <see langword="null"/> when the error arrived on a
    /// <b>200</b> — which is how the bulk surface reports throttling.
    ///
    /// <para>This is what lets a caller tell those apart without matching on message text. A
    /// <see cref="HttpStatusCode.BadRequest"/> from a <c>part</c>-paged bulk download means the parameter was
    /// rejected; a null status on the same endpoint means the download was throttled and should be retried
    /// later.</para></summary>
    public HttpStatusCode? StatusCode { get; }
}

/// <summary>FMP refused the request with 402 or 403 — the endpoint is outside the account's plan, or the key
/// itself was rejected.
///
/// <para><b>Read <see cref="StatusCode"/> before telling anyone to upgrade.</b> The two statuses are handled the
/// same way but do not mean the same thing: <b>402 Payment Required</b> is an entitlement answer about the
/// endpoint, while <b>403 Forbidden</b> is just as likely to mean the key is revoked, mistyped, or restricted —
/// which is exactly what FMP warns it will do to a key that abuses the bulk endpoints. Reporting both as "your
/// plan does not cover this" sends someone to the billing page over a broken credential.</para>
///
/// <para><b>Gating is not permanent and must not be cached as if it were.</b> Trader's adapter recorded
/// <c>profile-bulk</c> and <c>shares-float-all</c> as 402 on Premium; both answered 200 when re-probed on
/// 2026-08-26. Code that decides once that an endpoint is unavailable goes stale silently, and the SDK
/// deliberately carries no tier map for the same reason — entitlement moves, and it varies per key.</para>
///
/// <para><b>Every endpoint throws this; none of them signal a refusal by returning.</b> There is no
/// <c>Try</c>-prefixed twin anywhere in the SDK, and there deliberately is not going to be one. C# forbids
/// <c>out</c> parameters on async methods (CS1988), so the BCL's <c>bool TryX(out T)</c> shape cannot be
/// expressed on an async surface at all — which is why the framework has no <c>TryReadAsync</c> either, and why
/// <see cref="System.Threading.Channels.ChannelReader{T}"/> pairs a synchronous <c>TryRead</c> with an
/// asynchronous <c>ReadAsync</c> that throws. The nullable-return imitation this SDK briefly had was worse than
/// either: it put two error channels on one signature and overloaded a nullable return with a meaning the
/// signature could not carry, so a caller had to go and read a paragraph to learn that null meant "refused"
/// rather than "nothing there".</para>
///
/// <para><b>Null still means something, just never this.</b> Endpoints returning <c>T?</c> —
/// <see cref="Endpoints.CompanyEndpoints.GetProfileAsync"/> for an unknown symbol,
/// <see cref="Endpoints.StatementEndpoints.GetScoresAsync"/> for an ETF — use null for an answer FMP genuinely
/// gave, not for a failure. Errors are exceptions; null is data.</para></summary>
public sealed class FmpPlanRestrictedException : FmpException
{
    /// <summary>Creates the exception, recording which of 402 or 403 FMP answered.
    ///
    /// <para>There is deliberately no message-only overload. A refusal always arrives with a status, so a
    /// nullable <see cref="StatusCode"/> would model a state that cannot occur — and every caller reading it
    /// would have to handle a null that never comes.</para></summary>
    public FmpPlanRestrictedException(string message, HttpStatusCode statusCode) : base(message)
        => StatusCode = statusCode;

    /// <summary>The status FMP answered — <see cref="HttpStatusCode.PaymentRequired"/> or
    /// <see cref="HttpStatusCode.Forbidden"/>. See the type remarks: these mean different things and only one of
    /// them is about billing.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>True when FMP answered <b>402</b>, which is specifically an entitlement answer about the endpoint
    /// rather than about the credential.</summary>
    public bool IsPlanLimitation => StatusCode == HttpStatusCode.PaymentRequired;

    /// <summary>True when FMP answered <b>403</b>, which points at the key — revoked, mistyped or restricted —
    /// at least as often as at the plan.</summary>
    public bool IsRejectedCredential => StatusCode == HttpStatusCode.Forbidden;

    /// <summary>Builds the exception for a refused request, wording the message for the status actually
    /// received so the two causes stop reading identically in a log.</summary>
    internal static FmpPlanRestrictedException For(HttpStatusCode status, FmpRequest request) => new(
        status == HttpStatusCode.PaymentRequired
            ? $"FMP answered 402 for '{request}': the endpoint is outside this API key's plan."
            : $"FMP answered 403 for '{request}': the key was rejected. That can mean the plan does not cover "
              + "this endpoint, but it can equally mean the key is revoked, mistyped, or restricted — check the "
              + "credential before assuming it is a billing question.",
        status);
}
