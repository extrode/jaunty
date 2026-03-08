using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Base interface for terminal query operations (Select, Count, etc.)
/// All query builder interfaces inherit from this to provide terminal methods.
/// </summary>
public interface IQueryTerminal<T> where T : new()
{
    // Full entity selection (strict mapping - all columns)
    /// <summary>
    /// Executes the query and returns all results as a list.
    /// </summary>
    List<T> Select();

    /// <summary>
    /// Returns the first result or throws if empty.
    /// </summary>
    T SelectFirst();

    /// <summary>
    /// Returns the first result, or default if empty.
    /// </summary>
    T? SelectFirstOrDefault();

    /// <summary>
    /// Returns the single result or throws if empty or more than one.
    /// </summary>
    T SelectSingle();

    /// <summary>
    /// Returns the single result, or default if empty. Throws if more than one.
    /// </summary>
    T? SelectSingleOrDefault();

    // Partial entity selection - string-based column specification
    /// <summary>
    /// Executes the query selecting only the specified columns.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    List<T> SelectPartial(params string[] columns);

    /// <summary>
    /// Returns the first result with only the specified columns or throws if empty.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    T SelectPartialFirst(params string[] columns);

    /// <summary>
    /// Returns the first result with only the specified columns, or default if empty.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    T? SelectPartialFirstOrDefault(params string[] columns);

    /// <summary>
    /// Returns the single result with only the specified columns or throws if empty or more than one.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    T SelectPartialSingle(params string[] columns);

    /// <summary>
    /// Returns the single result with only the specified columns, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    T? SelectPartialSingleOrDefault(params string[] columns);

    // Partial entity selection - expression-based column specification
    /// <summary>
    /// Executes the query selecting only the specified columns.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    List<T> SelectPartial(params Expression<Func<T, object?>>[] columns);

    /// <summary>
    /// Returns the first result with only the specified columns or throws if empty.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    T SelectPartialFirst(params Expression<Func<T, object?>>[] columns);

    /// <summary>
    /// Returns the first result with only the specified columns, or default if empty.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    T? SelectPartialFirstOrDefault(params Expression<Func<T, object?>>[] columns);

    /// <summary>
    /// Returns the single result with only the specified columns or throws if empty or more than one.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    T SelectPartialSingle(params Expression<Func<T, object?>>[] columns);

    /// <summary>
    /// Returns the single result with only the specified columns, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    T? SelectPartialSingleOrDefault(params Expression<Func<T, object?>>[] columns);

    // Scalar aggregates - COUNT
    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of rows as long.
    /// </summary>
    long LongCount();

    /// <summary>
    /// Returns the count of non-null values for the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    int Count<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the count of non-null values for the specified column as long.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    long LongCount<TResult>(Expression<Func<T, TResult>> selector);

    // Scalar aggregates - SUM, AVG, MIN, MAX
    /// <summary>
    /// Returns the sum of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to sum.</param>
    TResult Sum<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the average of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to average.</param>
    double Avg<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the minimum value of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    TResult Min<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the maximum value of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    TResult Max<TResult>(Expression<Func<T, TResult>> selector);

    // SelectX aliases (explicit terminal operation naming)
    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int SelectCount();

    /// <summary>
    /// Returns the count of non-null values for the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    int SelectCount<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the sum of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to sum.</param>
    TResult SelectSum<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the average of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column to average.</param>
    double SelectAvg<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the minimum value of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    TResult SelectMin<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the maximum value of the specified column.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    TResult SelectMax<TResult>(Expression<Func<T, TResult>> selector);

    // Async variants - full entity selection
    /// <summary>
    /// Executes the query asynchronously and returns all results as a list.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if empty or more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (string-based)
    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously with only the specified columns or throws if empty.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously with only the specified columns, or default if empty.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously with only the specified columns or throws if empty or more than one.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously with only the specified columns, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (expression-based)
    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously with only the specified columns or throws if empty.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously with only the specified columns, or default if empty.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously with only the specified columns or throws if empty or more than one.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously with only the specified columns, or default if empty. Throws if more than one.
    /// </summary>
    /// <param name="columns">Expressions selecting the columns to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    // Async aggregates - COUNT
    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of non-null values for the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of non-null values for the specified column as long asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> LongCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // Async aggregates - SUM, AVG, MIN, MAX
    /// <summary>
    /// Returns the sum of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to sum.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the average of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to average.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the minimum value of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the maximum value of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // Async SelectX aliases
    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> SelectCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of non-null values for the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> SelectCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sum of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to sum.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> SelectSumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the average of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column to average.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<double> SelectAvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the minimum value of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> SelectMinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the maximum value of the specified column asynchronously.
    /// </summary>
    /// <param name="selector">Expression selecting the column.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResult> SelectMaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    // SQL introspection (for debugging/logging)
    /// <summary>
    /// Returns the SELECT SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();

    /// <summary>
    /// Returns the SELECT SQL for the specified columns (for debugging).
    /// </summary>
    /// <param name="columns">The column names to select.</param>
    string ToSql(params string[] columns);

    /// <summary>
    /// Returns the SELECT SQL for the specified columns (for debugging).
    /// </summary>
    /// <param name="columns">Expressions selecting the columns.</param>
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