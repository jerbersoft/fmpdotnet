namespace FmpDotNet;

/// <summary>The indicator asked of <c>GetAsync</c>, which selects the path segment after
/// <c>stable/technical-indicators/</c>.
///
/// <para>All nine paths return the <b>same shape</b> — <c>date, open, high, low, close, volume</c> plus one
/// column named after the segment. Measured 2026-08-29 across 88 non-empty responses, there were exactly nine
/// distinct key tuples, differing in that one element. That is why this SDK models the nine with one record
/// rather than nine.</para>
///
/// <para><b>Why a closed type when the segment is case-insensitive.</b> Unlike
/// <see cref="EconomicIndicator"/>, where <c>GDP</c> works and <c>gdp</c> does not, casing is forgiving here:
/// measured 2026-08-29, <c>SMA</c> returned a response byte-identical to <c>sma</c>. The enum earns its place
/// for two other reasons. An <i>unknown</i> segment answers <b>HTTP 404 with the body <c>[]</c></b> — the
/// success shape on a failure status, which reaches a caller as an exception naming neither the mistake nor
/// the fix. And this is the only place a caller will read the warm-up behaviour below.</para>
///
/// <para><b>The value FMP returns for a given date depends on the range you asked for.</b> This is the most
/// dangerous measured behaviour on these paths and it is invisible at every layer: the status is 200, the
/// array is well formed, and the numbers are plausible. Measured 2026-08-29 on AAPL at
/// <c>periodLength=10</c>, comparing a ten-row window against the same ten dates inside the 1254-row series,
/// the worst row of each:</para>
/// <list type="table">
///   <listheader><term>indicator</term><description>worst row</description></listheader>
///   <item><term><see cref="Sma"/>, <see cref="Wma"/>, <see cref="WilliamsR"/>,
///     <see cref="StandardDeviation"/>, <see cref="Rsi"/></term><description>0.0000% — exact</description></item>
///   <item><term><see cref="Ema"/></term><description>0.1616%</description></item>
///   <item><term><see cref="Tema"/></term><description>0.1540%</description></item>
///   <item><term><see cref="Dema"/></term><description>0.4021%</description></item>
///   <item><term><see cref="Adx"/></term><description><b>276.9981%</b></description></item>
/// </list>
/// <para>Use <see cref="TechnicalIndicatorExtensions.NeedsWarmUp"/> and
/// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> to act on this. The SDK does <b>not</b> act
/// on it for you: it sends exactly the range it was given.</para></summary>
public enum TechnicalIndicator
{
    /// <summary>Average Directional Index — segment <c>adx</c>, field <c>adx</c>.
    ///
    /// <para><b>The one indicator that is unusable on a short range.</b> Measured 2026-08-29 at
    /// <c>periodLength=10</c>, the newest row of a ten-row window read 57.743123 where the full series read
    /// 15.847068 — an error of <b>264%</b>. Convergence against history depth, newest row: 10 bars 264.377%,
    /// 42 bars 10.876%, 83 bars 0.139%, 145 bars 0.001%, 271 bars exact. Repeated at
    /// <c>periodLength=20</c>: 83 bars 35.6145%, 145 bars 3.3040%, 271 bars 0.0030%, 521 bars exact. Reaching
    /// the full-series value took 271 bars at one period and 521 at the other — about <b>26–27× the
    /// period</b> in both cases.</para></summary>
    Adx,

    /// <summary>Double Exponential Moving Average — segment <c>dema</c>, field <c>dema</c>. Measured
    /// 2026-08-29, worst row of a ten-row window at <c>periodLength=10</c>: <b>0.4021%</b> from the full
    /// series, the largest of the three moving averages that drift.</summary>
    Dema,

    /// <summary>Exponential Moving Average — segment <c>ema</c>, field <c>ema</c>. Measured 2026-08-29, worst
    /// row of a ten-row window at <c>periodLength=10</c>: <b>0.1616%</b> from the full series.</summary>
    Ema,

    /// <summary>Relative Strength Index — segment <c>rsi</c>, field <c>rsi</c>.
    ///
    /// <para><b>Recursive by construction and measured exact.</b> RSI uses Wilder smoothing, so theory says it
    /// carries state from before the window — yet measured 2026-08-29, every row of a ten-row window matched
    /// the full series to every digit. Whatever history FMP buffers ahead of the requested range is enough for
    /// this one. <see cref="TechnicalIndicatorExtensions.NeedsWarmUp"/> reports <see langword="false"/> here,
    /// on the measurement rather than on the textbook.</para></summary>
    Rsi,

