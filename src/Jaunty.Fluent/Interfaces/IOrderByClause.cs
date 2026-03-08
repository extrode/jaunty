using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the ORDER BY clause - allows ThenBy chaining.
/// </summary>
public interface IOrderByClause<T> : IQueryTerminal<T> where T : new()
{
    /// <summary>
    /// Adds an additional ORDER BY clause (ascending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    IOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY clause (descending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    IOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY clause (ascending) using column name.
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    IOrderByClause<T> ThenBy(string column);

    /// <summary>
    /// Adds an additional ORDER BY clause (descending) using column name.
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    IOrderByClause<T> ThenByDescending(string column);

    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows to return.</param>
    IOrderByClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    IOrderByClause<T> Skip(int count);
}