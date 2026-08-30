using System.Reflection;
using System.Text.RegularExpressions;

namespace Jaunty.Tests.Unit;

/// <summary>
/// Documentation drifts silently: nothing fails when a page describes a type that was renamed,
/// or one that was designed and never built. <c>docs/01-api-reference/configuration.md</c>
/// carried a full "NamingConvention Class" section - five methods, signatures and worked
/// examples - for a class that has never existed in <c>src/</c>, and the README repeated it.
/// Found 2026-08-31 while checking the docs before the repository went public.
/// </summary>
public class DocumentedApiTests
{
    private static readonly string[] NeverExisted =
    [
        "NamingConvention",
        "ExecuteStoredProcedureWithOutput",
    ];

    private static readonly string[] ScannedRoots = ["README.md", "SECURITY.md", "docs"];

    private static readonly string[] SkippedPathFragments =
    [
        "/docs/99-archive/", "/docs/plans/", "/docs/specs/", "/docs/decisions/",
    ];

    [Fact]
    public void NoPublishedPageClaimsAnApiThatWasNeverBuilt()
    {
        List<string> offences = new();

        foreach ((string relative, string text) in PublishedMarkdown())
        {
            foreach (string name in NeverExisted)
            {
                foreach (Match match in Regex.Matches(text, $@"\b{Regex.Escape(name)}\b"))
                {
                    int line = text.Take(match.Index).Count(c => c == '\n') + 1;

                    if (IsCorrectiveMention(text, match.Index))
                        continue;

                    offences.Add($"{relative}:{line} cites '{name}'");
                }
            }
        }

        Assert.True(offences.Count == 0,
            "Published documentation cites API that does not exist in src/:" +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void TheNamesThoseSectionsWereRewrittenToUseAreReal()
    {
        Type config = typeof(global::Jaunty.Configuration.JauntyConfig);

        Assert.NotNull(config.GetProperty("SchemaNameResolver", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(config.GetProperty("TableNameResolver", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(config.GetProperty("ColumnNameResolver", BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    /// The rewritten pages claim a resolver set after a type has been read is picked up rather
    /// than ignored, which rests on the generation counter moving. This asserts the counter, not
    /// <c>JauntyConfig</c> itself: this assembly is the parallel half of the suite, and assigning
    /// a global static here would race whatever else is running.
    /// </summary>
    [Fact]
    public void InvalidatingTheConfigurationGenerationMovesTheCounter()
    {
        int before = global::Jaunty.Internals.ConfigurationGeneration.Current;

        global::Jaunty.Internals.ConfigurationGeneration.Invalidate();

        Assert.NotEqual(before, global::Jaunty.Internals.ConfigurationGeneration.Current);
    }

    /// <summary>
    /// A page that says a name was wrong is not itself citing the name as API. The corrective
    /// notes are written as blockquotes, and the sketch in the design notes as a
    /// comment, so both are recognised by the marker on the line or the block above it.
    /// </summary>
    private static bool IsCorrectiveMention(string text, int index)
    {
        int start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        string line = text.Substring(start, text.IndexOf('\n', index) is int e && e > start
            ? e - start
            : text.Length - start);

        if (line.TrimStart().StartsWith(">", StringComparison.Ordinal)) return true;
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) return true;

        int blockStart = Math.Max(0, index - 400);
        string preceding = text.Substring(blockStart, index - blockStart);

        return preceding.Contains("never existed", StringComparison.OrdinalIgnoreCase)
            || preceding.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || preceding.Contains("Sketch only", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Relative, string Text)> PublishedMarkdown()
    {
        DirectoryInfo repoRoot = LocateRepositoryRoot();

        foreach (string root in ScannedRoots)
        {
            string path = Path.Combine(repoRoot.FullName, root);

            IEnumerable<string> files = File.Exists(path)
                ? [path]
                : Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.md", SearchOption.AllDirectories)
                    : [];

            foreach (string file in files)
            {
                string relative = file.Substring(repoRoot.FullName.Length)
                                      .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                      .Replace('\\', '/');

                if (SkippedPathFragments.Any(f => ("/" + relative).Contains(f, StringComparison.OrdinalIgnoreCase)))
                    continue;

                yield return (relative, File.ReadAllText(file));
            }
        }
    }

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
