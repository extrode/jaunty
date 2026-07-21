using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Spec 004 (gap #13, GroupBy+joins) T014-T016: GroupBy on a 3-way joined query, against a
/// real SQLite database - sibling to FluentGroupByJoinTests.cs (2-way), matching how
/// FluentMultiTableJoinTests.cs is a sibling of FluentJoinTests.cs for plain (non-grouped)
/// joins.
/// </summary>
public class FluentGroupByJoin3Tests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupByJoin3Tests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void GroupBy_KeyFromFirstEntity_WithAggregates_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum((p, c, s) => p.UnitsInStock)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.ProductCount > 0));
    }

    [Fact]
    public void GroupBy_KeyFromThirdEntity_ReturnsGroupedResults()
    {
        // Key drawn from the third joined entity, not From/second - proves parameter index 2
        // resolves correctly, not just 0/1.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => s.CompanyName)
            .Select(g => new { CompanyName = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.CompanyName)));
    }

    [Fact]
    public void GroupBy_AggregateOnThirdEntityColumn_ReturnsCorrectValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                DistinctSupplierIdCount = g.Count((p, c, s) => s.SupplierId)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.DistinctSupplierIdCount > 0));
    }

    [Fact]
    public void GroupBy_CompositeKey_ColumnNameCollisionAcrossThreeEntities_ResolvesCorrectly()
    {
        // Product.SupplierId and Supplier.SupplierId both physically map to "supplier_id" -
        // same collision shape as the 2-way test, now across a 3-way join.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => new { ProductSupplierId = p.SupplierId, JoinedSupplierId = s.SupplierId })
            .Select(g => new { g.Key.ProductSupplierId, g.Key.JoinedSupplierId, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(r.ProductSupplierId, r.JoinedSupplierId));
    }

    [Fact]
    public void GroupBy_Having_ClosureCapturedLocalVariable_FiltersGroups()
    {
        int minCount = 0;

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
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
            .GroupBy((p, c, s) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("JOIN", sql);
    }

    [Fact]
    public async Task GroupBy_SelectAsync_ReturnsGroupedResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GroupBy_SelectAsync_NonDbConnection_ThrowsNotSupportedException()
    {
        var query = new IDbConnectionWrapper(_fixture.Connection).From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            query.SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }));
    }
}
