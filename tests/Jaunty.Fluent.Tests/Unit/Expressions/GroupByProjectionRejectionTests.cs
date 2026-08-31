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
/// AUD-R35-193, 194 and 195 on <c>GroupByExpressionVisitor</c> and its joined twin.
/// </summary>
public class GroupByProjectionRejectionTests
{
    private readonly TestDialect _dialect = new();
    private readonly EntityMetadata[] _metadata =
    {
        FluentMetadataCache.GetMetadata<Product>(),
        FluentMetadataCache.GetMetadata<Category>()
    };

    private readonly CachedDialectMetadata[] _cachedMetadata;

    public GroupByProjectionRejectionTests()
    {
        _cachedMetadata =
        [
            FluentMetadataCache.GetForDialect<Product>(_dialect),
            FluentMetadataCache.GetForDialect<Category>(_dialect)
        ];
    }

    private JoinedGroupByExpressionVisitor CompositeJoinedVisitor()
    {
        Expression<Func<Product, Category, object>> keySelector = (p, c) => new { p.CategoryId, c.CategoryName };
        return new JoinedGroupByExpressionVisitor(_dialect, _metadata, _cachedMetadata, new[] { "p", "c" }, keySelector);
    }

    private JoinedGroupByExpressionVisitor SingleKeyJoinedVisitor()
    {
        Expression<Func<Product, Category, object>> keySelector = (p, c) => p.CategoryId;
        return new JoinedGroupByExpressionVisitor(_dialect, _metadata, _cachedMetadata, new[] { "p", "c" }, keySelector);
    }

    // ------------------------------------------------------------------
    // AUD-R35-193 - a composite key nested inside a projection
    // ------------------------------------------------------------------

    [Fact]
    public void CompositeKeyInsideAnAnonymousProjection_IsRejected()
    {
        Expression<Func<IGrouping<object, Product>, object>> expr = g => new { g.Key, Count = g.Count() };
        var visitor = new GroupByExpressionVisitor<Product, object>(_dialect, new[] { "[category_id]", "[supplier_id]" });

        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect(expr));

