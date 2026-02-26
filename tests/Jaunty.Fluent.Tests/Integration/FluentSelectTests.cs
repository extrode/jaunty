using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentSelectTests : IDisposable
{
    private readonly Database _db;

    public FluentSelectTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // --- Select All Tests ---

    [Fact]
    public void Select_AllColumns_ReturnsAllProducts()
    {
        var products = _db.Connection.From<Product>().Select();

        Assert.NotEmpty(products);
        Assert.True(products.Count > 0);
        Assert.NotEmpty(products.First().ProductName);
    }

    [Fact]
    public void Select_WithWhere_ReturnsFilteredProducts()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    // --- SelectPartial String-based Tests ---

    [Fact]
    public void SelectPartial_StringColumns_ReturnsOnlySelectedColumns()
    {
        // Use actual column names from the database
        var products = _db.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        Assert.NotEmpty(products);
        Assert.True(products.First().ProductId > 0);
        Assert.NotEmpty(products.First().ProductName);
    }

    [Fact]
    public void SelectPartial_StringColumns_OtherPropertiesAreDefault()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        Assert.NotEmpty(products);
        // Unselected nullable columns should be null
        Assert.Null(products.First().SupplierId);
        Assert.Null(products.First().UnitPrice);
    }

    // --- SelectPartial Expression-based Tests ---

    [Fact]
    public void SelectPartial_ExpressionColumns_ReturnsOnlySelectedColumns()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName);

        Assert.NotEmpty(products);
        Assert.True(products.First().ProductId > 0);
        Assert.NotEmpty(products.First().ProductName);
    }

    [Fact]
    public void SelectPartial_ExpressionColumns_MultipleColumns()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName, p => p.CategoryId, p => p.UnitPrice);

        Assert.NotEmpty(products);
        Assert.True(products.First().ProductId > 0);
        Assert.NotEmpty(products.First().ProductName);
    }

    // --- SelectFirst Tests ---

    [Fact]
    public void SelectFirst_ReturnsFirstProduct()
    {
        var product = _db.Connection.From<Product>().SelectFirst();

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Fact]
    public void SelectFirst_WithWhere_ReturnsFirstMatchingProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectFirst();

        Assert.NotNull(product);
        Assert.Equal((short)1, product.CategoryId);
    }

    [Fact]
    public void SelectFirst_WithOrderBy_ReturnsFirstInOrder()
    {
        var product = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .SelectFirst();

        var allOrdered = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.Equal(allOrdered.First().ProductId, product.ProductId);
    }

    // --- SelectFirstOrDefault Tests ---

    [Fact]
    public void SelectFirstOrDefault_WithMatch_ReturnsProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectFirstOrDefault();

        Assert.NotNull(product);
        Assert.Equal((short)1, product!.CategoryId);
    }

    [Fact]
    public void SelectFirstOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectFirstOrDefault();

        Assert.Null(product);
    }

    // --- SelectSingle Tests ---

    [Fact]
    public void SelectSingle_ExactlyOneMatch_ReturnsProduct()
    {
        // Get a specific product ID first
        var firstProduct = _db.Connection.From<Product>().SelectFirst();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == firstProduct.ProductId)
            .SelectSingle();

        Assert.NotNull(product);
        Assert.Equal(firstProduct.ProductId, product.ProductId);
    }

    [Fact]
    public void SelectSingle_MultipleMatches_Throws()
    {
        // Category 1 has multiple products
        var act = () => _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectSingle();

        Assert.Throws<InvalidOperationException>(act);
    }

    // --- SelectSingleOrDefault Tests ---

    [Fact]
    public void SelectSingleOrDefault_ExactlyOneMatch_ReturnsProduct()
    {
        var firstProduct = _db.Connection.From<Product>().SelectFirst();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == firstProduct.ProductId)
            .SelectSingleOrDefault();

        Assert.NotNull(product);
        Assert.Equal(firstProduct.ProductId, product!.ProductId);
    }

    [Fact]
    public void SelectSingleOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectSingleOrDefault();

        Assert.Null(product);
    }

    [Fact]
    public void SelectSingleOrDefault_MultipleMatches_Throws()
    {
        var act = () => _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectSingleOrDefault();

        Assert.Throws<InvalidOperationException>(act);
    }

    // --- SelectPartialFirst Tests ---

    [Fact]
    public void SelectPartialFirst_Expression_ReturnsFirstWithSelectedColumns()
    {
        var product = _db.Connection.From<Product>()
            .SelectPartialFirst(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.NotEmpty(product.ProductName);
    }

    [Fact]
    public void SelectPartialFirst_String_ReturnsFirstWithSelectedColumns()
    {
        var product = _db.Connection.From<Product>()
            .SelectPartialFirst("product_id", "product_name");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.NotEmpty(product.ProductName);
    }

    // --- SelectPartialFirstOrDefault Tests ---

    [Fact]
    public void SelectPartialFirstOrDefault_WithMatch_ReturnsProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectPartialFirstOrDefault(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(product);
    }

    [Fact]
    public void SelectPartialFirstOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialFirstOrDefault(p => p.ProductId);

        Assert.Null(product);
    }

    // --- Count Tests ---

    [Fact]
    public void Count_ReturnsProductCount()
    {
        var count = _db.Connection.From<Product>().Count();

        Assert.True(count > 0);
    }

    [Fact]
    public void Count_WithWhere_ReturnsFilteredCount()
    {
        var totalCount = _db.Connection.From<Product>().Count();
        var filteredCount = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        Assert.True(filteredCount > 0);
        Assert.True(filteredCount < totalCount);
    }

    // --- ToSql Tests ---

    [Fact]
    public void ToSql_NoColumns_GeneratesAllColumns()
    {
        var sql = _db.Connection.From<Product>().ToSql();

        // The implementation lists all columns explicitly instead of SELECT *
        Assert.Contains("SELECT", sql);
        Assert.Contains("product_id", sql);
        Assert.Contains("product_name", sql);
        Assert.Contains("products", sql);
    }

    [Fact]
    public void ToSql_WithColumns_GeneratesSelectedColumns()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => p.ProductId, p => p.ProductName);

        Assert.Contains("product_id", sql);
        Assert.Contains("product_name", sql);
        Assert.DoesNotContain("*", sql);
    }

    [Fact]
    public void ToSql_StringColumns_GeneratesSelectedColumns()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql("product_id", "product_name");

        Assert.Contains("product_id", sql);
        Assert.Contains("product_name", sql);
    }

    // --- Async Tests ---

    [Fact]
    public async Task SelectAsync_ReturnsProducts()
    {
        var products = await _db.Connection.From<Product>().SelectAsync();

        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task SelectFirstAsync_ReturnsFirstProduct()
    {
        var product = await _db.Connection.From<Product>().SelectFirstAsync();

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Fact]
    public async Task SelectFirstOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectFirstOrDefaultAsync();

        Assert.Null(product);
    }

    [Fact]
    public async Task SelectSingleAsync_ExactlyOneMatch_ReturnsProduct()
    {
        var firstProduct = _db.Connection.From<Product>().SelectFirst();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == firstProduct.ProductId)
            .SelectSingleAsync();

        Assert.Equal(firstProduct.ProductId, product.ProductId);
    }

    [Fact]
    public async Task SelectPartialAsync_Expression_ReturnsProducts()
    {
        var products = await _db.Connection.From<Product>()
            .SelectPartialAsync(new System.Linq.Expressions.Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task SelectPartialAsync_String_ReturnsProducts()
    {
        var products = await _db.Connection.From<Product>()
            .SelectPartialAsync(new[] { "product_id", "product_name" });

        Assert.NotEmpty(products);
    }
}
