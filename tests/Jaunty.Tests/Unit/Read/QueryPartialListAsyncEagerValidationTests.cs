using System.Data;
using System.Data.SQLite;

using Jaunty.Core;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests proving that all 4 <c>QueryPartialListAsync</c> overloads validate
/// <c>connection</c>/<c>sql</c> eagerly — i.e. calling the method itself throws immediately,
/// without needing to await the returned <see cref="ValueTask"/>. These overloads were previously
/// declared <c>async ValueTask&lt;...&gt;</c>, which defers argument-validation exceptions until
/// the task is awaited, unlike every other async partial-mapping method in the codebase.
/// </summary>
public class QueryPartialListAsyncEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    // ---------------------------------------------------------------------------
    // QueryPartialListAsync(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialListAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialListAsync("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialListAsync(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialListAsync("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartialListAsync(connection, sql, object parameters)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialListAsync_WithParameters_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialListAsync("SELECT 1", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithParameters_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialListAsync(null!, new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithParameters_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialListAsync("   \t\n  ", new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartialListAsync(connection, sql, CommandOptions options)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialListAsync_WithCommandOptions_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialListAsync("SELECT 1", new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithCommandOptions_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialListAsync(null!, new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialListAsync("   \t\n  ", new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartialListAsync(connection, sql, object parameters, CommandOptions options)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialListAsync_WithParametersAndCommandOptions_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialListAsync("SELECT 1", new { Id = 1 }, new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithParametersAndCommandOptions_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialListAsync(null!, new { Id = 1 }, new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListAsync_WithParametersAndCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialListAsync("   \t\n  ", new { Id = 1 }, new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }
}
