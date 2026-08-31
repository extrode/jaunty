using System.Data;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-080. Every public entry point taking <c>(connection, sql, parameters)</c> validated its
/// arguments in one order under <c>NET8_0_OR_GREATER</c> and a different one under the
/// <c>netstandard2.0</c> fallback: the modern branch ran
/// <c>ArgumentNullException.ThrowIfNull(parameters)</c> before
/// <c>ArgumentException.ThrowIfNullOrWhiteSpace(sql)</c>, the fallback ran them the other way
/// round. With both arguments bad the exception <em>type</em> and the parameter name differed
/// between target frameworks, so the same call reported a different fault depending on which
/// build the caller had - and any test pinning one TFM's behaviour would fail on the other. The
/// pattern was introduced library-wide by AUD-R4-002 and swept here across 292 blocks in 49 files.
/// <para>
/// The canonical order is the signature order, each argument fully checked before the next:
/// connection, sql (null then whitespace), then the remaining parameter. These tests run on
/// net10.0, so they pin the branch that moved.
/// </para>
/// </summary>
public class ArgumentValidationOrderTests
{
    private static IDbConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// The shared assertion: with whitespace sql <em>and</em> a null parameters object, the sql
    /// fault wins. Before the sweep every one of these threw
    /// <c>ArgumentNullException("parameters")</c> on net8.0+ and this on netstandard2.0.
    /// </summary>
    private static void AssertSqlFaultWins(Action call)
    {
        var ex = Assert.Throws<ArgumentException>(call);
        Assert.Equal("sql", ex.ParamName);
        Assert.IsNotType<ArgumentNullException>(ex);
    }

    private static void AssertSqlFaultWinsAsync(Func<Task> call)
        => AssertSqlFaultWins(() => call().GetAwaiter().GetResult());

    [Fact]
    public void Query_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.Query<object>(" ", (object)null!));
    }

    [Fact]
    public void QueryAsync_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWinsAsync(() => connection.QueryAsync<object>(" ", (object)null!).AsTask());
    }

    [Fact]
    public void Execute_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.Execute(" ", (object)null!));
    }

    [Fact]
    public void ExecuteAsync_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWinsAsync(() => connection.ExecuteAsync(" ", (object)null!).AsTask());
    }

    [Fact]
    public void ExecuteScalar_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.ExecuteScalar<int>(" ", (object)null!));
    }

    [Fact]
    public void ExecuteScalarAsync_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWinsAsync(() => connection.ExecuteScalarAsync<int>(" ", (object)null!).AsTask());
    }

    [Fact]
    public void ExecuteBatch_ValidatesSqlBeforeParameterSets()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.ExecuteBatch(" ", null!));
    }

    [Fact]
    public void QueryFirst_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.QueryFirst<object>(" ", (object)null!));
    }

    [Fact]
    public void QueryFirstOrDefault_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.QueryFirstOrDefault<object>(" ", (object)null!));
    }

    [Fact]
    public void QueryMultiple_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.QueryMultiple(" ", (object)null!));
    }

    [Fact]
    public void QueryScalar_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.QueryScalar<int>(" ", (object)null!));
    }

    [Fact]
    public void QueryPartialList_ValidatesSqlBeforeParameters()
    {
        using IDbConnection connection = OpenConnection();

        AssertSqlFaultWins(() => connection.QueryPartialList(" ", (object)null!));
    }

    // ------------------------------------------------------------------
    // Controls: each argument is still reported on its own when it is the only bad one.
    // ------------------------------------------------------------------

    [Fact]
    public void ANullParameters_IsStillReported_WhenTheSqlIsFine()
    {
        using IDbConnection connection = OpenConnection();

        var ex = Assert.Throws<ArgumentNullException>(
            () => connection.Query<object>("SELECT 1", (object)null!));
        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void ANullSql_IsStillAnArgumentNullException()
    {
        using IDbConnection connection = OpenConnection();

        var ex = Assert.Throws<ArgumentNullException>(
            () => connection.Query<object>(null!, new { }));
        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ANullConnection_StillWinsOverBothOfThem()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(
            () => connection!.Query<object>(" ", (object)null!));
        Assert.Equal("connection", ex.ParamName);
    }
}
