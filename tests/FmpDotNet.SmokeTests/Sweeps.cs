using System.Text;
using NodaTime;

namespace FmpDotNet.SmokeTests;

/// <summary>Runs the live sweep once and hands the same observations to every test in the class.
///
/// <para>Lazy rather than eager, and that is load-bearing rather than tidy. A fixture constructor is the wrong
/// place to decide whether to call FMP: it runs as part of setting the class up, not as part of running a test,
/// so an eager sweep would tie "these tests exist" to "a request went out". Deferring it means the network is
/// touched only when a test that is actually going to run asks for the observations, and a run where everything
/// skips for want of a key sends nothing.</para></summary>
public abstract class SweepFixture
{
    private Task<IReadOnlyList<Observation>>? _sweep;

    /// <summary>Whether this fixture sweeps the <c>*-bulk</c> groups or the ordinary ones.</summary>
    protected abstract bool Bulk { get; }

    /// <summary>The sweep, started on first use and shared by every test in the class.</summary>
    public Task<IReadOnlyList<Observation>> ObservationsAsync() => _sweep ??= Probe.SweepAsync(Bulk);
}

/// <summary>The sweep of everything that is not a <c>*-bulk</c> endpoint.</summary>
public sealed class OrdinarySweepFixture : SweepFixture
{
    /// <inheritdoc/>
    protected override bool Bulk => false;
}

/// <summary>The sweep of the <c>*-bulk</c> endpoints. Only built when a test that opted into bulk runs.</summary>
public sealed class BulkSweepFixture : SweepFixture
{
    /// <inheritdoc/>
    protected override bool Bulk => true;
}

/// <summary>The two comparisons every shape test makes against a recorded baseline.
///
/// <para>They are deliberately separate assertions with different severities. <see cref="FieldsStillArrive"/> is
/// the alarm: something the SDK models stopped coming back, which is a defect in production code that no unit
/// test can see. <see cref="BaselineDescribesTheApi"/> is housekeeping: FMP started sending something it did not
/// before, or an endpoint was added. Folding them together would mean a newly-populated field and a
/// newly-missing one produced the same red, and the second one is the one worth waking up for.</para></summary>
internal static class ShapeAssertions
{
    /// <summary>Rewrites the baseline when <see cref="LiveApi.UpdateVariable"/> is set, and reports whether it
    /// did — in which case the caller asserts nothing, having just replaced what it would assert against.</summary>
    public static bool Updated(IReadOnlyList<Observation> live, string path, string heading)
    {
        if (!LiveApi.Updating) return false;

        // A run that failed is not a measurement, and must never become the record.
        //
        // Without this guard a transport fault or a throttled key writes `outcome error` into the baseline as
        // the endpoint's recorded truth — and the NEXT run compares against it, finds the endpoint still
        // erroring, and PASSES. The suite would then be green precisely because the endpoint is broken, which is
        // the exact failure it exists to catch. This is not hypothetical: regenerating the bulk baseline on
        // 2026-08-27 recorded `error` for Bulk.StreamProfilesAsync and Bulk.StreamRatingsAsync, both of which
        // had answered `rows` a few hours earlier.
        //
        // Refusing the whole file, rather than keeping the last good line for the failed endpoint, is
        // deliberate. This file's claim is that it is one coherent observation of the API on the date in its
        // header; a file stitched together from several runs cannot be read that way, and the header date would
        // be a lie about some of its lines.
        //
        // Plan refusals are NOT failures and are still recorded. A 402 is a stable fact about what the key's
        // plan reaches, and noticing one change is half of why this suite exists.
        var failed = live.Where(o => o.Outcome == Probe.Error).ToList();
        if (failed.Count > 0)
            throw new InvalidOperationException(
                $"""
                 Refusing to record a baseline from a run that failed. {failed.Count} of {live.Count} endpoints
                 errored, so this run measured nothing that is worth writing down:

                 {Bullets([.. failed.Select(o => $"{o.Name}: {o.Detail}")])}

                 Fix the cause and regenerate. If the cause is upstream — a throttled or restricted key answers
                 this way — then wait, rather than recording it: an error written into the baseline becomes the
                 shape every later run is compared against, and the alarm stops working silently.
                 """);

        var measured = SystemClock.Instance.GetCurrentInstant().InUtc().Date;
        File.WriteAllText(path, Baseline.Render(live, measured, heading));
        return true;
    }

