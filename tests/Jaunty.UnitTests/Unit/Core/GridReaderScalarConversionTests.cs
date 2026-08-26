using System.Data;
using System.Data.Common;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// AUD-R26 (batch 4, low/bug). <c>GridReader.ReadScalar&lt;T&gt;</c> and
/// <c>ReadScalarAsync&lt;T&gt;</c> hand-rolled their conversion - <c>GetFieldValue&lt;T&gt;</c>
/// with a <c>Convert.ChangeType</c> fallback - instead of going through
/// <c>ScalarConverter&lt;T&gt;</c>, which is what <c>QueryScalar</c> uses. The library therefore had
/// two scalar conversion behaviours, and the grid one was the weaker of the pair.
///
/// <para>
/// <c>Convert.ChangeType</c> handles neither a <see cref="Nullable{T}"/> target nor the types that
/// do not implement <see cref="IConvertible"/> - <see cref="Guid"/>, <see cref="DateTimeOffset"/>,
/// <see cref="TimeSpan"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/> - nor an enum arriving as
/// its name. <c>ScalarConverter</c> handles all of them, and every one of those cases is ordinary on
/// SQLite, which stores dates, times and GUIDs as text.
/// </para>
///
/// <para>
/// The fallback is not a rare branch. Its own comment says SQLite throws from
/// <c>GetFieldValue&lt;T&gt;</c> "even though a value is present", which is exactly when the
/// hand-rolled conversion took over - so on the provider most likely to need the richer conversion,
/// the grid path was guaranteed to reach the poorer one.
/// </para>
/// </summary>
public class GridReaderScalarConversionTests
{
    private enum Priority
    {
        Low = 0,
        High = 7,
    }

    private static GridReader Grid(IDataReader reader) =>
        new(reader, new StubConnection(), closeConnection: false);

    // ------------------------------------------------------------------
    // Nullable targets
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>Convert.ChangeType(value, typeof(int?))</c> throws <see cref="InvalidCastException"/> -
    /// it has no notion of a nullable target. Any caller reading a nullable scalar off a grid
    /// through a provider that takes the fallback branch got that exception instead of a value.
    /// </summary>
    [Fact]
    public void ReadScalar_NullableTarget_ReturnsTheValue()
    {
        using GridReader grid = Grid(new ValueReader(5));

        Assert.Equal(5, grid.ReadScalar<int?>());
    }

    [Fact]
    public async Task ReadScalarAsync_NullableTarget_ReturnsTheValue()
    {
        using GridReader grid = Grid(new ThrowingDbReader(5));

        Assert.Equal(5, await grid.ReadScalarAsync<int?>());
    }

    /// <summary>
    /// A NULL still reads back as null rather than throwing or as a zero, which is the case that
    /// made a nullable target worth asking for in the first place.
    /// </summary>
    [Fact]
    public void ReadScalar_NullableTarget_NullStaysNull()
    {
        using GridReader grid = Grid(new ValueReader(DBNull.Value));

        Assert.Null(grid.ReadScalar<int?>());
    }

    // ------------------------------------------------------------------
    // The types Convert.ChangeType cannot produce
    // ------------------------------------------------------------------

    /// <summary>
    /// None of these implement <see cref="IConvertible"/>, so <c>Convert.ChangeType</c> throws
    /// <see cref="InvalidCastException"/> for all of them. SQLite returns each as text.
    /// </summary>
    [Fact]
    public void ReadScalar_GuidFromString_Converts()
    {
        var expected = Guid.Parse("8a1a0d4a-2f1b-4f2e-9c3d-5b6a7c8d9e0f");
        using GridReader grid = Grid(new ValueReader(expected.ToString()));

        Assert.Equal(expected, grid.ReadScalar<Guid>());
    }

    [Fact]
    public void ReadScalar_DateTimeOffsetFromString_Converts()
    {
        var expected = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        using GridReader grid = Grid(new ValueReader("2026-03-04T05:06:07+00:00"));

        Assert.Equal(expected, grid.ReadScalar<DateTimeOffset>());
    }

    [Fact]
    public void ReadScalar_TimeSpanFromString_Converts()
    {
        using GridReader grid = Grid(new ValueReader("02:03:04"));

        Assert.Equal(new TimeSpan(2, 3, 4), grid.ReadScalar<TimeSpan>());
    }

    // ScalarConverter's DateOnly/TimeOnly branches are themselves behind NET8_0_OR_GREATER, since
    // neither type exists on net472 - which this project also targets.
#if NET8_0_OR_GREATER
    [Fact]
    public void ReadScalar_DateOnlyFromString_Converts()
    {
        using GridReader grid = Grid(new ValueReader("2026-03-04"));

        Assert.Equal(new DateOnly(2026, 3, 4), grid.ReadScalar<DateOnly>());
    }
#endif

    // ------------------------------------------------------------------
    // Enums
    // ------------------------------------------------------------------

