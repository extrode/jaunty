using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// R27 batch 8 (medium). The mapper-based joined SelectPartialFirst/Single terminals decided
/// "no rows" with a null test on the mapped value, so a value-type mapper result made the
/// zero-row case return default(T) instead of throwing. Emptiness is now tracked by row
/// count/flag independent of the mapped value.
/// </summary>
public class FluentJoinSelectPartialEmptyResultTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinSelectPartialEmptyResultTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IJoinedQuery<Product, Category> EmptyJoin() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999");

    [Fact]
    public void SelectPartialFirst_ValueTypeMapper_NoRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EmptyJoin().SelectPartialFirst("p.product_id", r => r.GetInt32(0)));

        Assert.Contains("no elements", ex.Message);
    }

    [Fact]
    public void SelectPartialSingle_ValueTypeMapper_NoRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EmptyJoin().SelectPartialSingle("p.product_id", r => r.GetInt32(0)));

        Assert.Contains("no elements", ex.Message);
    }

    [Fact]
    public async Task SelectPartialSingleAsync_ValueTypeMapper_NoRows_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmptyJoin().SelectPartialSingleAsync("p.product_id", r => r.GetInt32(0)));

        Assert.Contains("no elements", ex.Message);
    }

    [Fact]
    public async Task SelectPartialFirstAsync_ValueTypeMapper_NoRows_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmptyJoin().SelectPartialFirstAsync("p.product_id", r => r.GetInt32(0)));
    }

    [Fact]
    public void SelectPartialFirstOrDefault_ValueTypeMapper_NoRows_ReturnsDefault()
    {
        int result = EmptyJoin().SelectPartialFirstOrDefault("p.product_id", r => r.GetInt32(0));

        Assert.Equal(0, result);
    }

    [Fact]
    public void SelectPartialSingleOrDefault_ValueTypeMapper_NoRows_ReturnsDefault()
    {
        int result = EmptyJoin().SelectPartialSingleOrDefault("p.product_id", r => r.GetInt32(0));

        Assert.Equal(0, result);
    }

    [Fact]
    public void SelectPartialFirst_ValueTypeMapper_WithRow_ReturnsValue()
    {
        int result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_name = 'Chai'")
            .SelectPartialFirst("p.units_in_stock", r => r.GetInt32(0));

        Assert.Equal(39, result);
    }
}
