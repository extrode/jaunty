using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class DeleteAsyncTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public DeleteAsyncTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        CreateTestTable();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _connection?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void CreateTestTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE bulk_test (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    private BulkTestEntity InsertTestEntity(string name, int value)
    {
        var entity = new BulkTestEntity { Name = name, Value = value };
        entity.Id = _connection.Insert(entity);
        return entity;
    }

    private int GetRowCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntity_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = await _connection.DeleteAsync(entity);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingEntity_ReturnsZero()
    {
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = await _connection.DeleteAsync(entity);

        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task DeleteAsync_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = await _connection.DeleteAsync(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task DeleteAsyncById_ExistingId_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = await _connection.DeleteAsync<BulkTestEntity>((object)entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task DeleteAsyncById_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = await _connection.DeleteAsync<BulkTestEntity>((object)entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }
}
