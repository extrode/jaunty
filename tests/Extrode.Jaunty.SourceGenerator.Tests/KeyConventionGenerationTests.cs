using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// Without <c>[Key]</c>, the generator takes <c>Id</c> or <c>{ClassName}Id</c> as the key, the
/// same convention as the reflection path. The test entities compiled into this project were
/// generated at build time, so only a generator run here can show which convention the current
/// generator applies.
/// </summary>
public class KeyConventionGenerationTests
{
    private const string Source = """
        using Extrode.Jaunty.Attributes;

        namespace KeyProbe;

        [Table("invoices")]
        public partial class Invoice
        {
            public int InvoiceId { get; set; }
            public int CustomerId { get; set; }
        }
        """;

    private static string Generated()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors, _) = GeneratorHarness.Run(Source);

        Assert.Empty(compileErrors);
        return Assert.Single(sources);
    }

    [Fact]
    public void ClassNameIdProperty_IsTheKey()
    {
        Assert.Contains("\"InvoiceId\", true, ", Generated(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherIdSuffixedProperty_IsNotTheKey()
    {
        Assert.Contains("\"CustomerId\", false, ", Generated(), StringComparison.Ordinal);
    }
}
