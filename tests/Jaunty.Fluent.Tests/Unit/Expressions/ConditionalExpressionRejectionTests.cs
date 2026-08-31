using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
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

    // AUD-R33-003: the AUD-R32-002 fix above never reached the JOIN and EXISTS visitors, which
    // have the identical gap. VisitBinary in each of them falls back to Visit(node.Left) /
    // Visit(node.Right) whenever the operand is not a plain column, which is exactly how a ternary
    // reaches the base traversal.

    [Fact]
    public void Join2_TernaryOperand_ThrowsNotSupported()
    {
        Expression<Func<Product, Category, bool>> expr =
            (p, c) => (p.Discontinued ? p.CategoryId : (short?)0) == c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("onditional", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Join3_TernaryOperand_ThrowsNotSupported()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr =
            (p, c, s) => (p.Discontinued ? p.CategoryId : (short?)0) == c.CategoryId && p.SupplierId == s.SupplierId;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("onditional", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Join4_TernaryOperand_ThrowsNotSupported()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr =
            (p, c, s, o) => (p.Discontinued ? p.CategoryId : (short?)0) == c.CategoryId
                            && p.SupplierId == s.SupplierId
                            && o.OrderId == p.ProductId;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("onditional", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The EXISTS visitor reaches a ternary only as the whole predicate. Its <c>VisitBinary</c>
    /// never calls <c>Visit</c> on a comparison operand - it routes non-column operands through
    /// <c>AnalyzeExpression</c>, which compiles them, so a ternary *operand* fails earlier with an
    /// <see cref="InvalidOperationException"/> from expression compilation. The top-level form is
    /// the one that used to reach the base traversal and emit fragments.
    /// </summary>
    [Fact]
    public void Exists_TopLevelTernaryPredicate_ThrowsNotSupported()
    {
        var visitor = new ExistsExpressionVisitor<Product, Category>(
            _dialect,
            FluentMetadataCache.GetMetadata<Product>(),
            FluentMetadataCache.GetMetadata<Category>(),
            "p",
            "c");

        Expression<Func<Product, Category, bool>> expr =
            (p, c) => p.Discontinued ? p.CategoryId == c.CategoryId : p.SupplierId == c.CategoryId;

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
        Assert.Contains("onditional", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Join2_TopLevelTernaryPredicate_ThrowsNotSupported()
    {
        Expression<Func<Product, Category, bool>> expr =
            (p, c) => p.Discontinued ? p.CategoryId == c.CategoryId : p.SupplierId == c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
    }

    /// <summary>
    /// The guards must not fire on ordinary predicates - this is the case that would break every
    /// existing join and EXISTS caller if the override were placed wrong.
    /// </summary>
    [Fact]
    public void OrdinaryJoinAndExistsPredicatesStillTranslate()
    {
        Expression<Func<Product, Category, bool>> plain = (p, c) => p.CategoryId == c.CategoryId;

        var (joinSql, _) = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c").Translate(plain);
        var (existsSql, _) = new ExistsExpressionVisitor<Product, Category>(
            _dialect,
            FluentMetadataCache.GetMetadata<Product>(),
            FluentMetadataCache.GetMetadata<Category>(),
            "p",
            "c").Translate(plain);

        Assert.Contains("category_id", joinSql, StringComparison.Ordinal);
        Assert.Contains("category_id", existsSql, StringComparison.Ordinal);
    }
}
