using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// HAVING operands are named after the aggregate they are compared to, so a command log reads
/// <c>HAVING SUM(p.unit_price) &gt; @sum_p_unit_price</c> rather than <c>&gt; @jhp_0</c>. The
/// suffix is spent only where one query compares the same aggregate twice, and the positional form
/// survives for a comparison with no aggregate to be named after.
/// </summary>
public class HavingParameterNamingTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public HavingParameterNamingTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IJoinedQuery<Product, Category> Joined() =>
        _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId);

    [Fact]
    public void ACountOperand_IsNamedCount()
    {
        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("HAVING COUNT(*) > @count", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ASumOperand_IsNamedAfterTheAggregateAndItsColumn()
    {
        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Sum((p, c) => p.UnitPrice) > 150m)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("> @sum_p_unit_price", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAvgOperand_CarriesNoCastNoiseFromTheRenderedSql()
    {
        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Avg((p, c) => p.UnitPrice) > 20.0)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("@avg_p_unit_price", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@avg_cast", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueOnTheLeftOfTheComparison_IsStillNamedFromTheAggregate()
    {
        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => 3 < g.Count())
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("HAVING @count < COUNT(*)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ACapturedLocal_IsNamedFromTheAggregateNotTheVariable()
    {
        int minimum = 3;

        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > minimum)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("HAVING COUNT(*) > @count", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("minimum", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TwoGroupingsOffOneJoin_BothCounting_TakeCountThenASuffix()
    {
        var joined = Joined();

        string first = joined.GroupBy((p, c) => p.CategoryId).Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });
        string second = joined.GroupBy((p, c) => p.CategoryId).Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("HAVING COUNT(*) > @count", first, StringComparison.Ordinal);
        Assert.EndsWith("HAVING COUNT(*) > @count_2", second, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoAggregatesInOneHaving_EachTakeTheirOwnName()
    {
        string sql = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 3 && g.Sum((p, c) => p.UnitPrice) > 150m)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("COUNT(*) > @count", sql, StringComparison.Ordinal);
        Assert.Contains("> @sum_p_unit_price", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AWhereThatAlreadyBoundTheName_PushesTheHavingOperandToASuffix()
    {
        string sql = Joined()
            .Where((p, c) => p.CategoryId == 1)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Sum((p, c) => p.UnitPrice) > 150m)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("WHERE (p.category_id = @p_category_id)", sql, StringComparison.Ordinal);
        Assert.Contains("> @sum_p_unit_price", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNamedOperandStillBindsItsValue()
    {
        var rows = Joined()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 1)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(r.Count > 1));
    }

    [Fact]
    public void SingleEntityHaving_IsNamedTheSameWay()
    {
        string sql = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Sum(p => p.UnitPrice) > 150m)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("> @sum_unit_price", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleEntityTwoGroupings_TakeTheNameThenASuffix()
    {
        var query = _fixture.Connection.From<Product>();

        string first = query.GroupBy(p => p.CategoryId).Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });
        string second = query.GroupBy(p => p.CategoryId).Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.EndsWith("HAVING COUNT(*) > @count", first, StringComparison.Ordinal);
        Assert.EndsWith("HAVING COUNT(*) > @count_2", second, StringComparison.Ordinal);
    }
}
