using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// What happens when a query mixes a lambda <c>On</c>, which infers an alias, with a string-form
/// API that names the table. SQL has no way to keep both: aliasing a table in the FROM clause
/// retires its name as a qualifier for the rest of the statement. These tests pin the behaviour
/// that follows so it is a documented failure rather than a surprise.
/// </summary>
public class InferredAliasAndStringApiTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public InferredAliasAndStringApiTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void AStringOnNamingAnAliasedTable_FailsAtTheDatabase()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>().On("products.supplier_id = suppliers.supplier_id");

        Assert.Contains("INNER JOIN suppliers ON products.supplier_id", query.ToSql(), StringComparison.Ordinal);

        var ex = Assert.ThrowsAny<Exception>(() => query.SelectAll());
        Assert.Contains("products.supplier_id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStringWhereNamingAnAliasedTable_FailsAtTheDatabase()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On((p, c) => p.CategoryId == c.CategoryId)
            .Where("products.unit_price > 10");

        var ex = Assert.ThrowsAny<Exception>(() => query.Select());
        Assert.Contains("products.unit_price", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameQueryWrittenWithTheInferredAlias_Works()
    {
        var rows = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>().On("p.supplier_id = suppliers.supplier_id")
            .SelectAll();

        Assert.NotEmpty(rows);
    }
}
