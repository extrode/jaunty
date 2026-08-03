using System.Data;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.Internals;

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
        // AUD-R35-251: described once and shared by the pipeline and the logger. Both hooks want
        // the same parameter set, and Describe allocates a Dictionary sized to the parameter list,
        // so calling it in both places allocated two identical dictionaries per non-query. The
        // logger and an interceptor now see the same instance, which is also what the sync and
        // async paths in core already do.
        object described = DuckDbObservation.Describe(parameters);

        return CommandObservation.Execute(
            sql, described, connection, DuckDbObservation.Text,
            () => ExecuteDirect(connection, sql, parameters, described, options));
    }

    private static int ExecuteDirect(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        object described,
        CommandOptions options)
    {
        CommandObservation.Log(sql, described);

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
    public static ValueTask<int> ExecuteAsync(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        // AUD-R35-251: see Execute.
        object described = DuckDbObservation.Describe(parameters);

        return CommandObservation.ExecuteAsync(
            sql, described, connection, DuckDbObservation.Text,
            () => ExecuteDirectAsync(connection, sql, parameters, described, options, cancellationToken),
            cancellationToken);
    }

    private static async ValueTask<int> ExecuteDirectAsync(
        DuckDBConnection connection,
        string sql,
        List<DuckDBParameter> parameters,
        object described,
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        CommandObservation.Log(sql, described);

        DuckDBCommand cmd = connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;
        ApplyOptions(cmd, options);
        foreach (DuckDBParameter param in parameters)
            cmd.Parameters.Add(param);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// AUD-R32-009: was private. The raw-SQL read path needs the same treatment and duplicating it
    /// there is how the two drift - the transaction-validation reasoning below is the whole point.
    /// </summary>
    internal static void ApplyOptions(DuckDBCommand cmd, CommandOptions options)
    {
        // cmd.Transaction's own property type is the narrower DuckDBTransaction, so assigning
        // through IDbCommand.Transaction (typed DbTransaction) is required to reach it generically.
        // That setter still casts internally, so a non-DbTransaction IDbTransaction would otherwise
        // throw an opaque InvalidCastException - validate via AsyncTransactionValidator first
        // (mirroring Jaunty core's GetByIdSimpleCoreDirect) for a clear ArgumentException instead.
        // The transaction must still be a DuckDBTransaction obtained from this same connection;
        // DuckDB.NET itself enforces that when the value is assigned.
        if (options.Transaction is not null)
            ((IDbCommand)cmd).Transaction = global::Jaunty.AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

        if (options.CommandTimeout.HasValue)
            cmd.CommandTimeout = options.CommandTimeout.Value;
    }
}
