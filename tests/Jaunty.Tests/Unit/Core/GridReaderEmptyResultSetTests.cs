using System.Data;

using Jaunty.Core;
using Jaunty.Interfaces;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// AUD-R25 (B4-4): <c>ReadCore</c> and <c>ReadStreamIterator</c> called
/// <c>DrDispatcher.Resolve</c> <em>before</em> the first <c>reader.Read()</c>, while every one of
/// their siblings resolved lazily after a row was confirmed - the four First/Single terminals do
/// <c>if (!reader.Read()) return default;</c> first, and both async counterparts use
/// <c>map ??= DrDispatcher.Resolve(...)</c> inside the read loop.
///
/// <para>
/// Resolution is not side-effect-free. It throws <see cref="InvalidOperationException"/> when no
/// mapper is registered, and for a source-generated entity in <c>MappingMode.Strict</c> it runs
/// <c>MapperFactory</c>, which validates the result-set shape. So for an <em>empty</em> result set,
/// <c>grid.Read&lt;T&gt;()</c> could throw while <c>await grid.ReadAsync&lt;T&gt;()</c> on the
/// identical grid returned an empty list and <c>grid.ReadFirstOrDefault&lt;T&gt;()</c> returned
/// null - three public APIs disagreeing on whether an empty result set is an error, with nothing in
/// their documentation distinguishing them.
/// </para>
///
/// <para>
/// <c>CountingEntity</c> below implements <see cref="IMapped{T}"/> with a
/// <c>CreateRowMapper</c> factory that counts its own invocations, which is the path
/// <c>DrDispatcher.Resolve</c> prefers in Strict mode. That makes "did resolution happen, and when?"
/// directly observable without touching any global configuration - and it is a sharper test of the
/// finding than a thrown exception, since the finding is about <em>when</em> resolution runs.
/// </para>
/// </summary>
public class GridReaderEmptyResultSetTests
{
    private static GridReader EmptyGrid() =>
        new(new EmptyReader(), new StubConnection(), closeConnection: false);

    // ------------------------------------------------------------------
    // Empty result set: every terminal agrees it is not an error
    // ------------------------------------------------------------------

    [Fact]
    public void Read_EmptyResultSet_ReturnsEmptyInsteadOfThrowing()
    {
        using GridReader grid = EmptyGrid();

        Assert.Empty(grid.Read<CountingEntity>());
    }

    [Fact]
    public void ReadStream_EmptyResultSet_ReturnsEmptyInsteadOfThrowing()
    {
        using GridReader grid = EmptyGrid();

        Assert.Empty(grid.ReadStream<CountingEntity>().ToList());
    }

    [Fact]
    public async Task ReadAsync_EmptyResultSet_ReturnsEmpty()
    {
        // The behaviour the sync path is being aligned to - it already worked.
        await using var grid = new GridReader(new EmptyDbReader(), new StubConnection(), closeConnection: false);

        Assert.Empty(await grid.ReadAsync<CountingEntity>());
    }

    [Fact]
    public void ReadFirstOrDefault_EmptyResultSet_ReturnsNull()
    {
        using GridReader grid = EmptyGrid();

        Assert.Null(grid.ReadFirstOrDefault<CountingEntity>());
    }

    [Fact]
    public void Read_And_ReadFirstOrDefault_AgreeOnAnEmptyResultSet()
    {
        // The heart of the finding: two public APIs over the same grid shape must not disagree on
        // whether "no rows" is an error.
        using GridReader gridA = EmptyGrid();
        using GridReader gridB = EmptyGrid();

        Exception? fromRead = Record.Exception(() => gridA.Read<CountingEntity>());
        Exception? fromFirst = Record.Exception(() => gridB.ReadFirstOrDefault<CountingEntity>());

        Assert.Null(fromRead);
        Assert.Null(fromFirst);
    }

    // ------------------------------------------------------------------
    // Resolution happens exactly when a row exists - not before, and not never
    // ------------------------------------------------------------------

