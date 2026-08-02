using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R33-010, the rest of the sweep AUD-R32-002 began. A ternary was not the only node type these
/// visitors failed to override: an <c>is</c>-test, a constructor call or a delegate invocation all
/// fell to <see cref="ExpressionVisitor"/>'s descend-into-children default, which appends nothing
/// for the wrapper node while its children still write into the shared SQL builder. The caller got a
/// query that looked fine and asked a different question.
/// <para>
/// <b>What verification changed.</b> The reachable route is not the one the finding assumed. A
/// non-column operand of a comparison never reaches <c>Visit</c> at all - <c>VisitBinary</c> hands it
/// to <c>EvaluateExpression</c>, which compiles it - so <c>p.Name == new string('x', 3)</c> is
/// evaluated, not visited, and always was. What does reach the visitor is a node in <em>predicate</em>
/// position: the lambda body itself, or an operand of <c>AndAlso</c>/<c>OrElse</c>, which
/// <c>VisitBinary</c> visits rather than evaluates. Those are the routes tested here. The overrides
/// for the shapes that are not currently reachable stay in place as guards - a throw is the right
/// default for a node type with no translation, and reachability is a property of
/// <c>VisitBinary</c>'s fallbacks that any future edit can change.
/// </para>
/// <para>
/// The nodes are built with <see cref="Expression"/> factory methods because C# will not compile the
/// equivalent source into these shapes - which is part of why the gaps went unnoticed.
/// </para>
/// </summary>
public class UntranslatableNodeRejectionTests
{
    private readonly TestDialect _dialect = new();

    private static readonly ParameterExpression Param = Expression.Parameter(typeof(Product), "p");

    private WhereExpressionVisitor<Product> Where() => new(_dialect);

    private static Expression<Func<Product, bool>> Predicate(Expression body)
        => Expression.Lambda<Func<Product, bool>>(body, Param);

    private static Expression IsAString()
        => Expression.TypeIs(Expression.Property(Param, nameof(Product.ProductName)), typeof(string));

    [Fact]
    public void Where_TypeTestAsThePredicate_ThrowsNotSupported()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Where().Translate(Predicate(IsAString())));

        Assert.Contains("Type tests", ex.Message);
    }

    /// <summary>
    /// The route that matters most: <c>AndAlso</c> visits both operands, so one bad conjunct used to
    /// vanish from a predicate whose other conjuncts translated perfectly well.
    /// </summary>
    [Fact]
    public void Where_TypeTestAsAConjunct_ThrowsNotSupported()
    {
        Expression body = Expression.AndAlso(
            Expression.GreaterThan(
                Expression.Property(Param, nameof(Product.ProductId)),
                Expression.Constant(0)),
            IsAString());

        Assert.Throws<NotSupportedException>(() => Where().Translate(Predicate(body)));
    }

    [Fact]
    public void Where_DelegateInvocationAsThePredicate_ThrowsNotSupported()
    {
        Expression<Func<Product, bool>> inner = x => x.ProductId > 0;

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Where().Translate(Predicate(Expression.Invoke(inner, Param))));

        Assert.Contains("delegate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Where_ConstructorCallAsAConjunct_ThrowsNotSupported()
    {
        Expression body = Expression.AndAlso(
            Expression.GreaterThan(
                Expression.Property(Param, nameof(Product.ProductId)),
                Expression.Constant(0)),
            Expression.New(typeof(bool)));

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Where().Translate(Predicate(body)));

        Assert.Contains("Constructing", ex.Message);
    }

    [Fact]
    public void Select_TypeTestProjection_ThrowsNotSupported()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => new SelectExpressionVisitor<Product>(_dialect)
                .Translate(Expression.Lambda<Func<Product, bool>>(IsAString(), Param)));

        Assert.Contains("Type tests", ex.Message);
    }

    /// <summary>
    /// <c>x =&gt; x</c> is the whole entity, not a column list. It used to come back as an empty
    /// projection, which reads as "select nothing" rather than as a mistake.
    /// </summary>
    [Fact]
    public void Select_BareParameterProjection_ThrowsNotSupported()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => new SelectExpressionVisitor<Product>(_dialect)
                .Translate(Expression.Lambda<Func<Product, Product>>(Param, Param)));

        Assert.Contains("whole entity", ex.Message);
    }

    /// <summary>
    /// The controls. Throwing overrides are only worth having if they are narrower than the
    /// predicates and projections people actually write - including the evaluated-operand case that
    /// looks like a constructor call but never reaches the visitor.
    /// </summary>
    [Fact]
    public void OrdinaryPredicatesAndProjectionsStillTranslate()
    {
        (string whereSql, _) = Where().Translate(p => p.UnitPrice > 10m && p.ProductName.StartsWith("A"));

        Assert.Contains("unit_price", whereSql);
        Assert.Contains("product_name", whereSql);

        List<SelectColumn> columns = new SelectExpressionVisitor<Product>(_dialect)
            .Translate(p => new { p.ProductId, p.ProductName });

        Assert.Equal(2, columns.Count);
    }

    [Fact]
    public void AnEvaluatedConstructorCallInAComparisonStillTranslates()
    {
        Expression body = Expression.Equal(
            Expression.Property(Param, nameof(Product.ProductName)),
            Expression.New(typeof(string).GetConstructor([typeof(char), typeof(int)])!,
                Expression.Constant('x'),
                Expression.Constant(3)));

        (string sql, List<(string Name, object? Value)> parameters) = Where().Translate(Predicate(body));

        Assert.Contains("product_name", sql);
        Assert.Equal("xxx", Assert.Single(parameters).Value);
    }
}
