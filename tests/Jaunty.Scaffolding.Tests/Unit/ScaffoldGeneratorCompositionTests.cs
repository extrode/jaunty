using System.Collections.Immutable;
using System.Reflection;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R25 (B8-1): the scaffolder's default output did not compose with the source generator, in
/// two opposite ways, and neither was diagnosed.
///
/// <list type="number">
/// <item><description>
/// <c>GeneratePartialClasses</c> defaulted to <see langword="false"/> (the CLI exposed it as an
/// opt-in <c>--partial</c>), so the scaffolder emitted <c>public class Customer</c>.
/// <c>JauntyGenerator</c> unconditionally emits a <c>partial class Customer</c> declaration for any
/// class carrying <c>[Table]</c>. Scaffold a table, add the generator package, and the build failed
/// with <b>CS0260</b> - "Missing partial modifier on declaration of type 'Customer'" - pointing at
/// the user's own scaffolded file.
/// </description></item>
/// <item><description>
/// When the generator did not fire, the failure was silent instead. <c>AppendClassAttributes</c>
/// skipped <c>[Table]</c> entirely when the table name equalled the class name and the schema was
/// empty, and <c>GetSemanticTargetForGeneration</c> requires <c>[Table]</c> to consider a class at
/// all. That combination is the <em>normal</em> case for SQLite and MySQL - <c>MySqlSchemaReader</c>
/// hardcodes <c>'' AS SchemaName</c> and SQLite has no schemas - so a SQLite table named
/// <c>Customer</c> scaffolded to a <c>Customer</c> class with no attribute and got no generated
/// mapper, silently falling back to reflection (or throwing, if <c>UseReflectionMapping()</c> was
/// never called) with nothing to indicate why.
/// </description></item>
/// </list>
///
/// <para>
/// So the two flagship codegen halves of the product either broke the build or silently disabled
/// each other under their own defaults - and, as the finding noted, the round trip was not covered
/// by any test. It is now: these compile the scaffolder's actual output together with the real
/// generator, in memory, via <see cref="CSharpGeneratorDriver"/>.
/// </para>
/// </summary>
public class ScaffoldGeneratorCompositionTests
{
    private readonly EntityCodeGenerator _generator = new(new SQLiteTypeMapper());

    /// <summary>A SQLite-shaped table: name equal to the class name, no schema. The case that broke.</summary>
    private static TableSchema SqliteShapedTable(string schema = "") => new()
    {
        TableName = "Customer",
        SchemaName = schema,
        Columns =
        [
            new ColumnSchema { ColumnName = "Id", DataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true, IsNullable = false },
            new ColumnSchema { ColumnName = "Name", DataType = "TEXT", IsNullable = true }
        ]
    };

    private static CodeGeneratorOptions DefaultOptions() => new() { Namespace = "Scaffolded.Entities" };

    // ------------------------------------------------------------------
    // The emission itself
    // ------------------------------------------------------------------

    [Fact]
    public void DefaultOutput_IsPartial()
    {
        var code = _generator.GenerateEntity(SqliteShapedTable(), DefaultOptions());

        Assert.Contains("public partial class Customer", code);
    }

    [Fact]
    public void DefaultOutput_CarriesTableAttribute_EvenWhenTheNameMatchesTheClass()
    {
        // Redundant to a human reader; not redundant to the generator, which keys off it.
        var code = _generator.GenerateEntity(SqliteShapedTable(), DefaultOptions());

        Assert.Contains("[Jaunty.Attributes.Table(\"Customer\")]", code);
    }

    [Fact]
    public void PartialCanStillBeTurnedOff_ButThenItIsAnExplicitChoice()
    {
        var options = new CodeGeneratorOptions { Namespace = "Scaffolded.Entities", GeneratePartialClasses = false };

        var code = _generator.GenerateEntity(SqliteShapedTable(), options);

        Assert.Contains("public class Customer", code);
        Assert.DoesNotContain("public partial class Customer", code);
    }

    [Fact]
    public void TableAttributeCanStillBeTurnedOff_ButThenItIsAnExplicitChoice()
    {
        var options = new CodeGeneratorOptions { Namespace = "Scaffolded.Entities", GenerateTableAttribute = false };

        var code = _generator.GenerateEntity(SqliteShapedTable(), options);

        Assert.DoesNotContain("Jaunty.Attributes.Table(", code);
    }

    // ------------------------------------------------------------------
    // The round trip: scaffold -> generate -> compile
    // ------------------------------------------------------------------

