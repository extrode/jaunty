using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Configuration;

/// <summary>
/// Unit tests for the thread safety of <see cref="JauntyConfig"/> interceptor registration.
/// </summary>
public class JauntyConfigInterceptorTests : IDisposable
{
    public void Dispose()
    {
        // No other test in the suite currently registers interceptors via JauntyConfig,
        // so it is safe to fully clear the pipeline here without affecting other tests
        // that may be running concurrently in other collections.
        JauntyConfig.ClearInterceptors();
    }

    [Fact]
    public async Task AddInterceptor_ConcurrentCalls_DoesNotLoseRegistrations()
    {
        // Regression: AddInterceptor previously did a non-atomic read-modify-write of the
        // static interceptor pipeline. Concurrent callers could read the same starting
        // pipeline, each add their own interceptor, and whichever write won would silently
        // discard the other caller's interceptor.
        JauntyConfig.ClearInterceptors();

        const int count = 64;
        var interceptors = new NoOpInterceptor[count];
        for (int i = 0; i < count; i++)
            interceptors[i] = new NoOpInterceptor(i);

        var tasks = new Task[count];
        for (int i = 0; i < count; i++)
        {
            var interceptor = interceptors[i];
            tasks[i] = Task.Run(() => JauntyConfig.AddInterceptor(interceptor));
        }
        await Task.WhenAll(tasks);

        var registeredIds = JauntyConfig.InterceptorPipeline!.GetInterceptors()
            .Cast<NoOpInterceptor>()
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(count, registeredIds.Length);
        Assert.Equal(Enumerable.Range(0, count), registeredIds);
    }

    [Fact]
    public async Task AddInterceptors_ConcurrentBulkCalls_DoesNotLoseRegistrations()
    {
        JauntyConfig.ClearInterceptors();

        const int bulkGroups = 16;
        const int perGroup = 4;
        var tasks = new Task[bulkGroups];
        var expectedIds = new List<int>();

        for (int g = 0; g < bulkGroups; g++)
        {
            var group = new NoOpInterceptor[perGroup];
            for (int i = 0; i < perGroup; i++)
            {
                int id = g * perGroup + i;
                group[i] = new NoOpInterceptor(id);
                expectedIds.Add(id);
            }

            tasks[g] = Task.Run(() => JauntyConfig.AddInterceptors(group));
        }
        await Task.WhenAll(tasks);

        var registeredIds = JauntyConfig.InterceptorPipeline!.GetInterceptors()
            .Cast<NoOpInterceptor>()
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(expectedIds.Count, registeredIds.Length);
        Assert.Equal(expectedIds.OrderBy(x => x), registeredIds);
    }

    private sealed class NoOpInterceptor(int id) : ICommandInterceptor
    {
        public int Id { get; } = id;

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }
}
