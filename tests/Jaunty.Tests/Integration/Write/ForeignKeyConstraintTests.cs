using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for foreign key constraint handling and BulkInsertIgnoreConstraints.
/// </summary>
[Collection("Write Operations")]
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

        // Category 999 doesn't exist in the seed data, so exactly nothing should be deleted;
        // a regression that deleted unrelated rows (e.g. a broken WHERE clause) would return
        // a non-zero count instead.
        Assert.Equal(0, result);
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
        // [MicrosoftSqlite]/[SystemSqlite] are the only dialects this theory runs against, so
        // dialect.Provider is always one of the two - no SqlServer/Postgres/MariaDB branch needed.
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
}