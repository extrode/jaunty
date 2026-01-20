using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentTakeSkipTests : IDisposable
{
    private readonly Database _db;

    public FluentTakeSkipTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void Take_LimitsResults()
    {
        var products = _db.Connection.From<Product>()
            .Take(5)
            .Select();

        products.Should().HaveCount(5);
    }

    [Fact]
    public void Skip_SkipsResults()
    {
        var allProducts = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Select();

        var skippedProducts = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        skippedProducts.Should().HaveCount(5);
        if (allProducts.Count > 5)
        {
            skippedProducts.First().ProductId.Should().Be(allProducts[5].ProductId);
        }
    }

    [Fact]
    public void Take_WithWhere_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Take(10)
            .ToSql();

        sql.Should().Contain("WHERE");
        sql.Should().Contain("LIMIT");
    }
}
