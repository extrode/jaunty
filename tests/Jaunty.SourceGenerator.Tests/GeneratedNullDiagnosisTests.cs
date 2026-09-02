using System.Data;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// The named NULL error from AUD-R35-069 (pinned in <see cref="GeneratedNonNullableNullTests"/>) is
/// now produced by catching the provider's own failure and re-examining the row, rather than by an
/// <c>IsDBNull</c> call ahead of every non-nullable column. That pre-check was one native call per
/// column per row on Microsoft.Data.Sqlite: 1.8 ms of a 6.2 ms 10k-row read (2026-09-02). These pin
/// the three things the rewrite has to hold: no <c>IsDBNull</c> on a non-nullable column of a clean
/// row, a failure that is not a NULL propagates untouched, and the named error keeps the provider's
/// exception as its cause.
/// </summary>
public sealed class GeneratedNullDiagnosisTests
{
    private static readonly string[] Columns = ["product_id", "product_name", "unit_price", "discontinued"];

    [Fact]
    public void ACleanRow_ChecksIsDBNull_OnlyForNullableAndReferenceProperties()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, false]);
        Assert.True(reader.Read());

        GenProduct.ReadEntity(reader);

        Assert.Equal(["product_name", "unit_price"], reader.IsDBNullColumns);
    }

    [Fact]
    public void ACleanRow_ThroughTheRowMapper_ChecksIsDBNull_OnlyForNullableAndReferenceProperties()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, false]);
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());
        reader.IsDBNullColumns.Clear();

        mapper(reader);

        Assert.Equal(["product_name", "unit_price"], reader.IsDBNullColumns);
    }

    [Fact]
    public void ARow_ThroughTheRowMapper_DoesNotReadFieldCount()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, false]);
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());
        int resolved = reader.FieldCountCalls;

        mapper(reader);

        Assert.Equal(resolved, reader.FieldCountCalls);
    }

    [Fact]
    public void AGetterFailure_ThatIsNotANull_PropagatesUnchanged()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, false], failOn: "discontinued");
        Assert.True(reader.Read());

        var ex = Assert.Throws<FormatException>(() => GenProduct.ReadEntity(reader));

        Assert.Equal("provider failure", ex.Message);
    }

    [Fact]
    public void AGetterFailure_ThatIsNotANull_PropagatesUnchanged_ThroughTheRowMapper()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, false], failOn: "discontinued");
        Func<IDataReader, GenProduct> mapper = GenProduct.CreateRowMapper(reader);
        Assert.True(reader.Read());

        var ex = Assert.Throws<FormatException>(() => mapper(reader));

        Assert.Equal("provider failure", ex.Message);
    }

    [Fact]
    public void TheNamedError_CarriesTheProviderException_AsItsCause()
    {
        var reader = new CountingReader(Columns, [1, "Chai", 10.25m, DBNull.Value]);
        Assert.True(reader.Read());

        var ex = Assert.Throws<InvalidOperationException>(() => GenProduct.ReadEntity(reader));

        Assert.Equal("Cannot assign NULL to non-nullable property 'Discontinued'.", ex.Message);
        Assert.IsType<InvalidCastException>(ex.InnerException);
    }

    [Fact]
    public void TheNamedError_NamesTheFirstNullNonNullableColumn_WhenTheFailureCameFromALaterOne()
    {
        var reader = new CountingReader(Columns, [DBNull.Value, "Chai", 10.25m, DBNull.Value]);
        Assert.True(reader.Read());

        var ex = Assert.Throws<InvalidOperationException>(() => GenProduct.ReadEntity(reader));

        Assert.Equal("Cannot assign NULL to non-nullable property 'ProductId'.", ex.Message);
    }

    private sealed class CountingReader(string[] names, object[] values, string? failOn = null) : IDataReader
    {
        private int _row;

        public List<string> IsDBNullColumns { get; } = [];

        public int FieldCountCalls { get; private set; }

        public int FieldCount
        {
            get
            {
                FieldCountCalls++;
                return names.Length;
            }
        }
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public Type GetFieldType(int i) => values[i] is DBNull ? typeof(object) : values[i].GetType();

        public bool IsDBNull(int i)
        {
            IsDBNullColumns.Add(names[i]);
            return values[i] is DBNull;
        }

        public int GetInt32(int i) => Convert.ToInt32(Value(i));
        public string GetString(int i) => (string)Value(i);
        public decimal GetDecimal(int i) => (decimal)Value(i);
        public bool GetBoolean(int i) => Convert.ToBoolean(Value(i));

        private object Value(int i) => names[i] == failOn ? throw new FormatException("provider failure") : values[i];

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
        public double GetDouble(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public long GetInt64(int i) => throw new NotSupportedException();
        public int GetValues(object[] values) => throw new NotSupportedException();
    }
}
