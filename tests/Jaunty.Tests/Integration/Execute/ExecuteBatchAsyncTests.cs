using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteBatchAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteBatchAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static int GetRowCount(System.Data.IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void ClearTestTable(System.Data.IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bulk_test";
        cmd.ExecuteNonQuery();
    }


    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteBatchAsync_MultipleInserts_ReturnsCumulativeRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var connection = (DbConnection)ctx.Connection;
        var paramSets = new object[]
        {
            new { Name = "AsyncBatch1", Value = 100 },
            new { Name = "AsyncBatch2", Value = 200 },
            new { Name = "AsyncBatch3", Value = 300 }
        };

        var totalRows = await connection.ExecuteBatchAsync(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            paramSets);

        Assert.Equal(3, totalRows);
        Assert.Equal(3, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteBatchAsync_WithEmptyList_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var connection = (DbConnection)ctx.Connection;
        var paramSets = new object[] { };

        var totalRows = await connection.ExecuteBatchAsync(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            paramSets);

        Assert.Equal(0, totalRows);
    }
}
