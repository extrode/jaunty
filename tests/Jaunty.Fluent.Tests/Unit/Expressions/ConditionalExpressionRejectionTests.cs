using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R32-002. A ternary reaches the visitors as a <see cref="ConditionalExpression"/>, which
/// neither visitor overrides. Before the fix the base <c>ExpressionVisitor</c> traversal walked
/// Test/IfTrue/IfFalse independently and each sub-expression emitted its own fragment, so the
/// caller got silently wrong SQL instead of an error. Every other untranslatable shape in these
/// two files throws <see cref="NotSupportedException"/>; these assert the ternary now does too.
/// </summary>
public class ConditionalExpressionRejectionTests
{
    private readonly TestDialect _dialect = new();

    [Fact]
    public void Select_TopLevelTernary_ThrowsNotSupported()
    {
        Expression<Func<Product, object>> expr = p => p.UnitPrice > 10m ? p.ProductName! : p.QuantityPerUnit!;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("conditional", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_TernaryInsideAnonymousProjection_ThrowsNotSupported()
    {
        Expression<Func<Product, object>> expr = p => new { p.ProductId, Name = p.UnitPrice > 10m ? p.ProductName! : p.QuantityPerUnit! };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
    }

    [Fact]
    public void Where_TernaryOperand_ThrowsNotSupported()
    {
        Expression<Func<Product, bool>> expr = p => (p.Discontinued ? p.UnitsInStock : p.ReorderLevel) > (short)5;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("conditional", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Where_TopLevelTernaryPredicate_ThrowsNotSupported()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued ? p.UnitsInStock > (short)5 : p.ReorderLevel > (short)5;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
    }
}
