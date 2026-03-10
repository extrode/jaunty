using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Integration.Interceptors;

/// <summary>
/// Integration tests for <see cref="LoggingInterceptor"/> with real database connections.
/// </summary>
public class LoggingInterceptorIntegrationTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly TestLoggerProvider _loggerProvider;
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptorIntegrationTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Initialize schema
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT,
                Password TEXT
            )";
        cmd.ExecuteNonQuery();

        _loggerProvider = new TestLoggerProvider();
        _logger = _loggerProvider;
    }

    [Fact]
    public async Task QueryPartial_WithLoggingInterceptor_LogsSqlAndParameters()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            LogParameters = true,
            MinimumLogLevel = LogLevel.Information
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Insert test data
            await _connection.InsertAsync(new TestUser { Name = "John Doe", Email = "john@example.com", Password = "secret123" });

            // Act - clear logs before query
            _loggerProvider.Clear();
            var results = _connection.QueryPartial<TestUser>("SELECT * FROM Users WHERE Name = @Name", new { Name = "John Doe" });

            // Assert
            var userList = results.ToList();
            Assert.Single(userList);
            Assert.Equal("John Doe", userList[0].Name);

            // Verify logging occurred
            Assert.NotEmpty(_loggerProvider.Logs);
            var logEntry = _loggerProvider.Logs.Single(l => l.Message.Contains("Executing"));
            Assert.Contains("SQL Text", logEntry.Message);
            Assert.Contains("SELECT", logEntry.Message);
            Assert.Contains("Name=", logEntry.Message);
        }
        finally
        {
            // Reset interceptor pipeline
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task InsertAsync_WithLoggingInterceptor_LogsInsertStatement()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            LogParameters = true,
            MinimumLogLevel = LogLevel.Information
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Act
            var user = new TestUser { Name = "Jane Smith", Email = "jane@example.com", Password = "password456" };
            _loggerProvider.Clear();
            var id = await _connection.InsertAsync(user);

            // Assert
            Assert.True(id > 0);
            Assert.NotEmpty(_loggerProvider.Logs);

            var execLog = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("Executing"));
            Assert.True(!string.IsNullOrEmpty(execLog.Message));
            Assert.Contains("INSERT", execLog.Message);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task UpdateAsync_WithLoggingInterceptor_LogsElapsedTime()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            LogParameters = false,
            MinimumLogLevel = LogLevel.Information
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Insert first
            var id = await _connection.InsertAsync(new TestUser { Name = "Update Test", Email = "update@test.com", Password = "pass" });

            // Act
            _loggerProvider.Clear();
            var updated = new TestUser { Id = (int)id, Name = "Updated Name", Email = "updated@test.com", Password = "pass" };
            var rowsAffected = await _connection.UpdateAsync(updated);

            // Assert
            Assert.Equal(1, rowsAffected);

            // Should have completion log with elapsed time
            var completedLog = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("Completed") && l.Message.Contains("ms"));
            Assert.True(!string.IsNullOrEmpty(completedLog.Message));
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task QueryPartial_WithSensitiveParameterNames_MasksPasswordInLogs()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            LogParameters = true,
            MinimumLogLevel = LogLevel.Information,
            MaskedValueFormat = "***MASKED***"
        };
        loggingConfig.SensitiveParameterNames.Add("Password");
        loggingConfig.SensitiveParameterNames.Add("password");
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Insert test data
            await _connection.InsertAsync(new TestUser { Name = "Secret User", Email = "secret@example.com", Password = "supersecret" });

            // Act - query with password parameter
            _loggerProvider.Clear();
            var results = _connection.QueryPartial<TestUser>("SELECT * FROM Users WHERE Password = @Password", new { Password = "supersecret" });

            // Assert
            var userList = results.ToList();
            Assert.Single(userList);

            // Verify password is masked in logs
            var logEntry = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("Executing"));
            Assert.True(!string.IsNullOrEmpty(logEntry.Message));
            Assert.Contains("***MASKED***", logEntry.Message);
            Assert.DoesNotContain("supersecret", logEntry.Message);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task ExecuteNonQuery_WithSlowQueryThreshold_LogsWarning()
    {
        // Arrange - set very low threshold to trigger slow query warning
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            MinimumLogLevel = LogLevel.Information,
            SlowQueryThreshold = TimeSpan.FromMilliseconds(1) // Very low threshold
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Act - add artificial delay via a complex query
            _loggerProvider.Clear();

            // Use a query that will take some time
            var command = _connection.CreateCommand();
            command.CommandText = @"
                WITH RECURSIVE cnt(x) AS (
                    SELECT 1
                    UNION ALL
                    SELECT x+1 FROM cnt WHERE x < 10000
                )
                SELECT COUNT(*) FROM cnt";
            command.ExecuteNonQuery();

            // Assert - should have slow query warning
            var slowQueryLog = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("SLOW"));
            Assert.True(!string.IsNullOrEmpty(slowQueryLog.Message));
            Assert.Equal(LogLevel.Warning, slowQueryLog.Level);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task DeleteAsync_WithLoggingInterceptor_LogsFailureOnException()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            MinimumLogLevel = LogLevel.Information
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Act & Assert - invalid SQL should trigger failure logging
            _loggerProvider.Clear();
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await _connection.DeleteAsync<TestUser>("INVALID SQL STATEMENT");
            });

            // Verify failure was logged
            var failureLog = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("Failed"));
            Assert.True(!string.IsNullOrEmpty(failureLog.Message));
            Assert.Equal(LogLevel.Error, failureLog.Level);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    [Fact]
    public async Task QueryScalar_WithLoggingInterceptor_LogsScalarQuery()
    {
        // Arrange
        var loggingConfig = new LoggingConfiguration
        {
            LogSql = true,
            LogParameters = true,
            MinimumLogLevel = LogLevel.Information
        };
        var interceptor = new LoggingInterceptor(_logger, loggingConfig);
        JauntyConfig.AddInterceptor(interceptor);

        try
        {
            // Insert test data
            await _connection.InsertAsync(new TestUser { Name = "Scalar Test", Email = "scalar@test.com", Password = "pass" });

            // Act
            _loggerProvider.Clear();
            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");

            // Assert
            Assert.True(count > 0);

            var execLog = _loggerProvider.Logs.FirstOrDefault(l => l.Message.Contains("Executing"));
            Assert.True(!string.IsNullOrEmpty(execLog.Message));
            Assert.Contains("SELECT COUNT", execLog.Message);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }
    }

    #region Helper Classes

    public class TestUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    private class TestLoggerProvider : ILoggerProvider, ILogger<LoggingInterceptor>
    {
        private ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _logs = new();

        public IEnumerable<(LogLevel Level, string Message, Exception? Exception)> Logs => _logs;

        public void Clear() => _logs = new ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)>();

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose() { }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _logs.Enqueue((logLevel, message, exception));
        }

        public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    #endregion

    public void Dispose()
    {
        _connection?.Dispose();
        JauntyConfig.ClearInterceptors();
    }
}
