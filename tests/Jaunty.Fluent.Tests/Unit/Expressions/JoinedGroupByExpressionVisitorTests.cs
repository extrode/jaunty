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
        var visitor = new JoinedGroupByExpressionVisitor(_dialect, _metadata, new[] { "p", "c" }, keySelector);

        Expression<Func<IGroupingJoined<object, Product, Category>, object>> selectExpr = g => g.Key;
        var (columns, aliases) = visitor.TranslateSelect(selectExpr);

        Assert.Equal(2, columns.Length);
        Assert.Contains("p.[category_id]", columns[0]);
        Assert.Contains("c.[category_name]", columns[1]);
        Assert.Equal(new[] { "Key0", "Key1" }, aliases);
    }
}
