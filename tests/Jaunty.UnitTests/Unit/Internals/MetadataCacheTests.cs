using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Attributes;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;
using Jaunty.Configuration;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests MetadataCache&lt;T&gt; static caching, GetSetters() mapping, and PropertySetter behavior.
/// Uses real SQLite in-memory connections to produce IDataReader instances.
/// </summary>
public class MetadataCacheTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public MetadataCacheTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedData();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SeedData()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, value INTEGER NOT NULL);
            INSERT INTO items (id, name, value) VALUES (1, 'Alpha', 10);
            INSERT INTO items (id, name, value) VALUES (2, 'Beta', 20);

            CREATE TABLE mapped_items (entity_id INTEGER PRIMARY KEY, full_name TEXT NOT NULL);
            INSERT INTO mapped_items (entity_id, full_name) VALUES (1, 'Alice');

            CREATE TABLE nullable_items (id INTEGER PRIMARY KEY, name TEXT, score INTEGER);
            INSERT INTO nullable_items (id, name, score) VALUES (1, NULL, NULL);
            INSERT INTO nullable_items (id, name, score) VALUES (2, 'Bob', 42);";
        cmd.ExecuteNonQuery();
    }

    #region Test Entities

    public class SimpleItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class ColumnMappedItem
    {
        [Column("entity_id")]
        public int Id { get; set; }

        [Column("full_name")]
        public string Name { get; set; } = string.Empty;
    }

    public class NullableItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? Score { get; set; }
    }

    public class NonNullableValueItem
    {
        public int Id { get; set; }
        public int Score { get; set; }
    }

    public class SubsetItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // AUD-R22: a *writable* indexer (unlike a get-only one) passes MetadataBuilder's
    // CanWrite check and used to be added as a ColumnMetadata. MetadataCache<T>'s static
    // constructor then calls CreateGetter/CreateSetter (Expression.Property) on every column,
    // which throws ArgumentException("Incorrect number of indexes") for an indexer - failing the
    // type's static initializer and breaking ALL reflection-based mapping for the entity, not
    // just the indexer.
    public class WritableIndexerItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        private readonly Dictionary<int, string> _data = new();
        public string this[int index]
        {
            get => _data.TryGetValue(index, out string? v) ? v : string.Empty;
            set => _data[index] = value;
        }
    }

    #endregion

    #region Metadata Caching

    [Fact]
    public void Metadata_IsCachedStatically()
    {
        var meta1 = MetadataCache<SimpleItem>.Metadata;
        var meta2 = MetadataCache<SimpleItem>.Metadata;

        Assert.Same(meta1, meta2);
    }

    [Fact]
    public void Metadata_ContainsCorrectColumns()
    {
        var meta = MetadataCache<SimpleItem>.Metadata;

        Assert.Equal(3, meta.Columns.Count());
        Assert.Contains(meta.Columns, c => c.Property.Name == "Id");
        Assert.Contains(meta.Columns, c => c.Property.Name == "Name");
        Assert.Contains(meta.Columns, c => c.Property.Name == "Value");
    }

    [Fact]
    public void Metadata_ColumnMappedEntity_UsesColumnNames()
    {
        var meta = MetadataCache<ColumnMappedItem>.Metadata;

        Assert.Contains(meta.Columns, c => c.ColumnName == "entity_id");
        Assert.Contains(meta.Columns, c => c.ColumnName == "full_name");
    }

    [Fact]
    public void Properties_EntityWithWritableIndexer_ExcludesIndexerAndDoesNotThrow()
    {
        var properties = MetadataCache<WritableIndexerItem>.Properties;

        Assert.Equal(2, properties.Length);
        Assert.Contains(properties, p => p.PropertyName == "Id");
        Assert.Contains(properties, p => p.PropertyName == "Name");
        Assert.DoesNotContain(properties, p => p.PropertyName == "Item");
    }

    #endregion

    #region GetSetters - Strict Mode

    [Fact]
    public void GetSetters_StrictMode_AllColumnsMatch_ReturnsSetters()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value FROM items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict);

        Assert.Equal(3, setters.Length);
    }

    [Fact]
    public void GetSetters_StrictMode_MissingColumn_Throws()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM items LIMIT 1"; // missing 'value'
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var ex = Assert.Throws<InvalidOperationException>(
            () => MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict));
        Assert.Contains("has no matching column in result set", ex.Message);
    }

    [Fact]
    public void GetSetters_StrictMode_ExtraColumn_Throws()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value, 999 AS extra FROM items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var ex = Assert.Throws<InvalidOperationException>(
            () => MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict));
        Assert.Contains("does not map to any property", ex.Message);
    }

    [Fact]
    public void GetSetters_StrictMode_ColumnAttribute_MapsCorrectly()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT entity_id, full_name FROM mapped_items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<ColumnMappedItem>.GetSetters(reader, MappingMode.Strict);

        Assert.Equal(2, setters.Length);
    }

    [Fact]
    public void GetSetters_StrictMode_CaseInsensitive()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT ID, NAME, VALUE FROM items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict);

        Assert.Equal(3, setters.Length);
    }

    #endregion

    #region GetSetters - Projection Mode

    [Fact]
    public void GetSetters_ProjectionMode_ExtraColumns_Ignored()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value, 999 AS extra FROM items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(3, setters.Length);
    }

    [Fact]
    public void GetSetters_ProjectionMode_MissingColumns_PartialSetters()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM items LIMIT 1"; // missing 'value'
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(2, setters.Length);
    }

    [Fact]
    public void GetSetters_ProjectionMode_SubsetEntity_Works()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value FROM items LIMIT 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        // SubsetItem only has Id and Name, so 'value' column is extra
        var setters = MetadataCache<SubsetItem>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(2, setters.Length);
    }

    [Fact]
    public void GetSetters_EmptyResultSet_ReturnsEmptyArray()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value FROM items WHERE 1=0";
        using var reader = cmd.ExecuteReader();

        // FieldCount is still 3 even with no rows
        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict);

        Assert.Equal(3, setters.Length);
    }

    #endregion

    #region PropertySetter - Value Application

    [Fact]
    public void PropertySetter_Set_AppliesValueToEntity()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value FROM items WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict);

        var item = new SimpleItem();
        foreach (var setter in setters)
            setter.Set(item, reader);

        Assert.Equal(1, item.Id);
        Assert.Equal("Alpha", item.Name);
        Assert.Equal(10, item.Value);
    }

    [Fact]
    public void PropertySetter_Set_NullableProperties_HandlesNull()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, score FROM nullable_items WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<NullableItem>.GetSetters(reader, MappingMode.Strict);

        var item = new NullableItem();
        foreach (var setter in setters)
            setter.Set(item, reader);

        Assert.Equal(1, item.Id);
        Assert.Null(item.Name);
        Assert.Null(item.Score);
    }

    [Fact]
    public void PropertySetter_Set_NullableProperties_HandlesNonNull()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, score FROM nullable_items WHERE id = 2";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<NullableItem>.GetSetters(reader, MappingMode.Strict);

        var item = new NullableItem();
        foreach (var setter in setters)
            setter.Set(item, reader);

        Assert.Equal(2, item.Id);
        Assert.Equal("Bob", item.Name);
        Assert.Equal(42, item.Score);
    }

    [Fact]
    public void PropertySetter_Set_NonNullableValueType_NullThrows()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, score FROM nullable_items WHERE id = 1"; // score is NULL
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<NonNullableValueItem>.GetSetters(reader, MappingMode.Strict);

        var item = new NonNullableValueItem();
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var setter in setters)
                setter.Set(item, reader);
        });
        Assert.Contains("Cannot assign NULL to non-nullable property", ex.Message);
        Assert.Contains("Score", ex.Message);
    }

    [Fact]
    public void PropertySetter_Set_ColumnAttribute_MapsCorrectly()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT entity_id, full_name FROM mapped_items WHERE entity_id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<ColumnMappedItem>.GetSetters(reader, MappingMode.Strict);

        var item = new ColumnMappedItem();
        foreach (var setter in setters)
            setter.Set(item, reader);

        Assert.Equal(1, item.Id);
        Assert.Equal("Alice", item.Name);
    }

    [Fact]
    public void PropertySetter_Set_MultipleRows_MapsAllCorrectly()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, value FROM items ORDER BY id";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Strict);

        var results = new List<SimpleItem>();

        do
        {
            var item = new SimpleItem();
            foreach (var setter in setters)
                setter.Set(item, reader);
            results.Add(item);
        }
        while (reader.Read());

        Assert.Equal(2, results.Count);
        Assert.Equal("Alpha", results[0].Name);
        Assert.Equal(10, results[0].Value);
        Assert.Equal("Beta", results[1].Name);
        Assert.Equal(20, results[1].Value);
    }

    #endregion

    #region CreateSetter - Expression Trees

    [Fact]
    public void CreateSetter_CompiledSetter_WorksForIntProperty()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM items WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Projection);

        var item = new SimpleItem();
        setters[0].Set(item, reader);

        Assert.Equal(1, item.Id);
    }

    [Fact]
    public void CreateSetter_CompiledSetter_WorksForStringProperty()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM items WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<SimpleItem>.GetSetters(reader, MappingMode.Projection);

        var item = new SimpleItem();
        setters[0].Set(item, reader);

        Assert.Equal("Alpha", item.Name);
    }

    [Fact]
    public void CreateSetter_CompiledSetter_WorksForNullableIntProperty()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT score FROM nullable_items WHERE id = 2";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<NullableItem>.GetSetters(reader, MappingMode.Projection);

        var item = new NullableItem();
        setters[0].Set(item, reader);

        Assert.Equal(42, item.Score);
    }

    #endregion

    #region ColumnToIndex - Dual Registration

    [Fact]
    public void ColumnToIndex_RegistersBothColumnNameAndPropertyName()
    {
        // ColumnMappedItem has [Column("entity_id")] on Id and [Column("full_name")] on Name
        // GetSetters should work with either column name or property name
        using var cmd = _connection.CreateCommand();
        // Use property names directly (Id, Name) instead of column names
        cmd.CommandText = "SELECT 1 AS Id, 'Test' AS Name";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var setters = MetadataCache<ColumnMappedItem>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(2, setters.Length);

        var item = new ColumnMappedItem();
        foreach (var setter in setters)
            setter.Set(item, reader);

        Assert.Equal(1, item.Id);
        Assert.Equal("Test", item.Name);
    }

    #endregion
}