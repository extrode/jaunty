using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Multiple;

/// <summary>
/// Regression tests for <c>QueryMultiple</c>/<c>QueryMultipleAsync</c> transaction handling against a real
/// <see cref="System.Data.Common.DbConnection"/>.
/// </summary>
/// <remarks>
/// The sync path builds its command via <see cref="System.Data.IDbCommand"/>, so any non-null
/// <see cref="System.Data.IDbTransaction"/> (even one that isn't a <see cref="System.Data.Common.DbTransaction"/>)
/// must be attached, not silently dropped. The async path builds its command via
/// <see cref="System.Data.Common.DbCommand"/>, whose <see cref="System.Data.Common.DbCommand.Transaction"/> setter
/// only accepts <see cref="System.Data.Common.DbTransaction"/>, so a non-<see cref="System.Data.Common.DbTransaction"/>
/// must be rejected with a clear exception (via <c>AsyncTransactionValidator.RequireDbTransaction</c>) instead of
/// silently running outside the caller's transaction.
/// </remarks>
public class QueryMultipleTransactionTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public QueryMultipleTransactionTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedSchema();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    private void SeedSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );
            INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks');";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Regression test for the genuinely-synchronous <see cref="IDbCommand"/> path used by
    /// <c>QueryMultiple</c>. This path must accept any <see cref="IDbTransaction"/>, not just
    /// <see cref="System.Data.Common.DbTransaction"/>, since <see cref="IDbCommand.Transaction"/> is
    /// typed <c>IDbTransaction?</c> and has no such requirement.
    /// </summary>
    /// <remarks>
    /// A fully fake ADO.NET stack is used here (not backed by a real provider such as SQLite)
    /// because real <see cref="System.Data.Common.DbCommand"/>-derived types implement
    /// <see cref="IDbCommand.Transaction"/> by casting to <see cref="System.Data.Common.DbTransaction"/>
    /// internally (a BCL-level constraint), which would mask the behavior under test regardless of
    /// Jaunty's own code — even <see cref="IDbTransactionWrapper"/> can't avoid this, since it
    /// delegates to a real inner <see cref="IDbCommand"/> that ultimately routes through
    /// <see cref="System.Data.Common.DbCommand"/>'s cast-enforcing setter.
    /// </remarks>
    [Fact]
    public void QueryMultiple_WithNonDbTransaction_ExecutesInsteadOfSilentlyDroppingTransaction()
    {
        var connection = new FakeConnection(scalarValue: 1L);
        var transaction = new FakeTransaction();

        using var gridReader = connection.QueryMultiple(
            "SELECT COUNT(*) FROM categories",
            CommandOptions.WithTransaction(transaction));

        var count = gridReader.ReadScalar<long>();

        Assert.Equal(1L, count);
        Assert.Same(transaction, connection.LastCommand?.Transaction);
    }

    [Fact]
    public async Task QueryMultipleAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            using var gridReader = await _connection.QueryMultipleAsync(
                "SELECT COUNT(*) FROM categories",
                CommandOptions.WithTransaction(nonDbTransaction));
        });

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    private sealed class FakeConnection : IDbConnection
    {
        private readonly object _scalarValue;

        public FakeConnection(object scalarValue) => _scalarValue = scalarValue;

        public FakeCommand? LastCommand { get; private set; }

        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "FakeDb";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => new FakeTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new FakeTransaction();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;

        public IDbCommand CreateCommand()
        {
            var command = new FakeCommand(_scalarValue);
            LastCommand = command;
            return command;
        }

        public void Open() => State = ConnectionState.Open;
        public void Dispose() { }
    }

    private sealed class FakeCommand : IDbCommand
    {
        private readonly object _scalarValue;

        public FakeCommand(object scalarValue) => _scalarValue = scalarValue;

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new FakeParameter();
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new FakeDataReader(_scalarValue);
        public IDataReader ExecuteReader(CommandBehavior behavior) => new FakeDataReader(_scalarValue);
        public object? ExecuteScalar() => null;
        public void Prepare() { }
        public void Dispose() { }
    }

    private sealed class FakeParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public bool IsNullable => false;
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        private readonly List<object> _parameters = new();

        public int Count => _parameters.Count;
        public object SyncRoot => this;
        public bool IsSynchronized => false;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;

        public int Add(object value) { _parameters.Add(value); return _parameters.Count - 1; }
        public bool Contains(object value) => _parameters.Contains(value);
        public bool Contains(string parameterName) => false;
        public void Clear() => _parameters.Clear();
        public int IndexOf(object value) => _parameters.IndexOf(value);
        public int IndexOf(string parameterName) => -1;
        public void Insert(int index, object value) => _parameters.Insert(index, value);
        public void Remove(object value) => _parameters.Remove(value);
        public void RemoveAt(int index) => _parameters.RemoveAt(index);
        public void RemoveAt(string parameterName) { }
        public object this[int index] { get => _parameters[index]; set => _parameters[index] = value; }
        public object this[string parameterName] { get => _parameters[0]; set => _parameters[0] = value; }
        public void CopyTo(Array array, int index) => ((System.Collections.IList)_parameters).CopyTo(array, index);
        public System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    }

    /// <summary>
    /// Yields exactly one row with one column containing a known scalar value, so
    /// <see cref="GridReader.ReadScalar{T}"/> exercises a real read rather than just proving no
    /// exception was thrown.
    /// </summary>
    private sealed class FakeDataReader : IDataReader
    {
        private readonly object _value;
        private bool _read;

        public FakeDataReader(object value) => _value = value;

        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public int FieldCount => 1;

        public void Close() { }
        public void Dispose() { }
        public DataTable GetSchemaTable() => new DataTable();
        public bool NextResult() => false;

        public bool Read()
        {
            if (_read) return false;
            _read = true;
            return true;
        }

        public int GetOrdinal(string name) => 0;
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => this;
        public string GetDataTypeName(int i) => string.Empty;
        public DateTime GetDateTime(int i) => DateTime.MinValue;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public Type GetFieldType(int i) => _value.GetType();
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => (long)_value;
        public string GetName(int i) => string.Empty;
        public string GetString(int i) => string.Empty;
        public object GetValue(int i) => _value;
        public int GetValues(object[] values) { values[0] = _value; return 1; }
        public bool IsDBNull(int i) => false;
        public object this[int i] => _value;
        public object this[string name] => _value;
    }

    private sealed class FakeTransaction : IDbTransaction
    {
        public IDbConnection? Connection { get; set; }
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }
}
