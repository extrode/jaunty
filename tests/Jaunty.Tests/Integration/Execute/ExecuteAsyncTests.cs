using System.Data;
using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteAsyncTests : IClassFixture<DialectFixture>
{
    private const string TableName = "execute_test";
    private readonly DialectFixture _fixture;

    public ExecuteAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static int GetRowCount(System.Data.IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {TableName}";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteAsync_Insert_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;
        var rows = await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
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
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.ExecuteAsync(
                $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
                new { Name = "AsyncTest", Value = 100 },
                cts.Token).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteAsync_WithDefaultCommandOptions_DoesNotThrow(DialectInfo dialect)
    {
        // Regression: default(CommandOptions) zero-initializes CommandType to 0, which is
        // neither CommandType.Text (1) nor a defined enum member. The async CommandType-setting
        // condition must treat this the same as the sync path (skip setting CommandType unless
        // it's genuinely StoredProcedure/TableDirect) instead of attempting
        // command.CommandType = (CommandType)0, which providers reject at the ADO.NET level.
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;

        var rows = await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            new { Name = "DefaultOptionsTest", Value = 200 },
            default(CommandOptions));

        Assert.Equal(1, rows);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }
}
