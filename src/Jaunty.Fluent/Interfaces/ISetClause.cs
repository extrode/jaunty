using System.Data;
using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

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