using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

[Collection("Write Operations")]
public class BulkOperationsTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkOperationsTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static int GetRowCount(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    #region BulkInsert Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_InsertsMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 },
            new() { Name = "Test3", Value = 300 }
        };

        int inserted = connection.BulkInsert(entities);

        Assert.Equal(3, inserted);
        Assert.Equal(3, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_IEntityImplementation_PopulatesIdsViaLoopPath(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // A single-entity list always routes through the loop-based insert path (the multi-row
        // path requires entityList.Count > 1), which is the only BulkInsert path that can
        // populate identity values back onto entities (see BulkInsert.cs's BulkInsertLoop remarks).
        var entities = new List<IEntityTestEntity>
        {
            new() { Name = "IdPopulationTest", Value = 42 }
        };

        int inserted = connection.BulkInsert(entities);

        Assert.Equal(1, inserted);
        Assert.NotEqual(0, entities[0].Id);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int inserted = connection.BulkInsert(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_SingleEntity_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Single", Value = 42 }
        };

        int inserted = connection.BulkInsert(entities);

        Assert.Equal(1, inserted);
        Assert.Equal(1, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_LargeCollection_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = Enumerable.Range(1, 1000)
            .Select(i => new BulkTestEntity { Name = $"Item{i}", Value = i })
            .ToList();

        int inserted = connection.BulkInsert(entities);

        Assert.Equal(1000, inserted);
        Assert.Equal(1000, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_NullConnection_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkInsert(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_NullEntities_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        IEnumerable<BulkTestEntity>? nullEntities = null;

        Assert.Throws<ArgumentNullException>(() => connection.BulkInsert(nullEntities!));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsertIgnoreConstraints_InsertsMultipleEntities(DialectInfo dialect)
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
            Assert.Throws<NotSupportedException>(() => connection.BulkInsertIgnoreConstraints(entities));
            return;
        }

        int inserted = connection.BulkInsertIgnoreConstraints(entities);

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsertIgnoreConstraints_WithCommandOptions_Works(DialectInfo dialect)
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
            Assert.Throws<NotSupportedException>(() => connection.BulkInsertIgnoreConstraints(entities));
            return;
        }

        using var transaction = connection.BeginTransaction();

        if (dialect.Provider is DialectProvider.SystemSqlite or DialectProvider.MicrosoftSqlite)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkInsertIgnoreConstraints(entities, new CommandOptions(transaction: transaction)));
            return;
        }

        int inserted = connection.BulkInsertIgnoreConstraints(entities, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsertIgnoreConstraints_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int inserted = connection.BulkInsertIgnoreConstraints(entities);

        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount(connection));
    }

    #endregion

    #region BulkUpdate Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdate_UpdatesMultipleEntities(DialectInfo dialect)
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
        connection.BulkInsert(entities);

        // Get inserted entities with their IDs
        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        // Update values
        foreach (var entity in inserted)
        {
            entity.Value *= 2;
        }

        int updated = connection.BulkUpdate(inserted);

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
    public void BulkUpdate_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int updated = connection.BulkUpdate(entities);

        Assert.Equal(0, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdate_NonExistentEntity_ReturnsZeroForThatRow(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert one entity
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };
        connection.BulkInsert(entities);

        // Try to update a non-existent entity
        var toUpdate = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int updated = connection.BulkUpdate(toUpdate);

        Assert.Equal(0, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdate_NullConnection_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Id = 1, Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkUpdate(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdateIgnoreConstraints_UpdatesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        connection.BulkInsert(entities);

        // Get inserted entities and update them
        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Name = "Updated" + entity.Id;
        }

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkUpdateIgnoreConstraints(inserted));
            return;
        }

        int updated = connection.BulkUpdateIgnoreConstraints(inserted);

        Assert.Equal(2, updated);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdateIgnoreConstraints_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        connection.BulkInsert(entities);

        var inserted = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");
        foreach (var entity in inserted)
        {
            entity.Value *= 10;
        }

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkUpdateIgnoreConstraints(inserted));
            return;
        }

        using var transaction = connection.BeginTransaction();

        if (dialect.Provider is DialectProvider.SystemSqlite or DialectProvider.MicrosoftSqlite)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkUpdateIgnoreConstraints(inserted, new CommandOptions(transaction: transaction)));
            return;
        }

        int updated = connection.BulkUpdateIgnoreConstraints(inserted, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, updated);
        var results = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        Assert.Equal(1000, results[0].Value);
        Assert.Equal(2000, results[1].Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkUpdateIgnoreConstraints_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int updated = connection.BulkUpdateIgnoreConstraints(entities);

        Assert.Equal(0, updated);
    }

    #endregion

    #region BulkDelete Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_DeletesMultipleEntities(DialectInfo dialect)
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
        connection.BulkInsert(entities);
        Assert.Equal(3, GetRowCount(connection));

        // Get entities to delete
        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        int deleted = connection.BulkDelete(toDelete);

        Assert.Equal(3, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int deleted = connection.BulkDelete(entities);

        Assert.Equal(0, deleted);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_PartialDelete_Works(DialectInfo dialect)
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
        connection.BulkInsert(entities);

        // Delete only some entities
        var all = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test ORDER BY id");
        var toDelete = all.Take(2).ToList();

        int deleted = connection.BulkDelete(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(1, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_NonExistentEntity_ReturnsZeroForThatRow(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var toDelete = new List<BulkTestEntity>
        {
            new() { Id = 999, Name = "NonExistent", Value = 999 }
        };

        int deleted = connection.BulkDelete(toDelete);

        Assert.Equal(0, deleted);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDelete_NullConnection_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity> { new() { Id = 1, Name = "Test", Value = 1 } };

        Assert.Throws<ArgumentNullException>(() => nullConnection!.BulkDelete(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDeleteIgnoreConstraints_DeletesMultipleEntities(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        // Insert initial data
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        connection.BulkInsert(entities);

        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkDeleteIgnoreConstraints(toDelete));
            return;
        }

        int deleted = connection.BulkDeleteIgnoreConstraints(toDelete);

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDeleteIgnoreConstraints_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };
        connection.BulkInsert(entities);

        var toDelete = connection.Query<BulkTestEntity>("SELECT id AS Id, name AS Name, value AS Value FROM bulk_test");

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkDeleteIgnoreConstraints(toDelete));
            return;
        }

        using var transaction = connection.BeginTransaction();

        if (dialect.Provider is DialectProvider.SystemSqlite or DialectProvider.MicrosoftSqlite)
        {
            Assert.Throws<NotSupportedException>(() => connection.BulkDeleteIgnoreConstraints(toDelete, new CommandOptions(transaction: transaction)));
            return;
        }

        int deleted = connection.BulkDeleteIgnoreConstraints(toDelete, new CommandOptions(transaction: transaction));
        transaction.Commit();

        Assert.Equal(2, deleted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkDeleteIgnoreConstraints_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        var entities = new List<BulkTestEntity>();

        int deleted = connection.BulkDeleteIgnoreConstraints(entities);

        Assert.Equal(0, deleted);
    }

    #endregion

    #region Transaction Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithTransaction_CommitsOnSuccess(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        using var transaction = connection.BeginTransaction();

        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 },
            new() { Name = "Test2", Value = 200 }
        };

        var options = new CommandOptions(transaction: transaction);
        int inserted = connection.BulkInsert(entities, options);
        transaction.Commit();

        Assert.Equal(2, inserted);
        Assert.Equal(2, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithTransaction_RollsBackOnError(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        using var transaction = connection.BeginTransaction();

        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Test1", Value = 100 }
        };

        var options = new CommandOptions(transaction: transaction);
        connection.BulkInsert(entities, options);

        // Rollback instead of commit
        transaction.Rollback();

        Assert.Equal(0, GetRowCount(connection));
    }

    #endregion
}