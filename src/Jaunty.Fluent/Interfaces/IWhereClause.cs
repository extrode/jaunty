using System.Data;
using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the WHERE clause - allows chaining additional conditions.
/// </summary>
public interface IWhereClause<T> : IQueryTerminal<T> where T : new()
{
    // AND conditions
    /// <summary>
    /// Adds AND condition using column name and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to match.</param>
    IWhereClause<T> And(string column, object? value);

    /// <summary>
    /// Adds AND condition using expression predicate.
    /// </summary>
    /// <param name="predicate">Expression predicate for the condition.</param>
    IWhereClause<T> And(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds AND condition using raw SQL.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    IWhereClause<T> AndRaw(string rawSql);

    /// <summary>
    /// Adds AND condition using raw SQL with parameters.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">Anonymous object containing parameter values.</param>
    IWhereClause<T> AndRaw(string rawSql, object parameters);

    // OR conditions
    /// <summary>
    /// Adds OR condition using column name and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to match.</param>
    IWhereClause<T> Or(string column, object? value);

    /// <summary>
    /// Adds OR condition using expression predicate.
    /// </summary>
    /// <param name="predicate">Expression predicate for the condition.</param>
    IWhereClause<T> Or(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds OR condition using raw SQL.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    IWhereClause<T> OrRaw(string rawSql);

    /// <summary>
    /// Adds OR condition using raw SQL with parameters.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">Anonymous object containing parameter values.</param>
    IWhereClause<T> OrRaw(string rawSql, object parameters);

    // AND IN / NOT IN - collection-based filtering
    /// <summary>
    /// Adds AND condition where the column value is in the specified collection.
    /// </summary>
    IWhereClause<T> AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds AND condition where the column value is NOT in the specified collection.
    /// </summary>
    IWhereClause<T> AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    // OR IN / NOT IN
    /// <summary>
    /// Adds OR condition where the column value is in the specified collection.
    /// </summary>
    IWhereClause<T> OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds OR condition where the column value is NOT in the specified collection.
    /// </summary>
    IWhereClause<T> OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    // AND BETWEEN / NOT BETWEEN - range filtering
    /// <summary>
    /// Adds AND condition where the column value is between the specified range (inclusive).
    /// </summary>
    IWhereClause<T> AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds AND condition where the column value is NOT between the specified range.
    /// </summary>
    IWhereClause<T> AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    // OR BETWEEN / NOT BETWEEN
    /// <summary>
    /// Adds OR condition where the column value is between the specified range (inclusive).
    /// </summary>
    IWhereClause<T> OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds OR condition where the column value is NOT between the specified range.
    /// </summary>
    IWhereClause<T> OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    // AND EXISTS / NOT EXISTS
    /// <summary>
    /// Adds AND EXISTS condition where a correlated subquery returns any rows.
    /// </summary>
    IWhereClause<T> AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds AND NOT EXISTS condition where a correlated subquery returns no rows.
    /// </summary>
    IWhereClause<T> AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    // OR EXISTS / NOT EXISTS
    /// <summary>
    /// Adds OR EXISTS condition where a correlated subquery returns any rows.
    /// </summary>
    IWhereClause<T> OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds OR NOT EXISTS condition where a correlated subquery returns no rows.
    /// </summary>
    IWhereClause<T> OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    // DELETE operation
    /// <summary>
    /// Deletes rows matching the WHERE conditions.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int Delete();

    /// <summary>
    /// Deletes rows matching the WHERE conditions, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <returns>Number of rows affected.</returns>
    int Delete(CommandOptions options);

    /// <summary>
    /// Asynchronously deletes rows matching the WHERE conditions.
    /// </summary>
    Task<int> DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes rows matching the WHERE conditions, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<int> DeleteAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the DELETE statement that <see cref="Delete()"/> and
    /// <see cref="DeleteAsync(CancellationToken)"/> would run, without running it.
    /// </summary>
    /// <remarks>
    /// AUD-R35-214. The inherited <c>IQueryTerminal&lt;T&gt;.ToSql()</c> returns the SELECT this
    /// chain would run, so on a chain destined for a delete it previewed a different statement
    /// against the same conditions - and nothing on the interface reached the DELETE at all. This is
    /// the delete-side counterpart of <c>IUpdateWhereClause&lt;T&gt;.ToSql()</c>, which returns the
    /// UPDATE. The statement carries the WHERE conditions but no ORDER BY, TOP or LIMIT: see
    /// <see cref="IPagedClause{T}"/> for why paging cannot be carried into a delete.
    /// </remarks>
    /// <returns>The DELETE statement, with parameter placeholders left in place.</returns>
    string ToDeleteSql();

    // ORDER BY - expression-based
    /// <summary>
    /// Orders results by the specified column (ascending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);

    /// <summary>
    /// Orders results by the specified column (descending).
    /// </summary>
    /// <param name="keySelector">Expression selecting the column to order by.</param>
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    // ORDER BY - string-based
    /// <summary>
    /// Orders results by the specified column (ascending).
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    IOrderByClause<T> OrderBy(string column);

    /// <summary>
    /// Orders results by the specified column (descending).
    /// </summary>
    /// <param name="column">The column name to order by.</param>
    IOrderByClause<T> OrderByDescending(string column);

    // TOP/LIMIT
    /// <summary>
    /// Limits the number of rows returned.
    /// </summary>
    /// <param name="count">The maximum number of rows to return.</param>
    IPagedWhereClause<T> Take(int count);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    IPagedWhereClause<T> Skip(int count);

    // GROUP BY
    /// <summary>
    /// Groups results by the specified key.
    /// </summary>
    IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector);

    // AND IN SUBQUERY / NOT IN SUBQUERY
    /// <summary>
    /// Adds AND condition where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IWhereClause<T> AndInSubquery<TValue, TSubquery>(
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
    IWhereClause<T> AndNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    // OR IN SUBQUERY / NOT IN SUBQUERY
    /// <summary>
    /// Adds OR condition where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IWhereClause<T> OrInSubquery<TValue, TSubquery>(
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
    IWhereClause<T> OrNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();
}