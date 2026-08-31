using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-035 (round-33 carry-forward, coverage). Every part of a partial type must repeat the same
/// accessibility, so <c>AccessibilityKeyword</c> decides whether the generated partial is the same
/// type as the entity or a compile error inside a <c>.g.cs</c>. Only its <c>public</c> and
/// <c>internal</c> arms were reachable until AUD-R33-006 made nested entities generate correctly -
/// <c>protected</c>, <c>protected internal</c>, <c>private protected</c> and <c>private</c> are
/// legal only on a nested type.
/// </summary>
public class AccessibilityKeywordTests
{
    private static string NestedEntity(string accessibility) => $$"""
        using Jaunty.Attributes;

        namespace AccessProbe;

        public partial class Outer
        {
            [Table("orders")]
            {{accessibility}} partial class Order
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }
        }
        """;

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    [InlineData("protected")]
    [InlineData("protected internal")]
    [InlineData("private protected")]
    [InlineData("private")]
    public void ANestedEntity_KeepsItsAccessibility_AndCompiles(string accessibility)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(NestedEntity(accessibility));

        var generated = Assert.Single(sources);

        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN004"));
        Assert.Empty(compileErrors);
        Assert.Contains($"{accessibility} partial class Order", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// No modifier at all. C# defaults a nested type to <c>private</c>, and the generated partial has
    /// to say so explicitly or it declares a different accessibility from the entity's other part.
    /// </summary>
    [Fact]
    public void ANestedEntityWithNoModifier_IsDeclaredPrivate()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(NestedEntity(string.Empty));

        Assert.Empty(compileErrors);
        Assert.Contains("private partial class Order", Assert.Single(sources), StringComparison.Ordinal);
    }

    /// <summary>
    /// A top-level entity with no modifier is <c>internal</c>, which is the other default the same
    /// switch has to get right.
    /// </summary>
    [Fact]
    public void ATopLevelEntityWithNoModifier_IsDeclaredInternal()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using Jaunty.Attributes;

                namespace AccessProbe;

                [Table("orders")]
                partial class Order
                {
                    public int Id { get; set; }
                }
                """);

        Assert.Empty(compileErrors);
        Assert.Contains("internal partial class Order", Assert.Single(sources), StringComparison.Ordinal);
    }
}
