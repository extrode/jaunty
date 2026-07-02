using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty;

/// <summary>
/// Provides extension methods for registering Jaunty logging and interceptors with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// This class enables easy integration of Jaunty's interceptors with
/// <see cref="Microsoft.Extensions.Logging"/>-based applications.
/// </para>
/// <para>
/// For NativeAOT scenarios, register interceptors directly via <see cref="JauntyConfig.AddInterceptor"/>
/// without using these extension methods.
/// </para>
/// </remarks>
public static class JauntyLoggingExtensions
{
    /// <summary>
    /// Adds Jaunty logging to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure logging options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="LoggingInterceptor"/> as a singleton
    /// and adds it to the Jaunty interceptor pipeline when <see cref="ApplyJauntyInterceptors(IServiceProvider)"/> is called.
    /// </para>
    /// <para>
    /// Default configuration:
    /// <list type="bullet">
    /// <item><description>MinimumLogLevel: Information</description></item>
    /// <item><description>SlowQueryThreshold: 1 second</description></item>
    /// <item><description>LogParameters: true</description></item>
    /// <item><description>LogSql: true</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddJauntyLogging(
        this IServiceCollection services,
        Action<LoggingConfiguration>? configure = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        // Register LoggingConfiguration
        var config = new LoggingConfiguration();
        configure?.Invoke(config);

        // Register the logger and interceptor
        services.AddSingleton(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LoggingInterceptor>();
            return new LoggingInterceptor(logger, config);
        });

        // Register the interceptor to be added to JauntyConfig
        services.AddSingleton<IInterceptorRegistration>(new InterceptorRegistration(
            sp => sp.GetRequiredService<LoggingInterceptor>()));

        return services;
    }

    /// <summary>
    /// Adds a custom interceptor to the service collection.
    /// </summary>
    /// <typeparam name="TInterceptor">The type of the custom interceptor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Use this method when you have a custom <see cref="ICommandInterceptor"/> implementation
    /// that you want to register with dependency injection.
    /// </remarks>
    public static IServiceCollection AddJauntyInterceptor<
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TInterceptor>(
        this IServiceCollection services)
        where TInterceptor : class, ICommandInterceptor
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddSingleton<TInterceptor>();
        services.AddSingleton<IInterceptorRegistration>(new InterceptorRegistration(
            sp => sp.GetRequiredService<TInterceptor>()));

        return services;
    }

    /// <summary>
    /// Applies all registered interceptors to the Jaunty configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Call this method after registering all interceptors to apply them to Jaunty.
    /// Typically called at application startup after building the service provider.
    /// </remarks>
    public static IServiceCollection ApplyJauntyInterceptors(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        // Add a post-configuration action to apply interceptors
        var serviceProvider = services.BuildServiceProvider();
        var registrations = serviceProvider.GetServices<IInterceptorRegistration>();

        foreach (var registration in registrations)
        {
            var interceptor = registration.Resolve(serviceProvider);
            JauntyConfig.AddInterceptor(interceptor);
        }

        return services;
    }

    /// <summary>
    /// Applies all registered interceptors to the Jaunty configuration.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <remarks>
    /// Call this method after building the service provider to apply interceptors to Jaunty.
    /// Typically called at application startup:
    /// <code>
    /// var app = builder.Build();
    /// app.Services.ApplyJauntyInterceptors();
    /// </code>
    /// </remarks>
    public static void ApplyJauntyInterceptors(this IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
            throw new ArgumentNullException(nameof(serviceProvider));

        var registrations = serviceProvider.GetServices<IInterceptorRegistration>();

        foreach (var registration in registrations)
        {
            var interceptor = registration.Resolve(serviceProvider);
            JauntyConfig.AddInterceptor(interceptor);
        }
    }

    /// <summary>
    /// Represents a pending interceptor registration.
    /// </summary>
    private interface IInterceptorRegistration
    {
        ICommandInterceptor Resolve(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// Implementation of interceptor registration.
    /// </summary>
    private sealed class InterceptorRegistration : IInterceptorRegistration
    {
        private readonly Func<IServiceProvider, ICommandInterceptor> _resolver;

        public InterceptorRegistration(Func<IServiceProvider, ICommandInterceptor> resolver)
        {
            _resolver = resolver;
        }

        public ICommandInterceptor Resolve(IServiceProvider serviceProvider) => _resolver(serviceProvider);
    }
}
