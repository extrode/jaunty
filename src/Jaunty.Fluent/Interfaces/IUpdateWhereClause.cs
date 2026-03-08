using System.Linq.Expressions;

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
    IUpdateWhereClause<T> And(string column, object? value);

    /// <summary>
    /// Adds AND condition using expression predicate.
    /// </summary>
    IUpdateWhereClause<T> And(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds AND condition using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> AndRaw(string rawSql);
    IUpdateWhereClause<T> AndRaw(string rawSql, object parameters);

    // OR conditions
    /// <summary>
    /// Adds OR condition using column name and value.
    /// </summary>
    IUpdateWhereClause<T> Or(string column, object? value);

    /// <summary>
    /// Adds OR condition using expression predicate.
    /// </summary>
    IUpdateWhereClause<T> Or(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds OR condition using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> OrRaw(string rawSql);
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

    // UPDATE terminal
    /// <summary>
    /// Executes the UPDATE statement with the specified SET and WHERE clauses.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int Update();

    /// <summary>
    /// Asynchronously executes the UPDATE statement with the specified SET and WHERE clauses.
    /// </summary>
    Task<int> UpdateAsync(CancellationToken cancellationToken = default);

    // SQL introspection
    /// <summary>
    /// Returns the UPDATE SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();
}