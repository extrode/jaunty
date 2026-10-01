using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R38 generator audit. The generated partial lives inside the entity, so an entity member
/// named <c>System</c>, <c>DBNull</c> or <c>StringComparison</c> wins simple-name lookup over the
/// namespace or type in expression context: <c>System.Enum.Parse</c> in <c>ReadFallback</c>, the
/// <c>ParameterMap</c> comparer, <c>DBNull.Value</c> in the binders and the column-name check in
/// <c>Resolve</c> all bound to the property (CS0120, CS0236). Emitted names are now
/// <c>global::</c>-qualified.
/// </summary>
public class GlobalQualifiedEmissionTests
{
    [Theory]
    [InlineData("System")]
    [InlineData("DBNull")]
    [InlineData("StringComparison")]
    public void PropertyNamedLikeAFrameworkName_CompilesInTheGeneratedSource(string name)
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile($$"""
            using Extrode.Jaunty.Attributes;

            namespace ShadowProbe;

            public enum Kind { A }

            [Table("t")]
            public partial class Order
            {
                public int Id { get; set; }
                public Kind Kind { get; set; }
                public char Letter { get; set; }
                public global::System.Guid Token { get; set; }
                public global::System.DateOnly Day { get; set; }
                public int {{name}} { get; set; }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains($"entity.{name} = ", sources[0], StringComparison.Ordinal);
    }
}
