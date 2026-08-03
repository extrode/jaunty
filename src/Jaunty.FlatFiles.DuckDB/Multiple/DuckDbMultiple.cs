using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    // Cache reflected properties for QueryMultiple(sql, parameters) anonymous/POCO parameter
    // objects to avoid repeated reflection lookups on every call for the same parameters type.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _multipleParameterPropertyCache = new();

    private static PropertyInfo[] GetCachedParameterProperties(Type parametersType)
        // AOT-SAFE: FlatFiles.DuckDB is reflection-based by design and on no AOT publish path; see MappedPropertyFilter.
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

        return ExecuteQueryMultiple(sql, null, default);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets, with command options.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// AUD-R35-076: none of the four <c>QueryMultiple</c>/<c>QueryMultipleAsync</c> entry points had
    /// an options overload and neither <c>ExecuteQueryMultipleDirect</c> nor its async twin called
    /// <c>NonQueryExecutor.ApplyOptions</c>, so a multi-result-set read could not be enlisted in the
    /// transaction or given the timeout its <c>Insert</c>/<c>Update</c>/<c>Delete</c> neighbours
    /// were given. <c>QueryMultiEntity</c> had this gap closed in AUD-R26-068 and <c>Query&lt;T&gt;</c>
    /// in AUD-R32-009, both for the identical stated reason; <c>QueryMultiple</c> was the one read
    /// surface still missing it.
    /// </remarks>
    public GridReader QueryMultiple(string sql, CommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return ExecuteQueryMultiple(sql, null, options);
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

        return ExecuteQueryMultiple(sql, parameters, default);
    }

    /// <inheritdoc cref="QueryMultiple(string, CommandOptions)"/>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    public GridReader QueryMultiple(string sql, object parameters, CommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return ExecuteQueryMultiple(sql, parameters, options);
    }

    private GridReader ExecuteQueryMultiple(string sql, object? parameters, CommandOptions options)
        => CommandObservation.Execute(
            sql, parameters, _connection, DuckDbObservation.Text,
            () => ExecuteQueryMultipleDirect(sql, parameters, options));

    private GridReader ExecuteQueryMultipleDirect(string sql, object? parameters, CommandOptions options)
    {
        CommandObservation.Log(sql, parameters);

        DuckDBCommand cmd = _connection.CreateCommand();
        try
        {
            cmd.CommandText = sql;
            NonQueryExecutor.ApplyOptions(cmd, options);

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