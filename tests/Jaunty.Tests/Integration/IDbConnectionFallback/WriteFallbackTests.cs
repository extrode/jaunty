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

    /// <summary>
    /// AUD-R26 (batch 4). This case used to be excluded, with a note explaining why:
    /// <c>SqlDialectFactory</c> resolved the dialect from the connection type name,
    /// <c>IDbConnectionWrapper</c>'s name matched no provider, and the silent fallback handed it SQL
    /// Server's dialect - which generates <c>MERGE</c> rather than SQLite's <c>ON CONFLICT</c>.
    ///
    /// <para>
    /// That note is the finding in miniature. The suite's other three operations "worked" through
    /// the wrapper only because INSERT, UPDATE and DELETE happen to look similar across the two
    /// engines; Upsert is where the dialects diverge, so it was the one that had to be left out.
    /// Nothing failed - the coverage just quietly stopped at the point where the wrong dialect
    /// would have shown.
    /// </para>
    ///
    /// <para>
    /// <c>GetDialect</c> now looks through connection decorators, so the wrapper resolves to
    /// SQLite's dialect and this works. It is the positive half of the fix: the throw proves Jaunty
    /// stops guessing, and this proves it stopped guessing by getting it right rather than by
    /// refusing everything.
    /// </para>
    /// </summary>
    [Fact]
    public void Upsert_ViaWrapper_UsesTheWrappedConnectionsDialect()
    {
        var existing = _wrapper.QueryFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description " +
            "FROM categories WHERE category_name = @Name",
            new { Name = "Beverages" });

        existing.Description = "Updated through the wrapper";

        _wrapper.Upsert(existing);

        var reloaded = _wrapper.QueryFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description " +
            "FROM categories WHERE category_id = @Id",
            new { Id = existing.CategoryId });

        Assert.Equal("Updated through the wrapper", reloaded.Description);
    }
}