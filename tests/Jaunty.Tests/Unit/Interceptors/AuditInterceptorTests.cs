using Jaunty.Diagnostics;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// Unit tests for <see cref="AuditInterceptor"/>.
/// </summary>
public class AuditInterceptorTests
{
    #region Helpers

    private static IDbConnection CreateMockConnection()
    {
        return new TestDbConnection();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultMaxRecords_CreatesInterceptor()
    {
        // Arrange & Act
        var interceptor = new AuditInterceptor();

        // Assert
        Assert.NotNull(interceptor);
        Assert.Equal(0, interceptor.RecordCount); // No records yet
    }

    [Fact]
    public void Constructor_WithCustomMaxRecords_UsesProvidedValue()
    {
        // Arrange & Act
        var interceptor = new AuditInterceptor(500);

        // Assert - we can't directly access maxRecords, but we can verify it accepts the parameter
        Assert.NotNull(interceptor);
    }

    [Fact]
    public async Task Constructor_WithZeroOrNegativeMaxRecords_UsesDefault()
    {
        // Arrange
        var interceptor = new AuditInterceptor(0);
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act - execute more than the documented default of 1000 records
        for (int i = 0; i < 1005; i++)
        {
            await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);
        }

        // Assert - should use default of 1000, not the unclamped 0
        Assert.Equal(1000, interceptor.RecordCount);
    }

    #endregion

    #region OnCommandExecutingAsync Tests

    [Fact]
    public async Task OnCommandExecutingAsync_CreatesExecutingRecord()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        var record = records[0];
        Assert.Equal(AuditPhase.Executing, record.Phase);
        Assert.Equal("SELECT * FROM Users", record.CommandText);
        Assert.Equal(CommandType.Text, record.CommandType);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_TrimsOldRecords_WhenExceedingMaxRecords()
    {
        // Arrange
        var interceptor = new AuditInterceptor(5);
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act - execute 10 commands
        for (int i = 0; i < 10; i++)
        {
            await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);
        }

        // Assert - should have at most 5 records
        Assert.True(interceptor.RecordCount <= 5);
    }

    #endregion

    #region OnCommandExecutedAsync Tests

    [Fact]
    public async Task OnCommandExecutedAsync_CreatesExecutedRecord()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var elapsed = TimeSpan.FromMilliseconds(150);
        var context = new CommandContext(
            "UPDATE Users SET Name = 'Test'",
            null,
            CreateMockConnection(),
            CommandType.Text,
            elapsed);

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        var record = records[0];
        Assert.Equal(AuditPhase.Executed, record.Phase);
        Assert.True(record.Success);
        Assert.Equal(150, record.ElapsedMilliseconds, 1);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_RecordsDatabaseName()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        Assert.Equal("TestDb", records[0].Database);
    }

    // AUD-R7: OnCommandExecutedAsync built its AuditRecord without setting ConnectionState at
    // all, unlike OnCommandExecutingAsync - it silently defaulted to ConnectionState.Closed
    // (enum value 0) regardless of the connection's actual state, misreporting every
    // "Executed" audit record for a compliance/troubleshooting audit trail.
    [Fact]
    public async Task OnCommandExecutedAsync_RecordsConnectionState()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert - TestDbConnection.State is always ConnectionState.Open
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        Assert.Equal(ConnectionState.Open, records[0].ConnectionState);
    }

    #endregion

    #region OnCommandFailedAsync Tests

    [Fact]
    public async Task OnCommandFailedAsync_CreatesFailedRecord()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var exception = new InvalidOperationException("Test error");
        var elapsed = TimeSpan.FromMilliseconds(50);
        var context = new CommandContext(
            "DELETE FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            elapsed,
            exception);

        // Act
        await interceptor.OnCommandFailedAsync(context, exception, CancellationToken.None);

        // Assert
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        var record = records[0];
        Assert.Equal(AuditPhase.Failed, record.Phase);
        Assert.False(record.Success);
        Assert.Equal("System.InvalidOperationException", record.ExceptionType);
        Assert.Equal("Test error", record.ExceptionMessage);
    }

    [Fact]
    public async Task OnCommandFailedAsync_RecordsElapsedTime()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var exception = new Exception("Error");
        var elapsed = TimeSpan.FromMilliseconds(100);
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            elapsed,
            exception);

        // Act
        await interceptor.OnCommandFailedAsync(context, exception, CancellationToken.None);

        // Assert
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        Assert.Equal(100, records[0].ElapsedMilliseconds, 1);
    }

    // AUD-R7: same gap as OnCommandExecutedAsync - OnCommandFailedAsync never set
    // ConnectionState either.
    [Fact]
    public async Task OnCommandFailedAsync_RecordsConnectionState()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var exception = new Exception("Error");
        var elapsed = TimeSpan.FromMilliseconds(10);
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            elapsed,
            exception);

        // Act
        await interceptor.OnCommandFailedAsync(context, exception, CancellationToken.None);

        // Assert - TestDbConnection.State is always ConnectionState.Open
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Single(records);
        Assert.Equal(ConnectionState.Open, records[0].ConnectionState);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_RemovesAllRecords()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var context = new CommandContext(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act - create some records
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);
        interceptor.Clear();

        // Assert
        Assert.Empty(interceptor.GetRecentRecords());
        Assert.Equal(0, interceptor.RecordCount);
    }

    #endregion

    #region GetRecentRecords Tests

    [Fact]
    public async Task GetRecentRecords_ReturnsAllRecords()
    {
        // Arrange
        var interceptor = new AuditInterceptor();
        var context1 = new CommandContext("SELECT 1", null, CreateMockConnection(), CommandType.Text);
        var context2 = new CommandContext("SELECT 2", null, CreateMockConnection(), CommandType.Text);
        var context3 = new CommandContext("SELECT 3", null, CreateMockConnection(), CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context1, CancellationToken.None);
        await interceptor.OnCommandExecutingAsync(context2, CancellationToken.None);
        await interceptor.OnCommandExecutingAsync(context3, CancellationToken.None);

        // Assert - all records should be returned
        var records = interceptor.GetRecentRecords().ToList();
        Assert.Equal(3, records.Count);
        var commandTexts = records.Select(r => r.CommandText).ToList();
        Assert.Contains("SELECT 1", commandTexts);
        Assert.Contains("SELECT 2", commandTexts);
        Assert.Contains("SELECT 3", commandTexts);
    }

    [Fact]
    public async Task GetRecentRecords_LimitsResults_WhenCountSpecified()
    {
        // Arrange
        var interceptor = new AuditInterceptor(100);
        var context = new CommandContext("SELECT 1", null, CreateMockConnection(), CommandType.Text);

        // Act - create 50 records
        for (int i = 0; i < 50; i++)
        {
            await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);
        }

        // Assert - request only 10
        var records = interceptor.GetRecentRecords(10).ToList();
        Assert.Equal(10, records.Count);
    }

    #endregion

    #region AuditRecord Tests

    [Fact]
    public void AuditRecord_ToString_ReturnsFormattedString()
    {
        // Arrange
        var record = new AuditRecord
        {
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Phase = AuditPhase.Executed,
            CommandType = CommandType.Text,
            ElapsedMilliseconds = 123.45,
            CommandText = "SELECT * FROM Users WHERE Id = 1"
        };

        // Act
        var result = record.ToString();

        // Assert
        Assert.Contains("Executed", result);
        Assert.Contains("Text", result);
        Assert.Contains("123.45", result);
    }

    #endregion

    #region Helper Classes

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
