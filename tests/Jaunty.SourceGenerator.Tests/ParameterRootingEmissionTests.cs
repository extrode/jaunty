using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Spec 010. Jaunty binds a parameters object by reflecting over its public properties, and on a
/// NativeAOT publish the trimmer removes those getters - measured on <c>samples/NativeAOT-Basic</c>,
/// 2026-07-30, as <c>No property found on type '&lt;&gt;f__AnonymousType0`1' matching SQL parameter
/// '@Id'. Available properties:</c> with the list empty. Spec 009's remedy was unavailable here: a
/// parameters object is not an entity, is usually anonymous, and Jaunty has never been told about it.
///
/// <para>
/// What is available is the call site. These tests pin what the generator emits after reading the
/// consumer's own <c>Query</c>/<c>Execute</c> invocations. As with spec 009, they cannot prove the
/// trimmer keeps anything - only publishing and running the binary does that, and it is the acceptance
/// criterion in the spec. What they do prove is the two things a published binary cannot tell apart
/// from a coincidence: that the emitted rooting <em>compiles</em>, and that it names the right types.
/// </para>
/// </summary>
public class ParameterRootingEmissionTests
{
    /// <summary>
    /// A consumer's file, with the pieces every case needs. <c>Execute(sql, parameters)</c> is used
    /// rather than a Query overload because it carries no entity constraint, so a test case can pass
    /// any shape as the parameters object without also having to satisfy a mapper.
    /// </summary>
    private static string Consumer(string body, string members = "") => $$"""
        using System.Collections.Generic;
        using System.Data;

        using Jaunty;

        namespace RootProbe;

        public class DataAccess
        {
            public void Run(IDbConnection connection)
            {
                {{body}}
            }

            {{members}}
        }
        """;

    // ------------------------------------------------------------------
    // What gets rooted
    // ------------------------------------------------------------------

