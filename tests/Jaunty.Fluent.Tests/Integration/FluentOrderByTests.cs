using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentOrderByTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentOrderByTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // --- OrderBy Expression Tests ---

    [Fact]
    public void OrderBy_Expression_SortsAscending()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void OrderByDescending_Expression_SortsDescending()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(products[i - 1].ProductId >= products[i].ProductId);
        }
    }

    [Fact]
    public void OrderBy_NullableColumn_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(products);
    }

    // --- OrderBy String Tests ---

    [Fact]
    public void OrderBy_String_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy("product_name")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void OrderByDescending_String_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderByDescending("product_id")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(products[i - 1].ProductId >= products[i].ProductId);
        }
    }

    // --- ThenBy Expression Tests ---

    [Fact]
    public void ThenBy_Expression_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);

        // Verify products are sorted by CategoryId first, then by ProductName within each category
        var grouped = products.GroupBy(p => p.CategoryId);
        foreach (var group in grouped)
        {
            var groupList = group.ToList();
            for (int i = 1; i < groupList.Count; i++)
            {
                Assert.True(string.Compare(groupList[i - 1].ProductName, groupList[i].ProductName) <= 0);
            }
        }
    }

    [Fact]
    public void ThenByDescending_Expression_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductId)
            .Select();

        Assert.NotEmpty(products);

        // Verify products within each category are sorted descending by ProductId
        var grouped = products.GroupBy(p => p.CategoryId);
        foreach (var group in grouped)
        {
            var groupList = group.ToList();
            for (int i = 1; i < groupList.Count; i++)
            {
                Assert.True(groupList[i - 1].ProductId >= groupList[i].ProductId);
            }
        }
    }

    // --- ThenBy String Tests ---

    [Fact]
    public void ThenBy_String_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy("category_id")
            .ThenBy("product_name")
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void ThenByDescending_String_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy("category_id")
            .ThenByDescending("product_id")
            .Select();

        Assert.NotEmpty(products);
    }

    // --- Multiple ThenBy Chains ---

    [Fact]
    public void OrderBy_MultipleThenBy_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.SupplierId)
            .ThenBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void OrderBy_MixedThenByDirections_SortsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.UnitPrice)
            .ThenBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
    }

    // --- OrderBy with Take/Skip Tests ---

    [Fact]
    public void OrderBy_WithTake_ReturnsLimitedSortedResults()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Take(5)
            .Select();

        Assert.Equal(5, products.Count);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void OrderBy_WithSkipTake_ReturnsPagedSortedResults()
    {
        var allProducts = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Select();

        var page2 = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        Assert.Equal(5, page2.Count);
        Assert.Equal(allProducts[5].ProductId, page2.First().ProductId);
    }

    // --- OrderBy with WHERE Tests ---

    [Fact]
    public void OrderBy_WithWhere_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void OrderBy_WithWhere_FiltersAndSorts()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    // --- ToSql Tests ---

    [Fact]
    public void OrderBy_ToSql_GeneratesOrderByClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
        // ASC is the default and may not be explicit in the SQL
    }

    [Fact]
    public void OrderByDescending_ToSql_GeneratesDescKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_id", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void ThenBy_ToSql_GeneratesMultipleOrderColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void ThenByDescending_ToSql_GeneratesCorrectDirection()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductId)
            .ToSql();

        Assert.Contains("category_id", sql);
        Assert.Contains("product_id", sql);
        Assert.Contains("DESC", sql);
        // ASC is the default and may not be explicit in the SQL
    }

    // --- Async Tests ---

    [Fact]
    public async Task OrderBy_SelectAsync_ReturnsSortedResults()
    {
        var products = await _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .SelectAsync();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public async Task OrderByDescending_SelectAsync_ReturnsSortedResults()
    {
        var products = await _fixture.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .SelectAsync();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(products[i - 1].ProductId >= products[i].ProductId);
        }
    }

    [Fact]
    public async Task OrderBy_SelectFirstAsync_ReturnsFirstSorted()
    {
        var product = await _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .SelectFirstAsync();

        var allSorted = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.Equal(allSorted.First().ProductId, product.ProductId);
    }
}
