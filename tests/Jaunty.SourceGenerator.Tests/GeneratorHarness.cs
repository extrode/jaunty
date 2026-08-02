using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Runs the real generator over in-memory source and compiles what it emits.
/// </summary>
/// <remarks>
/// The fixture entities under <c>Entities/</c> cover what generated code <em>does</em>; this covers
/// what it <em>is</em> - which properties it maps, which types it declares, and above all whether it
/// compiles at all. AUD-R33-006 and AUD-R33-007 were both defects whose only symptom was a C# error
/// inside a <c>.g.cs</c>, so a fixture entity could not have caught either: the test project
/// carrying one would simply have stopped building.
/// </remarks>
internal static class GeneratorHarness
{
    internal static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithPreprocessorSymbols("NET8_0_OR_GREATER", "NET6_0_OR_GREATER");

    /// <summary>
    /// Returns the generated texts, the generator's own diagnostics, and any compilation
    /// <em>errors</em> from the original source plus everything generated. Warnings are excluded -
    /// the nullability and unused-member noise of a fixture compilation is not what these tests are
    /// about.
    /// </summary>
    internal static (ImmutableArray<string> Sources, ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompileErrors) RunAndCompile(string source)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorHarnessProbe",
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: ParseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);

        ImmutableArray<string> sources =
            [.. driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString())];

        ImmutableArray<Diagnostic> compileErrors =
            [.. output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];

        return (sources, generatorDiagnostics, compileErrors);
    }

    private static IEnumerable<MetadataReference> ReferenceAssemblies()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
}
