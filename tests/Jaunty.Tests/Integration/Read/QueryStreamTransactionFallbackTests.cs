using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Regression tests for the genuinely-synchronous IDbCommand fallback path used by streaming
/// queries when the connection is not a <see cref="System.Data.Common.DbConnection"/>.
/// This path must accept any <see cref="IDbTransaction"/>, not just <see cref="System.Data.Common.DbTransaction"/>,
/// since <see cref="IDbCommand.Transaction"/> is typed <c>IDbTransaction?</c> and has no such requirement.
/// </summary>
/// <remarks>
/// A fully fake ADO.NET stack is used (not backed by a real provider such as SQLite) because
/// real <see cref="System.Data.Common.DbCommand"/>-derived types implement <see cref="IDbCommand.Transaction"/>
/// by casting to <see cref="System.Data.Common.DbTransaction"/> internally (a BCL-level constraint),
/// which would mask the behavior under test regardless of Jaunty's own code.
/// </remarks>
public class QueryStreamTransactionFallbackTests
{
    [Fact]
    public void QueryStream_FakeIDbCommand_WithNonDbTransaction_AttachesTransactionAndSucceeds()
    {
        var connection = new FakeConnection(rowCount: 3);
        var transaction = new FakeTransaction();

        var options = new CommandOptions<Category>(
            mapper: _ => new Category { CategoryId = 1, CategoryName = "Test" },
            transaction: transaction);

        var results = connection.QueryStream<Category>("SELECT 1", options).ToList();

        Assert.Equal(3, results.Count);
        Assert.Same(transaction, connection.LastCommand?.Transaction);
    }

    private sealed class FakeConnection : IDbConnection
    {
        private readonly int _rowCount;

        public FakeConnection(int rowCount) => _rowCount = rowCount;

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
            var command = new FakeCommand(_rowCount);
            LastCommand = command;
            return command;
        }

        public void Open() => State = ConnectionState.Open;
        public void Dispose() { }
    }

    private sealed class FakeCommand : IDbCommand
    {
        private readonly int _rowCount;

        public FakeCommand(int rowCount) => _rowCount = rowCount;

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
        public IDataReader ExecuteReader() => new FakeDataReader(_rowCount);
        public IDataReader ExecuteReader(CommandBehavior behavior) => new FakeDataReader(_rowCount);
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

    private sealed class FakeDataReader : IDataReader
    {
        private int _remaining;

        public FakeDataReader(int rowCount) => _remaining = rowCount;

        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public int FieldCount => 0;

        public void Close() { }
        public void Dispose() { }
        public DataTable GetSchemaTable() => new DataTable();
        public bool NextResult() => false;

        public bool Read()
        {
            if (_remaining <= 0) return false;
            _remaining--;
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
        public Type GetFieldType(int i) => typeof(string);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => string.Empty;
        public string GetString(int i) => string.Empty;
        public object GetValue(int i) => string.Empty;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
        public object this[int i] => string.Empty;
        public object this[string name] => string.Empty;
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
