using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class InsertTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public InsertTests()
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
    public void Insert_SingleEntity_ReturnsIdentity()
    {
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = _connection.Insert(entity);

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public void Insert_MultipleEntities_ReturnsIncrementingIds()
    {
        var entity1 = new BulkTestEntity { Name = "Test1", Value = 100 };
        var entity2 = new BulkTestEntity { Name = "Test2", Value = 200 };

        long id1 = _connection.Insert(entity1);
        long id2 = _connection.Insert(entity2);

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public void Insert_WithCommandOptions_Works()
    {
        using var transaction = _connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = _connection.Insert(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public void Insert_WithTransaction_RollbackDiscardsData()
    {
        using var transaction = _connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        _connection.Insert(entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(0, GetRowCount());
    }
}
