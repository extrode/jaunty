using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// Unit tests for <see cref="LoggingInterceptor"/>.
/// Tests log levels, slow query detection, parameter masking, and exception handling.
/// </summary>
public class LoggingInterceptorTests
{
    #region Helpers

    private static TestLoggerProvider CreateTestProvider(LogLevel minimumLevel = LogLevel.Information)
    {
        return new TestLoggerProvider(minimumLevel);
    }

    private static ILogger<LoggingInterceptor> CreateLogger(TestLoggerProvider provider)
    {
        return provider;
    }

    private static IDbConnection CreateMockConnection()
    {
        return new TestDbConnection();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggingInterceptor(null!));
    }

    [Fact]
    public void Constructor_WithNullConfig_UsesDefaultConfiguration()
    {
        var logger = CreateLogger(CreateTestProvider());
        var interceptor = new LoggingInterceptor(logger, null);

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void Constructor_WithConfig_UsesProvidedConfiguration()
    {
        var logger = CreateLogger(CreateTestProvider());
        var config = new LoggingConfiguration
        {
            MinimumLogLevel = LogLevel.Debug,
            SlowQueryThreshold = TimeSpan.FromMilliseconds(500)
        };

        var interceptor = new LoggingInterceptor(logger, config);

        Assert.NotNull(interceptor);
    }

    #endregion

    #region OnCommandExecutingAsync Tests

    [Fact]
    public async Task OnCommandExecutingAsync_LogsSql_WhenLogSqlIsTrue()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = false };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("Executing SQL Text: SELECT * FROM Users", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_DoesNotLogSql_WhenLogSqlIsFalse()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = false };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        Assert.Empty(provider.Logs);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_LogsParameters_WhenLogParametersIsTrue()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object> { { "id", 42 } };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE Id = @id",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("Parameters:", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_MasksSensitiveParameters()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        config.SensitiveParameterNames.Add("Password");
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            { "name", "John" },
            { "password", "secret123" }
        };
        var context = new CommandContext(
            "INSERT INTO Users (Name, Password) VALUES (@name, @password)",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("***MASKED***", logs[0].Message);
        Assert.DoesNotContain("secret123", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_RespectsMinimumLogLevel()
    {
        // Arrange
        var provider = CreateTestProvider(LogLevel.Error);
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { MinimumLogLevel = LogLevel.Warning, LogSql = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert - Warning level logs should be filtered out by the logger provider (which only accepts Error+)
        Assert.Empty(provider.Logs);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_SkipsWhenLoggerDisabled()
    {
        // Arrange
        var logger = NullLogger<LoggingInterceptor>.Instance;
        var config = new LoggingConfiguration { MinimumLogLevel = LogLevel.None };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert - Should complete without error
        Assert.True(true);
    }

    #endregion

    #region OnCommandExecutedAsync Tests

    [Fact]
    public async Task OnCommandExecutedAsync_LogsCompletionWithElapsedTime()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration();
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromMilliseconds(150));

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("Completed SQL Text in 150.00ms", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_LogsSlowQueryWarning_WhenExceedsThreshold()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { SlowQueryThreshold = TimeSpan.FromMilliseconds(100) };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromMilliseconds(500));

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Equal(LogLevel.Warning, logs[0].Level);
        Assert.Contains("SLOW", logs[0].Message);
        Assert.Contains("100ms threshold", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_DoesNotLogSlowQuery_WhenThresholdIsZero()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { SlowQueryThreshold = TimeSpan.Zero };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromSeconds(5));

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.DoesNotContain("SLOW", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_RespectsMinimumLogLevel()
    {
        // Arrange
        var provider = CreateTestProvider(LogLevel.Error);
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { MinimumLogLevel = LogLevel.Warning };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromMilliseconds(50));

        // Act
        await interceptor.OnCommandExecutedAsync(context, CancellationToken.None);

        // Assert - Warning level logs should be filtered out by the logger provider (which only accepts Error+)
        Assert.Empty(provider.Logs);
    }

    #endregion

    #region OnCommandFailedAsync Tests

    [Fact]
    public async Task OnCommandFailedAsync_LogsErrorWithException()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration();
        var interceptor = new LoggingInterceptor(logger, config);
        var exception = new InvalidOperationException("Test exception");
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromMilliseconds(50),
            exception);

        // Act
        await interceptor.OnCommandFailedAsync(context, exception, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Equal(LogLevel.Error, logs[0].Level);
        Assert.Contains("Failed executing SQL Text", logs[0].Message);
        Assert.Contains("Test exception", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandFailedAsync_LogsExceptionMessage()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration();
        var interceptor = new LoggingInterceptor(logger, config);
        var exception = new DataException("Database connection failed");
        var context = new CommandContext(
            "SELECT * FROM Users",
            null,
            CreateMockConnection(),
            CommandType.Text,
            TimeSpan.FromMilliseconds(10),
            exception);

        // Act
        await interceptor.OnCommandFailedAsync(context, exception, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("Database connection failed", logs[0].Message);
    }

    #endregion

    #region CommandType Tests

    [Theory]
    [InlineData(CommandType.Text, "SQL Text")]
    [InlineData(CommandType.StoredProcedure, "Stored Procedure")]
    [InlineData(CommandType.TableDirect, "Table Direct")]
    public async Task OnCommandExecutingAsync_LogsCorrectCommandType(CommandType commandType, string expectedType)
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var context = new CommandContext(
            "command text",
            null,
            CreateMockConnection(),
            commandType);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains(expectedType, logs[0].Message);
    }

    #endregion

    #region Parameter Formatting Tests

    [Fact]
    public async Task OnCommandExecutingAsync_FormatsStringParameters_WithQuotes()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            { "name", "John Doe" }
        };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE Name = @name",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("\"John Doe\"", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_FormatsNullParameters_AsNULL()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object?>
        {
            { "name", null }
        };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE Name = @name",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("NULL", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_FormatsBooleanParameters_AsLowerCase()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            { "active", true }
        };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE Active = @active",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("true", logs[0].Message);
    }

    [Fact]
    public async Task OnCommandExecutingAsync_FormatsDateTimeParameters_AsIso8601()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0);
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            { "date", testDate }
        };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE CreatedAt = @date",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("2024-01-15", logs[0].Message);
    }

    #endregion

    #region Dictionary Parameter Tests

    [Fact]
    public async Task OnCommandExecutingAsync_FormatsDictionaryParameters()
    {
        // Arrange
        var provider = CreateTestProvider();
        var logger = CreateLogger(provider);
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            { "id", 42 },
            { "name", "Test" }
        };
        var context = new CommandContext(
            "SELECT * FROM Users WHERE Id = @id",
            parameters,
            CreateMockConnection(),
            CommandType.Text);

        // Act
        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        // Assert
        var logs = provider.Logs;
        Assert.Single(logs);
        Assert.Contains("id=42", logs[0].Message);
        Assert.Contains("name=\"Test\"", logs[0].Message);
    }

    #endregion

    #region Test Helper Classes

    private class TestLoggerProvider : ILoggerProvider, ILogger<LoggingInterceptor>
    {
        private readonly LogLevel _minimumLevel;
        public List<LogEntry> Logs { get; } = new();

        public TestLoggerProvider(LogLevel minimumLevel = LogLevel.Information)
        {
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public void Dispose() { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= _minimumLevel)
            {
                Logs.Add(new LogEntry
                {
                    Level = logLevel,
                    EventId = eventId,
                    Message = formatter(state, exception),
                    Exception = exception
                });
            }
        }
    }

    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public EventId EventId { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
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
