namespace Jaunty.Interceptors;

/// <summary>
/// Defines a hook for intercepting command execution in Jaunty.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create custom interceptors for cross-cutting concerns
/// such as logging, auditing, performance monitoring, or telemetry.
/// </para>
/// <para>
/// Multiple interceptors can be registered and will execute in registration order.
/// </para>
/// <para>
/// To short-circuit command execution (prevent it from running), throw an exception
/// from <see cref="OnCommandExecutingAsync"/>.
/// </para>
/// <para>
/// <b>Streaming APIs do not invoke interceptors.</b> The <c>QueryStream</c>,
/// <c>QueryStreamAsync</c>, <c>GetAllStream</c> and <c>GetAllStreamAsync</c> families are lazy
/// iterators: the command isn't executed when the method returns, only when the caller starts
/// enumerating, and it stays open across the whole enumeration. There is no point at which the
/// pipeline could report a completed command without first materializing every row, which would
/// defeat the reason to stream at all. Their non-streaming counterparts (<c>Query</c>,
/// <c>QueryFirst</c>, <c>QuerySingle</c>, <c>GetAll</c>) do invoke interceptors, as do all write
/// operations. If you need auditing or telemetry to cover a query, use the non-streaming form.
/// </para>
/// <para>
/// <b>Interceptors are not the only way to observe.</b> Subscribing to the <c>"Jaunty"</c>
/// <see cref="System.Diagnostics.DiagnosticListener"/> is enough on its own - registering an
/// <see cref="ICommandInterceptor"/> purely to switch telemetry on is not required, and used to be.
/// </para>
/// <para>
/// <b>Coverage across assemblies.</b> The fluent API (<c>Jaunty.Fluent</c>) and the DuckDB flat-file
/// API (<c>Jaunty.FlatFiles.DuckDB</c>) route their reads and writes through the same pipeline as
/// core. Two DuckDB areas do not, deliberately: the session setup statements that install and load
/// DuckDB extensions and probe a source's schema, which are Jaunty's own plumbing rather than the
/// caller's data access; and <c>ImportCsv</c>'s row-copy loop, which is tracked as the remaining
/// part of AUD-R26-041.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class AuditInterceptor : ICommandInterceptor
/// {
///     public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken ct)
///     {
///         // Log command before execution
///         Console.WriteLine($"Executing: {context.CommandText}");
///         return ValueTask.CompletedTask;
///     }
///
///     public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken ct)
///     {
///         // Log execution time after completion
///         Console.WriteLine($"Completed in {context.Elapsed.TotalMilliseconds}ms");
///         return ValueTask.CompletedTask;
///     }
///
///     public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken ct)
///     {
///         // Log failure
///         Console.WriteLine($"Failed: {exception.Message}");
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface ICommandInterceptor
{
    /// <summary>
    /// Called before a command is executed.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Throw an exception from this method to prevent command execution.
    /// The exception will propagate to the caller without the command being sent to the database.
    /// </remarks>
    ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Called after a command is successfully executed.
    /// </summary>
    /// <param name="context">The command execution context with populated <see cref="CommandContext.Elapsed"/> time.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Called when a command execution fails with an exception.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="exception">The exception that occurred during execution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken);
}
