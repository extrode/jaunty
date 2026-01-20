using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentDistinctTests : IDisposable
{
    private readonly Database _db;

    public FluentDistinctTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // --- Basic Distinct Tests ---

    [Fact]
    public void Distinct_Select_ReturnsDistinctResults()
    {
        var categories = _db.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId);

        categories.Should().NotBeEmpty();

        // Verify distinct - no duplicate CategoryIds
        var categoryIds = categories.Select(p => p.CategoryId).ToList();
        categoryIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Distinct_WithMultipleColumns_ReturnsDistinctCombinations()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId, p => p.SupplierId);

        results.Should().NotBeEmpty();

        // Verify distinct combinations
        var combinations = results.Select(p => (p.CategoryId, p.SupplierId)).ToList();
        combinations.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Distinct_Count_ReturnsDistinctCount()
    {
        var distinctCount = _db.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId)
            .Count;

        var totalCount = _db.Connection.From<Product>().Count();

        // Distinct category count should be less than total products
        distinctCount.Should().BeLessThanOrEqualTo(totalCount);
    }

    // --- Distinct with WHERE Tests ---

    [Fact]
    public void Distinct_Where_FiltersBeforeDistinct()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();

        var categoryIds = results.Select(p => p.CategoryId).ToList();
        categoryIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Distinct_Where_StringColumn_FiltersCorrectly()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .Where("discontinued", false)
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();
    }

    // --- Distinct with ORDER BY Tests ---

    [Fact]
    public void Distinct_OrderBy_Expression_SortsResults()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();
        results.Should().BeInAscendingOrder(p => p.CategoryId);
    }

    [Fact]
    public void Distinct_OrderByDescending_Expression_SortsResults()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .OrderByDescending(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();
        results.Should().BeInDescendingOrder(p => p.CategoryId);
    }

    [Fact]
    public void Distinct_OrderBy_String_SortsResults()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .OrderBy("category_id")
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();
        results.Should().BeInAscendingOrder(p => p.CategoryId);
    }

    [Fact]
    public void Distinct_OrderByDescending_String_SortsResults()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .OrderByDescending("category_id")
            .SelectPartial(p => p.CategoryId);

        results.Should().NotBeEmpty();
        results.Should().BeInDescendingOrder(p => p.CategoryId);
    }

    // --- Distinct with Take/Skip Tests ---

    [Fact]
    public void Distinct_Take_LimitsResults()
    {
        var results = _db.Connection.From<Product>()
            .Distinct()
            .Take(3)
            .SelectPartial(p => p.CategoryId);

        results.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public void Distinct_Skip_Take_PaginatesResults()
    {
        var allDistinct = _db.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .SelectPartial(p => p.CategoryId);

        var paged = _db.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .Skip(1)
            .Take(2)
            .SelectPartial(p => p.CategoryId);

        paged.Should().HaveCountLessThanOrEqualTo(2);

        if (allDistinct.Count > 1)
        {
            paged.First().CategoryId.Should().Be(allDistinct[1].CategoryId);
        }
    }

    // --- Distinct ToSql Tests ---

    [Fact]
    public void Distinct_ToSql_GeneratesDistinctKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .Distinct()
            .ToSql(p => p.CategoryId);

        sql.Should().Contain("SELECT DISTINCT");
        sql.Should().Contain("category_id");
    }

    [Fact]
    public void Distinct_WithWhere_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .ToSql(p => p.CategoryId);

        sql.Should().Contain("SELECT DISTINCT");
        sql.Should().Contain("WHERE");
    }

    [Fact]
    public void Distinct_WithOrderBy_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.CategoryId)
            .ToSql(p => p.CategoryId);

        sql.Should().Contain("SELECT DISTINCT");
        sql.Should().Contain("ORDER BY");
    }

    // --- Distinct Async Tests ---

    [Fact]
    public async Task Distinct_SelectAsync_ReturnsDistinctResults()
    {
        var results = await _db.Connection.From<Product>()
            .Distinct()
            .SelectPartialAsync(["category_id"]);

        results.Should().NotBeEmpty();

        var categoryIds = results.Select(p => p.CategoryId).ToList();
        categoryIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Distinct_WithWhere_SelectAsync_FiltersCorrectly()
    {
        var results = await _db.Connection.From<Product>()
            .Distinct()
            .Where(p => p.Discontinued == false)
            .SelectPartialAsync(["category_id"]);

        results.Should().NotBeEmpty();
    }

    // --- Distinct with Aggregates Tests ---

    [Fact]
    public void Distinct_Count_OnDistinctSet_ReturnsCorrectCount()
    {
        // Count of distinct categories
        var distinctCategories = _db.Connection.From<Product>()
            .Distinct()
            .SelectPartial(p => p.CategoryId);

        distinctCategories.Count.Should().BeGreaterThan(0);

        // Should be fewer distinct categories than total products
        var totalProducts = _db.Connection.From<Product>().Count();
        distinctCategories.Count.Should().BeLessThanOrEqualTo(totalProducts);
    }
}
