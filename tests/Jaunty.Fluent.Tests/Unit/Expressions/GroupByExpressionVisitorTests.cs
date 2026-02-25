using System.Linq.Expressions;

using FluentAssertions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for GroupByExpressionVisitor.
/// Tests SQL GROUP BY and SELECT clause generation.
/// </summary>
public class GroupByExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();

    #region Simple Group By

    [Fact]
    public void TranslateSelect_WithKey_GeneratesKeyColumn()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Key;
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("[CategoryId]");
        aliases[0].Should().Be("Value");
    }

    #endregion

    #region Aggregate Functions

    [Fact]
    public void TranslateSelect_WithCount_GeneratesCountAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Count();
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("COUNT(*)");
        aliases[0].Should().Be("Value");
    }

    [Fact]
    public void TranslateSelect_WithSum_GeneratesSumAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Sum(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("SUM([unit_price])");
        aliases[0].Should().Be("Value");
    }

    [Fact]
    public void TranslateSelect_WithAvg_GeneratesAvgAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Avg(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("AVG([unit_price])");
        aliases[0].Should().Be("Value");
    }

    [Fact]
    public void TranslateSelect_WithMin_GeneratesMinAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Min(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("MIN([unit_price])");
        aliases[0].Should().Be("Value");
    }

    [Fact]
    public void TranslateSelect_WithMax_GeneratesMaxAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Max(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("MAX([unit_price])");
        aliases[0].Should().Be("Value");
    }

    #endregion

    #region Anonymous Type Projections

    [Fact]
    public void TranslateSelect_WithAnonymousType_GeneratesMultipleColumns()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new 
        { 
            CategoryId = g.Key, 
            Count = g.Count(),
            Total = g.Sum(p => p.UnitPrice)
        };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().HaveCount(3);
        aliases.Should().ContainInOrder(new[] { "CategoryId", "Count", "Total" });
    }

    [Fact]
    public void TranslateSelect_WithAnonymousType_KeyAndAggregates_GeneratesCorrectSql()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new 
        { 
            Key = g.Key, 
            ProductCount = g.Count()
        };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().HaveCount(2);
        columns[0].Should().Contain("[CategoryId]");
        columns[1].Should().Contain("COUNT(*)");
        aliases.Should().ContainInOrder(new[] { "Key", "ProductCount" });
    }

    #endregion

    #region DTO Projections

    [Fact]
    public void TranslateSelect_WithMemberInit_GeneratesColumns()
    {
        Expression<Func<IGrouping<short, Product>, CategoryStats>> expr = g => new CategoryStats
        {
            CategoryId = g.Key,
            ProductCount = g.Count()
        };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().HaveCount(2);
        aliases.Should().ContainInOrder(new[] { "CategoryId", "ProductCount" });
    }

    #endregion

    #region Count with Predicate

    [Fact]
    public void TranslateSelect_WithCountPredicate_GeneratesConditionalCount()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Count(p => p.Discontinued);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        columns.Should().ContainSingle();
        columns[0].Should().Contain("COUNT");
    }

    #endregion
}

// Helper class for DTO projection tests
public class CategoryStats
{
    public short CategoryId { get; set; }
    public int ProductCount { get; set; }
}
