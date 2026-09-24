using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact projection SQL, aliases, window-function chains and full rejection messages for the
/// SELECT visitor.
/// </summary>
public class SelectVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    public sealed class Named(string name)
    {
        public string Name { get; } = name;
    }

    public sealed class Inner
    {
        public string? X { get; set; }
    }

    public sealed class Outer
    {
        public Inner Nested { get; } = new();
    }

    private static readonly WindowBuilder<Product, long>? NoBuilder = null;

    private static WindowBuilder<Product, long> GetBuilder() => throw new InvalidOperationException();

    private List<(string, string)> Cols<TResult>(Expression<Func<Product, TResult>> expr)
        => new SelectExpressionVisitor<Product>(_dialect).Translate(expr).Select(c => (c.Sql, c.Alias)).ToList();

    private string Rejection<TResult>(Expression<Func<Product, TResult>> expr)
        => Assert.Throws<NotSupportedException>(() => Cols(expr)).Message;

    [Fact]
    public void AStandaloneFunction_IsAliasedValue()
        => Assert.Equal([("UPPER([product_name])", "Value")], Cols(p => Sql.Upper(p.ProductName)));

    [Fact]
    public void ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        visitor.Translate(p => p.ProductName);

        var columns = visitor.Translate(p => p.UnitPrice);

        Assert.Equal([("[unit_price]", "UnitPrice")], columns.Select(c => (c.Sql, c.Alias)));
    }

    [Fact]
    public void ANonAnonymousConstructor_IsRejected()
        => Assert.Equal("Only anonymous types with named members are supported in SELECT projections.", Rejection(p => new Named(p.ProductName)));

    [Fact]
    public void ANestedMemberBinding_IsRejected()
        => Assert.Equal(
            "Member binding 'MemberBinding' is not supported in SELECT projections. Only member assignments (Member = expression) can be translated.",
            Rejection(p => new Outer { Nested = { X = p.ProductName } }));

    [Fact]
    public void VisitingAnEntityMemberDirectly_AddsItsColumn()
    {
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(p => new { p.UnitPrice });

        visitor.Visit(Expression.Property(Expression.Parameter(typeof(Product), "p"), nameof(Product.ProductName)));

        Assert.Equal([("[unit_price]", "UnitPrice"), ("[product_name]", "ProductName")], columns.Select(c => (c.Sql, c.Alias)));
    }

    [Fact]
    public void VisitingANonEntityMemberDirectly_IsRejected()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new SelectExpressionVisitor<Product>(_dialect)
            .Visit(Expression.Property(Expression.Constant(new Product()), nameof(Product.ProductName))));

        Assert.Equal(
            "'ProductName' is not a property of the entity being projected. Only entity properties and Sql.* functions can be translated in SELECT projections.",
            ex.Message);
    }

    [Fact]
    public void Ternary_IsRejected()
        => Assert.Equal(
            "Conditional (ternary) expressions are not supported in SELECT projections. Use Sql.Case(...) for a CASE WHEN, or project the operands and branch in memory.",
            Rejection(p => p.Discontinued ? "a" : "b"));

    [Fact]
    public void ATypeTest_IsRejected()
        => Assert.Equal(
            "Type tests ('is', 'as') are not supported in SELECT projections. There is no SQL equivalent of a CLR type test; project a discriminator column instead.",
            Rejection(p => (object)p.ProductName is string));

    [Fact]
    public void AnArray_IsRejected()
        => Assert.Equal(
            "Array construction is not supported in SELECT projections. List the columns directly, or project into an anonymous type and build the array from the results.",
            Rejection(p => new[] { p.ProductName }));

    [Fact]
    public void ACollectionInitializer_IsRejected()
        => Assert.Equal(
            "Collection initializers are not supported in SELECT projections. Project the columns and build the collection from the results.",
            Rejection(p => new List<string> { p.ProductName }));

    [Fact]
    public void AnInvocation_IsRejected()
    {
        Func<Product, string> name = p => p.ProductName;
        Assert.Equal(
            "Invoking a delegate or a nested lambda is not supported in SELECT projections. Inline the projection, or apply the delegate to the results.",
            Rejection(p => name(p)));
    }

    [Fact]
    public void AnIndexer_IsRejected()
    {
        var p = Expression.Parameter(typeof(Product), "p");
        var index = Expression.MakeIndex(Expression.Constant(new List<int> { 1 }), typeof(List<int>).GetProperty("Item"), [Expression.Constant(0)]);

        Assert.Equal(
            "Indexer access is not supported in SELECT projections. Project the column and index the result.",
            Rejection(Expression.Lambda<Func<Product, int>>(index, p)));
    }

    [Fact]
    public void TheWholeEntity_IsRejected()
        => Assert.Equal(
            "'p' is the whole entity, not a projection. Select the columns you want, or run the query without a Select to get the entity.",
            Rejection(p => p));

    [Fact]
    public void ANegation_IsNamed()
        => Assert.Equal("Expression type 'Negate' is not supported in SELECT projections.", Rejection(p => new { X = -p.ProductId }));

    [Fact]
    public void AClrMethod_IsNamedWithItsType()
        => Assert.Equal("Method 'ToUpper' on type 'String' is not supported in SELECT projections.", Rejection(p => new { X = p.ProductName.ToUpper() }));

    [Fact]
    public void AnUnlistedSqlFunction_IsNamed()
        => Assert.Equal("SQL function 'Case' is not supported in SELECT projections.", Rejection(p => new { X = Sql.Case<Product, int>() }));

    [Fact]
    public void AWindowChain_KeepsPartitionAndOrderDirection()
        => Assert.Equal(
            [("ROW_NUMBER() OVER ()OVER (PARTITION BY [category_id] ORDER BY [unit_price] ASC, [product_id] DESC )", "R")],
            Cols(p => new { R = (long)Sql.RowNumber<Product>().PartitionBy(x => x.CategoryId).OrderBy(x => x.UnitPrice).OrderByDescending(x => x.ProductId) }));

    [Fact]
    public void AWindowAggregate_TranslatesItsColumn()
        => Assert.Equal(
            [("SUM([unit_price])OVER ()", "S")],
            Cols(p => new { S = (decimal?)Sql.Sum<Product, decimal?>(p.UnitPrice).Over() }));

    [Fact]
    public void AFunctionAsAPartitionKey_IsTranslated()
        => Assert.Equal(
            [("ROW_NUMBER() OVER ()OVER (PARTITION BY UPPER([product_name]) )", "R")],
            Cols(p => new { R = (long)Sql.RowNumber<Product>().PartitionBy(x => Sql.Upper(x.ProductName)) }));

    [Fact]
    public void ArithmeticAsAPartitionKey_IsNamed()
        => Assert.Equal(
            "Window function PARTITION BY and ORDER BY must reference entity properties or supported SQL functions. Got: Add",
            Rejection(p => new { R = (long)Sql.RowNumber<Product>().PartitionBy(x => x.ProductId + 1) }));

    [Fact]
    public void AChainRootedInAnotherMethod_IsNamed()
        => Assert.Equal(
            "Method 'GetBuilder' is not supported in window function chain.",
            Rejection(p => new { R = (long)GetBuilder().OrderBy(x => x.ProductId) }));

    [Fact]
    public void AChainRootedInAVariable_IsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Cols(p => new { R = (long)NoBuilder!.OrderBy(x => x.ProductId) }));
        Assert.Equal("Window function chain must start with an appropriate Sql.* method.", ex.Message);
    }
}
