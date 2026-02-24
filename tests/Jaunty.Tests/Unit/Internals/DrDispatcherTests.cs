using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests DrDispatcher mapper resolution logic through integration with real queries.
/// DrDispatcher resolves mappers in priority order:
/// 1. User override (CommandOptions.Mapper)
/// 2. Special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject)
/// 3. IMapped&lt;T&gt; (cached in MappedCache)
/// 4. Metadata reflection fallback
/// </summary>
public class DrDispatcherTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public DrDispatcherTests()
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
            CREATE TABLE test_items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, value INTEGER NOT NULL);
            INSERT INTO test_items (id, name, value) VALUES (1, 'Alice', 100);
            INSERT INTO test_items (id, name, value) VALUES (2, 'Bob', 200);";
        cmd.ExecuteNonQuery();
    }

    #region Test Entities

    public class PlainItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class MappedItem : IMapped<MappedItem>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        [Ignore]
        public bool WasMappedByCustomMapper { get; set; }

#if NET8_0_OR_GREATER
        public static MappedItem ReadEntity(IDataReader reader)
#else
        public MappedItem ReadEntity(IDataReader reader)
#endif
        {
            return new MappedItem
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Value = reader.GetInt32(reader.GetOrdinal("Value")),
                WasMappedByCustomMapper = true
            };
        }
    }

    #endregion

    #region Priority 1: User Override Mapper

    [Fact]
    public void Resolve_UserOverrideMapper_TakesPriority()
    {
        bool customMapperUsed = false;
        Func<IDataReader, PlainItem> mapper = r =>
        {
            customMapperUsed = true;
            return new PlainItem
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Name = r.GetString(r.GetOrdinal("Name")),
                Value = r.GetInt32(r.GetOrdinal("Value"))
            };
        };

        var results = _connection.Query<PlainItem>(
            "SELECT id AS Id, name AS Name, value AS Value FROM test_items WHERE id = 1",
            CommandOptions<PlainItem>.WithMapper(mapper));

        Assert.Single(results);
        Assert.True(customMapperUsed);
    }

    #endregion

    #region Priority 2: Special Types

    [Fact]
    public void Resolve_DictionaryStringObject_ReturnsDictionaries()
    {
        var results = _connection.Query<Dictionary<string, object>>(
            "SELECT id AS Id, name AS Name, value AS Value FROM test_items ORDER BY id");

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0]["Name"]);
        Assert.Equal("Bob", results[1]["Name"]);
    }

    [Fact]
    public void Resolve_KeyValuePair_ReturnsPairs()
    {
        var results = _connection.Query<KeyValuePair<int, string>>(
            "SELECT id, name FROM test_items ORDER BY id");

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Key);
        Assert.Equal("Alice", results[0].Value);
    }

    [Fact]
    public void Resolve_ValueTuple_ReturnsTuples()
    {
        var results = _connection.Query<(int, string, int)>(
            "SELECT id, name, value FROM test_items ORDER BY id");

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Item1);
        Assert.Equal("Alice", results[0].Item2);
        Assert.Equal(100, results[0].Item3);
    }

    #endregion

    #region Priority 3: IMapped<T>

    [Fact]
    public void Resolve_IMappedEntity_UsesCustomMapper()
    {
        var results = _connection.Query<MappedItem>(
            "SELECT id AS Id, name AS Name, value AS Value FROM test_items WHERE id = 1");

        Assert.Single(results);
        Assert.True(results[0].WasMappedByCustomMapper);
        Assert.Equal("Alice", results[0].Name);
    }

    #endregion

    #region Priority 4: Metadata Reflection

    [Fact]
    public void Resolve_PlainEntity_UsesReflectionMapper()
    {
        var results = _connection.Query<PlainItem>(
            "SELECT id AS Id, name AS Name, value AS Value FROM test_items ORDER BY id");

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0].Name);
        Assert.Equal(100, results[0].Value);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Resolve_KeyValuePair_InsufficientColumns_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _connection.Query<KeyValuePair<int, string>>("SELECT id FROM test_items"));
    }

    [Fact]
    public void Resolve_ValueTuple_InsufficientColumns_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _connection.Query<(int, string, int)>("SELECT id, name FROM test_items"));
    }

    #endregion
}
