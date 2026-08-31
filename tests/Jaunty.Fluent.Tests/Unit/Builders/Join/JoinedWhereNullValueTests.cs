using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R35-184. The string <c>Where</c> on the joined builders declared a non-nullable value and
/// bound a null as a parameter, emitting <c>col = @p</c> - UNKNOWN in SQL, so the filter silently
/// matched nothing. The single-table twin has always emitted <c>IS NULL</c>.
/// AUD-R35-185 covers the missing <c>SelectBothAsync</c>.
/// </summary>
public class JoinedWhereNullValueTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public JoinedWhereNullValueTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IJoinedQuery<Product, Category> TwoTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id");

    private IJoinedQuery3<Product, Category, Supplier> ThreeTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id");

    private IJoinedQuery4<Product, Category, Supplier, Order> FourTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("o.order_id > 0");

    [Fact]
    public void TwoTable_NullValue_EmitsIsNull()
    {
        string sql = TwoTable().Where("p.quantity_per_unit", null).ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("= @p_quantity_per_unit", sql);
    }

    [Fact]
    public void ThreeTable_NullValue_EmitsIsNull()
    {
        string sql = ThreeTable().Where("p.quantity_per_unit", null).ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("= @p_quantity_per_unit", sql);
    }

    [Fact]
    public void FourTable_NullValue_EmitsIsNull()
    {
        string sql = FourTable().Where("p.quantity_per_unit", null).ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("= @p_quantity_per_unit", sql);
    }

    [Fact]
    public void TwoTable_NonNullValue_StillBindsAParameter()
    {
        string sql = TwoTable().Where("p.product_name", "Chai").ToSql();

        Assert.DoesNotContain("IS NULL", sql);
        Assert.Contains("= @p_product_name", sql);
    }

    [Fact]
    public void TwoTable_NullValue_MatchesTheRowsTheCallerMeant()
    {
        // Every seeded product leaves quantity_per_unit unset, so the whole joined set is the
        // answer. Bound as a parameter the same filter returns 0, which is the defect.
        int all = TwoTable().Count();
        int nulls = TwoTable().Where("p.quantity_per_unit", null).Count();

        Assert.True(all > 0);
        Assert.Equal(all, nulls);
    }

    // ------------------------------------------------------------------
    // AUD-R35-185
    // ------------------------------------------------------------------

    [Fact]
    public async Task SelectBothAsync_ReturnsTheSameRowsAsSelectBoth()
    {
        var sync = TwoTable().SelectBoth();
        var async = await TwoTable().SelectBothAsync();

        Assert.NotEmpty(async);
        Assert.Equal(sync.Count, async.Count);
        Assert.Equal(
            sync.Select(r => r.From.ProductId),
            async.Select(r => r.From.ProductId));
        Assert.Equal(
            sync.Select(r => r.Joined.CategoryName),
            async.Select(r => r.Joined.CategoryName));
    }
}
