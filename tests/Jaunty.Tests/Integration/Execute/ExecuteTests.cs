using System.Data;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteTests : IClassFixture<DialectFixture>
{
    private const string TableName = "execute_test";
    private readonly DialectFixture _fixture;

    public ExecuteTests(DialectFixture fixture)
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
    public void Execute_Insert_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var rows = ctx.Connection.Execute(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            new { Name = "Test", Value = 100 });

        Assert.Equal(1, rows);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Execute_Update_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        ctx.Connection.Execute(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            new { Name = "Original", Value = 100 });

        using var getIdCmd = ctx.Connection.CreateCommand();
        getIdCmd.CommandText = $"SELECT id FROM {TableName} WHERE name = 'Original'";
        var id = Convert.ToInt64(getIdCmd.ExecuteScalar());

        var rows = ctx.Connection.Execute(
            $"UPDATE {TableName} SET value = @Value WHERE id = @Id",
            new { Value = 200, Id = id });

        Assert.Equal(1, rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Execute_Delete_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        ctx.Connection.Execute(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            new { Name = "ToDelete", Value = 100 });

        using var getIdCmd = ctx.Connection.CreateCommand();
        getIdCmd.CommandText = $"SELECT id FROM {TableName} WHERE name = 'ToDelete'";
        var id = Convert.ToInt64(getIdCmd.ExecuteScalar());

        var rows = ctx.Connection.Execute(
            $"DELETE FROM {TableName} WHERE id = @Id",
            new { Id = id });

        Assert.Equal(1, rows);
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Execute_WithTransaction_RollbackKeepsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        using var tx = ctx.Connection.BeginTransaction();

        var rows = ctx.Connection.Execute(
            $"INSERT INTO {TableName} (name, value) VALUES (@Name, @Value)",
            new { Name = "Rollback", Value = 100 },
            CommandOptions.WithTransaction(tx));

        Assert.Equal(1, rows);
        tx.Rollback();

        Assert.Equal(0, GetRowCount(ctx.Connection));
    }
}
