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
#pragma warning disable CS0618 // Type or member is obsolete
        return _connection.Query<T1, T2>(sql);
#pragma warning restore CS0618
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
#pragma warning disable CS0618 // Type or member is obsolete
        return _connection.Query<T1, T2>(sql, parameters);
#pragma warning restore CS0618
    }
}
