using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the result of a set operation (UNION, UNION ALL, EXCEPT, INTERSECT).
/// Allows chaining additional set operations, ordering, and terminal operations.
/// </summary>
public interface ISetOperationClause<T> where T : new()
{
    // Chain additional set operations
    /// <summary>
    /// Combines the results with another query using UNION (removes duplicates).
    /// </summary>
    ISetOperationClause<T> Union(IQueryTerminal<T> other);

    /// <summary>
    /// Combines the results with another query using UNION ALL (keeps duplicates).
    /// </summary>
    ISetOperationClause<T> UnionAll(IQueryTerminal<T> other);

    /// <summary>
    /// Returns rows from the first query that don't appear in the second query.
    /// </summary>
    ISetOperationClause<T> Except(IQueryTerminal<T> other);

    /// <summary>
    /// Returns only rows that appear in both queries.
    /// </summary>
    ISetOperationClause<T> Intersect(IQueryTerminal<T> other);

    // ORDER BY (applies to entire combined result)
    /// <summary>
    /// Orders the combined results by the specified column.
    /// </summary>
    ISetOperationOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Orders the combined results by the specified column in descending order.
    /// </summary>
    ISetOperationOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Orders the combined results by the specified column name.
    /// </summary>
    ISetOperationOrderByClause<T> OrderBy(string column);

    /// <summary>
    /// Orders the combined results by the specified column name in descending order.
    /// </summary>
    ISetOperationOrderByClause<T> OrderByDescending(string column);

    // Take/Skip (applies to entire combined result)
    /// <summary>
    /// Limits the number of rows returned from the combined results.
    /// </summary>
    ISetOperationClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows from the combined results.
    /// </summary>
    ISetOperationClause<T> Skip(int count);

    // Terminal operations
    List<T> Select();
    T SelectFirst();
    T? SelectFirstOrDefault();
    T SelectSingle();
    T? SelectSingleOrDefault();

    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    // SQL introspection
    string ToSql();
}

/// <summary>
/// Represents the ORDER BY clause for set operations.
/// Allows chaining ThenBy and terminal operations.
/// </summary>
public interface ISetOperationOrderByClause<T> where T : new()
{
    // ThenBy for additional ordering
    ISetOperationOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector);
    ISetOperationOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector);
    ISetOperationOrderByClause<T> ThenBy(string column);
    ISetOperationOrderByClause<T> ThenByDescending(string column);

    // Take/Skip
    ISetOperationOrderByClause<T> Take(int count);
    ISetOperationOrderByClause<T> Skip(int count);

    // Terminal operations
    List<T> Select();
    T SelectFirst();
    T? SelectFirstOrDefault();
    T SelectSingle();
    T? SelectSingleOrDefault();

    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    // SQL introspection
    string ToSql();
}