namespace FmpDotNet.SmokeTests;

/// <summary>Checks that the sweep can still reach every endpoint — without a key, and without a request.
///
/// <para><b>These are the only tests in this project that are not gated on <c>FMP_API_KEY</c>, and that is
/// deliberate.</b> The live suite runs on a schedule; a defect introduced on a Tuesday would otherwise sit
/// unnoticed until the next scheduled run, and would surface then as an exception inside a sweep rather than as
/// a compile-time-shaped complaint about the thing that actually changed. Both checks below are pure reflection
/// over the SDK's own types, so they run on every push, cost nothing, and fail on the commit that broke
/// them.</para>
///
/// <para>What they protect against is specific: the sweep discovers endpoints by reflection and synthesises
/// arguments by parameter name, so an endpoint added with a parameter named or typed in a way
/// <see cref="Probe.Argument"/> has never seen is an endpoint the live suite would silently never call. A smoke
/// suite that quietly stops covering something is worse than one that fails.</para></summary>
public class SweepCoverageTests
{
    [Fact]
    public void The_sweep_can_supply_arguments_for_every_endpoint_method()
    {
        var unreachable = new List<string>();

        foreach (var group in Probe.Groups())
        foreach (var method in Probe.EndpointMethods(group.PropertyType))
        foreach (var parameter in method.GetParameters())
        {
            try
            {
                Probe.Argument(parameter);
            }
            catch (NotSupportedException ex)
            {
                unreachable.Add(ex.Message);
            }
        }

        Assert.True(unreachable.Count == 0,
            "The live smoke sweep cannot call these endpoints, so they would go unprobed:\n  "
            + string.Join("\n  ", unreachable));
    }

    [Fact]
    public void The_sweep_can_read_rows_out_of_every_endpoint_return_type()
    {
        var unreadable = new List<string>();

        foreach (var group in Probe.Groups())
        foreach (var method in Probe.EndpointMethods(group.PropertyType))
        {
            try
            {
                Probe.ElementType(method.ReturnType);
            }
            catch (NotSupportedException ex)
            {
                unreadable.Add($"{group.Name}.{method.Name}: {ex.Message}");
            }
        }

        Assert.True(unreadable.Count == 0,
            "The live smoke sweep cannot destructure what these endpoints return:\n  "
            + string.Join("\n  ", unreadable));
    }

    [Fact]
    public void Both_tiers_are_populated()
    {
        // Not a tautology: the partition is decided by which transport a group is constructed with, so a
        // refactor that gave BulkEndpoints an FmpTransport would move all twenty bulk endpoints into the
        // scheduled, key-only run — calling FMP's most restricted surface automatically, every week, which is
        // exactly what the second opt-in switch exists to prevent.
        var groups = Probe.Groups().ToList();
        Assert.Contains(groups, g => Probe.IsBulk(g.PropertyType));
        Assert.Contains(groups, g => !Probe.IsBulk(g.PropertyType));
    }
}
