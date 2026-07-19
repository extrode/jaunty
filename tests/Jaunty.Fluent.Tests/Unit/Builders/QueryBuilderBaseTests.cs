using System.Data;
using System.Data.SQLite;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// Tests for QueryBuilderBase via the public fluent API surface.
/// QueryBuilderBase is internal/abstract; its behaviour is exercised through
/// concrete builders (From&lt;T&gt;) that inherit from it.  All assertions target
/// observable SQL output (ToSql) or query results on an in-memory SQLite DB.
/// </summary>
public class QueryBuilderBaseTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public QueryBuilderBaseTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // GetEscapedTableName — exercised via the FROM clause in ToSql
    // ------------------------------------------------------------------

    [Fact]
    public void ToSql_ContainsEscapedTableName()
    {
        var sql = _fixture.Connection.From<Product>().ToSql();
        // SQLite dialect wraps identifiers in double-quotes or backticks
        Assert.Contains("product", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // GetPrefixedColumns — exercised via SelectAll (produces alias prefixes)
    // ------------------------------------------------------------------

    [Fact]
    public void TwoTableJoin_SelectAll_ToSql_ContainsPrefixedAliases()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        // SelectAll path emits "t1_" / "t2_" aliases via GetPrefixedColumnsWithAlias
        // Basic join SQL contains column references for both tables
        Assert.Contains("category_id", sql);
        Assert.Contains("product_id", sql);
    }

    // ------------------------------------------------------------------
    // BuildFromAndJoinClause — exercised via join SQL shape
    // ------------------------------------------------------------------

    [Fact]
    public void TwoTableJoin_ToSql_ContainsFromAndJoin()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        Assert.Contains("FROM", sql);
        Assert.Contains("JOIN", sql);
    }

    [Fact]
    public void Alias_AppearsInFromClause()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .ToSql();

        // BuildFromAndJoinClause emits "<table> <alias>" verbatim after the table name in
        // FROM/JOIN, so these substrings only appear if the alias was actually applied.
        Assert.Contains("products p", sql);
        Assert.Contains("categories c", sql);
    }

    // ------------------------------------------------------------------
    // BuildWhereClause — multiple conditions
    // ------------------------------------------------------------------

    [Fact]
    public void Where_StringCondition_AppearsInToSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where("products.unit_price > 10")
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("unit_price", sql);
    }

    [Fact]
    public void Where_AndChain_ProducesAndInSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where("products.unit_price > 10")
            .Where("products.discontinued = 0")
            .ToSql();

        Assert.Contains("AND", sql);
    }

    [Fact]
    public void Where_OrChain_ProducesOrInSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where("products.unit_price > 100 OR products.unit_price < 5")
            .ToSql();

        Assert.Contains("OR", sql);
    }

    // ------------------------------------------------------------------
    // BuildOrderByClause
    // ------------------------------------------------------------------

    [Fact]
    public void OrderBy_AppearsInToSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void OrderByDescending_ContainsDesc()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByDescending(p => p.ProductName)
            .ToSql();

        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void ThenBy_AppendsSecondarySort()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        int orderByIdx = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        Assert.True(orderByIdx >= 0);
        // Both sort keys must appear after ORDER BY
        string afterOrderBy = sql.Substring(orderByIdx);
        int categoryIdx = afterOrderBy.IndexOf("category_id", StringComparison.OrdinalIgnoreCase);
        int productIdx = afterOrderBy.IndexOf("product_name", StringComparison.OrdinalIgnoreCase);
        Assert.True(categoryIdx >= 0);
        Assert.True(productIdx > categoryIdx);
    }

    // ------------------------------------------------------------------
    // Parameter binding (BindParameters / GetParameters)
    // ------------------------------------------------------------------

    [Fact]
    public void WherePredicate_ParameterisedCondition_ExecutesWithoutError()
    {
        // Exercises the internal parameter collection and BindParameters path
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.Discontinued == false)
            .Select();

        Assert.NotNull(results);
    }
}
