using System.Diagnostics;
using System.Data;

namespace Jaunty.Diagnostics;

/// <summary>
/// Emits diagnostic events for Jaunty command execution.
/// </summary>
/// <remarks>
/// <para>
/// This listener emits events at key points in the command execution lifecycle:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="CommandExecutingEventName"/> - Before command execution</description></item>
/// <item><description><see cref="CommandExecutedEventName"/> - After successful execution</description></item>
/// <item><description><see cref="CommandFailedEventName"/> - When execution fails</description></item>
/// </list>
/// <para>
/// Subscribe using <see cref="DiagnosticListener.Subscribe"/> to receive events
/// for integration with OpenTelemetry, Application Insights, or custom telemetry.
/// </para>
/// </remarks>
public sealed class JauntyDiagnosticListener : DiagnosticListener, IDisposable
{
    /// <summary>
    /// The name of the DiagnosticSource used by Jaunty.
    /// </summary>
    public const string DiagnosticSourceName = "Jaunty";

    /// <summary>
    /// Event name emitted before command execution.
    /// </summary>
    public const string CommandExecutingEventName = "Jaunty.Database.Command.Executing";

    /// <summary>
    /// Event name emitted after successful command execution.
    /// </summary>
    public const string CommandExecutedEventName = "Jaunty.Database.Command.Executed";

    /// <summary>
    /// Event name emitted when command execution fails.
    /// </summary>
    public const string CommandFailedEventName = "Jaunty.Database.Command.Failed";

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JauntyDiagnosticListener"/> class.
    /// </summary>
    public JauntyDiagnosticListener() : base(DiagnosticSourceName)
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="JauntyDiagnosticListener"/>.
    /// </summary>
    /// <remarks>
    /// This is a lazy-initialized singleton to avoid creating multiple listeners.
    /// </remarks>
    public static JauntyDiagnosticListener Instance => _instance.Value;

    private static readonly Lazy<JauntyDiagnosticListener> _instance = new(() => new JauntyDiagnosticListener());

    /// <summary>
    /// Emits a command executing event.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <remarks>
    /// Event payload includes:
    /// <list type="bullet">
    /// <item><description>CommandText - The SQL command text</description></item>
    /// <item><description>CommandType - The command type (Text, StoredProcedure, etc.)</description></item>
    /// <item><description>Database - The database name</description></item>
    /// <item><description>ConnectionId - A unique identifier for the connection</description></item>
    /// <item><description>Timestamp - UTC timestamp of the event</description></item>
    /// </list>
    /// </remarks>
    public void WriteCommandExecuting(Interceptors.CommandContext context)
    {
        if (IsEnabled() && !_disposed)
        {
            Write(CommandExecutingEventName, new CommandExecutingPayload(context));
        }
    }

    /// <summary>
    /// Emits a command executed event.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <remarks>
    /// Event payload includes all executing fields plus:
    /// <list type="bullet">
    /// <item><description>ElapsedMilliseconds - Duration of command execution</description></item>
    /// <item><description>Success - Always true for this event</description></item>
    /// </list>
    /// </remarks>
    public void WriteCommandExecuted(Interceptors.CommandContext context)
    {
        if (IsEnabled() && !_disposed)
        {
            Write(CommandExecutedEventName, new CommandExecutedPayload(context));
        }
    }

    /// <summary>
    /// Emits a command failed event.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <remarks>
    /// Event payload includes all executed fields plus:
    /// <list type="bullet">
    /// <item><description>ExceptionType - The full type name of the exception</description></item>
    /// <item><description>ExceptionMessage - The exception message</description></item>
    /// <item><description>Success - Always false for this event</description></item>
    /// </list>
    /// </remarks>
    public void WriteCommandFailed(Interceptors.CommandContext context, Exception exception)
    {
        if (IsEnabled() && !_disposed)
        {
            Write(CommandFailedEventName, new CommandFailedPayload(context, exception));
        }
    }

    /// <inheritdoc/>
    public new void Dispose()
    {
        if (!_disposed)
        {
            base.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Base class for command execution event payloads.
/// </summary>
public abstract class CommandEventPayload
{
    /// <summary>
    /// Gets the SQL command text.
    /// </summary>
    public string CommandText { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the command type (Text, StoredProcedure, TableDirect).
    /// </summary>
    public CommandType CommandType { get; protected set; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string Database { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets a unique identifier for the database connection.
    /// </summary>
    public string ConnectionId { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; protected set; }

    /// <summary>
    /// Gets the database provider name.
    /// </summary>
    public string ProviderName { get; protected set; } = string.Empty;
}

/// <summary>
/// Payload for the command executing event.
/// </summary>
public class CommandExecutingPayload : CommandEventPayload
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutingPayload"/> class.
    /// </summary>
    /// <param name="context">The command context.</param>
    public CommandExecutingPayload(Interceptors.CommandContext context)
    {
        CommandText = context.CommandText;
        CommandType = context.CommandType;
        Database = context.DatabaseName;
        ConnectionId = context.Connection.GetHashCode().ToString();
        Timestamp = DateTime.UtcNow;
        ProviderName = context.ProviderName;
    }
}

/// <summary>
/// Payload for the command executed event.
/// </summary>
public sealed class CommandExecutedPayload : CommandExecutingPayload
{
    /// <summary>
    /// Gets the elapsed time in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds { get; }

    /// <summary>
    /// Gets whether the command completed successfully.
    /// </summary>
    public bool Success => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutedPayload"/> class.
    /// </summary>
    /// <param name="context">The command context.</param>
    public CommandExecutedPayload(Interceptors.CommandContext context) : base(context)
    {
        ElapsedMilliseconds = context.Elapsed.TotalMilliseconds;
    }
}

/// <summary>
/// Payload for the command failed event.
/// </summary>
public sealed class CommandFailedPayload : CommandExecutingPayload
{
    /// <summary>
    /// Gets the elapsed time in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds { get; }

    /// <summary>
    /// Gets whether the command completed successfully.
    /// </summary>
    public bool Success => false;

    /// <summary>
    /// Gets the exception type name.
    /// </summary>
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public string ExceptionMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandFailedPayload"/> class.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="exception">The exception that occurred.</param>
    public CommandFailedPayload(Interceptors.CommandContext context, Exception exception) : base(context)
    {
        ElapsedMilliseconds = context.Elapsed.TotalMilliseconds;
        ExceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        ExceptionMessage = exception.Message;
    }
}
