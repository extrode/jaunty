using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the FROM clause - entry point after selecting table/columns.
/// Provides WHERE clause methods and terminal operations.
/// </summary>
public interface IFromClause<T> : IQueryTerminal<T> where T : new()
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
    /// Adds a WHERE clause with raw SQL. Use with caution for parameterized queries.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> WhereRaw(string rawSql);

    /// <summary>
    /// Adds a WHERE clause with raw SQL and parameters. Use with caution for parameterized queries.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">The parameters for the SQL condition.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> WhereRaw(string rawSql, object parameters);

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

    /// <summary>
    /// Filters results where the column value is between the specified range (inclusive).
    /// </summary>
    IWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Filters results where the column value is NOT between the specified range.
    /// </summary>
    IWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

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
    /// Applies DISTINCT to the query results.
    /// </summary>
    /// <returns>The query with DISTINCT applied.</returns>
    IDistinctClause<T> Distinct();

    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows.</param>
    /// <returns>The query with the LIMIT clause applied.</returns>
    IFromClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query with the OFFSET clause applied.</returns>
    IFromClause<T> Skip(int count);

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