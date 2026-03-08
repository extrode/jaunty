using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a grouped query that can be filtered with HAVING or projected with Select.
/// </summary>
/// <typeparam name="T">The entity type being grouped.</typeparam>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <example>
/// <code>
/// db.From&lt;Product&gt;()
///   .Where(p =&gt; p.Discontinued == false)
///   .GroupBy(p =&gt; p.CategoryId)
///   .Having(g =&gt; g.Count() &gt; 5)
///   .Select(g =&gt; new { g.Key, Count = g.Count() });
/// </code>
/// </example>
public interface IGroupedQuery<T, TKey> where T : new()
{
    /// <summary>
    /// Adds a HAVING clause to filter groups based on aggregate conditions.
    /// </summary>
    /// <param name="predicate">The condition to filter groups.</param>
    /// <returns>The grouped query for further chaining.</returns>
    /// <example>
    /// <code>
    /// .Having(g =&gt; g.Count() &gt; 5)
    /// .Having(g =&gt; g.Sum(p =&gt; p.UnitPrice) &gt; 100)
    /// </code>
    /// </example>
    IGroupedQuery<T, TKey> Having(Expression<Func<IGrouping<TKey, T>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results into a new type.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="selector">Expression defining the projection using g.Key and aggregate functions.</param>
    /// <returns>List of projected results.</returns>
    /// <example>
    /// <code>
    /// .Select(g =&gt; new {
    ///     CategoryId = g.Key,
    ///     ProductCount = g.Count(),
    ///     TotalPrice = g.Sum(p =&gt; p.UnitPrice)
    /// })
    /// </code>
    /// </example>
    List<TResult> Select<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously.
    /// </summary>
    Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector);
}