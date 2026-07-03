#if ASYNC_ENUMERABLE_SUPPORT
using System.Data;
using System.Data.Common;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Get;

[Collection("Get Operations")]
public class GetAllStreamAsyncTests : IClassFixture<DialectFixture>
{
    private const string TableName = "get_test";
    private readonly DialectFixture _fixture;

    public GetAllStreamAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void InsertRows(IDbConnection connection, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO {TableName} (name, value) VALUES (@name, @value)";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = $"Stream{i}"; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = i; cmd.Parameters.Add(p2);
            cmd.ExecuteNonQuery();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_PopulatedTable_YieldsAllRows(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 3);
        var dbConn = (DbConnection)ctx.Connection;

        var list = new List<GetTestEntity>();
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
            list.Add(row);

        Assert.Equal(3, list.Count);
        Assert.All(list, r => Assert.True(r.Id > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_EmptyTable_YieldsNothing(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var dbConn = (DbConnection)ctx.Connection;

        var list = new List<GetTestEntity>();
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
            list.Add(row);

        Assert.Empty(list);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_LazyEnumeration_StopsEarly(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        var count = 0;
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
        {
            Assert.True(row.Id > 0);
            count++;
            if (count >= 2) break;
        }

        Assert.Equal(2, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_WithCancellation_ThrowsOrCompletes(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 3);
        var dbConn = (DbConnection)ctx.Connection;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var list = new List<GetTestEntity>();
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>(cts.Token).WithCancellation(cts.Token))
            list.Add(row);

        Assert.Equal(3, list.Count);
    }
}
#endif
