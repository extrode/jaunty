using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for empty collection handling in bulk operations.
/// </summary>
public class BulkEmptyCollectionTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkEmptyCollectionTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
                createCmd.ExecuteNonQuery();
            }

            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.BulkInsert(emptyEntities);

            Assert.Equal(0, result);
        }
        else
        {
            using var connection = _fixture.GetWriteContext(dialect);
            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.Connection.BulkInsert(emptyEntities);
            Assert.Equal(0, result);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdate_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? (IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
                createCmd.ExecuteNonQuery();
            }

            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.BulkUpdate(emptyEntities);

            Assert.Equal(0, result);
        }
        else
        {
            using var connection = _fixture.GetWriteContext(dialect);
            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.Connection.BulkUpdate(emptyEntities);
            Assert.Equal(0, result);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? (IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
                createCmd.ExecuteNonQuery();
            }

            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.BulkDelete(emptyEntities);

            Assert.Equal(0, result);
        }
        else
        {
            using var connection = _fixture.GetWriteContext(dialect);
            var emptyEntities = new List<BulkTestEntity>();
            var result = connection.Connection.BulkDelete(emptyEntities);
            Assert.Equal(0, result);
        }
    }
}