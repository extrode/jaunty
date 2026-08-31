using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Marker interface representing a grouping for aggregate expressions over a 3-way joined
/// query. Sibling to <see cref="IGroupingJoined{TKey,TFrom,TJoin}"/> for the 3-entity case.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="T1">The primary (From) entity type.</typeparam>
/// <typeparam name="T2">The first joined entity type.</typeparam>
/// <typeparam name="T3">The second joined entity type.</typeparam>
/// <example>
/// <code>
/// db.From&lt;Product&gt;()
///   .InnerJoin&lt;Category&gt;().On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
///   .InnerJoin&lt;Supplier&gt;().On(p =&gt; p.SupplierId, s =&gt; s.SupplierId)
///   .GroupBy((p, c, s) =&gt; s.CompanyName)
///   .Select(g =&gt; new {
///       Supplier = g.Key,
///       Count = g.Count(),
///       Dearest = g.Max((p, c, s) =&gt; p.UnitPrice)
///   });
/// </code>
/// </example>
public interface IGroupingJoined3<TKey, T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
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
    int Count<TResult>(Expression<Func<T1, T2, T3, TResult>> selector);

    /// <summary>
    /// Returns the sum of values for the specified column.
    /// </summary>
    TResult Sum<TResult>(Expression<Func<T1, T2, T3, TResult>> selector);

    /// <summary>
    /// Returns the average of values for the specified column.
    /// </summary>
    double Avg<TResult>(Expression<Func<T1, T2, T3, TResult>> selector);

    /// <summary>
    /// Returns the minimum value for the specified column.
    /// </summary>
    TResult Min<TResult>(Expression<Func<T1, T2, T3, TResult>> selector);

    /// <summary>
    /// Returns the maximum value for the specified column.
    /// </summary>
    TResult Max<TResult>(Expression<Func<T1, T2, T3, TResult>> selector);
}
