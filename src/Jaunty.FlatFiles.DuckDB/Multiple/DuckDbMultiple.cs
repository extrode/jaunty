using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;

using DuckDB.NET.Data;

using Jaunty.Core;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    // Cache reflected properties for QueryMultiple(sql, parameters) anonymous/POCO parameter
    // objects to avoid repeated reflection lookups on every call for the same parameters type.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _multipleParameterPropertyCache = new();

    private static PropertyInfo[] GetCachedParameterProperties(Type parametersType)
        => _multipleParameterPropertyCache.GetOrAdd(parametersType, static t => t.GetProperties());

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
    ///     "SELECT * FROM sales WHERE revenue &gt; $MinRevenue; SELECT * FROM inventory WHERE stock &lt; $MaxStock",
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
        try
        {
            cmd.CommandText = sql;

            if (parameters != null)
            {
                // Bound by name, always - so the SQL must spell its placeholders $Foo, matching the
                // property name. Verified: "SELECT $Val" binds; "SELECT @Val" fails with
                // 'Binder Error: Referenced column "Val" not found' because DuckDB does not treat @ as
                // a placeholder prefix at all, and positional ? cannot be used through this overload
                // since every parameter added here is named. Hence the $ spelling in the examples above.
                foreach (PropertyInfo prop in GetCachedParameterProperties(parameters.GetType()))
                {
                    DbParameter param = cmd.CreateParameter();
                    param.ParameterName = prop.Name;
                    param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }
            }

            DuckDBDataReader reader = cmd.ExecuteReader();
            // The GridReader takes ownership of cmd from here on and disposes it alongside the reader.
            return new GridReader(reader, _connection, false, cmd);
        }
        catch
        {
            cmd.Dispose();
            throw;
        }
    }
}