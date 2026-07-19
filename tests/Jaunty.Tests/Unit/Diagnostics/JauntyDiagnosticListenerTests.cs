using Jaunty.Diagnostics;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Diagnostics;

/// <summary>
/// Unit tests for <see cref="JauntyDiagnosticListener"/>.
/// </summary>
public class JauntyDiagnosticListenerTests : IDisposable
{
    private readonly TestDiagnosticObserver _observer;
    private readonly JauntyDiagnosticListener _listener;
    private readonly TestDbConnection _connection;

    public JauntyDiagnosticListenerTests()
    {
        _observer = new TestDiagnosticObserver();
        _listener = new JauntyDiagnosticListener();
        _connection = new TestDbConnection();

        // Subscribe to all events
        _listener.Subscribe(_observer);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesListener()
    {
        // Arrange & Act
        var listener = new JauntyDiagnosticListener();

        // Assert
        Assert.NotNull(listener);
        Assert.Equal(JauntyDiagnosticListener.DiagnosticSourceName, listener.Name);
    }

    [Fact]
    public void Instance_ReturnsSingleton()
    {
        // Arrange & Act
        var instance1 = JauntyDiagnosticListener.Instance;
        var instance2 = JauntyDiagnosticListener.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    #endregion

    #region WriteCommandExecuting Tests

    [Fact]
    public void WriteCommandExecuting_EmitsEvent()
    {
        // Arrange
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            _connection,
            CommandType.Text);

        // Act
        _listener.WriteCommandExecuting(context);

        // Assert
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
        var eventPayload = (CommandExecutingPayload)_observer.Events.First(e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName).Value!;
        Assert.Equal("SELECT * FROM Users", eventPayload.CommandText);
        Assert.Equal(CommandType.Text, eventPayload.CommandType);
        Assert.Equal("TestDb", eventPayload.Database);
    }

    [Fact]
    public void WriteCommandExecuting_WhenDisabled_DoesNotEmit()
    {
        // Arrange - subscribe a local observer to the local listener under test. DiagnosticSource
        // subscriptions are per-instance, so asserting against the class-level _observer (which is
        // only ever subscribed to the class-level _listener, never this local one) would pass
        // unconditionally regardless of whether disposal actually suppressed emission.
        var listener = new JauntyDiagnosticListener();
        var localObserver = new TestDiagnosticObserver();
        listener.Subscribe(localObserver);
        var context = new CommandContext("SELECT 1", null, _connection, CommandType.Text);

        // Act - dispose to disable
        listener.Dispose();
        listener.WriteCommandExecuting(context);

        // Assert
        Assert.Empty(localObserver.Events);
    }

    #endregion

    #region WriteCommandExecuted Tests

    [Fact]
    public void WriteCommandExecuted_EmitsEvent()
    {
        // Arrange
        var elapsed = TimeSpan.FromMilliseconds(50);
        var context = new CommandContext(
            "UPDATE Users SET Name = 'Test'",
            null,
            _connection,
            CommandType.Text,
            elapsed);

        // Act
        _listener.WriteCommandExecuted(context);

        // Assert
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutedEventName);
        var eventPayload = (CommandExecutedPayload)_observer.Events.First(e => e.Key == JauntyDiagnosticListener.CommandExecutedEventName).Value!;
        Assert.Equal("UPDATE Users SET Name = 'Test'", eventPayload.CommandText);
        Assert.Equal(50, eventPayload.ElapsedMilliseconds, 1);
        Assert.True(eventPayload.Success);
    }

    #endregion

    #region WriteCommandFailed Tests

    [Fact]
    public void WriteCommandFailed_EmitsEvent()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var elapsed = TimeSpan.FromMilliseconds(25);
        var context = new CommandContext(
            "DELETE FROM Users",
            null,
            _connection,
            CommandType.Text,
            elapsed,
            exception);

        // Act
        _listener.WriteCommandFailed(context, exception);