        Assert.Contains("composite grouping key", ex.Message);
        Assert.Contains("g.Key.PropertyName", ex.Message);
    }

    [Fact]
    public void CompositeKeyInsideADtoProjection_IsRejected()
    {
        Expression<Func<IGrouping<object, Product>, KeyHolder>> expr = g => new KeyHolder { Key = g.Key };
        var visitor = new GroupByExpressionVisitor<Product, object>(_dialect, new[] { "[category_id]", "[supplier_id]" });

        Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect(expr));
    }

    [Fact]
    public void SingleColumnKeyInsideAProjection_IsStillTranslated()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new { CategoryId = g.Key, Count = g.Count() };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var (columns, aliases) = visitor.TranslateSelect(expr);

        Assert.Equal(2, columns.Length);
        Assert.Contains("[category_id]", columns[0]);
        Assert.Equal(new[] { "CategoryId", "Count" }, aliases);
    }

    [Fact]
    public void BareCompositeKey_StillEmitsEveryKeyColumn()
    {
        Expression<Func<IGrouping<object, Product>, object>> expr = g => g.Key;
        var visitor = new GroupByExpressionVisitor<Product, object>(_dialect, new[] { "[category_id]", "[supplier_id]" });

        var (columns, aliases) = visitor.TranslateSelect(expr);

        Assert.Equal(2, columns.Length);
        Assert.Equal(new[] { "Key0", "Key1" }, aliases);
    }

    [Fact]
    public void Joined_CompositeKeyInsideAProjection_IsRejected()
    {
        Expression<Func<IGroupingJoined<object, Product, Category>, object>> expr =
            g => new { g.Key, Count = g.Count() };

        var ex = Assert.Throws<NotSupportedException>(() => CompositeJoinedVisitor().TranslateSelect(expr));

        Assert.Contains("composite grouping key", ex.Message);
    }

    [Fact]
    public void Joined_SingleColumnKeyInsideAProjection_IsStillTranslated()
    {
        Expression<Func<IGroupingJoined<short, Product, Category>, object>> expr =
            g => new { CategoryId = g.Key, Count = g.Count() };

        var (columns, aliases) = SingleKeyJoinedVisitor().TranslateSelect(expr);

        Assert.Equal(2, columns.Length);
        Assert.Contains("p.[category_id]", columns[0]);
        Assert.Equal(new[] { "CategoryId", "Count" }, aliases);
    }

    // ------------------------------------------------------------------
    // AUD-R35-194 - an aggregate selector that is not an inline lambda
    // ------------------------------------------------------------------

    [Fact]
    public void AggregateOverASelectorVariable_IsRejectedInsteadOfEmittingTheVariableName()
    {
        Expression<Func<Product, short?>> selector = p => p.UnitsInStock;
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new { Total = g.Sum(selector) };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect(expr));

        Assert.Contains("Cannot extract column from aggregate expression", ex.Message);
        Assert.DoesNotContain("selector", ex.Message);
    }

    [Fact]
    public void AggregateOverAnInlineLambda_IsStillTranslated()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new { Total = g.Sum(p => p.UnitsInStock) };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var (columns, _) = visitor.TranslateSelect(expr);

        Assert.Contains("SUM([units_in_stock])", columns[0]);
    }

    [Fact]
    public void AggregateOverAConstant_IsStillTranslated()
    {
        Expression<Func<IGrouping<short, Product>, object>> expr = g => new { Total = g.Sum(p => 1) };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var (columns, _) = visitor.TranslateSelect(expr);

        Assert.Contains("SUM(1)", columns[0]);
    }

    [Fact]
    public void Joined_AggregateOverASelectorVariable_IsRejected()
    {
        Expression<Func<Product, Category, short?>> selector = (p, c) => p.UnitsInStock;
        Expression<Func<IGroupingJoined<short, Product, Category>, object>> expr =
            g => new { Total = g.Sum(selector) };

        Assert.Throws<NotSupportedException>(() => SingleKeyJoinedVisitor().TranslateSelect(expr));
    }

    // ------------------------------------------------------------------
    // AUD-R35-195 - a binding that is not a member assignment
    // ------------------------------------------------------------------

    [Fact]
    public void NestedMemberBinding_IsRejected()
    {
        Expression<Func<IGrouping<short, Product>, NestedStats>> expr =
            g => new NestedStats { Nested = { Count = g.Count() } };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect(expr));

        Assert.Contains("MemberBinding", ex.Message);
        Assert.Contains("GROUP BY Select", ex.Message);
    }

    [Fact]
    public void ListBinding_IsRejected()
    {
        Expression<Func<IGrouping<short, Product>, ListStats>> expr =
            g => new ListStats { Counts = { 1, 2 } };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect(expr));

        Assert.Contains("ListBinding", ex.Message);
    }

    [Fact]
    public void AssignmentBindings_AreStillTranslated()
    {
        Expression<Func<IGrouping<short, Product>, FlatStats>> expr =
            g => new FlatStats { CategoryId = g.Key, Count = g.Count() };
        var visitor = new GroupByExpressionVisitor<Product, short>(_dialect, new[] { "[category_id]" });

        var (columns, aliases) = visitor.TranslateSelect(expr);

        Assert.Equal(2, columns.Length);
        Assert.Equal(new[] { "CategoryId", "Count" }, aliases);
    }

    [Fact]
    public void Joined_NestedMemberBinding_IsRejected()
    {
        Expression<Func<IGroupingJoined<short, Product, Category>, NestedStats>> expr =
            g => new NestedStats { Nested = { Count = g.Count() } };

        var ex = Assert.Throws<NotSupportedException>(() => SingleKeyJoinedVisitor().TranslateSelect(expr));

        Assert.Contains("MemberBinding", ex.Message);
    }

    [Fact]
    public void Joined_AssignmentBindings_AreStillTranslated()
    {
        Expression<Func<IGroupingJoined<short, Product, Category>, FlatStats>> expr =
            g => new FlatStats { CategoryId = g.Key, Count = g.Count() };

        var (columns, aliases) = SingleKeyJoinedVisitor().TranslateSelect(expr);

        Assert.Equal(2, columns.Length);
        Assert.Equal(new[] { "CategoryId", "Count" }, aliases);
    }

    private sealed class KeyHolder
    {
        public object? Key { get; set; }
    }

    private sealed class FlatStats
    {
        public short CategoryId { get; set; }
        public int Count { get; set; }
    }

    private sealed class InnerStats
    {
        public int Count { get; set; }
    }

    private sealed class NestedStats
    {
        public InnerStats Nested { get; } = new();
    }

    private sealed class ListStats
    {
        public List<int> Counts { get; } = new();
    }
}
