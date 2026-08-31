using System.Data;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Get;

[Collection("Get Operations")]
public class GetTests : IClassFixture<DialectFixture>
{
    private const string TableName = "get_test";
    private readonly DialectFixture _fixture;

    public GetTests(DialectFixture fixture)
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
    public void Get_ExistingRow_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "Alpha", 42);

        var entity = ctx.Connection.Get<GetTestEntity>(id);

        Assert.NotNull(entity);
        Assert.Equal(id, entity.Id);
        Assert.Equal("Alpha", entity.Name);
        Assert.Equal(42, entity.Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Get_MissingRow_ReturnsNull(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);

        var entity = ctx.Connection.Get<GetTestEntity>(-1L);

        Assert.Null(entity);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_MissingRow_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);

        Assert.Throws<InvalidOperationException>(() =>
            ctx.Connection.GetRequired<GetTestEntity>(-1L));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_ExistingRow_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "Beta", 99);

        var entity = ctx.Connection.GetRequired<GetTestEntity>(id);

        Assert.Equal(id, entity.Id);
        Assert.Equal("Beta", entity.Name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Get_TypedKey_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "Gamma", 7);

        var entity = ctx.Connection.Get<GetTestEntityTyped, long>(id);

        Assert.NotNull(entity);
        Assert.Equal(id, entity.Id);
        Assert.Equal("Gamma", entity.Name);
        Assert.Equal(7, entity.Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Get_WithTransaction_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "TxRow", 5);
        using var tx = ctx.Connection.BeginTransaction();

        var entity = ctx.Connection.Get<GetTestEntity>(id, CommandOptions<GetTestEntity>.WithTransaction(tx));

        Assert.NotNull(entity);
        Assert.Equal("TxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Get_TypedKey_WithTransaction_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "TypedTxRow", 6);
        using var tx = ctx.Connection.BeginTransaction();

        var entity = ctx.Connection.Get<GetTestEntityTyped, long>(id, CommandOptions<GetTestEntityTyped>.WithTransaction(tx));

        Assert.NotNull(entity);
        Assert.Equal("TypedTxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_WithTransaction_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "ReqTxRow", 8);
        using var tx = ctx.Connection.BeginTransaction();

        var entity = ctx.Connection.GetRequired<GetTestEntity>(id, CommandOptions<GetTestEntity>.WithTransaction(tx));

        Assert.Equal("ReqTxRow", entity.Name);
        tx.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_TypedKey_ReturnsEntity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "TypedRequired", 11);

        var entity = ctx.Connection.GetRequired<GetTestEntityTyped, long>(id);

        Assert.Equal(id, entity.Id);
        Assert.Equal("TypedRequired", entity.Name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_TypedKey_MissingRow_Throws(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);

        Assert.Throws<InvalidOperationException>(() =>
            ctx.Connection.GetRequired<GetTestEntityTyped, long>(-1L));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GetRequired_TypedKey_WithTransaction_ReadsUncommittedInSameTx(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContextForTable(dialect, TableName);
        var id = InsertRow(ctx.Connection, "TypedReqTxRow", 9);
        using var tx = ctx.Connection.BeginTransaction();

        var entity = ctx.Connection.GetRequired<GetTestEntityTyped, long>(id, CommandOptions<GetTestEntityTyped>.WithTransaction(tx));

        Assert.Equal("TypedReqTxRow", entity.Name);
        tx.Rollback();
    }
}
