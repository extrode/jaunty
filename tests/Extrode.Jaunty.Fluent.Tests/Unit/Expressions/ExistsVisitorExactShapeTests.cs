using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact correlation SQL, parameter names and full rejection messages for the EXISTS visitor.
/// </summary>
public class ExistsVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    private ExistsExpressionVisitor<Category, Product> Visitor(Dictionary<string, int>? counts = null)
        => new(_dialect, FluentMetadataCache.GetMetadata<Category>(), FluentMetadataCache.GetMetadata<Product>(), "c", "p", counts);

    private string Sql(Expression<Func<Category, Product, bool>> expr) => Visitor().Translate(expr).Sql;

    private string Rejection(Expression node)
        => Assert.Throws<NotSupportedException>(() => Visitor().Visit(node)).Message;

    private string Rejection(Expression<Func<Category, Product, bool>> expr)
        => Assert.Throws<NotSupportedException>(() => Visitor().Translate(expr)).Message;

    [Fact]
    public void ColumnEqualsNull_IsIsNull()
        => Assert.Equal("(p.[category_id] IS NULL)", Sql((c, p) => p.CategoryId == null));

    [Fact]
    public void NullNotEqualsColumn_IsIsNotNull()
        => Assert.Equal("(p.[quantity_per_unit] IS NOT NULL)", Sql((c, p) => null != p.QuantityPerUnit));

    [Fact]
    public void Not_WrapsItsOperand()
        => Assert.Equal("NOT (p.[discontinued])", Sql((c, p) => !p.Discontinued));

    [Fact]
    public void StrictOrderings_UseTheirOperators()
        => Assert.Equal(
            "((p.[product_id] < c.[category_id]) AND (p.[product_id] > c.[category_id]))",
            Sql((c, p) => p.ProductId < c.CategoryId && p.ProductId > c.CategoryId));

    [Fact]
    public void RepeatedValues_AreNumberedFromTwo()
    {
        int a = 1, b = 2, d = 3;
        var (sql, parameters) = Visitor().Translate((c, p) => p.ProductId == a || p.ProductId == b || p.ProductId == d);

        Assert.Equal("(((p.[product_id] = @p_exists) OR (p.[product_id] = @p_exists2)) OR (p.[product_id] = @p_exists3))", sql);
        Assert.Equal([("@p_exists", (object?)1), ("@p_exists2", 2), ("@p_exists3", 3)], parameters);
    }

    [Fact]
    public void ASharedCounter_ContinuesWhereTheLastClauseStopped()
    {
        var counts = new Dictionary<string, int> { ["p_exists"] = 1 };

        var (sql, _) = Visitor(counts).Translate((c, p) => p.ProductName == "x");

        Assert.Equal("(p.[product_name] = @p_exists2)", sql);
        Assert.Equal(2, counts["p_exists"]);
    }

    [Fact]
    public void ReusingTheVisitor_StartsTheSqlAndParametersFromEmpty()
    {
        var visitor = Visitor();
        visitor.Translate((c, p) => p.ProductName == "a");

        var (sql, parameters) = visitor.Translate((c, p) => p.ProductName == "b");

        Assert.Equal("(p.[product_name] = @p_exists2)", sql);
        Assert.Equal([("@p_exists2", (object?)"b")], parameters);
    }

    private static string StillCorrelated(Expression expression)
        => $"'{expression}' is not a translatable column reference, and it cannot be evaluated before the query " +
           "because it still refers to a correlation parameter. Compare columns directly, or compute the value " +
           "outside the predicate and capture it.";

    [Fact]
    public void AMemberOverAComputedOuterValue_IsRejectedAsStillCorrelated()
    {
        Expression<Func<Category, Product, bool>> expr = (c, p) => (c.CategoryName + "x").Length == p.ProductId;
        Assert.Equal(StillCorrelated(((BinaryExpression)expr.Body).Left), Rejection(expr));
    }

    [Fact]
    public void AMemberOverAComputedSubqueryValue_IsRejectedAsStillCorrelated()
    {
        Expression<Func<Category, Product, bool>> expr = (c, p) => (p.ProductName + "x").Length == 3;
        Assert.Equal(StillCorrelated(((BinaryExpression)expr.Body).Left), Rejection(expr));
    }

    [Fact]
    public void ArithmeticOverAColumn_IsRejectedAsStillCorrelated()
    {
        Expression<Func<Category, Product, bool>> expr = (c, p) => p.ProductId + 1 == 3;
        Assert.Equal(StillCorrelated(((BinaryExpression)expr.Body).Left), Rejection(expr));
    }

    [Fact]
    public void AMethodCall_IsNamed()
        => Assert.Equal("Method 'StartsWith' is not supported in EXISTS correlation predicates.", Rejection((c, p) => p.ProductName.StartsWith("x")));

    [Fact]
    public void AnUnsupportedUnary_IsNamed()
        => Assert.Equal(
            "Unary operator 'IsTrue' is not supported in EXISTS correlation predicates. Compute the value before the query, or express the condition without it.",
            Rejection(Expression.IsTrue(Expression.Constant(true))));

    [Fact]
    public void Ternary_IsRejected()
        => Assert.Equal(
            "Conditional (ternary) expressions are not supported in EXISTS correlation predicates. Split the predicate into separate conditions.",
            Rejection(Expression.Condition(Expression.Constant(true), Expression.Constant(true), Expression.Constant(false))));

    [Fact]
    public void ATypeTest_IsRejected()
        => Assert.Equal(
            "Type tests ('is', 'as') are not supported in EXISTS correlation predicates. There is no SQL equivalent of a CLR type test over a column; correlate on a discriminator column.",
            Rejection(Expression.TypeIs(Expression.Constant(new object()), typeof(Category))));

    [Fact]
    public void AConstruction_IsNamed()
        => Assert.Equal(
            "Constructing a 'Boolean' is not supported in EXISTS correlation predicates. Compute the value before the query and compare against it.",
            Rejection(Expression.New(typeof(bool))));

    [Fact]
    public void AnArrayConstruction_IsRejected()
        => Assert.Equal(
            "Array construction is not supported in EXISTS correlation predicates. Build the array before the query and pass it in.",
            Rejection(Expression.NewArrayInit(typeof(int), Expression.Constant(1))));

    [Fact]
    public void AnObjectInitializer_IsNamed()
        => Assert.Equal(
            "Object initializers ('new Product { ... }') are not supported in EXISTS correlation predicates. Compute the value before the query and compare against it.",
            Rejection(Expression.MemberInit(Expression.New(typeof(Product)), Expression.Bind(typeof(Product).GetProperty(nameof(Product.ProductId))!, Expression.Constant(1)))));

    [Fact]
    public void ACollectionInitializer_IsRejected()
        => Assert.Equal(
            "Collection initializers are not supported in EXISTS correlation predicates. Build the collection before the query and pass it in.",
            Rejection(Expression.ListInit(Expression.New(typeof(List<int>)), Expression.Constant(1))));

    [Fact]
    public void AnInvocation_IsRejected()
        => Assert.Equal(
            "Invoking a delegate or a nested lambda is not supported in EXISTS correlation predicates. Inline the predicate, or evaluate the delegate before the query.",
            Rejection(Expression.Invoke(Expression.Constant((Func<bool>)(() => true)))));

    [Fact]
    public void AnIndexer_IsRejected()
        => Assert.Equal(
            "Indexer access is not supported in EXISTS correlation predicates. Read the element before the query and compare against the value.",
            Rejection(Expression.MakeIndex(Expression.Constant(new List<int> { 1 }), typeof(List<int>).GetProperty("Item"), [Expression.Constant(0)])));
}
