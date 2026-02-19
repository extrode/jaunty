using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class BulkOperationsTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public BulkOperationsTests()
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

    private void ClearTestTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bulk_test";
        cmd.ExecuteNonQuery();
    }

    private int GetRowCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    #region BulkInsert Tests

    [Fact]
    public void BulkInsert_InsertsMultipleEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };

        int inserted = _connection.BulkInsert(entities);

        Assert.Equal(3, inserted);
        Assert.Equal(3, GetRowCount());
    }

    [Fact]
    public void BulkInsert_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int inserted = _connection.BulkInsert(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void BulkInsert_SingleEntity_Works()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Single", Value = 42 }
        };

        int inserted = _connection.BulkInsert(entities);

        Assert.Equal(1, inserted);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public void BulkInsert_LargeCollection_Works()
    {
        var entities = Enumerable.Range(1, 1000)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        int inserted = _connection.BulkInsert(entities);

        Assert.Equal(1000, inserted);
        Assert.Equal(1000, GetRowCount());
    }

    [Fact]
    public void BulkInsert_NullConnection_Throws()
    {
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkInsert(entities));
    }

    [Fact]
    public void BulkInsert_NullEntities_Throws()
    {
        IEnumerable<BulkTestEntity>? nullEntities = null;

        Assert.Throws<ArgumentNullException>(() => _connection.BulkInsert(nullEntities!));
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_InsertsMultipleEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        int inserted = _connection.BulkInsertIgnoreConstraints(entities);

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_WithCommandOptions_Works()
    {
        using var transaction = _connection.BeginTransaction();
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        int inserted = _connection.BulkInsertIgnoreConstraints(entities, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int inserted = _connection.BulkInsertIgnoreConstraints(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount());
    }

    #endregion

    #region BulkUpdate Tests

    [Fact]
    public void BulkUpdate_UpdatesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        _connection.BulkInsert(entities);

        // Get inserted entities with their IDs
        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        // Update values
        foreach (var entity in inserted)
        {
            entity.Value *= 2;
        }

        int updated = _connection.BulkUpdate(inserted);

        Assert.Equal(3, updated);

        // Verify updates
        var results = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(200, results[0].Value);
        Assert.Equal(400, results[1].Value);
        Assert.Equal(600, results[2].Value);
    }

    [Fact]
    public void BulkUpdate_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int updated = _connection.BulkUpdate(entities);

        Assert.Equal(0, updated);
    }

    [Fact]
    public void BulkUpdate_NonExistentEntity_ReturnsZeroForThatRow()
    {
        // Insert one entity
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };
        _connection.BulkInsert(entities);

        // Try to update a non-existent entity
        var toUpdate = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int updated = _connection.BulkUpdate(toUpdate);

        Assert.Equal(0, updated);
    }

    [Fact]
    public void BulkUpdate_NullConnection_Throws()
    {
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Id = 1, Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkUpdate(entities));
    }

    [Fact]
    public void BulkUpdateIgnoreConstraints_UpdatesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        _connection.BulkInsert(entities);

        // Get inserted entities and update them
        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Name = "Updated" + entity.Id;
        }

        int updated = _connection.BulkUpdateIgnoreConstraints(inserted);

        Assert.Equal(2, updated);
    }

    [Fact]
    public void BulkUpdateIgnoreConstraints_WithCommandOptions_Works()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        _connection.BulkInsert(entities);

        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Value *= 10;
        }

        using var transaction = _connection.BeginTransaction();
        int updated = _connection.BulkUpdateIgnoreConstraints(inserted, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, updated);
        var results = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(1000, results[0].Value);
        Assert.Equal(2000, results[1].Value);
    }

    [Fact]
    public void BulkUpdateIgnoreConstraints_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int updated = _connection.BulkUpdateIgnoreConstraints(entities);

        Assert.Equal(0, updated);
    }

    #endregion

    #region BulkDelete Tests

    [Fact]
    public void BulkDelete_DeletesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        _connection.BulkInsert(entities);
        Assert.Equal(3, GetRowCount());

        // Get entities to delete
        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = _connection.BulkDelete(toDelete);

        Assert.Equal(3, deleted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void BulkDelete_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int deleted = _connection.BulkDelete(entities);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public void BulkDelete_PartialDelete_Works()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        _connection.BulkInsert(entities);

        // Delete only some entities
        var all = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        var toDelete = all.Take(2).ToList();

        int deleted = _connection.BulkDelete(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public void BulkDelete_NonExistentEntity_ReturnsZeroForThatRow()
    {
        var toDelete = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int deleted = _connection.BulkDelete(toDelete);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public void BulkDelete_NullConnection_Throws()
    {
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Id = 1, Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkDelete(entities));
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_DeletesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        _connection.BulkInsert(entities);

        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = _connection.BulkDeleteIgnoreConstraints(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_WithCommandOptions_Works()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        _connection.BulkInsert(entities);

        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        using var transaction = _connection.BeginTransaction();
        int deleted = _connection.BulkDeleteIgnoreConstraints(toDelete, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int deleted = _connection.BulkDeleteIgnoreConstraints(entities);

        Assert.Equal(0, deleted);
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public void BulkInsert_WithTransaction_CommitsOnSuccess()
    {
        using var transaction = _connection.BeginTransaction();

        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        var options = new CommandOptions(transaction: transaction);
        int inserted = _connection.BulkInsert(entities, options);
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public void BulkInsert_WithTransaction_RollsBackOnError()
    {
        using var transaction = _connection.BeginTransaction();

        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };

        var options = new CommandOptions(transaction: transaction);
        _connection.BulkInsert(entities, options);

        // Rollback instead of commit
        transaction.Rollback();

        Assert.Equal(0, GetRowCount());
    }

    #endregion
}
