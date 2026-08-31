using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a grouped 3-way joined query that can be filtered with HAVING or projected
/// with Select. Sibling to <see cref="IGroupedJoinedQuery{TFrom,TJoin,TKey}"/> for the
/// 3-entity case.
/// </summary>
public interface IGroupedJoinedQuery3<T1, T2, T3, TKey>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    /// <summary>
    /// Adds a HAVING clause to filter groups based on aggregate conditions.
    /// </summary>
    IGroupedJoinedQuery3<T1, T2, T3, TKey> Having(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results into a new type.
    /// </summary>
    List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector);

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
    List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options);

    /// <summary>
    /// Projects the grouped results into a new type asynchronously.
    /// </summary>
    Task<List<TResult>> SelectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CancellationToken cancellationToken = default);

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
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql<TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector);
}
