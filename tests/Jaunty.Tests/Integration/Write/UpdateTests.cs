using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Write;

public class UpdateTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public UpdateTests()
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

    private string? GetNameById(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM bulk_test WHERE id = @Id";
        var param = cmd.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = id;
        cmd.Parameters.Add(param);
        return cmd.ExecuteScalar()?.ToString();
    }

    [Fact]
    public void Update_ExistingEntity_ReturnsRowsAffected()
    {
        var entity = InsertTestEntity("Original", 100);
        entity.Name = "Updated";
        entity.Value = 200;

        int rows = Jaunty.Update(_connection, entity);

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(entity.Id));
    }

    [Fact]
    public void Update_NonExistingEntity_ReturnsZero()
    {
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = Jaunty.Update(_connection, entity);

        Assert.Equal(0, rows);
    }

    [Fact]
    public void Update_WithCommandOptions_Works()
    {
        var entity = InsertTestEntity("Original", 100);
        entity.Name = "Updated";

        using var transaction = _connection.BeginTransaction();
        int rows = Jaunty.Update(_connection, entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(entity.Id));
    }

    [Fact]
    public void Update_WithTransaction_RollbackKeepsOriginal()
    {
        var entity = InsertTestEntity("Original", 100);
        entity.Name = "Updated";

        using var transaction = _connection.BeginTransaction();
        Jaunty.Update(_connection, entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal("Original", GetNameById(entity.Id));
    }
}

