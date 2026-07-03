using System.Data;
using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Get;

[Collection("Get Operations")]
public class GetAllTests : IClassFixture<DialectFixture>
{
    private const string TableName = "get_test";
    private readonly DialectFixture _fixture;

    public GetAllTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void InsertRows(IDbConnection connection, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO {TableName} (name, value) VALUES (@name, @value)";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = $"Row{i}"; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = i * 10; cmd.Parameters.Add(p2);
            cmd.ExecuteNonQuery();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetAll_PopulatedTable_ReturnsAllRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 3);

        var rows = ctx.Connection.GetAll<GetTestEntity>();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.True(r.Id > 0);
            Assert.NotEmpty(r.Name);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetAll_EmptyTable_ReturnsEmptyList(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);

        var rows = ctx.Connection.GetAll<GetTestEntity>();

        Assert.Empty(rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllAsync_PopulatedTable_ReturnsAllRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 4);
        var dbConn = (DbConnection)ctx.Connection;

        var rows = await dbConn.GetAllAsync<GetTestEntity>();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.True(r.Id > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllAsync_EmptyTable_ReturnsEmptyList(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var dbConn = (DbConnection)ctx.Connection;

        var rows = await dbConn.GetAllAsync<GetTestEntity>();

        Assert.Empty(rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetAllStream_PopulatedTable_YieldsAllRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 3);

        var rows = ctx.Connection.GetAllStream<GetTestEntity>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.Id > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetAllStream_EmptyTable_YieldsNothing(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);

        var rows = ctx.Connection.GetAllStream<GetTestEntity>().ToList();

        Assert.Empty(rows);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetAll_WithTransaction_ReadsWithinTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 2);
        using var tx = ctx.Connection.BeginTransaction();

        var rows = ctx.Connection.GetAll<GetTestEntity>(CommandOptions<GetTestEntity>.WithTransaction(tx));

        Assert.Equal(2, rows.Count);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 2);
        var dbConn = (DbConnection)ctx.Connection;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var rows = await dbConn.GetAllAsync<GetTestEntity>(cts.Token);

        Assert.Equal(2, rows.Count);
    }
}
