#if NET8_0_OR_GREATER
using Microsoft.Data.Sqlite;

using Jaunty;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.MicrosoftSqlite;

/// <summary>
/// Integration tests using Microsoft.Data.Sqlite provider.
/// Verifies that Jaunty works correctly with Microsoft.Data.Sqlite.SqliteConnection,
/// which has a different type name ("SqliteConnection") than System.Data.SQLite's "SQLiteConnection".
/// This exercises the SqlDialectFactory bug fix that adds "SqliteConnection" matching.
/// </summary>
public class MicrosoftSqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MicrosoftSqliteTests()
    {
        _connection = new SqliteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region Sync Query Tests

    [Fact]
    public void Query_ReturnsResults()
    {
        var categories = _connection.QueryPartial<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public void Query_WithParameters_FiltersCorrectly()
    {
        var categories = _connection.QueryPartial<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Fact]
    public void QueryFirst_ReturnsFirst()
    {
        var category = _connection.QueryPartialFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public void QuerySingle_ReturnsSingle()
    {
        var category = _connection.QueryPartialSingle<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public void QueryScalar_ReturnsValue()
    {
        var count = _connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM categories");

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_ReturnsValue()
    {
        var count = _connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM categories");

        Assert.True(count > 0);
    }

    #endregion

    #region Async Query Tests

    [Fact]
    public async Task QueryAsync_ReturnsResults()
    {
        var categories = await _connection.QueryPartialAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public async Task QueryFirstAsync_ReturnsFirst()
    {
        var category = await _connection.QueryPartialFirstAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public async Task QuerySingleAsync_ReturnsSingle()
    {
        var category = await _connection.QueryPartialSingleAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public async Task QueryScalarAsync_ReturnsValue()
    {
        var count = await _connection.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM categories");

        Assert.True(count > 0);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Insert_And_Delete_Works()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );";
        cmd.ExecuteNonQuery();

        var category = new Category
        {
            CategoryName = "Test",
            Description = "Test Description"
        };

        var insertResult = conn.Insert(category);
        Assert.Equal(1, insertResult);

        var inserted = conn.QueryPartialFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");
        Assert.Equal("Test", inserted.CategoryName);

        var deleteResult = conn.Delete(inserted);
        Assert.Equal(1, deleteResult);
    }

    [Fact]
    public void Upsert_Works()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );";
        cmd.ExecuteNonQuery();

        var category = new Category
        {
            CategoryId = 1,
            CategoryName = "Test",
            Description = "Test"
        };

        var result = conn.Upsert(category);
        Assert.Equal(1, result);

        // Upsert again with updated values
        category.CategoryName = "Updated";
        var result2 = conn.Upsert(category);
        Assert.True(result2 >= 1);

        var fetched = conn.QueryPartialSingle<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });
        Assert.Equal("Updated", fetched.CategoryName);
    }

    #endregion

    #region Streaming Tests

    [Fact]
    public void QueryStream_YieldsResults()
    {
        var count = 0;
        foreach (var category in _connection.QueryPartialStream<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3"))
        {
            Assert.True(category.CategoryId > 0);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryStreamAsync_YieldsResults()
    {
        var categories = new List<Category>();
        await foreach (var category in _connection.QueryPartialStreamAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3"))
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    #endregion
}
#endif
