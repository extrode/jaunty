using System.Data;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-033 (round-33 carry-forward, coverage). The emitted <c>ReadFallback&lt;T&gt;</c> is what
/// AUD-R25 put in place of a raw unboxing cast, and it is reached by every property type
/// <c>GetReaderTypeInfo</c> does not name - including the <c>DateOnly</c>/<c>TimeOnly</c> pair every
/// scaffolder emits for <c>date</c>/<c>time</c> columns. Its branches were reached only by the
/// <c>value is T typed</c> early return, or through Microsoft.Data.Sqlite's <c>DbDataReader</c>,
/// which never calls the helper for anything but enums. These drive the plain <c>IDataReader</c>
/// path, where the provider hands back whatever it likes and every branch is live.
/// </summary>
public sealed class GeneratedReadFallbackTests
{
    private static readonly string[] Columns =
        ["id", "day", "moment", "duration", "occurred_at", "ref_id", "grade", "counter"];

    private static readonly object[] Defaults =
    [
        1,
        new DateOnly(2024, 1, 15),
        new TimeOnly(14, 30, 0),
        new TimeSpan(1, 2, 3),
        new DateTimeOffset(2024, 1, 15, 14, 30, 0, TimeSpan.Zero),
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        GenFallbackGrade.High,
        7u
    ];

    /// <summary>Reads one row with <paramref name="column"/> replaced by <paramref name="value"/>.</summary>
    private static GenFallbackEntity ReadWith(string column, object value)
    {
        var values = (object[])Defaults.Clone();
        values[Array.IndexOf(Columns, column)] = value;

        var reader = new PlainReader(Columns, values);
        Assert.True(reader.Read());
        return GenFallbackEntity.ReadEntity(reader);
    }

    [Fact]
    public void AValueAlreadyOfThePropertyType_IsReturnedUnchanged()
    {
        Assert.Equal(new DateOnly(2024, 1, 15), ReadWith("day", new DateOnly(2024, 1, 15)).Day);
    }

    [Theory]
    [InlineData("High", GenFallbackGrade.High)]
    [InlineData("low", GenFallbackGrade.Low)]
    public void AnEnum_IsParsedFromText_CaseInsensitively(string text, GenFallbackGrade expected)
    {
        Assert.Equal(expected, ReadWith("grade", text).Grade);
    }

    [Fact]
    public void AnEnum_IsConvertedFromANumber()
    {
        Assert.Equal(GenFallbackGrade.High, ReadWith("grade", 1L).Grade);
    }

    [Fact]
    public void ADateOnly_IsParsedFromText()
    {
        Assert.Equal(new DateOnly(2023, 6, 30), ReadWith("day", "2023-06-30").Day);
    }

    [Fact]
    public void ADateOnly_IsTakenFromADateTime()
    {
        Assert.Equal(new DateOnly(2023, 6, 30), ReadWith("day", new DateTime(2023, 6, 30, 9, 5, 42)).Day);
    }

    [Fact]
    public void ATimeOnly_IsParsedFromText()
    {
        Assert.Equal(new TimeOnly(9, 5, 42), ReadWith("moment", "09:05:42").Moment);
    }

    [Fact]
    public void ATimeOnly_IsTakenFromATimeSpan()
    {
        Assert.Equal(new TimeOnly(9, 5, 42), ReadWith("moment", new TimeSpan(9, 5, 42)).Moment);
    }

    [Fact]
    public void ATimeOnly_IsTakenFromADateTime()
    {
        Assert.Equal(new TimeOnly(9, 5, 42), ReadWith("moment", new DateTime(2023, 6, 30, 9, 5, 42)).Moment);
    }

    [Fact]
    public void ATimeSpan_IsParsedFromText()
    {
        Assert.Equal(new TimeSpan(4, 5, 6), ReadWith("duration", "04:05:06").Duration);
    }

