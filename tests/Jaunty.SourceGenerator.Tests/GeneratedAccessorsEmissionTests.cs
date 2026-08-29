using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Spec 009. <c>MappedCache&lt;T&gt;</c> and <c>WriteParameterCache&lt;T&gt;</c> used to locate the
/// generated <c>ReadEntity</c>, <c>CreateRowMapper</c> and <c>Bind*</c> members with
/// <c>typeof(T).GetMethod(name)</c>, and nothing arranged for those members to survive trimming. On a
/// NativeAOT publish the trimmer removed them: measured on 2026-07-30, <c>samples/NativeAOT-Basic</c>
/// threw <c>No mapper found for type 'Product'</c> and an <c>Insert</c> threw <c>No parameter binder
/// found for type 'Widget'</c>, while the identical code on the JIT worked - so the members were
/// generated and then trimmed away.
///
/// <para>
/// The fix is <c>IGeneratedAccessors&lt;T&gt;</c>: the generator hands the members over as delegates,
/// which are static method group references and therefore ordinary IL calls the trimmer must honour.
/// These tests pin the emission. What they cannot pin is that the trimmer actually keeps the members -
/// only publishing and running the binary shows that, which is why the spec's US-1 criterion is an
/// executed binary and not a clean build. A clean build is what the broken state already produced.
/// </para>
/// </summary>
public class GeneratedAccessorsEmissionTests
{
    private const string EntitySource = """
        using Jaunty.Attributes;

        namespace AccessorProbe;

        [Table("products")]
        public partial class Product
        {
            [Key]
            public int Id { get; set; }
            public string? Name { get; set; }
        }
        """;

    // ------------------------------------------------------------------
    // The interface is declared and all five accessors are emitted
    // ------------------------------------------------------------------

