using System.Data.Common;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

[Collection("Write Operations")]
public class UpdateTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public UpdateTests(DialectFixture fixture)
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
    [SqlServer]
    [Postgres]
    [MariaDB]
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
    [SqlServer]
    [Postgres]
    [MariaDB]
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
    [SqlServer]
    [Postgres]
    [MariaDB]
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
    [SqlServer]
    [Postgres]
    [MariaDB]
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_SameValues_StillReturnsRowsMatched(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = InsertTestEntity(ctx.Connection, "Original", 100);

        // Update with the same values
        int rows = ctx.Connection.Update(entity);

        Assert.Equal(1, rows);
        Assert.Equal("Original", GetNameById(ctx.Connection, entity.Id));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        BulkTestEntity nullEntity = null!;
        Assert.Throws<ArgumentNullException>(() => ctx.Connection.Update(nullEntity));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpdateAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Name = "Original", Value = 100 };
        long id = connection.Insert(entity);

        entity.Id = id;
        entity.Name = "UpdatedWithCancellationToken";
        using var cts = new CancellationTokenSource();

        int rows = await connection.UpdateAsync(entity, cts.Token);

        Assert.Equal(1, rows);
        var result = connection.QueryFirst<BulkTestEntity>(
            "SELECT id AS Id, name AS Name, value AS Value FROM bulk_test WHERE id = @Id",
            new { Id = id });
        Assert.Equal("UpdatedWithCancellationToken", result.Name);
    }
}