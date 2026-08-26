using NodaTime;

namespace FinancialModelingPrep.Http;

/// <summary>Minimal token bucket. Thread-safe: it serialises access to its own mutable state under a lock, so one
/// instance can be shared as a process-wide reservoir across concurrent callers.
///
/// <para>Time is passed in as seconds rather than read from a clock, which is what makes the rate policy assertable
/// without waiting on a real one.</para></summary>
public sealed class TokenBucket
{
    private readonly Lock _lock = new();
    private readonly double _capacity;
    private readonly double _refillPerSecond;
    private double _tokens;
    private double _lastSeconds;

    /// <summary>Creates a bucket that starts full.</summary>
    public TokenBucket(double capacity, double refillPerSecond, double nowSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refillPerSecond);
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        _tokens = capacity;
        _lastSeconds = nowSeconds;
    }

    /// <summary>Burst reservoir size. Worst-case grants in any rolling 60 s window is
    /// <c>Capacity + RefillPerSecond * 60</c>.</summary>
    public double Capacity => _capacity;

    /// <summary>Sustained refill rate, tokens per second.</summary>
    public double RefillPerSecond => _refillPerSecond;

    /// <summary>Take one token. Returns <see cref="Duration.Zero"/> if granted, else the wait until one frees.</summary>
    public Duration Acquire(double nowSeconds)
    {
        lock (_lock)
        {
            var elapsed = nowSeconds - _lastSeconds;
            if (elapsed > 0)
            {
                _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerSecond);
                _lastSeconds = nowSeconds;
            }
            if (_tokens >= 1)
            {
                _tokens -= 1;
                return Duration.Zero;
            }
            // `_lastSeconds` can sit in the FUTURE after a Drain — that is how the hold is expressed, since the
            // `elapsed > 0` guard above then refills nothing until the clock reaches it. The wait owed is therefore
            // the rest of the hold PLUS the time to refill the deficit. Without the hold term this returns
            // ~1/refill regardless, and a held caller spins at the refill rate for the whole penalty instead of
            // sleeping through it.
            var hold = Math.Max(0.0, _lastSeconds - nowSeconds);
            var deficit = 1 - _tokens;
            return Duration.FromSeconds(hold + deficit / _refillPerSecond);
        }
    }

    /// <summary>Empty the reservoir and refuse to refill it for <paramref name="holdSeconds"/> — the reaction to an
    /// upstream 429. Proactive pacing alone cannot answer a 429: the server has just said the emitted rate was too
    /// high, and a bucket that keeps granting at exactly that rate argues with it.
    ///
    /// <para>The hold is an ABSOLUTE deadline, not an increment: concurrent callers that each take a 429 carrying
    /// the same Retry-After converge on one hold rather than stacking N of them. Eight workers x 60 s would
    /// otherwise wedge every FMP call in the process for eight minutes over a single rate-limit window.</para>
    ///
    /// <para>Draining is meaningful even at <paramref name="holdSeconds"/> 0 (no Retry-After header): it spends the
    /// burst reservoir, so the next requests are paced at the refill rate instead of bursting straight back into
    /// the limit that just rejected them.</para></summary>
    public void Drain(double nowSeconds, double holdSeconds)
    {
        lock (_lock)
        {
            _tokens = 0;
            _lastSeconds = Math.Max(_lastSeconds, nowSeconds + Math.Max(0.0, holdSeconds));
        }
    }
}