    /// <summary>
    /// An enum stored as its name, which is what <c>EnumStorage.String</c> writes. Convert.ChangeType
    /// cannot parse it; <c>ScalarConverter</c> does, case-insensitively.
    /// </summary>
    [Fact]
    public void ReadScalar_EnumFromString_Converts()
    {
        using GridReader grid = Grid(new ValueReader("high"));

        Assert.Equal(Priority.High, grid.ReadScalar<Priority>());
    }

    [Fact]
    public void ReadScalar_EnumFromNumber_Converts()
    {
        using GridReader grid = Grid(new ValueReader(7));

        Assert.Equal(Priority.High, grid.ReadScalar<Priority>());
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    /// <summary>
    /// The plain path still works and still takes the provider's own typed accessor first, so this
    /// change is about the fallback rather than about replacing the fast path.
    /// </summary>
    [Fact]
    public void ReadScalar_ExactType_StillReturnsTheValue()
    {
        using GridReader grid = Grid(new ValueReader(42));

        Assert.Equal(42, grid.ReadScalar<int>());
    }

    [Fact]
    public void ReadScalar_NoRows_ReturnsDefault()
    {
        using GridReader grid = Grid(new ValueReader(1, hasRow: false));

        Assert.Equal(0, grid.ReadScalar<int>());
    }

    /// <summary>
    /// A value that genuinely cannot become the target type must still throw rather than come back
    /// as <c>default</c>. The distinction between "no value" and "wrong type" is documented on
    /// <c>ReadScalar</c> and is the reason its fallback rethrows.
    /// </summary>
    [Fact]
    public void ReadScalar_UnconvertibleValue_StillThrows()
    {
        using GridReader grid = Grid(new ValueReader("not-a-number"));

        Assert.ThrowsAny<Exception>(() => grid.ReadScalar<int>());
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// A plain <see cref="IDataReader"/>, so <c>ReadScalar</c> takes the non-<c>DbDataReader</c>
    /// branch - the same branch a provider forces by throwing from <c>GetFieldValue</c>.
    /// </summary>
    private sealed class ValueReader(object value, bool hasRow = true) : IDataReader
    {
        private int _row;

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public int FieldCount => 1;

        public bool Read() => hasRow && _row++ == 0;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => value;
        public object this[string name] => value;

        public object GetValue(int i) => value;
        public bool IsDBNull(int i) => value is DBNull;
        public Type GetFieldType(int i) => value.GetType();
        public string GetName(int i) => "Value";
        public int GetOrdinal(string name) => 0;
        public string GetDataTypeName(int i) => GetFieldType(i).Name;

        public bool GetBoolean(int i) => Convert.ToBoolean(value);
        public byte GetByte(int i) => Convert.ToByte(value);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public DateTime GetDateTime(int i) => Convert.ToDateTime(value);
        public decimal GetDecimal(int i) => Convert.ToDecimal(value);
        public double GetDouble(int i) => Convert.ToDouble(value);
        public float GetFloat(int i) => Convert.ToSingle(value);
        public Guid GetGuid(int i) => Guid.Parse((string)value);
        public short GetInt16(int i) => Convert.ToInt16(value);
        public int GetInt32(int i) => Convert.ToInt32(value);
        public long GetInt64(int i) => Convert.ToInt64(value);
        public string GetString(int i) => (string)value;
        public int GetValues(object[] values) => 0;
    }

    /// <summary>
    /// A <see cref="DbDataReader"/> whose typed accessors throw, which is the SQLite behaviour the
    /// fallback exists for. The async terminal requires a <c>DbDataReader</c>, so the fallback can
    /// only be reached this way.
    /// </summary>
    private sealed class ThrowingDbReader(object value) : DbDataReader
    {
        private int _row;

        public override int Depth => 0;
        public override int FieldCount => 1;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => _row++ == 0;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;

        public override T GetFieldValue<T>(int ordinal) =>
            throw new InvalidCastException("Provider cannot produce this type directly.");

        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) =>
            throw new InvalidCastException("Provider cannot produce this type directly.");

        public override object GetValue(int ordinal) => value;
        public override bool IsDBNull(int ordinal) => value is DBNull;
        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) =>
            Task.FromResult(IsDBNull(ordinal));

        public override object this[int ordinal] => value;
        public override object this[string name] => value;
        public override Type GetFieldType(int ordinal) => value.GetType();
        public override string GetName(int ordinal) => "Value";
        public override int GetOrdinal(string name) => 0;
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(value);
        public override byte GetByte(int ordinal) => Convert.ToByte(value);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(value);
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(value);
        public override double GetDouble(int ordinal) => Convert.ToDouble(value);
        public override float GetFloat(int ordinal) => Convert.ToSingle(value);
        public override Guid GetGuid(int ordinal) => Guid.Parse((string)value);
        public override short GetInt16(int ordinal) => Convert.ToInt16(value);
        public override int GetInt32(int ordinal) => Convert.ToInt32(value);
        public override long GetInt64(int ordinal) => Convert.ToInt64(value);
        public override string GetString(int ordinal) => (string)value;
        public override int GetValues(object[] values) => 0;
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
