using System.Data;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

public class UpdateTests : IClassFixture<WriteDialectFixture>
{
    private readonly WriteDialectFixture _fixture;

    public UpdateTests(WriteDialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static BulkTestEntity InsertTestEntity(IDbConnection connection, string name, int value)
    {
        var entity = new BulkTestEntity { Name = name, Value = value };
        entity.Id = connection.Insert(entity);
        return entity;
    }

    private static string? GetNameById(IDbConnection connection, long id)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM bulk_test WHERE id = @Id";
        var param = cmd.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = id;
        cmd.Parameters.Add(param);
        return cmd.ExecuteScalar()?.ToString();
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_ExistingEntity_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Original", 100);
        entity.Name = "Updated";
        entity.Value = 200;

        int rows = ctx.Connection.Update(entity);

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(ctx.Connection, entity.Id));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_NonExistingEntity_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = ctx.Connection.Update(entity);

        Assert.Equal(0, rows);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Original", 100);
        entity.Name = "Updated";

        using var transaction = ctx.Connection.BeginTransaction();
        int rows = ctx.Connection.Update(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(ctx.Connection, entity.Id));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_WithTransaction_RollbackKeepsOriginal(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Original", 100);
        entity.Name = "Updated";

        using var transaction = ctx.Connection.BeginTransaction();
        ctx.Connection.Update(entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal("Original", GetNameById(ctx.Connection, entity.Id));
    }
}
