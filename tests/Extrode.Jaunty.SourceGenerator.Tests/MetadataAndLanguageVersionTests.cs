using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Extrode.Jaunty.SourceGenerator.Tests;

public sealed class MetadataAndLanguageVersionTests
{
    private const string BaseLibrary = """
        namespace BaseLib
        {
            public class BaseEntity
            {
                public int Id { get; set; }
                public string Name { get; init; } = "";
            }
        }
        """;

    private const string Entity = """
        using Extrode.Jaunty.Attributes;

        [Table("derived")]
        public partial class Derived : BaseLib.BaseEntity
        {
            public string? Own { get; set; }
        }
        """;

    private const string ScriptEntity = """
        using Extrode.Jaunty.Attributes;

        [Table("orders")]
        public partial class Order { public int Id { get; set; } }
        """;

    private const string RootingCall = """
        namespace Extrode.Jaunty.Stubs
        {
            public class Api
            {
                public static void Values(object values) { }
                public static void Use() => Values(new { Id = 1 });
            }
        }
        """;

    private static readonly string[] Paths = [.. GeneratorHarness.ReferenceAssemblyPaths()];

    private static ImmutableArray<Diagnostic> Generate(string source, LanguageVersion? version, MetadataReference? extra, out ImmutableArray<string> generated)
    {
        CSharpParseOptions parseOptions = version is { } v ? GeneratorHarness.ParseOptions.WithLanguageVersion(v) : GeneratorHarness.ParseOptions;

        List<MetadataReference> references = [.. Paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))];
        if (extra is not null)
            references.Add(extra);

        var compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new global::Extrode.Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: parseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

        generated = [.. driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName)];
        return diagnostics;
    }

    private static MetadataReference BaseLibraryReference()
    {
        var library = CSharpCompilation.Create(
            "BaseLib",
            [CSharpSyntaxTree.ParseText(BaseLibrary, GeneratorHarness.ParseOptions)],
            Paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream stream = new();
        Assert.True(library.Emit(stream).Success);
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Fact]
    public void ADroppedPropertyFromAMetadataBaseClass_IsReportedAtTheEntityDeclaration()
    {
        ImmutableArray<Diagnostic> diagnostics = Generate(Entity, null, BaseLibraryReference(), out _);

        Diagnostic dropped = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN005");

        Assert.Contains("Name", dropped.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(3, dropped.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void TheRootingMethodIsEmittedFromCSharp9()
    {
        Generate(RootingCall, LanguageVersion.CSharp9, null, out ImmutableArray<string> generated);

        Assert.Contains("JauntyAotParameterRoots.g.cs", generated);
    }

    [Fact]
    public void TheRootingMethodIsNotEmittedBeforeCSharp9EvenWithTheAttributeAvailable()
    {
        Generate(RootingCall, LanguageVersion.CSharp8, null, out ImmutableArray<string> generated);

        Assert.DoesNotContain("JauntyAotParameterRoots.g.cs", generated);
    }

    [Fact]
    public void AScriptClass_IsReportedAsNotPartialWithoutCrashing()
    {
        CSharpParseOptions scriptOptions = GeneratorHarness.ParseOptions.WithKind(SourceCodeKind.Script);

        var compilation = CSharpCompilation.CreateScriptCompilation(
            "ScriptProbe",
            CSharpSyntaxTree.ParseText(ScriptEntity, scriptOptions),
            Paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new global::Extrode.Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: scriptOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic reported = Assert.Single(diagnostics);
        Assert.Equal("JAUNTYGEN004", reported.Id);
        Assert.Contains("is not declared 'partial'", reported.GetMessage(), StringComparison.Ordinal);
    }
}
