using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a grouped 2-way joined query that can be filtered with HAVING or projected
/// with Select. Sibling to <see cref="IGroupedQuery{T,TKey}"/> for the joined case.
/// </summary>
/// <typeparam name="TFrom">The primary (From) entity type.</typeparam>
/// <typeparam name="TJoin">The joined entity type.</typeparam>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <example>
/// <code>
/// db.From&lt;Film&gt;()
///   .InnerJoin&lt;Category&gt;().On(f =&gt; f.CategoryId, c =&gt; c.CategoryId)
///   .Where((f, c) =&gt; f.ReleaseYear &gt; 2000)
///   .GroupBy((f, c) =&gt; c.Name)
///   .Having(g =&gt; g.Count() &gt; 5)
///   .Select(g =&gt; new { Category = g.Key, Count = g.Count() });
/// </code>
/// </example>
public interface IGroupedJoinedQuery<TFrom, TJoin, TKey> where TFrom : new() where TJoin : new()
{
    /// <summary>
    /// Adds a HAVING clause to filter groups based on aggregate conditions.
    /// </summary>
    /// <param name="predicate">The condition to filter groups.</param>
    /// <returns>The grouped query for further chaining.</returns>
    IGroupedJoinedQuery<TFrom, TJoin, TKey> Having(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results into a new type.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="selector">Expression defining the projection using g.Key and aggregate functions.</param>
    /// <returns>List of projected results.</returns>
    List<TResult> Select<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector);

    /// <summary>
    /// Projects the grouped results into a new type, using the supplied
    /// <paramref name="options"/> for the command.
    /// </summary>
    /// <remarks>
    /// The joined counterpart of the overload <see cref="IGroupedQuery{T,TKey}"/> gained under
    /// AUD-R26-060: this builder executes its own command rather than delegating to core, so
    /// without this there is no way to enlist a grouped joined query in a caller's transaction
    /// or give it a timeout.
    /// </remarks>
    List<TResult> Select<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CommandOptions options);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously.
    /// </summary>
    Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously, using the supplied
    /// <paramref name="options"/> for the command.
    /// </summary>
    Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector);
}
