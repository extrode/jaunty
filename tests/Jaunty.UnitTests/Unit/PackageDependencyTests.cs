using System.Xml.Linq;

namespace Jaunty.Tests.Unit;

/// <summary>
/// The core package advertises zero dependencies on net8.0 and net10.0. Nothing in the build
/// enforces that: adding a <c>PackageReference</c> to <c>src/Jaunty</c> succeeds silently and the
/// only symptom is a dependency group in the shipped nuspec that nobody looks at. These facts
/// pin the contract at the csproj, which is where the mistake gets made.
/// </summary>
public class PackageDependencyTests
{
    private const string CoreProject = "src/Jaunty/Jaunty.csproj";
    private const string LoggingProject = "src/Jaunty.Extensions.Logging/Jaunty.Extensions.Logging.csproj";

    /// <summary>
    /// Both are backports of types that ship inside <c>Microsoft.NETCore.App</c> on modern .NET,
    /// which is why they are tolerated on the one target that predates them.
    /// </summary>
    private static readonly string[] AllowedNetStandardBackports =
    [
        "System.Diagnostics.DiagnosticSource",
        "Microsoft.Bcl.AsyncInterfaces",
    ];

    private static readonly string[] LoggingSatellitePackages =
    [
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
    ];

    [Fact]
    public void CoreReferencesNothingButTheNetStandardBackports()
    {
        List<string> offenders = new();
        int inspected = 0;

        foreach ((string id, string condition) in PackageReferences(CoreProject))
        {
            inspected++;

            if (!AllowedNetStandardBackports.Contains(id))
                offenders.Add($"'{id}' (condition: {Describe(condition)})");
        }

        Assert.True(offenders.Count == 0,
            "Extrode.Jaunty must ship with no dependencies on net8.0/net10.0 and only BCL backports " +
            "on netstandard2.0. Put integration code in a satellite package instead: " +
            string.Join(", ", offenders));

        Assert.True(inspected >= AllowedNetStandardBackports.Length,
            $"Expected at least {AllowedNetStandardBackports.Length} PackageReference elements in " +
            $"{CoreProject} but found {inspected}. This fact iterates what it finds, so deleting the " +
            "references outright would otherwise leave it green while proving nothing.");
    }

    /// <summary>
    /// Being in the allowlist is not enough. A backport referenced unconditionally would land in the
    /// net8.0 and net10.0 dependency groups too, which is the failure this whole file exists to stop.
    /// </summary>
    [Fact]
    public void EveryCoreReferenceIsGatedToNetStandard20()
    {
        List<string> ungated = new();

        foreach ((string id, string condition) in PackageReferences(CoreProject))
        {
            // 'AsyncInterfaces' is the DefineConstants flag set by the netstandard2.0 PropertyGroup;
            // the fact below proves that flag cannot be set on any other target.
            bool gated = condition.IndexOf("netstandard2.0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         condition.IndexOf("AsyncInterfaces", StringComparison.Ordinal) >= 0;

            if (!gated)
                ungated.Add($"'{id}' (condition: {Describe(condition)})");
        }

        Assert.True(ungated.Count == 0,
            "A PackageReference in the core project that is not conditioned on netstandard2.0 becomes " +
            "a dependency on every target framework: " + string.Join(", ", ungated));
    }

    [Fact]
    public void TheAsyncInterfacesFlagIsSetOnlyByNetStandard20()
    {
        XDocument document = Load(CoreProject);
        List<string> leaks = new();
        int declarations = 0;

        foreach (XElement constants in document.Descendants("DefineConstants"))
        {
            if (constants.Value.IndexOf("AsyncInterfaces", StringComparison.Ordinal) < 0)
                continue;

            // ASYNC_ENUMERABLE_SUPPORT also contains the substring but is a different flag, and it
            // deliberately is set on net8.0+ and in Debug.
            if (constants.Value.Replace("ASYNC_ENUMERABLE_SUPPORT", string.Empty)
                               .IndexOf("AsyncInterfaces", StringComparison.Ordinal) < 0)
                continue;

            declarations++;
            string condition = ConditionOf(constants);

            if (condition.IndexOf("netstandard2.0", StringComparison.OrdinalIgnoreCase) < 0)
                leaks.Add(Describe(condition));
        }

        Assert.True(declarations > 0,
            "No DefineConstants sets the AsyncInterfaces flag. If it was renamed, " +
            $"{nameof(EveryCoreReferenceIsGatedToNetStandard20)} is now accepting a condition that " +
            "means nothing.");

        Assert.True(leaks.Count == 0,
            "The AsyncInterfaces flag gates a PackageReference, so setting it outside the " +
            "netstandard2.0 PropertyGroup adds Microsoft.Bcl.AsyncInterfaces to that target: " +
            string.Join(", ", leaks));
    }

    /// <summary>
    /// The other half of the split: the ILogger interceptor and the DI registration extensions moved
    /// out of core, so the packages they need must have moved with them rather than been dropped.
    /// </summary>
    [Fact]
    public void TheLoggingSatelliteCarriesTheMicrosoftExtensionsPackages()
    {
        List<string> found = PackageReferences(LoggingProject).Select(r => r.Id).ToList();

        foreach (string expected in LoggingSatellitePackages)
        {
            Assert.True(found.Contains(expected),
                $"{LoggingProject} must reference '{expected}'; it is the package that exists to carry " +
                $"it. Found: {(found.Count == 0 ? "nothing" : string.Join(", ", found))}.");
        }
    }

    private static IEnumerable<(string Id, string Condition)> PackageReferences(string relativePath)
    {
        foreach (XElement reference in Load(relativePath).Descendants("PackageReference"))
        {
            string? id = (string?)reference.Attribute("Include");

            if (!string.IsNullOrWhiteSpace(id))
                yield return (id!, ConditionOf(reference));
        }
    }

    /// <summary>
    /// The conditions of the element and every ancestor, joined. A single <c>Parent</c> check misses
    /// an element-level condition and a <c>Choose</c>/<c>When</c> wrapper, and both gate just as hard.
    /// </summary>
    private static string ConditionOf(XElement element) =>
        string.Join(" && ", element.AncestorsAndSelf()
                                   .Select(e => (string?)e.Attribute("Condition"))
                                   .Where(c => !string.IsNullOrWhiteSpace(c)));

    private static string Describe(string condition) =>
        string.IsNullOrWhiteSpace(condition) ? "unconditional" : condition;

    private static XDocument Load(string relativePath)
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, relativePath);

        Assert.True(File.Exists(path), $"'{relativePath}' is missing; this fact cannot pass vacuously.");

        return XDocument.Load(path);
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
