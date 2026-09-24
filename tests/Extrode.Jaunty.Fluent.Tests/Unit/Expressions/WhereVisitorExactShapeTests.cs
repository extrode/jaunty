using System.Linq.Expressions;
using System.Reflection;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact WHERE text, parameter names and values, and full rejection messages for the shapes the
/// WHERE visitor reaches outside its column-against-value fast path.
/// </summary>
public class WhereVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    public sealed class Bag
    {
        public bool Contains(int value) => false;
    }

    private static CaseBuilder<Product, int> GetCase() => throw new InvalidOperationException();

    private (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<Product, bool>> expr)
        => new WhereExpressionVisitor<Product>(_dialect).Translate(expr);

    private string SqlOf(Expression<Func<Product, bool>> expr) => Translate(expr).Sql;

    private string Rejection(Expression<Func<Product, bool>> expr)
        => Assert.Throws<NotSupportedException>(() => Translate(expr)).Message;

    [Fact]
    public void ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        visitor.Translate(p => p.ProductName == "a");

        var (sql, parameters) = visitor.Translate(p => p.ProductName == "b");

        Assert.Equal("([product_name] = @product_name2)", sql);
        Assert.Equal([("@product_name2", (object?)"b")], parameters);
    }

    [Fact]
    public void NullOnTheLeftOfAFunction_IsIsNull()
        => Assert.Equal("(UPPER([product_name]) IS NULL)", SqlOf(p => null == Sql.Upper(p.ProductName)));

    [Fact]
    public void NullOnTheLeftOfAFunction_NotEqual_IsIsNotNull()
        => Assert.Equal("(UPPER([product_name]) IS NOT NULL)", SqlOf(p => null != Sql.Upper(p.ProductName)));

    [Theory]
    [InlineData(false, "[product_name] LIKE @product_name ESCAPE '\\'")]
    [InlineData(true, "LOWER([product_name]) LIKE LOWER(@product_name) ESCAPE '\\'")]
    public void Contains_IsALikeWithTheContainsPattern(bool ignoreCase, string expected)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var (sql, parameters) = Translate(p => p.ProductName.Contains("a", comparison));

        Assert.Equal(expected, sql);
        Assert.Equal([("@product_name", (object?)"%a%")], parameters);
    }

    [Theory]
    [InlineData(false, "[product_name] LIKE @product_name ESCAPE '\\'")]
    [InlineData(true, "LOWER([product_name]) LIKE LOWER(@product_name) ESCAPE '\\'")]
    public void StartsWith_IsALikeWithTheStartsWithPattern(bool ignoreCase, string expected)
    {
        var comparison = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
        var (sql, parameters) = Translate(p => p.ProductName.StartsWith("a", comparison));

        Assert.Equal(expected, sql);
        Assert.Equal([("@product_name", (object?)"a%")], parameters);
    }

    [Theory]
    [InlineData(false, "[product_name] LIKE @product_name ESCAPE '\\'")]
    [InlineData(true, "LOWER([product_name]) LIKE LOWER(@product_name) ESCAPE '\\'")]
    public void EndsWith_IsALikeWithTheEndsWithPattern(bool ignoreCase, string expected)
    {
        var comparison = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var (sql, parameters) = Translate(p => p.ProductName.EndsWith("a", comparison));

        Assert.Equal(expected, sql);
        Assert.Equal([("@product_name", (object?)"%a")], parameters);
    }

    [Fact]
    public void ANullSearchValue_IsAnEmptyPattern()
    {
        string? none = null;

        Assert.Equal([("@product_name", (object?)"%%")], Translate(p => p.ProductName.Contains(none!)).Parameters);
        Assert.Equal([("@product_name", (object?)"%")], Translate(p => p.ProductName.StartsWith(none!)).Parameters);
        Assert.Equal([("@product_name", (object?)"%")], Translate(p => p.ProductName.EndsWith(none!)).Parameters);
    }

    private static string CannotTranslate(MethodCallExpression node)
        => $"Cannot translate '{node}' to SQL. " +
           $"'{node.Method.DeclaringType?.Name}.{node.Method.Name}' is not supported in a Where " +
           "expression in this position. String methods (Contains, StartsWith, EndsWith, ToUpper, " +
           "ToLower, Trim, Substring) are translated only when applied directly to a mapped " +
           "property, so 'p.Name.ToUpper()' translates but 'p.Name.ToUpper().Contains(...)' does " +
           "not - a chained call's receiver is another method call, not a column. Rewrite the " +
           "condition to use a single method call on the property, or express it as raw SQL.";

    [Fact]
    public void AChainedStringCall_IsRejectedWithTheFullExplanation()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.ToUpper().Contains("A");
        Assert.Equal(CannotTranslate((MethodCallExpression)expr.Body), Rejection(expr));
    }

    [Fact]
    public void AStaticContainsOutsideMemoryExtensions_IsNotTreatedAsIn()
    {
        Expression<Func<Product, bool>> expr = p => WhereHelpers.Contains(new[] { 1 }, p.ProductId);
        Assert.Equal(CannotTranslate((MethodCallExpression)expr.Body), Rejection(expr));
    }

    [Fact]
    public void AnInstanceContainsOnANonCollection_IsNotTreatedAsIn()
    {
        var bag = new Bag();
        Expression<Func<Product, bool>> expr = p => bag.Contains(p.ProductId);
        Assert.Equal(CannotTranslate((MethodCallExpression)expr.Body), Rejection(expr));
    }

    [Fact]
    public void ContainsOverAComputedValue_IsNotTreatedAsIn()
    {
        var ids = new List<int> { 1 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId + 1);
        Assert.Equal(CannotTranslate((MethodCallExpression)expr.Body), Rejection(expr));
    }

    [Fact]
    public void AClosedBooleanCall_IsAConstantPredicate()
    {
        string? none = null;
        string some = "x";

        Assert.Equal("1 = 1", SqlOf(p => string.IsNullOrEmpty(none)));
        Assert.Equal("1 = 0", SqlOf(p => string.IsNullOrEmpty(some)));
    }

    [Fact]
    public void ClosedValuesBesideAFunction_AreBoundAsValue()
    {
        int limit = 3;
        var (sql, parameters) = Translate(p => Sql.Length(p.ProductName) > Math.Max(1, 2) && Sql.Length(p.ProductName) < limit);

        Assert.Equal("((LEN([product_name]) > @Value) AND (LEN([product_name]) < @Value2))", sql);
        Assert.Equal([("@Value", (object?)2), ("@Value2", 3)], parameters);
    }

    [Fact]
    public void ACapturedBool_IsBoundNotTreatedAsAColumn()
    {
        bool flag = true;
        var (sql, parameters) = Translate(p => flag);

        Assert.Equal("@Value", sql);
        Assert.Equal([("@Value", (object?)true)], parameters);
    }

    [Fact]
    public void ABareCase_IsRejected()
        => Assert.Equal("Sql.Case() must be followed by .When() and .Else() or .End()", Rejection(p => Sql.Case<Product, int>() != null));

    [Fact]
    public void AWindowFunction_IsRejected()
        => Assert.Equal("SQL function 'RowNumber' is not supported.", Rejection(p => Sql.RowNumber<Product>() != null));

    [Fact]
    public void ACaseChainRootedInAnotherMethod_IsNamed()
        => Assert.Equal("Unexpected method 'GetCase' in CASE expression chain.", Rejection(p => GetCase().When(x => x.Discontinued, 1).End() == 1));

    [Fact]
    public void ACaseWithNoWhen_IsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Translate(p => Sql.Case<Product, int>().End() == 1));
        Assert.Equal("CASE expression requires at least one WHEN clause.", ex.Message);
    }

    [Fact]
    public void ANegatedFunctionArgument_IsRejected()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Coalesce(-p.ProductName.Length, 0) == 1;
        var arg = ((MethodCallExpression)((BinaryExpression)expr.Body).Left).Arguments[0];

        Assert.Equal(
            $"Cannot translate '{arg}' to SQL as an argument to a Sql.* function. Only a mapped " +
            "property, a nested Sql.* call, a ?? expression, or a value that does not reference " +
            "the lambda parameter can appear there.",
            Rejection(expr));
    }

    [Fact]
    public void ANegatedColumn_IsRejected()
        => Assert.Equal(
            "Unary operator 'Negate' is not supported in WHERE predicates. Compute the value before the query, or express the condition without it.",
            Rejection(p => -p.ProductId > 0));

    [Fact]
    public void Coalesce_IsNamedAsAnUnsupportedOperator()
        => Assert.Equal("Operator Coalesce is not supported in WHERE expressions.", Rejection(p => (p.UnitPrice ?? 0) > 5));

