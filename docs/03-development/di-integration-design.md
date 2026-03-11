# Dependency Injection Integration Design

**Status:** Draft - Deferred for future discussion
**Created:** 2026-03-11
**Priority:** P0 (Critical for Production Adoption)

---

## Overview

This document explores design options for full Dependency Injection integration in Jaunty. The goal is to provide simple, intuitive registration of `IDbConnection` with automatic interceptor configuration.

---

## Current State

Existing DI extensions (in `JauntyLoggingExtensions.cs`):

```csharp
// Registers LoggingInterceptor
services.AddJauntyLogging(config => { ... });

// Registers custom interceptor
services.AddJauntyInterceptor<AuditInterceptor>();

// Applies interceptors to JauntyConfig (static)
services.ApplyJauntyInterceptors();
// OR
serviceProvider.ApplyJauntyInterceptors();
```

**Problem:** No built-in way to register `IDbConnection`. Users must manually wire up connections:

```csharp
// Current manual approach - verbose and error-prone
services.AddSingleton(sp =>
{
    var conn = new SqlConnection(connectionString);
    sp.ApplyJauntyInterceptors();
    return conn;
});
```

---

## Design Goals

1. **Simple for common case** - Single connection should be one line
2. **Support multiple connections** - Named/keyed connections for multi-database apps
3. **Proper lifetime management** - Scoped connections, Singleton factories
4. **Interceptor integration** - Automatic interceptor application
5. **NativeAOT compatible** - No reflection-based activation
6. **Multi-target support** - Work on .NET Standard 2.0, .NET Framework, and .NET 8+

---

## Option 1: Simple Connection Factory (Recommended)

### API Design

```csharp
// Simple single connection
services.AddJaunty<SqlConnection>("Server=...;Database=...")
        .AddJauntyLogging();

// Named connections
services.AddJaunty(options =>
{
    options.Add<SqlConnection>("default", "conn1");
    options.Add<SqlConnection>("analytics", "conn2");
});

// Usage - inject factory
public class ProductRepository(IDbConnectionFactory factory)
{
    private readonly IDbConnection _db = factory.CreateConnection("default");
}
```

### Implementation Sketch

```csharp
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection(string name = "default");
}

public static class JauntyServiceCollectionExtensions
{
    public static IServiceCollection AddJaunty<TConnection>(
        this IServiceCollection services,
        string connectionString)
        where TConnection : class, IDbConnection, new()
    {
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            sp.ApplyJauntyInterceptors();
            return new ConnectionFactory<TConnection>(connectionString);
        });

        services.AddScoped<IDbConnection>(sp =>
            sp.GetRequiredService<IDbConnectionFactory>().CreateConnection());

        return services;
    }

    public static IServiceCollection AddJaunty(this IServiceCollection services,
        Action<JauntyConnectionOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IDbConnectionFactory, ConnectionFactory>();
        services.AddScoped<IDbConnection>(sp =>
            sp.GetRequiredService<IDbConnectionFactory>().CreateConnection("default"));

        return services;
    }
}
```

### Pros

- Clean, simple API
- Connection factory abstraction (testable)
- Supports multiple named connections
- Proper lifetime (Scoped connections, Singleton factory)

### Cons

- Requires factory abstraction
- Users inject `IDbConnection` not concrete type
- Extra layer of indirection

---

## Option 2: Direct IDbConnection Registration (Keyed Services)

### API Design

```csharp
// .NET 8+ keyed services
services.AddJaunty<SqlConnection>("conn");

// Multiple named connections
services.AddJaunty(options =>
{
    options.Add<SqlConnection>("default", "conn1");
    options.Add<NpgsqlConnection>("analytics", "conn2");
});

// Usage - keyed injection
public class MultiDbRepo(
    [FromKeyedServices("default")] IDbConnection db,
    [FromKeyedServices("analytics")] IDbConnection analytics)
{
}
```

### Implementation Sketch

