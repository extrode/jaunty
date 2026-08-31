using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R25: ColumnMappingCache and TargetDdlGenerator each walked
/// <c>GetProperties(Public | Instance)</c> filtering only on CanRead/CanWrite, so neither honoured
/// <see cref="IgnoreAttribute"/> or <c>[NotMapped]</c>. Both mappers that feed the same entity types
/// do honour them - MetadataBuilder for the reflection path, JauntyGenerator for the source-generated
/// one - so an <c>[Ignore]</c>d property was excluded from every core CRUD statement while still
/// being read as a flat-file column and emitted into generated import DDL.
///
/// <para>
/// The same two loops also lacked the indexer guard that MetadataBuilder and ParameterCache have
/// carried since R16/AUD-R22, and an indexer reaches Expression.Property in ColumnMappingCache,
/// which throws.
/// </para>
/// </summary>
public class MappedPropertyFilterTests
{
    [Fact]
    public void ColumnMappingCache_SkipsIgnoredProperties()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(FilteredEntity));

        Assert.True(mappings.ContainsKey("Id"));
        Assert.True(mappings.ContainsKey("Name"));
        Assert.False(mappings.ContainsKey("Computed"));
    }

    [Fact]
    public void ColumnMappingCache_SkipsNotMappedProperties()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(FilteredEntity));

        Assert.False(mappings.ContainsKey("Transient"));
    }

    [Fact]
    public void ColumnMappingCache_HonoursTheColumnNameOfAnIgnoredPropertyToo()
    {
        // [Ignore] wins over [Column]: the property is not a column at all, so neither its property
        // name nor its mapped name may appear.
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(FilteredEntity));

        Assert.False(mappings.ContainsKey("renamed_ignored"));
        Assert.False(mappings.ContainsKey("RenamedIgnored"));
    }

    [Fact]
    public void ColumnMappingCache_DoesNotThrowOnAnEntityWithAnIndexer()
    {
        // Before the guard this reached Expression.Property(cast, indexerPropertyInfo), which throws.
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(IndexedEntity));

        Assert.True(mappings.ContainsKey("Id"));
        Assert.False(mappings.ContainsKey("Item"));
    }

    [Fact]
    public void TargetDdlGenerator_SkipsIgnoredAndNotMappedProperties()
    {
        List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns =
            TargetDdlGenerator.GetColumnDefinitions(typeof(FilteredEntity));

        string[] names = [.. columns.Select(c => c.Name)];

        Assert.Contains("Id", names);
        Assert.Contains("Name", names);
        Assert.DoesNotContain("Computed", names);
        Assert.DoesNotContain("Transient", names);
        Assert.DoesNotContain("renamed_ignored", names);
    }

    [Fact]
    public void TargetDdlGenerator_DoesNotEmitAnIndexerAsAColumn()
    {
        List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns =
            TargetDdlGenerator.GetColumnDefinitions(typeof(IndexedEntity));

        Assert.DoesNotContain("Item", columns.Select(c => c.Name));
    }

    [Fact]
    public void GetKeyColumnName_DoesNotTreatAnIgnoredKeyAsTheConflictTarget()
    {
        // An [Ignore]d property is not a column, so it cannot be the ON CONFLICT / MERGE target -
        // the generated DDL would reference a column that was never created.
        Assert.Equal("Id", TargetDdlGenerator.GetKeyColumnName(typeof(IgnoredKeyEntity)));
    }

    [Fact]
    public void GetKeyColumnName_IgnoredSecondKey_IsNotACompositeKeyConflict()
    {
        // Only one *mapped* [Key] remains, so this must not trip the composite-key NotSupportedException.
        Exception? ex = Record.Exception(() => TargetDdlGenerator.GetKeyColumnName(typeof(IgnoredKeyEntity)));

        Assert.Null(ex);
    }

    private class FilteredEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        [Ignore]
        public string Computed { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Transient { get; set; } = "";

        [Ignore]
        [Column("renamed_ignored")]
        public string RenamedIgnored { get; set; } = "";
    }

    private class IndexedEntity
    {
        public int Id { get; set; }

        public string this[int index]
        {
            get => index.ToString();
            set { }
        }
    }

    private class IgnoredKeyEntity
    {
        [Key]
        public int Id { get; set; }

        [Key]
        [Ignore]
        public int LegacyId { get; set; }

        public string Name { get; set; } = "";
    }
}
