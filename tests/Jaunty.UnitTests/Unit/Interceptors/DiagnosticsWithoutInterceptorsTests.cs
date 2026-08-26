using System.Data;

using Jaunty.Diagnostics;
using Jaunty.Interceptors;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R25 (B4-2): every guard in <see cref="InterceptorPipeline"/> tested
/// <c>HasInterceptors</c>, and the three diagnostic emits sit behind those guards. An application
/// that subscribed to the "Jaunty" diagnostic source and registered no
/// <see cref="ICommandInterceptor"/> therefore received nothing - not one event, ever - with no
/// diagnostic to explain the silence. The workaround a user had to discover was registering a
/// do-nothing interceptor purely to switch telemetry on.
///
/// <para>
/// Both types' documentation said otherwise: <see cref="JauntyDiagnosticListener"/>'s remarks
/// present it as the integration point for OpenTelemetry and Application Insights, and
/// <see cref="InterceptorPipeline"/>'s remarks describe diagnostics as something the pipeline "also
/// emits", not as contingent on interceptor registration.
/// </para>
/// </summary>
public class DiagnosticsWithoutInterceptorsTests : IDisposable
{
    private readonly JauntyDiagnosticListener _listener = new();
    private readonly RecordingObserver _observer = new();
    private readonly IDisposable _subscription;
    private readonly StubConnection _connection = new();

    public DiagnosticsWithoutInterceptorsTests() => _subscription = _listener.Subscribe(_observer);

    public void Dispose()
    {
        _subscription.Dispose();
        _listener.Dispose();
    }

    private InterceptorPipeline PipelineWithNoInterceptors() => new([], _listener);

    // ------------------------------------------------------------------
    // The defect
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteWithInterceptionAsync_NoInterceptors_StillEmitsDiagnostics()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1", null, _connection, CommandType.Text,
            () => new ValueTask<int>(1), CancellationToken.None);

        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutedEventName);
    }

    [Fact]
    public void ExecuteWithInterception_NoInterceptors_StillEmitsDiagnostics()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        pipeline.ExecuteWithInterception("SELECT 1", null, _connection, CommandType.Text, () => 1);

        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutedEventName);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_NoInterceptorsAndTheCommandThrows_StillEmitsTheFailedEvent()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync<int>(
                "SELECT 1", null, _connection, CommandType.Text,
                () => throw new InvalidOperationException("boom"), CancellationToken.None));

        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandFailedEventName);
    }

    [Fact]
    public async Task InvokeExecutingAsync_NoInterceptors_StillEmitsDiagnostics()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        await pipeline.InvokeExecutingAsync("SELECT 1", null, _connection, CommandType.Text, CancellationToken.None);

        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
    }

    [Fact]
    public void InvokeExecuting_NoInterceptors_StillEmitsDiagnostics()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        pipeline.InvokeExecuting("SELECT 1", null, _connection, CommandType.Text);

        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
    }

    // ------------------------------------------------------------------
    // The fast path must survive
    // ------------------------------------------------------------------

    [Fact]
    public void IsObserved_IsFalse_WhenNothingIsRegisteredOrSubscribed()
    {
        // An unsubscribed listener must leave the pipeline on its no-op path, or every command in
        // every application that uses neither feature pays for context construction.
        using var unsubscribed = new JauntyDiagnosticListener();
        var pipeline = new InterceptorPipeline([], unsubscribed);

        Assert.False(pipeline.IsObserved);
        Assert.False(pipeline.HasInterceptors);
    }

    [Fact]
    public void IsObserved_IsTrue_WithASubscriberAndNoInterceptors()
    {
        InterceptorPipeline pipeline = PipelineWithNoInterceptors();

        Assert.True(pipeline.IsObserved);
        Assert.False(pipeline.HasInterceptors);
    }

    [Fact]
    public void IsObserved_IsTrue_WithAnInterceptorAndNoListener()
    {
        var pipeline = new InterceptorPipeline([new NoOpInterceptor()], diagnosticListener: null);

        Assert.True(pipeline.IsObserved);
    }

    [Fact]
    public void ExecuteWithInterception_NothingObserving_StillRunsTheCommand()
    {
        using var unsubscribed = new JauntyDiagnosticListener();
        var pipeline = new InterceptorPipeline([], unsubscribed);

        Assert.Equal(42, pipeline.ExecuteWithInterception("SELECT 1", null, _connection, CommandType.Text, () => 42));
        Assert.Empty(_observer.Events);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_WithBothInterceptorAndSubscriber_RunsBoth()
    {
        var interceptor = new NoOpInterceptor();
        var pipeline = new InterceptorPipeline([interceptor], _listener);

        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1", null, _connection, CommandType.Text,
            () => new ValueTask<int>(1), CancellationToken.None);

        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
        Assert.Contains(_observer.Events, e => e.Key == JauntyDiagnosticListener.CommandExecutingEventName);
    }

    // ------------------------------------------------------------------

    private sealed class RecordingObserver : IObserver<KeyValuePair<string, object?>>
    {
        public List<KeyValuePair<string, object?>> Events { get; } = [];

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(KeyValuePair<string, object?> value) => Events.Add(value);
    }

    private sealed class NoOpInterceptor : ICommandInterceptor
    {
        public int ExecutingCount { get; private set; }
        public int ExecutedCount { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            ExecutingCount++;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            ExecutedCount++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken = default)
            => default;
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";
        public int ConnectionTimeout => 0;
        public string Database => "TestDb";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
