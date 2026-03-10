using System.Data;
using System.Diagnostics;

namespace Jaunty.Interceptors;

/// <summary>
/// Orchestrates the execution of multiple <see cref="ICommandInterceptor"/> instances
/// during command execution.
/// </summary>
/// <remarks>
/// <para>
/// This class manages the lifecycle of interceptor calls:
/// </para>
/// <list type="number">
/// <item><description><see cref="InvokeExecutingAsync"/> - Called before command execution (all interceptors in order)</description></item>
/// <item><description><see cref="InvokeExecutedAsync"/> - Called after successful command execution (all interceptors in order)</description></item>
/// <item><description><see cref="InvokeFailedAsync"/> - Called when command execution fails (all interceptors in order)</description></item>
/// </list>
/// <para>
/// If any interceptor throws during <see cref="InvokeExecutingAsync"/>, the command is not executed
/// and remaining interceptors' <see cref="ICommandInterceptor.OnCommandFailedAsync"/> methods are called.
/// </para>
/// </remarks>
public sealed class InterceptorPipeline
{
    private readonly ICommandInterceptor[] _interceptors;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptorPipeline"/> class.
    /// </summary>
    /// <param name="interceptors">The interceptors to invoke in order.</param>
    public InterceptorPipeline(IEnumerable<ICommandInterceptor> interceptors)
    {
        _interceptors = interceptors?.ToArray() ?? Array.Empty<ICommandInterceptor>();
    }

    /// <summary>
    /// Gets whether this pipeline has any registered interceptors.
    /// </summary>
    public bool HasInterceptors => _interceptors.Length > 0;

    /// <summary>
    /// Gets the registered interceptors.
    /// </summary>
    /// <returns>An array of registered interceptors.</returns>
    internal IEnumerable<ICommandInterceptor> GetInterceptors() => _interceptors;

    /// <summary>
    /// Invokes all <see cref="ICommandInterceptor.OnCommandExecutingAsync"/> methods.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an interceptor throws an exception.</exception>
    public async ValueTask InvokeExecutingAsync(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        CancellationToken cancellationToken)
    {
        if (!HasInterceptors)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            await _interceptors[i].OnCommandExecutingAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invokes all <see cref="ICommandInterceptor.OnCommandExecutedAsync"/> methods after successful execution.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="elapsed">The elapsed time for command execution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Exceptions from interceptors are swallowed to avoid masking the successful command result.
    /// </remarks>
    public async ValueTask InvokeExecutedAsync(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        if (!HasInterceptors)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            try
            {
                await _interceptors[i].OnCommandExecutedAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow exceptions from interceptors during executed phase
                // to avoid masking the successful command result
            }
        }
    }

    /// <summary>
    /// Invokes all <see cref="ICommandInterceptor.OnCommandFailedAsync"/> methods when execution fails.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="elapsed">The elapsed time when the failure occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask InvokeFailedAsync(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        TimeSpan elapsed,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!HasInterceptors)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed, exception);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            try
            {
                await _interceptors[i].OnCommandFailedAsync(context, exception, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow exceptions from interceptors during failure handling
                // to avoid masking the original exception
            }
        }
    }

    /// <summary>
    /// Executes a command with full interceptor lifecycle support.
    /// </summary>
    /// <typeparam name="T">The return type of the command execution.</typeparam>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="executeFunc">The function to execute the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the command execution.</returns>
    public async ValueTask<T> ExecuteWithInterceptionAsync<T>(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<ValueTask<T>> executeFunc,
        CancellationToken cancellationToken)
    {
        if (!HasInterceptors)
            return await executeFunc().ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Before execution
            await InvokeExecutingAsync(commandText, parameters, connection, commandType, cancellationToken).ConfigureAwait(false);

            var result = await executeFunc().ConfigureAwait(false);
            stopwatch.Stop();

            // After successful execution
            await InvokeExecutedAsync(commandText, parameters, connection, commandType, stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await InvokeFailedAsync(commandText, parameters, connection, commandType, stopwatch.Elapsed, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes a non-returning command with full interceptor lifecycle support.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="executeFunc">The function to execute the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask ExecuteWithInterceptionAsync(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<ValueTask> executeFunc,
        CancellationToken cancellationToken)
    {
        if (!HasInterceptors)
        {
            await executeFunc().ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Before execution
            await InvokeExecutingAsync(commandText, parameters, connection, commandType, cancellationToken).ConfigureAwait(false);

            await executeFunc().ConfigureAwait(false);
            stopwatch.Stop();

            // After successful execution
            await InvokeExecutedAsync(commandText, parameters, connection, commandType, stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await InvokeFailedAsync(commandText, parameters, connection, commandType, stopwatch.Elapsed, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