    [Fact]
    public void Read_EmptyResultSet_DoesNotResolveAMapperAtAll()
    {
        // The defect, stated directly: resolution ran before the first Read(), so an empty result
        // set still paid for - and could still fail in - shape validation.
        int before = CountingEntity.ResolveCount;
        using var grid = new GridReader(new EmptyReader(), new StubConnection(), closeConnection: false);

        grid.Read<CountingEntity>();

        Assert.Equal(before, CountingEntity.ResolveCount);
    }

    [Fact]
    public void ReadStream_EmptyResultSet_DoesNotResolveAMapperAtAll()
    {
        int before = CountingEntity.ResolveCount;
        using var grid = new GridReader(new EmptyReader(), new StubConnection(), closeConnection: false);

        grid.ReadStream<CountingEntity>().ToList();

        Assert.Equal(before, CountingEntity.ResolveCount);
    }

    [Fact]
    public void Read_WithRows_StillResolvesExactlyOncePerResultSet()
    {
        // Deferring must not turn a once-per-result-set resolution into a per-row one - that is the
        // whole reason DrDispatcher prefers the factory ("shape validated once here instead of per row").
        int before = CountingEntity.ResolveCount;
        using var grid = new GridReader(new ThreeRowReader(), new StubConnection(), closeConnection: false);

        List<CountingEntity> rows = grid.Read<CountingEntity>();

        Assert.Equal(3, rows.Count);
        Assert.Equal(before + 1, CountingEntity.ResolveCount);
    }

    [Fact]
    public void ReadStream_WithRows_StillResolvesExactlyOncePerResultSet()
    {
        int before = CountingEntity.ResolveCount;
        using var grid = new GridReader(new ThreeRowReader(), new StubConnection(), closeConnection: false);

        List<CountingEntity> rows = grid.ReadStream<CountingEntity>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(before + 1, CountingEntity.ResolveCount);
    }

    /// <summary>
    /// Source-generator-shaped: DrDispatcher prefers the CreateRowMapper factory in Strict mode, so
    /// the counter increments exactly when resolution happens.
    /// </summary>
    public class CountingEntity : IMapped<CountingEntity>
    {
        private static int _resolveCount;

        public static int ResolveCount => Volatile.Read(ref _resolveCount);

        public int Id { get; set; }

        public static Func<IDataReader, CountingEntity> CreateRowMapper(IDataReader reader)
        {
            Interlocked.Increment(ref _resolveCount);
            int ordinal = reader.GetOrdinal("Id");
            return r => new CountingEntity { Id = r.GetInt32(ordinal) };
        }

        // IMapped<T>.ReadEntity is a static abstract member on net8.0+ and an instance method on
        // net472, which does not support static abstract interface members - see IMapped<T> and the
        // same split in Jaunty.Tests.Entities.Product.
#if NET8_0_OR_GREATER
        public static CountingEntity ReadEntity(IDataReader reader)
#else
        public CountingEntity ReadEntity(IDataReader reader)
#endif
            => new() { Id = reader.GetInt32(reader.GetOrdinal("Id")) };
    }

    // ------------------------------------------------------------------

    private class EmptyReader : IDataReader
    {
        protected int Rows;

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public virtual int FieldCount => 1;

        public virtual bool Read() => false;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => 1;
        public object this[string name] => 1;

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
        public Type GetFieldType(int i) => typeof(int);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 1;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => "Id";
        public int GetOrdinal(string name) => 0;
        public string GetString(int i) => "";
        public object GetValue(int i) => 1;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => false;
    }

    private sealed class ThreeRowReader : EmptyReader
    {
        public override bool Read() => Rows++ < 3;
    }

    /// <summary>
    /// The async terminals require a <see cref="System.Data.Common.DbDataReader"/>, so the empty
    /// reader is mirrored on that base rather than reusing <see cref="EmptyReader"/>.
    /// </summary>
    private sealed class EmptyDbReader : System.Data.Common.DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 1;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => false;
        public override bool NextResult() => false;
        public override object this[int ordinal] => 1;
        public override object this[string name] => 1;

        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "int";
        public override DateTime GetDateTime(int ordinal) => default;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => default;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 1;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => "Id";
        public override int GetOrdinal(string name) => 0;
        public override string GetString(int ordinal) => "";
        public override object GetValue(int ordinal) => 1;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => false;
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