    /// <summary>
    /// The shipped sample's exact shape, and the case that cannot be solved any other way: an
    /// anonymous type has no name, so neither <c>PreserveParameters&lt;T&gt;()</c> nor
    /// <c>[DynamicDependency]</c> can reach it. The witness overload supplies the type by inference
    /// from a structurally identical instance, which C# unifies with the consumer's own.
    /// </summary>
    [Fact]
    public void AnAnonymousParametersObject_IsRootedByAStructurallyIdenticalWitness()
    {
        var generated = RunGenerator(Consumer("""connection.Execute("UPDATE t SET x = 1 WHERE id = @Id", new { Id = 1 });""")).Source;

        Assert.Contains(
            "global::Jaunty.JauntyAot.PreserveParameters(new { Id = default(int) });",
            generated,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Property order is part of an anonymous type's identity, so a witness that reorders them is a
    /// <em>different</em> type and roots nothing. This is the failure that would look like success:
    /// the rooting compiles, the build is clean, and the published binary still throws.
    /// </summary>
    [Fact]
    public void AnAnonymousWitness_KeepsPropertyOrderAndTypes()
    {
        var generated = RunGenerator(Consumer("""connection.Execute("UPDATE t SET x = @Name WHERE id = @Id", new { Id = 1, Name = "a", Active = true });""")).Source;

        Assert.Contains(
            "new { Id = default(int), Name = default(string), Active = default(bool) }",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ANamedParametersType_IsRootedByName()
    {
        var generated = RunGenerator(Consumer(
            """connection.Execute("UPDATE t SET x = 1 WHERE id = @Id", new ProductQuery());""",
            "public class ProductQuery { public int Id { get; set; } }")).Source;

        Assert.Contains(
            "global::Jaunty.JauntyAot.PreserveParameters<global::RootProbe.DataAccess.ProductQuery>();",
            generated,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The single most load-bearing character sequence in the emitted file. Measured while building
    /// this: the same rooting calls in an ordinary method nobody invokes preserve nothing, because the
    /// trimmer removes the method and the annotations inside it. The published sample failed exactly as
    /// it had before, with a generated file present and correct-looking.
    /// </summary>
    [Fact]
    public void TheRootingMethod_IsAModuleInitializer()
    {
        var generated = RunGenerator(Consumer("""connection.Execute("DELETE FROM t WHERE id = @Id", new { Id = 1 });""")).Source;

        Assert.Contains(
            "[global::System.Runtime.CompilerServices.ModuleInitializer]",
            generated,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Two call sites passing the same shape must produce one rooting call, and the emitted text must
    /// not depend on visit order - two builds of unchanged source have to produce identical output or
    /// the compiler's generated-file cache thrashes.
    /// </summary>
    [Fact]
    public void RepeatedParameterTypes_AreEmittedOnce()
    {
        var generated = RunGenerator(Consumer("""
            connection.Execute("UPDATE t SET x = 1 WHERE id = @Id", new { Id = 1 });
            connection.Execute("DELETE FROM t WHERE id = @Id", new { Id = 2 });
            """)).Source;

        string[] lines = generated.Split('\n');

        Assert.Single(lines, l => l.Contains("PreserveParameters(new { Id =", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // What is deliberately not rooted
    // ------------------------------------------------------------------

    /// <summary>
    /// A dictionary is bound by key and a scalar takes its name from the SQL text; neither reaches
    /// property reflection, so rooting them would be noise. <c>null</c> is asked of the semantic model
    /// rather than the syntax, because the library's own no-parameter overloads spell it
    /// <c>(object?)null</c> - a cast, not a literal - and a syntax check missed all twenty of them,
    /// reporting JAUNTYGEN003 against Jaunty's own source.
    /// </summary>
    [Theory]
    [InlineData("""connection.Execute("DELETE FROM t WHERE id = @Id", new Dictionary<string, object?> { ["Id"] = 1 });""")]
    [InlineData("""connection.Execute("DELETE FROM t WHERE id = @Id", 5);""")]
    [InlineData("""connection.Execute("DELETE FROM t WHERE id = @Id", (object?)null);""")]
    [InlineData("""connection.Execute("DELETE FROM t");""")]
    public void ShapesThatNeedNoPreservation_EmitNothing(string body)
    {
        var result = RunGenerator(Consumer(body));

        Assert.DoesNotContain("JauntyAotParameterRoots", result.Source, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "JAUNTYGEN003");
    }

    /// <summary>
    /// The syntax predicate matches on the name at the use site and so admits a consumer's own
    /// <c>Execute…</c>; the semantic check is what rejects it. Without this the generator would emit
    /// rooting for types that never reach Jaunty at all.
    /// </summary>
    [Fact]
    public void AMethodThatIsNotJauntys_IsIgnored()
    {
        var result = RunGenerator("""
            namespace RootProbe;

            public class NotJaunty
            {
                public void Run()
                {
                    ExecuteSomething("sql", new { Id = 1 });
                }

                private static void ExecuteSomething(string sql, object parameters) { }
            }
            """);

        Assert.DoesNotContain("JauntyAotParameterRoots", result.Source, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "JAUNTYGEN003");
    }

    // ------------------------------------------------------------------
    // JAUNTYGEN003
    // ------------------------------------------------------------------

    /// <summary>
    /// The concrete type was known one line earlier and erased by the declaration, so this is
    /// actionable at this call site - which is what makes it worth a diagnostic.
    /// </summary>
    [Fact]
    public void AnObjectTypedLocal_ReportsJAUNTYGEN003()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(Consumer("""
            object p = new { Id = 1 };
            connection.Execute("DELETE FROM t WHERE id = @Id", p);
            """)).Diagnostics;

        Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN003");
    }

    /// <summary>
    /// Forwarding an <c>object</c> is the one shape where the diagnostic would be useless: the concrete
    /// type is not knowable at a forwarding site, so the author of that line can do nothing about it.
    /// Measured while building this - without the exclusion it fired 170 times inside Jaunty's own
    /// source, every internal overload handing <c>object? parameters</c> to the next, and broke the
    /// library's own warnings-as-errors build.
    /// </summary>
    /// <remarks>
    /// This test also pins the honest limit of the whole approach, which the first version of it got
    /// wrong by asserting the outer call site would be rooted. It is not: an <c>object</c>-typed
    /// consumer wrapper is invisible from <em>both</em> ends - the inner call site is excused here, and
    /// the outer one is a call to the consumer's own method, which this generator has no reason to
    /// inspect. Such a chain needs <c>JauntyAot.PreserveParameters&lt;T&gt;()</c> or a generic wrapper.
    /// Asserting the gap keeps it from being rediscovered as a bug, and keeps it from being closed by
    /// accident without a decision.
    /// </remarks>
    [Fact]
    public void AForwardedObjectParameter_IsNeitherRootedNorReported()
    {
        var result = RunGenerator(Consumer(
            """Forward(connection, "DELETE FROM t WHERE id = @Id", new { Id = 1 });""",
            """
            private static void Forward(IDbConnection connection, string sql, object parameters)
                => connection.Execute(sql, parameters);
            """));

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "JAUNTYGEN003");
        Assert.DoesNotContain("JauntyAotParameterRoots", result.Source, StringComparison.Ordinal);
    }

    /// <summary>
    /// A generic wrapper is excused as well, and this test exists because the opposite was tried first.
    /// Reporting it looked strictly better - the consumer has a specific fix in annotating their own
    /// type parameter - until it was measured against the library: Jaunty's own generic write plumbing
    /// forwards the entity to <c>CommandObservation.Execute(sql, parameters, ...)</c>, where
    /// <c>parameters</c> feeds interceptors and logging rather than binding, so trimmed getters cost log
    /// detail and not a failed query. Six of those, warnings-as-errors, and the library stopped
    /// building. The two shapes are syntactically identical, so separating them would be a guess.
    /// </summary>
    [Fact]
    public void AForwardedTypeParameter_IsExcusedToo()
    {
        var result = RunGenerator(Consumer(
            """Forward(connection, "DELETE FROM t WHERE id = @Id", new { Id = 1 });""",
            """
            private static void Forward<TParam>(IDbConnection connection, string sql, TParam parameters)
                => connection.Execute(sql, parameters!);
            """));

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "JAUNTYGEN003");
    }

    /// <summary>
    /// A private nested type cannot be named from the generated file, which lives elsewhere in the
    /// assembly. Reporting it is the honest answer; emitting a name that does not resolve would break
    /// the consumer's build, which is strictly worse than the runtime failure being warned about.
    /// </summary>
    [Fact]
    public void AnInaccessibleParametersType_ReportsJAUNTYGEN003RatherThanEmittingIt()
    {
        var result = RunGenerator(Consumer(
            """connection.Execute("DELETE FROM t WHERE id = @Id", new Hidden());""",
            "private class Hidden { public int Id { get; set; } }"));

        Assert.Single(result.Diagnostics, d => d.Id == "JAUNTYGEN003");
        Assert.DoesNotContain("Hidden", result.Source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The message has to say what to do about it, not merely that something is wrong - the round-26
    /// lesson about error text naming a symptom and leaving the cause to guesswork.
    /// </summary>
    [Fact]
    public void TheDiagnosticMessage_NamesBothWaysOut()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(Consumer("""
            object p = new { Id = 1 };
            connection.Execute("DELETE FROM t WHERE id = @Id", p);
            """)).Diagnostics;

        var message = Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN003").GetMessage();

        Assert.Contains("JauntyAot.Parameters", message, StringComparison.Ordinal);
        Assert.Contains("PreserveParameters", message, StringComparison.Ordinal);
        Assert.Contains("Available properties:", message, StringComparison.Ordinal);
    }

    /// <summary>Warning, not error: the code is correct on the JIT and only a trimmed publish is affected.</summary>
    [Fact]
    public void TheDiagnostic_IsAWarning()
    {
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(Consumer("""
            object p = new { Id = 1 };
            connection.Execute("DELETE FROM t WHERE id = @Id", p);
            """)).Diagnostics;

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN003").Severity);
    }

    // ------------------------------------------------------------------
    // The emitted code has to compile
    // ------------------------------------------------------------------

    /// <summary>
    /// The one test the published binary cannot substitute for. A witness expression is generated C#
    /// assembled from symbol display strings; if any shape produces something that does not compile,
    /// the consumer gets a build error in a file they did not write and cannot edit. Every shape that
    /// reaches emission is compiled here together with its source.
    /// </summary>
    [Theory]
    [InlineData("""connection.Execute("s @Id", new { Id = 1 });""", "")]
    [InlineData("""connection.Execute("s @Name", new { Name = "a", When = System.DateTime.Now, Amount = 1.5m });""", "")]
    [InlineData("""connection.Execute("s @Id", new { Id = (int?)1, Tags = new[] { 1, 2 } });""", "")]
    [InlineData("""connection.Execute("s @Id", new { Outer = 1, Nested = new { Inner = 2 } });""", "")]
    [InlineData("""connection.Execute("s @Id", new Named());""", "public class Named { public int Id { get; set; } }")]
    [InlineData("""connection.Execute("s @Id", new Generic<int>());""", "public class Generic<T> { public T? Value { get; set; } }")]
    [InlineData("""connection.Execute("s @Id", new Rec(1));""", "public record Rec(int Id);")]
    public void TheEmittedRooting_Compiles(string body, string members)
    {
        var result = RunGenerator(Consumer(body, members));

        Assert.Empty(result.CompileErrors);
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static (string Source, ImmutableArray<Diagnostic> Diagnostics, IEnumerable<Diagnostic> CompileErrors) RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "RootProbe",
            [CSharpSyntaxTree.ParseText(source)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation updated, out _);

        GeneratorDriverRunResult result = driver.GetRunResult();

        var emitted = string.Join(
            "\n",
            result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        return (
            emitted,
            result.Diagnostics,
            updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Everything already loaded, plus the three assemblies these probes name explicitly.
    /// </summary>
    /// <remarks>
    /// <c>System.Data.Common</c> is the one that is easy to miss and expensive to misread. Without it
    /// <c>IDbConnection</c> resolves to an error type, the <c>connection.Execute(...)</c> invocation
    /// binds to nothing, and the generator correctly emits nothing - so 17 of these tests failed with
    /// an empty generated source, which reads exactly like a broken generator. It is the same trap the
    /// sibling <c>GeneratedAccessorsEmissionTests</c> harness documents for <c>[Table]</c>.
    /// </remarks>
    private static IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        IEnumerable<string> loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);

        string[] required =
        [
            typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location,
            typeof(global::Jaunty.JauntyAot).Assembly.Location,
            typeof(System.Data.IDbConnection).Assembly.Location,
        ];

        foreach (var path in loaded.Concat(required).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return MetadataReference.CreateFromFile(path);
    }
}
