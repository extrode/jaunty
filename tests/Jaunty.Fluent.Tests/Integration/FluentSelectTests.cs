using FluentAssertions;

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

        products.Should().NotBeEmpty();
        products.Should().HaveCountGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Select_WithWhere_ReturnsFilteredProducts()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    // --- SelectPartial String-based Tests ---

    [Fact]
    public void SelectPartial_StringColumns_ReturnsOnlySelectedColumns()
    {
        // Use actual column names from the database
        var products = _db.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartial_StringColumns_OtherPropertiesAreDefault()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        products.Should().NotBeEmpty();
        // Unselected nullable columns should be null
        products.First().SupplierId.Should().BeNull();
        products.First().UnitPrice.Should().BeNull();
    }

    // --- SelectPartial Expression-based Tests ---

    [Fact]
    public void SelectPartial_ExpressionColumns_ReturnsOnlySelectedColumns()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName);

        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartial_ExpressionColumns_MultipleColumns()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName, p => p.CategoryId, p => p.UnitPrice);

        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    // --- SelectFirst Tests ---

    [Fact]
    public void SelectFirst_ReturnsFirstProduct()
    {
        var product = _db.Connection.From<Product>().SelectFirst();

        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectFirst_WithWhere_ReturnsFirstMatchingProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectFirst();

        product.Should().NotBeNull();
        product.CategoryId.Should().Be(1);
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

        product.ProductId.Should().Be(allOrdered.First().ProductId);
    }

    // --- SelectFirstOrDefault Tests ---

    [Fact]
    public void SelectFirstOrDefault_WithMatch_ReturnsProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectFirstOrDefault();

        product.Should().NotBeNull();
        product!.CategoryId.Should().Be(1);
    }

    [Fact]
    public void SelectFirstOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectFirstOrDefault();

        product.Should().BeNull();
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

        product.Should().NotBeNull();
        product.ProductId.Should().Be(firstProduct.ProductId);
    }

    [Fact]
    public void SelectSingle_MultipleMatches_Throws()
    {
        // Category 1 has multiple products
        Action act = () => _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectSingle();

        act.Should().Throw<InvalidOperationException>();
    }

    // --- SelectSingleOrDefault Tests ---

    [Fact]
    public void SelectSingleOrDefault_ExactlyOneMatch_ReturnsProduct()
    {
        var firstProduct = _db.Connection.From<Product>().SelectFirst();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == firstProduct.ProductId)
            .SelectSingleOrDefault();

        product.Should().NotBeNull();
        product!.ProductId.Should().Be(firstProduct.ProductId);
    }

    [Fact]
    public void SelectSingleOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectSingleOrDefault();

        product.Should().BeNull();
    }

    [Fact]
    public void SelectSingleOrDefault_MultipleMatches_Throws()
    {
        Action act = () => _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectSingleOrDefault();

        act.Should().Throw<InvalidOperationException>();
    }

    // --- SelectPartialFirst Tests ---

    [Fact]
    public void SelectPartialFirst_Expression_ReturnsFirstWithSelectedColumns()
    {
        var product = _db.Connection.From<Product>()
            .SelectPartialFirst(p => p.ProductId, p => p.ProductName);

        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
        product.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartialFirst_String_ReturnsFirstWithSelectedColumns()
    {
        var product = _db.Connection.From<Product>()
            .SelectPartialFirst("product_id", "product_name");

        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
        product.ProductName.Should().NotBeNullOrEmpty();
    }

    // --- SelectPartialFirstOrDefault Tests ---

    [Fact]
    public void SelectPartialFirstOrDefault_WithMatch_ReturnsProduct()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectPartialFirstOrDefault(p => p.ProductId, p => p.ProductName);

        product.Should().NotBeNull();
    }

    [Fact]
    public void SelectPartialFirstOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialFirstOrDefault(p => p.ProductId);

        product.Should().BeNull();
    }

    // --- Count Tests ---

    [Fact]
    public void Count_ReturnsProductCount()
    {
        var count = _db.Connection.From<Product>().Count();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_WithWhere_ReturnsFilteredCount()
    {
        var totalCount = _db.Connection.From<Product>().Count();
        var filteredCount = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        filteredCount.Should().BeGreaterThan(0);
        filteredCount.Should().BeLessThan(totalCount);
    }

    // --- ToSql Tests ---

    [Fact]
    public void ToSql_NoColumns_GeneratesAllColumns()
    {
        var sql = _db.Connection.From<Product>().ToSql();

        // The implementation lists all columns explicitly instead of SELECT *
        sql.Should().Contain("SELECT");
        sql.Should().Contain("product_id");
        sql.Should().Contain("product_name");
        sql.Should().Contain("products");
    }

    [Fact]
    public void ToSql_WithColumns_GeneratesSelectedColumns()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => p.ProductId, p => p.ProductName);

        sql.Should().Contain("product_id");
        sql.Should().Contain("product_name");
        sql.Should().NotContain("*");
    }

    [Fact]
    public void ToSql_StringColumns_GeneratesSelectedColumns()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql("product_id", "product_name");

        sql.Should().Contain("product_id");
        sql.Should().Contain("product_name");
    }

    // --- Async Tests ---

    [Fact]
    public async Task SelectAsync_ReturnsProducts()
    {
        var products = await _db.Connection.From<Product>().SelectAsync();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectFirstAsync_ReturnsFirstProduct()
    {
        var product = await _db.Connection.From<Product>().SelectFirstAsync();

        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectFirstOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectFirstOrDefaultAsync();

        product.Should().BeNull();
    }

    [Fact]
    public async Task SelectSingleAsync_ExactlyOneMatch_ReturnsProduct()
    {
        var firstProduct = _db.Connection.From<Product>().SelectFirst();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == firstProduct.ProductId)
            .SelectSingleAsync();

        product.ProductId.Should().Be(firstProduct.ProductId);
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

        products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectPartialAsync_String_ReturnsProducts()
    {
        var products = await _db.Connection.From<Product>()
            .SelectPartialAsync(new[] { "product_id", "product_name" });

        products.Should().NotBeEmpty();
    }
}
