using System.Linq.Expressions;

namespace Jaunty.Fluent.Interfaces;

/// <summary>
/// Represents the SET clause for UPDATE operations - allows chaining multiple column assignments.
/// </summary>
public interface ISetClause<T> where T : new()
{
    // SET column chaining - expression-based
    /// <summary>
    /// Sets a column to a specified value.
    /// </summary>
    ISetClause<T> Set<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    // SET column chaining - string-based
    /// <summary>
    /// Sets a column to a specified value using column name.
    /// </summary>
    ISetClause<T> Set(string column, object? value);

    // SET multiple columns from anonymous object
    /// <summary>
    /// Sets multiple columns from an anonymous object's properties.
    /// </summary>
    ISetClause<T> Set(object values);

    // WHERE clause - expression-based predicate
    /// <summary>
    /// Adds a WHERE condition to the UPDATE.
    /// </summary>
    IUpdateWhereClause<T> Where(Expression<Func<T, bool>> predicate);

    // WHERE clause - column + value
    /// <summary>
    /// Adds a WHERE condition using column name and value.
    /// </summary>
    IUpdateWhereClause<T> Where(string column, object? value);

    // WHERE clause - raw SQL
    /// <summary>
    /// Adds a WHERE condition using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> WhereRaw(string rawSql);
    IUpdateWhereClause<T> WhereRaw(string rawSql, object parameters);

    // UPDATE ALL (no WHERE) - use with caution
    /// <summary>
    /// Updates all rows in the table without a WHERE clause. Use with caution.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int UpdateAll();

    /// <summary>
    /// Asynchronously updates all rows in the table without a WHERE clause.
    /// </summary>
    Task<int> UpdateAllAsync(CancellationToken cancellationToken = default);

    // SQL introspection
    /// <summary>
    /// Returns the UPDATE SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();
}
