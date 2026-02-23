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
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_WithColumnAttributes_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.QueryPartial<ProductWithAttributes>(
            ProductColumnsSql(dialect, "WHERE " + ProductIdColumn(dialect) + " = @Id"),
            new { Id = 1 });

        Assert.Single(products);
        Assert.True(products[0].ProductId > 0);
        Assert.False(string.IsNullOrEmpty(products[0].ProductName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_WithIgnoredProperty_DoesNotRequireColumn(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // ComputedField is marked [Ignore], so it shouldn't require a matching column
        var products = connection.QueryPartial<ProductWithAttributes>(
            ProductColumnsSql(dialect));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Null(p.ComputedField));
    }

    private static string ProductColumnsSql(DialectInfo dialect, string suffix = "") =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT ProductId, ProductName, UnitPrice FROM Products {suffix}".TrimEnd()
            : $"SELECT product_id, product_name, unit_price FROM products {suffix}".TrimEnd();

    private static string ProductIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "ProductId" : "product_id";
}

