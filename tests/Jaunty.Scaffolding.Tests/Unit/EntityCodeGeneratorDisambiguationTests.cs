using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R35-036. <c>ResolvePropertyName</c> renames a colliding property (Name to Name1), and the
/// only thing that preserves the column mapping afterwards is <c>[Column]</c> - which was emitted
/// only when <c>GenerateColumnAttribute</c> was true. Under <c>--no-column-attr</c> the renamed
/// property came out bare and mapped to a column "Name1" that does not exist: the reflection path
/// leaves it unset, the generated path resolves a missing ordinal. The disambiguation is
/// generator-internal, so opting out of <c>[Column]</c> silently converted a compile error the
/// generator was fixing into a runtime mapping fault.
/// </summary>
public class EntityCodeGeneratorDisambiguationTests
{
    private static string Generate(string tableName, string[] columnNames, bool generateColumnAttribute)
    {
        var columns = new List<ColumnSchema>();

        for (int i = 0; i < columnNames.Length; i++)
        {
            columns.Add(new ColumnSchema
            {
                ColumnName = columnNames[i],
                DataType = "TEXT",
                IsNullable = true,
                IsPrimaryKey = false,
                IsIdentity = false,
                IsComputed = false,
                OrdinalPosition = i + 1
            });
        }

        var table = new TableSchema
        {
            SchemaName = "",
            TableName = tableName,
            Columns = columns,
            PrimaryKey = null,
            ForeignKeys = []
        };

        return new EntityCodeGenerator(new SQLiteTypeMapper()).GenerateEntity(
            table,
            new CodeGeneratorOptions { Namespace = "N", GenerateColumnAttribute = generateColumnAttribute });
    }

    /// <summary>
    /// Two columns whose PascalCased names collide: "name" and "Name" both become Name, so the
    /// second is renamed Name1 and needs the attribute to still find its column.
    /// </summary>
    [Fact]
    public void ARenamedPropertyKeepsItsColumnAttributeWithoutTheOption()
    {
        string code = Generate("things", ["name", "Name"], generateColumnAttribute: false);

        Assert.Contains("public string? Name1", code, StringComparison.Ordinal);
        Assert.Contains("[Jaunty.Attributes.Column(\"Name\")]", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The class-name collision is the same case: usedPropertyNames is seeded with the class name,
    /// so a column matching it (CS0542) is renamed too.
    /// </summary>
    [Fact]
    public void APropertyRenamedOffTheClassNameKeepsItsColumnAttribute()
    {
        string code = Generate("Product", ["Product"], generateColumnAttribute: false);

        Assert.Contains("Product1", code, StringComparison.Ordinal);
        Assert.Contains("[Jaunty.Attributes.Column(\"Product\")]", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The option still does what it says for every property the generator did not rename: a
    /// snake_case column whose PascalCase name differs is exactly the case the caller opts out of.
    /// </summary>
    [Fact]
    public void AnUnrenamedPropertyStillHonoursTheOption()
    {
        string code = Generate("things", ["first_name"], generateColumnAttribute: false);

        Assert.Contains("FirstName", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Jaunty.Attributes.Column", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnrenamedPropertyGetsTheAttributeWhenTheOptionIsOn()
    {
        string code = Generate("things", ["first_name"], generateColumnAttribute: true);

        Assert.Contains("[Jaunty.Attributes.Column(\"first_name\")]", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// A property whose name already matches its column gets no attribute either way - the
    /// condition that suppresses it is unchanged.
    /// </summary>
    [Fact]
    public void AMatchingNameNeverGetsTheAttribute()
    {
        Assert.DoesNotContain(
            "Jaunty.Attributes.Column",
            Generate("things", ["Title"], generateColumnAttribute: true),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Three-way collision: the suffix keeps incrementing and every renamed property carries its
    /// own column name.
    /// </summary>
    [Fact]
    public void EveryRenamedPropertyInAChainCarriesItsOwnColumn()
    {
        string code = Generate("things", ["name", "Name", "_name"], generateColumnAttribute: false);

        Assert.Contains("Name1", code, StringComparison.Ordinal);
        Assert.Contains("Name2", code, StringComparison.Ordinal);
        Assert.Contains("[Jaunty.Attributes.Column(\"Name\")]", code, StringComparison.Ordinal);
        Assert.Contains("[Jaunty.Attributes.Column(\"_name\")]", code, StringComparison.Ordinal);
    }
}
