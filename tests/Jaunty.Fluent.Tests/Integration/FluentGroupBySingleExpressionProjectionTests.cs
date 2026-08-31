using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R34-005. A grouped projection whose body is a single expression - <c>.Select(g =&gt; g.Key)</c>
/// or <c>.Select(g =&gt; g.Count())</c> - was emitted into the SELECT list without an
/// <c>AS &lt;alias&gt;</c>, while the translator still reported the alias "Value" to the caller. The
/// mappers resolve every column by that reported name, so the lookup could never match the real
/// column name (<c>category_id</c>, or nothing at all for <c>COUNT(*)</c>). For a value-typed
/// <c>TResult</c> - the ordinary shape for both of these - the per-alias loop simply found nothing
/// and returned <c>Activator.CreateInstance&lt;TResult&gt;()</c>: <b>every row mapped to 0</b>,
/// silently, behind a query that ran correctly. The composite-key branch immediately above already
/// appended its aliases, which is what made the omission an oversight rather than a decision.
/// <para>
/// Asserting "not empty" would have passed throughout - each test here asserts the values.
/// </para>
/// </summary>
public class FluentGroupBySingleExpressionProjectionTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupBySingleExpressionProjectionTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void GroupBy_BareKeyProjection_ReturnsTheKeysRatherThanZeroes()
    {
        List<short?> keys = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => g.Key);

        List<short?> expected = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key })
            .Select(r => r.CategoryId)
            .ToList();

        Assert.NotEmpty(keys);
        Assert.Equal(expected.OrderBy(k => k), keys.OrderBy(k => k));
    }

    [Fact]
    public void GroupBy_BareCountProjection_ReturnsTheCountsRatherThanZeroes()
    {
        List<int> counts = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => g.Count());

        Assert.NotEmpty(counts);
        Assert.All(counts, c => Assert.True(c > 0, $"expected a positive group count, got {c}"));
    }

    [Fact]
    public void GroupBy_BareSumProjection_ReturnsTheSumsRatherThanZeroes()
    {
        List<short?> sums = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => g.Sum(p => p.UnitsInStock));

        Assert.NotEmpty(sums);
        Assert.Contains(sums, s => s > 0);
    }

    [Fact]
    public void GroupByJoin_BareCountProjection_ReturnsTheCountsRatherThanZeroes()
    {
        List<int> counts = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => c.CategoryName)
            .Select(g => g.Count());

        Assert.NotEmpty(counts);
        Assert.All(counts, c => Assert.True(c > 0, $"expected a positive group count, got {c}"));
    }

    [Fact]
    public void GroupByJoin_BareKeyProjection_ReturnsTheKeysRatherThanNulls()
    {
        List<string> names = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => c.CategoryName)
            .Select(g => g.Key);

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.False(string.IsNullOrEmpty(n)));
    }
}
