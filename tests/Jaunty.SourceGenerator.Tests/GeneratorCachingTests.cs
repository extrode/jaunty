using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R25 (B8-7): <c>JauntyGenerator</c> defeated the incremental driver's caching entirely.
/// Its pipeline combined <c>CompilationProvider</c> - a new object after every keystroke anywhere in
/// the project - with a collected array of <c>ClassDeclarationSyntax</c>, which has reference
/// equality. The combined input therefore compared unequal on every single run, so the source-output
/// step re-ran in full: semantic analysis for, and re-emission of, <em>every</em> <c>[Table]</c>
/// entity in the project, for every character typed in any file. That is the IDE typing loop, where
/// a generator's cost is paid interactively.
///
/// <para>
/// These tests are the reason the fix cannot silently rot. The failure mode of a caching regression
/// is not a wrong answer - the generated code stays byte-identical either way - it is only slowness,
/// which no ordinary test notices. Adding one <see cref="ISymbol"/>, <see cref="SyntaxNode"/>,
/// <see cref="Location"/> or bare <c>ImmutableArray</c> field to <c>EntityModel</c> is enough to undo
/// the whole thing, and these are what would catch it.
/// </para>
///
/// <para>
/// They read the driver's own accounting rather than timing anything:
/// <see cref="GeneratorDriverOptions.TrackIncrementalGeneratorSteps"/> makes the driver record, per
/// step, whether each output was recomputed or reused, so "the emit step did not re-run" is a direct
/// assertion instead of an inference from a stopwatch.
/// </para>
/// </summary>
public class GeneratorCachingTests
{
    private const string EntitySource = """
        using Jaunty.Attributes;

        namespace CacheProbe;

        [Table("orders")]
        public partial class Order
        {
            public int Id { get; set; }
            public string? Name { get; set; }

            public string Describe() => Name ?? "";
        }
        """;

    private const string UnrelatedSource = """
        namespace CacheProbe;

        public class NotAnEntity
        {
            public int Value => 1;
        }
        """;

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithPreprocessorSymbols("NET8_0_OR_GREATER", "NET6_0_OR_GREATER");

    // ------------------------------------------------------------------
    // The caching itself
    // ------------------------------------------------------------------

    [Fact]
    public void FirstRun_GeneratesTheMapper()
    {
        // The premise for everything below: if nothing is generated, "nothing was regenerated" is
        // trivially true and the other tests would pass while asserting nothing.
        (GeneratorDriver driver, _) = RunFirst(EntitySource, UnrelatedSource);

        Assert.Equal(1, driver.GetRunResult().Results.Sum(r => r.GeneratedSources.Length));
    }

    [Fact]
    public void EditingAnUnrelatedFile_DoesNotRerunTheEmitStep()
    {
        // The finding's headline case. Typing in a file with no entity in it used to re-emit every
        // entity in the project, because CompilationProvider changed and nothing downstream of it
        // could be cached.
        (GeneratorDriver driver, Compilation compilation) = RunFirst(EntitySource, UnrelatedSource);

        driver = RunAgain(driver, compilation, treeIndex: 1, """
            namespace CacheProbe;

            public class NotAnEntity
            {
                public int Value => 1;
                public int Doubled => Value * 2;
            }
            """);

        AssertAllOutputsReused(driver);
    }

    [Fact]
    public void EditingAnEntitysMethodBody_DoesNotRerunTheEmitStep()
    {
        // Stronger, and the more common case in practice: an edit inside the entity file itself that
        // changes nothing the mapper depends on. This is what the value-equatable model buys that
        // ForAttributeWithMetadataName alone would not - the transform re-runs for this tree, but its
        // output compares equal, so the driver stops there.
        (GeneratorDriver driver, Compilation compilation) = RunFirst(EntitySource, UnrelatedSource);

        driver = RunAgain(driver, compilation, treeIndex: 0, EntitySource.Replace(
            """public string Describe() => Name ?? "";""",
            """public string Describe() => (Name ?? "").Trim();"""));

        AssertAllOutputsReused(driver);
    }

    [Fact]
    public void ChangingAMappedColumn_DoesRerunTheEmitStep()
    {
        // The negative control. Without this, all three assertions above would still hold if the
        // generator simply stopped producing anything, and a model that ignored the change would
        // look like a caching success while emitting stale mappers.
        (GeneratorDriver driver, Compilation compilation) = RunFirst(EntitySource, UnrelatedSource);

        driver = RunAgain(driver, compilation, treeIndex: 0, EntitySource.Replace(
            "public string? Name { get; set; }",
            """[Column("customer_name")] public string? Name { get; set; }"""));

        Assert.Contains(OutputReasons(driver), reason => reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);
        Assert.Contains(
            "customer_name",
            driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Single().SourceText.ToString());
    }

    // ------------------------------------------------------------------
    // What counts as a [Table], now that matching is by metadata name
    // ------------------------------------------------------------------

