using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// Unit tests for <see cref="CommandContext"/>.
/// Covers connection string sanitization.
/// </summary>
public class CommandContextTests
{
    #region Helpers

    private static CommandContext CreateContext(string connectionString)
    {
        return new CommandContext(
            "SELECT 1",
            null,
            new TestDbConnection { ConnectionString = connectionString },
            CommandType.Text);
    }

    #endregion

    #region ConnectionString Tests

    [Fact]
    public void ConnectionString_WithSensitiveKey_RedactsValue()
    {
        // Arrange
        var context = CreateContext("Server=localhost;Database=Test;Password=secret123;");

        // Act
        var result = context.ConnectionString;

        // Assert
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("Password", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("localhost", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionString_WithPwdAlias_RedactsValue()
    {
        // Arrange
        var context = CreateContext("Server=localhost;Database=Test;Pwd=secret123;");

        // Act
        var result = context.ConnectionString;

        // Assert
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void ConnectionString_WithNoSensitiveKeys_RoundTripsValues()
    {
        // Arrange
        var context = CreateContext("Server=localhost;Database=Test;");

        // Act
        var result = context.ConnectionString;

        // Assert
        Assert.Contains("localhost", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionString_WhenMalformed_ReturnsUnknown()
    {
        // Arrange - an unterminated quoted value is rejected by DbConnectionStringBuilder's parser
        var context = CreateContext("Server=localhost;Password=\"unterminated;");

        // Act
        var result = context.ConnectionString;

        // Assert
        Assert.Equal("(unknown)", result);
    }

    [Fact]
    public void ConnectionString_WhenEmpty_ReturnsUnknown()
    {
        // Arrange
        var context = CreateContext(string.Empty);

        // Act
        var result = context.ConnectionString;

        // Assert
        Assert.Equal("(unknown)", result);
    }

    #endregion

    #region Test Helper Classes

    private class TestDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";
        public int ConnectionTimeout => 15;
        public string Database => "TestDb";
        public string DataSource => "InMemory";
        public IDbTransaction? Transaction { get; set; }
        public ConnectionState State => ConnectionState.Open;

        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Open() { }
        public void Dispose() { }
    }

    #endregion
}
