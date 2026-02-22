using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Write;

public class DeleteByEntityIdTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public DeleteByEntityIdTests()
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

    private EntityTestEntity InsertTestEntity(string name, int value)
    {
        var entity = new BulkTestEntity { Name = name, Value = value };
        entity.Id = _connection.Insert(entity);
        return new EntityTestEntity { Id = entity.Id, Name = name, Value = value };
    }

    private int GetRowCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public void DeleteByEntityId_ExistingId_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = _connection.Delete<EntityTestEntity, long>(entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void DeleteByEntityId_NonExistingId_ReturnsZero()
    {
        int rows = _connection.Delete<EntityTestEntity, long>(99999L);

        Assert.Equal(0, rows);
    }

    [Fact]
    public void DeleteByEntityId_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = _connection.Delete<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void DeleteByEntityId_WithTransaction_RollbackKeepsOriginal()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        _connection.Delete<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public async Task DeleteAsyncByEntityId_ExistingId_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = await _connection.DeleteAsync<EntityTestEntity, long>(entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task DeleteAsyncByEntityId_NonExistingId_ReturnsZero()
    {
        int rows = await _connection.DeleteAsync<EntityTestEntity, long>(99999L);

        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task DeleteAsyncByEntityId_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = await _connection.DeleteAsync<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }
}

