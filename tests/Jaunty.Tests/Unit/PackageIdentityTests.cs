using System.Xml.Linq;

namespace Jaunty.Tests.Unit;

/// <summary>
/// Package metadata fails silently: a <c>PackageId</c> that never applies does not error, it
/// produces a package named after the assembly instead. <c>Jaunty.Scaffolding.Cli</c> shipped that
/// way - its <c>PackageId</c> sat in a <c>PropertyGroup</c> conditioned on
/// <c>'$(TargetFramework)' == 'net10.0'</c>, and because pack evaluates the outer build of a
/// multi-targeted project with an empty <c>$(TargetFramework)</c>, the condition was false and the
/// declared id was discarded. The only symptom was the filename in the output directory.
/// </summary>
public class PackageIdentityTests
{
    private const string ExpectedRepositoryUrl = "https://github.com/extrode/jaunty";
    private const string ExpectedIdPrefix = "Extrode.Jaunty";
    private const string ExpectedOwner = "Extrode LLC";

    [Fact]
    public void EveryDeclaredPackageIdCarriesTheExtrodePrefix()
    {
        List<string> wrong = new();

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement id in document.Descendants("PackageId"))
            {
                if (!id.Value.StartsWith(ExpectedIdPrefix, StringComparison.Ordinal))
                    wrong.Add($"{project} declares PackageId '{id.Value}'");
            }
        }

        Assert.True(wrong.Count == 0,
            $"Published package ids must start with '{ExpectedIdPrefix}' after the move to the " +
            "extrode organisation: " + string.Join(", ", wrong));
    }

    [Fact]
    public void EveryPackageIdIsDeclaredUnconditionally()
    {
        List<string> conditioned = new();

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement id in document.Descendants("PackageId"))
            {
                string? condition = (string?)id.Parent?.Attribute("Condition");

                if (!string.IsNullOrWhiteSpace(condition))
                    conditioned.Add($"{project} (PropertyGroup Condition=\"{condition}\")");
            }
        }

        Assert.True(conditioned.Count == 0,
            "PackageId must sit in an unconditional PropertyGroup. Pack evaluates a multi-targeted " +
            "project with an empty $(TargetFramework), so a TFM-conditioned PackageId is dropped and " +
            "the package silently takes the assembly name instead: " + string.Join(", ", conditioned));
    }

    [Fact]
    public void EveryRepositoryAndProjectUrlPointsAtTheExtrodeRemote()
    {
        List<string> stale = new();

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement url in document.Descendants()
                         .Where(e => e.Name.LocalName is "RepositoryUrl" or "PackageProjectUrl"))
            {
                if (!string.Equals(url.Value, ExpectedRepositoryUrl, StringComparison.Ordinal))
                    stale.Add($"{project} has {url.Name.LocalName} '{url.Value}'");
            }
        }

        Assert.True(stale.Count == 0,
            $"Package metadata must point at '{ExpectedRepositoryUrl}': " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryAttributionNamesTheCompany()
    {
        List<string> misattributed = new();

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement element in document.Descendants()
                         .Where(e => e.Name.LocalName is "Authors" or "Company" or "Copyright"))
            {
                if (element.Value.IndexOf(ExpectedOwner, StringComparison.Ordinal) < 0)
                    misattributed.Add($"{project} has {element.Name.LocalName} '{element.Value}'");
            }
        }

        Assert.True(misattributed.Count == 0,
            $"Shipped assembly and package attribution must name '{ExpectedOwner}' rather than an " +
            "individual: " + string.Join(", ", misattributed));
    }

    /// <summary>Every <c>.csproj</c> under <c>src/</c>, plus <c>src/Directory.Build.props</c>.</summary>
    private static IEnumerable<(string Project, XDocument Document)> SourceProjects()
    {
        DirectoryInfo repoRoot = LocateRepositoryRoot();
        string sourceRoot = Path.Combine(repoRoot.FullName, "src");

        List<string> files = new(Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories));
        files.Add(Path.Combine(sourceRoot, "Directory.Build.props"));

        foreach (string file in files)
        {
            string relative = file.Substring(repoRoot.FullName.Length)
                                  .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  .Replace('\\', '/');

            if (relative.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relative.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            yield return (relative, XDocument.Load(file));
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
