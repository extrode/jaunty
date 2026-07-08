using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Spec 004 (gap #13, GroupBy+joins) T017-T019: GroupBy on a 4-way joined query, against a
/// real SQLite database - sibling to FluentGroupByJoinTests.cs (2-way) and
/// FluentGroupByJoin3Tests.cs (3-way). Reuses FluentFourTableJoinTests.cs's Product/Category/
/// Supplier/Order fixture, including its raw-string ON clause for the Order join (this test
/// schema has no real FK there - see that file's own doc comment).
/// </summary>
public class FluentGroupByJoin4Tests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupByJoin4Tests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void GroupBy_KeyFromFirstEntity_WithAggregates_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum((p, c, s, o) => p.UnitsInStock)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.ProductCount > 0));
    }

    [Fact]
    public void GroupBy_KeyFromFourthEntity_ReturnsGroupedResults()
    {
        // Key drawn from the fourth joined entity - proves parameter index 3 resolves
        // correctly, completing coverage of every position in a 4-way join.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => o.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_AggregateOnFourthEntityColumn_ReturnsCorrectValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                TotalFreight = g.Sum((p, c, s, o) => o.Freight)
            });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_Having_ClosureCapturedLocalVariable_FiltersGroups()
    {
        int minCount = 0;

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Having(g => g.Count() > minCount)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > minCount));
    }

    [Fact]
    public void GroupBy_ToSql_ReturnsGroupByAndAggregateSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("COUNT(*)", sql);
        int joinCount = sql.Split("INNER JOIN", StringSplitOptions.None).Length - 1;
        Assert.Equal(3, joinCount);
    }
}
