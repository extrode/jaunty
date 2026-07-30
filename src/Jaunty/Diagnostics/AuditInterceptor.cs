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
    private readonly object _trimLock = new();

    private long _sequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditInterceptor"/> class.
    /// </summary>
    /// <param name="maxRecords">
    /// Maximum number of audit <b>records</b> to retain. Defaults to 1000.
    /// </param>
    /// <remarks>
    /// The bound is on records, not commands, and this interceptor writes <b>two records per
    /// command</b> - an <see cref="AuditPhase.Executing"/> and then an
    /// <see cref="AuditPhase.Executed"/> or <see cref="AuditPhase.Failed"/>. The default of 1000
    /// therefore covers roughly 500 commands. Recorded under AUD-R26-055: for a knob whose whole
    /// purpose is bounding an audit trail, the factor of two is worth stating rather than leaving
    /// the caller to infer it.
    /// </remarks>
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
    /// <param name="count">Number of records to retrieve. Zero or less returns nothing.</param>
    /// <returns>
    /// The most recently written <paramref name="count"/> records, oldest first.
    /// </returns>
    /// <remarks>
    /// <para>
    /// AUD-R26-055 (batch 4, low/bug). This was
    /// <c>OrderByDescending(r =&gt; r.Timestamp).Take(count).Reverse()</c>, which did not deliver the
    /// chronological order it documented. <c>OrderByDescending</c> is stable, so records sharing a
    /// timestamp kept insertion order <em>within the descending sequence</em> and the trailing
    /// <c>Reverse()</c> flipped them - ties came back in reverse insertion order.
    /// </para>
    /// <para>
    /// The <c>Take</c> was the worse half. On a run of equal timestamps the stable descending sort
    /// leaves the queue in its original oldest-first order, so <c>Take(count)</c> selected the
    /// <b>oldest</b> <c>count</c> records - from a method named <c>GetRecentRecords</c>. Ties are
    /// the normal case, not an edge case: <see cref="DateTime.UtcNow"/> has about 15.6 ms of
    /// resolution on Windows and every command writes two records, so a single command can produce
    /// a mis-ordered pair.
    /// </para>
    /// <para>
    /// No sort is needed at all. <see cref="RecordAudit"/> is the only writer and enqueues under a
    /// lock, so the queue is already in insertion order - the last <paramref name="count"/> entries
    /// of a snapshot <em>are</em> the most recent, already oldest-first.
    /// <c>ConcurrentQueue.ToArray</c> gives that snapshot atomically, which also removes the
    /// torn read the old code was open to, where <c>Count</c> and the enumeration could disagree
    /// under a concurrent write.
    /// </para>
    /// </remarks>
    public IEnumerable<AuditRecord> GetRecentRecords(int count = 100)
    {
        if (count <= 0) return Array.Empty<AuditRecord>();

        AuditRecord[] snapshot = _auditLog.ToArray();
        if (snapshot.Length <= count) return snapshot;

        var recent = new AuditRecord[count];
        Array.Copy(snapshot, snapshot.Length - count, recent, 0, count);
        return recent;
    }

    /// <summary>
    /// Clears all retained audit records.
    /// </summary>
    public void Clear()
    {
        while (_auditLog.TryDequeue(out _)) { }
    }

    // AUD-R12: trim-then-enqueue on a ConcurrentQueue is not atomic on its own - concurrent
    // callers can each observe Count < _maxRecords, then all enqueue, letting the queue
    // temporarily exceed _maxRecords. Serializing trim+enqueue behind a lock keeps the bound
    // exact; contention is negligible since this only guards an O(1) dequeue/enqueue pair.
    private void RecordAudit(AuditRecord record)
    {
        lock (_trimLock)
        {
            // AUD-R26-055: numbered here, inside the same lock that orders the enqueue, so the
            // sequence a record carries agrees with its position in the queue. Assigning it at
            // construction instead would let two threads number in one order and enqueue in the
            // other.
            record.Sequence = ++_sequence;

            while (_auditLog.Count >= _maxRecords && _auditLog.TryDequeue(out _)) { }
            _auditLog.Enqueue(record);
        }
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
        RecordAudit(record);

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

        RecordAudit(record);

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

        RecordAudit(record);

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
    /// Gets or sets a monotonic, gap-free-until-trimmed number identifying this record's position
    /// in the interceptor that produced it. Starts at 1.
    /// </summary>
    /// <remarks>
    /// AUD-R26-055. <see cref="Timestamp"/> cannot totally order an audit trail - it is
    /// <see cref="DateTime.UtcNow"/>, whose resolution is about 15.6 ms on Windows, and a single
    /// command writes two records - so a consumer that persists these and sorts them later cannot
    /// recover the order they happened in. This can. A gap at the start of a retrieved run means
    /// older records were trimmed, which is worth being able to see in an audit log; numbers are
    /// never reused or renumbered.
    /// </remarks>
    public long Sequence { get; set; }

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
