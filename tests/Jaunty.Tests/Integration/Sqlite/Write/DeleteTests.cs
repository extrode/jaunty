using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class DeleteTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public DeleteTests()
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

    #region Delete By Entity

    [Fact]
    public void Delete_ExistingEntity_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = _connection.Delete(entity);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void Delete_NonExistingEntity_ReturnsZero()
    {
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = _connection.Delete(entity);

        Assert.Equal(0, rows);
    }

    [Fact]
    public void Delete_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = _connection.Delete(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void Delete_WithTransaction_RollbackKeepsEntity()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        _connection.Delete(entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(1, GetRowCount());
    }

    #endregion

    #region Delete By ID (object)

    [Fact]
    public void DeleteById_ExistingId_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Test1", 100);

        int rows = _connection.Delete<BulkTestEntity>((object)entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void DeleteById_NonExistingId_ReturnsZero()
    {
        int rows = _connection.Delete<BulkTestEntity>((object)99999L);

        Assert.Equal(0, rows);
    }

    [Fact]
    public void DeleteById_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Test1", 100);

        using var transaction = _connection.BeginTransaction();
        int rows = _connection.Delete<BulkTestEntity>((object)entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount());
    }

    #endregion

    #region Multiple Deletes

    [Fact]
    public void Delete_MultipleEntities_DeletesEach()
    {
        var entity1 = InsertTestEntity("Test1", 100);
        var entity2 = InsertTestEntity("Test2", 200);
        var entity3 = InsertTestEntity("Test3", 300);

        _connection.Delete(entity1);
        _connection.Delete(entity2);

        Assert.Equal(1, GetRowCount());
    }

    #endregion
}
