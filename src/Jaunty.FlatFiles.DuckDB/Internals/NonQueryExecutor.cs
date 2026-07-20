using System.Data;

using DuckDB.NET.Data;

using Jaunty.Core;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Executes non-query SQL commands (INSERT, UPDATE, DELETE) against DuckDB.
/// Provides both sync and async implementations.
/// </summary>
internal static class NonQueryExecutor
{
    /// <summary>
    /// Executes a SQL command synchronously and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">The parameters for the command.</param>
    /// <returns>The number of rows affected.</returns>
    public static int Execute(DuckDBConnection connection, string sql, List<DuckDBParameter> parameters)
        => Execute(connection, sql, parameters, default);

    /// <summary>
    /// Executes a SQL command synchronously, honoring the transaction/timeout in <paramref name="options"/>,
    /// and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">The parameters for the command.</param>
    /// <param name="options">Command options (transaction, timeout) to apply.</param>
    /// <returns>The number of rows affected.</returns>
    public static int Execute(DuckDBConnection connection, string sql, List<DuckDBParameter> parameters, CommandOptions options)
    {
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        ApplyOptions(cmd, options);
        foreach (DuckDBParameter param in parameters)
            cmd.Parameters.Add(param);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes a SQL command asynchronously and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">The parameters for the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    public static ValueTask<int> ExecuteAsync(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        CancellationToken cancellationToken)
        => ExecuteAsync(connection, sql, parameters, default, cancellationToken);

    /// <summary>
    /// Executes a SQL command asynchronously, honoring the transaction/timeout in <paramref name="options"/>,
    /// and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">The parameters for the command.</param>
    /// <param name="options">Command options (transaction, timeout) to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    public static async ValueTask<int> ExecuteAsync(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        DuckDBCommand cmd = connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;
        ApplyOptions(cmd, options);
        foreach (DuckDBParameter param in parameters)
            cmd.Parameters.Add(param);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyOptions(DuckDBCommand cmd, CommandOptions options)
    {
        // Assign through IDbCommand so any IDbTransaction implementation is accepted (matching the
        // convention used by Jaunty's core CommandOptions handling elsewhere in the codebase);
        // it must be a transaction obtained from this same connection.
        if (options.Transaction is not null)
            ((IDbCommand)cmd).Transaction = options.Transaction;

        if (options.CommandTimeout.HasValue)
            cmd.CommandTimeout = options.CommandTimeout.Value;
    }
}
