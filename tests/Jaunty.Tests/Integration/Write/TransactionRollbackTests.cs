using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for transaction rollback behavior.
/// </summary>
public class TransactionRollbackTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public TransactionRollbackTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithTransactionRollback_NoDataInserted(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            // SQLite in-memory test
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? (IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
                createCmd.ExecuteNonQuery();
            }

            var newEntities = new List<BulkTestEntity>
            {
                new() { Name = "Rollback Test 1", Value = 1 },
                new() { Name = "Rollback Test 2", Value = 2 }
            };

            using var transaction = connection.BeginTransaction();
            connection.BulkInsert(newEntities, CommandOptions<BulkTestEntity>.WithTransaction(transaction));
            transaction.Rollback();

            var count = connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM bulk_test WHERE name LIKE 'Rollback Test%'");

            Assert.Equal(0, count);
        }
        else
        {
            // For other databases, use existing bulk_test table
            using var connection = _fixture.GetWriteContext(dialect);

            var newEntities = new List<BulkTestEntity>
            {
                new() { Name = "Rollback Test 1", Value = 1 }
            };

            using var transaction = connection.Connection.BeginTransaction();
            connection.Connection.BulkInsert(newEntities, CommandOptions<BulkTestEntity>.WithTransaction(transaction));
            transaction.Rollback();

            var count = connection.Connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM bulk_test WHERE name = @Name",
                new { Name = "Rollback Test 1" });

            Assert.Equal(0, count);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_WithTransactionRollback_NoDataInserted(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            // SQLite in-memory test
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? (IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE categories (category_id INTEGER PRIMARY KEY AUTOINCREMENT, category_name TEXT, description TEXT)";
                createCmd.ExecuteNonQuery();
            }

            var newCategory = new Category { CategoryName = "Rollback Insert Test", Description = "Test" };

            using var transaction = connection.BeginTransaction();
            connection.Insert(newCategory, CommandOptions<Category>.WithTransaction(transaction));
            transaction.Rollback();

            var count = connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM categories WHERE category_name = @Name",
                new { Name = "Rollback Insert Test" });

            Assert.Equal(0, count);
        }
        else
        {
            // For other databases, verify transaction rollback works
            using var connection = _fixture.GetConnection(dialect);
            using var transaction = connection.BeginTransaction();

            var newCategory = new Category { CategoryName = "Rollback Test", Description = "Test" };
            connection.Insert(newCategory, CommandOptions<Category>.WithTransaction(transaction));
            transaction.Rollback();

            var count = connection.QueryScalar<long>(
                dialect.Provider == DialectProvider.SqlServer
                    ? "SELECT COUNT(*) FROM Categories WHERE CategoryName = @Name"
                    : "SELECT COUNT(*) FROM categories WHERE category_name = @Name",
                new { Name = "Rollback Test" });

            Assert.Equal(0, count);
        }
    }
}