    /// <summary>Simple Moving Average — segment <c>sma</c>, field <c>sma</c>. Measured 2026-08-29: exact on
    /// every row of a ten-row window. A sanity check on the column's meaning — at <c>periodLength=1</c> it
    /// equalled <c>close</c> on all 1254 rows.</summary>
    Sma,

    /// <summary>Rolling standard deviation of price — segment <c>standarddeviation</c>, field
    /// <b><c>standardDeviation</c></b>.
    ///
    /// <para><b>The one member in nine whose path segment is not its JSON field name.</b> The segment is
    /// all-lowercase and the field is camelCase, measured 2026-08-29. This is why the SDK holds both mappings
    /// rather than deriving one from the other.</para>
    ///
    /// <para>Measured 2026-08-29 on AAPL, 1254 daily rows at <c>periodLength=10</c>: 0.6703 to 18.9556, and
    /// exact on every row of a ten-row window.</para></summary>
    StandardDeviation,

    /// <summary>Triple Exponential Moving Average — segment <c>tema</c>, field <c>tema</c>. Measured
    /// 2026-08-29, worst row of a ten-row window at <c>periodLength=10</c>: <b>0.1540%</b> from the full
    /// series.</summary>
    Tema,

    /// <summary>Williams %R — segment <c>williams</c>, field <c>williams</c>.
    ///
    /// <para><b>Negative.</b> Measured 2026-08-29 on AAPL, 1254 daily rows at <c>periodLength=10</c> ran from
    /// <b>−99.5844</b> to 0.0000: 1252 strictly negative, two exactly zero, none positive. A model that
    /// assumes indicator columns are non-negative is wrong on this one.</para>
    ///
    /// <para>Named for the indicator rather than the segment, following <see cref="EconomicIndicator"/>, which
    /// renames freely from the wire.</para></summary>
    WilliamsR,

    /// <summary>Weighted Moving Average — segment <c>wma</c>, field <c>wma</c>. Measured 2026-08-29: exact on
    /// every row of a ten-row window.</summary>
    Wma,
}

