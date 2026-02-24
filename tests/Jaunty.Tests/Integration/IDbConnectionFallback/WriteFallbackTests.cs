using System.Data.SQLite;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests write operations (Insert, Update, Delete, Upsert) through the IDbConnection fallback path.
/// Uses an in-memory SQLite database so writes don't affect the Northwind test data.
/// </summary>
public class WriteFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public WriteFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=:memory:");
        _realConnection.Open();
        SeedSchema();
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    private void SeedSchema()
    {
        using var cmd = _realConnection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );
            INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks');
            INSERT INTO categories (category_name, description) VALUES ('Condiments', 'Sauces and spreads');";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Insert_ViaWrapper_InsertsRow()
    {
        var category = new Category
        {
            CategoryName = "Test Category",
            Description = "Test Description"
        };

        var result = _wrapper.Insert(category);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Update_ViaWrapper_UpdatesRow()
    {
        var category = new Category
        {
            CategoryId = 1,
            CategoryName = "Updated Beverages",
            Description = "Updated description"
        };

        var result = _wrapper.Update(category);

        Assert.Equal(1, result);

        // Verify the update
        var updated = _wrapper.QueryPartialSingle<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Equal("Updated Beverages", updated.CategoryName);
    }

    [Fact]
    public void Delete_ViaWrapper_DeletesRow()
    {
        // Insert a category to delete
        var category = new Category
        {
            CategoryName = "To Delete",
            Description = "Will be deleted"
        };
        _wrapper.Insert(category);

        // Get the inserted category
        var inserted = _wrapper.QueryPartialFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_name = @Name",
            new { Name = "To Delete" });

        var result = _wrapper.Delete(inserted);

        Assert.Equal(1, result);
    }

    // Note: Upsert is not tested via wrapper because SqlDialectFactory resolves
    // the dialect from the connection type name. IDbConnectionWrapper's type name
    // doesn't match any provider, so it defaults to SQL Server dialect which generates
    // MERGE syntax instead of SQLite's ON CONFLICT syntax.
}
