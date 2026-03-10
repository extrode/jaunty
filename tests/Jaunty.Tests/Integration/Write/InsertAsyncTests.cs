using System.Data.Common;
using System.Threading;
using System;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

public class InsertAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public InsertAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
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
    public async Task InsertAsync_SingleEntity_ReturnsIdentity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = await connection.InsertAsync(entity);

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_MultipleEntities_ReturnsIncrementingIds(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity1 = new BulkTestEntity { Name = "Test1", Value = 100 };
        var entity2 = new BulkTestEntity { Name = "Test2", Value = 200 };

        long id1 = await connection.InsertAsync(entity1);
        long id2 = await connection.InsertAsync(entity2);

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
        Assert.Equal(2, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        using var transaction = connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = await connection.InsertAsync(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_ExplicitlyProvidedId_IsIgnored(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Id = 999, Name = "ProvidedIdTest", Value = 123 };

        long id = await connection.InsertAsync(entity);

        Assert.NotEqual(999, id);
        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));

        var insertedEntity = connection.QueryFirst<BulkTestEntity>(
            "SELECT id AS Id, name AS Name, value AS Value FROM bulk_test WHERE id = @Id",
            new { Id = id });

        Assert.Equal(id, insertedEntity.Id);
        Assert.Equal("ProvidedIdTest", insertedEntity.Name);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await connection.InsertAsync<BulkTestEntity>(null!));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_WithTimeout_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = (DbConnection)ctx.Connection;
        var entity = new BulkTestEntity { Name = "TestTimeout", Value = 500 };

        long id = await connection.InsertAsync(entity, CommandOptions<BulkTestEntity>.WithTimeout(10));

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }
#endif
}
