using System.Data;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

public class DeleteTests : IClassFixture<WriteDialectFixture>
{
    private readonly WriteDialectFixture _fixture;

    public DeleteTests(WriteDialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static BulkTestEntity InsertTestEntity(IDbConnection connection, string name, int value)
    {
        var entity = new BulkTestEntity { Name = name, Value = value };
        entity.Id = connection.Insert(entity);
        return entity;
    }

    private static int GetRowCount(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_ExistingEntity_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        int rows = ctx.Connection.Delete(entity);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_NonExistingEntity_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = ctx.Connection.Delete(entity);

        Assert.Equal(0, rows);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        using var transaction = ctx.Connection.BeginTransaction();
        int rows = ctx.Connection.Delete(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_WithTransaction_RollbackKeepsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        using var transaction = ctx.Connection.BeginTransaction();
        ctx.Connection.Delete(entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_ExistingId_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        int rows = ctx.Connection.Delete<BulkTestEntity>((object)entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_NonExistingId_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        int rows = ctx.Connection.Delete<BulkTestEntity>((object)99999L);

        Assert.Equal(0, rows);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Test1", 100);

        using var transaction = ctx.Connection.BeginTransaction();
        int rows = ctx.Connection.Delete<BulkTestEntity>((object)entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_MultipleEntities_DeletesEach(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity1 = InsertTestEntity(ctx.Connection, "Test1", 100);
        var entity2 = InsertTestEntity(ctx.Connection, "Test2", 200);
        _ = InsertTestEntity(ctx.Connection, "Test3", 300);

        ctx.Connection.Delete(entity1);
        ctx.Connection.Delete(entity2);

        Assert.Equal(1, GetRowCount(ctx.Connection));
    }
}
