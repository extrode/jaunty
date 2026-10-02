using System.Linq.Expressions;

using Extrode.Jaunty.Core;

namespace Extrode.Jaunty.Fluent;

/// <summary>
/// AUD-R35. The query after a <c>Take</c> or a <c>Skip</c>, from which the write terminals are
/// unreachable at compile time.
/// </summary>
/// <remarks>
/// <para>
/// <c>Take</c>/<c>Skip</c> used to return the interface they were called on, which also declared
/// <c>DeleteAll</c>, <c>DeleteAllAsync</c> and <c>Set</c>, so
/// <c>From&lt;T&gt;().Take(5).DeleteAll()</c> compiled - and the paging was silently discarded,
/// because <c>BuildDeleteSql</c> and <c>BuildUpdateSql</c> read neither <c>_take</c> nor
/// <c>_skip</c>. The caller wrote "delete five rows" and every row in the table went.
/// </para>
/// <para>
/// The two neighbouring instances of the same reachability defect - paging before a join
/// (AUD-R26-051) and paging before a <c>GroupBy</c> (AUD-R33-002) - were closed with runtime
/// guards, because there Extrode.Jaunty genuinely cannot tell which of two reasonable meanings was
/// intended. Here there is no ambiguity to resolve and no reason to wait until runtime to say so,
/// so the write terminals are hidden behind <see cref="ObsoleteAttribute"/> with its error flag
/// set: the call does not compile, and the message says what to write instead.
/// </para>
/// <para>
/// The read surface is unchanged. Terminals, further paging, <c>OrderBy</c>, <c>Distinct</c>, the
/// joins and <c>GroupBy</c> are all inherited; only the <c>Where</c> family is re-declared, so that
/// <c>Take(5).Where(...)</c> stays paged and cannot reach a write terminal either.
/// </para>
/// <para>
/// <c>Distinct</c> stays paged too, through <see cref="IPagedDistinctClause{T}"/>. The compile-time
/// fence still cannot be complete: C# checks a variable's declared type, so an upcast
/// (<c>IFromClause&lt;T&gt; q = ...Take(5)</c>, or a query built conditionally) reaches
/// <c>DeleteAll</c> directly. So the builder also refuses, at runtime, any DELETE or UPDATE built
/// with paging set (<c>QueryBuilder.ThrowIfPagedWrite</c>, AUD-R38-008). The fence reports the
/// common mistake early; the runtime check is the guarantee.
/// </para>
/// </remarks>
public interface IPagedClause<T> : IFromClause<T> where T : new()
{
    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> Where(string column, object? value);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereRaw(string rawSql);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereRaw(string rawSql, object parameters);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}"/>
    new IPagedWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.Distinct"/>
    new IPagedDistinctClause<T> Distinct();

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE: SQL has no portable DELETE ... LIMIT, so this would delete every matching row rather than the paged subset. Select the keys of the rows you want, then delete them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new int DeleteAll();

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE: SQL has no portable DELETE ... LIMIT, so this would delete every matching row rather than the paged subset. Select the keys of the rows you want, then delete them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new int DeleteAll(CommandOptions options);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE: SQL has no portable DELETE ... LIMIT, so this would delete every matching row rather than the paged subset. Select the keys of the rows you want, then delete them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE: SQL has no portable DELETE ... LIMIT, so this would delete every matching row rather than the paged subset. Select the keys of the rows you want, then delete them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new Task<int> DeleteAllAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into UPDATE: SQL has no portable UPDATE ... LIMIT, so this would update every matching row rather than the paged subset. Select the keys of the rows you want, then update them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new ISetClause<T> Set<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into UPDATE: SQL has no portable UPDATE ... LIMIT, so this would update every matching row rather than the paged subset. Select the keys of the rows you want, then update them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new ISetClause<T> Set(string column, object? value);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into UPDATE: SQL has no portable UPDATE ... LIMIT, so this would update every matching row rather than the paged subset. Select the keys of the rows you want, then update them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).",
        error: true)]
    new ISetClause<T> Set(object values);
}
