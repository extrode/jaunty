using System.Linq.Expressions;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact SELECT lists, aliases and messages for the single-table GROUP BY visitor, including
/// aggregates reached through a static helper rather than an <c>IGrouping</c> instance method.
/// </summary>
public class GroupByVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    public sealed class Tally(int count)
    {
        public int Count { get; } = count;
    }

    public sealed class Stats
    {
        public int Count { get; set; }
        public decimal? Total { get; set; }
    }

    private GroupByExpressionVisitor<Product, short> Visitor(params string[] keys)
        => new(_dialect, keys.Length == 0 ? ["[category_id]"] : keys);

    private string[] Select<TResult>(Expression<Func<IGrouping<short, Product>, TResult>> expr)
        => Visitor().TranslateSelect(expr).SelectColumns;

    private string Rejection<TResult>(Expression<Func<IGrouping<short, Product>, TResult>> expr)
        => Assert.Throws<NotSupportedException>(() => Select(expr)).Message;

    [Fact]
    public void ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = Visitor();
        visitor.TranslateSelect(g => new { A = g.Count() });

        var (columns, aliases) = visitor.TranslateSelect(g => new { B = g.Count() });

        Assert.Equal(["COUNT(*) AS [B]"], columns);
        Assert.Equal(["B"], aliases);
    }

    [Fact]
    public void ANonAnonymousConstructor_UsesPositionalAliases()
        => Assert.Equal(["COUNT(*) AS [Column0]"], Select(g => new Tally(g.Count())));

    [Fact]
    public void ADtoInitializer_AliasesEachAssignment()
        => Assert.Equal(["COUNT(*) AS [Count]", "SUM([unit_price]) AS [Total]"], Select(g => new Stats { Count = g.Count(), Total = g.Sum(p => p.UnitPrice) }));

    [Fact]
    public void ANestedConversion_IsUnwrapped()
        => Assert.Equal(["COUNT(*) AS [C]"], Select(g => new { C = (object)(long)g.Count() }));

    [Fact]
    public void AConstant_IsALiteral()
        => Assert.Equal(["1 AS [One]"], Select(g => new { One = 1 }));

    [Fact]
    public void ANegation_IsNamed()
        => Assert.Equal("Expression type 'Negate' is not supported in GROUP BY Select.", Rejection(g => -g.Count()));

    [Fact]
    public void AnInstanceMethodOnSomethingElse_IsNamedWithItsType()
        => Assert.Equal("Method 'ToUpper' on type 'String' is not supported in GROUP BY Select.", Rejection(g => new { X = "a".ToUpper() }));

    [Fact]
    public void AStaticMethodWithNoArguments_IsNamedWithItsType()
        => Assert.Equal("Method 'NewGuid' on type 'Guid' is not supported in GROUP BY Select.", Rejection(g => new { X = Guid.NewGuid() }));

    [Fact]
    public void AStaticMethodOverAnAggregate_IsNamedWithItsType()
        => Assert.Equal("Method 'Abs' on type 'Math' is not supported in GROUP BY Select.", Rejection(g => new { X = Math.Abs(g.Count()) }));

    [Fact]
    public void AGroupingReachedThroughConversions_IsStillTheGrouping()
        => Assert.Equal(["COUNT(*) AS [C]"], Select(g => new { C = ((IGrouping<short, Product>)(object)g).Count() }));

    [Fact]
    public void AGroupingHeldInAVariable_IsTreatedAsTheGrouping()
    {
        IGrouping<short, Product> other = null!;
        Assert.Equal(["COUNT(*) AS [C]"], Select(g => new { C = other.Count() }));
    }

    [Fact]
    public void AStaticHelperTakingTheGrouping_ReadsItsSelector()
        => Assert.Equal(
            ["COUNT(*) AS [C]", "SUM([unit_price]) AS [S]", "MIN([unit_price]) AS [Lo]"],
            Select(g => new { C = Agg.Count(g), S = Agg.Sum(g, p => p.UnitPrice), Lo = Agg.Min(g, p => p.UnitPrice) }));

    [Fact]
    public void AStaticAverage_IsAFractionalAverage()
        => Assert.Equal(
            [FractionalAverage.Generate(_dialect, "[unit_price]") + " AS [A]"],
            Select(g => new { A = Agg.Average(g, p => p.UnitPrice) }));

    [Fact]
    public void AnUnknownStaticAggregate_IsNamed()
        => Assert.Equal("Method 'Median' is not supported in GROUP BY Select.", Rejection(g => new { M = Agg.Median(g) }));

    [Fact]
    public void AConvertedSelectorBody_IsUnwrapped()
        => Assert.Equal(["SUM([product_id]) AS [S]"], Select(g => new { S = g.Sum(p => (decimal)p.ProductId) }));

    [Fact]
    public void ANegatedSelectorBody_IsRejected()
        => Assert.Equal("Cannot extract column from aggregate expression of type 'Lambda'.", Rejection(g => new { S = g.Sum(p => -p.ProductId) }));

    [Fact]
    public void AMemberBindingMessage_IsComplete()
        => Assert.Equal(
            "Member binding 'MemberBinding' is not supported in GROUP BY Select. Only member assignments (Member = expression) can be translated.",
            Rejection(g => new SelectVisitorExactShapeTests.Outer { Nested = { X = "a" } }));

    [Fact]
    public void ACompositeKeyInsideAProjection_IsRejected()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Visitor("[category_id]", "[supplier_id]").TranslateSelect(g => new { g.Key }));
        Assert.Equal(
            "A composite grouping key cannot be projected as a whole inside a projection. " +
            "Project its parts instead - g.Key.PropertyName - or select the bare key, g => g.Key.",
            ex.Message);
    }
}

internal static class Agg
{
    public static int Count<TKey, T>(IGrouping<TKey, T> g) where T : new() => throw new InvalidOperationException();
    public static int Median<TKey, T>(IGrouping<TKey, T> g) where T : new() => throw new InvalidOperationException();
    public static TResult Sum<TKey, T, TResult>(IGrouping<TKey, T> g, Expression<Func<T, TResult>> selector) where T : new() => throw new InvalidOperationException();
    public static TResult Min<TKey, T, TResult>(IGrouping<TKey, T> g, Expression<Func<T, TResult>> selector) where T : new() => throw new InvalidOperationException();
    public static double Average<TKey, T, TResult>(IGrouping<TKey, T> g, Expression<Func<T, TResult>> selector) where T : new() => throw new InvalidOperationException();
}
