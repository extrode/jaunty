using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteTests(DialectFixture fixture)
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
    public void Execute_Insert_ReturnsRowsAffected(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var rows = ctx.Connection.Execute(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
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
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var entity = new BulkTestEntity { Name = "Original", Value = 100 };
        entity.Id = ctx.Connection.Insert(entity);

        var rows = ctx.Connection.Execute(
            "UPDATE bulk_test SET value = @Value WHERE id = @Id",
            new { Value = 200, Id = entity.Id });

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
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var entity = new BulkTestEntity { Name = "ToDelete", Value = 100 };
        entity.Id = ctx.Connection.Insert(entity);

        var rows = ctx.Connection.Execute(
            "DELETE FROM bulk_test WHERE id = @Id",
            new { Id = entity.Id });

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
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        using var tx = ctx.Connection.BeginTransaction();
        
        var rows = ctx.Connection.Execute(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            new { Name = "Rollback", Value = 100 },
            CommandOptions.WithTransaction(tx));

        Assert.Equal(1, rows);
        tx.Rollback();
        
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }
}
