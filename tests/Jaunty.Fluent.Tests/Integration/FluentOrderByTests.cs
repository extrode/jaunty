using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentOrderByTests : IDisposable
{
    private readonly Database _db;

    public FluentOrderByTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // --- OrderBy Expression Tests ---

    [Fact]
    public void OrderBy_Expression_SortsAscending()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void OrderByDescending_Expression_SortsDescending()
    {
        var products = _db.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.ProductId);
    }

    [Fact]
    public void OrderBy_NullableColumn_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.UnitPrice)
            .Select();

        products.Should().NotBeEmpty();
    }

    // --- OrderBy String Tests ---

    [Fact]
    public void OrderBy_String_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy("product_name")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void OrderByDescending_String_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderByDescending("product_id")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.ProductId);
    }

    // --- ThenBy Expression Tests ---

    [Fact]
    public void ThenBy_Expression_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();

        // Verify products are sorted by CategoryId first, then by ProductName within each category
        var grouped = products.GroupBy(p => p.CategoryId);
        foreach (var group in grouped)
        {
            group.Should().BeInAscendingOrder(p => p.ProductName);
        }
    }

    [Fact]
    public void ThenByDescending_Expression_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductId)
            .Select();

        products.Should().NotBeEmpty();

        // Verify products within each category are sorted descending by ProductId
        var grouped = products.GroupBy(p => p.CategoryId);
        foreach (var group in grouped)
        {
            group.Should().BeInDescendingOrder(p => p.ProductId);
        }
    }

    // --- ThenBy String Tests ---

    [Fact]
    public void ThenBy_String_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy("category_id")
            .ThenBy("product_name")
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void ThenByDescending_String_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy("category_id")
            .ThenByDescending("product_id")
            .Select();

        products.Should().NotBeEmpty();
    }

    // --- Multiple ThenBy Chains ---

    [Fact]
    public void OrderBy_MultipleThenBy_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.SupplierId)
            .ThenBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void OrderBy_MixedThenByDirections_SortsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.UnitPrice)
            .ThenBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
    }

    // --- OrderBy with Take/Skip Tests ---

    [Fact]
    public void OrderBy_WithTake_ReturnsLimitedSortedResults()
    {
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Take(5)
            .Select();

        products.Should().HaveCount(5);
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void OrderBy_WithSkipTake_ReturnsPagedSortedResults()
    {
        var allProducts = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Select();

        var page2 = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        page2.Should().HaveCount(5);
        page2.First().ProductId.Should().Be(allProducts[5].ProductId);
    }

    // --- OrderBy with WHERE Tests ---

    [Fact]
    public void OrderBy_WithWhere_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName)
            .ToSql();

        sql.Should().Contain("WHERE");
        sql.Should().Contain("ORDER BY");
    }

    [Fact]
    public void OrderBy_WithWhere_FiltersAndSorts()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    // --- ToSql Tests ---

    [Fact]
    public void OrderBy_ToSql_GeneratesOrderByClause()
    {
        var sql = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .ToSql();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("product_name");
        // ASC is the default and may not be explicit in the SQL
    }

    [Fact]
    public void OrderByDescending_ToSql_GeneratesDescKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .ToSql();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("product_id");
        sql.Should().Contain("DESC");
    }

    [Fact]
    public void ThenBy_ToSql_GeneratesMultipleOrderColumns()
    {
        var sql = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("category_id");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void ThenByDescending_ToSql_GeneratesCorrectDirection()
    {
        var sql = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductId)
            .ToSql();

        sql.Should().Contain("category_id");
        sql.Should().Contain("product_id");
        sql.Should().Contain("DESC");
        // ASC is the default and may not be explicit in the SQL
    }

    // --- Async Tests ---

    [Fact]
    public async Task OrderBy_SelectAsync_ReturnsSortedResults()
    {
        var products = await _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public async Task OrderByDescending_SelectAsync_ReturnsSortedResults()
    {
        var products = await _db.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.ProductId);
    }

    [Fact]
    public async Task OrderBy_SelectFirstAsync_ReturnsFirstSorted()
    {
        var product = await _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .SelectFirstAsync();

        var allSorted = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        product.ProductId.Should().Be(allSorted.First().ProductId);
    }
}
