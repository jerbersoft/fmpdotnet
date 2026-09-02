using System.Reflection;

namespace FmpDotNet.Tests;

/// <summary>Pins the dependency cut #61 made: the core assembly compiles against the options and logging
/// abstractions only, and the container wiring lives in <c>FmpDotNet.Extensions.DependencyInjection</c>.
///
/// <para><see cref="Assembly.GetReferencedAssemblies"/> lists what the compiled IL actually references, not what
/// the package graph carries — so a <c>using Microsoft.Extensions.DependencyInjection</c> that creeps back into
/// the core fails here, on the commit that adds it, rather than at the next consumer's restore. This project is
/// not AOT-compiled, so reading assembly metadata is fine here even though the library itself may not.</para>
/// </summary>
public class PackageBoundaryTests
{
    private static IReadOnlyList<string> CoreReferences { get; } =
        typeof(FmpClient).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    [Theory]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
    [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
    [InlineData("Microsoft.Extensions.Options.ConfigurationExtensions")]
    public void The_core_does_not_reference(string assembly) =>
        Assert.DoesNotContain(assembly, CoreReferences);

    /// <summary>The positive control. Without it, the theory above would pass against an empty list — for
    /// instance if <c>typeof(FmpClient)</c> ever resolved to some other assembly.</summary>
    [Theory]
    [InlineData("Microsoft.Extensions.Options")]
    [InlineData("Microsoft.Extensions.Logging.Abstractions")]
    [InlineData("NodaTime")]
    public void The_core_still_references(string assembly) =>
        Assert.Contains(assembly, CoreReferences);
}