/// <summary>Conversions and measured warm-up guidance for <see cref="TechnicalIndicator"/>.</summary>
public static class TechnicalIndicatorExtensions
{
    /// <summary>The segment FMP expects after <c>stable/technical-indicators/</c>.
    ///
    /// <para>A path segment rather than a query value, so an unmapped member must throw rather than fall back:
    /// measured 2026-08-29, an unrecognised segment answers <b>HTTP 404 with the body <c>[]</c></b>, which the
    /// transport reports as "FMP answered HTTP 404 (NotFound) with no explanation in the body" — true, and
    /// unhelpful about which argument was wrong.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToPathSegment(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx => "adx",
        TechnicalIndicator.Dema => "dema",
        TechnicalIndicator.Ema => "ema",
        TechnicalIndicator.Rsi => "rsi",
        TechnicalIndicator.Sma => "sma",
        TechnicalIndicator.StandardDeviation => "standarddeviation",
        TechnicalIndicator.Tema => "tema",
        TechnicalIndicator.WilliamsR => "williams",
        TechnicalIndicator.Wma => "wma",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known technical indicator."),
    };

    /// <summary>Whether the value FMP returns for this indicator changes when the requested range narrows.
    ///
    /// <para><see langword="true"/> for <see cref="TechnicalIndicator.Adx"/>,
    /// <see cref="TechnicalIndicator.Dema"/>, <see cref="TechnicalIndicator.Ema"/> and
    /// <see cref="TechnicalIndicator.Tema"/> — the four that drifted when measured 2026-08-29.
    /// <see langword="false"/> for the five that were exact.</para>
    ///
    /// <para><b>Deliberately not called <c>IsRecursive</c>.</b> <see cref="TechnicalIndicator.Rsi"/> is
    /// recursive by construction and measured exact, so a name asserting the textbook property would
    /// contradict the measurement it encodes. This reports what was observed, which is the only thing the SDK
    /// knows.</para></summary>
    public static bool NeedsWarmUp(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx or TechnicalIndicator.Dema
            or TechnicalIndicator.Ema or TechnicalIndicator.Tema => true,
        _ => false,
    };

    /// <summary>How many extra bars to request <i>before</i> the range you actually want, then discard.
    ///
    /// <para><b>This is a recommendation derived from the measurements, not a measured constant</b>, and the
    /// distinction matters. <see cref="TechnicalIndicator.Adx"/> was swept across five window widths at two
    /// periods and reached the full-series value at 271 bars for <c>periodLength=10</c> and 521 for
    /// <c>periodLength=20</c> — about 26–27× in both, which is why 27× is used here.
    /// <see cref="TechnicalIndicator.Ema"/>, <see cref="TechnicalIndicator.Dema"/> and
    /// <see cref="TechnicalIndicator.Tema"/> were measured at two periods but only at the narrow end: worst
    /// row 0.4021% at ten bars, and 0.002% or better by 42 bars. The 4× returned for those is a round number
    /// comfortably past where the error stopped mattering, not a threshold anyone measured.</para>
    ///
    /// <para>Zero for the five measured exact at the narrowest window tested — FMP evidently buffers enough
    /// history ahead of the range for them.</para>
    ///
    /// <para>The SDK never applies this itself. Over-fetching on the caller's behalf would transfer up to 27×
    /// the requested bytes, diverge silently from the request that was made, and could not always succeed
    /// anyway because of the roughly five-year span ceiling on daily bars.</para></summary>
    /// <param name="indicator">The indicator whose warm-up is wanted.</param>
    /// <param name="periodLength">The period the call will use. Must be 1 or greater.</param>
    /// <returns>Extra bars to prepend to the requested range, or zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodLength"/> is less than 1.</exception>
    public static int SuggestedWarmUpBars(this TechnicalIndicator indicator, int periodLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(periodLength, 1);

        return indicator switch
        {
            TechnicalIndicator.Adx => 27 * periodLength,
            TechnicalIndicator.Dema or TechnicalIndicator.Ema or TechnicalIndicator.Tema => 4 * periodLength,
            _ => 0,
        };
    }

    /// <summary>The JSON field carrying this indicator's value.
    ///
    /// <para>Equal to <see cref="ToPathSegment"/> on eight of nine.
    /// <see cref="TechnicalIndicator.StandardDeviation"/> is the exception — segment
    /// <c>standarddeviation</c>, field <c>standardDeviation</c>, measured 2026-08-29.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    internal static string ToJsonField(this TechnicalIndicator indicator) => indicator switch
    {
        TechnicalIndicator.Adx => "adx",
        TechnicalIndicator.Dema => "dema",
        TechnicalIndicator.Ema => "ema",
        TechnicalIndicator.Rsi => "rsi",
        TechnicalIndicator.Sma => "sma",
        TechnicalIndicator.StandardDeviation => "standardDeviation",
        TechnicalIndicator.Tema => "tema",
        TechnicalIndicator.WilliamsR => "williams",
        TechnicalIndicator.Wma => "wma",
        _ => throw new ArgumentOutOfRangeException(
            nameof(indicator), indicator, "Not a known technical indicator."),
    };

    /// <summary>Resolves a JSON field name back to the indicator it carries, for the converter that reads
    /// whichever ninth key arrived. Case-sensitive: the wire field is, even though the path segment is
    /// not.</summary>
    internal static bool TryFromJsonField(string field, out TechnicalIndicator indicator)
    {
        switch (field)
        {
            case "adx": indicator = TechnicalIndicator.Adx; return true;
            case "dema": indicator = TechnicalIndicator.Dema; return true;
            case "ema": indicator = TechnicalIndicator.Ema; return true;
            case "rsi": indicator = TechnicalIndicator.Rsi; return true;
            case "sma": indicator = TechnicalIndicator.Sma; return true;
            case "standardDeviation": indicator = TechnicalIndicator.StandardDeviation; return true;
            case "tema": indicator = TechnicalIndicator.Tema; return true;
            case "williams": indicator = TechnicalIndicator.WilliamsR; return true;
            case "wma": indicator = TechnicalIndicator.Wma; return true;
            default: indicator = default; return false;
        }
    }
}
