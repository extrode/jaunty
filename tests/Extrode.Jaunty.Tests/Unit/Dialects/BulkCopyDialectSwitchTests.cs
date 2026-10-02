using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Extensions.Reflection.Dialects;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

/// <summary>
/// <c>BulkCopyDialectFactory.Enable</c> and <c>ResetForTests</c> must each displace a dialect
/// <c>SqlDialectFactory</c> already resolved, and each wrapper must keep the dialect it was given.
/// </summary>
[Collection("Dialect Factory State")]
public class BulkCopyDialectSwitchTests
{
    [Fact]
    public void EnableAndReset_EachReplaceAnAlreadyResolvedDialect()
    {
        BulkCopyDialectFactory.ResetForTests();
        using var connection = new SqliteConnection();

        try
        {
            Assert.IsType<SQLiteDialect>(SqlDialectFactory.GetDialect(connection));

            BulkCopyDialectFactory.Enable();
            Assert.IsType<SQLiteDialectWithBulkCopy>(SqlDialectFactory.GetDialect(connection));

            BulkCopyDialectFactory.ResetForTests();
            Assert.IsType<SQLiteDialect>(SqlDialectFactory.GetDialect(connection));
        }
        finally
        {
            BulkCopyDialectFactory.ResetForTests();
        }
    }

    [Fact]
    public void ResetForTests_StopsWrapping()
    {
        BulkCopyDialectFactory.Enable();
        BulkCopyDialectFactory.ResetForTests();

        var dialect = new SqlServerDialect();

        Assert.Same(dialect, BulkCopyDialectFactory.GetDialect(dialect));
    }

    [Fact]
    public void Wrappers_KeepTheDialectTheyWereGiven()
    {
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();
        var mySql = new MySqlDialect();

        Assert.Same(sqlServer, new SqlServerDialectWithBulkCopy(sqlServer).InnerDialect);
        Assert.Same(postgres, new PostgreSqlDialectWithBulkCopy(postgres).InnerDialect);
        Assert.Same(mySql, new MySqlDialectWithBulkCopy(mySql).InnerDialect);
    }
}
