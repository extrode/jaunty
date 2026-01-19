using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentSelectTests : IDisposable
{
    private readonly Database _db;

    public FluentSelectTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void Select_AllColumns_ReturnsAllProducts()
    {
        // Act
        var products = _db.Connection.From<Product>().Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().HaveCountGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartial_StringColumns_ReturnsOnlySelectedColumns()
    {
        // Act - use actual database column names (snake_case)
        var products = _db.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        // Assert
        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartial_ExpressionColumns_ReturnsOnlySelectedColumns()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName);

        // Assert
        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectFirst_ReturnsFirstProduct()
    {
        // Act
        var product = _db.Connection.From<Product>().SelectFirst();

        // Assert
        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectFirstOrDefault_NoMatch_ReturnsNull()
    {
        // Act - use actual database column name
        var product = _db.Connection.From<Product>()
            .Where("product_id", -1)
            .SelectFirstOrDefault();

        // Assert
        product.Should().BeNull();
    }

    [Fact]
    public void Count_ReturnsProductCount()
    {
        // SQLite COUNT(*) returns Int64, so use LongCount()
        var count = _db.Connection.From<Product>().LongCount();

        count.Should().BeGreaterThan(0);
    }
}
