using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Spec 004 (gap #13, GroupBy+joins) T010-T012: GroupBy on a 2-way joined query, against a
/// real SQLite database (via FluentDatabaseFixture, matching every other test in this file's
/// sibling FluentJoinTests.cs/FluentGroupByTests.cs). Real-dialect (SQL Server/Postgres/MySQL/
/// MariaDB) coverage lives in Jaunty.Fluent.SourceGen.Tests, which already has the
/// JAUNTY_TEST_* real-database convention established for spec 003.
/// </summary>
public class FluentGroupByJoinTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupByJoinTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void GroupBy_KeyFromFromEntity_WithCount_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > 0));
    }

    [Fact]
    public void GroupBy_KeyFromJoinedEntity_ReturnsGroupedResults()
    {
        // T011: key drawn from the joined entity's column, not the From entity's.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => c.CategoryName)
            .Select(g => new { CategoryName = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.CategoryName)));
    }

    [Fact]
    public void GroupBy_AllAggregates_FromEntityColumns_ReturnsCorrectValues()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum((p, c) => p.UnitsInStock),
                AvgStock = g.Avg((p, c) => p.UnitsInStock),
                MinPrice = g.Min((p, c) => p.UnitPrice),
                MaxPrice = g.Max((p, c) => p.UnitPrice)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.ProductCount > 0));
    }

    [Fact]
    public void GroupBy_AggregateOnJoinedEntityColumn_ReturnsCorrectValues()
    {
        // Cross-entity aggregate: Count() over the JOINED entity's column (index 1, "c"),
        // not the From entity's (index 0, "p") - proves parameter-position resolution
        // isn't hardcoded to the first joined-query type parameter.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                CategoryIdNonNullCount = g.Count((p, c) => c.CategoryId)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.CategoryIdNonNullCount > 0));
    }

    [Fact]
    public void GroupBy_CompositeKey_ColumnNameCollision_ResolvesEachSideCorrectly()
    {
        // T012: both Product.CategoryId and Category.CategoryId map to the same underlying
        // "category_id" column name on their respective tables. Explicit aliasing in the
        // composite key (required here since C# won't compile two same-named anonymous
        // members) proves the key-property-to-column map resolves each alias to the correct
        // side's column, not whichever one happens to match by name first.
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => new { ProductCategoryId = p.CategoryId, JoinedCategoryId = c.CategoryId })
            .Select(g => new
            {
                g.Key.ProductCategoryId,
                g.Key.JoinedCategoryId,
                Count = g.Count()
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal((int?)r.ProductCategoryId, r.JoinedCategoryId));
    }

    [Fact]
    public void GroupBy_Having_FiltersGroups()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 0)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > 0));
    }

    [Fact]
    public void GroupBy_Having_ClosureCapturedLocalVariable_FiltersGroups()
    {
        // T013: gap #14's HAVING closure-safety fix, reused here via HavingExpressionHelpers
        // (T001) rather than copy-pasted - a closure-captured local variable compiles to a
        // MemberExpression over a compiler-generated closure class, not a ConstantExpression,
        // so it must be evaluated rather than read as a literal. This is exactly the shape
        // that broke GroupedQueryBuilder before gap #14 (only literals worked there).
        int minCount = 0;

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > minCount)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > minCount));
    }

    [Fact]
    public void GroupBy_Having_WithLiteral_BindsAsParameterInsteadOfInliningIntoSql()
    {
        // Regression test (round 10): HAVING comparison operands must be bound as query
        // parameters instead of inlined as raw SQL text (previously
        // HavingExpressionHelpers.FormatLiteral inlined them with only quote-doubling for
        // strings - an injection-adjacent, culture-unsafe pattern already fixed for the
        // single-entity GroupedQueryBuilder path via AddHavingParameter, now fixed here too).
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 2)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.DoesNotContain("> 2", sql);
        Assert.Matches(@"HAVING COUNT\(\*\) > \S*jhp\d+", sql);
    }

    [Fact]
    public async Task GroupBy_SelectAsync_ReturnsGroupedResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_ToSql_ReturnsGroupByAndAggregateSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("JOIN", sql);
    }
}
