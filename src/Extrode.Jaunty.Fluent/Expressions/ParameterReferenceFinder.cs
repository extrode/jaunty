using System.Linq.Expressions;

namespace Extrode.Jaunty.Fluent.Expressions;

/// <summary>
/// Answers whether an expression mentions one of a lambda's own parameters, matched by identity.
/// </summary>
/// <remarks>
/// Matching any <see cref="ParameterExpression"/> is wrong: a nested lambda's own parameter -
/// <c>a</c> in <c>allowed.Any(a =&gt; a == "x")</c> - is also one, and the closed-over call it sits
/// in can still be evaluated before the query (AUD-R38, WhereExpressionVisitor.ParameterFinder).
/// </remarks>
internal sealed class ParameterReferenceFinder : ExpressionVisitor
{
    private readonly IReadOnlyCollection<ParameterExpression> _parameters;
    private bool _found;

    private ParameterReferenceFinder(IReadOnlyCollection<ParameterExpression> parameters) => _parameters = parameters;

    public static bool Mentions(Expression expression, IReadOnlyCollection<ParameterExpression> parameters)
    {
        var finder = new ParameterReferenceFinder(parameters);
        finder.Visit(expression);
        return finder._found;
    }

    public override Expression? Visit(Expression? node) => _found ? node : base.Visit(node);

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameters.Contains(node))
            _found = true;

        return node;
    }
}
