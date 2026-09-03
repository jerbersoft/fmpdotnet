using System.Text.Json;
using System.Text.RegularExpressions;

namespace FmpDotNet.Tests;

/// <summary>Pins two facts about the documentation site (#71) that DocFX itself does not check.
///
/// <para><c>docs/docfx.json</c> names the projects whose doc comments become the API reference, one by one rather
/// than by glob, so that a third package is a decision in a diff. This is what stops the decision being skipped by
/// omission: a project under <c>src/</c> that is not listed fails here, and so does a listing for a project that no
/// longer exists.</para>
///
/// <para>DocFX builds a guide nobody has linked to without a word. The sidebar is <c>docs/guides/toc.yml</c>, and
/// every page under <c>docs/guides/</c> has to be in it — and every entry in it has to exist. A regex over the
/// <c>href:</c> lines is enough for a file of that shape; this project has no YAML dependency and should not gain
/// one for it.</para>
///
/// <para>Neither test needs DocFX installed. Both find the repository through <see cref="RepositoryLayout"/>.</para>
/// </summary>
public class DocsSiteTests
{
    [Fact]
    public void EveryShippingProjectIsInTheApiReference()
    {
        var src = Path.Combine(RepositoryLayout.Root(), "src");
        var onDisk = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(src, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        using var docfx = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryLayout.Root(), "docs", "docfx.json")));
        var listed = docfx.RootElement.GetProperty("metadata").EnumerateArray()
            .SelectMany(m => m.GetProperty("src").EnumerateArray())
            .SelectMany(s => s.GetProperty("files").EnumerateArray())
            .Select(f => f.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk, listed);
    }

    [Fact]
    public void EveryGuideIsInTheSidebar()
    {
        var guides = Path.Combine(RepositoryLayout.Root(), "docs", "guides");
        var pages = Directory.GetFiles(guides, "*.md")
            .Select(p => Path.GetFileName(p))
            .Order(StringComparer.Ordinal)
            .ToList();

        var sidebar = File.ReadAllText(Path.Combine(guides, "toc.yml"));
        var hrefs = Regex.Matches(sidebar, @"^\s*href:\s*(\S+\.md)\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(pages, hrefs);
    }
}
