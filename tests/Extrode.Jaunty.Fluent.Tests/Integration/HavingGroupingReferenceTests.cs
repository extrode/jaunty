using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-030: a HAVING operand that still refers to the grouping was compiled over the unbound
/// grouping parameter and failed with the evaluator's "referenced from scope ''" message.
/// </summary>
public class HavingGroupingReferenceTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public HavingGroupingReferenceTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void AKeyComparison_IsRefusedByName()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Key > 5)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() }));

        Assert.Contains("cannot be translated in HAVING", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANegatedAggregateComparison_IsRefusedByName()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => !(g.Count() > 5))
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() }));

        Assert.Contains("cannot be translated in HAVING", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACapturedValue_StillBinds()
    {
        int minCount = 1;

        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > minCount)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > minCount));
    }

    [Fact]
    public void AJoinedKeyComparison_IsRefusedByName()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Key > 5)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() }));

        Assert.Contains("cannot be translated in HAVING", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AJoinedCapturedValue_StillBinds()
    {
        int minCount = 1;

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > minCount)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > minCount));
    }
}
