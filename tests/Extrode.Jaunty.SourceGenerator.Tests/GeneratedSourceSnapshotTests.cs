using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// Pins the exact text the generator emits for every fixture entity and every scenario under
/// <c>SnapshotScenarios/</c>. Set <c>JAUNTY_UPDATE_SNAPSHOTS=1</c> to rewrite the approved files
/// under <c>Snapshots/</c> after an intended change to the emitted source.
/// </summary>
public sealed class GeneratedSourceSnapshotTests
{
    private const string FixturePrefix = "fixture/";
    private const string ScenarioPrefix = "scenario/";
    private const string SnapshotPrefix = "snapshot/";

    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; " +
        "global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks;";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Generated = new(GenerateAll);

    public static TheoryData<string> Keys => new(Generated.Value.Keys.OrderBy(k => k, StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Keys))]
    public void EmittedSource_MatchesTheApprovedSnapshot(string key)
    {
        string actual = Normalize(Generated.Value[key]);

        if (Environment.GetEnvironmentVariable("JAUNTY_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(SnapshotPath(key), actual);
            return;
        }

        Assert.Equal(Normalize(ReadResource(SnapshotPrefix + key + ".approved.txt")), actual);
    }

    [Fact]
    public void EveryApprovedSnapshot_MatchesASourceGeneratedWithinThisTest()
    {
        IReadOnlyDictionary<string, string> fresh = GenerateAll();

        string[] mismatched = [.. fresh
            .Where(kv => Normalize(ReadResource(SnapshotPrefix + kv.Key + ".approved.txt")) != Normalize(kv.Value))
            .Select(kv => kv.Key)
            .OrderBy(k => k, StringComparer.Ordinal)];

        Assert.Empty(mismatched);
    }

    [Fact]
    public void EverySnapshotResource_HasAGeneratedSource()
    {
        string[] approved = Resources(SnapshotPrefix)
            .Select(n => n.Substring(SnapshotPrefix.Length, n.Length - SnapshotPrefix.Length - ".approved.txt".Length))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Generated.Value.Keys.OrderBy(k => k, StringComparer.Ordinal), approved);
    }

    [Fact]
    public void EveryFixtureAndScenario_CompilesWithoutErrors()
    {
        foreach ((string name, string[] sources) in Groups().Where(g => !g.Name.EndsWith(".invalid", StringComparison.Ordinal)))
        {
            (_, ImmutableArray<Diagnostic> errors, _) = Run(sources);
            Assert.True(errors.IsEmpty, name + ": " + string.Join(Environment.NewLine, errors));
        }
    }

    [Fact]
    public void InvalidScenarios_DoNotCrashTheGenerator()
    {
        (string Name, string[] Sources)[] invalid = [.. Groups().Where(g => g.Name.EndsWith(".invalid", StringComparison.Ordinal))];

        Assert.NotEmpty(invalid);

        foreach ((string name, string[] sources) in invalid)
        {
            (_, _, string diagnostics) = Run(sources);
            Assert.False(diagnostics.Contains("CS8785", StringComparison.Ordinal), name + ": " + diagnostics);
        }
    }

    private static IReadOnlyDictionary<string, string> GenerateAll()
    {
        Dictionary<string, string> all = new(StringComparer.Ordinal);

        foreach ((string name, string[] sources) in Groups())
        {
            (ImmutableArray<(string HintName, string Text)> generated, _, string diagnostics) = Run(sources);
            foreach ((string hint, string text) in generated)
                all[name + "." + hint] = text;
            if (diagnostics.Length > 0)
                all[name + ".diagnostics"] = diagnostics;
        }

        return all;
    }

    private static IEnumerable<(string Name, string[] Sources)> Groups()
    {
        yield return ("fixtures", [.. Resources(FixturePrefix).Select(ReadResource), ImplicitUsings]);

        foreach (string scenario in Resources(ScenarioPrefix))
            yield return (scenario.Substring(ScenarioPrefix.Length, scenario.Length - ScenarioPrefix.Length - ".scenario.txt".Length), [ReadResource(scenario), ImplicitUsings]);
    }

    private static (ImmutableArray<(string HintName, string Text)> Generated, ImmutableArray<Diagnostic> Errors, string Diagnostics) Run(string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "SnapshotProbe",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, GeneratorHarness.ParseOptions)),
            ReferencePaths().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new global::Extrode.Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: GeneratorHarness.ParseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);

        ImmutableArray<(string, string)> generated =
            [.. driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => (s.HintName, s.SourceText.ToString()))];

        string diagnostics = string.Concat(generatorDiagnostics
            .OrderBy(d => d.ToString(), StringComparer.Ordinal)
            .Select(d => string.Join("|", d.Id, d.Severity, d.GetMessage(), d.Descriptor.Title, d.Descriptor.Category, d.Descriptor.IsEnabledByDefault, d.Descriptor.DefaultSeverity, d.Location.GetLineSpan()) + "\n"));

        return (generated, [.. output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)], diagnostics);
    }

    private static IEnumerable<string> ReferencePaths()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(GeneratorHarness.ReferenceAssemblyPaths())
            .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last());

    private static IEnumerable<string> Resources(string prefix)
        => typeof(GeneratedSourceSnapshotTests).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

    private static string ReadResource(string name)
    {
        using Stream stream = typeof(GeneratedSourceSnapshotTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded resource " + name);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string SnapshotPath(string key, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "Snapshots", key + ".approved.txt");
}
