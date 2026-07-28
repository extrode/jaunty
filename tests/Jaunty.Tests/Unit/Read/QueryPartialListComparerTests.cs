using System.Data;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R25 (B1-2): <c>QueryPartialList</c> and <c>QueryPartialListAsync</c> built each row as
/// <c>new Dictionary&lt;string, object?&gt;()</c> with the default ordinal, case-sensitive
/// comparer, so <c>row["productid"]</c> threw <see cref="KeyNotFoundException"/> when the result set
/// named the column <c>ProductId</c>.
///
/// <para>
/// The sibling untyped-row path - <c>SpecialTypeMappers.CreateDictionaryMapper</c>, which serves
/// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c> - deliberately uses
/// <see cref="StringComparer.OrdinalIgnoreCase"/> and says so in a comment. Two public APIs
/// returning the same untyped-row shape therefore disagreed on key lookup semantics, and the
/// difference was silent: a caller moving between them got no compile error, just runtime
/// <see cref="KeyNotFoundException"/>s that depended on how the database happened to case the column.
/// </para>
/// </summary>
public class QueryPartialListComparerTests
{
    [Theory]
    [InlineData("ProductId")]
    [InlineData("productid")]
    [InlineData("PRODUCTID")]
    [InlineData("pRoDuCtId")]
    public void QueryPartialList_RowKeys_AreCaseInsensitive(string lookup)
    {
        var connection = new FakeConnection([("ProductId", 42), ("ProductName", "Widget")]);

        List<IDictionary<string, object?>> rows = connection.QueryPartialList("SELECT 1");

        IDictionary<string, object?> row = Assert.Single(rows);
        Assert.Equal(42, row[lookup]);
    }

    [Fact]
    public async Task QueryPartialListAsync_RowKeys_AreCaseInsensitive()
    {
        var connection = new FakeDbConnection([("ProductId", 42)]);

        List<IDictionary<string, object?>> rows = await connection.QueryPartialListAsync("SELECT 1");

        Assert.Equal(42, Assert.Single(rows)["productid"]);
    }

    [Fact]
    public void QueryPartialList_RowKeys_StillReportTheOriginalCasingWhenEnumerated()
    {
        // Case-insensitive lookup must not rewrite the stored key - callers that enumerate the row
        // still need the column name the database returned.
        var connection = new FakeConnection([("ProductId", 42)]);

        IDictionary<string, object?> row = Assert.Single(connection.QueryPartialList("SELECT 1"));

        Assert.Equal("ProductId", Assert.Single(row.Keys));
    }

    [Fact]
    public void QueryPartialList_NullColumnValue_IsStoredAsNullNotDBNull()
    {
        // Unchanged behaviour, pinned alongside the comparer change since both live on the same line.
        var connection = new FakeConnection([("ProductId", DBNull.Value)]);

        Assert.Null(Assert.Single(connection.QueryPartialList("SELECT 1"))["ProductId"]);
    }

    [Fact]
    public void QueryPartialList_MissingColumn_StillThrows()
    {
        // The fold must not turn an absent column into a silent null.
        var connection = new FakeConnection([("ProductId", 42)]);

        IDictionary<string, object?> row = Assert.Single(connection.QueryPartialList("SELECT 1"));

        Assert.Throws<KeyNotFoundException>(() => row["Nonexistent"]);
    }

    // ------------------------------------------------------------------
    // A one-row reader over a caller-supplied column list
    // ------------------------------------------------------------------

    // ConnectionString and CommandText are declared [AllowNull] on the ADO.NET base types. The
    // attribute is not public on net472, so the stubs below re-declare them as plain non-nullable
    // strings and silence the resulting nullability mismatch rather than target-framework-fork it.
#pragma warning disable CS8765

    private sealed class FakeConnection(IReadOnlyList<(string Name, object Value)> columns) : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => new FakeCommand(columns) { Connection = this };
        public void Open() { }
        public void Dispose() { }
    }

    private sealed class FakeDbConnection(IReadOnlyList<(string Name, object Value)> columns) : System.Data.Common.DbConnection
    {
        public override string ConnectionString { get => ""; set { } }
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override System.Data.Common.DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override System.Data.Common.DbCommand CreateDbCommand() => new FakeDbCommand(columns) { Connection = this };
    }

    private sealed class FakeCommand(IReadOnlyList<(string Name, object Value)> columns) : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotSupportedException();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new OneRowReader(columns);
        public IDataReader ExecuteReader(CommandBehavior behavior) => new OneRowReader(columns);
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class FakeDbCommand(IReadOnlyList<(string Name, object Value)> columns) : System.Data.Common.DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override System.Data.Common.DbConnection? DbConnection { get; set; }
        protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override System.Data.Common.DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new OneRowDbReader(columns);
    }

    private sealed class FakeDbParameterCollection : System.Data.Common.DbParameterCollection
    {
        private readonly List<object> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => _items;

        public override int Add(object? value) { _items.Add(value!); return _items.Count - 1; }
        public override void AddRange(Array values) { }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object? value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) { }
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override System.Data.Common.DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override System.Data.Common.DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, System.Data.Common.DbParameter value) { }
        protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value) { }
    }

    private sealed class FakeParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }

    private sealed class OneRowReader(IReadOnlyList<(string Name, object Value)> columns) : IDataReader
    {
        private int _row;

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public int FieldCount => columns.Count;

        public bool Read() => _row++ < 1;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => columns[i].Value;
        public object this[string name] => throw new NotSupportedException();

        public string GetName(int i) => columns[i].Name;
        public object GetValue(int i) => columns[i].Value;
        public bool IsDBNull(int i) => columns[i].Value is DBNull;
        public Type GetFieldType(int i) => columns[i].Value.GetType();
        public int GetOrdinal(string name) => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "";
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public int GetInt32(int i) => (int)columns[i].Value;
        public long GetInt64(int i) => throw new NotSupportedException();
        public string GetString(int i) => (string)columns[i].Value;
        public int GetValues(object[] values) => 0;
    }

    private sealed class OneRowDbReader(IReadOnlyList<(string Name, object Value)> columns) : System.Data.Common.DbDataReader
    {
        private int _row;

        public override int Depth => 0;
        public override int FieldCount => columns.Count;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => _row++ < 1;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;

        public override object this[int ordinal] => columns[ordinal].Value;
        public override object this[string name] => throw new NotSupportedException();

        public override string GetName(int ordinal) => columns[ordinal].Name;
        public override object GetValue(int ordinal) => columns[ordinal].Value;
        public override bool IsDBNull(int ordinal) => columns[ordinal].Value is DBNull;
        public override Type GetFieldType(int ordinal) => columns[ordinal].Value.GetType();
        public override int GetOrdinal(string name) => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "";
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => (int)columns[ordinal].Value;
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => (string)columns[ordinal].Value;
        public override int GetValues(object[] values) => 0;
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }
#pragma warning restore CS8765
}
