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

/// <summary>FMP reported an error in the response BODY rather than in the status line.
///
/// <para>Measured 2026-08-26: a throttled bulk call returns <b>HTTP 200</b> with
/// <c>{"Error Message": "Limit Reach. …"}</c> — a JSON object, on an endpoint whose success shape is CSV. Status
/// code alone therefore cannot distinguish success from failure on the bulk surface, so the transport inspects the
/// payload and raises this.</para></summary>
public sealed class FmpApiException : FmpException
{
    /// <summary>Creates the exception from the message FMP put in the body.</summary>
    public FmpApiException(string message, string? requestUri = null)
        : base(requestUri is null ? message : $"{message} (request: {requestUri})")
        => ErrorMessage = message;

    /// <summary>The upstream's own error text, unwrapped from the JSON envelope.</summary>
    public string ErrorMessage { get; }
}

/// <summary>The endpoint is not available on the account's plan — FMP answers 402 or 403.
///
/// <para>Worth catching separately: plan gating changes. Trader's adapter recorded <c>profile-bulk</c> and
/// <c>shares-float-all</c> as 402 on Premium; both answered 200 when re-probed on 2026-08-26. Code that treats
/// gating as permanent goes stale silently.</para></summary>
public sealed class FmpPlanRestrictedException : FmpException
{
    /// <summary>Creates the exception for a path the account's plan does not cover.</summary>
    public FmpPlanRestrictedException(string message) : base(message) { }
}
