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


    [Theory]
    [InlineData("Server=localhost;Database=Test;Access Token=tok-abc123;", "tok-abc123")]
    [InlineData("Server=localhost;Database=Test;Client Secret=sec-abc123;", "sec-abc123")]
    [InlineData("Server=localhost;Database=Test;ApiKey=key-abc123;", "key-abc123")]
    [InlineData("Server=localhost;Database=Test;Passfile=/home/u/.pgpass;", ".pgpass")]
    [InlineData("Server=localhost;Database=Test;SSL Password=ssl-abc123;", "ssl-abc123")]
    [InlineData("Server=localhost;Database=Test;User Password=up-abc123;", "up-abc123")]
    public void ConnectionString_WithCredentialBearingKey_RedactsValue(string connectionString, string secret)
    {
        var context = CreateContext(connectionString);

        var result = context.ConnectionString;

        Assert.DoesNotContain(secret, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("localhost", result, StringComparison.OrdinalIgnoreCase);
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

    #region AUD-R35-166 and AUD-R35-167: DatabaseName, ProviderName and the constructor guards

    private static CommandContext CreateContext(IDbConnection connection) =>
        new("SELECT 1", null, connection, CommandType.Text);

    [Fact]
    public void DatabaseName_ComesFromTheConnection()
    {
        Assert.Equal("TestDb", CreateContext(new TestDbConnection()).DatabaseName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DatabaseName_FallsBackWhenTheProviderHasNothingToSay(string? database)
    {
        var context = CreateContext(new HostileDbConnection { DatabaseValue = database });

        Assert.Equal("(unknown)", context.DatabaseName);
    }

    [Fact]
    public void DatabaseName_SurvivesAProviderWhoseGetterThrows()
    {
        var context = CreateContext(new HostileDbConnection { ThrowFromDatabase = true });

        Assert.Equal("(unknown)", context.DatabaseName);
    }

    [Fact]
    public void ConnectionString_SurvivesAProviderWhoseGetterThrows()
    {
        var context = CreateContext(new HostileDbConnection { ThrowFromConnectionString = true });

        Assert.Equal("(unknown)", context.ConnectionString);
    }

    [Fact]
    public void ProviderName_IsTheConnectionTypeName()
    {
        Assert.Equal(nameof(TestDbConnection), CreateContext(new TestDbConnection()).ProviderName);
    }

    [Fact]
    public void TheConstructorRejectsNulls()
    {
        var connection = new TestDbConnection();

        Assert.Throws<ArgumentNullException>("commandText",
            () => new CommandContext(null!, null, connection, CommandType.Text));
        Assert.Throws<ArgumentNullException>("connection",
            () => new CommandContext("SELECT 1", null, null!, CommandType.Text));
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

    private sealed class HostileDbConnection : IDbConnection
    {
        public bool ThrowFromConnectionString { get; set; }

        public bool ThrowFromDatabase { get; set; }

        public string? DatabaseValue { get; set; }

        public string ConnectionString
        {
            get => ThrowFromConnectionString
                ? throw new ObjectDisposedException(nameof(HostileDbConnection))
                : "Data Source=:memory:";
            set { }
        }

        public int ConnectionTimeout => 15;
        public string Database => ThrowFromDatabase
            ? throw new ObjectDisposedException(nameof(HostileDbConnection))
            : DatabaseValue!;
        public ConnectionState State => ConnectionState.Closed;

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
