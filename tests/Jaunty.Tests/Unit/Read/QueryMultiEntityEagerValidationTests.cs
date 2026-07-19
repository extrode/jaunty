using System.Data;
using System.Data.SQLite;

using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests proving that the non-<c>QueryStream</c> arity-3 multi-entity overloads
/// (<c>Query</c>, <c>QueryFirst</c>, <c>QuerySingle</c>) validate <c>connection</c>/<c>sql</c>
/// eagerly, matching the sibling <c>QueryStream&lt;T1, T2, T3&gt;</c> overload (already covered in
/// <see cref="QueryStreamEagerValidationTests"/>). Previously these overloads had zero argument
/// validation. This is a representative sample — it does not exhaustively cover every arity/overload
/// combination across QueryMultiEntity3-7.cs.
/// </summary>
public class QueryMultiEntityEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    // ---------------------------------------------------------------------------
    // Query<T1, T2, T3>(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Query_T1T2T3_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.Query<Category, OrderSummary, ProductSummary>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_T1T2T3_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.Query<Category, OrderSummary, ProductSummary>("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QueryFirst<T1, T2, T3>(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QueryFirst_T1T2T3_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryFirst<Category, OrderSummary, ProductSummary>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryFirst_T1T2T3_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryFirst<Category, OrderSummary, ProductSummary>("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // QuerySingle<T1, T2, T3>(connection, sql)
    // ---------------------------------------------------------------------------

    [Fact]
    public void QuerySingle_T1T2T3_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QuerySingle<Category, OrderSummary, ProductSummary>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QuerySingle_T1T2T3_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QuerySingle<Category, OrderSummary, ProductSummary>("   \t\n  "));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // Query<T1, T2, T3>(connection, sql, object parameters) — with-parameters overload
    // ---------------------------------------------------------------------------

    [Fact]
    public void Query_T1T2T3_WithParameters_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.Query<Category, OrderSummary, ProductSummary>("SELECT 1", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_T1T2T3_WithParameters_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.Query<Category, OrderSummary, ProductSummary>("   \t\n  ", new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }
}
