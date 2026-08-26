using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests proving that all 4 <c>QueryPartial&lt;T&gt;</c> overloads validate
/// <c>connection</c>/<c>sql</c> eagerly — i.e. calling the method itself throws immediately.
/// Previously these overloads had zero argument validation.
/// </summary>
public class QueryPartialEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    // ---------------------------------------------------------------------------
    // QueryPartial<T>(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartial_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartial<Product>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartial<Product>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartial<Product>("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartial<T>(connection, sql, object parameters)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartial_WithParameters_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartial<Product>("SELECT 1", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithParameters_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartial<Product>(null!, new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithParameters_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartial<Product>("   \t\n  ", new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartial<T>(connection, sql, CommandOptions<T> options)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartial_WithCommandOptions_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartial<Product>("SELECT 1", default(CommandOptions<Product>)));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithCommandOptions_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartial<Product>(null!, default(CommandOptions<Product>)));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartial<Product>("   \t\n  ", default(CommandOptions<Product>)));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartial<T>(connection, sql, object parameters, CommandOptions<T> options)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartial_WithParametersAndCommandOptions_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartial<Product>("SELECT 1", new { Id = 1 }, default(CommandOptions<Product>)));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithParametersAndCommandOptions_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartial<Product>(null!, new { Id = 1 }, default(CommandOptions<Product>)));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_WithParametersAndCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartial<Product>("   \t\n  ", new { Id = 1 }, default(CommandOptions<Product>)));

        Assert.Equal("sql", ex.ParamName);
    }
}
