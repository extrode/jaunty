using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Jaunty.Core;

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
/// <remarks>
/// AUD-R35-207. The type-parameter order here is the entity first, while the
/// <see cref="IGrouping{TKey, T}"/> this interface's own members hand back puts the key first -
/// that one matches the BCL's <c>IGrouping&lt;TKey, TElement&gt;</c>, and this one matches the rest
/// of the fluent surface, where <c>T</c> is always the entity being queried. Both are defensible;
/// the cost is that a caller naming either type explicitly has to remember which convention applies
/// where. No behavioural effect, and reordering either is a breaking public-API change - the entry
/// in <c>work/todo.md</c> carries it as the owner's call.
/// </remarks>
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
    List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector);

    /// <summary>
    /// Projects the grouped results into a new type, using the supplied
    /// <paramref name="options"/> for the command.
    /// </summary>
    /// <remarks>
    /// AUD-R26-060: the grouped builder executes its own command instead of delegating to core,
    /// and so had no way to accept a transaction or a timeout at all.
    /// </remarks>
    List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector, CommandOptions options);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously.
    /// </summary>
    Task<List<TResult>> SelectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously, using the supplied
    /// <paramref name="options"/> for the command.
    /// </summary>
    Task<List<TResult>> SelectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector);
}