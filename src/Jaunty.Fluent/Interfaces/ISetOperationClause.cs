using System.Linq.Expressions;

using Jaunty.Core;

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
    /// <summary>
    /// Executes the query and returns all results as a list.
    /// </summary>
    List<T> Select();

    /// <summary>
    /// Executes the query and returns all results as a list, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    List<T> Select(CommandOptions options);

    /// <summary>
    /// Returns the first result or throws if empty.
    /// </summary>
    T SelectFirst();

    /// <summary>
    /// Returns the first result or throws if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T SelectFirst(CommandOptions options);

    /// <summary>
    /// Returns the first result, or default if empty.
    /// </summary>
    T? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T? SelectFirstOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the single result or throws if empty or more than one.
    /// </summary>
    T SelectSingle();

    /// <summary>
    /// Returns the single result or throws if empty or more than one, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T SelectSingle(CommandOptions options);

    /// <summary>
    /// Returns the single result, or default if empty. Throws if more than one.
    /// </summary>
    T? SelectSingleOrDefault();

    /// <summary>
    /// Returns the single result, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// Throws if more than one.
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T? SelectSingleOrDefault(CommandOptions options);

    /// <summary>
    /// Executes the query asynchronously and returns all results as a list.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all results as a list, executing within
    /// the given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if empty or more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if empty or more than one, executing
    /// within the given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// Throws if more than one.
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    // SQL introspection
    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();
}

/// <summary>
/// Represents the ORDER BY clause for set operations.
/// Allows chaining ThenBy and terminal operations.
/// </summary>
public interface ISetOperationOrderByClause<T> where T : new()
{
    /// <summary>
    /// Adds an additional ORDER BY clause (ascending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    ISetOperationOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY clause (descending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    ISetOperationOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY clause (ascending) using column name.
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    ISetOperationOrderByClause<T> ThenBy(string column);

    /// <summary>
    /// Adds an additional ORDER BY clause (descending) using column name.
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    ISetOperationOrderByClause<T> ThenByDescending(string column);

    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows to return.</param>
    ISetOperationOrderByClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    ISetOperationOrderByClause<T> Skip(int count);

    /// <summary>
    /// Executes the query and returns all results as a list.
    /// </summary>
    List<T> Select();

    /// <summary>
    /// Executes the query and returns all results as a list, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    List<T> Select(CommandOptions options);

    /// <summary>
    /// Returns the first result or throws if empty.
    /// </summary>
    T SelectFirst();

    /// <summary>
    /// Returns the first result or throws if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T SelectFirst(CommandOptions options);

    /// <summary>
    /// Returns the first result, or default if empty.
    /// </summary>
    T? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T? SelectFirstOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the single result or throws if empty or more than one.
    /// </summary>
    T SelectSingle();

    /// <summary>
    /// Returns the single result or throws if empty or more than one, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T SelectSingle(CommandOptions options);

    /// <summary>
    /// Returns the single result, or default if empty. Throws if more than one.
    /// </summary>
    T? SelectSingleOrDefault();

    /// <summary>
    /// Returns the single result, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// Throws if more than one.
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    T? SelectSingleOrDefault(CommandOptions options);

    /// <summary>
    /// Executes the query asynchronously and returns all results as a list.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all results as a list, executing within
    /// the given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if empty or more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if empty or more than one, executing
    /// within the given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(System.Data.IDbTransaction)"/>).
    /// Throws if more than one.
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();
}