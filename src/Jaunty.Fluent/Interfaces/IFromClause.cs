using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the FROM clause - entry point after selecting table/columns.
/// Provides WHERE clause methods and terminal operations.
/// </summary>
public interface IFromClause<T> : IQueryTerminal<T> where T : new()
{
    // WHERE clause - column + value
    IWhereClause<T> Where(string column, object? value);

    // WHERE clause - expression-based predicate
    IWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    // WHERE clause - raw SQL (use with caution for parameterized queries)
    IWhereClause<T> WhereRaw(string rawSql);
    IWhereClause<T> WhereRaw(string rawSql, object parameters);

    // ORDER BY - expression-based
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    // ORDER BY - string-based
    IOrderByClause<T> OrderBy(string column);
    IOrderByClause<T> OrderByDescending(string column);

    // DISTINCT
    IDistinctClause<T> Distinct();

    // TOP/LIMIT
    IFromClause<T> Take(int count);
    IFromClause<T> Skip(int count);
}
