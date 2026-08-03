using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-209 and AUD-R35-210: the aggregates declared on <c>IGroupingJoined3</c> and
/// <c>IGroupingJoined4</c> that no test called at their own arity - <c>Avg</c>, <c>Min</c> and
/// <c>Max</c> at 3, and those plus <c>Count(selector)</c> at 4. What is under test is the arity's
/// own declaration and the selector's parameter-index resolution, not the shared aggregate builder.
/// </summary>
public class FluentGroupedJoinAggregateCoverageTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupedJoinAggregateCoverageTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // Arity 3
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeWay_AvgMinMax_OverTheFirstEntity_ReturnCorrectValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Average = g.Avg((p, c, s) => p.UnitsInStock),
                Cheapest = g.Min((p, c, s) => p.UnitPrice),
                Dearest = g.Max((p, c, s) => p.UnitPrice)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Dearest >= r.Cheapest));
        Assert.All(results, r => Assert.True(r.Average >= 0));
    }

    [Fact]
    public void ThreeWay_MinMax_OverTheThirdEntity_ResolveThatEntitysColumn()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                FirstSupplier = g.Min((p, c, s) => s.CompanyName),
                LastSupplier = g.Max((p, c, s) => s.CompanyName)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.FirstSupplier)));
        Assert.All(results, r => Assert.True(string.CompareOrdinal(r.LastSupplier, r.FirstSupplier) >= 0));
    }

    [Fact]
    public void ThreeWay_Avg_OverTheThirdEntity_NamesThatEntitysColumn()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Average = g.Avg((p, c, s) => s.SupplierId) });

        Assert.Contains("AVG(", sql);
        Assert.Contains("supplier_id", sql);
    }

    // ------------------------------------------------------------------
    // Arity 4
    // ------------------------------------------------------------------

    [Fact]
    public void FourWay_AvgMinMax_OverTheFirstEntity_ReturnCorrectValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Average = g.Avg((p, c, s, o) => p.UnitsInStock),
                Cheapest = g.Min((p, c, s, o) => p.UnitPrice),
                Dearest = g.Max((p, c, s, o) => p.UnitPrice)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Dearest >= r.Cheapest));
    }

    [Fact]
    public void FourWay_CountOverASelector_CountsNonNullValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Rows = g.Count(),
                Named = g.Count((p, c, s, o) => p.ProductName)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(r.Rows, r.Named));
    }

    [Fact]
    public void FourWay_CountOverASelector_NamesTheSelectedColumn()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Named = g.Count((p, c, s, o) => p.ProductName) });

        Assert.Contains("COUNT(", sql);
        Assert.Contains("product_name", sql);
        Assert.DoesNotContain("COUNT(*)", sql);
    }

    [Fact]
    public void FourWay_MinMax_OverTheFourthEntity_ResolveThatEntitysColumn()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                FirstEmployee = g.Min((p, c, s, o) => o.EmployeeId),
                LastEmployee = g.Max((p, c, s, o) => o.EmployeeId)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.LastEmployee >= r.FirstEmployee));
    }
}
