using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class AttributeMappingTests : IDisposable
{
    private readonly Database _db;

    public AttributeMappingTests()
    {
        _db = new Database();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void QueryProjection_WithColumnAttributes_MapsCorrectly()
    {
        var products = _db.Connection.QueryProjection<ProductWithAttributes>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.Single(products);
        Assert.True(products[0].ProductId > 0);
        Assert.False(string.IsNullOrEmpty(products[0].ProductName));
    }

    [Fact]
    public void QueryProjection_WithIgnoredProperty_DoesNotRequireColumn()
    {
        // ComputedField is marked [Ignore], so it shouldn't require a matching column
        var products = _db.Connection.QueryProjection<ProductWithAttributes>(
            "SELECT product_id, product_name, unit_price FROM products");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Null(p.ComputedField));
    }
}
