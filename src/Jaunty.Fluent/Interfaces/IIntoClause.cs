using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the INTO clause for INSERT operations - the entry point for specifying values.
/// </summary>
public interface IIntoClause<T> where T : new()
{
    /// <summary>
    /// Specifies values to insert using an entity object.
    /// </summary>
    /// <param name="entity">The entity containing values to insert.</param>
    IValuesClause<T> Values(T entity);

    /// <summary>
    /// Specifies values to insert using an anonymous object.
    /// Property names should match entity property names.
    /// </summary>
    /// <param name="values">Anonymous object with properties to insert.</param>
    IValuesClause<T> Values(object values);

    /// <summary>
    /// Specifies a single column and value to insert.
    /// </summary>
    IValuesClause<T> Value<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    /// <summary>
    /// Specifies a single column and value to insert using column name.
    /// </summary>
    IValuesClause<T> Value(string column, object? value);
}

/// <summary>
/// Represents the VALUES clause for INSERT operations - allows adding more values and executing.
/// </summary>
public interface IValuesClause<T> where T : new()
{
    /// <summary>
    /// Adds another column and value to the INSERT.
    /// </summary>
    IValuesClause<T> Value<TValue>(Expression<Func<T, TValue>> selector, TValue value);

    /// <summary>
    /// Adds another column and value to the INSERT using column name.
    /// </summary>
    IValuesClause<T> Value(string column, object? value);

    /// <summary>
    /// Executes the INSERT statement.
    /// </summary>
    /// <returns>
    /// For identity columns: the generated identity value.
    /// For non-identity: number of rows affected (always 1 for single insert).
    /// </returns>
    long Insert();

    /// <summary>
    /// Asynchronously executes the INSERT statement.
    /// </summary>
    Task<long> InsertAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the INSERT SQL that would be executed (for debugging).
    /// </summary>
    string ToSql();
}
