using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents DISTINCT selection - provides terminal operations and WHERE clause.
/// </summary>
public interface IDistinctClause<T> : IQueryTerminal<T> where T : new()
{
    /// <summary>
    /// Adds a WHERE clause with a column and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> Where(string column, object? value);

    /// <summary>
    /// Adds a WHERE clause with an expression-based predicate.
    /// </summary>
    /// <param name="predicate">The predicate expression.</param>
    /// <returns>The query with the WHERE clause applied.</returns>
    IWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    // AUD-R35-204. This interface declared only the two Where overloads while IFromClause<T> offers
    // nine more predicate forms, and IWhereClause<T> declares no Distinct(), so neither ordering
    // could express DISTINCT with any of them - `SELECT DISTINCT ... WHERE x IN (...)` was
    // unreachable through the fluent API. FluentSubqueryTests records having had to move a
    // .Distinct() onto the subquery side for exactly that reason. QueryBuilder<T> already implements
    // every one of these publicly on the same instance and `_distinct` is a plain field that
    // survives them, so the narrowing was accidental rather than a guard: unlike the join methods,
    // which AUD-R26-051 deliberately keeps off the paged surface, nothing here is dropped silently.

    /// <inheritdoc cref="IFromClause{T}.WhereRaw(string)"/>
    IWhereClause<T> WhereRaw(string rawSql);

    /// <inheritdoc cref="IFromClause{T}.WhereRaw(string, object)"/>
    IWhereClause<T> WhereRaw(string rawSql, object parameters);

    /// <inheritdoc cref="IFromClause{T}.WhereIn{TValue}(Expression{Func{T, TValue}}, IEnumerable{TValue})"/>
    IWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}.WhereNotIn{TValue}(Expression{Func{T, TValue}}, IEnumerable{TValue})"/>
    IWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <inheritdoc cref="IFromClause{T}.WhereBetween{TValue}(Expression{Func{T, TValue}}, TValue, TValue)"/>
    IWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}.WhereNotBetween{TValue}(Expression{Func{T, TValue}}, TValue, TValue)"/>
    IWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <inheritdoc cref="IFromClause{T}.WhereExists{TSubquery}(Expression{Func{T, TSubquery, bool}})"/>
    IWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereNotExists{TSubquery}(Expression{Func{T, TSubquery, bool}})"/>
    IWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereInSubquery{TValue, TSubquery}(Expression{Func{T, TValue}}, Expression{Func{TSubquery, TValue}}, IQueryTerminal{TSubquery})"/>
    IWhereClause<T> WhereInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <inheritdoc cref="IFromClause{T}.WhereNotInSubquery{TValue, TSubquery}(Expression{Func{T, TValue}}, Expression{Func{TSubquery, TValue}}, IQueryTerminal{TSubquery})"/>
    IWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Adds an ORDER BY clause in ascending order.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause in descending order.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause in ascending order by column name.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderBy(string column);

    /// <summary>
    /// Adds an ORDER BY clause in descending order by column name.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The query with the ORDER BY clause applied.</returns>
    IOrderByClause<T> OrderByDescending(string column);

    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows.</param>
    /// <returns>The query with the LIMIT clause applied.</returns>
    IDistinctClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query with the OFFSET clause applied.</returns>
    IDistinctClause<T> Skip(int count);
}