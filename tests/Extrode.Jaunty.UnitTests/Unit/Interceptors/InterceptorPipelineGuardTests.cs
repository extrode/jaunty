using System.Diagnostics;
#if NET
using System.Runtime.CompilerServices;
#endif

using Extrode.Jaunty.Diagnostics;
using Extrode.Jaunty.Interceptors;

using Npgsql;

namespace Extrode.Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R38-054: a diagnostic subscriber throwing on Executed turned a successful command into a
/// failure, and one throwing on Failed replaced the original exception and skipped the interceptors.
/// AUD-R38-055: the sync path called GetResult on an incomplete IValueTaskSource-backed ValueTask.
/// </summary>
public sealed class InterceptorPipelineGuardTests
{
    private sealed class ThrowingObserver(string eventName) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == eventName)
                throw new NotSupportedException("subscriber");
        }
    }

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public List<string> Calls { get; } = [];

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Calls.Add("executing");
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Calls.Add("executed");
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Calls.Add("failed");
            return default;
        }
    }

    private static (InterceptorPipeline Pipeline, RecordingInterceptor Interceptor, IDisposable Subscription, JauntyDiagnosticListener Listener) Build(string throwOn)
    {
        var listener = new JauntyDiagnosticListener();
        IDisposable subscription = listener.Subscribe(new ThrowingObserver(throwOn));
        var interceptor = new RecordingInterceptor();
        return (new InterceptorPipeline([interceptor], listener), interceptor, subscription, listener);
    }

    [Fact]
    public void Sync_SubscriberThrowingOnExecuted_LeavesTheCommandSuccessful()
    {
        var (pipeline, interceptor, subscription, listener) = Build(JauntyDiagnosticListener.CommandExecutedEventName);
        using (listener)
        using (subscription)
        {
            int result = pipeline.ExecuteWithInterception("SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => 7);

            Assert.Equal(7, result);
            Assert.Equal(["executing", "executed"], interceptor.Calls);
        }
    }

    [Fact]
    public async Task Async_SubscriberThrowingOnExecuted_LeavesTheCommandSuccessful()
    {
        var (pipeline, interceptor, subscription, listener) = Build(JauntyDiagnosticListener.CommandExecutedEventName);
        using (listener)
        using (subscription)
        {
            int result = await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => new ValueTask<int>(7), CancellationToken.None);

            Assert.Equal(7, result);
            Assert.Equal(["executing", "executed"], interceptor.Calls);
        }
    }

    [Fact]
    public void Sync_SubscriberThrowingOnFailed_KeepsTheOriginalException_AndRunsTheInterceptors()
    {
        var (pipeline, interceptor, subscription, listener) = Build(JauntyDiagnosticListener.CommandFailedEventName);
        using (listener)
        using (subscription)
        {
            var original = new TimeoutException("db");

            var thrown = Assert.Throws<TimeoutException>(() =>
                pipeline.ExecuteWithInterception<int>("SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => throw original));

            Assert.Same(original, thrown);
            Assert.Equal(["executing", "failed"], interceptor.Calls);
        }
    }

    [Fact]
    public async Task Async_SubscriberThrowingOnFailed_KeepsTheOriginalException_AndRunsTheInterceptors()
    {
        var (pipeline, interceptor, subscription, listener) = Build(JauntyDiagnosticListener.CommandFailedEventName);
        using (listener)
        using (subscription)
        {
            var original = new TimeoutException("db");

            var thrown = await Assert.ThrowsAsync<TimeoutException>(async () =>
                await pipeline.ExecuteWithInterceptionAsync<int>(
                    "SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => throw original, CancellationToken.None));

            Assert.Same(original, thrown);
            Assert.Equal(["executing", "failed"], interceptor.Calls);
        }
    }

#if NET
    private sealed class PooledAsyncInterceptor : ICommandInterceptor
    {
        public List<string> Calls { get; } = [];

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        public async ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            Calls.Add("executing");
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        public async ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            Calls.Add("executed");
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        public async ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            Calls.Add("failed");
        }
    }

    [Fact]
    public void Sync_PooledAsyncInterceptor_IsWaitedFor_OnSuccess()
    {
        var interceptor = new PooledAsyncInterceptor();
        var pipeline = new InterceptorPipeline([interceptor], null);

        int result = pipeline.ExecuteWithInterception("SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => 3);

        Assert.Equal(3, result);
        Assert.Equal(["executing", "executed"], interceptor.Calls);
    }

    [Fact]
    public void Sync_PooledAsyncInterceptor_IsWaitedFor_OnFailure()
    {
        var interceptor = new PooledAsyncInterceptor();
        var pipeline = new InterceptorPipeline([interceptor], null);

        Assert.Throws<TimeoutException>(() =>
            pipeline.ExecuteWithInterception<int>("SELECT 1", null, new NpgsqlConnection(), CommandType.Text, () => throw new TimeoutException()));

        Assert.Equal(["executing", "failed"], interceptor.Calls);
    }
#endif
}
