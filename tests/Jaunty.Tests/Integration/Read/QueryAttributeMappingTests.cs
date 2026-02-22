using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryAttributeMappingTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryAttributeMappingTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_WithColumnAttributes_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.QueryPartial<ProductWithAttributes>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.Single(products);
        Assert.True(products[0].ProductId > 0);
        Assert.False(string.IsNullOrEmpty(products[0].ProductName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_WithIgnoredProperty_DoesNotRequireColumn(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // ComputedField is marked [Ignore], so it shouldn't require a matching column
        var products = connection.QueryPartial<ProductWithAttributes>(
            "SELECT product_id, product_name, unit_price FROM products");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Null(p.ComputedField));
    }
}

