using System.Data.Common;
using System.Reflection;

using DuckDB.NET.Data;

using Jaunty.Core;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <summary>
    /// Executes a SQL query that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// Use this method when you need to execute a query that returns multiple result sets in a single database round-trip.
    /// The <see cref="GridReader"/> must be disposed after use, or use a <c>using</c> statement.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open while the <see cref="GridReader"/> is in use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic multiple query
    /// using var grid = db.QueryMultiple(
    ///     "SELECT * FROM sales WHERE revenue &gt; 1000; SELECT * FROM inventory WHERE stock &lt; 10");
    ///
    /// var highRevenueSales = grid.Read&lt;SalesRecord&gt;();
    /// var lowStockItems = grid.Read&lt;InventoryItem&gt;();
    /// </code>
    /// </example>
    public GridReader QueryMultiple(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return ExecuteQueryMultiple(sql, null);
    }

    /// <summary>
    /// Executes a SQL query with parameters that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <example>
    /// <code>
    /// // Multiple queries with parameters
    /// using var grid = db.QueryMultiple(
    ///     "SELECT * FROM sales WHERE revenue &gt; @MinRevenue; SELECT * FROM inventory WHERE stock &lt; @MaxStock",
    ///     new { MinRevenue = 1000, MaxStock = 10 });
    ///
    /// var highRevenueSales = grid.Read&lt;SalesRecord&gt;();
    /// var lowStockItems = grid.Read&lt;InventoryItem&gt;();
    /// </code>
    /// </example>
    public GridReader QueryMultiple(string sql, object parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return ExecuteQueryMultiple(sql, parameters);
    }

    private GridReader ExecuteQueryMultiple(string sql, object? parameters)
    {
        DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null)
        {
            // DuckDB uses positional parameters ($1, $2, ...)
            // For simplicity, we'll use named parameters and let DuckDB handle the binding
            foreach (PropertyInfo prop in parameters.GetType().GetProperties())
            {
                DbParameter param = cmd.CreateParameter();
                param.ParameterName = prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        DuckDBDataReader reader = cmd.ExecuteReader();
        return new GridReader(reader, _connection, false);
    }
}