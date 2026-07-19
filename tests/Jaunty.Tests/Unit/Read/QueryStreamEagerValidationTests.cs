using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests proving that <c>QueryStream&lt;T1, T2&gt;</c> (the obsolete overload),
/// <c>QueryStream&lt;T1, T2, T3&gt;</c>, and both <c>QueryPartialList</c> overloads validate
/// <c>connection</c>/<c>sql</c> eagerly — i.e. calling the method itself throws immediately, with
/// no need to enumerate the returned <see cref="IEnumerable{T}"/> (via <c>.ToList()</c>,
/// <c>.GetEnumerator()</c>, or a <c>foreach</c>) for the exception to surface. Previously these
/// methods deferred validation to first enumeration (or, for a null connection, threw an unhelpful
/// <see cref="NullReferenceException"/> instead of <see cref="ArgumentNullException"/>).
/// </summary>
#pragma warning disable CS0618 // Suppress obsolete warning — these tests intentionally call the obsolete QueryStream<T1, T2> overload.
public class QueryStreamEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    // ---------------------------------------------------------------------------
    // QueryStream<T1, T2>(connection, sql, parameters = null, options = default) (obsolete overload)
    //
    // Called as a static method (Jaunty.QueryStream<...>) with `options: default(CommandOptions)`
    // explicitly specified (non-generic CommandOptions), which is the only way to disambiguate this
    // obsolete overload from the newer, non-obsolete QueryStream<T1, T2>(connection, sql) overload
    // that would otherwise win overload resolution for a 2-argument call.
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryStream_T1T2_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            Jaunty.QueryStream<Category, OrderSummary>(nullConnection!, "SELECT 1", parameters: null, options: default(CommandOptions)));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            Jaunty.QueryStream<Category, OrderSummary>(connection, null!, parameters: null, options: default(CommandOptions)));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2_EmptySql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Jaunty.QueryStream<Category, OrderSummary>(connection, "", parameters: null, options: default(CommandOptions)));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Jaunty.QueryStream<Category, OrderSummary>(connection, "   \t\n  ", parameters: null, options: default(CommandOptions)));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryStream<T1, T2, T3> (representative higher-arity overload)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryStream_T1T2T3_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryStream<Category, OrderSummary, ProductSummary>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2T3_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryStream<Category, OrderSummary, ProductSummary>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2T3_EmptySql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryStream<Category, OrderSummary, ProductSummary>(""));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2T3_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryStream<Category, OrderSummary, ProductSummary>("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartialList(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialList_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialList("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialList_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialList(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialList_EmptySql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialList(""));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialList_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialList("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryPartialList(connection, sql, parameters)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryPartialListWithParameters_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryPartialList("SELECT 1", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListWithParameters_NullSql_ThrowsArgumentNullExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartialList(null!, new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListWithParameters_EmptySql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialList("", new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryPartialListWithParameters_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryPartialList("   \t\n  ", new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }
}
#pragma warning restore CS0618