#pragma warning disable CS0464
    [Fact]
    public void ANullBesideAFunctionInAnOrdering_IsTheNullLiteral()
        => Assert.Equal("(LEN([product_name]) > NULL)", SqlOf(p => (int?)Sql.Length(p.ProductName) > null));
#pragma warning restore CS0464

    [Fact]
    public void BooleanConstants_AreConstantPredicates()
    {
        Assert.Equal("1 = 1", SqlOf(p => true));
        Assert.Equal("1 = 0", SqlOf(p => false));
    }

    [Fact]
    public void StringLength_IsTheLengthFunction()
    {
        var (sql, parameters) = Translate(p => p.ProductName.Length > 3);

        Assert.Equal("(LEN([product_name]) > @Value)", sql);
        Assert.Equal([("@Value", (object?)3)], parameters);
    }

    [Fact]
    public void Ternary_IsRejected()
        => Assert.Equal(
            "Conditional (ternary) expressions are not supported in WHERE predicates. Use Sql.Case(...) for a CASE WHEN, or split the predicate into separate conditions.",
            Rejection(p => p.Discontinued ? p.ProductId > 1 : p.ProductId < 1));

    [Fact]
    public void ATypeTest_IsRejected()
        => Assert.Equal(
            "Type tests ('is', 'as') are not supported in WHERE predicates. There is no SQL equivalent of a CLR type test over a column; filter on a discriminator column instead.",
            Rejection(p => (object)p.ProductName is string));

    [Fact]
    public void AConstruction_IsNamed()
        => Assert.Equal(
            "Constructing a 'Product' is not supported inside a WHERE predicate. Compute the value before the query and compare against it.",
            Rejection(p => new Product() == null));

    [Fact]
    public void AnArray_IsRejected()
        => Assert.Equal(
            "Array construction is not supported inside a WHERE predicate. Build the array before the query and pass it in - Contains over a local collection translates to IN.",
            Rejection(p => new[] { p.ProductId } == null));

    [Fact]
    public void AnObjectInitializer_IsNamed()
        => Assert.Equal(
            "Object initializers ('new Product { ... }') are not supported inside a WHERE predicate. Compute the value before the query and compare against it.",
            Rejection(p => new Product { ProductId = p.ProductId } == null));

    [Fact]
    public void ACollectionInitializer_IsRejected()
        => Assert.Equal(
            "Collection initializers are not supported inside a WHERE predicate. Build the collection before the query and pass it in.",
            Rejection(p => new List<int> { p.ProductId } == null));

    [Fact]
    public void AnInvocation_IsRejected()
    {
        Func<Product, bool> isCheap = x => x.UnitPrice < 10;
        Assert.Equal(
            "Invoking a delegate or a nested lambda is not supported inside a WHERE predicate. Inline the predicate, or evaluate the delegate before the query.",
            Rejection(p => isCheap(p)));
    }

    [Fact]
    public void AnIndexer_IsRejected()
    {
        var index = Expression.MakeIndex(Expression.Constant(new List<int> { 1 }), typeof(List<int>).GetProperty("Item"), [Expression.Constant(0)]);

        Assert.Equal(
            "Indexer access is not supported inside a WHERE predicate. Read the element before the query and compare against the value.",
            Assert.Throws<NotSupportedException>(() => new WhereExpressionVisitor<Product>(_dialect).Visit(index)).Message);
    }

    private static Expression<Func<Product, bool>> SpanContains(Type spanType, int[] ids)
    {
        var p = Expression.Parameter(typeof(Product), "p");
        MethodInfo toSpan = spanType.GetMethod("op_Implicit", [typeof(int[])])!;
        MethodInfo contains = typeof(MemoryExtensions).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType.IsGenericType &&
                        m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == spanType.GetGenericTypeDefinition())
            .MakeGenericMethod(typeof(int));
        var body = Expression.Call(contains, Expression.Call(toSpan, Expression.Constant(ids)), Expression.Property(p, nameof(Product.ProductId)));
        return Expression.Lambda<Func<Product, bool>>(body, p);
    }

    [Theory]
    [InlineData(typeof(ReadOnlySpan<int>))]
    [InlineData(typeof(Span<int>))]
    public void MemoryExtensionsContainsOverASpan_IsAnInList(Type spanType)
    {
        var (sql, parameters) = Translate(SpanContains(spanType, [4, 5]));

        Assert.Equal("[product_id] IN (@product_id_0, @product_id_1)", sql);
        Assert.Equal([("@product_id_0", (object?)4), ("@product_id_1", 5)], parameters);
    }
}

internal static class WhereHelpers
{
    public static bool Contains(int[] values, int value) => false;
}
