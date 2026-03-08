using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentDistinctTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentDistinctTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // --- Basic Distinct Tests ---

    [Fact]
    public void Distinct_Select_ReturnsDistinctResults()
    {
        var categories = _fixture.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(categories);

        // Verify distinct - no duplicate CategoryIds
        var categoryIds = categories.Select(p => p.CategoryId).ToList();
        Assert.Equal(categoryIds.Distinct().Count(), categoryIds.Count);
    }

    [Fact]
    public void Distinct_WithMultipleColumns_ReturnsDistinctCombinations()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId, p => p.SupplierId);

        Assert.NotEmpty(results);

        // Verify distinct combinations
        var combinations = results.Select(p => (p.CategoryId, p.SupplierId)).ToList();
        Assert.Equal(combinations.Distinct().Count(), combinations.Count);
    }

    [Fact]
    public void Distinct_Count_ReturnsDistinctCount()
    {
        var distinctCount = _fixture.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId)
            .Count;

        var totalCount = _fixture.Connection.From<Product>().Count();

        // Distinct category count should be less than total products
        Assert.True(distinctCount <= totalCount);
    }

    // --- Distinct with WHERE Tests ---

    [Fact]
    public void Distinct_Where_FiltersBeforeDistinct()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);

        var categoryIds = results.Select(p => p.CategoryId).ToList();
        Assert.Equal(categoryIds.Distinct().Count(), categoryIds.Count);
    }

    [Fact]
    public void Distinct_Where_StringColumn_FiltersCorrectly()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .Where("discontinued", false)
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);
    }

    // --- Distinct with ORDER BY Tests ---

    [Fact]
    public void Distinct_OrderBy_Expression_SortsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].CategoryId <= results[i].CategoryId);
        }
    }

    [Fact]
    public void Distinct_OrderByDescending_Expression_SortsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderByDescending(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].CategoryId >= results[i].CategoryId);
        }
    }

    [Fact]
    public void Distinct_OrderBy_String_SortsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy("category_id")
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].CategoryId <= results[i].CategoryId);
        }
    }

    [Fact]
    public void Distinct_OrderByDescending_String_SortsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderByDescending("category_id")
            .SelectPartial(p => p.CategoryId);

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].CategoryId >= results[i].CategoryId);
        }
    }

    // --- Distinct with Take/Skip Tests ---

    [Fact]
    public void Distinct_Take_LimitsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Distinct()
            .Take(3)
            .SelectPartial(p => p.CategoryId);

        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void Distinct_Skip_Take_PaginatesResults()
    {
        var allDistinct = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        var paged = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .Skip(1)
            .Take(2)
            .SelectPartial(p => p.CategoryId);

        Assert.True(paged.Count <= 2);

        if (allDistinct.Count > 1)
        {
            Assert.Equal(allDistinct[1].CategoryId, paged.First().CategoryId);
        }
    }

    // --- Distinct ToSql Tests ---

    [Fact]
    public void Distinct_ToSql_GeneratesDistinctKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .ToSql(p => p.CategoryId);

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("category_id", sql);
    }

    [Fact]
    public void Distinct_WithWhere_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .ToSql(p => p.CategoryId);

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("WHERE", sql);
    }

    [Fact]
    public void Distinct_WithOrderBy_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .ToSql(p => p.CategoryId);

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("ORDER BY", sql);
    }

    // --- Distinct Async Tests ---

    [Fact]
    public async Task Distinct_SelectAsync_ReturnsDistinctResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Distinct()
            .SelectPartialAsync(["category_id"]);

        Assert.NotEmpty(results);

        var categoryIds = results.Select(p => p.CategoryId).ToList();
        Assert.Equal(categoryIds.Distinct().Count(), categoryIds.Count);
    }

    [Fact]
    public async Task Distinct_WithWhere_SelectAsync_FiltersCorrectly()
    {
        var results = await _fixture.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .SelectPartialAsync(["category_id"]);

        Assert.NotEmpty(results);
    }

    // --- Distinct with Aggregates Tests ---

    [Fact]
    public void Distinct_Count_OnDistinctSet_ReturnsCorrectCount()
    {
        // Count of distinct categories
        var distinctCategories = _fixture.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId);

        Assert.True(distinctCategories.Count > 0);

        // Should be fewer distinct categories than total products
        var totalProducts = _fixture.Connection.From<Product>().Count();
        Assert.True(distinctCategories.Count <= totalProducts);
    }
}