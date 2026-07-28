using System.Data;
using System.Data.Common;

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
    public CommandContext(string commandText, object? parameters, IDbConnection connection, CommandType commandType, TimeSpan elapsed = default, Exception? exception = null)
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
    /// <para>
    /// This is the caller's parameters instance, stored verbatim - an anonymous type, a
    /// <see cref="System.Collections.IDictionary"/>, or whatever else was passed to the query. It
    /// therefore exposes parameter <strong>values</strong> as well as names, and nothing is
    /// redacted on the way in.
    /// </para>
    /// <para>
    /// <strong>Interceptors that log or forward this object are responsible for their own
    /// redaction.</strong> Jaunty's built-in <c>LoggingInterceptor</c> masks values whose parameter
    /// name matches <c>LoggingConfiguration.SensitiveParameterNames</c>, but that masking is opt-in
    /// and applies only to that interceptor - it does not protect a custom one that reads this
    /// property directly.
    /// </para>
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
    /// Gets the connection string with sensitive values (such as passwords) redacted.
    /// </summary>
    /// <remarks>
    /// The underlying provider's connection string is parsed with <see cref="DbConnectionStringBuilder"/>
    /// and any key that case-insensitively matches a known sensitive keyword (e.g. "Password", "Pwd",
    /// "User Password") is removed before the string is rebuilt. This sanitization is performed
    /// unconditionally; it does not rely on the provider itself withholding sensitive data.
    /// If the connection string cannot be parsed, "(unknown)" is returned instead of the raw value.
    /// </remarks>
    public string ConnectionString => SanitizeConnectionString(Connection.ConnectionString);

    private static readonly string[] SensitiveConnectionStringKeys = { "Password", "Pwd", "User Password" };

    private static string SanitizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "(unknown)";

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            foreach (var sensitiveKey in SensitiveConnectionStringKeys)
            {
                if (builder.ContainsKey(sensitiveKey))
                    builder.Remove(sensitiveKey);
            }

            return builder.ConnectionString ?? "(unknown)";
        }
        catch (ArgumentException)
        {
            return "(unknown)";
        }
    }
}
