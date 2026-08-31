using System.Data;

using Jaunty.Attributes;
using Jaunty.Fluent;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26-057 (batch 5, low/performance). Three joined terminals that need at most one or two rows
/// fetched the whole joined result set.
///
/// <para>
/// <c>SelectSingle</c> and <c>SelectSingleOrDefault</c> passed <c>BuildSelectSql(columns)</c>
/// straight through with no paging, while their <c>SelectFirst</c> neighbours four lines above
/// wrapped the same SQL in <c>GetPagingSql(..., 0, 1)</c>. <c>SelectFirstBoth</c> called
/// <c>SelectBothInternal</c> - which had no limit parameter at all - and then indexed <c>[0]</c>, so
/// it materialised and mapped every joined row to return one.
/// </para>
///
/// <para>
/// <c>QueryBuilder</c>'s equivalent does bound it: <c>SelectSingle</c> sets <c>_take = 2</c>
/// precisely so the "more than one element" check can be made without reading the table. Two is the
/// right number - <c>QueryPartialSingle</c> throws as soon as a second row exists, so a third row
/// cannot change the answer. On a large join, <c>SelectSingle()</c> read the entire result set to
/// discover it should have thrown.
/// </para>
///
/// <para>
/// The finding named only the sync file. The async counterparts had the identical gap, so both are
/// covered here - the unit of work is the mechanism, not the file list.
/// </para>
/// </summary>
public class JoinedTerminalsAreBoundedTests
{
    [Table("bounded_orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    [Table("bounded_customers")]
    public class Customer
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static (SqliteConnection Connection, IJoinedQuery<Order, Customer> Query) Joined()
    {
        var connection = new SqliteConnection();
        IJoinedQuery<Order, Customer> query = connection
            .From<Order>()
            .InnerJoin<Customer>()
            .On((o, c) => o.CustomerId == c.Id);

        return (connection, query);
    }

    private static void Swallow(Action action)
    {
        // The stub reader returns no rows, so the Single/First terminals throw their own
        // "sequence contains no elements" - which is not what these tests are about. The SQL has
        // already been captured by the time that happens.
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
        }
    }

    // ------------------------------------------------------------------
    // Sync
    // ------------------------------------------------------------------

    [Fact]
    public void SelectSingle_PagesToTwoRows()
    {
        (SqliteConnection connection, IJoinedQuery<Order, Customer> query) = Joined();

        Swallow(() => query.SelectSingle());

        Assert.Contains("LIMIT 2", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectSingleOrDefault_PagesToTwoRows()
    {
        (SqliteConnection connection, IJoinedQuery<Order, Customer> query) = Joined();

        Swallow(() => query.SelectSingleOrDefault());

        Assert.Contains("LIMIT 2", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectFirstBoth_PagesToOneRow()
    {
        (SqliteConnection connection, IJoinedQuery<Order, Customer> query) = Joined();

        Swallow(() => query.SelectFirstBoth());

        Assert.Contains("LIMIT 1", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Async
    // ------------------------------------------------------------------

    [Fact]
    public async Task SelectFirstBothAsync_PagesToOneRow()
    {
        var connection = new Async.SqliteConnection();
        IJoinedQuery<Order, Customer> query = connection
            .From<Order>()
            .InnerJoin<Customer>()
            .On((o, c) => o.CustomerId == c.Id);

        try
        {
            await query.SelectFirstBothAsync();
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Contains("LIMIT 1", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>SelectBoth</c> returns every joined row by contract and must stay unpaged - the limit is
    /// an argument to the shared internal, defaulting to none, rather than a change to it.
    /// </summary>
    [Fact]
    public void SelectBoth_IsStillUnbounded()
    {
        (SqliteConnection connection, IJoinedQuery<Order, Customer> query) = Joined();

        Swallow(() => query.SelectBoth());

        Assert.DoesNotContain("LIMIT", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And <c>SelectFirst</c>, which was already correct, still pages to one rather than picking up
    /// the two used by the Single terminals.
    /// </summary>
    [Fact]
    public void SelectFirst_StillPagesToOneRow()
    {
        (SqliteConnection connection, IJoinedQuery<Order, Customer> query) = Joined();

        Swallow(() => query.SelectFirst());

        Assert.Contains("LIMIT 1", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Stubs. The class must be named SqliteConnection: dialect resolution keys on the
    // connection's exact Type.Name.
    // ------------------------------------------------------------------

    private sealed class SqliteConnection : IDbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;

        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Dispose() { }
        public void Open() { }

        public IDbCommand CreateCommand() => new CapturingCommand(text => LastCommandText = text);
    }

    private sealed class CapturingCommand(Action<string> record) : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new CapturingParameters();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new CapturingParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() { record(CommandText); return 0; }
        public IDataReader ExecuteReader() { record(CommandText); return new EmptyReader(); }
        public IDataReader ExecuteReader(CommandBehavior behavior) { record(CommandText); return new EmptyReader(); }
        public object? ExecuteScalar() { record(CommandText); return 0; }
        public void Prepare() { }
    }

    private sealed class CapturingParameters : List<object>, IDataParameterCollection
    {
        public object this[string parameterName] { get => this[0]; set { } }
        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }

    private sealed class CapturingParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class EmptyReader : IDataReader
    {
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public int FieldCount => 0;

        public bool Read() => false;
        public bool NextResult() => false;
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
        public Type GetFieldType(int i) => typeof(int);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => "";
        public int GetOrdinal(string name) => 0;
        public string GetString(int i) => "";
        public object GetValue(int i) => 0;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
    }

    /// <summary>
    /// The async terminals require a <see cref="System.Data.Common.DbConnection"/>, so the stub is
    /// mirrored on that base - and it must <em>also</em> be called <c>SqliteConnection</c>, because
    /// <c>From&lt;Order&gt;()</c> resolves the dialect from the connection's exact
    /// <see cref="Type.Name"/> before any terminal runs. Two sibling nested types cannot share a
    /// name, so this one is nested one level deeper; <c>Type.Name</c> ignores the enclosing type.
    /// </summary>
    private static class Async
    {
    internal sealed class SqliteConnection : System.Data.Common.DbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;

        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override System.Data.Common.DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override System.Data.Common.DbCommand CreateDbCommand() =>
            new CapturingDbCommand(text => LastCommandText = text);
    }
    }

    private sealed class CapturingDbCommand(Action<string> record) : System.Data.Common.DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override System.Data.Common.DbConnection? DbConnection { get; set; }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } = new CapturingDbParameters();
        protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() { record(CommandText); return 0; }
        public override object? ExecuteScalar() { record(CommandText); return 0; }
        public override void Prepare() { }

        protected override System.Data.Common.DbParameter CreateDbParameter() => new CapturingDbParameter();

        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            record(CommandText);
            return new EmptyDbReader();
        }
    }

    private sealed class CapturingDbParameters : System.Data.Common.DbParameterCollection
    {
        private readonly List<object> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => _items;

        public override int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public override void AddRange(Array values) { }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) { }
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }

        protected override System.Data.Common.DbParameter GetParameter(int index) => (System.Data.Common.DbParameter)_items[index];
        protected override System.Data.Common.DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, System.Data.Common.DbParameter value) { }
        protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value) { }
    }

    private sealed class CapturingDbParameter : System.Data.Common.DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    private sealed class EmptyDbReader : System.Data.Common.DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 0;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => false;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override bool NextResult() => false;

        public override object this[int ordinal] => 0;
        public override object this[string name] => 0;
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "int";
        public override DateTime GetDateTime(int ordinal) => default;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => Guid.Empty;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => "";
        public override int GetOrdinal(string name) => 0;
        public override string GetString(int ordinal) => "";
        public override object GetValue(int ordinal) => 0;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
    }
}
