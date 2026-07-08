using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a grouped 4-way joined query that can be filtered with HAVING or projected
/// with Select. Sibling to <see cref="IGroupedJoinedQuery{TFrom,TJoin,TKey}"/> for the
/// 4-entity case.
/// </summary>
public interface IGroupedJoinedQuery4<T1, T2, T3, T4, TKey>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    /// <summary>
    /// Adds a HAVING clause to filter groups based on aggregate conditions.
    /// </summary>
    IGroupedJoinedQuery4<T1, T2, T3, T4, TKey> Having(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results into a new type.
    /// </summary>
    List<TResult> Select<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously.
    /// </summary>
    Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector);
}