        // Assert
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandFailedEventName);
        var eventPayload = (CommandFailedPayload)_observer.Events.First(e => e.Key == JauntyDiagnosticListener.CommandFailedEventName).Value!;
        Assert.Equal("DELETE FROM Users", eventPayload.CommandText);
        Assert.Equal("System.InvalidOperationException", eventPayload.ExceptionType);
        Assert.Equal("Test error", eventPayload.ExceptionMessage);
        Assert.False(eventPayload.Success);
    }

    [Fact]
    public void WriteCommandFailed_RecordsElapsedTime()
    {
        // Arrange
        var exception = new Exception("Error");
        var elapsed = TimeSpan.FromMilliseconds(100);
        var context = new CommandContext("SELECT 1", null, _connection, CommandType.Text, elapsed, exception);

        // Act
        _listener.WriteCommandFailed(context, exception);

        // Assert
        var eventPayload = (CommandFailedPayload)_observer.Events.First(e => e.Key == JauntyDiagnosticListener.CommandFailedEventName).Value!;
        Assert.Equal(100, eventPayload.ElapsedMilliseconds, 1);
    }

    #endregion

    #region Payload Tests

    [Fact]
    public void CommandExecutingPayload_InitializesProperties()
    {
        // Arrange
        var context = new CommandContext(
            "SELECT * FROM Products",
            new Dictionary<string, object> { { "id", 42 } },
            _connection,
            CommandType.StoredProcedure);

        // Act
        var payload = new CommandExecutingPayload(context);

        // Assert
        Assert.Equal("SELECT * FROM Products", payload.CommandText);
        Assert.Equal(CommandType.StoredProcedure, payload.CommandType);
        Assert.Equal("TestDb", payload.Database);
        Assert.NotEmpty(payload.ConnectionId);
        Assert.Equal(_connection.GetType().Name, payload.ProviderName);
    }

    [Fact]
    public void CommandExecutingPayload_ConnectionId_IsStableForSameConnection()
    {
        var context1 = new CommandContext("SELECT 1", null, _connection, CommandType.Text);
        var context2 = new CommandContext("SELECT 2", null, _connection, CommandType.Text);

        var payload1 = new CommandExecutingPayload(context1);
        var payload2 = new CommandExecutingPayload(context2);

        Assert.Equal(payload1.ConnectionId, payload2.ConnectionId);
    }

    [Fact]
    public void CommandExecutingPayload_ConnectionId_IsUniquePerConnectionInstance()
    {
        // Regression test: ConnectionId used to be Connection.GetHashCode().ToString(), which is
        // not guaranteed unique and can be reused across different instances once one is GC'd.
        var otherConnection = new TestDbConnection();
        var context1 = new CommandContext("SELECT 1", null, _connection, CommandType.Text);
        var context2 = new CommandContext("SELECT 1", null, otherConnection, CommandType.Text);

        var payload1 = new CommandExecutingPayload(context1);
        var payload2 = new CommandExecutingPayload(context2);

        Assert.NotEqual(payload1.ConnectionId, payload2.ConnectionId);
    }

    [Fact]
    public void CommandExecutedPayload_InitializesProperties()
    {
        // Arrange
        var elapsed = TimeSpan.FromMilliseconds(75);
        var context = new CommandContext("SELECT 1", null, _connection, CommandType.Text, elapsed);

        // Act
        var payload = new CommandExecutedPayload(context);

        // Assert
        Assert.Equal(75, payload.ElapsedMilliseconds, 1);
        Assert.True(payload.Success);
    }

    [Fact]
    public void CommandFailedPayload_InitializesProperties()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var elapsed = TimeSpan.FromMilliseconds(30);
        var context = new CommandContext("SELECT 1", null, _connection, CommandType.Text, elapsed, exception);

        // Act
        var payload = new CommandFailedPayload(context, exception);

        // Assert
        Assert.Equal(30, payload.ElapsedMilliseconds, 1);
        Assert.Equal("System.ArgumentException", payload.ExceptionType);
        Assert.Equal("Invalid argument", payload.ExceptionMessage);
        Assert.False(payload.Success);
    }

    #endregion

    #region Helper Classes

    private class TestDiagnosticObserver : IObserver<KeyValuePair<string, object?>>
    {
        public List<KeyValuePair<string, object?>> Events { get; } = new();

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            Events.Add(value);
        }
    }

    private class TestDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";
        public int ConnectionTimeout => 15;
        public string Database => "TestDb";
        public string DataSource => "InMemory";
        public IDbTransaction? Transaction { get; set; }
        public ConnectionState State => ConnectionState.Open;

        public IDbCommand CreateCommand() => new TestDbCommand(this);
        public IDbTransaction BeginTransaction() => new TestDbTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new TestDbTransaction();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Open() { }
        public void Dispose() { }
    }

    private class TestDbCommand : IDbCommand
    {
        private readonly TestDbConnection _connection;

        public TestDbCommand(TestDbConnection connection)
        {
            _connection = connection;
        }

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new TestParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.Both;

        public void Cancel() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new TestDbDataReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => new TestDbDataReader();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
        public void Dispose() { }

        public IDbDataParameter CreateParameter() => new TestDbParameter();
    }

    private class TestDbParameter : IDbDataParameter
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

    private class TestParameterCollection : IDataParameterCollection
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

    private class TestDbTransaction : IDbTransaction
    {
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public IDbConnection? Connection { get; set; }
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }

    private class TestDbDataReader : IDataReader
    {
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;

        public void Close() { }
        public void Dispose() { }
        public DataTable GetSchemaTable() => new DataTable();
        public bool NextResult() => false;
        public bool Read() => false;
        public int GetOrdinal(string name) => 0;
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldoffset, byte[]? buffer, int bufferoffset, int length) => 0;
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
        public int FieldCount => 0;
        public string GetString(int i) => string.Empty;
        public object GetValue(int i) => string.Empty;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
        public object this[int i] => string.Empty;
        public object this[string name] => string.Empty;
    }

    #endregion
}
