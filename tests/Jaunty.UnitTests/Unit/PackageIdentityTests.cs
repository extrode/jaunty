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
    private const string ExpectedProjectUrl = "https://extrode.com/jaunty";
    private const string ExpectedIdPrefix = "Extrode.Jaunty";
    private const string ExpectedOwner = "Extrode LLC";
    private const int ExpectedPackageCount = 8;

    private static readonly string[] RetiredOwnerNames = ["Beparey LLC", "Beparey.com"];

    private static readonly string[] PackCriticalProperties =
    [
        "PackageId", "PackAsTool", "ToolCommandName", "RepositoryUrl", "PackageProjectUrl",
        "Authors", "Company", "Copyright", "Description", "Version", "PackageVersion",
        "PackageLicenseFile", "PackageReadmeFile", "PackageIcon",
    ];

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
    public void EveryPackCriticalPropertyIsDeclaredUnconditionally()
    {
        List<string> conditioned = new();

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement property in document.Descendants()
                         .Where(e => PackCriticalProperties.Contains(e.Name.LocalName) &&
                                     e.Parent?.Name.LocalName == "PropertyGroup"))
            {
                XElement? carrier = ConditioningAncestor(property);

                if (carrier is not null)
                {
                    conditioned.Add(
                        $"{project}: {property.Name.LocalName} under <{carrier.Name.LocalName} " +
                        $"Condition=\"{(string?)carrier.Attribute("Condition")}\">");
                }
            }
        }

        Assert.True(conditioned.Count == 0,
            "Pack metadata must sit in an unconditional PropertyGroup. Pack evaluates a multi-targeted " +
            "project with an empty $(TargetFramework), so a TFM-conditioned property is dropped and the " +
            "package silently falls back to a default - for PackageId, the assembly name: " +
            string.Join(", ", conditioned));
    }

    [Fact]
    public void EveryPackableProjectDeclaresAPackageId()
    {
        List<string> undeclared = new();
        int declared = 0;

        foreach ((string project, XDocument document) in SourceProjects())
        {
            if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                continue;

            bool packable = !document.Descendants()
                                     .Any(e => e.Name.LocalName == "IsPackable" &&
                                               string.Equals(e.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));

            if (!packable)
                continue;

            if (document.Descendants("PackageId").Any())
                declared++;
            else
                undeclared.Add(project);
        }

        Assert.True(undeclared.Count == 0,
            "A packable project that declares no PackageId packs under its assembly name, losing the " +
            $"'{ExpectedIdPrefix}' prefix without any build error. Either declare a PackageId or set " +
            "<IsPackable>false</IsPackable>: " + string.Join(", ", undeclared));

        Assert.True(declared >= ExpectedPackageCount,
            $"Expected at least {ExpectedPackageCount} packable projects declaring a PackageId but found " +
            $"{declared}. Every other fact here iterates the elements it finds, so a mass deletion or " +
            "rename would leave them green while shipping nothing.");
    }

    /// <summary>
    /// The two URLs are different things and must not drift back together: <c>RepositoryUrl</c> is
    /// the source remote and feeds Source Link, while <c>PackageProjectUrl</c> is the product page
    /// NuGet renders for buyers.
    /// </summary>
    [Fact]
    public void EveryRepositoryAndProjectUrlPointsAtTheExtrodeRemote()
    {
        List<string> stale = new();
        int repositoryUrls = 0;
        int projectUrls = 0;

        foreach ((string project, XDocument document) in SourceProjects())
        {
            foreach (XElement url in document.Descendants()
                         .Where(e => e.Name.LocalName is "RepositoryUrl" or "PackageProjectUrl"))
            {
                bool repository = url.Name.LocalName == "RepositoryUrl";
                string expected = repository ? ExpectedRepositoryUrl : ExpectedProjectUrl;

                if (repository)
                    repositoryUrls++;
                else
                    projectUrls++;

                if (!string.Equals(url.Value, expected, StringComparison.Ordinal))
                    stale.Add($"{project} has {url.Name.LocalName} '{url.Value}', expected '{expected}'");
            }
        }

        Assert.True(stale.Count == 0,
            $"RepositoryUrl must be '{ExpectedRepositoryUrl}' and PackageProjectUrl must be " +
            $"'{ExpectedProjectUrl}': " + string.Join(", ", stale));

        Assert.True(repositoryUrls >= ExpectedPackageCount,
            $"Expected at least {ExpectedPackageCount} RepositoryUrl declarations but found {repositoryUrls}.");

        Assert.True(projectUrls >= 1,
            "No PackageProjectUrl was found anywhere under src/. This fact only validates the elements " +
            "it finds, so deleting the shared declaration would otherwise leave it green while every " +
            "package shipped with no product page.");
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

    /// <summary>
    /// The licensing documents name the same entity as the packages. They drift because nothing
    /// compiles them: the order form is copied per customer and the ISL-R ships in the repo, so a
    /// stale licensor there contradicts the copyright in every shipped assembly.
    /// </summary>
    [Theory]
    [InlineData("LICENSE.md")]
    [InlineData("docs/06-releases/order-form-template.md")]
    public void EveryLicensingDocumentNamesTheCompanyAsLicensor(string relativePath)
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, relativePath);

        Assert.True(File.Exists(path), $"'{relativePath}' is missing; this fact cannot pass vacuously.");

        string text = File.ReadAllText(path);

        Assert.True(text.IndexOf(ExpectedOwner, StringComparison.Ordinal) >= 0,
            $"'{relativePath}' must name '{ExpectedOwner}' as licensor to match the package copyright.");

        foreach (string retired in RetiredOwnerNames)
        {
            Assert.True(text.IndexOf(retired, StringComparison.OrdinalIgnoreCase) < 0,
                $"'{relativePath}' still names the retired licensor '{retired}'.");
        }
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

    /// <summary>
    /// The nearest ancestor (or the element itself) carrying a <c>Condition</c>, or <c>null</c> when the
    /// property is evaluated unconditionally. Checking only <c>Parent</c> misses an element-level
    /// condition and a <c>Choose</c>/<c>When</c> wrapper, both of which drop the property just as silently.
    /// </summary>
    private static XElement? ConditioningAncestor(XElement property) =>
        property.AncestorsAndSelf()
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace((string?)e.Attribute("Condition")));

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
