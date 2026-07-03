using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteAsyncTests(DialectFixture fixture)
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
    public async Task ExecuteAsync_Insert_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var connection = (DbConnection)ctx.Connection;
        var rows = await connection.ExecuteAsync(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            new { Name = "AsyncTest", Value = 100 });

        Assert.Equal(1, rows);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var connection = (DbConnection)ctx.Connection;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.ExecuteAsync(
                "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
                new { Name = "AsyncTest", Value = 100 },
                cts.Token).AsTask());
    }
}
