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

    // WHERE IN / NOT IN - collection-based filtering
    /// <summary>
    /// Filters results where the column value is in the specified collection.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the collection.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="values">Collection of values to match against.</param>
    IWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Filters results where the column value is NOT in the specified collection.
    /// </summary>
    IWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    // WHERE BETWEEN - range filtering
    /// <summary>
    /// Filters results where the column value is between the specified range (inclusive).
    /// </summary>
    IWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Filters results where the column value is NOT between the specified range.
    /// </summary>
    IWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

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

    // JOIN operations
    /// <summary>
    /// Adds an INNER JOIN to another table.
    /// </summary>
    /// <typeparam name="TJoin">The type of entity to join.</typeparam>
    /// <param name="alias">Optional alias for the joined table (used in string-based conditions).</param>
    IJoinClause<T, TJoin> InnerJoin<TJoin>(string? alias = null) where TJoin : new();

    /// <summary>
    /// Adds a LEFT JOIN to another table.
    /// </summary>
    /// <typeparam name="TJoin">The type of entity to join.</typeparam>
    /// <param name="alias">Optional alias for the joined table (used in string-based conditions).</param>
    IJoinClause<T, TJoin> LeftJoin<TJoin>(string? alias = null) where TJoin : new();

    /// <summary>
    /// Adds a RIGHT JOIN to another table.
    /// </summary>
    /// <typeparam name="TJoin">The type of entity to join.</typeparam>
    /// <param name="alias">Optional alias for the joined table (used in string-based conditions).</param>
    IJoinClause<T, TJoin> RightJoin<TJoin>(string? alias = null) where TJoin : new();

    // GROUP BY
    /// <summary>
    /// Groups results by the specified key.
    /// </summary>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <param name="keySelector">Expression selecting the grouping key.</param>
    /// <returns>A grouped query builder for HAVING and Select operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .GroupBy(p =&gt; p.CategoryId)
    ///   .Select(g =&gt; new { g.Key, Count = g.Count() });
    /// </code>
    /// </example>
    IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector);
}