    /// <summary>Fails when the API stopped answering the way it was recorded answering.</summary>
    public static void FieldsStillArrive(IReadOnlyList<Observation> live, string path)
    {
        var baseline = Baseline.Read(path);
        var refused = new List<string>();
        var lost = new List<string>();

        foreach (var observation in live.OrderBy(o => o.Name, StringComparer.Ordinal))
        {
            // An endpoint with no recorded shape is new. That is drift for the other test to report, not a
            // regression — nothing has been lost that was ever measured.
            if (!baseline.TryGetValue(observation.Name, out var was)) continue;

            if (was.Outcome != observation.Outcome)
            {
                // Reported once, and the property comparison skipped. A refused or failed call records no
                // properties at all, so comparing them would bury one real finding under sixty derived ones.
                refused.Add($"{observation.Name}: answered `{observation.Outcome}`, was `{was.Outcome}` "
                            + $"— {observation.Detail}");
                continue;
            }

            // Only a property that came back and was empty counts. One that is in neither list has been removed
            // from the model, which is a change to this repository rather than to FMP.
            lost.AddRange(was.Set
                .Where(p => observation.Unset.Contains(p, StringComparer.Ordinal))
                .Select(p => $"{observation.Name}.{p}"));
        }

        // An outcome change is reported in both directions, not only downward. A gate that OPENS is as much a
        // finding as one that closes: trader's adapter was written against profile-bulk and shares-float-all
        // answering 402, both answered 200 when re-probed on 2026-08-26, and nothing told it they had changed.
        var message = new StringBuilder("The live API no longer answers the way this SDK recorded it answering.\n");
        if (refused.Count > 0)
            message.Append("\nEndpoints whose outcome changed:\n").Append(Bullets(refused)).Append('\n');
        if (lost.Count > 0)
            message.Append("\nFields that stopped arriving:\n").Append(Bullets(lost)).Append('\n');

        message.Append(
            $"""

             A renamed or dropped field does NOT surface as an error: almost every property on these models is
             nullable and not `required`, so System.Text.Json deserialises the missing field to null and the rows
             keep arriving looking fine. Check FMP's response for the endpoint before touching anything — if the
             field moved, the model's [JsonPropertyName] is what needs to change, and only then the baseline:

                 {LiveApi.KeyVariable}=… {LiveApi.UpdateVariable}=1 dotnet test tests/FmpDotNet.SmokeTests
             """);

        Assert.True(refused.Count == 0 && lost.Count == 0, message.ToString());
    }

    /// <summary>Fails on any difference at all, in either direction, and asks for a regenerated baseline.</summary>
    public static void BaselineDescribesTheApi(IReadOnlyList<Observation> live, string path)
    {
        var baseline = Baseline.Read(path);
        var drift = new List<string>();

        foreach (var name in baseline.Keys.Except(live.Select(o => o.Name)).Order(StringComparer.Ordinal))
            drift.Add($"{name}: recorded, but the sweep no longer calls it");

        foreach (var observation in live.OrderBy(o => o.Name, StringComparer.Ordinal))
        {
            if (!baseline.TryGetValue(observation.Name, out var was))
            {
                drift.Add($"{observation.Name}: called, but never recorded — answered `{observation.Outcome}`");
                continue;
            }

            if (was.Outcome != observation.Outcome)
            {
                drift.Add($"{observation.Name}: answered `{observation.Outcome}`, was `{was.Outcome}` "
                          + $"— {observation.Detail}");
                continue;
            }

            foreach (var property in observation.Set.Where(p => was.Unset.Contains(p, StringComparer.Ordinal)))
                drift.Add($"{observation.Name}.{property}: now populated, was always null");
            foreach (var property in observation.Unset.Where(p => was.Set.Contains(p, StringComparer.Ordinal)))
                drift.Add($"{observation.Name}.{property}: now always null, was populated");

            var known = was.Set.Concat(was.Unset).ToHashSet(StringComparer.Ordinal);
            foreach (var property in observation.Set.Concat(observation.Unset)
                         .Where(p => !known.Contains(p)).Order(StringComparer.Ordinal))
                drift.Add($"{observation.Name}.{property}: on the model, absent from the baseline");
        }

        Assert.True(drift.Count == 0,
            $"""
             The recorded shapes no longer describe what the API answers.

             {Bullets(drift)}

             This is drift, not necessarily a break — a field FMP started populating, or an endpoint added to the
             SDK, reads the same way here. Read the list, satisfy yourself nothing was lost, then regenerate:

                 {LiveApi.KeyVariable}=… {LiveApi.UpdateVariable}=1 dotnet test tests/FmpDotNet.SmokeTests
             """);
    }

    /// <summary>Caps a failure listing. A whole-suite outage would otherwise print several hundred lines and
    /// bury the count, which is the one number that says whether this is one endpoint or all of them.</summary>
    private static string Bullets(IReadOnlyList<string> lines, int max = 40) =>
        string.Join('\n', lines.Take(max).Select(line => "  " + line))
        + (lines.Count > max ? $"\n  … and {lines.Count - max} more ({lines.Count} in total)" : "");
}
