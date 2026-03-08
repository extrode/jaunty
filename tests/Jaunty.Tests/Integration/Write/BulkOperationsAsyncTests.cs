
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

public class BulkOperationsAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkOperationsAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static int GetRowCount(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    #region BulkInsertAsync Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_InsertsMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };

        int inserted = await connection.BulkInsertAsync(entities);

        Assert.Equal(3, inserted);
        Assert.Equal(3, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int inserted = await connection.BulkInsertAsync(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_SingleEntity_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Single", Value = 42 }
        };

        int inserted = await connection.BulkInsertAsync(entities);

        Assert.Equal(1, inserted);
        Assert.Equal(1, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_LargeCollection_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = Enumerable.Range(1, 1000)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        int inserted = await connection.BulkInsertAsync(entities);

        Assert.Equal(1000, inserted);
        Assert.Equal(1000, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_CancellationToken_Respects(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = Enumerable.Range(1, 100)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.BulkInsertAsync(entities, cts.Token).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertIgnoreConstraintsAsync_InsertsMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkInsertIgnoreConstraintsAsync(entities).AsTask());
            return;
        }

        int inserted = await connection.BulkInsertIgnoreConstraintsAsync(entities);

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertIgnoreConstraintsAsync_WithOptions_InsertsEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkInsertIgnoreConstraintsAsync(entities).AsTask());
            return;
        }

        using var transaction = connection.BeginTransaction();
        int inserted = await connection.BulkInsertIgnoreConstraintsAsync(entities, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount(connection));
    }

    #endregion

    #region BulkUpdateAsync Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkUpdateAsync_UpdatesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await connection.BulkInsertAsync(entities);

        // Get inserted entities with their IDs
        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        // Update values
        foreach (var entity in inserted)
        {
            entity.Value *= 2;
        }

        int updated = await connection.BulkUpdateAsync(inserted);

        Assert.Equal(3, updated);

        // Verify updates
        var results = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(200, results[0].Value);
        Assert.Equal(400, results[1].Value);
        Assert.Equal(600, results[2].Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkUpdateAsync_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int updated = await connection.BulkUpdateAsync(entities);

        Assert.Equal(0, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkUpdateAsync_NonExistentEntity_ReturnsZeroForThatRow(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert one entity
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };
        await connection.BulkInsertAsync(entities);

        // Try to update a non-existent entity
        var toUpdate = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int updated = await connection.BulkUpdateAsync(toUpdate);

        Assert.Equal(0, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkUpdateIgnoreConstraintsAsync_UpdatesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await connection.BulkInsertAsync(entities);

        // Get inserted entities and update them
        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Name = "Updated" + entity.Id;
        }

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkUpdateIgnoreConstraintsAsync(inserted).AsTask());
            return;
        }

        int updated = await connection.BulkUpdateIgnoreConstraintsAsync(inserted);

        Assert.Equal(2, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkUpdateIgnoreConstraintsAsync_WithOptions_UpdatesEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await connection.BulkInsertAsync(entities);

        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Value *= 10;
        }

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkUpdateIgnoreConstraintsAsync(inserted).AsTask());
            return;
        }

        using var transaction = connection.BeginTransaction();
        int updated = await connection.BulkUpdateIgnoreConstraintsAsync(inserted, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, updated);

        var results = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(1000, results[0].Value);
        Assert.Equal(2000, results[1].Value);
    }

    #endregion

    #region BulkDeleteAsync Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteAsync_DeletesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await connection.BulkInsertAsync(entities);
        Assert.Equal(3, GetRowCount(connection));

        // Get entities to delete
        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = await connection.BulkDeleteAsync(toDelete);

        Assert.Equal(3, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteAsync_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int deleted = await connection.BulkDeleteAsync(entities);

        Assert.Equal(0, deleted);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteAsync_PartialDelete_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };
        await connection.BulkInsertAsync(entities);

        // Delete only some entities
        var all = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        var toDelete = all.Take(2).ToList();

        int deleted = await connection.BulkDeleteAsync(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(1, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteAsync_NonExistentEntity_ReturnsZeroForThatRow(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var toDelete = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int deleted = await connection.BulkDeleteAsync(toDelete);

        Assert.Equal(0, deleted);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteIgnoreConstraintsAsync_DeletesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await connection.BulkInsertAsync(entities);

        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkDeleteIgnoreConstraintsAsync(toDelete).AsTask());
            return;
        }

        int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkDeleteIgnoreConstraintsAsync_WithOptions_DeletesEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        await connection.BulkInsertAsync(entities);

        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => connection.BulkDeleteIgnoreConstraintsAsync(toDelete).AsTask());
            return;
        }

        using var transaction = connection.BeginTransaction();
        int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(toDelete, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    #endregion
}