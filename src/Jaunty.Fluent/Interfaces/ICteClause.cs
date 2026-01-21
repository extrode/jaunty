using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the initial CTE definition clause.
/// </summary>
/// <typeparam name="T">The entity type for the CTE.</typeparam>
public interface ICteClause<T> where T : new()
{
    /// <summary>
    /// Defines the CTE query using a builder function.
    /// </summary>
    /// <param name="queryBuilder">Function that builds the CTE query.</param>
    /// <returns>A clause for querying the CTE results.</returns>
    ICteQueryClause<T> As(Func<IFromClause<T>, IWhereClause<T>> queryBuilder);

    /// <summary>
    /// Defines the CTE query using an existing query builder (must have WHERE clause).
    /// </summary>
    /// <param name="query">The query to use as the CTE definition.</param>
    /// <returns>A clause for querying the CTE results.</returns>
    ICteQueryClause<T> As(IWhereClause<T> query);
}

/// <summary>
/// Represents a CTE that can be queried.
/// </summary>
/// <typeparam name="T">The entity type for the CTE.</typeparam>
public interface ICteQueryClause<T> where T : new()
{
    /// <summary>
    /// Adds a WHERE condition to filter CTE results.
    /// </summary>
    ICteQueryClause<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds a WHERE condition using column name and value.
    /// </summary>
    ICteQueryClause<T> Where(string column, object? value);

    /// <summary>
    /// Adds an AND condition to the WHERE clause.
    /// </summary>
    ICteQueryClause<T> And(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds an OR condition to the WHERE clause.
    /// </summary>
    ICteQueryClause<T> Or(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Orders the CTE results by the specified column ascending.
    /// </summary>
    ICteQueryClause<T> OrderBy<TKey>(Expression<Func<T, TKey>> selector);

    /// <summary>
    /// Orders the CTE results by the specified column descending.
    /// </summary>
    ICteQueryClause<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> selector);

    /// <summary>
    /// Limits the number of results.
    /// </summary>
    ICteQueryClause<T> Take(int count);

    /// <summary>
    /// Skips a number of results.
    /// </summary>
    ICteQueryClause<T> Skip(int count);

    /// <summary>
    /// Executes the CTE query and returns all matching entities.
    /// </summary>
    List<T> Select();

    /// <summary>
    /// Executes the CTE query asynchronously.
    /// </summary>
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the CTE query and returns the first entity.
    /// </summary>
    T SelectFirst();

    /// <summary>
    /// Executes the CTE query and returns the first entity or default.
    /// </summary>
    T? SelectFirstOrDefault();

    /// <summary>
    /// Returns the generated SQL for debugging.
    /// </summary>
    string ToSql();
}
