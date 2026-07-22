using System.Data;
using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteBatchAsyncTests : IClassFixture<DialectFixture>
{
    private const string TableName = "execute_test";
    private readonly DialectFixture _fixture;

    public ExecuteBatchAsyncTests(DialectFixture fixture)
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
    public async Task ExecuteBatchAsync_MultipleInserts_ReturnsCumulativeRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;
        var paramSets = new object[]
        {
            new { Name = "AsyncBatch1", Value = 100 },
            new { Name = "AsyncBatch2", Value = 200 },
            new { Name = "AsyncBatch3", Value = 300 }
        };

        var totalRows = await connection.ExecuteBatchAsync(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
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
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;
        var paramSets = new object[] { };

        var totalRows = await connection.ExecuteBatchAsync(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            paramSets);

        Assert.Equal(0, totalRows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteBatchAsync_WithDefaultCommandOptions_ReturnsCumulativeRows(DialectInfo dialect)
    {
        // AUD-R19 batch-2: default(CommandOptions) zero-initializes CommandType to (CommandType)0
        // rather than running the primary constructor's CommandType.Text default. ExecuteBatchCoreAsync
        // previously set command.CommandType whenever commandType != CommandType.Text, so a
        // default-initialized options struct wrongly assigned an invalid CommandType to the command.
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var connection = (DbConnection)ctx.Connection;
        var paramSets = new object[]
        {
            new { Name = "AsyncDefaultOptions1", Value = 100 },
            new { Name = "AsyncDefaultOptions2", Value = 200 }
        };

        var totalRows = await connection.ExecuteBatchAsync(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            paramSets,
            default(CommandOptions));

        Assert.Equal(2, totalRows);
        Assert.Equal(2, GetRowCount(ctx.Connection));
    }
}
