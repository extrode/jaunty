using System.Xml.Linq;

namespace Jaunty.Tests.Unit;

/// <summary>
/// A test project that is not listed in <c>Jaunty.slnx</c> is invisible: CI restores, builds and
/// tests the solution, so the project is never compiled and its tests never run - and nothing
/// fails, which is the whole problem. <c>tests/Jaunty.Fluent.SourceGen.Tests</c> sat outside the
/// solution from the day spec 003 created it until round 31 noticed, surviving thirty audit rounds
/// because no assertion covered the solution file itself.
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