    [Fact]
    public void DefaultOutput_CompilesCleanlyWithTheGenerator()
    {
        var code = _generator.GenerateEntity(SqliteShapedTable(), DefaultOptions());

        (ImmutableArray<Diagnostic> diagnostics, int generatedFiles) = RunGeneratorAndCompile(code);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(1, generatedFiles);
    }

    [Fact]
    public void DefaultOutput_ActuallyGetsAMapper()
    {
        // The silent half of the finding: with [Table] elided, the generator did not consider the
        // class at all and emitted nothing, with no diagnostic to say so.
        var code = _generator.GenerateEntity(SqliteShapedTable(), DefaultOptions());

        (_, int generatedFiles) = RunGeneratorAndCompile(code);

        Assert.Equal(1, generatedFiles);
    }

    [Fact]
    public void NonPartialOutput_StillFailsWithCS0260_WhichIsWhyItIsNoLongerTheDefault()
    {
        // Pins the consequence rather than just the default, so the reason the default changed
        // stays visible. Opting out is legitimate - it just must not be what happens silently.
        var options = new CodeGeneratorOptions { Namespace = "Scaffolded.Entities", GeneratePartialClasses = false };
        var code = _generator.GenerateEntity(SqliteShapedTable(), options);

        (ImmutableArray<Diagnostic> diagnostics, _) = RunGeneratorAndCompile(code);

        Assert.Contains(diagnostics, d => d.Id == "CS0260");
    }

    [Fact]
    public void OutputWithoutTheTableAttribute_GetsNoMapperAndNoDiagnostic()
    {
        // Pins the other consequence, and the fact that it is silent: the generator has no
        // "entity was not recognised" diagnostic, which is exactly why the elision was invisible.
        var options = new CodeGeneratorOptions { Namespace = "Scaffolded.Entities", GenerateTableAttribute = false };
        var code = _generator.GenerateEntity(SqliteShapedTable(), options);

        (ImmutableArray<Diagnostic> diagnostics, int generatedFiles) = RunGeneratorAndCompile(code);

        Assert.Equal(0, generatedFiles);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ATableWithASchema_AlsoCompilesWithTheGenerator()
    {
        // The case that always emitted [Table] - covered so the round trip is pinned for the
        // SQL Server/Postgres shape too, not only the one that was broken.
        var code = _generator.GenerateEntity(SqliteShapedTable("sales"), DefaultOptions());

        (ImmutableArray<Diagnostic> diagnostics, int generatedFiles) = RunGeneratorAndCompile(code);

        Assert.Contains("[Jaunty.Attributes.Table(\"Customer\", \"sales\")]", code);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(1, generatedFiles);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> with <c>JauntyGenerator</c> attached and returns the
    /// resulting diagnostics together with the number of files the generator produced.
    /// </summary>
    private static (ImmutableArray<Diagnostic> Diagnostics, int GeneratedFiles) RunGeneratorAndCompile(string source)
    {
        // The generated mapper is multi-targeted internally - ReadEntity is a static abstract
        // interface implementation on net8.0+ and an instance method below - and IMapped<T> in the
        // referenced Jaunty assembly is the net8.0 build. Without the symbol the generated source
        // takes the net472 branch and fails with CS8928, which would be an artefact of this harness
        // rather than anything about the scaffolder. NET6_0_OR_GREATER covers the generated
        // ReadFallback<T>'s DateOnly/TimeOnly block for the same reason.
        var parseOptions = CSharpParseOptions.Default
            .WithPreprocessorSymbols("NET8_0_OR_GREATER", "NET6_0_OR_GREATER");

        var compilation = CSharpCompilation.Create(
            "ScaffoldGeneratorComposition",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);

        GeneratorDriverRunResult result = driver.GetRunResult();

        return (output.GetDiagnostics(), result.Results.Sum(r => r.GeneratedSources.Length));
    }

    private static IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        // Everything already loaded, plus the reference assemblies next to them. Simplest reliable
        // way to give the in-memory compilation the BCL plus Jaunty without hardcoding paths.
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);

        var jaunty = typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location;
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var paths = new HashSet<string>(loaded, StringComparer.OrdinalIgnoreCase)
        {
            jaunty,
            Path.Combine(runtimeDirectory, "System.Runtime.dll"),
            Path.Combine(runtimeDirectory, "System.Data.Common.dll"),
            Path.Combine(runtimeDirectory, "netstandard.dll")
        };

        foreach (var path in paths.Where(File.Exists))
            yield return MetadataReference.CreateFromFile(path);
    }
}
