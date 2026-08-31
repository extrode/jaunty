using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Marker interface representing a grouping for aggregate expressions over a 2-way joined
/// query. Sibling to <see cref="IGrouping{TKey,T}"/> for the joined case - aggregate
/// selectors are multi-parameter (<c>Expression&lt;Func&lt;TFrom,TJoin,TResult&gt;&gt;</c>)
/// rather than single-entity, matching the existing joined-WHERE convention.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="TFrom">The primary (From) entity type.</typeparam>
/// <typeparam name="TJoin">The joined entity type.</typeparam>
/// <example>
/// <code>
/// db.From&lt;Film&gt;()
///   .InnerJoin&lt;Category&gt;().On(f =&gt; f.CategoryId, c =&gt; c.CategoryId)
///   .GroupBy((f, c) =&gt; c.Name)
///   .Select(g =&gt; new {
///       Category = g.Key,
///       Count = g.Count(),
///       TotalRevenue = g.Sum((f, c) =&gt; f.Revenue)
///   });
/// </code>
/// </example>
public interface IGroupingJoined<TKey, TFrom, TJoin> where TFrom : new() where TJoin : new()
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
    int Count<TResult>(Expression<Func<TFrom, TJoin, TResult>> selector);

    /// <summary>
    /// Returns the sum of values for the specified column.
    /// </summary>
    TResult Sum<TResult>(Expression<Func<TFrom, TJoin, TResult>> selector);

    /// <summary>
    /// Returns the average of values for the specified column.
    /// </summary>
    double Avg<TResult>(Expression<Func<TFrom, TJoin, TResult>> selector);

    /// <summary>
    /// Returns the minimum value for the specified column.
    /// </summary>
    TResult Min<TResult>(Expression<Func<TFrom, TJoin, TResult>> selector);

    /// <summary>
    /// Returns the maximum value for the specified column.
    /// </summary>
    TResult Max<TResult>(Expression<Func<TFrom, TJoin, TResult>> selector);
}
