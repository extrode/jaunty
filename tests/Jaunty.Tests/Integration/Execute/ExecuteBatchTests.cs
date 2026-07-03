using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Execute;

[Collection("Execute Operations")]
public class ExecuteBatchTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteBatchTests(DialectFixture fixture)
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
    public void ExecuteBatch_MultipleInserts_ReturnsCumulativeRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var paramSets = new object[]
        {
            new { Name = "Test1", Value = 100 },
            new { Name = "Test2", Value = 200 },
            new { Name = "Test3", Value = 300 }
        };

        var totalRows = ctx.Connection.ExecuteBatch(
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
    public void ExecuteBatch_WithEmptyList_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        var paramSets = new object[] { };

        var totalRows = ctx.Connection.ExecuteBatch(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            paramSets);

        Assert.Equal(0, totalRows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteBatch_WithTransaction_RollbackUndoesAll(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        ClearTestTable(ctx.Connection);
        using var tx = ctx.Connection.BeginTransaction();
        var paramSets = new object[]
        {
            new { Name = "Batch1", Value = 100 },
            new { Name = "Batch2", Value = 200 }
        };

        var totalRows = ctx.Connection.ExecuteBatch(
            "INSERT INTO bulk_test (name, value) VALUES (@Name, @Value)",
            paramSets,
            CommandOptions.WithTransaction(tx));

        Assert.Equal(2, totalRows);
        tx.Rollback();
        
        Assert.Equal(0, GetRowCount(ctx.Connection));
    }
}
