using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests proving that the 6 legacy <c>[Obsolete]</c> arity-2 multi-entity async
/// overloads (the ones taking a non-generic <see cref="CommandOptions"/>) validate
/// <c>connection</c>/<c>sql</c>/<c>map</c> eagerly - i.e. calling the method itself throws
/// immediately, without needing to await the returned <see cref="ValueTask"/>. Previously these
/// overloads only checked <c>connection is not DbConnection</c>, so a null connection threw
/// <see cref="InvalidOperationException"/> instead of <see cref="ArgumentNullException"/>, and a
/// null/whitespace sql wasn't validated at all before reaching the database call.
/// </summary>
#pragma warning disable CS0618 // Suppress obsolete warnings — these tests intentionally call obsolete methods
public class QueryMultiEntityAsyncLegacyEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    // ---------------------------------------------------------------------------
    // QueryAsync<T1, T2>(connection, sql, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryAsync<Category, OrderSummary>("SELECT 1", options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryAsync<Category, OrderSummary>("   \t\n  ", options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryAsync<T1, T2, TResult>(connection, sql, map, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryAsyncWithMap_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryAsync<Category, OrderSummary, string>(
                "SELECT 1", (c, o) => c.CategoryName!, options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryAsyncWithMap_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryAsync<Category, OrderSummary, string>(
                "   \t\n  ", (c, o) => c.CategoryName!, options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryAsyncWithMap_NullMap_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryAsync<Category, OrderSummary, string>(
                "SELECT 1", null!, options: new CommandOptions()));

        Assert.Equal("map", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryFirstAsync<T1, T2>(connection, sql, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryFirstAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryFirstAsync<Category, OrderSummary>("SELECT 1", options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryFirstAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryFirstAsync<Category, OrderSummary>("   \t\n  ", options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryFirstOrDefaultAsync<T1, T2>(connection, sql, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryFirstOrDefaultAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryFirstOrDefaultAsync<Category, OrderSummary>("SELECT 1", options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryFirstOrDefaultAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryFirstOrDefaultAsync<Category, OrderSummary>("   \t\n  ", options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QuerySingleAsync<T1, T2>(connection, sql, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QuerySingleAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QuerySingleAsync<Category, OrderSummary>("SELECT 1", options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QuerySingleAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QuerySingleAsync<Category, OrderSummary>("   \t\n  ", options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QuerySingleOrDefaultAsync<T1, T2>(connection, sql, parameters, options, cancellationToken)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QuerySingleOrDefaultAsync_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QuerySingleOrDefaultAsync<Category, OrderSummary>("SELECT 1", options: new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QuerySingleOrDefaultAsync_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QuerySingleOrDefaultAsync<Category, OrderSummary>("   \t\n  ", options: new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }
}
