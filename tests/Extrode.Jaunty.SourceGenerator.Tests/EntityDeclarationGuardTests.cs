using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-028. AUD-R33-006 added the "emit nothing rather than a broken partial" guard, but only
/// for types <em>enclosing</em> the entity. The entity's own declaration was never checked, though
/// <c>GenerateMapper</c> assumes a concrete, instantiable, non-generic, <c>partial</c>,
/// non-file-local class - so each of these used to emit a <c>.g.cs</c> that failed the consumer's
/// build with nothing explaining it, which is exactly the outcome JAUNTYGEN004 exists to prevent.
/// </summary>
public class EntityDeclarationGuardTests
{
    private static string Entity(string declaration, string members = "public int Id { get; set; }") => $$"""
        using Extrode.Jaunty.Attributes;

        namespace DeclProbe;

        [Table("orders")]
        {{declaration}}
        {
            {{members}}
        }
        """;

    private static Diagnostic AssertSkippedWithDiagnostic(string source)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, _) =
            GeneratorHarness.RunAndCompile(source);

        Assert.Empty(sources);
        return Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
    }

    [Fact]
    public void AnAbstractEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity("public abstract partial class Order"));

        Assert.Contains("Order", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("abstract", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AStaticEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(
            Entity("public static partial class Order", "public static int Id { get; set; }"));

        Assert.Contains("static", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AGenericEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity("public partial class Order<T>"));

        Assert.Contains("generic", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANonPartialEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity("public class Order"));

        Assert.Contains("partial", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFileLocalEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity("file partial class Order"));

        Assert.Contains("file", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntityWithOnlyAParameterizedConstructor_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity(
            "public partial class Order",
            "public Order(int id) { Id = id; } public int Id { get; set; }"));

        Assert.Contains("parameterless constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntityWithAPrivateParameterlessConstructor_ReportsJauntyGen004AndGeneratesNothing()
    {
        AssertSkippedWithDiagnostic(Entity(
            "public partial class Order",
            "private Order() { } public int Id { get; set; }"));
    }

    [Theory]
    [InlineData("internal")]
    [InlineData("protected internal")]
    [InlineData("private protected")]
    public void AnEntityWithANonPublicParameterlessConstructor_ReportsJauntyGen004AndGeneratesNothing(string accessibility)
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity(
            "public partial class Order",
            $"{accessibility} Order() {{ }} public int Id {{ get; set; }}"));

        Assert.Contains("public parameterless constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public required string Name { get; set; }")]
    [InlineData("public required string Name;")]
    public void AnEntityWithARequiredMember_ReportsJauntyGen004AndGeneratesNothing(string member)
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity(
            "public partial class Order",
            $"public int Id {{ get; set; }} {member}"));

        Assert.Contains("'Name' is a required member", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntityInheritingARequiredMember_ReportsJauntyGen004AndGeneratesNothing()
    {
        string source = """
            using Extrode.Jaunty.Attributes;

            namespace DeclProbe;

            public class Base { public required string Tag { get; set; } }

            [Table("orders")]
            public partial class Order : Base
            {
                public int Id { get; set; }
            }
            """;

        Diagnostic diagnostic = AssertSkippedWithDiagnostic(source);

        Assert.Contains("'Tag' is a required member", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntityWhoseParameterlessConstructorIsObsoleteAsAnError_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity(
            "public partial class Order",
            "[System.Obsolete(\"use the factory\", true)] public Order() { } public int Id { get; set; }"));

        Assert.Contains("[Obsolete] as an error", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void APositionalRecordEntity_ReportsJauntyGen004AndGeneratesNothing()
    {
        Diagnostic diagnostic = AssertSkippedWithDiagnostic(Entity("public partial record Order(int Id, string Name);", ""));

        Assert.Contains("parameterless constructor", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Controls: the shapes that were always fine must stay fine.
    // ------------------------------------------------------------------

    [Fact]
    public void APlainPartialEntity_StillGenerates()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("public partial class Order"));

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void AnEntityWithAnExplicitPublicParameterlessConstructor_StillGenerates()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(
                "public partial class Order",
                "public Order() { } public Order(int id) { Id = id; } public int Id { get; set; }"));

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void AnInternalPartialEntity_StillGenerates()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("internal partial class Order"));

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void AnEntityWhoseParameterlessConstructorIsObsoleteButNotAsAnError_StillGenerates()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(
                "public partial class Order",
                "[System.Obsolete(\"prefer the factory\")] public Order() { } public int Id { get; set; }"));

        Assert.Single(sources);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void ARecordEntityWithAParameterlessConstructor_GeneratesAPartialRecord()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(
                "public partial record Order",
                "public int Id { get; set; } public string Name { get; set; } = \"\";"));

        string source = Assert.Single(sources);
        Assert.Contains("partial record Order : IMapped<Order>", source, StringComparison.Ordinal);
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
    }
}
