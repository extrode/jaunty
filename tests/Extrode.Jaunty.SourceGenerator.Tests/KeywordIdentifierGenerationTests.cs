using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R38 generator audit. <c>ISymbol.Name</c> carries no <c>@</c>, and the generator emitted it
/// bare, so a keyword used as an entity, enclosing type or property name produced
/// <c>partial class event</c> or <c>entity.default = ...</c> in a <c>.g.cs</c>. The column name and
/// the metadata's property name stay unescaped: they are data, not identifiers.
/// </summary>
public class KeywordIdentifierGenerationTests
{
    private const string Header = """
        using Extrode.Jaunty.Attributes;

        namespace KeywordProbe;

        """;

    [Theory]
    [InlineData("[Table(\"t\")] public partial class Order { public int Id { get; set; } public string @event { get; set; } = \"\"; public int @default { get; set; } }")]
    [InlineData("[Table(\"t\")] public partial class @event { public int Id { get; set; } public string Name { get; set; } = \"\"; }")]
    [InlineData("public partial class @class { [Table(\"t\")] public partial class Order { public int Id { get; set; } } }")]
    public void KeywordNames_CompileInTheGeneratedSource(string body)
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Header + body);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void KeywordProperty_KeepsTheBareNameAsColumnAndPropertyName()
    {
        (ImmutableArray<string> sources, _, _) = GeneratorHarness.RunAndCompile(
            Header + "[Table(\"t\")] public partial class Order { public int Id { get; set; } public string @event { get; set; } = \"\"; }");

        Assert.Contains("entity.@event = ", sources[0], StringComparison.Ordinal);
        Assert.Contains("new string[] { \"Id\", \"event\" }", sources[0], StringComparison.Ordinal);
        Assert.Contains("new ColumnInfo(n.Column(1), \"event\"", sources[0], StringComparison.Ordinal);
    }

    [Fact]
    public void VerbatimClassName_DoesNotShareAHintNameWithAnUnderscoredOne()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using Extrode.Jaunty.Attributes;

                namespace Q { [Table("t")] public partial class @class { public int Id { get; set; } } }
                [Table("u")] public partial class Q_class { public int Id { get; set; } }
                """);

        Assert.Equal(2, sources.Length);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Id.StartsWith("CS", StringComparison.Ordinal));
    }
}
