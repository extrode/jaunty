using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R35-170 and AUD-R35-172. The stopwatch used to start before the executing hooks ran, so the
/// <see cref="CommandContext.Elapsed"/> every executed and failed hook received included the time
/// spent inside every interceptor's own executing hook - a value documented as the command's
/// execution time, and the one <c>LoggingInterceptor</c> compares against its slow-query threshold.
/// </summary>
public class InterceptorElapsedScopeTests
{
    private static readonly TimeSpan HookDelay = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan Ceiling = TimeSpan.FromMilliseconds(150);

    private sealed class SlowExecutingInterceptor : ICommandInterceptor, ISyncCommandInterceptor
    {
        public TimeSpan? Executed { get; private set; }

        public TimeSpan? Failed { get; private set; }

        public void OnCommandExecuting(CommandContext context) => Thread.Sleep(HookDelay);

        public void OnCommandExecuted(CommandContext context) => Executed = context.Elapsed;

        public void OnCommandFailed(CommandContext context, Exception exception) => Failed = context.Elapsed;

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Thread.Sleep(HookDelay);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed = context.Elapsed;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Failed = context.Elapsed;
            return default;
        }
    }

    private static (InterceptorPipeline Pipeline, SlowExecutingInterceptor Interceptor) Build()
    {
        var interceptor = new SlowExecutingInterceptor();
        return (new InterceptorPipeline([interceptor]), interceptor);
    }

    private static readonly IDbConnection Connection = new SqliteConnection("Data Source=:memory:");

    [Fact]
    public void TheSyncElapsedExcludesTheExecutingHook()
    {
        (InterceptorPipeline pipeline, SlowExecutingInterceptor interceptor) = Build();

        pipeline.ExecuteWithInterception("SELECT 1", null, Connection, CommandType.Text, () => 1);

        Assert.NotNull(interceptor.Executed);
        Assert.True(interceptor.Executed < Ceiling, $"Elapsed was {interceptor.Executed}");
    }

    [Fact]
    public async Task TheAsyncElapsedExcludesTheExecutingHook()
    {
        (InterceptorPipeline pipeline, SlowExecutingInterceptor interceptor) = Build();

        await pipeline.ExecuteWithInterceptionAsync("SELECT 1", null, Connection, CommandType.Text,
            () => new ValueTask<int>(1), CancellationToken.None);

        Assert.NotNull(interceptor.Executed);
        Assert.True(interceptor.Executed < Ceiling, $"Elapsed was {interceptor.Executed}");
    }

    [Fact]
    public async Task TheNonGenericAsyncElapsedExcludesTheExecutingHook()
    {
        (InterceptorPipeline pipeline, SlowExecutingInterceptor interceptor) = Build();

        await pipeline.ExecuteWithInterceptionAsync("SELECT 1", null, Connection, CommandType.Text,
            () => default, CancellationToken.None);

        Assert.NotNull(interceptor.Executed);
        Assert.True(interceptor.Executed < Ceiling, $"Elapsed was {interceptor.Executed}");
    }

    [Fact]
    public void TheFailedElapsedExcludesTheExecutingHookToo()
    {
        (InterceptorPipeline pipeline, SlowExecutingInterceptor interceptor) = Build();

        Assert.Throws<InvalidOperationException>(() =>
            pipeline.ExecuteWithInterception<int>("SELECT 1", null, Connection, CommandType.Text,
                () => throw new InvalidOperationException("boom")));

        Assert.NotNull(interceptor.Failed);
        Assert.True(interceptor.Failed < Ceiling, $"Elapsed was {interceptor.Failed}");
    }

    /// <summary>
    /// AUD-R35-172: the sync surface had no non-returning wrapper, so a void caller had to invent a
    /// throwaway return value.
    /// </summary>
    [Fact]
    public void TheSyncActionOverload_RunsTheFullLifecycle()
    {
        (InterceptorPipeline pipeline, SlowExecutingInterceptor interceptor) = Build();
        bool ran = false;

        pipeline.ExecuteWithInterception("SELECT 1", null, Connection, CommandType.Text, () => ran = true);

        Assert.True(ran);
        Assert.NotNull(interceptor.Executed);
        Assert.True(interceptor.Executed < Ceiling, $"Elapsed was {interceptor.Executed}");
    }

    [Fact]
    public void TheSyncActionOverload_RejectsANullAction()
    {
        (InterceptorPipeline pipeline, _) = Build();

        Assert.Throws<ArgumentNullException>("executeAction",
            () => pipeline.ExecuteWithInterception("SELECT 1", null, Connection, CommandType.Text, (Action)null!));
    }
}