    [Fact]
    public void TheGeneratedClass_DeclaresIGeneratedAccessors()
    {
        var generated = RunGenerator(EntitySource).Source;

        Assert.Contains("IGeneratedAccessors<Product>", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// Explicit implementations, so the accessors stay off the consumer's entity surface - these are
    /// the caller's own classes, and <c>IEntityMetadataSource.Columns</c> set the precedent.
    /// </summary>
    [Theory]
    [InlineData("Func<IDataReader, Product> IGeneratedAccessors<Product>.RowMapper")]
    [InlineData("Func<IDataReader, Func<IDataReader, Product>> IGeneratedAccessors<Product>.RowMapperFactory => CreateRowMapper;")]
    [InlineData("Action<IDbCommand, Product> IGeneratedAccessors<Product>.InsertBinder => BindInsert;")]
    [InlineData("Action<IDbCommand, Product> IGeneratedAccessors<Product>.UpdateBinder => BindUpdate;")]
    [InlineData("Action<IDbCommand, Product> IGeneratedAccessors<Product>.DeleteBinder => BindDelete;")]
    public void EachAccessor_IsEmittedAsAnExplicitImplementation(string expected)
    {
        var generated = RunGenerator(EntitySource).Source;

        Assert.Contains(expected, generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The whole mechanism is that the delegate names the generated method directly. If an accessor
    /// were emitted as a lambda that resolved the method some other way, the static reference - and
    /// with it the preservation - would be gone while these tests still passed on the member names.
    /// </summary>
    [Fact]
    public void TheNet8RowMapper_IsTheReadEntityMethodGroupItself()
    {
        var generated = RunGenerator(EntitySource).Source;

        Assert.Contains(
            "Func<IDataReader, Product> IGeneratedAccessors<Product>.RowMapper => ReadEntity;",
            generated,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Below net8.0 <c>ReadEntity</c> is an instance member (see <c>IMapped&lt;T&gt;</c>), so the
    /// accessor has to construct one - and it must do so <em>per row</em>, because the reflection
    /// fallback it replaces did <c>r =&gt; openDelegate(new T(), r)</c>. Hoisting the instance out of
    /// the lambda would change behaviour for any <c>ReadEntity</c> that touches <c>this</c>, and would
    /// do it silently on exactly the targets least likely to be exercised.
    /// </summary>
    [Fact]
    public void ThePreNet8RowMapper_ConstructsAFreshEntityPerRow()
    {
        var generated = RunGenerator(EntitySource).Source;

        Assert.Contains(
            "Func<IDataReader, Product> IGeneratedAccessors<Product>.RowMapper => r => new Product().ReadEntity(r);",
            generated,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Both branches must be present, guarded - the generated file is compiled by the consumer under
    /// whichever target they picked.
    /// </summary>
    [Fact]
    public void BothRowMapperBranches_AreGuardedByTheTargetCheck()
    {
        var generated = RunGenerator(EntitySource).Source;

        Assert.Contains("#if NET8_0_OR_GREATER", generated, StringComparison.Ordinal);
        Assert.Contains("#else", generated, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // JAUNTYGEN002
    // ------------------------------------------------------------------

    /// <summary>
    /// A hand-written <c>IMapped&lt;T&gt;</c> is the population the reflection fallback still serves,
    /// and the population the trimmer will break. The warning turns that from a runtime
    /// "No mapper found" into a build-time message naming the type.
    /// </summary>
    [Fact]
    public void AHandWrittenIMapped_ReportsJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;
            using Jaunty.Interfaces;

            namespace AccessorProbe;

            public class HandRolled : IMapped<HandRolled>
            {
                public int Id { get; set; }

            #if NET8_0_OR_GREATER
                public static HandRolled ReadEntity(IDataReader reader) => new HandRolled();
            #else
                public HandRolled ReadEntity(IDataReader reader) => new HandRolled();
            #endif
            }
            """).Diagnostics;

        Diagnostic warning = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN002");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("HandRolled", warning.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The message has to say what to do, not just that something is wrong - the round-26 lesson about
    /// error text that names a symptom and leaves the cause to guesswork.
    /// </summary>
    [Fact]
    public void TheDiagnosticMessage_NamesEveryWayOut()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;
            using Jaunty.Interfaces;

            namespace AccessorProbe;

            public class HandRolled : IMapped<HandRolled>
            {
            #if NET8_0_OR_GREATER
                public static HandRolled ReadEntity(IDataReader reader) => new HandRolled();
            #else
                public HandRolled ReadEntity(IDataReader reader) => new HandRolled();
            #endif
            }
            """).Diagnostics;

        var message = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN002").GetMessage();

        Assert.Contains("[Table]", message, StringComparison.Ordinal);
        Assert.Contains("IGeneratedAccessors", message, StringComparison.Ordinal);
        Assert.Contains("DynamicDependency", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>[Table]</c> entity gets the accessors emitted for it, so it is trim-safe and warning about
    /// it would be a false positive on the common case.
    /// </summary>
    [Fact]
    public void AGeneratedEntity_DoesNotReportJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(EntitySource).Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "JAUNTYGEN002");
    }

    /// <summary>
    /// And neither does a type that supplies the accessors itself - it has done exactly what the
    /// diagnostic asks for.
    /// </summary>
    [Fact]
    public void ATypeImplementingTheAccessorsItself_DoesNotReportJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System;
            using System.Data;
            using Jaunty.Interfaces;

            namespace AccessorProbe;

            public class SelfRooted : IMapped<SelfRooted>, IGeneratedAccessors<SelfRooted>
            {
            #if NET8_0_OR_GREATER
                public static SelfRooted ReadEntity(IDataReader reader) => new SelfRooted();
            #else
                public SelfRooted ReadEntity(IDataReader reader) => new SelfRooted();
            #endif

                Func<IDataReader, SelfRooted> IGeneratedAccessors<SelfRooted>.RowMapper
                    => r => new SelfRooted();
                Func<IDataReader, Func<IDataReader, SelfRooted>> IGeneratedAccessors<SelfRooted>.RowMapperFactory
                    => r => rr => new SelfRooted();
                Action<IDbCommand, SelfRooted> IGeneratedAccessors<SelfRooted>.InsertBinder => (c, e) => { };
                Action<IDbCommand, SelfRooted> IGeneratedAccessors<SelfRooted>.UpdateBinder => (c, e) => { };
                Action<IDbCommand, SelfRooted> IGeneratedAccessors<SelfRooted>.DeleteBinder => (c, e) => { };
            }
            """).Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "JAUNTYGEN002");
    }

    /// <summary>
    /// A class with a base list that has nothing to do with Jaunty must not trip the check - the
    /// predicate deliberately matches every class with a base list, so the semantic filter is what
    /// keeps this quiet.
    /// </summary>
    [Fact]
    public void AnUnrelatedBaseList_DoesNotReportJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            namespace AccessorProbe;

            public interface IUnrelated { }

            public class Ordinary : IUnrelated { }
            """).Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "JAUNTYGEN002");
    }

    /// <summary>
    /// The write path has its own reflection: <c>WriteParameterCache.TryGetGeneratedBinder</c> looks
    /// up <c>BindInsert</c>/<c>BindUpdate</c>/<c>BindDelete</c> by name. A type supplying those and
    /// nothing else has no base list at all, so the old predicate never saw it and the write half of
    /// the trimming problem went unwarned while the read half did not.
    /// </summary>
    [Fact]
    public void AConventionBinderWithNoBaseList_ReportsJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;

            namespace AccessorProbe;

            public class HandBound
            {
                public int Id { get; set; }

                public static void BindInsert(IDbCommand command, HandBound entity) { }
                public static void BindUpdate(IDbCommand command, HandBound entity) { }
            }
            """).Diagnostics;

        var message = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN002").GetMessage();

        Assert.Contains("HandBound", message, StringComparison.Ordinal);
        Assert.Contains("BindInsert/BindUpdate", message, StringComparison.Ordinal);
        Assert.DoesNotContain("BindDelete", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shape has to match the one reflection looks for. An instance method, a wrong parameter
    /// list or a foreign entity type is not what <c>TryGetGeneratedBinder</c> would find, so warning
    /// about it would be a false positive on ordinary code that happens to share a name.
    /// </summary>
    [Fact]
    public void AMethodThatOnlySharesTheName_DoesNotReportJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;

            namespace AccessorProbe;

            public class NotABinder
            {
                public void BindInsert(IDbCommand command, NotABinder entity) { }
                public static void BindUpdate(IDbCommand command) { }
                public static int BindDelete(IDbCommand command, NotABinder entity) => 0;
            }
            """).Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "JAUNTYGEN002");
    }

    /// <summary>
    /// A <c>[Table]</c> entity gets all three binders emitted, so declaring them is the generator's
    /// job and not a warning - the same false-positive guard the read side already had.
    /// </summary>
    [Fact]
    public void AGeneratedEntityWithBinders_DoesNotReportJAUNTYGEN002()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;
            using Jaunty.Attributes;

            namespace AccessorProbe;

            [Table("bound")]
            public partial class Bound
            {
                public int Id { get; set; }

                public static void BindInsert(IDbCommand command, Bound entity) { }
            }
            """).Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "JAUNTYGEN002");
    }

    /// <summary>
    /// One type doing both gets one diagnostic naming both, not two diagnostics or a message that
    /// mentions only whichever half was checked first.
    /// </summary>
    [Fact]
    public void AMapperThatAlsoBinds_NamesBothInOneDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator("""
            using System.Data;
            using Jaunty.Interfaces;

            namespace AccessorProbe;

            public class BothHalves : IMapped<BothHalves>
            {
            #if NET8_0_OR_GREATER
                public static BothHalves ReadEntity(IDataReader reader) => new BothHalves();
            #else
                public BothHalves ReadEntity(IDataReader reader) => new BothHalves();
            #endif

                public static void BindDelete(IDbCommand command, BothHalves entity) { }
            }
            """).Diagnostics;

        var message = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN002").GetMessage();

        Assert.Contains("ReadEntity", message, StringComparison.Ordinal);
        Assert.Contains("BindDelete", message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static (string Source, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "AccessorProbe",
            [CSharpSyntaxTree.ParseText(source)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator())
            .RunGenerators(compilation);

        GeneratorDriverRunResult result = driver.GetRunResult();

        var emitted = string.Join(
            "\n",
            result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        return (emitted, result.Diagnostics);
    }

    /// <summary>
    /// Everything already loaded, plus Jaunty itself - the same approach as
    /// <see cref="InheritedPropertyTests"/>. Without the explicit reference <c>[Table]</c> and
    /// <c>IMapped&lt;T&gt;</c> resolve to error types and the generator correctly matches nothing,
    /// which reads as a generator bug rather than a missing reference.
    /// </summary>
    private static IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        IEnumerable<string> loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);

        // System.Data.Common is listed rather than assumed: it is loaded by the time the whole suite
        // has run, so an unfiltered run passed either way, but a filtered run reached these tests
        // before anything had loaded it and IDbCommand resolved to an error type - which reads as the
        // generator failing to match rather than the harness failing to reference.
        string[] required =
        [
            typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location,
            typeof(global::Jaunty.Interfaces.IGeneratedAccessors<>).Assembly.Location,
            typeof(System.Data.IDbCommand).Assembly.Location,
        ];

        foreach (var path in loaded.Concat(required).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return MetadataReference.CreateFromFile(path);
    }
}
