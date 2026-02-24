using System.Linq.Expressions;

namespace Jaunty.Fluent.Interfaces;

/// <summary>
/// Represents DISTINCT selection - provides terminal operations and WHERE clause.
/// </summary>
public interface IDistinctClause<T> : IQueryTerminal<T> where T : new()
{
    // WHERE clause - column + value
    IWhereClause<T> Where(string column, object? value);

    // WHERE clause - expression-based predicate
    IWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    // ORDER BY - expression-based
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    // ORDER BY - string-based
    IOrderByClause<T> OrderBy(string column);
    IOrderByClause<T> OrderByDescending(string column);

    // TOP/LIMIT
    IDistinctClause<T> Take(int count);
    IDistinctClause<T> Skip(int count);
}
