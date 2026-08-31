using System.Data;

using Jaunty;
using Jaunty.SourceGenerator.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Runtime validation for PRD-001: the source-generated ordinal cache must never
/// serve stale ordinals when the result shape (column order, subset, or width)
/// changes between readers or between result sets on the same reader. These tests
/// exercise the actual generator output compiled into this project, end-to-end
/// through a real ADO.NET provider (the hand-simulated equivalent that used to live
/// in Jaunty.Tests as OrdinalMapCacheEntryTests was removed - it tested a copy of
/// the generator's template, not the template itself).
/// </summary>
public sealed class SqliteGeneratedMapperShapeTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteGeneratedMapperShapeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE gen_products (
                product_id INTEGER PRIMARY KEY,
                product_name TEXT NOT NULL,
                unit_price NUMERIC NULL,
                discontinued INTEGER NOT NULL
            );
            INSERT INTO gen_products VALUES (1, 'Chai', 18.0, 0);
            INSERT INTO gen_products VALUES (2, 'Chang', 19.0, 1);
            INSERT INTO gen_products VALUES (3, 'Aniseed Syrup', NULL, 0);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Query_DifferentColumnOrder_AcrossQueries_MapsCorrectly()
    {
        List<GenProduct> natural = _connection.Query<GenProduct>(
            "SELECT product_id, product_name, unit_price, discontinued FROM gen_products ORDER BY product_id");

        List<GenProduct> reversed = _connection.Query<GenProduct>(
            "SELECT discontinued, unit_price, product_name, product_id FROM gen_products ORDER BY product_id");

        Assert.Equal(3, natural.Count);
        Assert.Equal(3, reversed.Count);
        for (int i = 0; i < natural.Count; i++)
        {
            Assert.Equal(natural[i].ProductId, reversed[i].ProductId);
            Assert.Equal(natural[i].ProductName, reversed[i].ProductName);
            Assert.Equal(natural[i].UnitPrice, reversed[i].UnitPrice);
            Assert.Equal(natural[i].Discontinued, reversed[i].Discontinued);
        }

        Assert.Equal("Chai", natural[0].ProductName);
        Assert.Equal(18.0m, natural[0].UnitPrice);
        Assert.True(natural[1].Discontinued);
        Assert.Null(natural[2].UnitPrice);
    }

    [Fact]
    public void Query_ExtraAndReorderedColumns_MapsCorrectly()
    {
        List<GenProduct> rows = _connection.Query<GenProduct>(
            "SELECT 42 AS noise_a, unit_price, 'x' AS noise_b, product_name, discontinued, product_id FROM gen_products WHERE product_id = 2");

        GenProduct row = Assert.Single(rows);
        Assert.Equal(2, row.ProductId);
        Assert.Equal("Chang", row.ProductName);
        Assert.Equal(19.0m, row.UnitPrice);
        Assert.True(row.Discontinued);
    }

    [Fact]
    public void ReadEntity_SameReader_NextResult_DifferentColumnOrder_MapsCorrectly()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT product_id, product_name, unit_price, discontinued FROM gen_products WHERE product_id = 1;
            SELECT discontinued, unit_price, product_name, product_id FROM gen_products WHERE product_id = 2;
            """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        GenProduct first = GenProduct.ReadEntity(reader);
        Assert.Equal(1, first.ProductId);
        Assert.Equal("Chai", first.ProductName);
        Assert.Equal(18.0m, first.UnitPrice);
        Assert.False(first.Discontinued);

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        GenProduct second = GenProduct.ReadEntity(reader);
        Assert.Equal(2, second.ProductId);
        Assert.Equal("Chang", second.ProductName);
        Assert.Equal(19.0m, second.UnitPrice);
        Assert.True(second.Discontinued);
    }

    [Fact]
    public void ReadEntity_SameReader_NextResult_MissingColumn_ThrowsInsteadOfStaleMapping()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT product_id, product_name, unit_price, discontinued FROM gen_products WHERE product_id = 1;
            SELECT product_id, product_name FROM gen_products WHERE product_id = 2;
            """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        GenProduct first = GenProduct.ReadEntity(reader);
        Assert.Equal(1, first.ProductId);

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        // The cached ordinals from the first result set no longer match; strict
        // generated mapping must fail loudly on the narrower shape, not reuse them.
        // Microsoft.Data.Sqlite's GetOrdinal throws ArgumentOutOfRangeException for a missing
        // column name (unlike System.Data.SqlClient, which throws IndexOutOfRangeException for
        // the same failure mode) - confirmed by running this test against the real provider.
        Assert.Throws<ArgumentOutOfRangeException>(() => GenProduct.ReadEntity(reader));
    }

    [Fact]
    public void CreateRowMapper_SameShapeAcrossRows_MapsEachRowCorrectly()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT product_id, product_name, unit_price, discontinued FROM gen_products ORDER BY product_id";

        using SqliteDataReader reader = cmd.ExecuteReader();
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);

        var rows = new List<GenProduct>();
        while (reader.Read())
            rows.Add(mapper(reader));

        Assert.Equal(3, rows.Count);
        Assert.Equal("Chai", rows[0].ProductName);
        Assert.Equal(18.0m, rows[0].UnitPrice);
        Assert.True(rows[1].Discontinued);
        Assert.Null(rows[2].UnitPrice);
    }

    [Fact]
    public void CreateRowMapper_ReaderShapeChangesUnderStaleDelegate_FallsBackToReadEntity()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT product_id, product_name, unit_price, discontinued FROM gen_products WHERE product_id = 1;
            SELECT 42 AS noise, discontinued, unit_price, product_name, product_id FROM gen_products WHERE product_id = 2;
            """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        // Mapper is created once against the first result set's 4-column shape, simulating a
        // caller that (incorrectly) keeps reusing the same delegate across NextResult() instead
        // of calling CreateRowMapper again per result set.
        Func<IDataReader, GenProduct> staleMapper = GenProduct.CreateRowMapper(reader);
        GenProduct first = staleMapper(reader);
        Assert.Equal(1, first.ProductId);
        Assert.Equal("Chai", first.ProductName);

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        // The second result set has 5 columns (noise + reordered) vs. the first's 4, so the
        // mapper's FieldCount guard must trip and fall back to ReadEntity's full ordinal
        // resolution instead of reusing the first result set's cached column positions - which
        // would otherwise silently misread "noise" or the wrong column into each property.
        GenProduct second = staleMapper(reader);
        Assert.Equal(2, second.ProductId);
        Assert.Equal("Chang", second.ProductName);
        Assert.Equal(19.0m, second.UnitPrice);
        Assert.True(second.Discontinued);
    }

    [Fact]
    public void CreateRowMapper_PlainIDataReader_NotDbDataReader_UsesFallbackPath()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT product_id, product_name, unit_price, discontinued FROM gen_products ORDER BY product_id";

        using SqliteDataReader sqliteReader = cmd.ExecuteReader();
        // Wraps a real reader in a plain IDataReader (deliberately not a DbDataReader) so
        // CreateRowMapper's "reader is DbDataReader" check is false and it must take the
        // reflection-free plain-IDataReader fallback closure (JauntyGenerator.cs lines
        // 281-306, using reader.IsDBNull/GetXxx directly) instead of the DbDataReader fast
        // path (GetFieldValue<T>) exercised by every other CreateRowMapper test in this file.
        IDataReader reader = new PlainDataReader(sqliteReader);
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);

        var rows = new List<GenProduct>();
        while (reader.Read())
            rows.Add(mapper(reader));

        Assert.Equal(3, rows.Count);
        Assert.Equal("Chai", rows[0].ProductName);
        Assert.Equal(18.0m, rows[0].UnitPrice);
        Assert.True(rows[1].Discontinued);
        Assert.Null(rows[2].UnitPrice);
    }

    [Fact]
    public void ReadEntity_InterleavedReaders_DoNotCrossContaminate()
    {
        using SqliteCommand cmdA = _connection.CreateCommand();
        cmdA.CommandText = "SELECT product_id, product_name, unit_price, discontinued FROM gen_products WHERE product_id = 1";
        using SqliteCommand cmdB = _connection.CreateCommand();
        cmdB.CommandText = "SELECT discontinued, unit_price, product_name, product_id FROM gen_products WHERE product_id = 2";

        using SqliteDataReader readerA = cmdA.ExecuteReader();
        Assert.True(readerA.Read());
        GenProduct a = GenProduct.ReadEntity(readerA);
        readerA.Close();

        using SqliteDataReader readerB = cmdB.ExecuteReader();
        Assert.True(readerB.Read());
        GenProduct b = GenProduct.ReadEntity(readerB);

        Assert.Equal(1, a.ProductId);
        Assert.Equal("Chai", a.ProductName);
        Assert.Equal(2, b.ProductId);
        Assert.Equal("Chang", b.ProductName);
    }

    /// <summary>
    /// Forwards every IDataReader/IDataRecord member to an inner reader without deriving from
    /// DbDataReader, so generated code's "reader is DbDataReader" checks evaluate false through
    /// this wrapper - used to exercise CreateRowMapper's plain-IDataReader fallback path.
    /// </summary>
    private sealed class PlainDataReader : IDataReader
    {
        private readonly IDataReader _inner;

        public PlainDataReader(IDataReader inner) => _inner = inner;

        public int Depth => _inner.Depth;
        public bool IsClosed => _inner.IsClosed;
        public int RecordsAffected => _inner.RecordsAffected;
        public int FieldCount => _inner.FieldCount;

        public void Close() => _inner.Close();
        public DataTable? GetSchemaTable() => _inner.GetSchemaTable();
        public bool NextResult() => _inner.NextResult();
        public bool Read() => _inner.Read();
        public void Dispose() => _inner.Dispose();

        public string GetName(int i) => _inner.GetName(i);
        public string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
        public Type GetFieldType(int i) => _inner.GetFieldType(i);
        public object GetValue(int i) => _inner.GetValue(i);
        public int GetValues(object[] values) => _inner.GetValues(values);
        public int GetOrdinal(string name) => _inner.GetOrdinal(name);
        public bool GetBoolean(int i) => _inner.GetBoolean(i);
        public byte GetByte(int i) => _inner.GetByte(i);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => _inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
        public char GetChar(int i) => _inner.GetChar(i);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => _inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
        public Guid GetGuid(int i) => _inner.GetGuid(i);
        public short GetInt16(int i) => _inner.GetInt16(i);
        public int GetInt32(int i) => _inner.GetInt32(i);
        public long GetInt64(int i) => _inner.GetInt64(i);
        public float GetFloat(int i) => _inner.GetFloat(i);
        public double GetDouble(int i) => _inner.GetDouble(i);
        public string GetString(int i) => _inner.GetString(i);
        public decimal GetDecimal(int i) => _inner.GetDecimal(i);
        public DateTime GetDateTime(int i) => _inner.GetDateTime(i);
        public IDataReader GetData(int i) => _inner.GetData(i);
        public bool IsDBNull(int i) => _inner.IsDBNull(i);

        public object this[int i] => _inner[i];
        public object this[string name] => _inner[name];
    }
}
