using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>Holds the README to the code (#23).
///
/// <para><b>Why this is a test rather than a habit.</b> The README is packed into the NuGet package
/// (<c>PackageReadmeFile</c>), so it is not a repo file a consumer might read — it is the package's front page.
/// It had drifted four ways by the time this was written: it listed <c>Company.TryGetAllSharesFloatAsync</c> as
/// shipped after that member was deleted, described <c>TryGetListAsync</c> returning null in one paragraph and
/// explained two paragraphs later that no <c>Try</c>-prefixed method exists anywhere, showed an escape-hatch
/// example calling that deleted method, and reported four endpoints as "not started" after they shipped. Every
/// one of those is a doc that a reader cannot tell is wrong.</para>
///
/// <para><b>The coverage table is discovered by driving the code, not by reading it.</b> Each public endpoint
/// method is invoked against a stub and the path it actually requests is recorded. The obvious alternative —
/// grepping the sources for <c>new FmpRequest("…")</c> — was tried first and silently missed thirteen endpoints:
/// the seven period-shaped statements go through <c>StatementEndpoints.Periodic</c> and six bulk statement
/// variants through <c>BulkEndpoints.Periodic</c>, so their paths are arguments to a helper rather than literals
/// at a construction site. A text scan that is wrong produces a table that looks complete, which is the exact
/// failure this file exists to prevent.</para></summary>
public partial class EndpointCoverageTests
{
    /// <summary>FMP documents 263 APIs over 230 distinct paths — the asset-class sections (Indexes, Commodity,
    /// Forex, Crypto) re-document <c>/stable/quote</c> and <c>/stable/historical-price-eod</c> rather than adding
    /// endpoints. The denominator is the unique-path count, because that is what a client implements.</summary>
    private const int DocumentedPaths = 230;

    private const string BeginMarker = "<!-- BEGIN GENERATED: endpoint coverage -->";
    private const string EndMarker = "<!-- END GENERATED: endpoint coverage -->";

    /// <summary>Set to rewrite the README's generated block instead of asserting against it.</summary>
    private const string UpdateVariable = "FMPDOTNET_UPDATE_README";

    private sealed record Modelled(string Group, string Method, string Path);

    // ---- the generated coverage table ---------------------------------------------------------------------------

    [Fact]
    public void The_coverage_table_in_the_readme_matches_the_endpoints_the_code_actually_calls()
    {
        var expected = Render(Discover());
        var path = ReadmePath();
        var readme = File.ReadAllText(path);

        var start = readme.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = readme.IndexOf(EndMarker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{path} is missing the generated coverage block markers.");

        var actual = readme[start..(end + EndMarker.Length)];
        if (actual == expected) return;

        if (Environment.GetEnvironmentVariable(UpdateVariable) is not (null or ""))
        {
            File.WriteAllText(path, string.Concat(readme[..start], expected, readme[(end + EndMarker.Length)..]));
            return;
        }

        Assert.Fail(
            $"""
             The README's endpoint coverage table no longer matches the code.

             Regenerate it, then commit the result:

                 {UpdateVariable}=1 dotnet test

             ---- expected ----
             {expected}
             ---- found ----
             {actual}
             """);
    }

    [Fact]
    public void Every_public_endpoint_method_reaches_the_api()
    {
        // The coverage table is built from the requests the methods make, so a method that makes none would be
        // absent from the table without failing anything — the table would look complete while under-reporting
        // the surface. This is the guard for that: discovery must account for every public *Async method.
        var discovered = Discover().Select(m => $"{m.Group}.{m.Method}").ToHashSet(StringComparer.Ordinal);

        var silent = Groups()
            .SelectMany(g => EndpointMethods(g.Type).Select(m => $"{g.Name}.{m.Name}"))
            .Where(m => !discovered.Contains(m))
            .ToList();

        Assert.True(silent.Count == 0,
            "These endpoint methods issued no request under the test's synthesised arguments, so they are " +
            "missing from the README coverage table:\n  " + string.Join("\n  ", silent));
    }

    // ---- the rest of the README ---------------------------------------------------------------------------------

    [Fact]
    public void Every_sdk_member_the_readme_names_still_exists()
    {
        // This is the check that would have caught `Company.TryGetAllSharesFloatAsync` surviving the member's
        // deletion, and the escape-hatch example calling `transport.TryGetListAsync`. Prose and code samples are
        // both scanned: the stale row lived in a table, the broken call in a fenced example.
        var readme = File.ReadAllText(ReadmePath());
        var groups = Groups().ToDictionary(g => g.Name, g => g.Type, StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (Match match in BacktickedMember().Matches(readme))
        {
            // Only a name that IS one of the client's groups is checked. `JsonSerializer.DeserializeAsync` and
            // friends match the same shape, and this test has no business asserting on the BCL.
            var group = match.Groups["group"].Value;
            if (groups.TryGetValue(group, out var type) && !HasMember(type, match.Groups["member"].Value))
                missing.Add($"{match.Value} — {type.Name} has no {match.Groups["member"].Value}");
        }

        foreach (Match match in CalledMember().Matches(readme))
        {
            var member = match.Groups["member"].Value;
            if (match.Groups["transport"].Success)
            {
                if (!HasMember(typeof(FmpTransport), member))
                    missing.Add($"transport.{member} — FmpTransport has no {member}");
                continue;
            }

            var group = match.Groups["group"].Value;
            if (!groups.TryGetValue(group, out var type))
                missing.Add($"fmp.{group} — FmpClient has no {group} property");
            else if (!HasMember(type, member))
                missing.Add($"fmp.{group}.{member} — {type.Name} has no {member}");
        }

        Assert.True(missing.Count == 0,
            "The README names SDK members that no longer exist:\n  " + string.Join("\n  ", missing));
    }

    /// <summary>A member reference written as <c>`Group.MemberAsync`</c> anywhere in the prose.</summary>
    [GeneratedRegex(@"`(?<group>[A-Z]\w*)\.(?<member>\w+Async)`")]
    private static partial Regex BacktickedMember();

    /// <summary>A call written against the client or the transport in a code sample.</summary>
    [GeneratedRegex(@"\b(?:fmp\.(?<group>[A-Z]\w*)|(?<transport>transport))\.(?<member>\w+)\s*(?:<[^>()]*>)?\(")]
    private static partial Regex CalledMember();

    private static bool HasMember(Type type, string name) =>
        type.GetMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length > 0;

    // ---- discovery ----------------------------------------------------------------------------------------------

    /// <summary>Invokes every public endpoint method against a stub and records the path each one requests.</summary>
    private static IReadOnlyList<Modelled> Discover()
    {
        var modelled = new List<Modelled>();

        foreach (var (name, type) in Groups())
        {
            foreach (var method in EndpointMethods(type))
            {
                // A fresh stub per method, so `Requests` holds only what this method asked for.
                var (endpoints, handler) = Build(type);

                // Driven once per combination of enum arguments, not once per method, because an enum can select
                // the PATH rather than a query value. ChartEndpoints.GetIntradayAsync is one method over six
                // paths: driving it once recorded `historical-chart/1min` and left the other five intervals out
                // of the table entirely — reachable code the generated coverage claimed was not there, which is
                // exactly the drift this file exists to prevent. Enums that only select a query value, such as
                // FiscalPeriod, produce the same path each time and are deduplicated below.
                foreach (var arguments in ArgumentSets(method))
                    Drive(method, endpoints, arguments);

                foreach (var path in handler.Requests
                             .Select(uri => uri.AbsolutePath.TrimStart('/'))
                             .Distinct(StringComparer.Ordinal))
                {
                    modelled.Add(new Modelled(name, method.Name, path));
                }
            }
        }

        return modelled;
    }

    private static IEnumerable<(string Name, Type Type)> Groups() =>
        typeof(FmpClient).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    private static IEnumerable<MethodInfo> EndpointMethods(Type group) =>
        group.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal);

    private static (object Endpoints, StubHandler Handler) Build(Type group)
    {
        // Every failure answer is HTTP 400. It has to be a failure: `StreamAllProfilesAsync` walks parts upward
        // until one errors, so a stub that succeeded forever would never terminate. 400 specifically, because the
        // walk treats it as "past the last part" — and rethrows it on part 0, which is the request being recorded.
        var handler = new StubHandler(StubHandler.Status(HttpStatusCode.BadRequest));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };

        var constructor = group.GetConstructors().Single();
        var transport = Activator.CreateInstance(
            constructor.GetParameters()[0].ParameterType,
            http,
            Options.Create(new FmpOptions { ApiKey = "k" }));

        return (constructor.Invoke([transport]), handler);
    }

    /// <summary>Every argument list this method should be driven with — the cross product of its enum parameters,
    /// with every other parameter fixed by <see cref="Argument"/>.
    ///
    /// <para>One list for a method with no enum parameters, which is nearly all of them. Six for
    /// <c>GetIntradayAsync</c>, whose <see cref="ChartInterval"/> chooses the path segment.</para></summary>
    private static IEnumerable<object[]> ArgumentSets(MethodInfo method)
    {
        var parameters = method.GetParameters();
        IEnumerable<object[]> sets = [[]];

        foreach (var parameter in parameters)
        {
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            var choices = type.IsEnum
                ? Enum.GetValues(type).Cast<object>().ToArray()
                : [Argument(parameter)];

            sets = sets.SelectMany(_ => choices, (set, choice) => (object[])[.. set, choice]);
        }

        return sets;
    }

    private static void Drive(MethodInfo method, object endpoints, object[] arguments)
    {
        object? result;
        try
        {
            result = method.Invoke(endpoints, arguments);
        }
        catch (TargetInvocationException)
        {
            return; // Rejected its arguments before requesting anything — nothing to record.
        }

        // What the call returns is discarded on purpose: the stub answers an error, and the only thing being
        // observed is which path went out. Every exception below is that error arriving as designed.
        try
        {
            switch (result)
            {
                case Task task:
                    task.GetAwaiter().GetResult();
                    break;
                case not null:
                    var item = result.GetType().GetInterfaces()
                        .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                        .GetGenericArguments()[0];
                    ((Task)PullOne.MakeGenericMethod(item).Invoke(null, [result])!).GetAwaiter().GetResult();
                    break;
            }
        }
        catch (Exception)
        {
            // Expected: the stub refuses every request.
        }
    }

    private static readonly MethodInfo PullOne =
        typeof(EndpointCoverageTests).GetMethod(nameof(PullOneAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Starts a stream and stops at the first element, which is enough to make the request.</summary>
    private static async Task PullOneAsync<T>(IAsyncEnumerable<T> rows)
    {
        await using var enumerator = rows.GetAsyncEnumerator(CancellationToken.None);
        await enumerator.MoveNextAsync();
    }

    /// <summary>Supplies a valid argument for a parameter, keyed by name where the type alone is ambiguous.
    ///
    /// <para>Unknown types throw rather than defaulting. A new parameter type on a new endpoint would otherwise
    /// pass <see langword="null"/> or zero into a validated method, the call would throw before requesting
    /// anything, and the endpoint would drop silently out of the coverage table.</para></summary>
    private static object Argument(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

        if (type == typeof(CancellationToken)) return CancellationToken.None;

        // Name-dispatched for the same reason as Probe.Argument, though the stakes are lower here: this harness
        // only records which path went out, not response content, so a meaningless-but-valid value is harmless —
        // "exchange" and "query" fall through to the AAPL default below rather than getting their own case, and
        // that is fine for what this harness checks.
        if (type == typeof(string))
        {
            return parameter.Name switch
            {
                "cik" => "320193",
                "cusip" => "037833100",
                "isin" => "US0378331005",
                _ => "AAPL",
            };
        }

        // The batch-quote endpoints. Two symbols rather than one, and non-blank: the methods reject a list that
        // would reach FMP empty, and a rejected call requests nothing and so records no path.
        if (type == typeof(IEnumerable<string>)) return new[] { "AAPL", "MSFT" };
        if (type == typeof(bool)) return false;
        if (type == typeof(LocalDate)) return new LocalDate(2026, 1, 2);
        if (type == typeof(ScreenerCriteria)) return new ScreenerCriteria();
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0)!;
        if (type == typeof(int))
        {
            return parameter.Name switch
            {
                "year" => 2025,
                // Under the delisted archive's hard cap of 100, and positive — both are validated.
                "limit" => 5,
                _ => 0, // page, part
            };
        }

        throw new NotSupportedException(
            $"EndpointCoverageTests cannot supply a {parameter.ParameterType.Name} for '{parameter.Name}'. " +
            "Add a case above, or the endpoint declaring it will be missing from the README coverage table.");
    }

    // ---- rendering ----------------------------------------------------------------------------------------------

    private static string Render(IReadOnlyList<Modelled> modelled)
    {
        var paths = modelled.Select(m => m.Path).Distinct(StringComparer.Ordinal).Count();

        var markdown = new StringBuilder()
            .AppendLine(BeginMarker)
            .AppendLine("<!-- Generated from the code by EndpointCoverageTests. Do not edit by hand — run")
            .AppendLine($"     `{UpdateVariable}=1 dotnet test` and commit the result. -->")
            .AppendLine()
            .AppendLine($"**{paths} of FMP's {DocumentedPaths} endpoint paths are modelled.**")
            .AppendLine();

        foreach (var group in modelled.GroupBy(m => m.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            markdown
                .AppendLine($"`fmp.{group.Key}`")
                .AppendLine()
                .AppendLine("| FMP endpoint | Method |")
                .AppendLine("|---|---|");

            foreach (var path in group.GroupBy(m => m.Path).OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var methods = path
                    .Select(m => m.Method)
                    .OrderBy(m => m, StringComparer.Ordinal)
                    .Select(m => $"`{m}`");
                markdown.AppendLine($"| `{path.Key}` | {string.Join(", ", methods)} |");
            }

            markdown.AppendLine();
        }

        return markdown.Append(EndMarker).ToString().ReplaceLineEndings("\n");
    }

    /// <summary>Locates the README from this file's compile-time path, so the test does not depend on the working
    /// directory a runner happens to choose.</summary>
    private static string ReadmePath([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FmpDotNet.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "README.md");
    }
}
