using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class InsertAsyncTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public InsertAsyncTests()
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

    private int GetRowCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public async Task InsertAsync_SingleEntity_ReturnsIdentity()
    {
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = await _connection.InsertAsync(entity);

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public async Task InsertAsync_MultipleEntities_ReturnsIncrementingIds()
    {
        var entity1 = new BulkTestEntity { Name = "Test1", Value = 100 };
        var entity2 = new BulkTestEntity { Name = "Test2", Value = 200 };

        long id1 = await _connection.InsertAsync(entity1);
        long id2 = await _connection.InsertAsync(entity2);

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public async Task InsertAsync_WithCommandOptions_Works()
    {
        using var transaction = _connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = await _connection.InsertAsync(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount());
    }
}
