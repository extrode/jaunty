using System.Data.Common;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

[Collection("Write Operations")]
public class DeleteByEntityIdTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public DeleteByEntityIdTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static EntityTestEntity InsertTestEntity(IDbConnection connection, string name, int value)
    {
        var entity = new BulkTestEntity { Name = name, Value = value };
        entity.Id = connection.Insert(entity);
        return new EntityTestEntity { Id = entity.Id, Name = name, Value = value };
    }

    private static int GetRowCount(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteByEntityId_ExistingId_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        int rows = ctx.Connection.Delete<EntityTestEntity, long>(entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteByEntityId_NonExistingId_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        int rows = ctx.Connection.Delete<EntityTestEntity, long>(99999L);

        Assert.Equal(0, rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteByEntityId_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        using var transaction = ctx.Connection.BeginTransaction();
        int rows = ctx.Connection.Delete<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteByEntityId_WithTransaction_RollbackKeepsOriginal(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        using var transaction = ctx.Connection.BeginTransaction();
        ctx.Connection.Delete<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncByEntityId_ExistingId_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        int rows = await connection.DeleteAsync<EntityTestEntity, long>(entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncByEntityId_NonExistingId_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        int rows = await connection.DeleteAsync<EntityTestEntity, long>(99999L);

        Assert.Equal(0, rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncByEntityId_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        using var transaction = connection.BeginTransaction();
        int rows = await connection.DeleteAsync<EntityTestEntity, long>(entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }
}