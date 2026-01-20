using System.Data;
using System.Data.SQLite;
using FluentAssertions;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Write;

public class UpsertTests : IDisposable
{
    private readonly Database _db;
    private readonly SQLiteConnection _sqliteConn;

    public UpsertTests()
    {
        _db = new Database();
        _sqliteConn = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
    }

    public void Dispose()
    {
        _db.Dispose();
        _sqliteConn.Dispose();
    }

    private void ExecuteSql(string sql, object? parameters = null)
    {
        if (_db.Connection.State == ConnectionState.Closed)
            _db.Connection.Open();

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Upsert_NewEntity_InsertsRecord()
    {
        // Get max category ID first
        var maxId = _db.Connection.QueryScalar<long>("SELECT MAX(category_id) FROM categories");
        var newId = (int)(maxId + 100);

        var category = new Category
        {
            CategoryId = newId,
            CategoryName = "UpsertTest",
            Description = "Test category for upsert"
        };

        // Upsert should insert the new record
        var result = _db.Connection.Upsert(category);

        result.Should().Be(1);

        // Verify it was inserted
        var inserted = _db.Connection.QueryPartialFirstOrDefault<Category>(
            "SELECT * FROM categories WHERE category_id = @id", new { id = newId });

        inserted.Should().NotBeNull();
        inserted!.CategoryName.Should().Be("UpsertTest");

        // Cleanup
        ExecuteSql("DELETE FROM categories WHERE category_id = @id", new { id = newId });
    }

    [Fact]
    public void Upsert_ExistingEntity_UpdatesRecord()
    {
        // First insert a record
        var maxId = _db.Connection.QueryScalar<long>("SELECT MAX(category_id) FROM categories");
        var testId = (int)(maxId + 101);

        ExecuteSql(
            "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
            new { id = testId, name = "OriginalName", desc = "Original description" });

        // Now upsert with the same ID but different data
        var category = new Category
        {
            CategoryId = testId,
            CategoryName = "UpdatedName",
            Description = "Updated description"
        };

        var result = _db.Connection.Upsert(category);

        result.Should().Be(1);

        // Verify it was updated
        var updated = _db.Connection.QueryPartialFirstOrDefault<Category>(
            "SELECT * FROM categories WHERE category_id = @id", new { id = testId });

        updated.Should().NotBeNull();
        updated!.CategoryName.Should().Be("UpdatedName");
        updated.Description.Should().Be("Updated description");

        // Cleanup
        ExecuteSql("DELETE FROM categories WHERE category_id = @id", new { id = testId });
    }

    [Fact]
    public async Task UpsertAsync_NewEntity_InsertsRecord()
    {
        var maxId = _db.Connection.QueryScalar<long>("SELECT MAX(category_id) FROM categories");
        var newId = (int)(maxId + 102);

        var category = new Category
        {
            CategoryId = newId,
            CategoryName = "AsyncUpsertTest",
            Description = "Async test category"
        };

        var result = await _sqliteConn.UpsertAsync(category);

        result.Should().Be(1);

        // Verify
        var inserted = _db.Connection.QueryPartialFirstOrDefault<Category>(
            "SELECT * FROM categories WHERE category_id = @id", new { id = newId });

        inserted.Should().NotBeNull();
        inserted!.CategoryName.Should().Be("AsyncUpsertTest");

        // Cleanup
        ExecuteSql("DELETE FROM categories WHERE category_id = @id", new { id = newId });
    }

    [Fact]
    public async Task UpsertAsync_ExistingEntity_UpdatesRecord()
    {
        var maxId = _db.Connection.QueryScalar<long>("SELECT MAX(category_id) FROM categories");
        var testId = (int)(maxId + 103);

        ExecuteSql(
            "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
            new { id = testId, name = "AsyncOriginal", desc = "Async original" });

        var category = new Category
        {
            CategoryId = testId,
            CategoryName = "AsyncUpdated",
            Description = "Async updated"
        };

        var result = await _sqliteConn.UpsertAsync(category);

        result.Should().Be(1);

        // Verify
        var updated = _db.Connection.QueryPartialFirstOrDefault<Category>(
            "SELECT * FROM categories WHERE category_id = @id", new { id = testId });

        updated.Should().NotBeNull();
        updated!.CategoryName.Should().Be("AsyncUpdated");

        // Cleanup
        ExecuteSql("DELETE FROM categories WHERE category_id = @id", new { id = testId });
    }
}
