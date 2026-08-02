using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R33-011 (coverage). <c>EscapeStringLiteral</c> is the only thing standing between a
/// <c>[Table]</c>/<c>[Column]</c> name and a generated string literal it could otherwise break out
/// of, injecting arbitrary code into a <c>.g.cs</c> the consumer never sees. It had no test: no
/// fixture entity carries a <c>"</c> or <c>\</c> in an attribute name, and nothing asserted on
/// escaped output. AUD-R32-003 covered the <em>dialects</em>' <c>EscapeStringLiteral</c>, which is a
/// different method.
/// <para>
/// A fixture entity could not cover this - if escaping were broken, the test project would fail to
/// build rather than fail a test. Nor can a text assertion: a string literal containing
/// <c>class Injected</c> and an escaped-out-of declaration of one are the same substring. So these
/// compile the output and ask the <em>compilation</em> whether such a type exists.
/// </para>
/// </summary>
public class GeneratedStringLiteralEscapingTests
{
    private const string Injection = "; class Injected { } //";

    private static string EntityWithColumnNamed(string columnName) => $$"""
        using Jaunty.Attributes;

        namespace EscapeProbe;

        [Table("orders")]
        public partial class Order
        {
            [Key]
            public int Id { get; set; }

            [Column("{{columnName}}")]
            public string? Name { get; set; }
        }
        """;

    [Theory]
    [InlineData("a\\\"" + Injection)]
    [InlineData("back\\\\slash")]
    [InlineData("quote\\\"inside")]
    [InlineData("trailing\\\\")]
    public void AColumnNameContainingQuotesOrBackslashes_DeclaresNothingExtra(string columnName)
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors, Compilation output) =
            GeneratorHarness.Run(EntityWithColumnNamed(columnName));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Null(output.GetTypeByMetadataName("Injected"));
    }

    /// <summary>
    /// The escaping must be lossless as well as safe: the column name reaches
    /// <c>reader.GetOrdinal</c> and the parameter name, so mangling it would break every query
    /// against such a column instead of breaking the build.
    /// </summary>
    [Fact]
    public void AnEscapedColumnNameRoundTripsIntoTheGeneratedSource()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(EntityWithColumnNamed("odd\\\"name"));

        Assert.Empty(compileErrors);

        // The probe source declares [Column("odd\"name")], so the column name is `odd"name` and the
        // generated literal must spell it `odd\"name` again - not `odd"name`, which would close the
        // literal, and not `odd\\"name`, which would change the name.
        Assert.Contains("odd\\\"name", sources[0]);
    }

    /// <summary>
    /// The same hazard through the table name, which reaches a different set of call sites.
    /// </summary>
    [Fact]
    public void ATableNameContainingAQuote_DeclaresNothingExtra()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors, Compilation output) =
            GeneratorHarness.Run($$"""
                using Jaunty.Attributes;

                namespace EscapeProbe;

                [Table("or\"ders{{Injection}}")]
                public partial class Order
                {
                    [Key]
                    public int Id { get; set; }
                }
                """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Null(output.GetTypeByMetadataName("Injected"));
    }
}
