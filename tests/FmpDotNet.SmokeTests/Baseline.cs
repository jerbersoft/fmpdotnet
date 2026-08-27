using System.Runtime.CompilerServices;
using System.Text;
using NodaTime;

namespace FmpDotNet.SmokeTests;

/// <summary>One endpoint's recorded shape, as it stands in a baseline file.</summary>
/// <param name="Name">Group and method, e.g. <c>Statements.GetIncomeStatementAsync</c>.</param>
/// <param name="Outcome">The outcome recorded when it was last measured.</param>
/// <param name="Set">Properties that were populated.</param>
/// <param name="Unset">Properties that were not.</param>
public sealed record Recorded(string Name, string Outcome, IReadOnlyList<string> Set, IReadOnlyList<string> Unset);

/// <summary>Reads and writes the checked-in record of what the live API answered.
///
/// <para><b>The file is a measurement, not a specification.</b> Nothing in it was decided; every line was
/// observed on the date in its header and written down. That is why the update path is a switch rather than a
/// hand edit — a baseline someone adjusted to make a test pass has stopped being evidence of anything.</para>
///
/// <para>The format is one property per line so that a rename is a one-line diff. A denser encoding — a
/// comma-joined list, or JSON — would show the same change as a rewritten paragraph, and the whole value of
/// checking this in is that a reviewer can see at a glance which single field stopped arriving.</para></summary>
internal static class Baseline
{
    private const string SetPrefix = "set";
    private const string NullPrefix = "null";
    private const string OutcomePrefix = "outcome";

    /// <summary>Locates a baseline beside this source file, so the tests do not depend on which working
    /// directory a runner chooses — and so the update path rewrites the tree, not a copy under <c>bin</c>.</summary>
    public static string Path(string file, [CallerFilePath] string here = "") =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, file);

    public static IReadOnlyDictionary<string, Recorded> Read(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, Recorded>(StringComparer.Ordinal);

        var recorded = new Dictionary<string, Recorded>(StringComparer.Ordinal);
        string? name = null;
        var outcome = "";
        List<string> set = [], unset = [];

        void Flush()
        {
            if (name is not null) recorded[name] = new Recorded(name, outcome, set, unset);
            outcome = "";
            set = [];
            unset = [];
        }

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                Flush();
                name = line[1..^1];
                continue;
            }

            var split = line.IndexOf(' ');
            var (key, value) = split < 0 ? (line, "") : (line[..split], line[(split + 1)..].Trim());
            switch (key)
            {
                case OutcomePrefix: outcome = value; break;
                case SetPrefix: set.Add(value); break;
                case NullPrefix: unset.Add(value); break;
                // Rejected rather than skipped: a line this parser does not understand is a line whose
                // assertion silently stops being made, which is the failure mode a baseline cannot afford.
                default:
                    throw new FormatException(
                        $"{path}: unrecognised line '{raw}'. Expected '{OutcomePrefix}', '{SetPrefix}' or "
                        + $"'{NullPrefix}', a [Group.Method] heading, a # comment, or a blank line.");
            }
        }

        Flush();
        return recorded;
    }

    public static string Render(IReadOnlyList<Observation> observations, LocalDate measured, string heading)
    {
        var markdown = new StringBuilder(heading.TrimEnd())
            .AppendLine()
            .AppendLine("#")
            .AppendLine($"# Measured {measured:uuuu-MM-dd}. Regenerate — after checking the diff is drift and not")
            .AppendLine("# a break — with:")
            .AppendLine("#")
            .AppendLine($"#     {LiveApi.KeyVariable}=… {LiveApi.UpdateVariable}=1 \\")
            .AppendLine("#         dotnet test tests/FmpDotNet.SmokeTests")
            .AppendLine("#")
            .AppendLine($"# `{SetPrefix}` means the property carried a value on at least one row FMP returned.")
            .AppendLine($"# `{NullPrefix}` means it was null, blank or empty on every one of them.")
            .AppendLine();

        foreach (var observation in observations.OrderBy(o => o.Name, StringComparer.Ordinal))
        {
            markdown.AppendLine($"[{observation.Name}]").AppendLine($"{OutcomePrefix} {observation.Outcome}");
            foreach (var property in observation.Set.Order(StringComparer.Ordinal))
                markdown.AppendLine($"{SetPrefix} {property}");
            foreach (var property in observation.Unset.Order(StringComparer.Ordinal))
                markdown.AppendLine($"{NullPrefix} {property}");
            markdown.AppendLine();
        }

        return markdown.ToString().ReplaceLineEndings("\n");
    }
}
