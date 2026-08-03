using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-071. Twin divergence with the reflection mapper. <c>MetadataBuilder</c> guards against an
/// empty <c>[Column("")]</c> name and falls back to the property name (AUD-R32-006), and the source
/// generator carries the same guard. <c>MappedPropertyFilter.GetColumnName</c> - which
/// <see cref="ColumnMappingCache"/>, <see cref="TargetDdlGenerator"/> and
/// <c>ExpressionTranslator</c> all delegate to - was a bare
/// <c>GetCustomAttribute&lt;ColumnAttribute&gt;()?.Name ?? property.Name</c>.
/// <see cref="ColumnAttribute"/>'s constructor rejects null but not <c>""</c>, so on the flat-file
/// path the property mapped to the empty column name: the read path never matched a file column and
/// left it silently unset, and the generated import DDL carried an empty-named column.
/// </summary>
public class EmptyColumnAttributeTests
{
    private sealed class EmptyColumnEntity
    {
        [Key]
        public int Id { get; set; }

        [Column("")]
        public string Description { get; set; } = string.Empty;

        [Column("named_explicitly")]
        public string Named { get; set; } = string.Empty;
    }

    [Fact]
    public void GetColumnName_AnEmptyColumnName_FallsBackToThePropertyName()
    {
        Assert.Equal(
            nameof(EmptyColumnEntity.Description),
            MappedPropertyFilter.GetColumnName(typeof(EmptyColumnEntity).GetProperty(nameof(EmptyColumnEntity.Description))!));
    }

    [Fact]
    public void GetColumnName_ANonEmptyColumnName_StillWins()
    {
        Assert.Equal(
            "named_explicitly",
            MappedPropertyFilter.GetColumnName(typeof(EmptyColumnEntity).GetProperty(nameof(EmptyColumnEntity.Named))!));
    }

    [Fact]
    public void GetColumnName_NoColumnAttribute_IsThePropertyName()
    {
        Assert.Equal(
            nameof(EmptyColumnEntity.Id),
            MappedPropertyFilter.GetColumnName(typeof(EmptyColumnEntity).GetProperty(nameof(EmptyColumnEntity.Id))!));
    }

    [Fact]
    public void ColumnMappingCache_MapsTheEmptyNamedColumnUnderThePropertyName()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(EmptyColumnEntity));

        Assert.True(mappings.ContainsKey(nameof(EmptyColumnEntity.Description)));
        Assert.False(mappings.ContainsKey(""));
    }

    [Fact]
    public void ColumnMappingCache_StillHonoursANonEmptyColumnName()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(EmptyColumnEntity));

        Assert.True(mappings.ContainsKey("named_explicitly"));
        Assert.False(mappings.ContainsKey(nameof(EmptyColumnEntity.Named)));
    }

    [Fact]
    public void TargetDdlGenerator_DoesNotEmitAnEmptyNamedColumn()
    {
        string sql = TargetDdlGenerator.GenerateCreateTableSql(
            typeof(EmptyColumnEntity), "empty_column_entities", SqliteImportDialect.Instance);

        Assert.Contains("\"Description\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveColumnName_AnEmptyColumnName_FallsBackToThePropertyName()
    {
        Assert.Equal(
            nameof(EmptyColumnEntity.Description),
            ExpressionTranslator.ResolveColumnName<EmptyColumnEntity>(e => e.Description));
    }
}
