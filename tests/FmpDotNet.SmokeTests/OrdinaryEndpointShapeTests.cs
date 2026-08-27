namespace FmpDotNet.SmokeTests;

/// <summary>Calls every ordinary endpoint against the live API and compares what came back with what was
/// recorded (#26).
///
/// <para>This is the suite the unit tests cannot be. Every test in <c>FmpDotNet.Tests</c> answers from a stub, so
/// all of them keep passing on the day FMP renames a field, moves a plan gate, or starts answering a different
/// media type — the stub still says what it always said. The evidence that this is not hypothetical is in the
/// SDK's own history: trader's adapter recorded <c>profile-bulk</c> and <c>shares-float-all</c> as 402 on
/// Premium, and both answered 200 when re-probed on 2026-08-26.</para></summary>
public sealed class OrdinaryEndpointShapeTests(OrdinarySweepFixture sweep) : IClassFixture<OrdinarySweepFixture>
{
    private const string File = "baseline-ordinary.txt";

    private const string Heading =
        """
        # Live shapes of FMP's ordinary endpoints, as this SDK reads them.
        #
        # The *-bulk endpoints are NOT here — they are separately throttled and FMP restricts keys that call
        # them often, so they have their own opt-in sweep and their own file. See baseline-bulk.txt.
        """;

    [LiveFact]
    public async Task Every_field_the_baseline_recorded_still_arrives()
    {
        var live = await sweep.ObservationsAsync();
        if (ShapeAssertions.Updated(live, Baseline.Path(File), Heading)) return;
        ShapeAssertions.FieldsStillArrive(live, Baseline.Path(File));
    }

    [LiveFact]
    public async Task The_baseline_still_describes_what_the_api_returns()
    {
        var live = await sweep.ObservationsAsync();
        if (ShapeAssertions.Updated(live, Baseline.Path(File), Heading)) return;
        ShapeAssertions.BaselineDescribesTheApi(live, Baseline.Path(File));
    }
}
