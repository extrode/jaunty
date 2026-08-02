using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-030 (round-33 carry-forward). Two of <c>BuildEntityModel</c>'s skips are divergences from
/// the reflection twin rather than agreed exclusions: an <c>init</c>-only setter and an inaccessible
/// one are both <c>CanWrite == true</c>, so <c>MetadataBuilder</c> maps the column and
/// <c>SetValue</c> writes it - and referencing the generator package then drops it with no error
/// anywhere. JAUNTYGEN005 makes that loud. A get-only property is skipped by both paths and must
/// stay silent, as must an explicitly excluded one.
/// </summary>
public class DroppedPropertyDiagnosticTests
{
    private static string Entity(string members, string baseMembers = "") => $$"""
        using Jaunty.Attributes;

        namespace DropProbe;

        public class AuditedBase
        {
            {{baseMembers}}
        }

        [Table("orders")]
        public partial class Order : AuditedBase
        {
            public int Id { get; set; }
            {{members}}
        }
        """;

    private static ImmutableArray<Diagnostic> Run(string source)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(source);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        return generatorDiagnostics;
    }

    [Fact]
    public void AnInitOnlyProperty_ReportsJauntyGen005()
    {
        Diagnostic diagnostic = Assert.Single(
            Run(Entity("public string Name { get; init; }")),
            d => d.Id == "JAUNTYGEN005");

        var message = diagnostic.GetMessage();
        Assert.Contains("Order.Name", message, StringComparison.Ordinal);
        Assert.Contains("init", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABaseClassPrivateSetter_ReportsJauntyGen005()
    {
        Diagnostic diagnostic = Assert.Single(
            Run(Entity("", "public string CreatedBy { get; private set; }")),
            d => d.Id == "JAUNTYGEN005");

        Assert.Contains("CreatedBy", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("not accessible", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDroppedProperty_IsReportedSeparately()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(Entity(
            "public string Name { get; init; }", "public string CreatedBy { get; private set; }"));

        Assert.Equal(2, diagnostics.Count(d => d.Id == "JAUNTYGEN005"));
    }

    [Fact]
    public void TheDiagnostic_PointsAtTheProperty_NotTheClass()
    {
        Diagnostic diagnostic = Assert.Single(
            Run(Entity("public string Name { get; init; }")),
            d => d.Id == "JAUNTYGEN005");

        // The model holds a LocationInfo, not a Location - a Location roots its SyntaxTree and would
        // defeat the incremental caching EntityModel exists for - so the rebuilt location has a file
        // path and a span but no tree. The line is what pins "the property, not the class".
        Assert.Equal(13, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    // ------------------------------------------------------------------
    // Silence, for the skips both mapping paths agree on.
    // ------------------------------------------------------------------

    [Fact]
    public void AGetOnlyProperty_IsNotReported()
    {
        Assert.Empty(Run(Entity("public string Name => \"x\";")).Where(d => d.Id == "JAUNTYGEN005"));
    }

    [Fact]
    public void AnIgnoredInitOnlyProperty_IsNotReported()
    {
        Assert.Empty(Run(Entity("[Ignore] public string Name { get; init; }")).Where(d => d.Id == "JAUNTYGEN005"));
    }

    [Fact]
    public void APlainSettableProperty_IsNotReported()
    {
        Assert.Empty(Run(Entity("public string Name { get; set; }")).Where(d => d.Id == "JAUNTYGEN005"));
    }

    /// <summary>
    /// When nothing is generated the reflection fallback maps the entity in full, so there is no
    /// divergence to report and JAUNTYGEN004 is the whole story.
    /// </summary>
    [Fact]
    public void AnEntityThatGeneratesNothing_ReportsOnlyTheSkipDiagnostic()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, _) =
            GeneratorHarness.RunAndCompile("""
                using Jaunty.Attributes;

                namespace DropProbe;

                [Table("orders")]
                public class Order
                {
                    public int Id { get; set; }
                    public string Name { get; init; }
                }
                """);

        Assert.Empty(sources);
        Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
        Assert.Empty(generatorDiagnostics.Where(d => d.Id == "JAUNTYGEN005"));
    }
}
