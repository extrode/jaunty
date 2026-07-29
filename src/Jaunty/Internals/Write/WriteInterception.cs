using System.Data;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty.Internals.Write;

/// <summary>
/// The single place the write paths route command execution through
/// <see cref="Configuration.JauntyConfig.InterceptorPipeline"/> and
/// <see cref="Configuration.JauntyConfig.Logger"/>.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26. Eleven public write APIs - <c>Upsert</c>, the six <c>Bulk*</c> families and
/// <c>ExecuteBatch</c> - executed their commands without ever consulting either hook, so they were
/// invisible to every registered <see cref="Interceptors.ICommandInterceptor"/>. Measured by grep
/// across the whole write surface: zero occurrences of <c>InterceptorPipeline</c> and zero of
/// <c>JauntyConfig.Logger</c> in any of the nine files.
/// </para>
/// <para>
/// That contradicted the contract in writing. <see cref="Interceptors.ICommandInterceptor"/>'s
/// <c>&lt;remarks&gt;</c> enumerates the one known exemption - the streaming read APIs, which cannot
/// report a completed command without materialising every row - and ends "<i>as do all write
/// operations</i>". Someone registering <see cref="Diagnostics.AuditInterceptor"/> for a compliance
/// requirement got every single-row <c>Insert</c> and none of the <c>BulkInsert</c> calls that wrote
/// the overwhelming majority of the rows. The streaming rationale does not apply here: every one of
/// these paths is fully buffered - it executes, completes and returns a row count.
/// </para>
/// <para>
/// Existing single-row paths (<c>InsertCore</c>, <c>UpdateCore</c>, <c>DeleteCore</c>,
/// <c>ExecuteNonQueryCore</c>) spell the same two steps inline and predate this helper. They are
/// behaviourally identical to it, and were deliberately left alone: they are correct and covered,
/// and rewriting a working transactional write path to prove a point is not worth the regression
/// risk. <c>WriteObservabilityTests</c> asserts the whole write surface - inline form and helper
/// alike - reaches both hooks, so the two forms cannot drift apart unnoticed while both exist.
/// </para>
/// </remarks>
internal static class WriteInterception
{
    /// <summary>
    /// Runs <paramref name="body"/> inside the interceptor pipeline when interceptors are
    /// registered, and directly otherwise.
    /// </summary>
    /// <remarks>
    /// The <c>HasInterceptors</c> test is what keeps this free when nobody is listening: without it
    /// every write would allocate the closure and enter the pipeline only to fall straight through.
    /// </remarks>
    public static T Execute<T>(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<T> body)
    {
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return JauntyConfig.InterceptorPipeline.ExecuteWithInterception(
                commandText, parameters, connection, commandType, body);
        }

        return body();
    }

    /// <summary>Asynchronous counterpart to <see cref="Execute{T}"/>.</summary>
    public static ValueTask<T> ExecuteAsync<T>(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<ValueTask<T>> body,
        CancellationToken cancellationToken)
    {
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                commandText, parameters, connection, commandType, body, cancellationToken);
        }

        return body();
    }

    /// <summary>
    /// Invokes <see cref="Configuration.JauntyConfig.Logger"/> for a command about to execute.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Execute{T}"/> because the two hooks fire at different points: the
    /// pipeline wraps the whole operation, while the logger fires per command actually sent, which
    /// on the single-row paths is the same thing and on the bulk paths is not.
    /// </remarks>
    public static void Log(string commandText, object? parameters)
        => JauntyConfig.Logger?.Invoke(commandText, parameters);
}

/// <summary>
/// What a bulk write reports to interceptors and to the logger in place of a parameter set.
/// </summary>
/// <remarks>
/// <para>
/// A bulk operation has no single parameter set - it has N of them, and on the native bulk-copy path
/// it has no <see cref="IDbCommand"/> parameters at all. Handing the entity list over directly is
/// worse than useless: <c>LoggingInterceptor.FormatParameters</c> reflects over the object's own
/// public properties, so a <c>List&lt;T&gt;</c> would log <c>Capacity=…, Count=…</c> and an array
/// would log <c>Length, LongLength, Rank, SyncRoot…</c> - the same meaningless reflection output
/// that batch 1 recorded against the mis-documented "positional parameters" path.
/// </para>
/// <para>
/// So the operation describes itself instead, in properties that format cleanly through the existing
/// interceptors and answer what an auditor actually asks: which operation, over what type, for how
/// many rows. Row-level capture is deliberately not offered - materialising 100,000 entities into a
/// log line is not auditing, and an interceptor that genuinely needs the rows has the connection and
/// the command text to work from.
/// </para>
/// </remarks>
internal sealed class BulkOperationParameters
{
    public BulkOperationParameters(string operation, Type entityType, int rowCount)
        : this(operation, entityType.Name, rowCount)
    {
    }

    public BulkOperationParameters(string operation, string? entityType, int? rowCount)
    {
        Operation = operation;
        EntityType = entityType;
        RowCount = rowCount;
    }

    /// <summary>The public API that was called, e.g. <c>"BulkInsert"</c>.</summary>
    public string Operation { get; }

    /// <summary>The entity type name, e.g. <c>"Order"</c>, or <see langword="null"/> where the
    /// operation is not entity-typed - <c>ExecuteBatch</c> takes arbitrary caller objects.</summary>
    public string? EntityType { get; }

    /// <summary>
    /// How many rows or parameter sets the call was given, or <see langword="null"/> when that is
    /// not knowable without consuming the caller's sequence. <c>ExecuteBatch</c> accepts a lazy
    /// <see cref="IEnumerable{T}"/>, and draining it early to produce a count for a log line would
    /// change the method's memory behaviour - so it reports what it knows and no more.
    /// </summary>
    public int? RowCount { get; }

    public override string ToString()
    {
        string entity = EntityType is null ? string.Empty : $"<{EntityType}>";
        string count = RowCount is null ? string.Empty : $" x{RowCount.Value}";
        return $"{Operation}{entity}{count}";
    }
}