```csharp
public static class JauntyServiceCollectionExtensions
{
    public static IServiceCollection AddJaunty<TConnection>(
        this IServiceCollection services,
        string connectionString)
        where TConnection : class, IDbConnection, new()
    {
        services.AddSingleton(sp =>
        {
            JauntyConfig.InitializeInterceptors(sp);
            return NullInterceptors.Instance;
        });

        services.AddScoped<IDbConnection>(sp =>
        {
            var conn = new TConnection { ConnectionString = connectionString };
            return conn;
        });

        return services;
    }

    public static IServiceCollection AddJaunty(
        this IServiceCollection services,
        Action<JauntyOptions> configure)
    {
        var options = new JauntyOptions();
        configure(options);

        services.AddSingleton(sp =>
        {
            JauntyConfig.InitializeInterceptors(sp);
            return NullInterceptors.Instance;
        });

        foreach (var connConfig in options.Connections)
        {
            services.AddKeyedScoped<IDbConnection>(connConfig.Name, (sp, key) =>
            {
                var conn = connConfig.Factory(sp);
                return conn;
            });
        }

        return services;
    }
}
```

### Pros

- Minimal abstraction
- Uses built-in keyed services (.NET 8+)
- Clear injection points

### Cons

- `[FromKeyedServices]` couples user code to DI framework
- Doesn't work on .NET Standard 2.0 or .NET Framework 4.7.2
- Requires conditional compilation or multi-target gymnastics

---

## Option 3: Typed Connections (Concrete Type Injection)

### API Design

```csharp
// Register with concrete type
services.AddJaunty<SqlConnection>("conn")
        .AddJauntyLogging();

// Usage - inject concrete type
public class ProductRepository(SqlConnection db) { }
public class AnalyticsService(NpgsqlConnection analytics) { }
```

### Implementation Sketch

```csharp
public static class JauntyServiceCollectionExtensions
{
    public static IServiceCollection AddJaunty<TConnection>(
        this IServiceCollection services,
        string connectionString)
        where TConnection : class, IDbConnection, new()
    {
        services.AddSingleton(sp =>
        {
            JauntyConfig.InitializeInterceptors(sp);
            return NullInterceptors.Instance;
        });

        services.AddScoped<TConnection>(sp =>
        {
            return new TConnection { ConnectionString = connectionString };
        });

        return services;
    }
}
```

### Pros

- Inject concrete connection types
- No factory abstraction needed
- Simplest for single-connection scenarios

### Cons

- Each connection type needs separate registration
- Doesn't scale well for many connections
- No unified factory for dynamic scenarios

---

## Option 4: Polly-Style Builder Pattern

### API Design

```csharp
services.AddJaunty(builder =>
{
    builder.UseConnection<SqlConnection>("default", "conn1")
           .UseConnection<NpgsqlConnection>("analytics", "conn2")
           .UseLogging()
           .UseInterceptor<AuditInterceptor>()
           .UseRetry(retry => retry.WithMaxAttempts(3));
});

// Usage
public class MultiDbRepo(IDbConnectionFactory factory)
{
    IDbConnection Default => factory.Get("default");
    IDbConnection Analytics => factory.Get("analytics");
}
```

### Implementation Sketch

```csharp
public class JauntyBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<ConnectionConfig> _connections = new();
    private readonly List<Action<IServiceProvider>> _configurers = new();

    public JauntyBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public JauntyBuilder UseConnection<TConnection>(
        string name,
        string connectionString)
        where TConnection : class, IDbConnection, new()
    {
        _connections.Add(new ConnectionConfig
        {
            Name = name,
            ConnectionString = connectionString,
            Factory = sp => new TConnection { ConnectionString = connectionString }
        });
        return this;
    }

    public JauntyBuilder UseLogging(Action<LoggingConfiguration>? configure = null)
    {
        _services.AddJauntyLogging(configure);
        return this;
    }

    public JauntyBuilder UseInterceptor<T>() where T : class, ICommandInterceptor
    {
        _services.AddJauntyInterceptor<T>();
        return this;
    }

    public IServiceCollection Build()
    {
        // Apply interceptors at startup
        _services.AddSingleton(sp =>
        {
            JauntyConfig.InitializeInterceptors(sp);
            return NullInterceptors.Instance;
        });

        // Register connection factory
        _services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            return new MultiConnectionFactory(
                _connections.ToDictionary(c => c.Name, c => c.Factory));
        });

        return _services;
    }
}
```

