using DuckDB.NET.Data;

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
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
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
    public static async ValueTask<int> ExecuteAsync(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
            cmd.Parameters.Add(param);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}