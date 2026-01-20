using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentJoinTests : IDisposable
{
    private readonly Database _db;

    public FluentJoinTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void InnerJoin_ExpressionKeys_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId != null);
    }

    [Fact]
    public void InnerJoin_PredicateExpression_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_StringColumns_WithAliases_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .OnColumns("p.category_id", "c.category_id")
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_RawCondition_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .OnRaw("p.category_id = c.category_id")
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_SelectJoined_ReturnsCategories()
    {
        var categories = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectJoined();

        categories.Should().NotBeEmpty();
        categories.First().CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_SelectBoth_ReturnsTuples()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.From.ProductName.Should().NotBeNullOrEmpty();
        first.Joined.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_WithWhere_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void InnerJoin_WithOrderBy_OrdersResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void InnerJoin_Count_ReturnsCorrectCount()
    {
        var count = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCount();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void InnerJoin_ToSql_GeneratesValidSql()
    {
        var sql = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        sql.Should().Contain("INNER JOIN");
        sql.Should().Contain("ON");
        sql.Should().Contain("category_id");
    }

    [Fact]
    public void InnerJoin_WithAliases_ToSql_GeneratesValidSql()
    {
        var sql = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .OnColumns("p.category_id", "c.category_id")
            .ToSql();

        sql.Should().Contain("products p");
        sql.Should().Contain("categories c");
        sql.Should().Contain("p.category_id = c.category_id");
    }

    [Fact]
    public void LeftJoin_ReturnsAllFromLeft()
    {
        var products = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_SelectWithMapper_ReturnsProjectedResults()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select((p, c) => new { p.ProductName, c.CategoryName });

        results.Should().NotBeEmpty();
        results.First().ProductName.Should().NotBeNullOrEmpty();
        results.First().CategoryName.Should().NotBeNullOrEmpty();
    }
}
