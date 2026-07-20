using System.Data;

using Jaunty.Dialects;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Unit tests for <see cref="SqlDialectFactory"/> connection-to-dialect resolution and
/// registration/cache-invalidation behavior, plus dialect-generation edge cases that are
/// unique to specific dialect instances and are not already covered by the canonical dialect
/// test suite in Unit/Dialects/SqlDialectTests.cs.
/// </summary>
[Collection("Dialect Factory State")]
public class SqlDialectFactoryTests
{
    [Fact]
    public void GetDialect_SQLiteConnection_ReturnsSQLiteDialect()
    {
        var connection = new SQLiteConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<SQLiteDialect>(dialect);
    }

    [Fact]
    public void GetDialect_SqlConnection_ReturnsSqlServerDialect()
    {
        var connection = new SqlConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<SqlServerDialect>(dialect);
    }

    [Fact]
    public void GetDialect_NpgsqlConnection_ReturnsPostgreSqlDialect()
    {
        var connection = new NpgsqlConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<PostgreSqlDialect>(dialect);
    }

    [Fact]
    public void GetDialect_MySqlConnection_ReturnsMySqlDialect()
    {
        var connection = new MySqlConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<MySqlDialect>(dialect);
    }

    [Fact]
    public void GetDialect_SqliteConnection_MicrosoftDataSqlite_ReturnsSQLiteDialect()
    {
        var connection = new SqliteConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<SQLiteDialect>(dialect);
    }

    [Fact]
    public void GetDialect_UnknownConnection_DefaultsToSqlServer()
    {
        var connection = new UnknownConnection();
        var dialect = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<SqlServerDialect>(dialect);
    }

    [Fact]
    public void RegisterDialect_ByName_InvalidatesAlreadyCachedResolutionForThatType()
    {
        // Regression: a connection type resolved (and cached) via GetDialect BEFORE
        // RegisterDialect(string, ISqlDialect) is called for that same type name must pick
        // up the newly registered dialect on the next GetDialect call, not keep returning
        // the stale built-in dialect from the cache.
        var connection = new CacheInvalidationConnection();

        // Prime the cache with the default (SQL Server) resolution.
        var before = SqlDialectFactory.GetDialect(connection);
        Assert.IsType<SqlServerDialect>(before);

        var custom = new PostgreSqlDialect();
        SqlDialectFactory.RegisterDialect(nameof(CacheInvalidationConnection), custom);

        var after = SqlDialectFactory.GetDialect(connection);
        Assert.Same(custom, after);
    }

    // Mock connection classes whose type names match the factory's switch cases
    private class SQLiteConnection : MockConnectionBase { }
    private class SqliteConnection : MockConnectionBase { } // Microsoft.Data.Sqlite uses this name
    private class SqlConnection : MockConnectionBase { }
    private class NpgsqlConnection : MockConnectionBase { }
    private class MySqlConnection : MockConnectionBase { }
    private class UnknownConnection : MockConnectionBase { }
    private class CacheInvalidationConnection : MockConnectionBase { }

