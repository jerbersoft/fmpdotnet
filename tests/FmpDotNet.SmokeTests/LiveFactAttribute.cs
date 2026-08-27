namespace FmpDotNet.SmokeTests;

/// <summary>A fact that calls the real API, and skips itself when there is no key to call it with.
///
/// <para>The skip is computed in the constructor because xUnit v2 has no runtime skip — <c>Assert.Skip</c>
/// arrived in v3. Deciding at discovery time is the better answer here anyway: a run with no key reports
/// "skipped: FMP_API_KEY is not set" against every live test, which says what did not happen. A runtime skip
/// would first have to build the client, and building the client is the step that needs the key.</para></summary>
public class LiveFactAttribute : FactAttribute
{
    /// <summary>Skips unless the API key is present in the environment.</summary>
    public LiveFactAttribute()
    {
        if (LiveApi.ApiKey is null)
            Skip = $"{LiveApi.KeyVariable} is not set — the live smoke suite is skipped.";
    }
}

/// <summary>A live fact for the <c>*-bulk</c> endpoints, which need a second, deliberate opt-in.
///
/// <para>Two switches rather than one is the whole of this suite's answer to "bulk endpoints excluded or
/// heavily rate-limited". A scheduled run supplies the key alone and these stay skipped; running them is
/// something a person chooses, having read why. See <see cref="LiveApi.BulkVariable"/>.</para></summary>
public sealed class LiveBulkFactAttribute : LiveFactAttribute
{
    /// <summary>Skips unless the API key is present <i>and</i> bulk has been opted into.</summary>
    public LiveBulkFactAttribute()
    {
        if (Skip is null && !LiveApi.BulkEnabled)
            Skip = $"{LiveApi.BulkVariable} is not set — the bulk endpoints are excluded by default. "
                   + "FMP restricts keys for frequent bulk use; set it only for a deliberate run.";
    }
}
