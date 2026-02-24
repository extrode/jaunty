using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Base interface for terminal query operations (Select, Count, etc.)
/// All query builder interfaces inherit from this to provide terminal methods.
/// </summary>
public interface IQueryTerminal<T> where T : new()
{
    // Full entity selection (strict mapping - all columns)
    List<T> Select();
    T SelectFirst();
    T? SelectFirstOrDefault();
    T SelectSingle();
    T? SelectSingleOrDefault();

    // Partial entity selection - string-based column specification
    List<T> SelectPartial(params string[] columns);
    T SelectPartialFirst(params string[] columns);
    T? SelectPartialFirstOrDefault(params string[] columns);
    T SelectPartialSingle(params string[] columns);
    T? SelectPartialSingleOrDefault(params string[] columns);

    // Partial entity selection - expression-based column specification
    List<T> SelectPartial(params Expression<Func<T, object?>>[] columns);
    T SelectPartialFirst(params Expression<Func<T, object?>>[] columns);
    T? SelectPartialFirstOrDefault(params Expression<Func<T, object?>>[] columns);
    T SelectPartialSingle(params Expression<Func<T, object?>>[] columns);
    T? SelectPartialSingleOrDefault(params Expression<Func<T, object?>>[] columns);

    // Scalar aggregates - COUNT
    int Count();
    long LongCount();
    int Count<TResult>(Expression<Func<T, TResult>> selector);
    long LongCount<TResult>(Expression<Func<T, TResult>> selector);

    // Scalar aggregates - SUM, AVG, MIN, MAX
    TResult Sum<TResult>(Expression<Func<T, TResult>> selector);
    double Avg<TResult>(Expression<Func<T, TResult>> selector);
    TResult Min<TResult>(Expression<Func<T, TResult>> selector);
    TResult Max<TResult>(Expression<Func<T, TResult>> selector);

    // SelectX aliases (explicit terminal operation naming)
    int SelectCount();
    int SelectCount<TResult>(Expression<Func<T, TResult>> selector);
    TResult SelectSum<TResult>(Expression<Func<T, TResult>> selector);
    double SelectAvg<TResult>(Expression<Func<T, TResult>> selector);
    TResult SelectMin<TResult>(Expression<Func<T, TResult>> selector);
    TResult SelectMax<TResult>(Expression<Func<T, TResult>> selector);

    // Async variants - full entity selection
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (string-based)
    Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (expression-based)
    Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    // Async aggregates - COUNT
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<long> LongCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // Async aggregates - SUM, AVG, MIN, MAX
    Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // Async SelectX aliases
    Task<int> SelectCountAsync(CancellationToken cancellationToken = default);
    Task<int> SelectCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> SelectSumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<double> SelectAvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> SelectMinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> SelectMaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // SQL introspection (for debugging/logging)
    string ToSql();
    string ToSql(params string[] columns);
    string ToSql(params Expression<Func<T, object?>>[] columns);

    /// <summary>
    /// Returns the SELECT SQL with a projection expression, supporting window functions.
    /// </summary>
    /// <typeparam name="TResult">The projected result type (typically anonymous type).</typeparam>
    /// <param name="selector">Projection expression defining columns and window functions.</param>
    /// <example>
    /// <code>
    /// var sql = db.From&lt;Product&gt;()
    ///   .ToSql(p =&gt; new {
    ///       p.ProductName,
    ///       p.UnitPrice,
    ///       RowNum = Sql.RowNumber().PartitionBy(p.CategoryId).OrderBy(p.UnitPrice)
    ///   });
    /// // SQL: SELECT product_name, unit_price, ROW_NUMBER() OVER (PARTITION BY category_id ORDER BY unit_price) AS RowNum FROM products
    /// </code>
    /// </example>
    string ToSql<TResult>(Expression<Func<T, TResult>> selector);

    // Set operations (UNION, UNION ALL, EXCEPT, INTERSECT)
    /// <summary>
    /// Combines the results with another query using UNION (removes duplicates).
    /// </summary>
    /// <param name="other">The query to combine with.</param>
    /// <example>
    /// <code>
    /// // Get products from category 1 OR category 2 (no duplicates)
    /// db.From&lt;Product&gt;()
    ///   .Where(p =&gt; p.CategoryId == 1)
    ///   .Union(db.From&lt;Product&gt;().Where(p =&gt; p.CategoryId == 2))
    ///   .Select();
    /// </code>
    /// </example>
    ISetOperationClause<T> Union(IQueryTerminal<T> other);

    /// <summary>
    /// Combines the results with another query using UNION ALL (keeps duplicates).
    /// </summary>
    /// <param name="other">The query to combine with.</param>
    /// <example>
    /// <code>
    /// // Get all products from category 1 and category 2 (may include duplicates)
    /// db.From&lt;Product&gt;()
    ///   .Where(p =&gt; p.CategoryId == 1)
    ///   .UnionAll(db.From&lt;Product&gt;().Where(p =&gt; p.CategoryId == 2))
    ///   .Select();
    /// </code>
    /// </example>
    ISetOperationClause<T> UnionAll(IQueryTerminal<T> other);

    /// <summary>
    /// Returns rows from this query that don't appear in the other query.
    /// </summary>
    /// <param name="other">The query to exclude rows from.</param>
    /// <example>
    /// <code>
    /// // Get products from category 1 that are NOT discontinued
    /// db.From&lt;Product&gt;()
    ///   .Where(p =&gt; p.CategoryId == 1)
    ///   .Except(db.From&lt;Product&gt;().Where(p =&gt; p.Discontinued))
    ///   .Select();
    /// </code>
    /// </example>
    ISetOperationClause<T> Except(IQueryTerminal<T> other);

    /// <summary>
    /// Returns only rows that appear in both queries.
    /// </summary>
    /// <param name="other">The query to intersect with.</param>
    /// <example>
    /// <code>
    /// // Get products that are in both category 1 AND have low stock
    /// db.From&lt;Product&gt;()
    ///   .Where(p =&gt; p.CategoryId == 1)
    ///   .Intersect(db.From&lt;Product&gt;().Where(p =&gt; p.UnitsInStock &lt; 10))
    ///   .Select();
    /// </code>
    /// </example>
    ISetOperationClause<T> Intersect(IQueryTerminal<T> other);
}
