using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for foreign key constraint handling and BulkInsertIgnoreConstraints.
/// </summary>
public class ForeignKeyConstraintTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ForeignKeyConstraintTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// SQLite supports FK toggle via PRAGMA. Other databases handle constraints differently.
    /// </summary>
    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDeleteIgnoreConstraints_DoesNotThrow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = new List<Category> { new() { CategoryId = 999 } };

        // Should not throw even with non-existent category
        var result = connection.BulkDeleteIgnoreConstraints(categories);

        // May not delete anything, but should not throw
        Assert.True(result >= 0);
    }

    /// <summary>
    /// BulkInsertIgnoreConstraints works with in-memory SQLite.
    /// For other databases, tests basic bulk insert functionality.
    /// </summary>
    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsertIgnoreConstraints_WithValidData_InsertsSuccessfully(DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SystemSqlite || dialect.Provider == DialectProvider.MicrosoftSqlite)
        {
            // SQLite in-memory test
            using var connection = dialect.Provider == DialectProvider.SystemSqlite
                ? (IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
                : (IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            connection.Open();

            // Create test table
            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
                createCmd.ExecuteNonQuery();
            }

            var newEntities = new List<BulkTestEntity>
            {
                new() { Name = "Test Entity 1", Value = 1 },
                new() { Name = "Test Entity 2", Value = 2 }
            };

            var result = connection.BulkInsertIgnoreConstraints(newEntities);

            // Should insert all entities
            Assert.Equal(newEntities.Count, result);
        }
        else
        {
            // For SQL Server, Postgres, MariaDB - just verify the method doesn't throw
            using var connection = _fixture.GetConnection(dialect);

            var newEntities = new List<BulkTestEntity>
            {
                new() { Name = "Test Entity 1", Value = 1 }
            };

            // May fail due to schema, but should not throw for the method itself
            var ex = Record.Exception(() => connection.BulkInsertIgnoreConstraints(newEntities));

            // If it throws, it should be about schema, not the method
            if (ex != null)
            {
                Assert.Contains("bulk_test", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}