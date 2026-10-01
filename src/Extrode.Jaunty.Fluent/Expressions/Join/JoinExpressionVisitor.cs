using System.Linq.Expressions;

using Extrode.Jaunty.Dialects;

namespace Extrode.Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, bool}} join predicates to SQL ON clauses.
/// </summary>
/// <remarks>The translation lives in <see cref="JoinPredicateVisitor"/>, shared by every arity.</remarks>
internal sealed class JoinExpressionVisitor<T1, T2> : JoinPredicateVisitor
    where T1 : new()
    where T2 : new()
{
    public JoinExpressionVisitor(ISqlDialect dialect, string? alias1, string? alias2)
        : base(dialect, [JoinSide.For<T1>(dialect, alias1), JoinSide.For<T2>(dialect, alias2)])
    {
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T1, T2, bool>> predicate)
        => TranslateCore(predicate);
}
