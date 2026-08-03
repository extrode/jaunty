using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// AUD-R35. The WHERE clause after a <c>Take</c> or a <c>Skip</c>, from which the write
/// terminals are unreachable at compile time. See <see cref="IPagedClause{T}"/> for why.
/// </summary>
public interface IPagedWhereClause<T> : IWhereClause<T> where T : new()
{
    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> And(string column, object? value);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> And(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndRaw(string rawSql);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndRaw(string rawSql, object parameters);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> Or(string column, object? value);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> Or(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrRaw(string rawSql);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrRaw(string rawSql, object parameters);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> AndNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IWhereClause{T}"/>
    new IPagedWhereClause<T> OrNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE - BuildDeleteSql reads only the table and the WHERE conditions, so this would delete every matching row rather than the paged subset. Remove the Take/Skip, or select the rows first and delete them by key.",
        error: true)]
    new int Delete();

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE - BuildDeleteSql reads only the table and the WHERE conditions, so this would delete every matching row rather than the paged subset. Remove the Take/Skip, or select the rows first and delete them by key.",
        error: true)]
    new int Delete(CommandOptions options);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE - BuildDeleteSql reads only the table and the WHERE conditions, so this would delete every matching row rather than the paged subset. Remove the Take/Skip, or select the rows first and delete them by key.",
        error: true)]
    new Task<int> DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>Not available after Take/Skip; see the compile error for what to write instead.</summary>
    [Obsolete(
        "Take/Skip are not carried into DELETE - BuildDeleteSql reads only the table and the WHERE conditions, so this would delete every matching row rather than the paged subset. Remove the Take/Skip, or select the rows first and delete them by key.",
        error: true)]
    new Task<int> DeleteAsync(CommandOptions options, CancellationToken cancellationToken = default);
}