    [Fact]
    public void DataAnnotationsTableAttribute_IsStillRecognized()
    {
        // ForAttributeWithMetadataName matches one attribute per registration, so the generator
        // registers twice. Dropping either one silently stops generating for half the world.
        (GeneratorDriver driver, _) = RunFirst("""
            using System.ComponentModel.DataAnnotations.Schema;

            namespace CacheProbe;

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
            }
            """);

        Assert.Equal(1, driver.GetRunResult().Results.Sum(r => r.GeneratedSources.Length));
    }

    [Fact]
    public void AForeignAttributeNamedTableAttribute_IsNoLongerRecognized()
    {
        // A deliberate narrowing, pinned here so it is a decision rather than an accident.
        // Recognition used to be by simple name - `a.AttributeClass?.Name == "TableAttribute"` - so
        // any attribute called TableAttribute, from any namespace, including one the consumer wrote
        // themselves for an unrelated purpose, marked a class as a Jaunty entity and got a mapper
        // emitted into it. The XML docs, the attribute reference and the scaffolder have only ever
        // named the two supported attributes; matching a third was undocumented and unintended.
        (GeneratorDriver driver, _) = RunFirst("""
            namespace SomeoneElse;

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class TableAttribute : System.Attribute
            {
                public TableAttribute(string name) { }
            }

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
            }
            """);

        Assert.Empty(driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources));
    }

    [Fact]
    public void AnAliasedTableAttribute_IsStillRecognized()
    {
        // The matching moved from the spelling at the use site to the resolved symbol, so an alias
        // is if anything better supported than before - but AliasedTableAttributeGenerationTests
        // covers this through the real build, and this is the direct statement of it.
        (GeneratorDriver driver, _) = RunFirst("""
            using JauntyTable = Jaunty.Attributes.TableAttribute;

            namespace CacheProbe;

            [JauntyTable("orders")]
            public partial class Order
            {
                public int Id { get; set; }
            }
            """);

        Assert.Equal(1, driver.GetRunResult().Results.Sum(r => r.GeneratedSources.Length));
    }

    [Fact]
    public void TwoPartsBothCarryingTable_EmitOnlyOneSource()
    {
        // Dedupe, which moved from an INamedTypeSymbol key to the hint name when the model stopped
        // carrying symbols. A second AddSource under one hint name fails the build outright.
        (GeneratorDriver driver, _) = RunFirst(
            """
            using Jaunty.Attributes;

            namespace CacheProbe;

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
            }
            """,
            """
            using Jaunty.Attributes;

            namespace CacheProbe;

            [Table("orders")]
            public partial class Order
            {
                public string? Name { get; set; }
            }
            """);

        Assert.Equal(1, driver.GetRunResult().Results.Sum(r => r.GeneratedSources.Length));
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static (GeneratorDriver Driver, Compilation Compilation) RunFirst(params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorCaching",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)),
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: ParseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        return (driver.RunGenerators(compilation), compilation);
    }

    /// <summary>
    /// Replaces one syntax tree and re-runs the <em>same</em> driver, which is what makes the run
    /// incremental - a fresh driver has no prior state to reuse and would report everything as new.
    /// </summary>
    private static GeneratorDriver RunAgain(GeneratorDriver driver, Compilation compilation, int treeIndex, string newSource)
    {
        SyntaxTree original = compilation.SyntaxTrees.ElementAt(treeIndex);
        Compilation edited = compilation.ReplaceSyntaxTree(original, CSharpSyntaxTree.ParseText(newSource, ParseOptions));

        return driver.RunGenerators(edited);
    }

    private static ImmutableArray<IncrementalStepRunReason> OutputReasons(GeneratorDriver driver)
        => [.. driver.GetRunResult().Results
            .SelectMany(result => result.TrackedOutputSteps)
            .SelectMany(step => step.Value)
            .SelectMany(run => run.Outputs)
            .Select(output => output.Reason)];

    private static void AssertAllOutputsReused(GeneratorDriver driver)
    {
        ImmutableArray<IncrementalStepRunReason> reasons = OutputReasons(driver);

        Assert.NotEmpty(reasons);
        Assert.All(reasons, reason =>
            Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"expected the emit step to be reused, but the driver reported '{reason}'"));
    }

    private static IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        // Everything already loaded, plus Jaunty itself. Simplest reliable way to give the in-memory
        // compilation the BCL plus the attributes without hardcoding paths.
        IEnumerable<string> loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);

        // Named explicitly rather than relied on to be loaded: System.ComponentModel.Annotations is
        // not referenced by anything this test assembly touches at runtime, so without this the
        // DataAnnotations [Table] resolves to an error type and the generator - correctly - does not
        // match it, which reads as a generator bug rather than a missing reference.
        string[] required =
        [
            typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location,
            typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute).Assembly.Location
        ];

        foreach (var path in loaded.Concat(required).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return MetadataReference.CreateFromFile(path);
    }
}
