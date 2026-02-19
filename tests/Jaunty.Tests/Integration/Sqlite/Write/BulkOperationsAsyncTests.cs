using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class BulkOperationsAsyncTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public BulkOperationsAsyncTests()
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

    #region BulkInsertAsync Tests

    [Fact]
    public async Task BulkInsertAsync_InsertsMultipleEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };

        int inserted = await _connection.BulkInsertAsync(entities);

        Assert.Equal(3, inserted);
        Assert.Equal(3, GetRowCount());
    }

    [Fact]
    public async Task BulkInsertAsync_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int inserted = await _connection.BulkInsertAsync(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task BulkInsertAsync_SingleEntity_Works()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Single", Value = 42 }
        };

        int inserted = await _connection.BulkInsertAsync(entities);

        Assert.Equal(1, inserted);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public async Task BulkInsertAsync_LargeCollection_Works()
    {
        var entities = Enumerable.Range(1, 1000)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        int inserted = await _connection.BulkInsertAsync(entities);

        Assert.Equal(1000, inserted);
        Assert.Equal(1000, GetRowCount());
    }

    [Fact]
    public async Task BulkInsertAsync_CancellationToken_Respects()
    {
        var entities = Enumerable.Range(1, 100)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _connection.BulkInsertAsync(entities, cts.Token).AsTask());
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_InsertsMultipleEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        int inserted = await _connection.BulkInsertIgnoreConstraintsAsync(entities);

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount());
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_WithOptions_InsertsEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        using var transaction = _connection.BeginTransaction();
        int inserted = await _connection.BulkInsertIgnoreConstraintsAsync(entities, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount());
    }

    #endregion

    #region BulkUpdateAsync Tests

    [Fact]
    public async Task BulkUpdateAsync_UpdatesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await _connection.BulkInsertAsync(entities);

        // Get inserted entities with their IDs
        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        // Update values
        foreach (var entity in inserted)
        {
            entity.Value *= 2;
        }

        int updated = await _connection.BulkUpdateAsync(inserted);

        Assert.Equal(3, updated);

        // Verify updates
        var results = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(200, results[0].Value);
        Assert.Equal(400, results[1].Value);
        Assert.Equal(600, results[2].Value);
    }

    [Fact]
    public async Task BulkUpdateAsync_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int updated = await _connection.BulkUpdateAsync(entities);

        Assert.Equal(0, updated);
    }

    [Fact]
    public async Task BulkUpdateAsync_NonExistentEntity_ReturnsZeroForThatRow()
    {
        // Insert one entity
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };
        await _connection.BulkInsertAsync(entities);

        // Try to update a non-existent entity
        var toUpdate = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int updated = await _connection.BulkUpdateAsync(toUpdate);

        Assert.Equal(0, updated);
    }

    [Fact]
    public async Task BulkUpdateIgnoreConstraintsAsync_UpdatesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await _connection.BulkInsertAsync(entities);

        // Get inserted entities and update them
        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Name = "Updated" + entity.Id;
        }

        int updated = await _connection.BulkUpdateIgnoreConstraintsAsync(inserted);

        Assert.Equal(2, updated);
    }

    [Fact]
    public async Task BulkUpdateIgnoreConstraintsAsync_WithOptions_UpdatesEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await _connection.BulkInsertAsync(entities);

        var inserted = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Value *= 10;
        }

        using var transaction = _connection.BeginTransaction();
        int updated = await _connection.BulkUpdateIgnoreConstraintsAsync(inserted, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, updated);

        var results = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(1000, results[0].Value);
        Assert.Equal(2000, results[1].Value);
    }

    #endregion

    #region BulkDeleteAsync Tests

    [Fact]
    public async Task BulkDeleteAsync_DeletesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await _connection.BulkInsertAsync(entities);
        Assert.Equal(3, GetRowCount());

        // Get entities to delete
        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = await _connection.BulkDeleteAsync(toDelete);

        Assert.Equal(3, deleted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task BulkDeleteAsync_EmptyCollection_ReturnsZero()
    {
        var entities = new List<BulkTestEntity>();

        int deleted = await _connection.BulkDeleteAsync(entities);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task BulkDeleteAsync_PartialDelete_Works()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await _connection.BulkInsertAsync(entities);

        // Delete only some entities
        var all = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        var toDelete = all.Take(2).ToList();

        int deleted = await _connection.BulkDeleteAsync(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public async Task BulkDeleteAsync_NonExistentEntity_ReturnsZeroForThatRow()
    {
        var toDelete = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int deleted = await _connection.BulkDeleteAsync(toDelete);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task BulkDeleteIgnoreConstraintsAsync_DeletesMultipleEntities()
    {
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await _connection.BulkInsertAsync(entities);

        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = await _connection.BulkDeleteIgnoreConstraintsAsync(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount());
    }

    [Fact]
    public async Task BulkDeleteIgnoreConstraintsAsync_WithOptions_DeletesEntities()
    {
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await _connection.BulkInsertAsync(entities);

        var toDelete = _connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        using var transaction = _connection.BeginTransaction();
        int deleted = await _connection.BulkDeleteIgnoreConstraintsAsync(toDelete, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount());
    }

    #endregion
}

