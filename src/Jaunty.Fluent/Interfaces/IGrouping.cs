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
/// <remarks>
/// AUD-R35-206. This name collides with <c>System.Linq.IGrouping&lt;TKey, TElement&gt;</c>, which
/// <c>ImplicitUsings</c> brings into every file of a consuming project. Lambda-inferred usage - the
/// documented style, and the only style the examples show - is unaffected, but a consumer that names
/// the type explicitly, in a variable declaration or a helper method's parameter, gets
/// <c>CS0104: 'IGrouping&lt;,&gt;' is an ambiguous reference</c>, and the resolution (qualify as
/// <c>Jaunty.Fluent.IGrouping&lt;TKey, T&gt;</c>, or add a <c>using</c> alias) is not discoverable
/// from the message. No in-repo site can see it: every one sits inside or under the
/// <c>Jaunty.Fluent</c> namespace, where enclosing-namespace lookup beats the using directive.
/// Recorded rather than renamed - renaming a public interface is the owner's call, and the entry in
/// <c>work/todo.md</c> carries the options.
/// </remarks>
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