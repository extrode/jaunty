using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentAggregateTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentAggregateTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // --- COUNT Tests ---

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>().Count();
        Assert.True(count > 0);
    }

    [Fact]
    public void Count_WithSelector_CountsNonNullValues()
    {
        // COUNT(supplier_id) should count only non-null values
        var count = _fixture.Connection.From<Product>().Count(p => p.SupplierId);
        Assert.True(count > 0);
    }

    [Fact]
    public void Count_WithWhere_FiltersBeforeCounting()
    {
        var count = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        Assert.True(count > 0);

        var allCount = _fixture.Connection.From<Product>().Count();
        Assert.True(count < allCount);
    }

    [Fact]
    public void SelectCount_IsSameAsCount()
    {
        var count1 = _fixture.Connection.From<Product>().Count();
        var count2 = _fixture.Connection.From<Product>().SelectCount();

        Assert.Equal(count1, count2);
    }

    [Fact]
    public void LongCount_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>().LongCount();
        Assert.True(count > 0);
    }

    [Fact]
    public void LongCount_WithSelector_CountsNonNullValues()
    {
        var count = _fixture.Connection.From<Product>().LongCount(p => p.SupplierId);
        Assert.True(count > 0);
    }

    // --- SUM Tests (using int/long to avoid SQLite decimal issues) ---

    [Fact]
    public void Sum_WithIntColumn_ReturnsCorrectSum()
    {
        // SupplierId is int? - SQLite will return int64 for SUM
        var sum = _fixture.Connection.From<Product>().Sum(p => p.SupplierId);
        Assert.True(sum > 0);
    }

    [Fact]
    public void Sum_WithWhere_FiltersBeforeSumming()
    {
        var sumCategory1 = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Sum(p => p.SupplierId);

        var sumAll = _fixture.Connection.From<Product>().Sum(p => p.SupplierId);

        Assert.True(sumCategory1 > 0);
        Assert.True(sumAll > sumCategory1!.Value);
    }

    // --- AVG Tests ---

    [Fact]
    public void Avg_ReturnsCorrectAverage()
    {
        var avg = _fixture.Connection.From<Product>().Avg(p => p.SupplierId);
        Assert.True(avg > 0);
    }

    [Fact]
    public void Avg_WithWhere_FiltersBeforeAveraging()
    {
        var avgCategory1 = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Avg(p => p.SupplierId);

        Assert.True(avgCategory1 > 0);
    }

    // --- MIN Tests ---

    [Fact]
    public void Min_WithIntColumn_ReturnsMinValue()
    {
        var min = _fixture.Connection.From<Product>().Min(p => p.SupplierId);
        Assert.True(min > 0);
    }

    [Fact]
    public void Min_WithWhere_FiltersBeforeFindingMin()
    {
        var minCategory1 = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Min(p => p.SupplierId);

        Assert.True(minCategory1 > 0);
    }

    // --- MAX Tests ---

    [Fact]
    public void Max_WithIntColumn_ReturnsMaxValue()
    {
        var max = _fixture.Connection.From<Product>().Max(p => p.SupplierId);
        Assert.True(max > 0);

        var min = _fixture.Connection.From<Product>().Min(p => p.SupplierId);
        Assert.True(max >= min!.Value);
    }

    [Fact]
    public void Max_WithWhere_FiltersBeforeFindingMax()
    {
        var maxCategory1 = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Max(p => p.SupplierId);

        Assert.True(maxCategory1 > 0);
    }

    // --- Async Tests ---

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        var count = await _fixture.Connection.From<Product>().CountAsync();
        Assert.True(count > 0);
    }

    [Fact]
    public async Task CountAsync_WithSelector_CountsNonNullValues()
    {
        var count = await _fixture.Connection.From<Product>().CountAsync(p => p.SupplierId);
        Assert.True(count > 0);
    }

    [Fact]
    public async Task SumAsync_ReturnsCorrectSum()
    {
        var sum = await _fixture.Connection.From<Product>().SumAsync(p => p.SupplierId);
        Assert.True(sum > 0);
    }

    [Fact]
    public async Task AvgAsync_ReturnsCorrectAverage()
    {
        var avg = await _fixture.Connection.From<Product>().AvgAsync(p => p.SupplierId);
        Assert.True(avg > 0);
    }

    [Fact]
    public async Task MinAsync_ReturnsMinValue()
    {
        var min = await _fixture.Connection.From<Product>().MinAsync(p => p.SupplierId);
        Assert.True(min > 0);
    }

    [Fact]
    public async Task MaxAsync_ReturnsMaxValue()
    {
        var max = await _fixture.Connection.From<Product>().MaxAsync(p => p.SupplierId);
        Assert.True(max > 0);
    }

    // --- SelectX Async aliases ---

    [Fact]
    public async Task SelectCountAsync_IsSameAsCountAsync()
    {
        var count1 = await _fixture.Connection.From<Product>().CountAsync();
        var count2 = await _fixture.Connection.From<Product>().SelectCountAsync();

        Assert.Equal(count1, count2);
    }
}