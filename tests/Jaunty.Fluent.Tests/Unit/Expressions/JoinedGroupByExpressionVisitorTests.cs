using System.Linq.Expressions;

using Jaunty.Fluent;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Jaunty.Internals.Entity;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for JoinedGroupByExpressionVisitor.
/// </summary>
public class JoinedGroupByExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();
    private readonly EntityMetadata[] _metadata =
    {
        FluentMetadataCache.GetMetadata<Product>(),
        FluentMetadataCache.GetMetadata<Category>()
    };

    // AUD-R26-058: parallel to _metadata, in the same order. The visitor is non-generic - one
    // implementation serves arities 2, 3 and 4 - so its callers supply the pre-escaped per-dialect
    // metadata rather than it resolving the cache entries itself.
    private readonly CachedDialectMetadata[] _cachedMetadata;

    public JoinedGroupByExpressionVisitorTests()
    {
        _cachedMetadata =
        [
            FluentMetadataCache.GetForDialect<Product>(_dialect),
            FluentMetadataCache.GetForDialect<Category>(_dialect)
        ];
    }

    [Fact]
    public void TranslateSelect_WithCompositeKey_BareKeyProjection_EmitsAllKeyColumns()
    {
        // AUD-R18: bare `g => g.Key` over a composite grouping key used to silently emit only
        // the first GROUP BY column instead of all of them - mirrors the same fix already made
        // to the single-entity GroupByExpressionVisitor (see
        // GroupByExpressionVisitorTests.TranslateSelect_WithCompositeKey_BareKeyProjection_EmitsAllKeyColumns).
        // Aliases are positional ("Key0", "Key1", ...) for the same reason as the single-entity
        // case: a bare `g.Key` access has no expression tree describing the composite type's
        // real property names.
        Expression<Func<Product, Category, object>> keySelector = (p, c) => new { p.CategoryId, c.CategoryName };
        var visitor = new JoinedGroupByExpressionVisitor(_dialect, _metadata, _cachedMetadata, new[] { "p", "c" }, keySelector);

        Expression<Func<IGroupingJoined<object, Product, Category>, object>> selectExpr = g => g.Key;
        var (columns, aliases) = visitor.TranslateSelect(selectExpr);

        Assert.Equal(2, columns.Length);
        Assert.Contains("p.[category_id]", columns[0]);
        Assert.Contains("c.[category_name]", columns[1]);
        Assert.Equal(new[] { "Key0", "Key1" }, aliases);
    }

    [Fact]
    public void TranslateHavingPredicate_BareAggregateWithoutComparison_TranslatesOperandDirectly()
    {
        // AUD-R20: a top-level HAVING predicate that isn't a BinaryExpression (e.g. a bare
        // aggregate call with no comparison) used to throw NotSupportedException instead of
        // falling through to TranslateHavingOperand, unlike the single-entity
        // GroupedQueryBuilder.TranslateHavingExpression it's meant to mirror.
        Expression<Func<Product, Category, object>> keySelector = (p, c) => new { p.CategoryId };
        var visitor = new JoinedGroupByExpressionVisitor(_dialect, _metadata, _cachedMetadata, new[] { "p", "c" }, keySelector);

        Expression<Func<IGroupingJoined<object, Product, Category>, int>> havingExpr = g => g.Count();
        var (sql, parameters) = visitor.TranslateHavingPredicate(havingExpr);

        Assert.Equal("COUNT(*)", sql);
        Assert.Empty(parameters);
    }
}
