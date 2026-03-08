using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents DISTINCT selection - provides terminal operations and WHERE clause.
/// </summary>
public interface IDistinctClause<T> : IQueryTerminal<T> where T : new()
{
    /// <summary>
    /// Adds a WHERE clause with a column and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> Where(string column, object? value);

    /// <summary>
    /// Adds a WHERE clause with an expression-based predicate.
    /// </summary>
    /// <param name="predicate">The predicate expression.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds an ORDER BY clause in ascending order.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause in descending order.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause in ascending order by column name.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderBy(string column);

    /// <summary>
    /// Adds an ORDER BY clause in descending order by column name.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderByDescending(string column);

    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows.</param>
    /// <returns>The query with the LIMIT clause applied.</returns>
    IDistinctClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query with the OFFSET clause applied.</returns>
    IDistinctClause<T> Skip(int count);
}