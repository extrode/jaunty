using System.Data.Common;
using System.Reflection;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <summary>
    /// Executes a SQL query that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// Use this method when you need to execute a query that returns multiple result sets in a single database round-trip.
    /// The <see cref="GridReader"/> must be disposed after use, or use an <c>await using</c> statement.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open while the <see cref="GridReader"/> is in use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic multiple query async
    /// await using var grid = await db.QueryMultipleAsync(
    ///     "SELECT * FROM sales WHERE revenue &gt; 1000; SELECT * FROM inventory WHERE stock &lt; 10");
    ///
    /// var highRevenueSales = grid.Read&lt;SalesRecord&gt;();
    /// var lowStockItems = grid.Read&lt;InventoryItem&gt;();
    /// </code>
    /// </example>
    public async ValueTask<GridReader> QueryMultipleAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteQueryMultipleAsync(sql, null, default, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="QueryMultiple(string, CommandOptions)"/>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public async ValueTask<GridReader> QueryMultipleAsync(string sql, CommandOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteQueryMultipleAsync(sql, null, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a SQL query with parameters that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <example>
    /// <code>
    /// // Multiple queries with parameters async
    /// await using var grid = await db.QueryMultipleAsync(
    ///     "SELECT * FROM sales WHERE revenue &gt; $MinRevenue; SELECT * FROM inventory WHERE stock &lt; $MaxStock",
    ///     new { MinRevenue = 1000, MaxStock = 10 });
    ///
    /// var highRevenueSales = grid.Read&lt;SalesRecord&gt;();
    /// var lowStockItems = grid.Read&lt;InventoryItem&gt;();
    /// </code>
    /// </example>
    public async ValueTask<GridReader> QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteQueryMultipleAsync(sql, parameters, default, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="QueryMultiple(string, CommandOptions)"/>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public async ValueTask<GridReader> QueryMultipleAsync(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteQueryMultipleAsync(sql, parameters, options, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<GridReader> ExecuteQueryMultipleAsync(string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
        => CommandObservation.ExecuteAsync(
            sql, parameters, _connection, DuckDbObservation.Text,
            () => ExecuteQueryMultipleDirectAsync(sql, parameters, options, cancellationToken), cancellationToken);

    private async ValueTask<GridReader> ExecuteQueryMultipleDirectAsync(string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
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

            DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            // The GridReader takes ownership of cmd from here on and disposes it alongside the reader.
            return new GridReader(reader, _connection, false, cmd);
        }
        catch
        {
            await cmd.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}