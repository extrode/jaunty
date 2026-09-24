using System.Linq.Expressions;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact SELECT lists, HAVING text, HAVING parameter names and messages for the joined GROUP BY
/// visitor.
/// </summary>
public class JoinedGroupByVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    public sealed class KeyOf(short? categoryId)
    {
        public short? CategoryId { get; } = categoryId;
        public short? Other => null;
    }

    private JoinedGroupByExpressionVisitor Visitor(LambdaExpression keySelector)
        => new(
            _dialect,
            [FluentMetadataCache.GetMetadata<Product>(), FluentMetadataCache.GetMetadata<Category>()],
            [FluentMetadataCache.GetForDialect<Product>(_dialect), FluentMetadataCache.GetForDialect<Category>(_dialect)],
            ["p", "c"],
            keySelector);

    private JoinedGroupByExpressionVisitor SingleKey()
        => Visitor((Expression<Func<Product, Category, short?>>)((p, c) => p.CategoryId));

    private string[] Select<TResult>(Expression<Func<IGroupingJoined<short?, Product, Category>, TResult>> expr)
        => SingleKey().TranslateSelect(expr).SelectColumns;

    private string SelectRejection<TResult>(Expression<Func<IGroupingJoined<short?, Product, Category>, TResult>> expr)
        => Assert.Throws<NotSupportedException>(() => Select(expr)).Message;

    private (string Sql, IReadOnlyList<(string Name, object? Value)> Parameters) Having<TResult>(Expression<Func<IGroupingJoined<short?, Product, Category>, TResult>> expr)
    {
        var parameters = new ParameterCollection();
        string sql = SingleKey().TranslateHavingPredicate(expr, parameters);
        return (sql, parameters.GetAll());
    }

    private string HavingRejection<TResult>(Expression<Func<IGroupingJoined<short?, Product, Category>, TResult>> expr)
        => Assert.Throws<NotSupportedException>(() => Having(expr)).Message;

    [Fact]
    public void ABoxedHavingPredicate_IsUnwrapped()
        => Assert.Equal("COUNT(*) > @count", Having(g => (object)(g.Count() > 1)).Sql);

    [Fact]
    public void AConvertedAggregate_StillNamesTheParameter()
    {
        long n = 1;
        var (sql, parameters) = Having(g => (long)g.Count() == n);

        Assert.Equal("COUNT(*) = @count", sql);
        Assert.Equal([("@count", (object?)1L)], parameters);
    }

    [Fact]
    public void AggregateStems_NameTheirParameters()
        => Assert.Equal(
            "(((COUNT(p.[product_id]) > @count_p_product_id AND " + FractionalAverage.Generate(_dialect, "p.[unit_price]") + " > @avg_p_unit_price) AND MIN(p.[unit_price]) < @min_p_unit_price) AND MAX(p.[unit_price]) <= @max_p_unit_price)",
            Having(g => g.Count((p, c) => p.ProductId) > 1 && g.Avg((p, c) => p.UnitPrice) > 2 && g.Min((p, c) => p.UnitPrice) < 3 && g.Max((p, c) => p.UnitPrice) <= 4).Sql);

    [Fact]
    public void AnAggregateOverNoColumn_IsNamedAfterTheFunctionAlone()
        => Assert.Equal("SUM(1) >= @sum", Having(g => g.Sum((p, c) => 1) >= 5).Sql);

    [Fact]
    public void AConvertedSelectorBody_StillNamesTheParameter()
        => Assert.Equal("SUM(p.[product_id]) <> @sum_p_product_id", Having(g => g.Sum((p, c) => (int?)p.ProductId) != 5).Sql);

    [Fact]
    public void ValuesWithNoAggregateBeside_TakeUniqueNames()
    {
        int a = 2;
        var (sql, parameters) = Having(g => a > 1 || g.Count() > 0);

        Assert.Equal("(@jhp_0 > @jhp_1 OR COUNT(*) > @count)", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void AnotherMethodInHaving_IsNamed()
        => Assert.Equal("Method 'ToString' is not supported in HAVING.", HavingRejection(g => g.Count().ToString() == "1"));

    [Fact]
    public void ArithmeticInHaving_IsNamed()
        => Assert.Equal("HAVING expression type 'Add' is not supported.", HavingRejection(g => g.Count() + 1 > 2));

    [Fact]
    public void AnArithmeticHavingBody_IsNamedAsAnOperator()
        => Assert.Equal("Operator 'Add' is not supported.", HavingRejection(g => g.Count() + 1));

    [Fact]
    public void ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = SingleKey();
        visitor.TranslateSelect((Expression<Func<IGroupingJoined<short?, Product, Category>, object>>)(g => new { A = g.Count() }));

        var (columns, aliases) = visitor.TranslateSelect((Expression<Func<IGroupingJoined<short?, Product, Category>, object>>)(g => new { B = g.Count() }));

        Assert.Equal(["COUNT(*) AS [B]"], columns);
        Assert.Equal(["B"], aliases);
    }

    [Fact]
    public void ABareSingleKey_IsAliasedValue()
        => Assert.Equal(["p.[category_id] AS [Value]"], Select(g => g.Key));

    [Fact]
    public void ANonAnonymousConstructor_UsesPositionalAliases()
        => Assert.Equal(["COUNT(*) AS [Column0]"], Select(g => new GroupByVisitorExactShapeTests.Tally(g.Count())));

    [Fact]
    public void ADtoInitializer_AliasesEachAssignment()
        => Assert.Equal(
            ["COUNT(*) AS [Count]", "SUM(p.[unit_price]) AS [Total]"],
            Select(g => new GroupByVisitorExactShapeTests.Stats { Count = g.Count(), Total = g.Sum((p, c) => p.UnitPrice) }));

    [Fact]
    public void AMemberBindingMessage_IsComplete()
        => Assert.Equal(
            "Member binding 'MemberBinding' is not supported in GROUP BY Select. Only member assignments (Member = expression) can be translated.",
            SelectRejection(g => new SelectVisitorExactShapeTests.Outer { Nested = { X = "a" } }));

    [Fact]
    public void ANestedConversion_IsUnwrapped()
        => Assert.Equal(["COUNT(*) AS [C]"], Select(g => new { C = (object)(long)g.Count() }));

    [Fact]
    public void AConstant_IsALiteral()
        => Assert.Equal(["1 AS [One]"], Select(g => new { One = 1 }));

    [Fact]
    public void ANegation_IsNamed()
        => Assert.Equal("Expression type 'Negate' is not supported in GROUP BY Select.", SelectRejection(g => new { X = -g.Count() }));

    [Fact]
    public void ACompositeKeyInsideAProjection_IsRejected()
    {
        var visitor = Visitor((Expression<Func<Product, Category, object>>)((p, c) => new { p.CategoryId, c.CategoryName }));
        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect((Expression<Func<IGroupingJoined<object, Product, Category>, object>>)(g => new { g.Key })));

        Assert.Equal(
            "A composite grouping key cannot be projected as a whole inside a projection. " +
            "Project its parts instead - g.Key.PropertyName - or select the bare key, g => g.Key.",
            ex.Message);
    }

    [Fact]
    public void AKeyBuiltByAConstructor_ResolvesItsPartsByArgumentName()
    {
        var visitor = Visitor((Expression<Func<Product, Category, KeyOf>>)((p, c) => new KeyOf(p.CategoryId)));

        var (columns, _) = visitor.TranslateSelect((Expression<Func<IGroupingJoined<KeyOf, Product, Category>, object>>)(g => new { g.Key.CategoryId }));

        Assert.Equal(["p.[category_id] AS [CategoryId]"], columns);
    }

    [Fact]
    public void AnUnknownKeyPart_IsNamed()
    {
        var visitor = Visitor((Expression<Func<Product, Category, KeyOf>>)((p, c) => new KeyOf(p.CategoryId)));

        var ex = Assert.Throws<NotSupportedException>(() => visitor.TranslateSelect((Expression<Func<IGroupingJoined<KeyOf, Product, Category>, object>>)(g => new { g.Key.Other })));

        Assert.Equal("Unknown GROUP BY key property 'Other'.", ex.Message);
    }

    [Fact]
    public void AStaticMethodWithNoArguments_IsNamedWithItsType()
        => Assert.Equal("Method 'NewGuid' on type 'Guid' is not supported in GROUP BY Select.", SelectRejection(g => new { X = Guid.NewGuid() }));

    [Fact]
    public void AStaticMethodOverAnAggregate_IsNamedWithItsType()
        => Assert.Equal("Method 'Abs' on type 'Math' is not supported in GROUP BY Select.", SelectRejection(g => new { X = Math.Abs(g.Count()) }));

    [Fact]
    public void InstanceAggregates_UseTheirFunctions()
        => Assert.Equal(
            ["SUM(p.[unit_price]) AS [S]", "MIN(p.[unit_price]) AS [Lo]", "MAX(p.[unit_price]) AS [Hi]", FractionalAverage.Generate(_dialect, "p.[unit_price]") + " AS [A]"],
            Select(g => new { S = g.Sum((p, c) => p.UnitPrice), Lo = g.Min((p, c) => p.UnitPrice), Hi = g.Max((p, c) => p.UnitPrice), A = g.Avg((p, c) => p.UnitPrice) }));

    [Fact]
    public void AStaticHelperTakingTheGrouping_ReadsItsSelector()
        => Assert.Equal(
            ["COUNT(*) AS [C]", "SUM(p.[unit_price]) AS [S]", FractionalAverage.Generate(_dialect, "c.[category_id]") + " AS [A]"],
            Select(g => new { C = JAgg.Count(g), S = JAgg.Sum(g, (p, c) => p.UnitPrice), A = JAgg.Average(g, (p, c) => c.CategoryId) }));

    [Fact]
    public void AnUnknownStaticAggregate_IsNamed()
        => Assert.Equal("Method 'Median' is not supported in GROUP BY Select.", SelectRejection(g => new { M = JAgg.Median(g) }));

    [Fact]
    public void AConvertedSelectorBody_IsUnwrapped()
        => Assert.Equal(["SUM(p.[product_id]) AS [S]"], Select(g => new { S = g.Sum((p, c) => (decimal)p.ProductId) }));

    [Fact]
    public void AConstantSelectorBody_IsALiteral()
        => Assert.Equal(["SUM(1) AS [S]"], Select(g => new { S = g.Sum((p, c) => 1) }));

    [Fact]
    public void ANegatedSelectorBody_IsRejected()
        => Assert.Equal("Cannot extract column from HAVING/GROUP BY aggregate expression.", SelectRejection(g => new { S = g.Sum((p, c) => -p.ProductId) }));

    [Fact]
    public void AnEntityReachedThroughConversions_IsStillThatEntity()
        => Assert.Equal(["SUM(p.[unit_price]) AS [S]"], Select(g => new { S = g.Sum((p, c) => ((Product)(object)p).UnitPrice) }));

    [Fact]
    public void AGroupingReachedThroughConversions_IsStillTheGrouping()
        => Assert.Equal(["COUNT(*) AS [C]"], Select(g => new { C = ((IGroupingJoined<short?, Product, Category>)(object)g).Count() }));

    [Fact]
    public void AKeyOverAParameterNotInTheSelector_IsNamed()
    {
        var p = Expression.Parameter(typeof(Product), "p");
        var c = Expression.Parameter(typeof(Category), "c");
        var stray = Expression.Property(Expression.Parameter(typeof(Product), "x"), nameof(Product.CategoryId));

        var ex = Assert.Throws<NotSupportedException>(() => Visitor(Expression.Lambda(stray, p, c)));

        Assert.Equal("Cannot resolve which joined entity member 'CategoryId' belongs to.", ex.Message);
    }
}

internal static class JAgg
{
    public static int Count<TKey, T1, T2>(IGroupingJoined<TKey, T1, T2> g) where T1 : new() where T2 : new() => throw new InvalidOperationException();
    public static int Median<TKey, T1, T2>(IGroupingJoined<TKey, T1, T2> g) where T1 : new() where T2 : new() => throw new InvalidOperationException();
    public static TResult Sum<TKey, T1, T2, TResult>(IGroupingJoined<TKey, T1, T2> g, Expression<Func<T1, T2, TResult>> selector) where T1 : new() where T2 : new() => throw new InvalidOperationException();
    public static double Average<TKey, T1, T2, TResult>(IGroupingJoined<TKey, T1, T2> g, Expression<Func<T1, T2, TResult>> selector) where T1 : new() where T2 : new() => throw new InvalidOperationException();
}
