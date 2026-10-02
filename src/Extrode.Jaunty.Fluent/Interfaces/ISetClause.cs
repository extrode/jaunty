using System.Data;
using System.Linq.Expressions;

using Extrode.Jaunty.Core;

namespace Extrode.Jaunty.Fluent;

/// <summary>
/// Represents the SET clause for UPDATE operations - allows chaining multiple column assignments.
/// </summary>
public interface ISetClause<T> where T : new()
{
    // SET column chaining - expression-based
    /// <summary>
    /// Sets a column to a specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="selector">Expression selecting the column to set.</param>
    /// <param name="value">The value to set.</param>
    ISetClause<T> Set<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    // SET column chaining - string-based
    /// <summary>
    /// Sets a column to a specified value using column name.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to set.</param>
    ISetClause<T> Set(string column, object? value);

    // SET multiple columns from anonymous object
    /// <summary>
    /// Sets multiple columns from an anonymous object's properties.
    /// </summary>
    /// <param name="values">Anonymous object containing column-value pairs.</param>
    ISetClause<T> Set(object values);

    // WHERE clause - expression-based predicate
    /// <summary>
    /// Adds a WHERE condition to the UPDATE.
    /// </summary>
    /// <param name="predicate">Expression predicate for the WHERE condition.</param>
    IUpdateWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    // WHERE clause - column + value
    /// <summary>
    /// Adds a WHERE condition using column name and value.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to match.</param>
    IUpdateWhereClause<T> Where(string column, object? value);

    // WHERE clause - raw SQL
    /// <summary>
    /// Adds a WHERE condition using raw SQL.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    IUpdateWhereClause<T> WhereRaw(string rawSql);

    /// <summary>
    /// Adds a WHERE condition using raw SQL with parameters.
    /// </summary>
    /// <param name="rawSql">The raw SQL condition.</param>
    /// <param name="parameters">Anonymous object containing parameter values.</param>
    IUpdateWhereClause<T> WhereRaw(string rawSql, object parameters);

    // AUD-R38-103: the same opening filters IFromClause<T> and IDistinctClause<T> offer. CF-9 gave
    // IUpdateWhereClause<T> the And/Or forms; without these an UPDATE could only reach them after a
    // first Where.
    /// <summary>
    /// Adds a WHERE condition where the column value is in the specified collection.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the collection.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="values">Collection of values to match against.</param>
    IUpdateWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds a WHERE condition where the column value is NOT in the specified collection.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the collection.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="values">Collection of values to exclude.</param>
    IUpdateWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);

    /// <summary>
    /// Adds a WHERE condition where the column value is between the specified range (inclusive).
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="from">The lower bound.</param>
    /// <param name="to">The upper bound.</param>
    IUpdateWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds a WHERE condition where the column value is NOT between the specified range.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="from">The lower bound.</param>
    /// <param name="to">The upper bound.</param>
    IUpdateWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to);

    /// <summary>
    /// Adds a WHERE EXISTS condition where a correlated subquery returns any rows.
    /// </summary>
    /// <typeparam name="TSubquery">The type of entity in the subquery.</typeparam>
    /// <param name="predicate">Expression relating the updated entity to the subquery entity.</param>
    IUpdateWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds a WHERE NOT EXISTS condition where a correlated subquery returns no rows.
    /// </summary>
    /// <typeparam name="TSubquery">The type of entity in the subquery.</typeparam>
    /// <param name="predicate">Expression relating the updated entity to the subquery entity.</param>
    IUpdateWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new();

    /// <summary>
    /// Adds a WHERE condition where the column value is in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> WhereInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    /// <summary>
    /// Adds a WHERE condition where the column value is NOT in the result of a subquery.
    /// </summary>
    /// <typeparam name="TValue">The type of the column value.</typeparam>
    /// <typeparam name="TSubquery">The subquery entity type.</typeparam>
    /// <param name="selector">Expression selecting the column to filter.</param>
    /// <param name="subquerySelector">Expression selecting the column from the subquery.</param>
    /// <param name="subquery">The subquery terminal.</param>
    IUpdateWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new();

    // UPDATE ALL (no WHERE) - use with caution
    /// <summary>
    /// Updates all rows in the table without a WHERE clause. Use with caution.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int UpdateAll();

    /// <summary>
    /// Updates all rows in the table without a WHERE clause, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// Use with caution.
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <returns>Number of rows affected.</returns>
    int UpdateAll(CommandOptions options);

    /// <summary>
    /// Asynchronously updates all rows in the table without a WHERE clause.
    /// </summary>
    Task<int> UpdateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates all rows in the table without a WHERE clause, executing within the
    /// given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    /// <param name="options">Options controlling command execution, such as an ambient transaction.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<int> UpdateAllAsync(CommandOptions options, CancellationToken cancellationToken = default);

    // SQL introspection
    /// <summary>
    /// Returns the UPDATE SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();
}