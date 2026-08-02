using System.Data.Common;

using Jaunty.Core;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R34-003. Every stored-procedure family reached its options only past a mandatory
/// <c>object? parameters</c> argument: the sole three-argument overload was
/// <c>(connection, procedureName, object? parameters)</c>. So
/// <c>connection.ExecuteStoredProcedure&lt;T&gt;("proc", options)</c> bound the options struct as a
/// parameters object and discarded the caller's transaction, timeout and command type with no
/// diagnostic - the AUD-R34-002 defect, in a family the AUD-R34-002 conversion could not reach,
/// because there was no <c>CommandOptions&lt;T&gt;</c> overload for it to select. The type's own
/// documented example (<c>ExecuteStoredProcedureScalar&lt;int&gt;("GetTotalCount", options: options)</c>)
/// used the missing shape and did not compile.
/// <para>
/// No provider here on purpose. SQLite rejects <see cref="CommandType.StoredProcedure"/> from the
/// <c>CommandType</c> setter, so a real connection throws before anything is observable; a stub
/// records what Jaunty actually put on the command, which is the thing under test.
/// </para>
/// </summary>
public class StoredProcedureOptionsOverloadTests
{
    internal sealed class Row
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// Derives from <see cref="DbConnection"/> rather than implementing <see cref="IDbConnection"/>:
    /// Jaunty's async stored-procedure paths reject a bare <c>IDbConnection</c> with "Async
    /// connection requires a DbConnection or its subclass", so the async half of the fix would be
    /// untestable otherwise. The async overrides inherited from <see cref="DbCommand"/> forward to
    /// the synchronous ones, which is all the recording needs.
    /// </summary>
#pragma warning disable CS8765 // DbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
    private sealed class RecordingConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        internal string? ExecutedCommandText { get; private set; }
        internal CommandType? ExecutedCommandType { get; private set; }
        internal int? ExecutedTimeout { get; private set; }
        internal IDbTransaction? ExecutedTransaction { get; private set; }

        public override string ConnectionString { get => ""; set { } }
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new StubTransaction(this);
        protected override DbCommand CreateDbCommand() => new RecordingCommand(this);

        internal void Record(RecordingCommand command)
        {
            ExecutedCommandText = command.CommandText;
            ExecutedCommandType = command.CommandType;
            ExecutedTimeout = command.CommandTimeout;
            ExecutedTransaction = command.Transaction;
        }
    }

    private sealed class StubTransaction(DbConnection connection) : DbTransaction
    {
        protected override DbConnection? DbConnection { get; } = connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class RecordingCommand(RecordingConnection owner) : DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = owner;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new StubParameterCollection();

        public override void Cancel() { }
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new StubParameter();

        public override int ExecuteNonQuery()
        {
            owner.Record(this);
            return 0;
        }

        public override object? ExecuteScalar()
        {
            owner.Record(this);
            return 0;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            owner.Record(this);
            return new EmptyDbReader();
        }
    }
#pragma warning restore CS8765

    private sealed class StubParameter : DbParameter
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

    private sealed class StubParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => _items;

        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (object v in values) Add(v); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) =>
            _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }

    private sealed class EmptyDbReader : DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 1;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => false;
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
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => default;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => "Id";
        public override int GetOrdinal(string name) => 0;
        public override string GetString(int ordinal) => "";
        public override object GetValue(int ordinal) => 0;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => false;
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    /// <summary>
    /// The damaging case, and the one the whole finding is about: the caller asks for their
    /// transaction and, before the fix, the procedure ran outside it.
    /// </summary>
    [Fact]
    public void ExecuteStoredProcedure_GivenOptionsAndNoParameters_AppliesTheTransaction()
    {
        using var connection = new RecordingConnection();
        using IDbTransaction transaction = connection.BeginTransaction();

        connection.ExecuteStoredProcedure<Row>("GetRows", CommandOptions<Row>.WithTransaction(transaction));

        Assert.Same(transaction, connection.ExecutedTransaction);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
        Assert.Equal("GetRows", connection.ExecutedCommandText);
    }

    [Fact]
    public void ExecuteStoredProcedureFirstOrDefault_GivenOptionsAndNoParameters_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        connection.ExecuteStoredProcedureFirstOrDefault<Row>("GetRow", CommandOptions<Row>.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_GivenOptionsAndNoParameters_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        connection.ExecuteStoredProcedureScalar<int>("GetCount", CommandOptions<int>.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_GivenOptionsAndNoParameters_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        connection.ExecuteStoredProcedureNonQuery("DoWork", CommandOptions.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task ExecuteStoredProcedureAsync_GivenOptionsAndNoParameters_AppliesTheTransaction()
    {
        using var connection = new RecordingConnection();
        using IDbTransaction transaction = connection.BeginTransaction();

        await connection.ExecuteStoredProcedureAsync<Row>("GetRows", CommandOptions<Row>.WithTransaction(transaction));

        Assert.Same(transaction, connection.ExecutedTransaction);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_GivenOptionsAndNoParameters_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        await connection.ExecuteStoredProcedureScalarAsync<int>("GetCount", CommandOptions<int>.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task ExecuteStoredProcedureNonQueryAsync_GivenOptionsAndNoParameters_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        await connection.ExecuteStoredProcedureNonQueryAsync("DoWork", CommandOptions.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
    }

    /// <summary>
    /// The non-generic form reaches the new overloads through the AUD-R34-002 conversion, so this
    /// covers both fixes at once - and it is the shape the <c>CommandOptions</c> documentation gives
    /// as its worked example, which did not compile before either fix.
    /// </summary>
    [Fact]
    public void ExecuteStoredProcedureScalar_GivenTheDocumentedNonGenericShape_AppliesTheTimeout()
    {
        using var connection = new RecordingConnection();

        connection.ExecuteStoredProcedureScalar<int>("GetTotalCount", options: CommandOptions.WithTimeout(97));

        Assert.Equal(97, connection.ExecutedTimeout);
    }

    /// <summary>
    /// The control: a real parameters object must still bind to the parameters overload rather than
    /// being captured by the new options overload.
    /// </summary>
    [Fact]
    public void ExecuteStoredProcedure_GivenAParametersObject_StillBindsItAsParameters()
    {
        using var connection = new RecordingConnection();

        connection.ExecuteStoredProcedure<Row>("GetRows", new { CategoryId = 5 });

        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
        Assert.Equal(0, connection.ExecutedTimeout);
        Assert.Null(connection.ExecutedTransaction);
    }
}
