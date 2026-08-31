using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R35-023, coverage. The four fail-fast branches of <c>ResolveSpecialTypeMapper</c> had no
/// test anywhere: a non-string Dictionary key, a KeyValuePair over fewer than two columns, an
/// 8-or-more-element ValueTuple, and a ValueTuple wider than the result set. Each exists to turn
/// a silent misbinding into an error, so an unpinned one is a guard that can be removed without
/// anything noticing.
/// </summary>
[Collection("Type Handler Operations")]
public class SpecialTypeMapperValidationTests
{
    private static Func<Type, IDataReader, object> Resolver()
    {
        SpecialTypeMappers.Register();
        return JauntyConfig.SpecialTypeMapperResolver!;
    }

    [Fact]
    public void NonStringDictionaryKey_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            Resolver()(typeof(Dictionary<int, object>), new StubReader(["a"])));

        Assert.Contains("key type must be string", ex.Message);
    }

    [Fact]
    public void StringDictionaryKey_IsAccepted() =>
        Assert.NotNull(Resolver()(typeof(Dictionary<string, object>), new StubReader(["a"])));

    [Fact]
    public void KeyValuePairOverOneColumn_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Resolver()(typeof(KeyValuePair<string, int>), new StubReader(["k"])));

        Assert.Contains("requires at least 2 columns", ex.Message);
        Assert.Contains("returned 1", ex.Message);
    }

    [Fact]
    public void KeyValuePairOverTwoColumns_IsAccepted() =>
        Assert.NotNull(Resolver()(typeof(KeyValuePair<string, int>), new StubReader(["k", "v"])));

    [Fact]
    public void EightElementValueTuple_Throws()
    {
        // The 8th type argument is always the nested "Rest" tuple, which the positional mapper
        // would treat as one plain column.
        Type wide = typeof(ValueTuple<int, int, int, int, int, int, int, ValueTuple<int>>);

        var ex = Assert.Throws<NotSupportedException>(() =>
            Resolver()(wide, new StubReader(["a", "b", "c", "d", "e", "f", "g", "h"])));

        Assert.Contains("8 or more elements", ex.Message);
    }

    [Fact]
    public void SevenElementValueTuple_IsAccepted() =>
        Assert.NotNull(Resolver()(
            typeof(ValueTuple<int, int, int, int, int, int, int>),
            new StubReader(["a", "b", "c", "d", "e", "f", "g"])));

    [Fact]
    public void ValueTupleWiderThanTheResultSet_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Resolver()(typeof(ValueTuple<int, int, int>), new StubReader(["a", "b"])));

        Assert.Contains("requires 3 columns", ex.Message);
        Assert.Contains("returned 2", ex.Message);
    }

    [Fact]
    public void ValueTupleNarrowerThanTheResultSet_IsAccepted() =>
        Assert.NotNull(Resolver()(typeof(ValueTuple<int, int>), new StubReader(["a", "b", "c"])));

    // AUD-R35-226: the two untyped-row mappers disagree on key case sensitivity, and the
    // divergence is ExpandoObject's rather than either mapper's - its IDictionary is ordinal
    // case-sensitive and takes no comparer.

    [Fact]
    public void ADictionaryRow_ResolvesAKeyRegardlessOfCase()
    {
        var reader = new StubReader(["CustomerID"]);
        var mapper = (Func<IDataReader, object>)Resolver()(typeof(Dictionary<string, object>), reader);

        var row = (Dictionary<string, object?>)mapper(reader);

        Assert.True(row.ContainsKey("CustomerID"));
        Assert.True(row.ContainsKey("customerid"));
    }

    [Fact]
    public void AnExpandoRow_ResolvesAKeyOnlyByItsExactCase()
    {
        var reader = new StubReader(["CustomerID"]);
        var mapper = (Func<IDataReader, object>)Resolver()(typeof(object), reader);

        var row = (IDictionary<string, object?>)mapper(reader);

        Assert.True(row.ContainsKey("CustomerID"));
        Assert.False(row.ContainsKey("customerid"));
    }

    [Fact]
    public void AnUnrelatedType_ResolvesToNothing() =>
        Assert.Null(Resolver()(typeof(SpecialTypeMapperValidationTests), new StubReader(["a"])));

    private sealed class StubReader(string[] names) : IDataReader
    {
        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.IndexOf(names, name);
        public object GetValue(int i) => 0;
        public bool IsDBNull(int i) => false;
        public Type GetFieldType(int i) => typeof(int);
        public int GetInt32(int i) => 0;
        public string GetString(int i) => string.Empty;

        public bool Read() => false;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public void Close() { }
        public void Dispose() { }
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => 0;
        public object this[string name] => 0;

        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "int";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public long GetInt64(int i) => 0;
        public int GetValues(object[] values) => 0;
    }
}
