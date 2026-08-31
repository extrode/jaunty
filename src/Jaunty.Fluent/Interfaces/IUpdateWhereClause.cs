using System.Data;
using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the WHERE clause for UPDATE operations - allows chaining conditions and executing the update.
/// </summary>
public interface IUpdateWhereClause<T> where T : new()
{
    // AND conditions
    /// <summary>
    /// Adds AND condition using column name and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to match.</param>
    IUpdateWhereClause<T> And(string column, object? value);

    /// <summary>
    /// Adds AND condition using expression predicate.
    /// </summary>
    /// <param name="predicate">Expression predicate for the condition.</param>
    IUpdateWhereClause<T> And(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds AND condition using raw SQL.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    IUpdateWhereClause<T> AndRaw(string rawSql);

    /// <summary>
    /// Adds AND condition using raw SQL with parameters.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">Anonymous object containing parameter values.</param>
    IUpdateWhereClause<T> AndRaw(string rawSql, object parameters);

    // OR conditions
    /// <summary>
    /// Adds OR condition using column name and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to match.</param>
    IUpdateWhereClause<T> Or(string column, object? value);

    /// <summary>
    /// Adds OR condition using expression predicate.
    /// </summary>
    /// <param name="predicate">Expression predicate for the condition.</param>
    IUpdateWhereClause<T> Or(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds OR condition using raw SQL.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    IUpdateWhereClause<T> OrRaw(string rawSql);

    /// <summary>
    /// Adds OR condition using raw SQL with parameters.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">Anonymous object containing parameter values.</param>
    IUpdateWhereClause<T> OrRaw(string rawSql, object parameters);

    // AND IN / NOT IN - collection-based filtering
    /// <summary>
    /// Adds AND condition where the column value is in the specified collection.
    /// </summary>
    IUpdateWhereClause<T> AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds AND condition where the column value is NOT in the specified collection.
    /// </summary>
    IUpdateWhereClause<T> AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    // OR IN / NOT IN
    /// <summary>
    /// Adds OR condition where the column value is in the specified collection.
    /// </summary>
    IUpdateWhereClause<T> OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds OR condition where the column value is NOT in the specified collection.
    /// </summary>
    IUpdateWhereClause<T> OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    // AND/OR BETWEEN - range filtering. These mirror IWhereClause<T>: an UPDATE's WHERE clause is
    // built by the same QueryBuilder and produces the same SQL, so a predicate expressible when
    // selecting or deleting is expressible when updating.
    /// <summary>
    /// Adds AND condition where the column value is between the specified range (inclusive).
    /// </summary>
    IUpdateWhereClause<T> AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds AND condition where the column value is NOT between the specified range.
    /// </summary>
    IUpdateWhereClause<T> AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds OR condition where the column value is between the specified range (inclusive).
    /// </summary>
    IUpdateWhereClause<T> OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds OR condition where the column value is NOT between the specified range.
    /// </summary>
    IUpdateWhereClause<T> OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    // AND/OR EXISTS - correlated subqueries
    /// <summary>
    /// Adds AND EXISTS condition where a correlated subquery returns any rows.
    /// </summary>
    IUpdateWhereClause<T> AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds AND NOT EXISTS condition where a correlated subquery returns no rows.
    /// </summary>
    IUpdateWhereClause<T> AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds OR EXISTS condition where a correlated subquery returns any rows.
    /// </summary>
    IUpdateWhereClause<T> OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds OR NOT EXISTS condition where a correlated subquery returns no rows.
    /// </summary>
    IUpdateWhereClause<T> OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    // AND/OR IN SUBQUERY
    /// <summary>
    /// Adds AND condition where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> AndInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Adds AND condition where the column value is NOT in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> AndNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Adds OR condition where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> OrInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Adds OR condition where the column value is NOT in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> OrNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    // UPDATE terminal
    /// <summary>
    /// Executes the UPDATE statement with the specified SET and WHERE clauses.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int Update();

    /// <summary>
    /// Executes the UPDATE statement with the specified SET and WHERE clauses, within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <returns>Number of rows affected.</returns>
    int Update(CommandOptions options);

    /// <summary>
    /// Asynchronously executes the UPDATE statement with the specified SET and WHERE clauses.
    /// </summary>
    Task<int> UpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the UPDATE statement with the specified SET and WHERE clauses,
    /// within the given <see cref="CommandOptions"/> (e.g.
    /// <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<int> UpdateAsync(CommandOptions options, CancellationToken cancellationToken = default);

    // SQL introspection
    /// <summary>
    /// Returns the UPDATE SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();
}