namespace FmpDotNet.SmokeTests;

/// <summary>Checks that a failed run cannot become the recorded baseline.
///
/// <para>Offline, keyless, and therefore run on every push — because the thing it guards is invisible when it
/// breaks. If a failed sweep is allowed to write itself down, the next run compares against `outcome error`,
/// agrees, and reports success; the suite goes green *because* an endpoint is broken, and stays that way. There
/// is no later signal that would catch it, so the check has to sit here rather than in the scheduled run.</para>
///
/// <para>The environment variable is set and restored rather than injected. The suite disables test
/// parallelisation assembly-wide, so a process-global switch is safe to move; a seam threaded through
/// <see cref="ShapeAssertions"/> purely for this test would let the tested path and the real path diverge, and
/// the real path is the one that decides whether a baseline is trustworthy.</para></summary>
public sealed class BaselineRecordingTests : IDisposable
{
    private readonly string? _updating = Environment.GetEnvironmentVariable(LiveApi.UpdateVariable);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"fmp-baseline-{Guid.NewGuid():N}.txt");

    public BaselineRecordingTests() => Environment.SetEnvironmentVariable(LiveApi.UpdateVariable, "1");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LiveApi.UpdateVariable, _updating);
        File.Delete(_path);
    }

    private static Observation Answered(string method, string outcome, params string[] set) =>
        new("Bulk", method, outcome, outcome == Probe.Error ? "HttpRequestException: refused" : "1 rows",
            set, []);

    [Fact]
    public void A_sweep_with_a_failed_endpoint_is_not_recorded()
    {
        Observation[] live =
        [
            Answered("StreamPeersAsync", Probe.Rows, "Symbol"),
            Answered("StreamProfilesAsync", Probe.Error),
        ];

        var refusal = Assert.Throws<InvalidOperationException>(
            () => ShapeAssertions.Updated(live, _path, "# heading"));

        // The endpoint that failed has to be named. A refusal that only says "something errored" sends whoever
        // is regenerating back to a two-and-a-half-hour bulk run to find out which one.
        Assert.Contains("Bulk.StreamProfilesAsync", refusal.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_path), "A refused run must leave no baseline behind at all.");
    }

    [Fact]
    public void A_sweep_where_an_endpoint_refused_on_plan_is_recorded()
    {
        // The opposite case, and the reason the guard tests for `error` rather than for "not rows". A 402 is a
        // measurement — it is what trader was bitten by when two of them turned into 200s — so it belongs in the
        // file. Blocking on it would make the plan gate the one change this suite could never record.
        Observation[] live =
        [
            Answered("StreamPeersAsync", Probe.Rows, "Symbol"),
            Answered("StreamProfilesAsync", Probe.PlanRequired),
        ];

        Assert.True(ShapeAssertions.Updated(live, _path, "# heading"));
        Assert.Contains($"{Probe.PlanRequired}", File.ReadAllText(_path), StringComparison.Ordinal);
    }

    [Fact]
    public void What_is_recorded_reads_back_as_what_was_observed()
    {
        // The two halves of the format are written and parsed by different code, and only the live suite
        // normally exercises both — a run that writes a line `Read` cannot parse would leave a baseline that
        // throws the *next* time anyone runs the suite, hours or a week later, nowhere near the cause.
        Observation[] live =
        [
            new("Bulk", "StreamProfilesAsync", Probe.Rows, "25 rows", ["Symbol", "Sector"], ["Cik"]),
            new("Bulk", "StreamRatingsAsync", Probe.PlanRequired, "402", [], []),
        ];

        Assert.True(ShapeAssertions.Updated(live, _path, "# heading"));
        var read = Baseline.Read(_path);

        Assert.Equal(2, read.Count);
        Assert.Equal(Probe.Rows, read["Bulk.StreamProfilesAsync"].Outcome);
        Assert.Equal(["Sector", "Symbol"], read["Bulk.StreamProfilesAsync"].Set);
        Assert.Equal(["Cik"], read["Bulk.StreamProfilesAsync"].Unset);
        Assert.Equal(Probe.PlanRequired, read["Bulk.StreamRatingsAsync"].Outcome);
        Assert.Empty(read["Bulk.StreamRatingsAsync"].Set);
    }

    [Fact]
    public void Nothing_is_recorded_when_the_update_switch_is_unset()
    {
        Environment.SetEnvironmentVariable(LiveApi.UpdateVariable, null);

        Assert.False(ShapeAssertions.Updated([Answered("StreamPeersAsync", Probe.Rows, "Symbol")], _path, "#"));
        Assert.False(File.Exists(_path));
    }
}
