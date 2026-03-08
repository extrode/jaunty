using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

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

        var column = Assert.Single(columns);
        Assert.Contains("[CategoryId]", column);
        Assert.Equal("Value", aliases[0]);
    }

    #endregion

    #region Aggregate Functions

    [Fact]
    public void TranslateSelect_WithCount_GeneratesCountAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Count();
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("COUNT(*)", column);
        Assert.Equal("Value", aliases[0]);
    }

    [Fact]
    public void TranslateSelect_WithSum_GeneratesSumAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Sum(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("SUM([unit_price])", column);
        Assert.Equal("Value", aliases[0]);
    }

    [Fact]
    public void TranslateSelect_WithAvg_GeneratesAvgAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Avg(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("AVG([unit_price])", column);
        Assert.Equal("Value", aliases[0]);
    }

    [Fact]
    public void TranslateSelect_WithMin_GeneratesMinAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Min(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("MIN([unit_price])", column);
        Assert.Equal("Value", aliases[0]);
    }

    [Fact]
    public void TranslateSelect_WithMax_GeneratesMaxAggregate()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Max(p => p.UnitPrice);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("MAX([unit_price])", column);
        Assert.Equal("Value", aliases[0]);
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

        Assert.Equal(3, columns.Length);
        Assert.Equal(new[] { "CategoryId", "Count", "Total" }, aliases);
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

        Assert.Equal(2, columns.Length);
        Assert.Contains("[CategoryId]", columns[0]);
        Assert.Contains("COUNT(*)", columns[1]);
        Assert.Equal(new[] { "Key", "ProductCount" }, aliases);
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

        Assert.Equal(2, columns.Length);
        Assert.Equal(new[] { "CategoryId", "ProductCount" }, aliases);
    }

    #endregion

    #region Count with Predicate

    [Fact]
    public void TranslateSelect_WithCountPredicate_GeneratesConditionalCount()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => g.Count(p => p.Discontinued);
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[CategoryId]" });
        var (columns, aliases) = visitor.TranslateSelect(expr);

        var column = Assert.Single(columns);
        Assert.Contains("COUNT", column);
    }

    #endregion
}

// Helper class for DTO projection tests
public class CategoryStats
{
    public short CategoryId { get; set; }
    public int ProductCount { get; set; }
}