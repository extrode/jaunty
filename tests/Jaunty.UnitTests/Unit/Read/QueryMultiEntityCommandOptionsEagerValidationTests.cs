using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Regression tests for the arity-2 <c>MultiEntityCommandOptions&lt;T1, T2&gt;</c> overloads
/// (AUD-R8): the netstandard2.0 fallback branch collapsed the null-vs-whitespace <c>sql</c> checks
/// into a single <c>if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(...)</c>,
/// which throws the wrong exception type for a non-null whitespace-only <c>sql</c> and doesn't
/// match the established <see cref="QueryMultiEntityEagerValidationTests"/> pattern used by every
/// sibling arity. This is a representative sample across sync/async and no-params/with-params
/// shapes — it does not exhaustively cover all 24 new overloads.
/// </summary>
public class QueryMultiEntityCommandOptionsEagerValidationTests
{
    private static SQLiteConnection CreateUnopenedConnection() => new();

    [Fact]
    public void Query_T1T2_MultiEntityCommandOptions_NullConnection_ThrowsArgumentNullExceptionEagerly()
    {
        IDbConnection? nullConnection = null;
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.Query<Category, ProductSummary>("SELECT 1", options));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_T1T2_MultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        // ArgumentNullException derives from ArgumentException; on net472 the codebase's
        // established #else fallback idiom throws ArgumentNullException for both null AND
        // whitespace/empty sql (unlike net8.0's ArgumentException.ThrowIfNullOrWhiteSpace, which
        // throws the narrower ArgumentException for whitespace/empty). ThrowsAny accepts either
        // concrete type so this test holds across both target frameworks.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.Query<Category, ProductSummary>("   \t\n  ", options));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void Query_T1T2_WithParametersAndMultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.Query<Category, ProductSummary>("   \t\n  ", new { Id = 1 }, options));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryFirst_T1T2_MultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryFirst<Category, ProductSummary>("   \t\n  ", options));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStream_T1T2_MultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryStream<Category, ProductSummary>("   \t\n  ", options).ToList());

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QueryAsync_T1T2_MultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await connection.QueryAsync<Category, ProductSummary>("   \t\n  ", options, CancellationToken.None));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void QueryStreamAsync_T1T2_MultiEntityCommandOptions_WhitespaceSql_ThrowsArgumentExceptionEagerly()
    {
        using var connection = CreateUnopenedConnection();
        var options = new MultiEntityCommandOptions<Category, ProductSummary>();

        // QueryStreamAsync itself is a plain (non-iterator) method that validates and delegates
        // to the core async-iterator method, so the exception is thrown synchronously on the
        // call itself - not deferred to first enumeration.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.QueryStreamAsync<Category, ProductSummary>("   \t\n  ", options, CancellationToken.None));

        Assert.Equal("sql", ex.ParamName);
    }
}
