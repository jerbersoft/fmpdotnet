using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>Public float and shares outstanding for one symbol, from <c>stable/shares-float</c>.
///
/// <para>The endpoint answers a single-element array and holds no history: a <c>limit</c> parameter is accepted and
/// then ignored, so exactly one row comes back however it is asked. That is why the SDK surfaces it as one nullable
/// record rather than a list. An unknown symbol answers <c>[]</c> with HTTP 200, so "not found" is a shape here, not
/// a status code — the same rule <c>stable/profile</c> follows.</para>
///
/// <para>Measured against the live API on 2026-08-26 across 40 symbols: the six properties below are the entire
/// response, with none missing and none extra on any row.</para>
///
/// <para>The share counts are <see langword="decimal"/> and not <see langword="long"/>, even though every one of
/// those 40 rows was integral. FMP serializes share counts as JSON floating-point — <c>floatShares</c> has been
/// observed as <c>25595002.125</c>, not an integer — so these must be read as decimal: a <c>long?</c> deserialize
/// throws on any fractional value. The fractions are computation artifacts (float = outstanding × free float %), so
/// they appear intermittently rather than for particular symbols, and <c>System.Text.Json</c> throwing on one of
/// them would abort the whole response, not just that field. Callers that want whole shares can round.</para>
///
/// <para>Class-share tickers need FMP's hyphenated spelling. <c>BRK.B</c> and <c>BF.B</c> answer <c>[]</c> while
/// <c>BRK-B</c> and <c>BF-B</c> answer a row, and <c>stable/profile</c> behaves the same way. It is FMP's spelling
/// of the symbol rather than an escaping fault — the SDK escapes the query correctly — but it is worth knowing
/// because the wrong spelling surfaces as an empty result rather than an error.</para></summary>
public sealed record SharesFloat
{
    /// <summary>Ticker as FMP spells it — see the hyphenated class-share note on the type.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>When FMP last refreshed this row, in <b>UTC</b>.
    ///
    /// <para>Named <c>AsOf</c> rather than <c>Date</c> on purpose: the value carries a time of day and records when
    /// the row was recomputed, not a calendar date something happened on. Calling it <c>Date</c> would invite
    /// callers to compare it against a fiscal or trade date it has nothing to do with.</para>
    ///
    /// <para>Deliberately read with <see cref="NullableFmpInstantJsonConverter"/> — the <b>UTC</b> converter — and
    /// not with <see cref="NullableEasternInstantJsonConverter"/>, which the statement endpoints' <c>acceptedDate</c>
    /// uses. Both parse the identical <c>"uuuu-MM-dd HH:mm:ss"</c> shape, so the string cannot tell you which is
    /// right, and picking the wrong one shifts every value by 4 or 5 hours. UTC was established by measurement on
    /// 2026-08-26, not assumed: 40 stamps spread evenly from <c>00:09:20</c> to <c>14:13:45</c>, the latest sitting
    /// 26 minutes <i>before</i> UTC-now and never ahead of it. Read as Eastern, that latest stamp would be 3.5 hours
    /// in the future, which is impossible for a value recording when a row was last refreshed.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableFmpInstantJsonConverter))]
    public Instant? AsOf { get; init; }

    /// <summary>Free float as a <b>percentage on 0–100</b>, not a fraction: AAPL's 14,669,554,809 float shares
    /// against 14,687,356,000 outstanding arrive as <c>99.87879921341867</c>, not <c>0.9987…</c>. Multiplying by
    /// 100 a second time is the mistake this exists to prevent. Arrives as a JSON float or as an integer.
    ///
    /// <para>ETFs report <c>0</c> here and in <see cref="FloatShares"/> while still reporting a real
    /// <see cref="OutstandingShares"/> — SPY, QQQ, VOO and IWM all did on 2026-08-26. The zero means "not computed
    /// for this security", not "no shares are freely tradable", so it must not be fed into a float-based
    /// calculation as though it were measured.</para></summary>
    [JsonPropertyName("freeFloat")] public decimal? FreeFloat { get; init; }

    /// <summary>Shares in the public float. Zero for ETFs — see <see cref="FreeFloat"/>.</summary>
    [JsonPropertyName("floatShares")] public decimal? FloatShares { get; init; }

    /// <summary>Total shares outstanding. Populated even where <see cref="FloatShares"/> is zero.</summary>
    [JsonPropertyName("outstandingShares")] public decimal? OutstandingShares { get; init; }

    /// <summary>URL of the SEC EDGAR filing the counts were taken from, or <see langword="null"/> when FMP names no
    /// source. Null is normal rather than exceptional: SPY, QQQ, VOO, IWM and CIG all answered null on 2026-08-26.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
}
