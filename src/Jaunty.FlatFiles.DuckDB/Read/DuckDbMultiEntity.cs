using Jaunty.Core;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <summary>
    /// Executes a SQL query and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type.</typeparam>
    /// <typeparam name="T2">The second entity type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of tuples containing mapped entities.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong>. All public writable properties on both
    /// <typeparamref name="T1"/> and <typeparamref name="T2"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var results = db.QueryMultiEntity&lt;SalesRecord, CustomerProfile&gt;(
    ///     "SELECT s.id AS Id, s.product_name, c.id AS CustomerId, c.name FROM sales s JOIN customers c ON s.customer_id = c.id");
    ///
    /// foreach (var (sale, customer) in results)
    /// {
    ///     Console.WriteLine($"{sale.ProductName} sold to {customer.Name}");
    /// }
    /// </code>
    /// </example>
    public List<(T1, T2)> QueryMultiEntity<T1, T2>(string sql) where T1 : new() where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _connection.Query<T1, T2>(sql);
    }

    /// <summary>
    /// Executes a SQL query with parameters and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type.</typeparam>
    /// <typeparam name="T2">The second entity type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>A list of tuples containing mapped entities.</returns>
    public List<(T1, T2)> QueryMultiEntity<T1, T2>(string sql, object parameters) where T1 : new() where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _connection.Query<T1, T2>(sql, parameters);
    }

    /// <summary>
    /// Executes a SQL query and maps columns to two entity types, using <paramref name="options"/>
    /// for the command.
    /// </summary>
    /// <typeparam name="T1">The first entity type.</typeparam>
    /// <typeparam name="T2">The second entity type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options - transaction, timeout, command type.</param>
    /// <returns>A list of tuples containing mapped entities.</returns>
    /// <remarks>
    /// AUD-R26-068: <c>QueryMultiEntity</c> was the one read surface on <see cref="DuckDb"/> with no
    /// options overload at all, while <c>Query</c>, <c>Insert</c>, <c>Update</c> and <c>Delete</c>
    /// all had one - so these reads could not be put in the same transaction as the writes beside
    /// them.
    /// </remarks>
    public List<(T1, T2)> QueryMultiEntity<T1, T2>(string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _connection.Query<T1, T2>(sql, options);
    }

    /// <inheritdoc cref="QueryMultiEntity{T1, T2}(string, CommandOptions{ValueTuple{T1, T2}})"/>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options - transaction, timeout, command type.</param>
    public List<(T1, T2)> QueryMultiEntity<T1, T2>(string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _connection.Query<T1, T2>(sql, parameters, options);
    }
}
