using System.Data.Common;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

[Collection("Write Operations")]
public class DeleteAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public DeleteAsyncTests(DialectFixture fixture)
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
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_ExistingEntity_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        int rows = await connection.DeleteAsync(entity);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_NonExistingEntity_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Id = 99999, Name = "DoesNotExist", Value = 0 };

        int rows = await connection.DeleteAsync(entity);

        Assert.Equal(0, rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        using var transaction = connection.BeginTransaction();
        int rows = await connection.DeleteAsync(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncById_ExistingId_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        int rows = await connection.DeleteAsync<BulkTestEntity>((object)entity.Id);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncById_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);

        using var transaction = connection.BeginTransaction();
        int rows = await connection.DeleteAsync<BulkTestEntity>((object)entity.Id, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        BulkTestEntity nullEntity = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await ctx.Connection.DeleteAsync(nullEntity));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncById_NullId_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        object nullId = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await ctx.Connection.DeleteAsync<BulkTestEntity>(nullId));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);
        using var cts = new CancellationTokenSource();

        int rows = await connection.DeleteAsync(entity, cts.Token);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsyncById_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = InsertTestEntity(connection, "Test1", 100);
        using var cts = new CancellationTokenSource();

        int rows = await connection.DeleteAsync<BulkTestEntity>((object)entity.Id, cts.Token);

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(connection));
    }
#endif
}