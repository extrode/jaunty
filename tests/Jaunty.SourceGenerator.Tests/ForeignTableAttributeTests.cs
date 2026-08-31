using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-031 (round-33 carry-forward). Entity discovery matches <c>[Table]</c> by fully-qualified
/// metadata name, but <c>GetTableNameAndSchema</c> and <c>FindHandWrittenMapper</c> asked for the
/// simple name. <c>TableAttribute</c> is a common enough name that a consumer or another library
/// defines one, and either of those two questions answered "yes" for it: a hand-written
/// <c>IMapped&lt;T&gt;</c> carrying a foreign <c>[Table]</c> lost its JAUNTYGEN002 warning while
/// staying exactly as trim-unsafe as before, and an entity carrying both a recognized and a foreign
/// <c>[Table]</c> could have its table name and schema read off whichever came first in source
/// order.
/// </summary>
public class ForeignTableAttributeTests
{
    /// <summary>
    /// A <c>TableAttribute</c> that is nobody's mapping attribute, shaped like both recognized ones
    /// so a simple-name match cannot tell them apart.
    /// </summary>
    private const string ForeignAttribute = """
        namespace Foreign
        {
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
            public sealed class TableAttribute : System.Attribute
            {
                public TableAttribute(string name) { Name = name; }
                public string Name { get; }
                public string Schema { get; set; }
            }
        }
        """;

    [Fact]
    public void AForeignTableAttribute_DoesNotSuppressJauntyGen002()
    {
        (ImmutableArray<string> _, ImmutableArray<Diagnostic> diagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using System.Data;
                using Foreign;
                using Jaunty.Interfaces;

                namespace ForeignProbe
                {
                    [Table("hand_rolled")]
                    public class HandRolled : IMapped<HandRolled>
                    {
                        public int Id { get; set; }

                #if NET8_0_OR_GREATER
                        public static HandRolled ReadEntity(IDataReader reader) => new HandRolled();
                #else
                        public HandRolled ReadEntity(IDataReader reader) => new HandRolled();
                #endif
                    }
                }
                """ + ForeignAttribute);

        Assert.Empty(compileErrors);
        Assert.Contains("HandRolled", Assert.Single(diagnostics, d => d.Id == "JAUNTYGEN002").GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AJauntyTableAttribute_StillSuppressesJauntyGen002()
    {
        (ImmutableArray<string> _, ImmutableArray<Diagnostic> diagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                using System.Data;
                using Jaunty.Attributes;
                using Jaunty.Interfaces;

                namespace ForeignProbe
                {
                    [Table("hand_rolled")]
                    public partial class HandRolled : IMapped<HandRolled>
                    {
                        public int Id { get; set; }
                    }
                }
                """);

        Assert.Empty(compileErrors);
        Assert.Empty(diagnostics.Where(d => d.Id == "JAUNTYGEN002"));
    }

    [Theory]
    [InlineData("""[Foreign.Table("wrong_table", Schema = "wrong_schema")]""", """[Jaunty.Attributes.Table("orders", "sales")]""")]
    [InlineData("""[Jaunty.Attributes.Table("orders", "sales")]""", """[Foreign.Table("wrong_table", Schema = "wrong_schema")]""")]
    public void AForeignTableAttributeAlongsideTheRealOne_NeverNamesTheTable(string first, string second)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile($$"""
                namespace ForeignProbe
                {
                    {{first}}
                    {{second}}
                    public partial class Order
                    {
                        public int Id { get; set; }
                    }
                }
                """ + ForeignAttribute);

        Assert.Empty(compileErrors);
        var generated = Assert.Single(sources);

        Assert.Contains("\"orders\"", generated, StringComparison.Ordinal);
        Assert.Contains("\"sales\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong_table", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong_schema", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A type carrying only a foreign <c>[Table]</c> is not an entity at all, so nothing is
    /// generated for it - discovery has always matched by metadata name, and this pins that the two
    /// post-discovery questions now agree with it.
    /// </summary>
    [Fact]
    public void AForeignTableAttributeAlone_GeneratesNothing()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile("""
                namespace ForeignProbe
                {
                    [Foreign.Table("wrong_table")]
                    public partial class Order
                    {
                        public int Id { get; set; }
                    }
                }
                """ + ForeignAttribute);

        Assert.Empty(compileErrors);
        Assert.Empty(sources);
    }
}
