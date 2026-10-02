using System.Linq.Expressions;

namespace Extrode.Jaunty.Fluent;

/// <summary>
/// AUD-R38 Q1. A <c>Distinct</c> query after a <c>Take</c> or a <c>Skip</c>, in either order. Its
/// <c>Where</c> family returns <see cref="IPagedWhereClause{T}"/>, so <c>Delete</c> does not
/// compile, as it does not after <c>Take(5).Where(...)</c>. See <see cref="IPagedClause{T}"/> for
/// why.
/// </summary>
public interface IPagedDistinctClause<T> : IDistinctClause<T> where T : new()
{
    /// <inheritdoc cref="IDistinctClause{T}.Where(string, object)"/>
    new IPagedWhereClause<T> Where(string column, object? value);

    /// <inheritdoc cref="IDistinctClause{T}.Where(Expression{Func{T, bool}})"/>
    new IPagedWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="IFromClause{T}.WhereRaw(string)"/>
    new IPagedWhereClause<T> WhereRaw(string rawSql);

    /// <inheritdoc cref="IFromClause{T}.WhereRaw(string, object)"/>
    new IPagedWhereClause<T> WhereRaw(string rawSql, object parameters);

    /// <inheritdoc cref="IFromClause{T}.WhereIn{TValue}(Expression{Func{T, TValue}}, IEnumerable{TValue})"/>
    new IPagedWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}.WhereNotIn{TValue}(Expression{Func{T, TValue}}, IEnumerable{TValue})"/>
    new IPagedWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}.WhereBetween{TValue}(Expression{Func{T, TValue}}, TValue, TValue)"/>
    new IPagedWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}.WhereNotBetween{TValue}(Expression{Func{T, TValue}}, TValue, TValue)"/>
    new IPagedWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}.WhereExists{TSubquery}(Expression{Func{T, TSubquery, bool}})"/>
    new IPagedWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereNotExists{TSubquery}(Expression{Func{T, TSubquery, bool}})"/>
    new IPagedWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereInSubquery{TValue, TSubquery}(Expression{Func{T, TValue}}, Expression{Func{TSubquery, TValue}}, IQueryTerminal{TSubquery})"/>
    new IPagedWhereClause<T> WhereInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereNotInSubquery{TValue, TSubquery}(Expression{Func{T, TValue}}, Expression{Func{TSubquery, TValue}}, IQueryTerminal{TSubquery})"/>
    new IPagedWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery) where TSubquery : new();
}
