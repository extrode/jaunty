using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

using Jaunty.Interceptors;

namespace Jaunty.Diagnostics;

/// <summary>
/// An <see cref="ICommandInterceptor"/> that audits command execution for compliance and troubleshooting.
/// </summary>
/// <remarks>
/// <para>
/// This interceptor provides audit logging for Jaunty command execution including:
/// </para>
/// <list type="bullet">
/// <item><description>Command type and SQL text</description></item>
/// <item><description>Execution duration</description></item>
/// <item><description>Success/failure status</description></item>
/// <item><description>Exception details on failure</description></item>
/// </list>
/// <para>
/// Unlike <see cref="LoggingInterceptor"/>, this interceptor does not log parameter values
/// to avoid capturing sensitive data in audit logs.
/// </para>
/// </remarks>
public sealed class AuditInterceptor : ISyncCommandInterceptor
{
    private readonly ConcurrentQueue<AuditRecord> _auditLog = new();
    private readonly int _maxRecords;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditInterceptor"/> class.
    /// </summary>
    /// <param name="maxRecords">Maximum number of audit records to retain. Defaults to 1000.</param>
    public AuditInterceptor(int maxRecords = 1000)
    {
        _maxRecords = maxRecords > 0 ? maxRecords : 1000;
    }

    /// <summary>
    /// Gets the number of audit records currently retained.
    /// </summary>
    public int RecordCount => _auditLog.Count;

    /// <summary>
    /// Gets the most recent audit records.
    /// </summary>
    /// <param name="count">Number of records to retrieve.</param>
    /// <returns>The most recent audit records in chronological order.</returns>
    public IEnumerable<AuditRecord> GetRecentRecords(int count = 100)
    {
        return _auditLog.OrderByDescending(r => r.Timestamp).Take(count).Reverse();
    }

    /// <summary>
    /// Clears all retained audit records.
    /// </summary>
    public void Clear()
    {
        while (_auditLog.TryDequeue(out _)) { }
    }

    /// <inheritdoc/>
    public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
    {
        // Create audit record at start of execution
        var record = new AuditRecord
        {
            Timestamp = DateTime.UtcNow,
            CommandText = context.CommandText,
            CommandType = context.CommandType,
            Database = context.Connection.Database,
            ConnectionState = context.Connection.State,
            Phase = AuditPhase.Executing
        };

        // Trim log if exceeding max records
        while (_auditLog.Count >= _maxRecords && _auditLog.TryDequeue(out _)) { }

        _auditLog.Enqueue(record);

        return new ValueTask();
    }

    /// <inheritdoc/>
    public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var record = new AuditRecord
        {
            Timestamp = DateTime.UtcNow,
            CommandText = context.CommandText,
            CommandType = context.CommandType,
            Database = context.Connection.Database,
            ConnectionState = context.Connection.State,
            ElapsedMilliseconds = context.Elapsed.TotalMilliseconds,
            Phase = AuditPhase.Executed,
            Success = true
        };

        while (_auditLog.Count >= _maxRecords && _auditLog.TryDequeue(out _)) { }

        _auditLog.Enqueue(record);

        return new ValueTask();
    }

    /// <inheritdoc/>
    public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
    {
        var record = new AuditRecord
        {
            Timestamp = DateTime.UtcNow,
            CommandText = context.CommandText,
            CommandType = context.CommandType,
            Database = context.Connection.Database,
            ConnectionState = context.Connection.State,
            ElapsedMilliseconds = context.Elapsed.TotalMilliseconds,
            Phase = AuditPhase.Failed,
            Success = false,
            ExceptionType = exception.GetType().FullName,
            ExceptionMessage = exception.Message
        };

        while (_auditLog.Count >= _maxRecords && _auditLog.TryDequeue(out _)) { }

        _auditLog.Enqueue(record);

        return new ValueTask();
    }

    /// <inheritdoc/>
    public void OnCommandExecuting(CommandContext context)
        => OnCommandExecutingAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void OnCommandExecuted(CommandContext context)
        => OnCommandExecutedAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void OnCommandFailed(CommandContext context, Exception exception)
        => OnCommandFailedAsync(context, exception, CancellationToken.None).GetAwaiter().GetResult();
}

/// <summary>
/// Represents a single audit record for command execution.
/// </summary>
public sealed class AuditRecord
{
    /// <summary>
    /// Gets or sets the UTC timestamp of the audit event.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the phase of command execution.
    /// </summary>
    public AuditPhase Phase { get; set; }

    /// <summary>
    /// Gets or sets the SQL command text.
    /// </summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of command.
    /// </summary>
    public CommandType CommandType { get; set; }

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection state.
    /// </summary>
    public ConnectionState ConnectionState { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time in milliseconds (only for Executed/Failed phases).
    /// </summary>
    public double ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets whether the command completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the exception type name (only for Failed phase).
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the exception message (only for Failed phase).
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Returns a string representation of the audit record.
    /// </summary>
    public override string ToString()
    {
        var truncatedCommand = CommandText.Length > 50
            ? CommandText.Substring(0, 50)
            : CommandText;
        return $"{Timestamp:O} | {Phase,-10} | {CommandType,-16} | {ElapsedMilliseconds,8:F2}ms | {truncatedCommand}";
    }
}

/// <summary>
/// Phases of command execution for audit tracking.
/// </summary>
public enum AuditPhase
{
    /// <summary>
    /// Command is about to be executed.
    /// </summary>
    Executing,

    /// <summary>
    /// Command has completed execution successfully.
    /// </summary>
    Executed,

    /// <summary>
    /// Command execution failed with an exception.
    /// </summary>
    Failed
}
