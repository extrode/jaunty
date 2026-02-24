using System.Data.Common;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

public class UpdateAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public UpdateAsyncTests(DialectFixture fixture)
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
    public async Task UpdateAsync_ExistingEntity_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Original", 100);
        entity.Name = "Updated";
        entity.Value = 200;

        int rows = await connection.UpdateAsync(entity);

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(connection, entity.Id));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpdateAsync_NonExistingEntity_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = await connection.UpdateAsync(entity);

        Assert.Equal(0, rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpdateAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Original", 100);
        entity.Name = "Updated";

        using var transaction = connection.BeginTransaction();
        int rows = await connection.UpdateAsync(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal("Updated", GetNameById(connection, entity.Id));
    }
}