    [Fact]
    public void ADateTimeOffset_IsParsedFromText()
    {
        Assert.Equal(
            new DateTimeOffset(2023, 6, 30, 9, 5, 42, TimeSpan.Zero),
            ReadWith("occurred_at", "2023-06-30T09:05:42+00:00").OccurredAt);
    }

    [Fact]
    public void ADateTimeOffset_IsTakenFromADateTime()
    {
        var local = new DateTime(2023, 6, 30, 9, 5, 42, DateTimeKind.Unspecified);

        Assert.Equal(new DateTimeOffset(local), ReadWith("occurred_at", local).OccurredAt);
    }

    [Fact]
    public void AGuid_IsParsedFromText()
    {
        Assert.Equal(
            Guid.Parse("99999999-8888-7777-6666-555555555555"),
            ReadWith("ref_id", "99999999-8888-7777-6666-555555555555").RefId);
    }

    /// <summary>
    /// AUD-R35-049. This used to be <c>AGuid_IsBuiltFromBytes</c>, asserting that the generated
    /// path returns <c>new Guid(bytes)</c> - which round-trips a locally produced
    /// <c>ToByteArray()</c> and so passed, while being wrong on every provider whose binary
    /// layout is not SQL Server's. Cluster B: a test pinning the defect. The core converter
    /// refuses a byte[] for a Guid on purpose and says why; the generated path now agrees.
    /// </summary>
    [Fact]
    public void AGuid_FromBytes_IsRefusedRatherThanGuessed()
    {
        var value = Guid.Parse("99999999-8888-7777-6666-555555555555");

        var ex = Assert.Throws<InvalidCastException>(() => ReadWith("ref_id", value.ToByteArray()));

        Assert.Contains("byte order", ex.Message, StringComparison.Ordinal);
        Assert.Contains("will not guess it", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A byte[] property is still read as a byte[] - the route the message points the caller at.
    /// </summary>
    [Fact]
    public void AGuidAlreadyOfTheRightType_IsStillReturned()
    {
        var value = Guid.Parse("99999999-8888-7777-6666-555555555555");

        Assert.Equal(value, ReadWith("ref_id", value).RefId);
    }

    /// <summary>
    /// The tail. Nothing above matches a numeric widening, so it goes to
    /// <c>Convert.ChangeType</c> - the branch every unnamed IConvertible type depends on.
    /// </summary>
    [Fact]
    public void AnythingElseConvertible_GoesThroughChangeType()
    {
        Assert.Equal(42u, ReadWith("counter", 42L).Counter);
    }

    [Fact]
    public void AnythingElseConvertible_IsConvertedFromTextToo()
    {
        Assert.Equal(42u, ReadWith("counter", "42").Counter);
    }

    /// <summary>
    /// A plain <c>IDataReader</c> - not a <c>DbDataReader</c> - so the generated mapper takes the
    /// <c>GetValue</c> branch and every catch-all type reaches <c>ReadFallback&lt;T&gt;</c>.
    /// </summary>
    private sealed class PlainReader(string[] names, object[] values) : IDataReader
    {
        private int _row;

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public bool IsDBNull(int i) => values[i] is null or DBNull;
        public Type GetFieldType(int i) => values[i].GetType();
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i];

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

        public bool GetBoolean(int i) => Convert.ToBoolean(values[i]);
        public byte GetByte(int i) => Convert.ToByte(values[i]);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => Convert.ToChar(values[i]);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => this;
        public string GetDataTypeName(int i) => values[i].GetType().Name;
        public DateTime GetDateTime(int i) => Convert.ToDateTime(values[i]);
        public decimal GetDecimal(int i) => Convert.ToDecimal(values[i]);
        public double GetDouble(int i) => Convert.ToDouble(values[i]);
        public float GetFloat(int i) => Convert.ToSingle(values[i]);
        public Guid GetGuid(int i) => (Guid)values[i];
        public short GetInt16(int i) => Convert.ToInt16(values[i]);
        public long GetInt64(int i) => Convert.ToInt64(values[i]);
        public int GetValues(object[] valueArray) => 0;
    }
}
