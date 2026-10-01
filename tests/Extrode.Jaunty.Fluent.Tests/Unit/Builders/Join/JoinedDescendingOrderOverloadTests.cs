using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R38-091. Each arity-3/4 descending joined-order overload picks a join alias by index; these
/// pin that each one reaches its own entity's alias.
/// </summary>
public class JoinedDescendingOrderOverloadTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public JoinedDescendingOrderOverloadTests(FluentDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private IJoinedQuery3<Product, Category, Supplier> Three() => _fixture.Connection.From<Product>("p")
        .InnerJoin<Category>("c")
        .On("p.category_id", "c.category_id")
        .InnerJoin<Supplier>("s")
        .On("p.supplier_id", "s.supplier_id");

    private IJoinedQuery4<Product, Category, Supplier, Order> Four() => Three()
        .LeftJoin<Product, Category, Supplier, Order>("o")
        .On("o.order_id > 0");

    private static string OrderBy(string sql) => sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];

    [Fact]
    public void Three_OrderByJoinedDescending_Second()
    {
        Assert.Equal("ORDER BY c.category_name DESC", OrderBy(Three().OrderByJoinedDescending(c => c.CategoryName).ToSql()));
    }

    [Fact]
    public void Three_OrderByJoinedDescending_Third()
    {
        Assert.Equal("ORDER BY s.company_name DESC", OrderBy(Three().OrderByJoinedDescending(s => s.CompanyName).ToSql()));
    }

    [Fact]
    public void Three_ThenByJoinedDescending_Second()
    {
        Assert.Equal("ORDER BY p.product_id, c.category_name DESC",
            OrderBy(Three().OrderBy(p => p.ProductId).ThenByJoinedDescending(c => c.CategoryName).ToSql()));
    }

    [Fact]
    public void Three_ThenByJoinedDescending_Third()
    {
        Assert.Equal("ORDER BY p.product_id, s.company_name DESC",
            OrderBy(Three().OrderBy(p => p.ProductId).ThenByJoinedDescending(s => s.CompanyName).ToSql()));
    }

    [Fact]
    public void Four_OrderByJoinedDescending_Second()
    {
        Assert.Equal("ORDER BY c.category_name DESC", OrderBy(Four().OrderByJoinedDescending(c => c.CategoryName).ToSql()));
    }

    [Fact]
    public void Four_OrderByJoinedDescending_Third()
    {
        Assert.Equal("ORDER BY s.company_name DESC", OrderBy(Four().OrderByJoinedDescending(s => s.CompanyName).ToSql()));
    }

    [Fact]
    public void Four_OrderByJoinedDescending_Fourth()
    {
        Assert.Equal("ORDER BY o.order_id DESC", OrderBy(Four().OrderByJoinedDescending(o => o.OrderId).ToSql()));
    }

    [Fact]
    public void Four_ThenByJoinedDescending_Second()
    {
        Assert.Equal("ORDER BY p.product_id, c.category_name DESC",
            OrderBy(Four().OrderBy(p => p.ProductId).ThenByJoinedDescending(c => c.CategoryName).ToSql()));
    }

    [Fact]
    public void Four_ThenByJoinedDescending_Third()
    {
        Assert.Equal("ORDER BY p.product_id, s.company_name DESC",
            OrderBy(Four().OrderBy(p => p.ProductId).ThenByJoinedDescending(s => s.CompanyName).ToSql()));
    }

    [Fact]
    public void Four_ThenByJoinedDescending_Fourth()
    {
        Assert.Equal("ORDER BY p.product_id, o.order_id DESC",
            OrderBy(Four().OrderBy(p => p.ProductId).ThenByJoinedDescending(o => o.OrderId).ToSql()));
    }
}