    private abstract class MockConnectionBase : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotImplementedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotImplementedException();
        public void Dispose() { }
        public void Open() { }
    }

    #region Dialect-Specific Edge Cases Not Covered By Unit/Dialects/SqlDialectTests.cs

    public class SQLiteDialectEdgeCaseTests
    {
        private readonly SQLiteDialect _dialect = new();

        [Fact]
        public void EscapeTableName_SchemaIgnored()
        {
            // SQLite doesn't support schemas, so schema is ignored
            Assert.Equal("products", _dialect.EscapeTableName("myschema", "products"));
        }

        [Fact]
        public void IsKeyword_CaseInsensitive()
        {
            Assert.True(_dialect.IsKeyword("select"));
            Assert.True(_dialect.IsKeyword("SELECT"));
            Assert.True(_dialect.IsKeyword("Select"));
        }

        [Fact]
        public void GenerateOverClause_AllCombinations()
        {
            // Both null - note leading space
            var result1 = _dialect.GenerateOverClause(null, null);
            Assert.Equal(" OVER ()", result1);

            // Partition only
            var result2 = _dialect.GenerateOverClause(new[] { "col1" }, null);
            Assert.Contains("PARTITION BY", result2);

            // Order only
            var result3 = _dialect.GenerateOverClause(null, new[] { ("col1", false) });
            Assert.Contains("ORDER BY", result3);

            // Both
            var result4 = _dialect.GenerateOverClause(new[] { "col1" }, new[] { ("col2", true) });
            Assert.Contains("PARTITION BY", result4);
            Assert.Contains("ORDER BY", result4);
            Assert.Contains("DESC", result4);
        }

        [Fact]
        public void GetPagingSql_EdgeCases()
        {
            // Zero offset
            var result1 = _dialect.GetPagingSql("SELECT * FROM t", 0, 10);
            Assert.Equal("SELECT * FROM t LIMIT 10 OFFSET 0", result1);

            // Zero fetchNext
            var result2 = _dialect.GetPagingSql("SELECT * FROM t", 10, 0);
            Assert.Equal("SELECT * FROM t LIMIT 0 OFFSET 10", result2);
        }
    }

    public class SqlServerDialectEdgeCaseTests
    {
        private readonly SqlServerDialect _dialect = new();

        [Fact]
        public void EscapeTableName_KeywordSchema()
        {
            Assert.Equal("[USER].products", _dialect.EscapeTableName("USER", "products"));
        }

        [Fact]
        public void GenerateOverClause_AllCombinations()
        {
            // Both null - note leading space
            var result1 = _dialect.GenerateOverClause(null, null);
            Assert.Equal(" OVER ()", result1);

            // Partition only
            var result2 = _dialect.GenerateOverClause(new[] { "col1" }, null);
            Assert.Contains("PARTITION BY", result2);

            // Order only ascending
            var result3 = _dialect.GenerateOverClause(null, new[] { ("col1", false) });
            Assert.Contains("ORDER BY", result3);
            Assert.DoesNotContain("DESC", result3);

            // Order only descending
            var result4 = _dialect.GenerateOverClause(null, new[] { ("col1", true) });
            Assert.Contains("ORDER BY", result4);
            Assert.Contains("DESC", result4);
        }

        [Fact]
        public void GetPagingSql_EdgeCases()
        {
            // Zero offset
            var result1 = _dialect.GetPagingSql("SELECT * FROM t", 0, 10);
            Assert.Equal("SELECT * FROM t ORDER BY (SELECT NULL) OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY", result1);

            // Zero fetchNext
            var result2 = _dialect.GetPagingSql("SELECT * FROM t", 10, 0);
            Assert.Equal("SELECT * FROM t ORDER BY (SELECT NULL) OFFSET 10 ROWS FETCH NEXT 0 ROWS ONLY", result2);
        }
    }

    public class PostgreSqlDialectEdgeCaseTests
    {
        private readonly PostgreSqlDialect _dialect = new();

        [Fact]
        public void GetPagingSql_EdgeCases()
        {
            // Zero offset
            var result1 = _dialect.GetPagingSql("SELECT * FROM t", 0, 10);
            Assert.Equal("SELECT * FROM t LIMIT 10 OFFSET 0", result1);

            // Zero fetchNext
            var result2 = _dialect.GetPagingSql("SELECT * FROM t", 10, 0);
            Assert.Equal("SELECT * FROM t LIMIT 0 OFFSET 10", result2);
        }
    }

    public class MySqlDialectEdgeCaseTests
    {
        private readonly MySqlDialect _dialect = new();

        [Fact]
        public void GenerateOverClause_PartitionOnly_NoOrder()
        {
            var result = _dialect.GenerateOverClause(
                new[] { "department" },
                null);
            Assert.Equal(" OVER (PARTITION BY department)", result);
        }

        [Fact]
        public void GenerateOverClause_OrderOnly_NoPartition()
        {
            var result = _dialect.GenerateOverClause(
                null,
                new[] { ("salary", true) });
            Assert.Equal(" OVER (ORDER BY salary DESC)", result);
        }

        [Fact]
        public void GenerateOverClause_PartitionAndOrder()
        {
            var result = _dialect.GenerateOverClause(
                new[] { "department" },
                new[] { ("salary", false) });
            Assert.Equal(" OVER (PARTITION BY department ORDER BY salary)", result);
        }

        [Fact]
        public void GetPagingSql_EdgeCases()
        {
            // Zero offset
            var result1 = _dialect.GetPagingSql("SELECT * FROM t", 0, 10);
            Assert.Equal("SELECT * FROM t LIMIT 0, 10", result1);

            // Zero fetchNext
            var result2 = _dialect.GetPagingSql("SELECT * FROM t", 10, 0);
            Assert.Equal("SELECT * FROM t LIMIT 10, 0", result2);
        }
    }

    #endregion
}
