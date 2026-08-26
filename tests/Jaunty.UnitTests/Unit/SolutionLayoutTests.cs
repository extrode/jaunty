using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Jaunty.Tests.Unit;

/// <summary>
/// A test suite nothing invokes is invisible, and invisibility is silent at every layer: a project
/// missing from <c>Jaunty.slnx</c> is skipped by solution-wide build and test without a warning,
/// and a <c>--filter</c> that selects nothing prints "No test matches" and exits 0 - the same green
/// as a passing run. <c>tests/Jaunty.Fluent.SourceGen.Tests</c> was outside the solution from the
/// day spec 003 created it, and neither it nor <c>tests/Jaunty.Scaffolding.Cli.Tests</c> had a CI
/// step: 51 tests that had never run in CI on any platform, through thirty-one audit rounds that
/// read both projects' sources and never asked what ran them.
/// </summary>
public class SolutionLayoutTests
{
    [Fact]
    public void EveryTestProjectOnDiskIsListedInTheSolution()
    {
        DirectoryInfo repoRoot = LocateRepositoryRoot();
        string solutionPath = Path.Combine(repoRoot.FullName, "Jaunty.slnx");

        HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement project in XDocument.Load(solutionPath).Descendants("Project"))
        {
            string? path = (string?)project.Attribute("Path");
            if (!string.IsNullOrEmpty(path))
                listed.Add(Normalize(path!));
        }

        string testsRoot = Path.Combine(repoRoot.FullName, "tests");
        List<string> missing = new();

        foreach (string file in Directory.GetFiles(testsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string relative = Normalize(file.Substring(repoRoot.FullName.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (relative.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relative.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (!listed.Contains(relative))
                missing.Add(relative);
        }

        Assert.True(missing.Count == 0,
            "These test projects exist on disk but are absent from Jaunty.slnx, so `dotnet build` and " +
            "`dotnet test` on the solution skip them silently and CI never runs their tests. Add each to " +
            "the /tests/ folder in Jaunty.slnx: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The CI workflow runs named projects, not the solution, so being in <c>Jaunty.slnx</c> only
    /// gets a suite compiled - a step has to name it or it still never executes. Both net8.0 and
    /// net10.0 legs count, hence the two-invocation requirement: adding a suite to one leg only is
    /// the next version of this bug.
    /// </summary>
    [Fact]
    public void EveryTestProjectInTheSolutionIsInvokedByBothCiFrameworkLegs()
    {
        DirectoryInfo repoRoot = LocateRepositoryRoot();
        string workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "ci.yml");
        string workflow = File.ReadAllText(workflowPath);

        // Filters match on a substring of the fully qualified name, so "~Jaunty.FlatFiles" covers
        // both FlatFiles suites - and, exactly as this bug went, "~Jaunty.Fluent.Tests" covers
        // Jaunty.Fluent.Tests and nothing else.
        List<string> filterTokens = new();
        foreach (Match match in Regex.Matches(workflow, @"FullyQualifiedName~([A-Za-z0-9_.]+)"))
            filterTokens.Add(match.Groups[1].Value);

        List<string> uninvoked = new();

        foreach (string project in SolutionTestProjectNames(repoRoot))
        {
            int invocations = Regex.Matches(
                workflow,
                "tests/" + Regex.Escape(project) + "(?![A-Za-z0-9_./])").Count;

            foreach (string token in filterTokens)
            {
                if (project.Equals(token, StringComparison.Ordinal) ||
                    project.StartsWith(token + ".", StringComparison.Ordinal))
                    invocations++;
            }

            if (invocations < 2)
                uninvoked.Add($"{project} (invoked {invocations}x, expected one per framework leg)");
        }

        Assert.True(uninvoked.Count == 0,
            "These suites are in Jaunty.slnx but .github/workflows/ci.yml does not run them on both " +
            "the net8.0 and net10.0 legs, so their tests do not execute in CI - silently, because a " +
            "filter matching nothing still exits 0. Add a step naming the project, or a filter whose " +
            "token prefixes its namespace: " + string.Join(", ", uninvoked));
    }

    /// <summary>Project names under <c>tests/</c> as listed in the solution file.</summary>
    private static IEnumerable<string> SolutionTestProjectNames(DirectoryInfo repoRoot)
    {
        string solutionPath = Path.Combine(repoRoot.FullName, "Jaunty.slnx");

        foreach (XElement project in XDocument.Load(solutionPath).Descendants("Project"))
        {
            string path = Normalize((string?)project.Attribute("Path") ?? string.Empty);
            if (!path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return path.Split('/')[1];
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>Walks up from the test binary to the directory holding <c>Jaunty.slnx</c>.</summary>
    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Jaunty.slnx")))
                return dir;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate Jaunty.slnx walking up from '{AppContext.BaseDirectory}'.");
    }
}
