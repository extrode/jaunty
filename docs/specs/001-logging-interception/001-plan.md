# Implementation Plan: Logging and Command Interception

**Branch**: `001-logging-interception`
**Date**: 2026-03-10
**Spec**: [spec.md](spec.md)
**Input**: `/specs/001-logging-interception/spec.md`

---

## Summary

**Primary Requirement**: Add comprehensive logging and command interception support to Jaunty ORM, including Microsoft.Extensions.Logging integration, configurable interceptors for command execution lifecycle hooks, and DiagnosticSource event emission.

**Technical Approach**:
1. Create new `Interceptors` folder in `src/Jaunty/Internals/` for interceptor infrastructure
2. Define `ICommandInterceptor` interface and `CommandContext` record in core
3. Implement `InterceptorPipeline` to orchestrate multiple interceptors
4. Add `DiagnosticSource` integration via `JauntyDiagnosticListener`
5. Create `LoggingInterceptor` that wraps `ILogger`
6. Add `JauntyLoggingExtensions` for `IServiceCollection` registration
7. Integrate interceptor pipeline into existing `QueryCore` and `ExecuteNonQueryCore` execution paths
8. Add configuration options via `LoggingConfiguration` class
9. Write comprehensive unit tests for interceptor pipeline and logging
10. Write integration tests with real database execution

---

## Technical Context

| Field | Value |
|-------|-------|
| **Language** | C# (netstandard2.0, net8.0) |
| **Dependencies** | Microsoft.Extensions.Logging.Abstractions (optional), System.Diagnostics.DiagnosticSource |
| **Storage** | N/A (in-memory interceptor chain) |
| **Testing** | xUnit, Moq for ILogger, real DB instances |
| **Platform** | Cross-platform (.NET) |
| **Project Type** | Library |
| **Performance** | <5μs overhead per no-op interceptor |
| **Constraints** | NativeAOT-compatible, backward compatible |

---

## Constitution Check

- [ ] Performance-first upheld (interceptor overhead <5μs)
- [ ] NativeAOT-compatible (no reflection.emit, source-gen friendly)
- [ ] Zero dependencies for core (Logging abstractions as optional)
- [ ] Tests before implementation (TDD approach)

---

## Project Structure

### Documentation (this feature)

```
docs/specs/001-logging-interception/
├── spec.md          # Feature spec
├── plan.md          # This file
├── tasks.md         # Task list
└── research.md      # Technical research
```

### Source Code

```
src/
├── Jaunty/
│   ├── Interceptors/
│   │   ├── ICommandInterceptor.cs
│   │   ├── CommandContext.cs
│   │   ├── InterceptorPipeline.cs
│   │   └── LoggingInterceptor.cs
│   ├── Diagnostics/
│   │   └── JauntyDiagnosticListener.cs
│   ├── Configuration/
│   │   └── LoggingConfiguration.cs
│   └── Internals/
│       └── Read/QueryCore.cs (modified)
│       └── Write/ExecuteNonQueryCore.cs (modified)
└── Jaunty.Extensions.Logging/
    ├── JauntyLoggingExtensions.cs
    └── JauntyLoggerProvider.cs

tests/
└── Jaunty.Tests/
    ├── Interceptors/
    │   ├── InterceptorPipelineTests.cs
    │   ├── CommandContextTests.cs
    │   └── LoggingInterceptorTests.cs
    └── Diagnostics/
        └── JauntyDiagnosticListenerTests.cs
```

---

## Implementation Phases

### Phase 1: Core Infrastructure
1. Define `ICommandInterceptor` interface
2. Create `CommandContext` record/class
3. Implement `InterceptorPipeline` with async support
4. Add interceptor registration to `JauntyConfig`

### Phase 2: DiagnosticSource Integration
5. Create `JauntyDiagnosticListener` wrapper
6. Define event names and payload structures
7. Emit events at execution points (Executing, Executed, Failed)

### Phase 3: Microsoft.Extensions.Logging Integration
8. Create `LoggingConfiguration` with thresholds and masking
9. Implement `LoggingInterceptor` using `ILogger`
10. Add `JauntyLoggingExtensions` for DI registration

### Phase 4: Integration with Execution Paths
11. Modify `QueryCore` to invoke interceptor pipeline
12. Modify `ExecuteNonQueryCore` for write operations
13. Ensure async paths are covered

### Phase 5: Testing & Documentation
14. Write unit tests for all components
15. Write integration tests with real DB
16. Add XML documentation and usage examples

---

## API Design

### Interfaces

```csharp
public interface ICommandInterceptor
{
    ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken ct);
    ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken ct);
    ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken ct);
}
```

### Configuration

```csharp
public sealed class LoggingConfiguration
{
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromSeconds(1);
    public bool LogParameters { get; set; } = true;
    public ISet<string> SensitiveParameterNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
```

### Usage

```csharp
// Registration
services.AddJauntyLogging(config => {
    config.MinimumLogLevel = LogLevel.Debug;
    config.SlowQueryThreshold = TimeSpan.FromMilliseconds(500);
    config.SensitiveParameterNames.Add("Password");
});

// Custom interceptor
public class AuditInterceptor : ICommandInterceptor
{
    public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken ct)
    {
        // Log or modify before execution
        return ValueTask.CompletedTask;
    }
    // ...
}
```

---

**Next**: `/tasks` → Generate task list
