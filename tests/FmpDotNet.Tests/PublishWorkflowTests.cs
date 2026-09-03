using System.Text.RegularExpressions;

namespace FmpDotNet.Tests;

/// <summary>Pins the half of publishing (#73) that lives in files rather than in a run.
///
/// <para><c>publish.yml</c> pushes one file per id in its <c>PACKAGES</c> list, by name, rather than globbing
/// <c>artifacts/*.nupkg</c>. That is what makes a third package a decision somebody records — the workflow fails
/// on a packed package the list does not name. This test closes the other half of the loop: a project added under
/// <c>src/</c> and never added to the list fails here, on its own commit, rather than at the release that skips
/// it.</para>
///
/// <para>A regex over the one <c>PACKAGES:</c> line is enough for a file of that shape, and this project has no
/// YAML dependency and should not gain one for it. Neither test needs the workflow to run.</para>
/// </summary>
public class PublishWorkflowTests
{
    [Fact]
    public void EveryPackableProjectIsNamedInThePublishWorkflow()
    {
        var src = Path.Combine(RepositoryLayout.Root(), "src");
        var onDisk = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Select(x => Regex.Match(x, @"<PackageId>([^<]+)</PackageId>").Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        // An empty entry means a csproj under src/ declares no PackageId, which would pack under its assembly
        // name and could never match the list.
        Assert.All(onDisk, id => Assert.NotEmpty(id));

        var workflow = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root(), ".github", "workflows", "publish.yml"));
        var listed = Regex.Match(workflow, @"^\s*PACKAGES:\s*(.+)$", RegexOptions.Multiline).Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk, listed);
    }

    /// <summary>The GitHub Packages feed stopped receiving publishes at 0.1.0-ci.89 (#73). Prose may name it —
    /// the versioning guide says where it stops and why — but nothing a reader follows may hand them the source
    /// URL, because adding it produces a 401 rather than a package. <c>docs/superpowers/</c> is exempt: it is a
    /// record of decisions as they were made, and rewriting history there would be a lie.</summary>
    [Fact]
    public void NothingPointsAtTheRetiredFeed()
    {
        var root = RepositoryLayout.Root();
        var files = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\', '/').Contains("/docs/superpowers/"))
            .Where(p => !p.Replace('\\', '/').Contains("/docs/_site/"))
            .Concat([
                Path.Combine(root, "README.md"),
                Path.Combine(root, "CONTRIBUTING.md"),
                Path.Combine(root, "SECURITY.md"),
            ]);

        var offenders = files
            .Where(p => File.ReadAllText(p).Contains("nuget.pkg.github.com"))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }
}
