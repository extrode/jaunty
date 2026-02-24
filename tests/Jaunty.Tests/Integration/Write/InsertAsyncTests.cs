using System.Data.Common;

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

    private static int GetRowCount(System.Data.IDbConnection connection)
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
}
