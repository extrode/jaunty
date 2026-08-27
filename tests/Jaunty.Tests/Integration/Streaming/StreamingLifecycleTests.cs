#if ASYNC_ENUMERABLE_SUPPORT
using System.Data;
using System.Data.Common;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Streaming;

[Collection("Get Operations")]
public class StreamingLifecycleTests : IClassFixture<DialectFixture>
{
    private const string TableName = "get_test";
    private readonly DialectFixture _fixture;

    public StreamingLifecycleTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void InsertRows(IDbConnection connection, int count)
    {
        using var tx = connection.BeginTransaction();
        for (int i = 1; i <= count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"INSERT INTO {TableName} (name, value) VALUES (@name, @value)";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = $"Stream{i}"; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = i; cmd.Parameters.Add(p2);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static long CountRows(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {TableName}";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_PreCancelledToken_ThrowsBeforeYieldingAnyRow(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var yielded = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>(cts.Token))
                yielded++;
        });

        Assert.Equal(0, yielded);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_CancelAfterFirstRow_StopsWithOperationCanceled(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        using var cts = new CancellationTokenSource();

        var yielded = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>(cts.Token))
            {
                yielded++;
                cts.Cancel();
            }
        });

        Assert.Equal(1, yielded);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_CancelMidIteration_LeavesConnectionUsable(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>(cts.Token))
                cts.Cancel();
        });

        Assert.Equal(ConnectionState.Open, dbConn.State);
        Assert.Equal(5, CountRows(dbConn));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_BreakMidIteration_LeavesConnectionUsable(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        var yielded = 0;
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
        {
            yielded++;
            if (yielded == 2) break;
        }

        Assert.Equal(2, yielded);
        Assert.Equal(ConnectionState.Open, dbConn.State);
        Assert.Equal(5, CountRows(dbConn));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_DisposeMidIteration_IsIdempotent(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        var enumerator = dbConn.GetAllStreamAsync<GetTestEntity>().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());

        await enumerator.DisposeAsync();
        await enumerator.DisposeAsync();

        Assert.Equal(ConnectionState.Open, dbConn.State);
        Assert.Equal(5, CountRows(dbConn));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_MoveNextAfterDispose_ReturnsFalse(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertRows(ctx.Connection, 5);
        var dbConn = (DbConnection)ctx.Connection;

        var enumerator = dbConn.GetAllStreamAsync<GetTestEntity>().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();

        Assert.False(await enumerator.MoveNextAsync());
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_TwentyThousandRows_YieldsEveryRowInOrder(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertGeneratedRows(ctx.Connection, 20_000);
        var dbConn = (DbConnection)ctx.Connection;

        long seen = 0;
        long lastId = 0;
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
        {
            seen++;
            Assert.True(row.Id > lastId);
            lastId = row.Id;
        }

        Assert.Equal(20_000, seen);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAllStreamAsync_TwentyThousandRows_BreakingEarlyStillReleasesTheReader(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        InsertGeneratedRows(ctx.Connection, 20_000);
        var dbConn = (DbConnection)ctx.Connection;

        var yielded = 0;
        await foreach (var row in dbConn.GetAllStreamAsync<GetTestEntity>())
        {
            yielded++;
            if (yielded == 10) break;
        }

        Assert.Equal(10, yielded);
        Assert.Equal(20_000, CountRows(dbConn));
    }

    private static void InsertGeneratedRows(IDbConnection connection, int count)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"WITH RECURSIVE counter(x) AS (SELECT 1 UNION ALL SELECT x + 1 FROM counter WHERE x < {count}) " +
            $"INSERT INTO {TableName} (name, value) SELECT 'Stream' || x, x FROM counter";
        cmd.ExecuteNonQuery();
    }
}
#endif
