using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for 3-table join functionality.
/// Note: 4-table joins require additional entities (e.g., OrderDetail, Customer) 
/// that are not currently available in the Fluent test entities.
/// </summary>
public class FluentMultiTableJoinTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentMultiTableJoinTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // Three-Table Join Tests - Basic Functionality
    // ==========================================

    [Fact]
    public void ThreeTableJoin_InnerJoinTwice_ReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotNull(first.Item1);
        Assert.NotNull(first.Item2);
        Assert.NotNull(first.Item3);
    }

    [Fact]
    public void ThreeTableJoin_Select_ReturnsPrimaryEntity()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Select();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.True(first.ProductId > 0);
        Assert.NotEmpty(first.ProductName);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_ReturnsValidSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .ToSql();

        Assert.Contains("SELECT", sql);
        Assert.Contains("FROM", sql);
        Assert.Contains("INNER JOIN", sql);
        var joinCount = sql.Split("INNER JOIN", StringSplitOptions.None).Length - 1;
        Assert.Equal(2, joinCount);
    }

    [Fact]
    public void ThreeTableJoin_WithStringJoinColumns_Works()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectAll();

        Assert.NotEmpty(results);
    }

    // ==========================================
    // Three-Table Join Tests - Where Clause
    // ==========================================

    [Fact]
    public void ThreeTableJoin_WhereWithStringCondition_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .Where("p.unit_price > 10")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.UnitPrice > 10));
    }

    [Fact]
    public void ThreeTableJoin_WhereWithExpression_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.False(p.Discontinued));
    }

    [Fact]
    public void ThreeTableJoin_WhereWithMultipleConditions_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1 && s.Country == "UK")
            .SelectAll();

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.Equal((short)1, r.Item1.CategoryId);
            Assert.Equal("UK", r.Item3.Country);
        });
    }

    // ==========================================
    // Three-Table Join Tests - SelectAll Variants
    // ==========================================

    [Fact]
    public void ThreeTableJoin_SelectAll_ReturnsAllEntities()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.Item1.ProductName);
        Assert.NotEmpty(first.Item2.CategoryName);
        Assert.NotEmpty(first.Item3.CompanyName);
    }

    [Fact]
    public void ThreeTableJoin_WithWhere_SelectAll_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1)
            .SelectAll();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal((short)1, r.Item1.CategoryId));
    }

    // ==========================================
    // Three-Table Join Tests - No Results Scenarios
    // ==========================================

    [Fact]
    public void ThreeTableJoin_NoResults_Select_ReturnsEmpty()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == -999)
            .Select();

        Assert.Empty(results);
    }

    [Fact]
    public void ThreeTableJoin_NoResults_SelectAll_ReturnsEmpty()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == -999)
            .SelectAll();

        Assert.Empty(results);
    }
}
