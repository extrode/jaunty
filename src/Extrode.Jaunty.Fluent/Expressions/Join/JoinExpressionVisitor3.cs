using System.Linq.Expressions;

using Extrode.Jaunty.Dialects;

namespace Extrode.Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, T3, bool}} predicates to SQL JOIN ON or WHERE clauses.
/// </summary>
/// <remarks>The translation lives in <see cref="JoinPredicateVisitor"/>, shared by every arity.</remarks>
internal sealed class JoinExpressionVisitor3<T1, T2, T3> : JoinPredicateVisitor
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    public JoinExpressionVisitor3(ISqlDialect dialect, string? alias1, string? alias2, string? alias3)
        : base(dialect, [JoinSide.For<T1>(dialect, alias1), JoinSide.For<T2>(dialect, alias2), JoinSide.For<T3>(dialect, alias3)])
    {
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T1, T2, T3, bool>> predicate)
        => TranslateCore(predicate);
}
