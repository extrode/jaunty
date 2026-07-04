namespace Jaunty.Interceptors;

/// <summary>
/// Extends <see cref="ICommandInterceptor"/> with synchronous hooks so the synchronous
/// query/execute APIs never block a thread on asynchronous interceptor machinery.
/// </summary>
/// <remarks>
/// <para>
/// When Jaunty's <em>synchronous</em> APIs (for example <c>Query&lt;T&gt;</c> or
/// <c>Execute</c>) run with interceptors registered, the pipeline calls the methods on
/// this interface for interceptors that implement it. Interceptors that only implement
/// <see cref="ICommandInterceptor"/> are still invoked on the synchronous path by
/// blocking on their asynchronous hooks - implement this interface to avoid that.
/// </para>
/// <para>
/// The asynchronous APIs always use the <see cref="ICommandInterceptor"/> hooks;
/// the members of this interface are never called from asynchronous execution.
/// </para>
/// <para>
/// Jaunty's built-in interceptors (<c>LoggingInterceptor</c>, <c>AuditInterceptor</c>)
/// implement this interface.
/// </para>
/// </remarks>
public interface ISyncCommandInterceptor : ICommandInterceptor
{
    /// <summary>
    /// Called before a command is executed on the synchronous execution path.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <remarks>
    /// Throw an exception from this method to prevent command execution.
    /// The exception will propagate to the caller without the command being sent to the database.
    /// </remarks>
    void OnCommandExecuting(CommandContext context);

    /// <summary>
    /// Called after a command is successfully executed on the synchronous execution path.
    /// </summary>
    /// <param name="context">The command execution context with populated <see cref="CommandContext.Elapsed"/> time.</param>
    void OnCommandExecuted(CommandContext context);

    /// <summary>
    /// Called when a command execution fails on the synchronous execution path.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="exception">The exception that occurred during execution.</param>
    void OnCommandFailed(CommandContext context, Exception exception);
}
