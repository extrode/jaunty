using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Marker interface representing a grouping for aggregate expressions.
/// Used in GROUP BY Select projections to access the key and aggregate functions.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="T">The entity type being grouped.</typeparam>
/// <example>
/// <code>
/// db.From&lt;Product&gt;()
///   .GroupBy(p =&gt; p.CategoryId)
///   .Select(g =&gt; new {
///       CategoryId = g.Key,
///       Count = g.Count(),
///       TotalPrice = g.Sum(p =&gt; p.UnitPrice)
///   });
/// </code>
/// </example>
public interface IGrouping<TKey, T> where T : new()
{
    /// <summary>
    /// Gets the grouping key value.
    /// </summary>
    TKey Key { get; }

    /// <summary>
    /// Returns the count of items in this group.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of non-null values for the specified column.
    /// </summary>
    int Count<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the sum of values for the specified column.
    /// </summary>
    TResult Sum<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the average of values for the specified column.
    /// </summary>
    double Avg<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the minimum value for the specified column.
    /// </summary>
    TResult Min<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Returns the maximum value for the specified column.
    /// </summary>
    TResult Max<TResult>(Expression<Func<T, TResult>> selector);
}