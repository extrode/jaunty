using System.Data;

using Jaunty.Diagnostics;
using Jaunty.Interceptors;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty.Internals;

/// <summary>
/// The single place anything in Jaunty decides whether a command is being observed, and the single
/// place it routes execution through <see cref="Configuration.JauntyConfig.InterceptorPipeline"/>
/// and <see cref="Configuration.JauntyConfig.Logger"/>.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 4 / batch 5 / batch 7, filed four times, fixed as one). Three separate problems
/// shared one cause - every caller spelled the observation test itself, so there were thirty
/// independent copies of a predicate and no way to change what "observed" means:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Diagnostics were unreachable.</b> AUD-R25-013 added
/// <see cref="InterceptorPipeline.IsObserved"/> so that subscribing to the "Jaunty"
/// <see cref="System.Diagnostics.DiagnosticListener"/> without registering an
/// <see cref="ICommandInterceptor"/> would still emit events. It had no effect on any ordinary
/// Jaunty API: thirty call sites tested <c>HasInterceptors</c> and returned early, so the pipeline
/// was never entered and the new gate never ran. The ten tests covering it all constructed a
/// pipeline directly and called <c>ExecuteWithInterception</c>, so not one exercised a guard.
/// </description></item>
/// <item><description>
/// <b>Whole assemblies were invisible.</b> Jaunty.Fluent and the FlatFiles assemblies contained
/// zero references to <see cref="InterceptorPipeline"/> - they built and executed commands inline -
/// so whether a call was audited depended on whether its terminal happened to delegate to a core
/// extension method. <c>.Select()</c> was intercepted and <c>.SelectBoth()</c> on the identical
/// query was not.
/// </description></item>
/// <item><description>
/// <b>The guard shape spread.</b> The count grew from 29 to 30 during round 26 itself, because a
/// fix for a different finding added two more copies of it. Anything that can be copied thirty
/// times will be copied a thirty-first.
/// </description></item>
/// </list>
/// <para>
/// The predicate lives here now, so the answer to "is anyone watching" is given in one place and
/// every assembly asks the same question. <c>InterceptorObservabilityTests</c> asserts that
/// <c>HasInterceptors</c> appears nowhere outside <see cref="InterceptorPipeline"/> itself, so the
/// shape cannot come back the way it came back last time.
/// </para>
/// <para>
/// <b>The pipeline is read once.</b> <c>JauntyConfig.InterceptorPipeline</c> is a volatile field
/// that <see cref="Configuration.JauntyConfig.ClearInterceptors"/> and
/// <see cref="Configuration.JauntyConfig.Reset"/> set to <see langword="null"/>. Every one of the
/// thirty call sites read it twice - once to test, once to call - so a clear landing between the
/// two reads threw <see cref="NullReferenceException"/> out of a live query. Snapshotting it into a
/// local also gives the behaviour <see cref="Configuration.JauntyConfig"/> already documents:
/// "commands that are already executing keep the configuration they observed at their start".
/// </para>
/// </remarks>
internal static class CommandObservation
{
    /// <summary>
    /// The pipeline used when a diagnostics subscriber is the only observer, so there is something
    /// to route through even though no <see cref="ICommandInterceptor"/> was ever registered.
    /// </summary>
    private static readonly Lazy<InterceptorPipeline> DiagnosticsOnlyPipeline =
        new(() => new InterceptorPipeline(Array.Empty<ICommandInterceptor>(), JauntyDiagnosticListener.Instance));

    /// <summary>
    /// Resolves the pipeline to route this command through, or <see langword="null"/> when nothing
    /// is watching and the command should run untouched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The null case is the whole point of the second branch. <c>JauntyConfig.InterceptorPipeline</c>
    /// is only ever assigned by <c>AddInterceptor</c>/<c>AddInterceptors</c> - it starts
    /// <see langword="null"/> and <c>ClearInterceptors</c>/<c>Reset</c> put it back - so in the exact
    /// case AUD-R25-013 set out to fix, "a diagnostics subscriber and no interceptor", there was no
    /// pipeline object in existence to ask. Correcting the thirty call-site guards to test
    /// <see cref="InterceptorPipeline.IsObserved"/> instead of <c>HasInterceptors</c> therefore
    /// changed nothing on its own: the guard was false because of the null, not the predicate.
    /// Measured before and after - <c>InterceptorObservabilityTests</c> covers five public APIs, and
    /// all five stayed silent until this branch existed.
    /// </para>
    /// <para>
    /// Materialising the listener singleton here is also what makes Jaunty discoverable: a
    /// <c>DiagnosticListener.AllListeners</c> observer cannot see the "Jaunty" source until
    /// something constructs it.
    /// </para>
    /// </remarks>
    public static InterceptorPipeline? Observer => ResolveObserver();

    private static InterceptorPipeline? ResolveObserver()
    {
        InterceptorPipeline? configured = JauntyConfig.InterceptorPipeline;

        if (configured is not null)
            return configured.IsObserved ? configured : null;

        return JauntyDiagnosticListener.Instance.IsEnabled() ? DiagnosticsOnlyPipeline.Value : null;
    }

    /// <summary>
    /// Whether anything is watching command execution - a registered
    /// <see cref="ICommandInterceptor"/>, a "Jaunty" <see cref="System.Diagnostics.DiagnosticListener"/>
    /// subscriber, or both.
    /// </summary>
    public static bool IsObserved => ResolveObserver() is not null;

    /// <summary>
    /// Runs <paramref name="body"/> inside the interceptor pipeline when anything is observing, and
    /// directly otherwise.
    /// </summary>
    /// <remarks>
    /// The <see cref="IsObserved"/> test is what keeps this free when nobody is listening: without
    /// it every command would allocate the closure and enter the pipeline only to fall straight
    /// through.
    /// </remarks>
    public static T Execute<T>(
        string commandText,
        object? parameters,
        IDbConnection connection,
        CommandType commandType,
        Func<T> body)
    {
        InterceptorPipeline? pipeline = ResolveObserver();

        if (pipeline is not null)
            return pipeline.ExecuteWithInterception(commandText, parameters, connection, commandType, body);

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
        InterceptorPipeline? pipeline = ResolveObserver();

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterceptionAsync(
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
