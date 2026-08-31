using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R33-006. A <c>[Table]</c> class nested inside another type was discovered like any other
/// entity, but the emitted file reconstructed only <c>namespace + class</c> - so the generated
/// partial sat at namespace scope and was a different type from the entity. Every member it emitted
/// named something that type did not declare (CS1061), and a nested <c>private</c> entity emitted an
/// accessibility illegal at namespace scope (CS1527), inside a <c>.g.cs</c> the consumer cannot open.
/// <para>
/// These tests run the real generator over an in-memory compilation and then <b>compile the result</b>,
/// because the whole defect was that the generated source did not compile - asserting on its text
/// alone would have missed the shape of the bug. The nesting cases that remain impossible
/// (a non-partial or generic enclosing type) must instead produce JAUNTYGEN004 and no source at all.
/// </para>
/// </summary>
public class NestedEntityGenerationTests
{
    private const string NestedInPartialClass = """
        using Jaunty.Attributes;

        namespace NestProbe;

        public partial class Outer
        {
            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }
        }
        """;

    [Fact]
    public void EntityNestedInAPartialClass_GeneratesSourceThatCompiles()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(NestedInPartialClass);

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void EntityNestedInAPartialClass_ReDeclaresTheEnclosingType()
    {
        (ImmutableArray<string> sources, _, _) = GeneratorHarness.RunAndCompile(NestedInPartialClass);

        Assert.Contains("public partial class Outer", sources[0]);
        Assert.Contains("partial class Order", sources[0]);
    }

    [Fact]
    public void EntityNestedTwoLevelsDeep_GeneratesSourceThatCompiles()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace NestProbe;

            public partial class Outer
            {
                internal partial struct Middle
                {
                    [Table("orders")]
                    internal partial class Order
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
                    }
                }
            }
            """);

        Assert.Single(sources);
        Assert.Contains("internal partial struct Middle", sources[0]);
        Assert.Empty(compileErrors);
    }

    /// <summary>
    /// The CS1527 half of the finding: a <c>private</c> nested entity emitted <c>private partial
    /// class</c> at namespace scope, which is not a legal accessibility there.
    /// </summary>
    [Fact]
    public void PrivateNestedEntity_GeneratesSourceThatCompiles()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace NestProbe;

            public partial class Outer
            {
                [Table("orders")]
                private partial class Order
                {
                    public int Id { get; set; }
                    public string? Name { get; set; }
                }

                public string Probe() => Order.TableName;
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void EntityNestedInANonPartialClass_ReportsJauntyGen004AndGeneratesNothing()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, _) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace NestProbe;

            public class Outer
            {
                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Empty(sources);

        Diagnostic diagnostic = Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("partial", diagnostic.GetMessage());
    }

    [Fact]
    public void EntityNestedInAGenericClass_ReportsJauntyGen004AndGeneratesNothing()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, _) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace NestProbe;

            public partial class Outer<T>
            {
                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Empty(sources);

        Diagnostic diagnostic = Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
        Assert.Contains("generic", diagnostic.GetMessage());
    }

    /// <summary>
    /// The control: a top-level entity must be unaffected by all of the above - no enclosing type
    /// re-declared, no diagnostic, and the same compiling output as before.
    /// </summary>
    [Fact]
    public void TopLevelEntity_IsUnaffected()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using Jaunty.Attributes;

                namespace NestProbe;

                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                    public string? Name { get; set; }
                }
                """);

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
        Assert.DoesNotContain("partial class Outer", sources[0]);
    }

}
