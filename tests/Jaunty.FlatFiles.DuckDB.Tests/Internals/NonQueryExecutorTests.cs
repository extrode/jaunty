using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class NonQueryExecutorTests : IDisposable
{
    private readonly DuckDBConnection _connection;

    public NonQueryExecutorTests()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test (id INTEGER, name TEXT)";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void Execute_InsertsRow_ReturnsRowCount()
    {
        // Arrange
        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 1 },
            new() { Value = "Test" }
        };

        // Act
        var result = NonQueryExecutor.Execute(_connection, sql, parameters);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_InsertsRow_ReturnsRowCount()
    {
        // Arrange
        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 2 },
            new() { Value = "Test2" }
        };

        // Act
        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, parameters, default);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_UpdatesRows_ReturnsRowCount()
    {
        // Arrange - insert first
        var insertSql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        NonQueryExecutor.Execute(_connection, insertSql, new List<DuckDBParameter>
        {
            new() { Value = 3 },
            new() { Value = "Original" }
        });

        // Act
        var updateSql = "UPDATE test SET name = $1 WHERE id = $2";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = "Updated" },
            new() { Value = 3 }
        };
        var result = NonQueryExecutor.Execute(_connection, updateSql, parameters);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_DeletesRows_ReturnsRowCount()
    {
        // Arrange - insert first
        var insertSql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        NonQueryExecutor.Execute(_connection, insertSql, new List<DuckDBParameter>
        {
            new() { Value = 4 },
            new() { Value = "ToDelete" }
        });

        // Act
        var deleteSql = "DELETE FROM test WHERE id = $1";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 4 }
        };
        var result = NonQueryExecutor.Execute(_connection, deleteSql, parameters);

        // Assert
        Assert.Equal(1, result);
    }
}