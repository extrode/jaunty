using System.Data;
using System.Data.Common;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Get;

[Collection("Get Operations")]
public class GetAsyncTests : IClassFixture<DialectFixture>
{
    private const string TableName = "get_test";
    private readonly DialectFixture _fixture;

    public GetAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static long InsertRow(IDbConnection connection, string name, int value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {TableName} (name, value) VALUES (@name, @value)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = name; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = value; cmd.Parameters.Add(p2);
        cmd.ExecuteNonQuery();

        using var idCmd = connection.CreateCommand();
        idCmd.CommandText = $"SELECT id FROM {TableName} WHERE name = @name";
        var p3 = idCmd.CreateParameter(); p3.ParameterName = "@name"; p3.Value = name; idCmd.Parameters.Add(p3);
        return Convert.ToInt64(idCmd.ExecuteScalar());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_ExistingRow_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncAlpha", 10);
        var dbConn = (DbConnection)ctx.Connection;

        var entity = await dbConn.GetAsync<GetTestEntity>(id);

        Assert.NotNull(entity);
        Assert.Equal(id, entity.Id);
        Assert.Equal("AsyncAlpha", entity.Name);
        Assert.Equal(10, entity.Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_MissingRow_ReturnsNull(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var dbConn = (DbConnection)ctx.Connection;

        var entity = await dbConn.GetAsync<GetTestEntity>(-1L);

        Assert.Null(entity);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetRequiredAsync_MissingRow_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var dbConn = (DbConnection)ctx.Connection;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dbConn.GetRequiredAsync<GetTestEntity>(-1L).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_TypedKey_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncGamma", 77);
        var dbConn = (DbConnection)ctx.Connection;

        var entity = await dbConn.GetAsync<GetTestEntityTyped, long>(id);

        Assert.NotNull(entity);
        Assert.Equal(id, entity.Id);
        Assert.Equal("AsyncGamma", entity.Name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "CancelTest", 1);
        var dbConn = (DbConnection)ctx.Connection;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var entity = await dbConn.GetAsync<GetTestEntity>(id, cts.Token);

        Assert.NotNull(entity);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_WithCommandOptions_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncTxRow", 15);
        var dbConn = (DbConnection)ctx.Connection;
        using var tx = dbConn.BeginTransaction();

        var entity = await dbConn.GetAsync<GetTestEntity>(id, CommandOptions<GetTestEntity>.WithTransaction(tx));

        Assert.NotNull(entity);
        Assert.Equal("AsyncTxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetAsync_TypedKey_WithCommandOptions_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncTypedTxRow", 16);
        var dbConn = (DbConnection)ctx.Connection;
        using var tx = dbConn.BeginTransaction();

        var entity = await dbConn.GetAsync<GetTestEntityTyped, long>(id, CommandOptions<GetTestEntityTyped>.WithTransaction(tx));

        Assert.NotNull(entity);
        Assert.Equal("AsyncTypedTxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetRequiredAsync_WithCommandOptions_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncReqTxRow", 17);
        var dbConn = (DbConnection)ctx.Connection;
        using var tx = dbConn.BeginTransaction();

        var entity = await dbConn.GetRequiredAsync<GetTestEntity>(id, CommandOptions<GetTestEntity>.WithTransaction(tx));

        Assert.Equal("AsyncReqTxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetRequiredAsync_TypedKey_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncTypedRequired", 18);
        var dbConn = (DbConnection)ctx.Connection;

        var entity = await dbConn.GetRequiredAsync<GetTestEntityTyped, long>(id);

        Assert.Equal(id, entity.Id);
        Assert.Equal("AsyncTypedRequired", entity.Name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetRequiredAsync_TypedKey_MissingRow_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var dbConn = (DbConnection)ctx.Connection;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dbConn.GetRequiredAsync<GetTestEntityTyped, long>(-1L).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GetRequiredAsync_TypedKey_WithCommandOptions_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "AsyncTypedReqTxRow", 19);
        var dbConn = (DbConnection)ctx.Connection;
        using var tx = dbConn.BeginTransaction();

        var entity = await dbConn.GetRequiredAsync<GetTestEntityTyped, long>(id, CommandOptions<GetTestEntityTyped>.WithTransaction(tx));

        Assert.Equal("AsyncTypedReqTxRow", entity.Name);
        tx.Rollback();
    }
}
