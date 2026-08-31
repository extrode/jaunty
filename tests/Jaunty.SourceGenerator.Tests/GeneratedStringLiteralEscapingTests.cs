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
    /// AUD-R35-024. A regular C# string literal cannot span a line, and a quoted identifier may
    /// contain one - <c>CREATE TABLE t ("line1[LF]line2" TEXT)</c> is accepted by SQLite and by SQL
    /// Server in bracket form. The generator escaped backslash and double quote only, so such a name
    /// was emitted raw and the <c>.g.cs</c> failed with CS1010 in a file the consumer cannot open.
    /// The twin, <c>EntityCodeGenerator.EscapeStringLiteral</c>, has escaped the whole control range
    /// since AUD-R26-015; the fix was never carried across.
    /// </summary>
    [Theory]
    [InlineData("line1\\nline2")]
    [InlineData("line1\\rline2")]
    [InlineData("carriage\\r\\nreturn")]
    [InlineData("tab\\there")]
    [InlineData("nul\\0here")]
    [InlineData("bell\\ahere")]
    [InlineData("vertical\\vtab")]
    [InlineData("next\\u0085line")]
    [InlineData("line\\u2028separator")]
    [InlineData("para\\u2029separator")]
    [InlineData("unit\\u001fseparator")]
    public void AColumnNameContainingAControlCharacter_StillCompiles(string columnName)
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(EntityWithColumnNamed(columnName));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void ATableNameContainingANewline_StillCompiles()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using Jaunty.Attributes;

                namespace EscapeProbe;

                [Table("or\nders")]
                public partial class Order
                {
                    [Key]
                    public int Id { get; set; }
                }
                """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }

    /// <summary>
    /// Lossless as well as safe: the escaped name still has to be the name the reader is asked for.
    /// </summary>
    [Fact]
    public void AnEscapedControlCharacterRoundTrips()
    {
        (ImmutableArray<string> sources, _, _) =
            GeneratorHarness.RunAndCompile(EntityWithColumnNamed("line1\\nline2"));

        Assert.Contains("line1\\nline2", sources[0]);
        Assert.DoesNotContain("line1\nline2", sources[0]);
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
