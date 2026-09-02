using System.Data;
using System.Data.Common;

using Jaunty.SourceGenerator.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// A decimal property whose column reports <see cref="double"/> from <c>GetFieldType</c> is read
/// through <c>GetDouble</c> and cast; any other column type keeps <c>GetDecimal</c>. Microsoft.Data.Sqlite
/// implements <c>GetDecimal</c> on a REAL column as text formatting plus <c>decimal.Parse</c>, which
/// measured at 4.8 ms per 10k rows against 1.8 ms for <c>GetDouble</c> (2026-09-02). The decision is
/// made once per result set, when the ordinal map is resolved.
/// </summary>
public sealed class GeneratedDecimalReadTests
{
    private static readonly string[] Columns = ["product_id", "product_name", "unit_price", "discontinued"];

    [Fact]
    public void ADoubleColumn_IsReadThroughGetDouble_OnReadEntity()
    {
        var reader = new TypedReader(Columns, [1, "Chai", 10.25, false], unitPriceType: typeof(double));
        Assert.True(reader.Read());

        GenProduct entity = GenProduct.ReadEntity(reader);

        Assert.Equal(10.25m, entity.UnitPrice);
        Assert.Equal(1, reader.GetDoubleCalls);
        Assert.Equal(0, reader.GetDecimalCalls);
    }

    [Fact]
    public void ADecimalColumn_KeepsGetDecimal_OnReadEntity()
    {
        var reader = new TypedReader(Columns, [1, "Chai", 10.25m, false], unitPriceType: typeof(decimal));
        Assert.True(reader.Read());

        GenProduct entity = GenProduct.ReadEntity(reader);

        Assert.Equal(10.25m, entity.UnitPrice);
        Assert.Equal(0, reader.GetDoubleCalls);
        Assert.Equal(1, reader.GetDecimalCalls);
    }

    [Fact]
    public void ADoubleColumn_IsReadThroughGetDouble_OnTheRowMapper()
    {
        var reader = new TypedReader(Columns, [1, "Chai", 10.25, false], unitPriceType: typeof(double));
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());

        GenProduct entity = mapper(reader);

        Assert.Equal(10.25m, entity.UnitPrice);
        Assert.Equal(1, reader.GetDoubleCalls);
        Assert.Equal(0, reader.GetDecimalCalls);
    }

    [Fact]
    public void ADecimalColumn_KeepsGetDecimal_OnTheRowMapper()
    {
        var reader = new TypedReader(Columns, [1, "Chai", 10.25m, false], unitPriceType: typeof(decimal));
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());

        GenProduct entity = mapper(reader);

        Assert.Equal(10.25m, entity.UnitPrice);
        Assert.Equal(0, reader.GetDoubleCalls);
        Assert.Equal(1, reader.GetDecimalCalls);
    }

    [Fact]
    public void ANullDoubleColumn_StillSkips()
    {
        var reader = new TypedReader(Columns, [1, "Chai", DBNull.Value, false], unitPriceType: typeof(double));
        Assert.True(reader.Read());

        GenProduct entity = GenProduct.ReadEntity(reader);

        Assert.Null(entity.UnitPrice);
        Assert.Equal(0, reader.GetDoubleCalls);
    }

    [Theory]
    [InlineData(10.25, "10.25")]
    [InlineData(0.1, "0.1")]
    [InlineData(1234567890.123456, "1234567890.12346")]
    [InlineData(2.675, "2.675")]
    public void ASqliteRealColumn_MapsTheSameValueGetDecimalWouldHave(double stored, string expected)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var ddl = connection.CreateCommand())
        {
            ddl.CommandText = "CREATE TABLE gen_products (product_id INTEGER PRIMARY KEY, product_name TEXT NOT NULL, unit_price REAL, discontinued INTEGER NOT NULL)";
            ddl.ExecuteNonQuery();
            ddl.CommandText = "INSERT INTO gen_products VALUES (1, 'Chai', @p, 0)";
            ddl.Parameters.AddWithValue("@p", stored);
            ddl.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT product_id, product_name, unit_price, discontinued FROM gen_products";
        using DbDataReader reader = select.ExecuteReader();

        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());
        GenProduct viaRowMapper = mapper(reader);
        GenProduct viaReadEntity = GenProduct.ReadEntity(reader);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), viaRowMapper.UnitPrice);
        Assert.Equal(reader.GetDecimal(2), viaRowMapper.UnitPrice);
        Assert.Equal(viaRowMapper.UnitPrice, viaReadEntity.UnitPrice);
    }

    private sealed class TypedReader(string[] names, object[] values, Type unitPriceType) : IDataReader
    {
        private int _row;

        public int GetDoubleCalls { get; private set; }
        public int GetDecimalCalls { get; private set; }

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public bool IsDBNull(int i) => values[i] is DBNull;
        public Type GetFieldType(int i) => i == 2 ? unitPriceType : values[i].GetType();
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i];
        public bool GetBoolean(int i) => Convert.ToBoolean(values[i]);

        public decimal GetDecimal(int i)
        {
            GetDecimalCalls++;
            return (decimal)values[i];
        }

        public double GetDouble(int i)
        {
            GetDoubleCalls++;
            return (double)values[i];
        }

        public bool Read() => _row++ < 1;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => values[i];
        public object this[string name] => values[GetOrdinal(name)];

        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public long GetInt64(int i) => throw new NotSupportedException();
        public int GetValues(object[] values) => throw new NotSupportedException();
    }
}
