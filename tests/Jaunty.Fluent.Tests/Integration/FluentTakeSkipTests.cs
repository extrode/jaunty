using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentTakeSkipTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentTakeSkipTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void Take_LimitsResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Take(5)
            .Select();

        Assert.Equal(5, products.Count);
    }

    [Fact]
    public void Skip_SkipsResults()
    {
        var allProducts = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Select();

        var skippedProducts = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        Assert.Equal(5, skippedProducts.Count);
        if (allProducts.Count > 5)
        {
            Assert.Equal(allProducts[5].ProductId, skippedProducts.First().ProductId);
        }
    }

    [Fact]
    public void Take_WithWhere_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Take(10)
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("LIMIT", sql);
    }
}