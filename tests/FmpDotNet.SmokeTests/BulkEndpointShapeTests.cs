namespace FmpDotNet.SmokeTests;

/// <summary>The same sweep for the <c>*-bulk</c> endpoints, behind a second switch (#26).
///
/// <para><b>Excluded by default, and throttled when it is not.</b> FMP's own throttle message warns that
/// "frequent abuse on this API Endpoint may result in restrictions placed on this API Key", so the cost of
/// running this too often is the key rather than the minutes. Two things keep that in hand and neither is a
/// <c>Thread.Sleep</c>: <see cref="LiveApi.BulkVariable"/> has to be set deliberately, and the calls that do go
/// out queue behind the SDK's own bulk token bucket, which refills at <see cref="FmpOptions.BulkPerMinuteCap"/>
/// — a default of 2 a minute, itself a measured answer to how quickly FMP refuses a second bulk call. Eighteen
/// probes therefore take about eight minutes of mostly waiting (measured 2026-08-26: 8 m 4 s), which is the point.</para>
///
/// <para>Each probe reads the first few rows and abandons the download; nothing here transfers a whole file.
/// The largest of them is 69 MB.</para></summary>
public sealed class BulkEndpointShapeTests(BulkSweepFixture sweep) : IClassFixture<BulkSweepFixture>
{
    private const string File = "baseline-bulk.txt";

    private const string Heading =
        """
        # Live shapes of FMP's *-bulk endpoints, as this SDK reads them.
        #
        # Recorded from the first rows of each download, not from the whole file: a bulk response runs to tens
        # of megabytes and the sweep aborts it once it has enough rows to tell a sparse column from an absent
        # one. A property recorded here as `null` is therefore null across that sample, not across the universe.
        """;

    [LiveBulkFact]
    public async Task Every_field_the_baseline_recorded_still_arrives()
    {
        var live = await sweep.ObservationsAsync();
        if (ShapeAssertions.Updated(live, Baseline.Path(File), Heading)) return;
        ShapeAssertions.FieldsStillArrive(live, Baseline.Path(File));
    }

    [LiveBulkFact]
    public async Task The_baseline_still_describes_what_the_api_returns()
    {
        var live = await sweep.ObservationsAsync();
        if (ShapeAssertions.Updated(live, Baseline.Path(File), Heading)) return;
        ShapeAssertions.BaselineDescribesTheApi(live, Baseline.Path(File));
    }
}
