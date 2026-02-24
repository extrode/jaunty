using System.Linq.Expressions;

namespace Jaunty.Fluent.Interfaces;

/// <summary>
/// Represents the ORDER BY clause - allows ThenBy chaining.
/// </summary>
public interface IOrderByClause<T> : IQueryTerminal<T> where T : new()
{
    // ThenBy - expression-based
    IOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector);
    IOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector);

    // ThenBy - string-based
    IOrderByClause<T> ThenBy(string column);
    IOrderByClause<T> ThenByDescending(string column);

    // TOP/LIMIT (after ORDER BY)
    IOrderByClause<T> Take(int count);
    IOrderByClause<T> Skip(int count);
}
