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
        var products = _db.Connection.From<Product>().Select();

        products.Should().NotBeEmpty();
        products.Should().HaveCountGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectPartial_StringColumns_ReturnsOnlySelectedColumns()
    {
        var products = _db.Connection.From<Product>()
            .SelectPartial("ProductId", "ProductName");

        products.Should().NotBeEmpty();
        products.First().ProductId.Should().BeGreaterThan(0);
        products.First().ProductName.Should().NotBeNullOrEmpty();
    }

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
    public void SelectFirst_ReturnsFirstProduct()
    {
        var product = _db.Connection.From<Product>().SelectFirst();

        product.Should().NotBeNull();
        product.ProductId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectFirstOrDefault_NoMatch_ReturnsNull()
    {
        var product = _db.Connection.From<Product>()
            .Where("ProductId", -1)
            .SelectFirstOrDefault();

        product.Should().BeNull();
    }

    [Fact]
    public void Count_ReturnsProductCount()
    {
        var count = _db.Connection.From<Product>().Count();

        count.Should().BeGreaterThan(0);
    }
}
