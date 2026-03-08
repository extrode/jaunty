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

    // WHERE EXISTS / NOT EXISTS - subquery filtering
    /// <summary>
    /// Filters results where a correlated subquery returns any rows.
    /// </summary>
    /// <typeparam name="TSubquery">The type of entity in the subquery.</typeparam>
    /// <param name="predicate">Expression relating the outer entity to the subquery entity.</param>
    /// <example>
    /// <code>
    /// // Find categories that have at least one product
    /// db.From&lt;Category&gt;()
    ///   .WhereExists&lt;Product&gt;((c, p) =&gt; c.CategoryId == p.CategoryId)
    ///   .Select();
    /// // SQL: SELECT * FROM categories c WHERE EXISTS (SELECT 1 FROM products p WHERE c.category_id = p.category_id)
    /// </code>
    /// </example>
    IWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Filters results where a correlated subquery returns no rows.
    /// </summary>
    /// <typeparam name="TSubquery">The type of entity in the subquery.</typeparam>
    /// <param name="predicate">Expression relating the outer entity to the subquery entity.</param>
    /// <example>
    /// <code>
    /// // Find categories that have no products
    /// db.From&lt;Category&gt;()
    ///   .WhereNotExists&lt;Product&gt;((c, p) =&gt; c.CategoryId == p.CategoryId)
    ///   .Select();
    /// // SQL: SELECT * FROM categories c WHERE NOT EXISTS (SELECT 1 FROM products p WHERE c.category_id = p.category_id)
    /// </code>
    /// </example>
    IWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

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

    // DELETE operation
    /// <summary>
    /// Deletes all rows from the table. Use with caution - requires explicit call without WHERE.
    /// For safety, prefer using Where() before Delete().
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int DeleteAll();

    /// <summary>
    /// Asynchronously deletes all rows from the table.
    /// </summary>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);

    // UPDATE SET operations
    /// <summary>
    /// Begins an UPDATE statement by setting a column to a value.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <param name="selector">Expression selecting the column to update.</param>
    /// <param name="value">The new value for the column.</param>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Set(p =&gt; p.UnitPrice, 15.00m)
    ///   .Set(p =&gt; p.Discontinued, true)
    ///   .Where(p =&gt; p.CategoryId == 1)
    ///   .Update();
    /// </code>
    /// </example>
    ISetClause<T> Set<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    /// <summary>
    /// Begins an UPDATE statement by setting a column to a value using column name.
    /// </summary>
    ISetClause<T> Set(string column, object? value);

    /// <summary>
    /// Begins an UPDATE statement by setting multiple columns from an anonymous object.
    /// </summary>
    ISetClause<T> Set(object values);

    // SUBQUERY support
    /// <summary>
    /// Filters results where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The type of entity in the subquery.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery that provides the values.</param>
    /// <example>
    /// <code>
    /// // Find products in categories that have "Beverage" in their name
    /// db.From&lt;Product&gt;()
    ///   .WhereInSubquery(
    ///       p =&gt; p.CategoryId,
    ///       c =&gt; c.CategoryId,
    ///       db.From&lt;Category&gt;().Where(c =&gt; c.CategoryName.Contains("Beverage")))
    ///   .Select();
    /// // SQL: SELECT * FROM products WHERE category_id IN (SELECT category_id FROM categories WHERE category_name LIKE '%Beverage%')
    /// </code>
    /// </example>
    IWhereClause<T> WhereInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Filters results where the column value is NOT in the result of a subquery.
    /// </summary>
    IWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();
}