### Pros

- Most expressive API
- Single configuration point
- Easy to extend with new features (retry, caching)

### Cons

- Most complex implementation
- Builder pattern overhead
- More surface area to maintain

---

## Comparison Matrix

| Feature | Option 1: Factory | Option 2: Keyed | Option 3: Typed | Option 4: Builder |
|---------|------------------|-----------------|-----------------|-------------------|
| Simple API | yes | yes | yes | Verbose |
| Multiple connections | yes | yes | Manual | yes |
| .NET Standard 2.0 | yes | no | yes | yes |
| .NET Framework 4.7.2 | yes | no | yes | yes |
| Concrete type injection | no | no | yes | Via factory |
| Testability | Good | Coupled | Best | Good |
| Implementation complexity | Low | Medium | Lowest | High |
| Extensibility | Medium | Medium | Low | High |

---

## Recommended Approach

### Hybrid: Option 1 + Option 3

Provide both patterns for different scenarios:

```csharp
// Scenario 1: Single connection - use typed registration
services.AddJaunty<SqlConnection>("conn")
        .AddJauntyLogging();

public class Repo(SqlConnection db) { }

// Scenario 2: Multiple connections - use factory
services.AddJaunty(options =>
{
    options.Add<SqlConnection>("default", "conn1");
    options.Add<NpgsqlConnection>("analytics", "conn2");
});

public class MultiDbRepo(IDbConnectionFactory factory)
{
    IDbConnection Default => factory.Get("default");
    IDbConnection Analytics => factory.Get("analytics");
}
```

---

## Open Design Questions

### 1. Connection Lifetime

| Lifetime | Recommendation |
|----------|----------------|
| Scoped | Recommended - one connection per request/operation |
| Transient | Possible but creates new connection each time |
| Singleton | Not recommended - connection state issues |

**Decision needed:** Default to Scoped? Allow override?

### 2. Interceptor Timing

| Strategy | Description |
|----------|-------------|
| Eager (at startup) | Apply interceptors when service provider builds |
| Lazy (on first use) | Apply interceptors when first connection opened |

**Recommendation:** Eager at startup for fail-fast behavior.

### 3. Multiple Providers

Should we support mixing database providers in same application?

```csharp
services.AddJaunty<SqlConnection>("default", "...")
        .AddJaunty<NpgsqlConnection>("analytics", "...");
```

**Recommendation:** Yes, but keep it opt-in.

### 4. Connection String Management

How to handle connection strings securely?

```csharp
// Option A: Direct string
services.AddJaunty<SqlConnection>(configuration.GetConnectionString("Default"));

// Option B: Factory with IOptions
services.AddJaunty<SqlConnection>(options =>
    options.ConnectionString = configuration.GetConnectionString("Default"));

// Option C: Named from IConfiguration
services.AddJaunty<SqlConnection>("Default"); // Auto-resolves from IConfiguration
```

**Recommendation:** Support all three patterns.

---

## Implementation Checklist

When we implement this:

- [ ] Create `IDbConnectionFactory` interface
- [ ] Create `JauntyConnectionOptions` class
- [ ] Create `JauntyServiceCollectionExtensions` class
- [ ] Support .NET Standard 2.0 (no keyed services)
- [ ] Support .NET 8+ (keyed services optional)
- [ ] Add XML documentation
- [ ] Add unit tests for DI registration
- [ ] Add integration tests with real connections
- [ ] Update README with usage examples
- [ ] Update feature-gap-analysis.md

---

## Related Documents

- [`../06-releases/feature-gap-analysis.md`](../06-releases/feature-gap-analysis.md) - Feature roadmap
- [`../04-extensions/README.md`](../04-extensions/README.md) - Extension documentation

---

## Decision Log

| Date | Decision | Notes |
|------|----------|-------|
| 2026-03-11 | Deferred | Revisit after other P0 features complete |
