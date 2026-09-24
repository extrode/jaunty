using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Exact ON-clause text and exact rejection messages for the two-table join visitor.
/// </summary>
public class JoinExpressionVisitorExactShapeTests
{
    private readonly TestDialect _dialect = new();

    private (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<Product, Category, bool>> expr)
        => new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c").Translate(expr);

    private string Rejection(Expression<Func<Product, Category, bool>> expr)
        => Assert.Throws<NotSupportedException>(() => Translate(expr)).Message;

    [Fact]
    public void ColumnEqualsNull_IsIsNull()
        => Assert.Equal("(p.[category_id] IS NULL)", Translate((p, c) => p.CategoryId == null).Sql);

    [Fact]
    public void ColumnNotEqualsNull_IsIsNotNull()
        => Assert.Equal("(c.[description] IS NOT NULL)", Translate((p, c) => c.Description != null).Sql);

    [Fact]
    public void NullEqualsColumn_IsIsNull()
        => Assert.Equal("(p.[category_id] IS NULL)", Translate((p, c) => null == p.CategoryId).Sql);

    [Fact]
    public void NullNotEqualsColumn_IsIsNotNull()
        => Assert.Equal("(c.[description] IS NOT NULL)", Translate((p, c) => null != c.Description).Sql);

    [Fact]
    public void ACapturedNull_IsIsNull()
    {
        string? none = null;
        Assert.Equal("(c.[description] IS NULL)", Translate((p, c) => c.Description == none).Sql);
    }

    [Fact]
    public void ACapturedValue_IsAParameterNamedAfterTheColumn()
    {
        string name = "Beverages";
        var (sql, parameters) = Translate((p, c) => c.CategoryName == name);

        Assert.Equal("(c.[category_name] = @c_category_name)", sql);
        Assert.Equal([("@c_category_name", (object?)"Beverages")], parameters);
    }

    [Fact]
    public void AValueOnTheLeft_IsNamedAfterTheRightColumn()
    {
        var (sql, parameters) = Translate((p, c) => "Beverages" == c.CategoryName);

        Assert.Equal("(@c_category_name = c.[category_name])", sql);
        Assert.Equal([("@c_category_name", (object?)"Beverages")], parameters);
    }

    [Fact]
    public void Not_WrapsItsOperand()
        => Assert.Equal("NOT ((p.[category_id] = c.[category_id]))", Translate((p, c) => !(p.CategoryId == c.CategoryId)).Sql);

    [Fact]
    public void ABooleanColumnCompared_KeepsBothSides()
        => Assert.Equal("(p.[discontinued] = @p_discontinued)", Translate((p, c) => p.Discontinued == true).Sql);

    [Fact]
    public void AndAlsoAndOrElse_AreParenthesizedInOrder()
        => Assert.Equal(
            "((p.[category_id] = c.[category_id]) AND ((p.[product_id] > c.[category_id]) OR (p.[product_id] <= c.[category_id])))",
            Translate((p, c) => p.CategoryId == c.CategoryId && (p.ProductId > c.CategoryId || p.ProductId <= c.CategoryId)).Sql);

    [Fact]
    public void RemainingComparisons_UseTheirOperators()
        => Assert.Equal(
            "((p.[product_id] < c.[category_id]) AND (p.[product_id] >= c.[category_id]))",
            Translate((p, c) => p.ProductId < c.CategoryId && p.ProductId >= c.CategoryId).Sql);

    [Fact]
    public void ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        visitor.Translate((p, c) => c.CategoryName == "a");

        var (sql, parameters) = visitor.Translate((p, c) => c.CategoryName == "b");

        Assert.Equal("(c.[category_name] = @c_category_name)", sql);
        Assert.Equal([("@c_category_name", (object?)"b")], parameters);
    }

    [Fact]
    public void AnUnsupportedBinaryOperator_IsNamed()
        => Assert.Equal("Operator Add is not supported in JOIN expressions.", Rejection((p, c) => p.ProductId + c.CategoryId == 3 && p.ProductId + 1 > 0));

    [Fact]
    public void AMethodCall_IsNamed()
        => Assert.Equal("Method 'StartsWith' is not supported in JOIN expressions.", Rejection((p, c) => c.CategoryName.StartsWith("B")));

    [Fact]
    public void Negation_IsNamed()
        => Assert.Equal("Unary operator 'Negate' is not supported in JOIN expressions.", Rejection((p, c) => -p.ProductId == c.CategoryId));

    [Fact]
    public void Ternary_IsRejected()
        => Assert.Equal(
            "Conditional (ternary) expressions are not supported in JOIN predicates. " +
            "Split the predicate into separate conditions, or filter with Where after the join.",
            Rejection((p, c) => p.Discontinued ? p.ProductId == 1 : c.CategoryId == 2));

    [Fact]
    public void AnObjectInitializer_IsNamed()
        => Assert.Equal(
            "Object initializers ('new Category { ... }') are not supported inside a " +
            "JOIN predicate. Compute the value before the query and compare against it.",
            Rejection((p, c) => new Category { CategoryId = 1 } == c));

    [Fact]
    public void AWholeEntity_IsNamed()
        => Assert.Equal("'c' is a whole entity, not a condition. Compare its properties instead.", Rejection((p, c) => c == null));
}
