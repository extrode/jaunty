using System.Data;

namespace Jaunty.Interceptors;

/// <summary>
/// Provides context for command execution intercepted by <see cref="ICommandInterceptor"/>.
/// </summary>
/// <remarks>
/// This class is immutable to ensure interceptors cannot inadvertently modify
/// the command being executed. For scenarios requiring modification, use a custom
/// context wrapper.
/// </remarks>
public sealed class CommandContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandContext"/> class.
    /// </summary>
    /// <param name="commandText">The SQL command text being executed.</param>
    /// <param name="parameters">The parameters bound to the command, or null if none.</param>
    /// <param name="connection">The database connection being used.</param>
    /// <param name="commandType">The type of command being executed.</param>
    /// <param name="elapsed">The elapsed time for command execution, or <see cref="TimeSpan.Zero"/> if not yet executed.</param>
    /// <param name="exception">The exception that occurred, or null if execution was successful.</param>
    public CommandContext(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        TimeSpan elapsed = default,
        Exception? exception = null)
    {
        CommandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        Parameters = parameters;
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        CommandType = commandType;
        Elapsed = elapsed;
        Exception = exception;
    }

    /// <summary>
    /// Gets the SQL command text being executed.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Gets the parameters bound to the command, or null if none.
    /// </summary>
    /// <remarks>
    /// The actual parameter values are not exposed for security reasons.
    /// Use <see cref="GetParameterNames"/> to inspect parameter names.
    /// </remarks>
    public object? Parameters { get; }

    /// <summary>
    /// Gets the database connection being used.
    /// </summary>
    public IDbConnection Connection { get; }

    /// <summary>
    /// Gets the type of command being executed.
    /// </summary>
    public CommandType CommandType { get; }

    /// <summary>
    /// Gets the elapsed time for command execution.
    /// </summary>
    /// <remarks>
    /// This is <see cref="TimeSpan.Zero"/> during <see cref="ICommandInterceptor.OnCommandExecutingAsync"/>
    /// and contains the actual execution time during <see cref="ICommandInterceptor.OnCommandExecutedAsync"/>.
    /// </remarks>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the exception that occurred during execution, or null if successful.
    /// </summary>
    /// <remarks>
    /// This is only populated during <see cref="ICommandInterceptor.OnCommandFailedAsync"/>.
    /// </remarks>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the database provider name (e.g., "System.Data.SqlClient", "Npgsql").
    /// </summary>
    public string ProviderName => Connection.GetType().Name;

    /// <summary>
    /// Gets the database name if available, or "(unknown)" if not.
    /// </summary>
    public string DatabaseName => Connection.Database ?? "(unknown)";

    /// <summary>
    /// Gets the connection string (without sensitive data if provider supports it).
    /// </summary>
    /// <remarks>
    /// Some providers may return a sanitized connection string.
    /// </remarks>
    public string ConnectionString => Connection.ConnectionString ?? "(unknown)";
}
