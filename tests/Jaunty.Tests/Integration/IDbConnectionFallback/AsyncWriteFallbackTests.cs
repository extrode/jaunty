using System.Data.SQLite;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests that async write operations correctly reject non-DbConnection wrappers.
/// The async public API requires DbConnection and throws InvalidOperationException
/// for IDbConnection-only implementations.
/// </summary>
public class AsyncWriteFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public AsyncWriteFallbackTests()
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
            INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks');";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task InsertAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var category = new Category { CategoryName = "Test", Description = "Test" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.InsertAsync(category).AsTask());
    }

    [Fact]
    public async Task UpdateAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var category = new Category { CategoryId = 1, CategoryName = "Updated", Description = "Updated" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.UpdateAsync(category).AsTask());
    }

    [Fact]
    public async Task DeleteAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var category = new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Soft drinks" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.DeleteAsync(category).AsTask());
    }

    [Fact]
    public async Task UpsertAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var category = new Category { CategoryId = 1, CategoryName = "Test", Description = "Test" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.UpsertAsync(category).AsTask());
    }
}
