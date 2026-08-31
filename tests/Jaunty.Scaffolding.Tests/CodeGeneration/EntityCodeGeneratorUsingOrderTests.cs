using System.Globalization;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Tests.CodeGeneration;

/// <summary>
/// AUD-R35-265. <c>AppendUsings</c> sorted with <c>OrderBy(x =&gt; x)</c>, which resolves to
/// <c>Comparer&lt;string&gt;.Default</c> and is culture-sensitive, while every other ordering and
/// comparison decision in the generator is explicitly ordinal. Generated using-directive order
/// could therefore differ between two machines scaffolding the same database.
/// </summary>
public class EntityCodeGeneratorUsingOrderTests
{
    /// <summary>
    /// Returns a fixed <c>RequiredUsing</c> per column, so a test can choose the namespaces whose
    /// ordinal and linguistic orders disagree.
    /// </summary>
    private sealed class FixedUsingTypeMapper : ITypeMapper
    {
        public CSharpTypeInfo MapToCSharpType(ColumnSchema column) =>
            new() { TypeName = "string", IsValueType = false, RequiredUsing = column.DataType };
    }

    private static CodeGeneratorOptions Options() =>
        new()
        {
            Namespace = "Test.Entities",
            UseFileScopedNamespace = true,
            GenerateTableAttribute = true,
            GenerateColumnAttribute = true,
            GenerateKeyAttribute = true,
            GenerateDatabaseGeneratedAttribute = true,
            UseNullableReferenceTypes = true,
            Singularize = true
        };

    // "System" and "acme.Widgets" order one way ordinally (uppercase S is 83, lowercase a is 97)
    // and the other way linguistically (a before s, case being a tie-break), in every culture
    // including the invariant one.
    private static TableSchema TableWithClashingUsings() =>
        new()
        {
            SchemaName = "dbo",
            TableName = "Products",
            Columns =
            [
                new ColumnSchema { ColumnName = "A", DataType = "acme.Widgets", OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "B", DataType = "System", OrdinalPosition = 2 },
            ]
        };

    private static List<string> UsingsOf(string code) =>
        [.. code.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("using ", StringComparison.Ordinal))];

    [Fact]
    public void UsingsAreOrderedOrdinally_NotLinguistically()
    {
        var generator = new EntityCodeGenerator(new FixedUsingTypeMapper());

        var code = generator.GenerateEntity(TableWithClashingUsings(), Options());

        Assert.Equal(["using System;", "using acme.Widgets;"], UsingsOf(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("da-DK")]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void TheOrderDoesNotDependOnTheCurrentCulture(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var generator = new EntityCodeGenerator(new FixedUsingTypeMapper());

            var code = generator.GenerateEntity(TableWithClashingUsings(), Options());

            Assert.Equal(["using System;", "using acme.Widgets;"], UsingsOf(code));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
