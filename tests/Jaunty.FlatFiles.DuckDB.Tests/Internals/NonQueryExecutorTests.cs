using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;

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

    // ==========================================
    // CommandOptions (transaction/timeout) overloads (AUD-R9)
    // ==========================================

    [Fact]
    public void Execute_WithTransactionOptions_RollbackUndoesInsert()
    {
        using var tx = _connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 5 },
            new() { Value = "RolledBack" }
        };

        var result = NonQueryExecutor.Execute(_connection, sql, parameters, options);
        Assert.Equal(1, result);

        tx.Rollback();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM test WHERE id = 5";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransactionOptions_RollbackUndoesInsert()
    {
        using var tx = _connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 6 },
            new() { Value = "RolledBackAsync" }
        };

        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, parameters, options, default);
        Assert.Equal(1, result);

        tx.Rollback();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM test WHERE id = 6";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(0, count);
    }

    [Fact]
    public void Execute_WithTimeoutOptions_DoesNotThrowAndStillExecutes()
    {
        var options = CommandOptions.WithTimeout(30);
        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 7 },
            new() { Value = "WithTimeout" }
        };

        var result = NonQueryExecutor.Execute(_connection, sql, parameters, options);

        Assert.Equal(1, result);
    }

    // ==========================================
    // Transaction guard (AUD-R11)
    // ==========================================

    // ApplyOptions assigned options.Transaction to cmd via IDbCommand.Transaction unconditionally.
    // DuckDBCommand's own Transaction setter (reached via the base DbCommand path) casts internally,
    // so a non-DbTransaction IDbTransaction threw an opaque InvalidCastException instead of Jaunty's
    // clear ArgumentException.

    [Fact]
    public void Execute_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);
        var options = CommandOptions.WithTransaction(nonDbTransaction);

        var sql = "INSERT INTO test (id, name) VALUES ($1, $2)";
        var parameters = new List<DuckDBParameter>
        {
            new() { Value = 8 },
            new() { Value = "ShouldNotInsert" }
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            NonQueryExecutor.Execute(_connection, sql, parameters, options));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }
}