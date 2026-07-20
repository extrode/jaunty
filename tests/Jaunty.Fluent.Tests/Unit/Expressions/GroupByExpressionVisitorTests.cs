using System.Linq.Expressions;

using Jaunty.Dialects;
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

    #region Composite Key Projections

    [Fact]
    public void TranslateSelect_WithCompositeKey_BareKeyProjection_EmitsAllKeyColumns()
    {
        // Bare `g => g.Key` over a composite grouping key must emit every GROUP BY column,
        // not silently drop all but the first (only the `g.Key.Property` form used to handle
        // composite keys before this fix). Aliases are positional ("Key0", "Key1", ...) rather
        // than the real property names ("CategoryId", "SupplierId"): no expression tree
        // describes a bare `g.Key` access, so the real names aren't recoverable without
        // reflection, which this project doesn't use outside Jaunty.Extensions.Reflection -
        // matching the same convention already used for the single-column bare-`g.Key` case,
        // which aliases as the generic placeholder "Value".
        var (columns, aliases) = TranslateBareCompositeKey(
            p => new { p.CategoryId, p.SupplierId },
            new[] { "[category_id]", "[supplier_id]" },
            _dialect);

        Assert.Equal(2, columns.Length);
        Assert.Contains("[category_id]", columns[0]);
        Assert.Contains("[supplier_id]", columns[1]);
        Assert.Equal(new[] { "Key0", "Key1" }, aliases);
    }

    /// <summary>
    /// Translates a bare `g => g.Key` selector over a composite key built by
    /// <paramref name="keySelector"/> - only used here as a type witness so TKey (an anonymous
    /// type not otherwise nameable from a test) can be inferred at the call site.
    /// </summary>
    private static (string[] Columns, string[] Aliases) TranslateBareCompositeKey<TKey>(
        Expression<Func<Product, TKey>> keySelector, string[] groupByColumns, ISqlDialect dialect)
    {
        _ = keySelector;
        var visitor = new GroupByExpressionVisitor<Product, TKey>(dialect, groupByColumns);
        Expression<Func<IGrouping<TKey, Product>, object>> selectExpr = g => g.Key;
        return visitor.TranslateSelect(selectExpr);
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
        Assert.Contains("COUNT([discontinued])", column);
        Assert.DoesNotContain("COUNT(*)", column);
    }

    #endregion
}

// Helper class for DTO projection tests
public class CategoryStats
{
    public short CategoryId { get; set; }
    public int ProductCount { get; set; }
}