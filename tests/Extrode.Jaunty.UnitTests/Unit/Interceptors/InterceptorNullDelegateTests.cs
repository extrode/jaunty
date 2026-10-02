using Extrode.Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R38-123: a null delegate is an argument error on every wrapper, raised before any hook runs.
/// </summary>
public class InterceptorNullDelegateTests
{
    private sealed class CountingInterceptor : ICommandInterceptor, ISyncCommandInterceptor
    {
        public int Calls { get; private set; }

        public void OnCommandExecuting(CommandContext context) => Calls++;

        public void OnCommandExecuted(CommandContext context) => Calls++;

        public void OnCommandFailed(CommandContext context, Exception exception) => Calls++;

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }
    }

    private static readonly IDbConnection Connection = new SqliteConnection("Data Source=:memory:");

    [Fact]
    public void SyncResult_NullDelegate_ThrowsBeforeAnyHook()
    {
        var interceptor = new CountingInterceptor();
        var pipeline = new InterceptorPipeline([interceptor]);

        Assert.Throws<ArgumentNullException>("executeFunc",
            () => pipeline.ExecuteWithInterception<int>("SELECT 1", null, Connection, CommandType.Text, null!));
        Assert.Equal(0, interceptor.Calls);
    }

    [Fact]
    public async Task AsyncResult_NullDelegate_ThrowsBeforeAnyHook()
    {
        var interceptor = new CountingInterceptor();
        var pipeline = new InterceptorPipeline([interceptor]);

        await Assert.ThrowsAsync<ArgumentNullException>("executeFunc",
            async () => await pipeline.ExecuteWithInterceptionAsync<int>("SELECT 1", null, Connection, CommandType.Text, null!, CancellationToken.None));
        Assert.Equal(0, interceptor.Calls);
    }

    [Fact]
    public async Task AsyncNoResult_NullDelegate_ThrowsBeforeAnyHook()
    {
        var interceptor = new CountingInterceptor();
        var pipeline = new InterceptorPipeline([interceptor]);

        await Assert.ThrowsAsync<ArgumentNullException>("executeFunc",
            async () => await pipeline.ExecuteWithInterceptionAsync("SELECT 1", null, Connection, CommandType.Text, (Func<ValueTask>)null!, CancellationToken.None));
        Assert.Equal(0, interceptor.Calls);
    }

    [Fact]
    public async Task AnUnobservedPipeline_AlsoRejectsANullDelegate()
    {
        var pipeline = new InterceptorPipeline([]);

        Assert.Throws<ArgumentNullException>("executeFunc",
            () => pipeline.ExecuteWithInterception<int>("SELECT 1", null, Connection, CommandType.Text, null!));
        await Assert.ThrowsAsync<ArgumentNullException>("executeFunc",
            async () => await pipeline.ExecuteWithInterceptionAsync<int>("SELECT 1", null, Connection, CommandType.Text, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>("executeFunc",
            async () => await pipeline.ExecuteWithInterceptionAsync("SELECT 1", null, Connection, CommandType.Text, (Func<ValueTask>)null!, CancellationToken.None));
    }
}
