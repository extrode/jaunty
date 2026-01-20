using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentAggregateTests : IDisposable
{
    private readonly Database _db;

    public FluentAggregateTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // --- COUNT Tests ---

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var count = _db.Connection.From<Product>().Count();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_WithSelector_CountsNonNullValues()
    {
        // COUNT(supplier_id) should count only non-null values
        var count = _db.Connection.From<Product>().Count(p => p.SupplierId);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_WithWhere_FiltersBeforeCounting()
    {
        var count = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        count.Should().BeGreaterThan(0);

        var allCount = _db.Connection.From<Product>().Count();
        count.Should().BeLessThan(allCount);
    }

    [Fact]
    public void SelectCount_IsSameAsCount()
    {
        var count1 = _db.Connection.From<Product>().Count();
        var count2 = _db.Connection.From<Product>().SelectCount();

        count2.Should().Be(count1);
    }

    [Fact]
    public void LongCount_ReturnsCorrectCount()
    {
        var count = _db.Connection.From<Product>().LongCount();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LongCount_WithSelector_CountsNonNullValues()
    {
        var count = _db.Connection.From<Product>().LongCount(p => p.SupplierId);
        count.Should().BeGreaterThan(0);
    }

    // --- SUM Tests (using int/long to avoid SQLite decimal issues) ---

    [Fact]
    public void Sum_WithIntColumn_ReturnsCorrectSum()
    {
        // SupplierId is int? - SQLite will return int64 for SUM
        var sum = _db.Connection.From<Product>().Sum(p => p.SupplierId);
        sum.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sum_WithWhere_FiltersBeforeSumming()
    {
        var sumCategory1 = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Sum(p => p.SupplierId);

        var sumAll = _db.Connection.From<Product>().Sum(p => p.SupplierId);

        sumCategory1.Should().BeGreaterThan(0);
        sumAll.Should().BeGreaterThan(sumCategory1!.Value);
    }

    // --- AVG Tests ---

    [Fact]
    public void Avg_ReturnsCorrectAverage()
    {
        var avg = _db.Connection.From<Product>().Avg(p => p.SupplierId);
        avg.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Avg_WithWhere_FiltersBeforeAveraging()
    {
        var avgCategory1 = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Avg(p => p.SupplierId);

        avgCategory1.Should().BeGreaterThan(0);
    }

    // --- MIN Tests ---

    [Fact]
    public void Min_WithIntColumn_ReturnsMinValue()
    {
        var min = _db.Connection.From<Product>().Min(p => p.SupplierId);
        min.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Min_WithWhere_FiltersBeforeFindingMin()
    {
        var minCategory1 = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Min(p => p.SupplierId);

        minCategory1.Should().BeGreaterThan(0);
    }

    // --- MAX Tests ---

    [Fact]
    public void Max_WithIntColumn_ReturnsMaxValue()
    {
        var max = _db.Connection.From<Product>().Max(p => p.SupplierId);
        max.Should().BeGreaterThan(0);

        var min = _db.Connection.From<Product>().Min(p => p.SupplierId);
        max.Should().BeGreaterThanOrEqualTo(min!.Value);
    }

    [Fact]
    public void Max_WithWhere_FiltersBeforeFindingMax()
    {
        var maxCategory1 = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Max(p => p.SupplierId);

        maxCategory1.Should().BeGreaterThan(0);
    }

    // --- Async Tests ---

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        var count = await _db.Connection.From<Product>().CountAsync();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CountAsync_WithSelector_CountsNonNullValues()
    {
        var count = await _db.Connection.From<Product>().CountAsync(p => p.SupplierId);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SumAsync_ReturnsCorrectSum()
    {
        var sum = await _db.Connection.From<Product>().SumAsync(p => p.SupplierId);
        sum.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AvgAsync_ReturnsCorrectAverage()
    {
        var avg = await _db.Connection.From<Product>().AvgAsync(p => p.SupplierId);
        avg.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MinAsync_ReturnsMinValue()
    {
        var min = await _db.Connection.From<Product>().MinAsync(p => p.SupplierId);
        min.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MaxAsync_ReturnsMaxValue()
    {
        var max = await _db.Connection.From<Product>().MaxAsync(p => p.SupplierId);
        max.Should().BeGreaterThan(0);
    }

    // --- SelectX Async aliases ---

    [Fact]
    public async Task SelectCountAsync_IsSameAsCountAsync()
    {
        var count1 = await _db.Connection.From<Product>().CountAsync();
        var count2 = await _db.Connection.From<Product>().SelectCountAsync();

        count2.Should().Be(count1);
    }
}
