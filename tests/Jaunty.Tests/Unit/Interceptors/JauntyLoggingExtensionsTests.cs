using Jaunty.Configuration;
using Jaunty.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// Tests for JauntyLoggingExtensions:
///   - AddJauntyLogging(IServiceCollection, Action&lt;LoggingConfiguration&gt;?)
///   - AddJauntyInterceptor&lt;TInterceptor&gt;(IServiceCollection)
///   - ApplyJauntyInterceptors(IServiceProvider)
/// </summary>
[Collection("Logging Extensions")]
public class JauntyLoggingExtensionsTests : IDisposable
{
    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
    }

    // ------------------------------------------------------------------
    // AddJauntyLogging
    // ------------------------------------------------------------------

    [Fact]
    public void AddJauntyLogging_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddJauntyLogging());
    }

    [Fact]
    public void AddJauntyLogging_RegistersLoggingInterceptorAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddJauntyLogging();

        var provider = services.BuildServiceProvider();
        var interceptor = provider.GetService<LoggingInterceptor>();

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddJauntyLogging_TwoResolves_ReturnSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddJauntyLogging();

        var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredService<LoggingInterceptor>();
        var b = provider.GetRequiredService<LoggingInterceptor>();

        Assert.Same(a, b);
    }

    [Fact]
    public void AddJauntyLogging_WithConfigureAction_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        var ex = Record.Exception(() =>
            services.AddJauntyLogging(cfg =>
            {
                cfg.LogSql = false;
                cfg.LogParameters = false;
                cfg.MinimumLogLevel = LogLevel.Debug;
            }));

        Assert.Null(ex);
    }

    [Fact]
    public void AddJauntyLogging_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        var returned = services.AddJauntyLogging();

        Assert.Same(services, returned);
    }

    // ------------------------------------------------------------------
    // AddJauntyInterceptor<T>
    // ------------------------------------------------------------------

    [Fact]
    public void AddJauntyInterceptor_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddJauntyInterceptor<StubInterceptor>());
    }

    [Fact]
    public void AddJauntyInterceptor_RegistersConcreteTypeAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddJauntyInterceptor<StubInterceptor>();

        var provider = services.BuildServiceProvider();
        var interceptor = provider.GetService<StubInterceptor>();

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddJauntyInterceptor_TwoResolves_ReturnSameInstance()
    {
        var services = new ServiceCollection();
        services.AddJauntyInterceptor<StubInterceptor>();

        var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredService<StubInterceptor>();
        var b = provider.GetRequiredService<StubInterceptor>();

        Assert.Same(a, b);
    }

    [Fact]
    public void AddJauntyInterceptor_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddJauntyInterceptor<StubInterceptor>();

        Assert.Same(services, returned);
    }

    // ------------------------------------------------------------------
    // ApplyJauntyInterceptors
    // ------------------------------------------------------------------

    [Fact]
    public void ApplyJauntyInterceptors_NullProvider_Throws()
    {
        IServiceProvider? provider = null;
        Assert.Throws<ArgumentNullException>(() => provider!.ApplyJauntyInterceptors());
    }

    [Fact]
    public void ApplyJauntyInterceptors_WithLogging_PipelineHasInterceptors()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddJauntyLogging();

        var provider = services.BuildServiceProvider();
        provider.ApplyJauntyInterceptors();

        Assert.NotNull(JauntyConfig.InterceptorPipeline);
        Assert.True(JauntyConfig.InterceptorPipeline!.HasInterceptors);
    }

    [Fact]
    public void ApplyJauntyInterceptors_WithCustomInterceptor_PipelineHasInterceptors()
    {
        var services = new ServiceCollection();
        services.AddJauntyInterceptor<StubInterceptor>();

        var provider = services.BuildServiceProvider();
        provider.ApplyJauntyInterceptors();

        Assert.NotNull(JauntyConfig.InterceptorPipeline);
        Assert.True(JauntyConfig.InterceptorPipeline!.HasInterceptors);
    }

    [Fact]
    public void ApplyJauntyInterceptors_NoRegistrations_PipelineRemainsNull()
    {
        // No AddJauntyLogging or AddJauntyInterceptor called — nothing to apply
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        provider.ApplyJauntyInterceptors();

        // Pipeline stays null when there were no registrations
        Assert.Null(JauntyConfig.InterceptorPipeline);
    }

    [Fact]
    public void ApplyJauntyInterceptors_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddJauntyInterceptor<StubInterceptor>();
        var provider = services.BuildServiceProvider();

        provider.ApplyJauntyInterceptors();
        var ex = Record.Exception(() => provider.ApplyJauntyInterceptors());

        Assert.Null(ex);
    }

    [Fact]
    public void ApplyJauntyInterceptors_CalledTwice_WithSameProvider_DoesNotDuplicateInterceptorInstance()
    {
        // Regression: ApplyJauntyInterceptors must resolve interceptors from the real,
        // already-built IServiceProvider passed in (not a throwaway container), and must not
        // append the same singleton interceptor instance to JauntyConfig a second time when
        // called more than once with the same provider.
        var services = new ServiceCollection();
        services.AddJauntyInterceptor<StubInterceptor>();
        var provider = services.BuildServiceProvider();

        provider.ApplyJauntyInterceptors();
        provider.ApplyJauntyInterceptors();

        var interceptors = JauntyConfig.InterceptorPipeline!.GetInterceptors().ToList();
        Assert.Single(interceptors);
        Assert.Same(provider.GetRequiredService<StubInterceptor>(), interceptors[0]);
    }

    // ------------------------------------------------------------------
    // Stub interceptor — minimal ICommandInterceptor implementation
    // ------------------------------------------------------------------

    private sealed class StubInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
