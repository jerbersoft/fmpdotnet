using System.Runtime.CompilerServices;

namespace FmpDotNet.Tests;

/// <summary>Where the repository is, for the tests that read files out of it — the README that
/// <see cref="EndpointCoverageTests"/> regenerates, the site configuration <see cref="DocsSiteTests"/> pins.
///
/// <para>Located from the calling file's compile-time path rather than the working directory, which a runner
/// chooses and this cannot: walk up from the caller until a directory holds <c>FmpDotNet.slnx</c>.</para>
/// </summary>
internal static class RepositoryLayout
{
    public static string Root([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FmpDotNet.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
