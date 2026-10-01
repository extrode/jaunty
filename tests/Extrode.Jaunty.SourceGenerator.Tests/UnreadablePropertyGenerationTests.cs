using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R38 generator audit. The settable-shape guards checked the setter only, so a write-only
/// property, or an inherited one whose getter is <c>private</c>, was mapped and the generated
/// binders and getter lambdas read it: CS0154 and CS0271 inside a <c>.g.cs</c>. Both are now left
/// out and reported as JAUNTYGEN005, like the setter cases, while a getter the entity can reach stays
/// mapped.
/// </summary>
public class UnreadablePropertyGenerationTests
{
    private static string Entity(string baseMembers, string ownMembers = "") => $$"""
        using Extrode.Jaunty.Attributes;

        namespace GetterProbe;

        public abstract class AuditableBase
        {
        {{baseMembers}}
        }

        [Table("orders")]
        public partial class Order : AuditableBase
        {
            public int Id { get; set; }
        {{ownMembers}}
        }
        """;

    public static TheoryData<string, string> UnreadableShapes => new()
    {
        { "", "    public string Secret { set { } }" },
        { "    public string Secret { private get; set; } = \"\";", "" },
        { "    public string Secret { set { } }", "" },
    };

    [Theory]
    [MemberData(nameof(UnreadableShapes))]
    public void UnreadableProperty_IsDroppedReportedAndTheOutputCompiles(string baseMembers, string ownMembers)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(baseMembers, ownMembers));

        Assert.Empty(compileErrors);
        Assert.Single(sources);
        Assert.DoesNotContain("Secret", sources[0]);
        Diagnostic dropped = Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN005");
        Assert.Contains("'Order.Secret'", dropped.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("no getter accessible", dropped.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("    public string Secret { protected get; set; } = \"\";", "")]
    [InlineData("    public string Secret { internal get; set; } = \"\";", "")]
    [InlineData("", "    public string Secret { private get; set; } = \"\";")]
    public void ReachableGetter_IsStillMapped(string baseMembers, string ownMembers)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(baseMembers, ownMembers));

        Assert.Empty(compileErrors);
        Assert.Contains("entity.Secret", sources[0], StringComparison.Ordinal);
        Assert.DoesNotContain(generatorDiagnostics, d => d.GetMessage().Contains("Secret", StringComparison.Ordinal));
    }
}
