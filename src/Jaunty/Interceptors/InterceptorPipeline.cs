using System.Data;
using System.Diagnostics;

using Jaunty.Diagnostics;

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
/// When a caller drives this lifecycle through <see cref="ExecuteWithInterceptionAsync{T}"/> or
/// <see cref="ExecuteWithInterception{T}"/>, an interceptor throwing during
/// <see cref="InvokeExecutingAsync"/> causes the command not to execute, and every registered
/// interceptor's <see cref="ICommandInterceptor.OnCommandFailedAsync"/> method is called -
/// including ones that already ran successfully before the failing interceptor, and the failing
/// interceptor itself. This failure notification is implemented by those wrapper methods, not by
/// <see cref="InvokeExecutingAsync"/> itself - a caller invoking <see cref="InvokeExecutingAsync"/>
/// directly does not get it.
/// </para>
/// <para>
/// This pipeline also emits diagnostic events via <see cref="JauntyDiagnosticListener"/>
/// for integration with OpenTelemetry, Application Insights, and other telemetry systems.
/// </para>
/// </remarks>
public sealed class InterceptorPipeline
{
    private readonly ICommandInterceptor[] _interceptors;
    private readonly JauntyDiagnosticListener? _diagnosticListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptorPipeline"/> class.
    /// </summary>
    /// <param name="interceptors">The interceptors to invoke in order.</param>
    public InterceptorPipeline(IEnumerable<ICommandInterceptor> interceptors)
        : this(interceptors, JauntyDiagnosticListener.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptorPipeline"/> class.
    /// </summary>
    /// <param name="interceptors">The interceptors to invoke in order.</param>
    /// <param name="diagnosticListener">Optional diagnostic listener for event emission.</param>
    internal InterceptorPipeline(IEnumerable<ICommandInterceptor> interceptors, JauntyDiagnosticListener? diagnosticListener)
    {
        _interceptors = interceptors?.ToArray() ?? Array.Empty<ICommandInterceptor>();
        _diagnosticListener = diagnosticListener;
    }

    /// <summary>
    /// Gets whether this pipeline has any registered interceptors.
    /// </summary>
    public bool HasInterceptors => _interceptors.Length > 0;

    /// <summary>
    /// Gets whether anything is observing command execution - a registered
    /// <see cref="ICommandInterceptor"/>, a <see cref="JauntyDiagnosticListener"/> subscriber, or both.
    /// </summary>
    /// <remarks>
    /// AUD-R25: every guard in this type used to test <see cref="HasInterceptors"/> alone, and the
    /// three diagnostic emits sit behind those guards. An application that subscribed to the
    /// "Jaunty" diagnostic source but registered no interceptor therefore received nothing - not one
    /// event, ever - with no diagnostic to explain the silence, and the workaround was to register a
    /// do-nothing interceptor purely to switch telemetry on. That contradicted both types' own
    /// documentation, which presents diagnostics as something the pipeline "also emits" rather than
    /// as contingent on interceptor registration.
    ///
    /// <para>
    /// <see cref="DiagnosticListener.IsEnabled()"/> is a volatile read of the subscriber list, so
    /// the no-interceptor/no-subscriber fast path stays a field read plus that check.
    /// </para>
    /// </remarks>
    public bool IsObserved => HasInterceptors || _diagnosticListener?.IsEnabled() == true;

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
    /// <remarks>
    /// Exceptions thrown by an interceptor propagate unchanged to the caller.
    /// </remarks>
    public async ValueTask InvokeExecutingAsync(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        CancellationToken cancellationToken)
    {
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandExecuting(context);

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
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandExecuted(context);

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
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed, exception);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandFailed(context, exception);

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
        if (!IsObserved)
            return await executeFunc().ConfigureAwait(false);

        // AUD-R35-170. The stopwatch used to start here, before the executing hooks ran, so the
        // TimeSpan every OnCommandExecuted and OnCommandFailed hook received included the time spent
        // inside every interceptor's own executing hook. CommandContext.Elapsed is documented as the
        // command's execution time and LoggingInterceptor compares it against SlowQueryThreshold, so
        // one slow audit or tracing interceptor inflated every command's reported duration and could
        // trip the slow-query warning for a command that was never slow. It is started immediately
        // before the command instead; if an executing hook throws, the failure hooks see Zero, which
        // is the truth - the command never ran.
        var stopwatch = new Stopwatch();
        try
        {
            // Before execution
            await InvokeExecutingAsync(commandText, parameters, connection, commandType, cancellationToken).ConfigureAwait(false);

            stopwatch.Start();
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
        if (!IsObserved)
        {
            await executeFunc().ConfigureAwait(false);
            return;
        }

        // AUD-R35-170. The stopwatch used to start here, before the executing hooks ran, so the
        // TimeSpan every OnCommandExecuted and OnCommandFailed hook received included the time spent
        // inside every interceptor's own executing hook. CommandContext.Elapsed is documented as the
        // command's execution time and LoggingInterceptor compares it against SlowQueryThreshold, so
        // one slow audit or tracing interceptor inflated every command's reported duration and could
        // trip the slow-query warning for a command that was never slow. It is started immediately
        // before the command instead; if an executing hook throws, the failure hooks see Zero, which
        // is the truth - the command never ran.
        var stopwatch = new Stopwatch();
        try
        {
            // Before execution
            await InvokeExecutingAsync(commandText, parameters, connection, commandType, cancellationToken).ConfigureAwait(false);

            stopwatch.Start();
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

    /// <summary>
    /// Invokes the executing hooks on the synchronous execution path.
    /// <see cref="ISyncCommandInterceptor"/> implementations are called synchronously;
    /// async-only interceptors are invoked by blocking on their completed hook.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    public void InvokeExecuting(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType)
    {
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandExecuting(context);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            if (_interceptors[i] is ISyncCommandInterceptor sync)
                sync.OnCommandExecuting(context);
            else
                _interceptors[i].OnCommandExecutingAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Invokes the executed hooks on the synchronous execution path after successful execution.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="elapsed">The elapsed time for command execution.</param>
    /// <remarks>
    /// Exceptions from interceptors are swallowed to avoid masking the successful command result.
    /// </remarks>
    public void InvokeExecuted(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        TimeSpan elapsed)
    {
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandExecuted(context);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            try
            {
                if (_interceptors[i] is ISyncCommandInterceptor sync)
                    sync.OnCommandExecuted(context);
                else
                    _interceptors[i].OnCommandExecutedAsync(context, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // Swallow exceptions from interceptors during executed phase
                // to avoid masking the successful command result
            }
        }
    }

    /// <summary>
    /// Invokes the failed hooks on the synchronous execution path when execution fails.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="elapsed">The elapsed time before the failure.</param>
    /// <param name="exception">The exception that occurred during execution.</param>
    /// <remarks>
    /// Exceptions from interceptors are swallowed to avoid masking the original exception.
    /// </remarks>
    public void InvokeFailed(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        TimeSpan elapsed,
        Exception exception)
    {
        if (!IsObserved)
            return;

        var context = new CommandContext(commandText, parameters, connection, commandType, elapsed, exception);

        // Emit diagnostic event
        _diagnosticListener?.WriteCommandFailed(context, exception);

        for (int i = 0; i < _interceptors.Length; i++)
        {
            try
            {
                if (_interceptors[i] is ISyncCommandInterceptor sync)
                    sync.OnCommandFailed(context, exception);
                else
                    _interceptors[i].OnCommandFailedAsync(context, exception, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // Swallow exceptions from interceptors during failure handling
                // to avoid masking the original exception
            }
        }
    }

    /// <summary>
    /// Executes a command with full interceptor lifecycle support on the synchronous path.
    /// The thread is never blocked on asynchronous machinery for interceptors that
    /// implement <see cref="ISyncCommandInterceptor"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the command execution.</typeparam>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="parameters">The command parameters.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="executeFunc">The function to execute the command.</param>
    /// <returns>The result of the command execution.</returns>
    public T ExecuteWithInterception<T>(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<T> executeFunc)
    {
        if (!IsObserved)
            return executeFunc();

        // AUD-R35-170. The stopwatch used to start here, before the executing hooks ran, so the
        // TimeSpan every OnCommandExecuted and OnCommandFailed hook received included the time spent
        // inside every interceptor's own executing hook. CommandContext.Elapsed is documented as the
        // command's execution time and LoggingInterceptor compares it against SlowQueryThreshold, so
        // one slow audit or tracing interceptor inflated every command's reported duration and could
        // trip the slow-query warning for a command that was never slow. It is started immediately
        // before the command instead; if an executing hook throws, the failure hooks see Zero, which
        // is the truth - the command never ran.
        var stopwatch = new Stopwatch();
        try
        {
            // Before execution
            InvokeExecuting(commandText, parameters, connection, commandType);

            stopwatch.Start();
            var result = executeFunc();
            stopwatch.Stop();

            // After successful execution
            InvokeExecuted(commandText, parameters, connection, commandType, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InvokeFailed(commandText, parameters, connection, commandType, stopwatch.Elapsed, ex);
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
    /// <param name="executeAction">The action that executes the command.</param>
    /// <remarks>
    /// AUD-R35-172. The async surface has both a returning and a non-returning wrapper; the sync
    /// surface had only the generic one, so a void-returning caller had to invent a throwaway return
    /// value to enter the pipeline. This is the missing half, delegating to the generic overload so
    /// there is one ordering of the hooks and one stopwatch, not two.
    /// </remarks>
    public void ExecuteWithInterception(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Action executeAction)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(executeAction);
#else
        if (executeAction is null) throw new ArgumentNullException(nameof(executeAction));
#endif

        ExecuteWithInterception<object?>(commandText, parameters, connection, commandType, () =>
        {
            executeAction();
            return null;
        });
    }
}
