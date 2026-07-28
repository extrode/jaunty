# Feature Specification: Logging and Command Interception

**Branch**: `001-logging-interception`
**Created**: 2026-03-10
**Status**: Draft
**Input**: "Add comprehensive logging and command interception support to Jaunty ORM, including Microsoft.Extensions.Logging integration, configurable interceptors for command execution lifecycle hooks, and structured event emission for observability."

---

## 1. User Scenarios & Testing

### User Story: SQL Execution Logging with ILogger

**Priority**: `P1` (MVP)

**Description**: As a developer, I want Jaunty to log all SQL commands executed with timing and parameter information so that I can debug performance issues and audit database access.

**Why This Priority**: Logging is essential for production debugging, performance analysis, and compliance. Without it, diagnosing slow queries or tracking database access patterns is extremely difficult.

**Independent Test**: Configure ILogger, execute queries, verify log output contains SQL, parameters, and execution time.

**Acceptance Scenarios**:
```gherkin
Given Jaunty is configured with an ILoggerFactory
When a query is executed via Query<T>
Then the SQL command text is logged at Information level
And parameter values are logged (with sensitive data handling)
And execution duration is logged in milliseconds

Given a query takes longer than the configured threshold (e.g., 1000ms)
When the query completes
Then a warning is logged indicating a slow query

Given sensitive parameters (e.g., passwords, tokens)
When parameters are logged
Then sensitive values are masked or omitted
```

### User Story: Command Interception for Cross-Cutting Concerns

**Priority**: `P1` (MVP)

**Description**: As a developer, I want to intercept command execution before and after it runs so that I can implement cross-cutting concerns like audit logging, performance monitoring, or custom telemetry.

**Why This Priority**: Interceptors enable observability, auditing, and custom behavior without modifying Jaunty's core code. This is critical for enterprise scenarios requiring audit trails or custom metrics.

**Independent Test**: Implement a custom interceptor, register it, execute a query, verify interceptor methods were called with correct context.

**Acceptance Scenarios**:
```gherkin
Given a custom interceptor implementing ICommandInterceptor
When a command is executed
Then OnCommandExecuting is called before SQL execution
And OnCommandExecuted is called after SQL execution
And the interceptor receives command text, parameters, and execution duration

Given an interceptor throws an exception in OnCommandExecuting
When a command is executed
Then the command is NOT sent to the database
And the exception propagates to the caller

Given a command fails with a database exception
When the command completes
Then OnCommandFailed is called with the exception details
```

### User Story: Diagnostic Event Subscription

**Priority**: `P2`

**Description**: As a platform engineer, I want Jaunty to emit diagnostic events via DiagnosticSource so that I can integrate with .NET's built-in observability ecosystem (OpenTelemetry, App Insights, etc.).

**Why This Priority**: DiagnosticSource is the .NET standard for telemetry. Supporting it enables seamless integration with enterprise monitoring tools without custom code.

**Independent Test**: Subscribe to Jaunty's DiagnosticSource, execute queries, verify events are received with expected payload.

**Acceptance Scenarios**:
```gherkin
Given an application subscribes to Jaunty's DiagnosticSource
When a command is executed
Then "Jaunty.Command.Executing" event is emitted before execution
And "Jaunty.Command.Executed" event is emitted after execution
And "Jaunty.Command.Failed" event is emitted on failure
And each event includes command text, parameters, duration, and connection info
```

---

## 2. Requirements

### Functional Requirements

- `FR-001`: System MUST integrate with `Microsoft.Extensions.Logging.ILoggerFactory` to create loggers per operation
- `FR-002`: System MUST support registering multiple `ICommandInterceptor` instances executed in registration order
- `FR-003`: System MUST emit diagnostic events via `System.Diagnostics.DiagnosticSource` for all command executions
- `FR-004`: System MUST provide command context including SQL, parameters, connection info, and execution duration to interceptors
- `FR-005`: System MUST support async interceptor methods (`OnCommandExecutingAsync`, `OnCommandExecutedAsync`, `OnCommandFailedAsync`)
- `FR-006`: System MUST allow interceptors to short-circuit execution by throwing or returning a result from `OnCommandExecuting`
- `FR-007`: System MUST log slow queries when execution time exceeds configurable threshold (default: 1000ms)
- `FR-008`: System MUST support parameter value masking for sensitive data (configurable property names to redact)
- `FR-009`: System MUST maintain backward compatibility - existing code without logging/interception MUST continue to work
- `FR-010`: System MUST ensure interceptors cannot inadvertently modify command SQL or parameters (defensive copying or read-only context)

### Non-Functional Requirements

- `NFR-001`: Interceptor overhead MUST NOT exceed 5 microseconds per interceptor when no-op
- `NFR-002`: Logging MUST be async-safe and not block command execution
- `NFR-003`: System MUST support .NET 6+ and .NET Standard 2.0 (with reduced DiagnosticSource features on netstandard2.0)

### Key Entities

| Entity | Description | Key Attributes |
|--------|-------------|----------------|
| `ICommandInterceptor` | Interface for command execution hooks | `OnCommandExecuting`, `OnCommandExecuted`, `OnCommandFailed` |
| `CommandContext` | Context passed to interceptors | `CommandText`, `Parameters`, `Connection`, `Elapsed`, `Exception` |
| `JauntyDiagnosticListener` | DiagnosticSource event emitter | Event names, payload builders |
| `LoggingConfiguration` | Logging-specific settings | `LogLevel`, `SlowQueryThreshold`, `SensitiveParameterNames` |

---

## 3. Success Criteria

- `SC-001`: Logging integration passes all unit tests for ILogger abstraction (95%+ code coverage)
- `SC-002`: Interceptor pipeline executes interceptors in registered order with <10μs overhead per no-op interceptor
- `SC-003`: Diagnostic events are received by subscribers within same execution context (verified via integration tests)
- `SC-004`: Slow query logging triggers correctly at configured threshold (±10% tolerance)
- `SC-005`: Sensitive parameter masking correctly identifies and redacts configured parameter names
- `SC-006`: Backward compatibility verified - existing tests pass without any logging/interceptor configuration
- `SC-007`: Documentation includes setup examples for ILogger, custom interceptors, and DiagnosticSource subscription

---

## Section Summary

| Section | Required | Purpose |
|---------|----------|---------|
| User Scenarios | Yes | User stories + acceptance |
| Requirements | Yes | Functional capabilities |
| Success Criteria | Yes | Measurable outcomes |

---

**Next**: `/plan` → Implementation plan
