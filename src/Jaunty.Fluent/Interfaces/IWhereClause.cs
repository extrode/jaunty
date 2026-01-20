using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the WHERE clause - allows chaining additional conditions.
/// </summary>
public interface IWhereClause<T> : IQueryTerminal<T> where T : new()
{
    // AND conditions
    IWhereClause<T> And(string column, object? value);
    IWhereClause<T> And(Expression<Func<T, bool>> predicate);
    IWhereClause<T> AndRaw(string rawSql);
    IWhereClause<T> AndRaw(string rawSql, object parameters);

    // OR conditions
    IWhereClause<T> Or(string column, object? value);
    IWhereClause<T> Or(Expression<Func<T, bool>> predicate);
    IWhereClause<T> OrRaw(string rawSql);
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

    // ORDER BY - expression-based
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    // ORDER BY - string-based
    IOrderByClause<T> OrderBy(string column);
    IOrderByClause<T> OrderByDescending(string column);

    // TOP/LIMIT
    IWhereClause<T> Take(int count);
    IWhereClause<T> Skip(int count);

    // GROUP BY
    /// <summary>
    /// Groups results by the specified key.
    /// </summary>
    IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector);
}
